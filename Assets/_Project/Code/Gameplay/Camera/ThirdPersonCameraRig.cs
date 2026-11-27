using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Câmera de ombro em 3ª pessoa feita à mão (sem Cinemachine): menos dependências e controle
    /// total do "feel". A mira é sempre o centro da tela.
    /// - Agachar: a câmera desce pouco e suave (não "dá zoom").
    /// - Botão direito: modo mira (mais perto do ombro e FOV menor) — só com rifle/pistola na mão.
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
            bool aiming = motor != null && motor.LastInput.AimHeld && (weapons == null || weapons.CanAimZoom);
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, config.aimLerpSpeed * dt);
            float distance = Mathf.Lerp(config.distance, config.aimDistance, aimBlend);
            float shoulder = Mathf.Lerp(config.shoulderOffset, config.aimShoulderOffset, aimBlend);
            float fov = Mathf.Lerp(config.fieldOfView, config.aimFieldOfView, aimBlend);

            Vector3 basePosition = visual != null ? visual.InterpolatedPosition : target.position;
            Vector3 pivot = basePosition + Vector3.up * pivotHeight;
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

            transform.SetPositionAndRotation(desired, rotation);
            cam.fieldOfView = fov;
        }
    }
}
