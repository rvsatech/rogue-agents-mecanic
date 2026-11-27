using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Flash: pinta a tela de branco POR CIMA de tudo (até do HUD) e deixa surdo — o volume do jogo cai quase a zero
    /// enquanto a tela está branca e volta junto com ela. O zumbido toca por cima (ele não fica abafado).
    /// Fica na câmera (o ScreenEffectsOverlay coloca sozinho).
    /// </summary>
    public sealed class FlashOverlay : MonoBehaviour
    {
        [Tooltip("Volume do jogo com a flash no máximo (0,04 = quase mudo).")]
        [Range(0f, 1f)] [SerializeField] private float deafVolume = 0.04f;

        private static FlashOverlay instance;
        private AudioSource ringSource;
        private bool deafened;

        private void Awake()
        {
            instance = this;
            ringSource = gameObject.AddComponent<AudioSource>();
            ringSource.playOnAwake = false;
            ringSource.spatialBlend = 0f;
            ringSource.ignoreListenerVolume = true; // o zumbido não fica surdo
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (deafened) AudioListener.volume = 1f;
        }

        /// <summary>Zumbido no ouvido de quem ficou cego (toca mesmo com o jogo "surdo").</summary>
        public static void PlayRing(SoundCue cue, float volume)
        {
            if (instance == null || cue == null) return;
            AudioClip clip = cue.Pick();
            if (clip == null) return;

            instance.ringSource.Stop();
            instance.ringSource.clip = clip;
            instance.ringSource.volume = Mathf.Clamp01(volume) * cue.volume;
            instance.ringSource.Play();
        }

        private void Update()
        {
            float flash = ScreenEffects.FlashAmount;
            if (flash > 0.001f)
            {
                AudioListener.volume = Mathf.Lerp(1f, deafVolume, flash);
                deafened = true;
            }
            else if (deafened)
            {
                AudioListener.volume = 1f;
                deafened = false;
            }
        }

        private void OnGUI()
        {
            float flash = ScreenEffects.FlashAmount;
            if (flash <= 0.001f) return;

            GUI.depth = -1000; // por cima de tudo, até do HUD
            Color before = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, flash);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}
