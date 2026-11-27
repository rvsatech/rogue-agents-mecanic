using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// O "corpo que você vê". Separado da cápsula de física e interpolado entre os ticks
    /// (a simulação anda em passos de 60 Hz, o visual desliza suave em qualquer FPS).
    ///
    /// Para onde o corpo olha:
    /// - Andando normal: para a DIREÇÃO DO MOVIMENTO (correr para a câmera mostra o rosto).
    /// - Atirando, mirando, golpeando ou com o gelo na mão: para a MIRA; as pernas seguem o movimento
    ///   (ex.: correr de ré atirando para frente).
    /// - Cambalhota: para a direção da cambalhota.
    /// - Parede: ao longo da parede, inclinado para fora. Pendurado: para a parede.
    /// - Deitado: de barriga para cima, pés para a mira, cabeça levantada olhando para frente.
    /// </summary>
    public sealed class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerWeapons weapons;

        [Header("Rotação do corpo")]
        [Tooltip("Graus por segundo que o corpo gira. Alto = vira quase na hora.")]
        [SerializeField] private float turnSpeed = 900f;

        [Tooltip("Giro durante a cambalhota (bem rápido: o corpo já rola para o lado certo).")]
        [SerializeField] private float rollTurnSpeed = 3000f;

        [Tooltip("Giro deitado (mais lento, como no chão de verdade).")]
        [SerializeField] private float proneTurnSpeed = 300f;

        [Tooltip("Segundos que o corpo continua virado para a mira depois do último ataque.")]
        [SerializeField] private float combatHoldTime = 0.8f;

        [Header("Wall run")]
        [SerializeField] private float wallRunLeanAngle = 18f;
        [SerializeField] private float wallRunVisualOffset = 0.12f;

        [Header("Pendurado")]
        [Tooltip("Aproxima o corpo visível da parede quando pendurado (a cápsula de física não encosta tanto).")]
        [SerializeField] private float hangVisualOffset = 0.15f;

        [Header("Deitado")]
        [Tooltip("Altura do quadril do modelo em pé (metros).")]
        [SerializeField] private float hipHeight = 0.9f;

        [Tooltip("Altura do quadril deitado (metade da grossura do corpo).")]
        [SerializeField] private float proneHipHeight = 0.15f;

        [Tooltip("Segundos para deitar/levantar.")]
        [SerializeField] private float proneTransitionTime = 0.25f;

        [Header("Cápsula provisória (se não houver modelo)")]
        [SerializeField] private Transform body;
        [SerializeField] private float bodyWidth = 0.7f;

        private float bodyYaw;
        private float lean;
        private float wallOffset;
        private float hangOffset;
        private float proneWeight;
        private bool initialized;

        /// <summary>Para onde o corpo está virado (graus).</summary>
        public float BodyYaw => bodyYaw;

        /// <summary>Frente do corpo no plano do chão (não inclui deitar/inclinar).</summary>
        public Vector3 BodyForward => Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward;
        public Vector3 BodyRight => Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.right;

        /// <summary>Posição interpolada da cápsula (o que a câmera segue).</summary>
        public Vector3 InterpolatedPosition { get; private set; }

        /// <summary>0 = em pé, 1 = deitado.</summary>
        public float ProneWeight => proneWeight;

        /// <summary>Atirando, mirando, golpeando ou com o gelo na mão: corpo e braços seguem a mira.</summary>
        public bool InCombat { get; private set; }

        private void Update()
        {
            if (motor == null) return;

            float dt = Time.deltaTime;
            PlayerSimState s = motor.State;

            // Posição interpolada entre os dois últimos ticks.
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            Vector3 position = Vector3.Lerp(motor.PreviousPosition, motor.CurrentPosition, alpha);
            InterpolatedPosition = position;

            // Modo combate.
            bool handsBusy = s.Mode == MotorMode.LedgeHang || s.Mode == MotorMode.LedgeClimb || s.Mode == MotorMode.Roll;
            bool recentAction = weapons != null && Time.time - weapons.LastActionTime < combatHoldTime;
            WeaponConfig weapon = weapons != null ? weapons.CurrentConfig : null;
            bool gadgetInHand = weapon != null && weapon.kind == WeaponKind.Gadget;
            bool attacking = weapons != null && weapons.IsMeleeAttacking;
            InCombat = !handsBusy &&
                       (motor.LastInput.AimHeld || motor.LastInput.FireHeld || recentAction || attacking || gadgetInHand);

            float aimYaw = input != null ? input.Yaw : bodyYaw;
            float targetYaw = bodyYaw;
            float speed = turnSpeed;

            switch (s.Mode)
            {
                case MotorMode.WallRun:
                    targetYaw = InCombat ? aimYaw : YawOf(s.WallTangent, bodyYaw);
                    break;
                case MotorMode.LedgeHang:
                case MotorMode.LedgeClimb:
                    targetYaw = YawOf(-s.LedgeNormal, bodyYaw);
                    break;
                case MotorMode.Slide:
                    targetYaw = InCombat ? aimYaw : YawOf(s.SlideDirection, bodyYaw);
                    break;
                case MotorMode.Roll:
                    targetYaw = YawOf(s.RollDirection, bodyYaw);
                    speed = rollTurnSpeed;
                    break;
                default:
                    if (s.Prone)
                    {
                        targetYaw = aimYaw;
                        speed = proneTurnSpeed;
                    }
                    else if (InCombat)
                    {
                        targetYaw = aimYaw;
                    }
                    else if (s.HorizontalSpeed > 0.3f)
                    {
                        targetYaw = YawOf(new Vector3(s.Velocity.x, 0f, s.Velocity.z), bodyYaw);
                    }
                    break;
            }

            if (!initialized)
            {
                bodyYaw = targetYaw;
                initialized = true;
            }
            bodyYaw = Mathf.MoveTowardsAngle(bodyYaw, targetYaw, speed * dt);

            // Inclinação e deslocamento na parede (pés na parede, cabeça para fora).
            float targetLean = 0f;
            float targetOffset = 0f;
            if (s.Mode == MotorMode.WallRun)
            {
                float wallOnRight = Vector3.Dot(-s.WallNormal, BodyRight) >= 0f ? 1f : -1f;
                targetLean = wallOnRight * wallRunLeanAngle; // Z positivo inclina a cabeça para a esquerda
                targetOffset = wallRunVisualOffset;
            }
            lean = Mathf.MoveTowards(lean, targetLean, 150f * dt);
            wallOffset = Mathf.MoveTowards(wallOffset, targetOffset, 1f * dt);
            if (wallOffset > 0f)
                position += -s.WallNormal * wallOffset;

            // Pendurado: corpo visível mais perto da parede (mãos alcançam a beirada).
            hangOffset = Mathf.MoveTowards(hangOffset, s.Mode == MotorMode.LedgeHang ? hangVisualOffset : 0f, 1.5f * dt);
            if (hangOffset > 0f)
                position += -s.LedgeNormal * hangOffset;

            // Deitado: gira o corpo 90° PARA TRÁS em volta do quadril (barriga para cima, pés para a mira)
            // e encosta as costas no chão.
            proneWeight = Mathf.MoveTowards(proneWeight, s.Prone ? 1f : 0f, dt / Mathf.Max(0.01f, proneTransitionTime));
            Quaternion bodyRotation = Quaternion.Euler(0f, bodyYaw, 0f) * Quaternion.Euler(-90f * proneWeight, 0f, 0f);
            Vector3 hipsWanted = position + Vector3.up * Mathf.Lerp(hipHeight, proneHipHeight, proneWeight);
            Vector3 rootPosition = hipsWanted - bodyRotation * (Vector3.up * hipHeight);

            transform.SetPositionAndRotation(rootPosition, bodyRotation * Quaternion.Euler(0f, 0f, lean));

            // Cápsula provisória (só se não houver modelo).
            if (body != null)
            {
                float height = s.Height;
                body.localPosition = new Vector3(0f, height * 0.5f, 0f);
                body.localScale = new Vector3(bodyWidth, height * 0.5f, bodyWidth);
            }
        }

        private static float YawOf(Vector3 direction, float fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return fallback;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }
    }
}
