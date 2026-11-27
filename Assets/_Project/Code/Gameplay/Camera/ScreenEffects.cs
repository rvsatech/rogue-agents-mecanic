using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Estado dos efeitos de tela de quem joga neste PC (flash). O ScreenEffectsOverlay desenha.
    /// </summary>
    public static class ScreenEffects
    {
        private static float flashStart = -999f;
        private static float flashHold;
        private static float flashFade = 1f;
        private static float flashPeak;

        /// <summary>
        /// Flash: tela branca com força 0..1 (1 = toda branca) por hold segundos; depois volta ao normal em fade
        /// segundos. Uma flash mais fraca não apaga uma mais forte que ainda está valendo.
        /// </summary>
        public static void Flash(float strength, float hold, float fade)
        {
            strength = Mathf.Clamp01(strength);
            if (strength <= 0f || strength < FlashAmount) return;

            flashPeak = strength;
            flashHold = Mathf.Max(0f, hold);
            flashFade = Mathf.Max(0.05f, fade);
            flashStart = Time.time;
        }

        /// <summary>Quanto de branco na tela agora (0..1).</summary>
        public static float FlashAmount
        {
            get
            {
                float t = Time.time - flashStart;
                if (t < 0f) return 0f;
                if (t <= flashHold) return flashPeak;
                float f = (t - flashHold) / flashFade;
                return f >= 1f ? 0f : flashPeak * (1f - f);
            }
        }
    }
}
