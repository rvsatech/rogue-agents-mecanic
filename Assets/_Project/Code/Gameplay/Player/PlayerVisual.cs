using UnityEngine;
using UnityEngine.Rendering;

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
    /// - Parede: SEMPRE ao longo da parede, inclinado para fora (mesmo virando a câmera ou atirando:
    ///   só o tronco gira para a mira, com limite — nunca fica de costas). Pendurado: para a parede.
    /// - Deitado: de barriga para cima, pés para a mira, cabeça levantada. Fica PARADO: só gira (devagar,
    ///   arrastando no chão) quando a mira sai do cone de proneAimFreeAngle na frente dos pés.
    /// - Levantando: senta e agacha no tempo da etapa 1 (proneToCrouchTime).
    /// </summary>
    public sealed class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerWeapons weapons;

        [Header("Rotação do corpo")]
        [Tooltip("Graus por segundo que o corpo gira. Alto = vira quase na hora.")]
        [SerializeField] private float turnSpeed = 540f;

        [Tooltip("Giro durante a cambalhota (bem rápido: o corpo já rola para o lado certo).")]
        [SerializeField] private float rollTurnSpeed = 3000f;

        [Tooltip("Deitado: graus por segundo que o corpo gira arrastando no chão (bem lento).")]
        [SerializeField] private float proneDragTurnSpeed = 60f;

        [Tooltip("Deitado: a mira pode ir até este ângulo para cada lado dos pés sem o corpo se mexer.")]
        [SerializeField] private float proneAimFreeAngle = 50f;

        [Tooltip("Segundos que o corpo continua virado para a mira depois do último ataque.")]
        [SerializeField] private float combatHoldTime = 0.8f;

        [Header("Atirando correndo para trás")]
        [Tooltip("Correndo para longe da mira (ângulo entre a mira e a direção da corrida maior que isto) e atirando " +
                 "com arma de fogo: o corpo continua de frente para onde corre; só a cabeça e o braço da arma viram para trás.")]
        [SerializeField] private float backShotAngle = 120f;

        [Tooltip("Velocidade mínima (m/s) para valer o tiro para trás correndo.")]
        [SerializeField] private float backShotMinSpeed = 1.5f;

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

        [Tooltip("Altura do quadril do modelo agachado (metros).")]
        [SerializeField] private float crouchHipHeight = 0.6f;

        [Tooltip("Segundos para deitar.")]
        [SerializeField] private float proneTransitionTime = 0.25f;

        [Header("Cápsula provisória (se não houver modelo)")]
        [SerializeField] private Transform body;
        [SerializeField] private float bodyWidth = 0.7f;

        private float bodyYaw;
        private float lean;
        private float wallOffset;
        private float hangOffset;
        private float proneWeight;
        private float pivotHipHeight = -1f;
        private bool initialized;
        private Renderer[] ownRenderers;
        private ShadowCastingMode[] ownShadowModes;
        private bool scopedHidden;

        /// <summary>Para onde o corpo está virado (graus).</summary>
        public float BodyYaw => bodyYaw;

        /// <summary>Frente do corpo no plano do chão (não inclui deitar/inclinar).</summary>
        public Vector3 BodyForward => Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.forward;
        public Vector3 BodyRight => Quaternion.Euler(0f, bodyYaw, 0f) * Vector3.right;

        /// <summary>Posição interpolada da cápsula (o que a câmera segue).</summary>
        public Vector3 InterpolatedPosition { get; private set; }

        /// <summary>0 = em pé, 1 = deitado.</summary>
        public float ProneWeight => proneWeight;

        /// <summary>Deitado: até quantos graus de cada lado a mira vai sem girar o corpo.</summary>
        public float ProneAimFreeAngle => proneAimFreeAngle;

        /// <summary>Atirando, mirando, golpeando ou com o gelo na mão: corpo e braços seguem a mira.</summary>
        public bool InCombat { get; private set; }

        /// <summary>
        /// Correndo para trás (em relação à mira) atirando/mirando com arma de fogo: o corpo fica de frente para onde
        /// corre e o animator vira a cabeça e o braço da arma para trás.
        /// </summary>
        public bool BackShooting { get; private set; }

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
            bool gadgetInHand = weapon != null && (weapon.kind == WeaponKind.Gadget || weapon.kind == WeaponKind.Throwable);
            bool attacking = weapons != null && weapons.IsMeleeAttacking;
            InCombat = !handsBusy &&
                       (motor.LastInput.AimHeld || motor.LastInput.FireHeld || recentAction || attacking || gadgetInHand);

            float aimYaw = input != null ? input.Yaw : bodyYaw;
            float targetYaw = bodyYaw;
            float speed = turnSpeed;

            // Correndo para trás atirando: o corpo não vira para a mira (com folga para não ficar trocando).
            Vector3 flatVelocity = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            bool gunInHand = weapon != null && weapon.kind == WeaponKind.Hitscan;
            float moveAimAngle = Mathf.Abs(Mathf.DeltaAngle(aimYaw, YawOf(flatVelocity, aimYaw)));
            bool movingAway = BackShooting
                ? s.HorizontalSpeed > backShotMinSpeed * 0.66f && moveAimAngle > backShotAngle - 20f
                : s.HorizontalSpeed > backShotMinSpeed && moveAimAngle > backShotAngle;
            // Deslizando para longe da mira: mesma pose (cabeça e braço da arma para trás).
            float slideAimAngle = Mathf.Abs(Mathf.DeltaAngle(aimYaw, YawOf(s.SlideDirection, aimYaw)));
            bool slidingAway = s.Mode == MotorMode.Slide &&
                               slideAimAngle > (BackShooting ? backShotAngle - 20f : backShotAngle);
            BackShooting = InCombat && gunInHand && (weapons == null || !weapons.IsScoped) &&
                           ((movingAway && s.Mode == MotorMode.Locomotion && !s.Prone) || slidingAway);

            switch (s.Mode)
            {
                case MotorMode.WallRun:
                    targetYaw = YawOf(s.WallTangent, bodyYaw); // nunca de costas (o tronco gira no animator)
                    break;
                case MotorMode.LedgeHang:
                case MotorMode.LedgeClimb:
                    targetYaw = YawOf(-s.LedgeNormal, bodyYaw);
                    break;
                case MotorMode.Slide:
                    // Deslizando: o corpo segue o slide (pernas travadas nessa direção). Atirando, só o tronco gira
                    // para a mira, com limite; mirando para trás, a cabeça e o braço da arma viram para trás.
                    targetYaw = YawOf(s.SlideDirection, bodyYaw);
                    break;
                case MotorMode.Roll:
                    targetYaw = YawOf(s.RollDirection, bodyYaw);
                    speed = rollTurnSpeed;
                    break;
                default:
                    if (s.Prone)
                    {
                        // Deitado: parado. Só gira (devagar) para a mira não sair do cone na frente dos pés.
                        float offset = Mathf.DeltaAngle(bodyYaw, aimYaw);
                        targetYaw = Mathf.Abs(offset) > proneAimFreeAngle
                            ? aimYaw - Mathf.Sign(offset) * proneAimFreeAngle
                            : bodyYaw;
                        speed = proneDragTurnSpeed;
                    }
                    else if (InCombat && !BackShooting)
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
            // e encosta as costas no chão. Levantando (etapa 1), volta no tempo da etapa.
            float proneTarget = s.Prone ? 1f : 0f;
            float proneRate = dt / Mathf.Max(0.01f, proneTransitionTime);
            if (s.Mode == MotorMode.GetUp && s.GetUpPhase == 1 && motor.Config != null)
            {
                float t = Mathf.Clamp01(s.GetUpPhaseTimer / Mathf.Max(0.01f, motor.Config.proneToCrouchTime));
                proneTarget = 1f - Mathf.SmoothStep(0f, 1f, t);
                proneRate = dt / 0.05f; // acompanha a etapa, suave entre os ticks
            }
            proneWeight = Mathf.MoveTowards(proneWeight, proneTarget, proneRate);

            // Ponto de giro = quadril da pose que está tocando (em pé ou agachado).
            float wantedPivot = s.Crouching && !s.Prone ? crouchHipHeight : hipHeight;
            pivotHipHeight = pivotHipHeight < 0f ? wantedPivot : Mathf.MoveTowards(pivotHipHeight, wantedPivot, 3f * dt);

            Quaternion bodyRotation = Quaternion.Euler(0f, bodyYaw, 0f) * Quaternion.Euler(-90f * proneWeight, 0f, 0f);
            Vector3 hipsWanted = position + Vector3.up * Mathf.Lerp(pivotHipHeight, proneHipHeight, proneWeight);
            Vector3 rootPosition = hipsWanted - bodyRotation * (Vector3.up * pivotHipHeight);

            transform.SetPositionAndRotation(rootPosition, bodyRotation * Quaternion.Euler(0f, 0f, lean));

            // Cápsula provisória (só se não houver modelo).
            if (body != null)
            {
                float height = s.Height;
                body.localPosition = new Vector3(0f, height * 0.5f, 0f);
                body.localScale = new Vector3(bodyWidth, height * 0.5f, bodyWidth);
            }
        }

        /// <summary>
        /// Na luneta (1ª pessoa): o corpo e as armas somem da câmera, mas a sombra continua.
        /// </summary>
        public void SetScopedView(bool hidden)
        {
            if (hidden == scopedHidden) return;
            scopedHidden = hidden;

            if (ownRenderers == null)
            {
                ownRenderers = GetComponentsInChildren<Renderer>(true);
                ownShadowModes = new ShadowCastingMode[ownRenderers.Length];
                for (int i = 0; i < ownRenderers.Length; i++)
                    ownShadowModes[i] = ownRenderers[i] != null ? ownRenderers[i].shadowCastingMode : ShadowCastingMode.On;
            }

            for (int i = 0; i < ownRenderers.Length; i++)
            {
                Renderer r = ownRenderers[i];
                if (r == null) continue;
                // Só fica a sombra de quem já fazia sombra; o resto (clarão do cano e afins) some.
                // forceRenderingOff não mexe no "enabled" que outros scripts usam para ligar/desligar.
                if (ownShadowModes[i] == ShadowCastingMode.Off) r.forceRenderingOff = hidden;
                else r.shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : ownShadowModes[i];
            }
        }

        /// <summary>
        /// O modelo mudou (troca de personagem/skin): esquece a lista de peças que somem na luneta e,
        /// se estiver na luneta agora, esconde as peças novas.
        /// </summary>
        public void RefreshRenderers()
        {
            bool hidden = scopedHidden;
            if (hidden) SetScopedView(false);
            ownRenderers = null;
            ownShadowModes = null;
            if (hidden) SetScopedView(true);
        }

        private static float YawOf(Vector3 direction, float fallback)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return fallback;
            return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }
    }
}
