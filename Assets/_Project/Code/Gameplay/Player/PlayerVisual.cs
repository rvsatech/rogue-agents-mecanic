using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// O "corpo que você vê". Fica separado da cápsula de física e é interpolado entre
    /// os dois últimos ticks: a simulação anda em passos de 60 Hz, mas o visual desliza
    /// suave em qualquer taxa de quadros. Sem isso, o boneco "treme" em monitores rápidos.
    /// </summary>
    public sealed class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Cápsula do corpo (escala muda ao agachar).")]
        [SerializeField] private Transform body;

        [Tooltip("Marcador que mostra para onde o boneco está virado.")]
        [SerializeField] private Transform facingMarker;

        [SerializeField] private float bodyWidth = 0.7f;

        private void Update()
        {
            if (motor == null) return;

            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            transform.position = Vector3.Lerp(motor.PreviousPosition, motor.CurrentPosition, alpha);

            // Estilo "torreta": o corpo sempre olha para onde a câmera olha.
            if (input != null)
                transform.rotation = Quaternion.Euler(0f, input.Yaw, 0f);

            float height = motor.State.Height;

            if (body != null)
            {
                // A cápsula padrão da Unity tem 2 m de altura, por isso height * 0.5.
                body.localPosition = new Vector3(0f, height * 0.5f, 0f);
                body.localScale = new Vector3(bodyWidth, height * 0.5f, bodyWidth);
            }

            if (facingMarker != null)
                facingMarker.localPosition = new Vector3(0f, height * 0.8f, bodyWidth * 0.5f);
        }
    }
}
