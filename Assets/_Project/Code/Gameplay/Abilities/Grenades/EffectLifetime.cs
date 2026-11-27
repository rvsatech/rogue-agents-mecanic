using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Efeito de estouro: apaga a luz rapidinho e se destrói depois de um tempo.</summary>
    public sealed class EffectLifetime : MonoBehaviour
    {
        private float lifetime = 2f;
        private float lightFadeTime = 0.2f;
        private float startTime;
        private float startIntensity;
        private Light fadingLight;

        public void Init(float seconds, Light light, float lightFade)
        {
            lifetime = seconds;
            fadingLight = light;
            lightFadeTime = Mathf.Max(0.01f, lightFade);
            startIntensity = light != null ? light.intensity : 0f;
            startTime = Time.time;
        }

        private void Update()
        {
            float age = Time.time - startTime;
            if (fadingLight != null)
                fadingLight.intensity = startIntensity * Mathf.Clamp01(1f - age / lightFadeTime);
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
