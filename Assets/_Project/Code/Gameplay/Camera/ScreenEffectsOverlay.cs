using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Fica na câmera e pinta a tela de cinza quando a câmera está dentro de uma smoke (atrás do HUD: vida e balas
    /// continuam aparecendo). O branco da flash e o "surdo" ficam no FlashOverlay (colocado aqui sozinho).
    /// </summary>
    public sealed class ScreenEffectsOverlay : MonoBehaviour
    {
        [SerializeField] private Color smokeColor = new Color(0.62f, 0.62f, 0.62f, 0.93f);

        private void Awake()
        {
            if (!TryGetComponent(out FlashOverlay _)) gameObject.AddComponent<FlashOverlay>();
        }

        private void OnGUI()
        {
            float smoke = SmokeCloud.MaxDensityAt(transform.position);
            if (smoke <= 0.01f) return;

            GUI.depth = 10; // atrás do HUD
            Color before = GUI.color;
            GUI.color = new Color(smokeColor.r, smokeColor.g, smokeColor.b, smokeColor.a * smoke);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
