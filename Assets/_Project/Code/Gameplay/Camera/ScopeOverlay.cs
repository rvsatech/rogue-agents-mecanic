using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Desenho da luneta da AWP: círculo no meio da tela com o resto preto, cruz fina e um ponto vermelho no centro.
    /// Aparece quando a câmera termina de entrar na luneta (ThirdPersonCameraRig.ScopeBlend).
    /// A textura do círculo é gerada uma vez no começo (sem arquivo de imagem).
    /// </summary>
    public sealed class ScopeOverlay : MonoBehaviour
    {
        [SerializeField] private ThirdPersonCameraRig rig;

        [Tooltip("Tamanho do círculo em relação à altura da tela.")]
        [Range(0.5f, 1f)] [SerializeField] private float circleSize = 0.96f;

        private Texture2D circle;

        private void Awake()
        {
            if (rig == null) rig = GetComponent<ThirdPersonCameraRig>();

            // Preto fora do círculo, transparente dentro (borda suave).
            const int size = 256;
            circle = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((r - 0.96f) / 0.04f);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(alpha * 255f));
                }
            }
            circle.SetPixels32(pixels);
            circle.Apply();
        }

        private void OnDestroy()
        {
            if (circle != null) Destroy(circle);
        }

        private void OnGUI()
        {
            if (rig == null || circle == null || rig.ScopeBlend < 0.95f) return;

            float diameter = Screen.height * circleSize;
            float left = (Screen.width - diameter) * 0.5f;
            float top = (Screen.height - diameter) * 0.5f;
            float centerX = Screen.width * 0.5f;
            float centerY = Screen.height * 0.5f;

            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(left, top, diameter, diameter), circle);

            // Laterais e faixas de cima/baixo pretas.
            GUI.color = Color.black;
            Texture2D white = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(0f, 0f, left + 1f, Screen.height), white);
            GUI.DrawTexture(new Rect(left + diameter - 1f, 0f, left + 2f, Screen.height), white);
            if (top > 0f)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, top + 1f), white);
                GUI.DrawTexture(new Rect(0f, top + diameter - 1f, Screen.width, top + 2f), white);
            }

            // Cruz fina.
            GUI.DrawTexture(new Rect(left, centerY - 0.75f, diameter, 1.5f), white);
            GUI.DrawTexture(new Rect(centerX - 0.75f, top, 1.5f, diameter), white);

            // Ponto vermelho no centro.
            GUI.color = new Color(1f, 0.2f, 0.2f);
            GUI.DrawTexture(new Rect(centerX - 2f, centerY - 2f, 4f, 4f), white);
            GUI.color = previous;
        }
    }
}
