using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Câmera de ombro em 3ª pessoa feita à mão (sem Cinemachine): menos dependências e
    /// controle total do "feel". A mira é o centro da tela; o tiro vai sair daqui no futuro.
    /// Roda no LateUpdate, depois que o visual do personagem já se moveu no frame.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField] private CameraConfig config;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;

        [Tooltip("O visual interpolado do personagem (não a cápsula de física).")]
        [SerializeField] private Transform target;

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (config == null || input == null || target == null) return;

            float height = motor != null ? motor.State.Height : 1.8f;
            Vector3 pivot = target.position + Vector3.up * (height * config.pivotHeightFactor);

            Quaternion rotation = Quaternion.Euler(input.Pitch, input.Yaw, 0f);
            Vector3 desired = pivot + rotation * new Vector3(config.shoulderOffset, 0f, -config.distance);

            // Não deixa a câmera entrar na parede: puxa para frente se tiver algo no caminho.
            Vector3 toDesired = desired - pivot;
            float distance = toDesired.magnitude;
            if (distance > 0.001f)
            {
                Vector3 direction = toDesired / distance;
                if (Physics.SphereCast(pivot, config.collisionRadius, direction, out RaycastHit hit,
                        distance, config.collisionMask, QueryTriggerInteraction.Ignore))
                {
                    desired = pivot + direction * Mathf.Max(hit.distance, 0.1f);
                }
            }

            transform.SetPositionAndRotation(desired, rotation);
            cam.fieldOfView = config.fieldOfView;
        }
    }
}
