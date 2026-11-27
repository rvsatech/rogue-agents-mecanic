using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Câmera de ombro em 3ª pessoa feita à mão (sem Cinemachine): menos dependências e controle
    /// total do "feel". A mira é sempre o centro da tela.
    /// - Agachar: a câmera desce pouco e suave (não "dá zoom").
    /// - Botão direito: modo mira (mais perto do ombro e FOV menor) — só com rifle/pistola na mão.
    /// - AWP + botão direito: LUNETA — a câmera vai para os olhos do personagem (1ª pessoa), zoom forte
    ///   (scopeFieldOfView da arma), o corpo some (só a sombra fica) e o ScopeOverlay desenha a luneta.
    /// - Mirando, o mouse fica mais lento (aimSensitivity); na luneta, na proporção do zoom.
    /// - Sensação: FOV abre correndo rápido, deslizando e na parede; inclina na parede; afunda e volta (mola)
    ///   ao pousar de uma queda grande; dá um tranco pequeno a cada tiro (não na luneta).
    ///   A inclinação não muda a mira (o centro da tela continua no mesmo lugar).
    /// Roda no LateUpdate, depois que o visual do personagem já se moveu no frame.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField] private CameraConfig config;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;

        [Tooltip("O visual do personagem: a câmera segue a posição interpolada da cápsula.")]
        [SerializeField] private PlayerVisual visual;

        [Tooltip("Usado só se não houver PlayerVisual.")]
        [SerializeField] private Transform target;

        [Tooltip("Armas do jogador: o zoom da mira só funciona com arma de fogo.")]
        [SerializeField] private PlayerWeapons weapons;

        private Camera cam;
        private float pivotHeight = -1f;
        private float aimBlend;
        private float wallRunPull;
        private float scopeBlend;
        private float scopeFieldOfView = 16f;

        // Sensação de movimento.
        private const float DipStiffness = 130f;
        private const float DipDamping = 18f;
        private const float DipImpulse = 27f; // velocidade por metro de afundar (o pico fica ~ landDip)
        private float fovBoost;
        private float roll;
        private float dip;
        private float dipVelocity;
        private float lowestAirSpeed;
        private bool wasGrounded = true;
        private float lastShotSeen = -999f;
        private float shakeTimer;
        private float shakeStrength;
        private Vector3 shakeDirection;
        private const float ShakeTime = 0.1f;

        /// <summary>0 = câmera normal, 1 = na luneta.</summary>
        public float ScopeBlend => scopeBlend;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (weapons == null && motor != null) weapons = motor.GetComponent<PlayerWeapons>();
        }

        private void LateUpdate()
        {
            if (config == null || input == null || (visual == null && target == null)) return;

            float dt = Time.deltaTime;

            // Altura do pivô: em pé ou agachado, mudando devagar.
            float targetPivot = config.standPivotHeight;
            if (motor != null && motor.Config != null)
            {
                MovementConfig m = motor.Config;
                float crouchAmount = Mathf.InverseLerp(m.standHeight, m.crouchHeight, motor.State.Height);
                targetPivot = Mathf.Lerp(config.standPivotHeight, config.crouchPivotHeight, crouchAmount);
                if (visual != null)
                    targetPivot = Mathf.Lerp(targetPivot, config.pronePivotHeight, visual.ProneWeight);
            }
            pivotHeight = pivotHeight < 0f
                ? targetPivot
                : Mathf.MoveTowards(pivotHeight, targetPivot, config.pivotHeightSpeed * dt);

            // Mira (botão direito).
            bool onWall = motor != null && motor.State.Mode == MotorMode.WallRun;
            bool aimBlockedOnWall = onWall && motor.Config != null && !motor.Config.wallRunAllowAim;
            bool aiming = motor != null && motor.LastInput.AimHeld && (weapons == null || weapons.CanAimZoom) &&
                          !aimBlockedOnWall; // na parede: sem zoom
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, config.aimLerpSpeed * dt);
            float distance = Mathf.Lerp(config.distance, config.aimDistance, aimBlend);
            // Na parede a câmera se afasta devagar (e volta devagar ao sair).
            wallRunPull = Mathf.MoveTowards(wallRunPull, onWall ? config.wallRunDistanceBoost : 0f, 2.5f * dt);
            distance += wallRunPull;
            float shoulder = Mathf.Lerp(config.shoulderOffset, config.aimShoulderOffset, aimBlend);
            float fov = Mathf.Lerp(config.fieldOfView, config.aimFieldOfView, aimBlend);

            UpdateFeel(dt, aimBlend, ref fov);

            Vector3 basePosition = visual != null ? visual.InterpolatedPosition : target.position;
            Vector3 pivot = basePosition + Vector3.up * (pivotHeight + dip);
            Quaternion rotation = Quaternion.Euler(input.Pitch, input.Yaw, 0f);
            Vector3 desired = pivot + rotation * new Vector3(shoulder, 0f, -distance);

            // Não deixa a câmera entrar na parede: puxa para frente se tiver algo no caminho.
            Vector3 toDesired = desired - pivot;
            float length = toDesired.magnitude;
            if (length > 0.001f)
            {
                Vector3 direction = toDesired / length;
                if (Physics.SphereCast(pivot, config.collisionRadius, direction, out RaycastHit hit,
                        length, config.collisionMask, QueryTriggerInteraction.Ignore))
                {
                    desired = pivot + direction * Mathf.Max(hit.distance, config.minDistance);
                }
            }

            // Luneta (AWP): vai para os olhos (1ª pessoa) com zoom forte.
            WeaponConfig weapon = weapons != null ? weapons.CurrentConfig : null;
            if (weapon != null && weapon.HasScope) scopeFieldOfView = weapon.scopeFieldOfView;
            bool scoped = weapons != null && weapons.IsScoped;
            scopeBlend = Mathf.MoveTowards(scopeBlend, scoped ? 1f : 0f, config.scopeLerpSpeed * dt);
            if (scopeBlend > 0f)
            {
                float smooth = scopeBlend * scopeBlend * (3f - 2f * scopeBlend);
                desired = Vector3.Lerp(desired, pivot, smooth);
                fov = Mathf.Lerp(fov, scopeFieldOfView, smooth);
            }
            if (visual != null) visual.SetScopedView(scopeBlend > 0.5f);

            // Mouse mais lento mirando (aimSensitivity) e, na luneta, na proporção do zoom.
            float sensitivity = Mathf.Lerp(1f, config.aimSensitivity, aimBlend);
            if (scopeBlend > 0f && config.fieldOfView > 0f)
            {
                float scopeSensitivity = config.scopeSensitivity * scopeFieldOfView / config.fieldOfView;
                sensitivity = Mathf.Lerp(sensitivity, scopeSensitivity, scopeBlend);
            }
            input.LookSensitivityScale = Mathf.Clamp(sensitivity, 0.05f, 1f);

            // Tranco do tiro (some na luneta) e inclinação na parede (só gira em volta do centro da tela).
            if (shakeTimer > 0f)
            {
                float k = Mathf.Clamp01(shakeTimer / ShakeTime);
                desired += rotation * shakeDirection * (shakeStrength * k * k * (1f - scopeBlend));
            }
            Quaternion finalRotation = Quaternion.Euler(input.Pitch, input.Yaw, roll * (1f - scopeBlend));

            transform.SetPositionAndRotation(desired, finalRotation);
            cam.fieldOfView = fov;
        }

        /// <summary>FOV extra (velocidade, slide, parede), inclinação na parede, afundar ao pousar e tranco do tiro.</summary>
        private void UpdateFeel(float dt, float aimBlend, ref float fov)
        {
            if (motor == null) return;
            PlayerSimState s = motor.State;

            // FOV extra: abre um pouco com a velocidade, no slide e na parede (mirando, não).
            float targetBoost = 0f;
            if (s.Mode == MotorMode.Slide) targetBoost = config.slideFovBoost;
            else if (s.Mode == MotorMode.WallRun) targetBoost = config.wallRunFovBoost;
            else if (motor.Config != null)
            {
                float run = Mathf.Max(0.1f, motor.Config.runSpeed);
                targetBoost = config.speedFovBoost * Mathf.InverseLerp(run * 1.02f, run * 1.5f, s.HorizontalSpeed);
            }
            targetBoost *= 1f - aimBlend;
            fovBoost = Mathf.Lerp(fovBoost, targetBoost, 1f - Mathf.Exp(-config.fovBoostSpeed * dt));
            fov += fovBoost;

            // Parede: inclina para longe dela.
            float targetRoll = 0f;
            if (s.Mode == MotorMode.WallRun)
            {
                Vector3 cameraRight = Quaternion.Euler(0f, input.Yaw, 0f) * Vector3.right;
                targetRoll = (Vector3.Dot(-s.WallNormal, cameraRight) >= 0f ? 1f : -1f) * config.wallRunTilt;
            }
            roll = Mathf.MoveTowards(roll, targetRoll, 40f * dt);

            // Pouso: afunda e volta com mola (maior a queda, mais afunda).
            if (!s.Grounded) lowestAirSpeed = Mathf.Min(lowestAirSpeed, s.Velocity.y);
            if (s.Grounded && !wasGrounded && s.Mode == MotorMode.Locomotion)
            {
                float fall = -lowestAirSpeed;
                if (fall > config.landDipMinSpeed)
                {
                    float k = Mathf.InverseLerp(config.landDipMinSpeed, config.landDipMaxSpeed, fall);
                    dipVelocity -= config.landDip * Mathf.Lerp(0.35f, 1f, k) * DipImpulse;
                }
            }
            if (s.Grounded) lowestAirSpeed = 0f;
            wasGrounded = s.Grounded;

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.005f), 1, 12);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                dipVelocity += (-DipStiffness * dip - DipDamping * dipVelocity) * h;
                dip += dipVelocity * h;
            }
            dip = Mathf.Clamp(dip, -0.6f, 0.3f);

            // Tiro: tranco pequeno para trás (mais forte nas armas de mais coice).
            if (weapons != null && weapons.LastShotTime > lastShotSeen)
            {
                lastShotSeen = weapons.LastShotTime;
                WeaponConfig weapon = weapons.CurrentConfig;
                if (weapon != null && weapon.kind == WeaponKind.Hitscan && config.shotShake > 0f)
                {
                    shakeStrength = config.shotShake * (0.006f + weapon.recoilDistance * 0.25f) *
                                    (weapons.IsAiming ? 0.6f : 1f);
                    shakeDirection = (Vector3.back * 0.7f + Random.insideUnitSphere * 0.5f).normalized;
                    shakeTimer = ShakeTime;
                }
            }
            if (shakeTimer > 0f) shakeTimer -= dt;
        }
    }
}
