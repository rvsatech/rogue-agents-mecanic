using System.Collections.Generic;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Nuvem da smoke: cilindro no chão (raio effectRadius = 8,8 m, altura smokeHeight = 5,2 m).
    /// Enche em ~1 s, dura smokeDuration e some nos últimos 2,5 s. As partículas crescem junto com a nuvem.
    /// Tapa a visão com partículas grandes; com a câmera dentro, a tela fica cinza (ScreenEffectsOverlay).
    /// Tiro atravessa (fumaça não é parede).
    /// </summary>
    public sealed class SmokeCloud : MonoBehaviour
    {
        private const float FadeInTime = 1.2f;
        private const float FadeOutTime = 2.5f;


        private static readonly List<SmokeCloud> Active = new List<SmokeCloud>();

        private float radius = 2.2f;
        private float height = 2.6f;
        private float duration = 15f;
        private float edgeSoftness = 0.6f;
        private float startTime;

        public static SmokeCloud Spawn(Vector3 groundPoint, WeaponConfig cfg)
        {
            var go = new GameObject("Smoke (nuvem)");
            go.transform.position = groundPoint;
            var cloud = go.AddComponent<SmokeCloud>();
            cloud.Setup(cfg);
            return cloud;
        }

        /// <summary>Maior densidade de fumaça neste ponto, somando todas as nuvens (0 = limpo, 1 = dentro).</summary>
        public static float MaxDensityAt(Vector3 point)
        {
            float best = 0f;
            for (int i = 0; i < Active.Count; i++) best = Mathf.Max(best, Active[i].DensityAt(point));
            return best;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        private void Update()
        {
            if (Time.time - startTime >= duration) Destroy(gameObject);
        }

        private void Setup(WeaponConfig cfg)
        {
            radius = Mathf.Max(0.3f, cfg.effectRadius);
            height = Mathf.Max(0.5f, cfg.smokeHeight);
            duration = Mathf.Max(1f, cfg.smokeDuration);
            edgeSoftness = Mathf.Clamp(radius * 0.15f, 0.6f, 2f);
            startTime = Time.time;
            if (cfg.particleMaterial == null) return;

            float fadeIn = FadeInTime / duration;
            float fadeOut = FadeOutTime / duration;
            ParticleSystem system = GrenadeVfx.CreateSystem(gameObject, cfg.particleMaterial, duration, 256,
                fadeIn, fadeOut, 0.35f, 1.12f);

            // Partículas do tamanho da nuvem; quantidade para um olhar atravessar umas 10 no meio (bem fechada).
            float size = Mathf.Clamp(Mathf.Min(radius, height) * 0.75f, 1.6f, 4f);
            const float layers = 10f;
            int count = Mathf.Clamp(Mathf.RoundToInt(2f * layers * radius * height / (size * size)), 40, 250);
            float margin = size * 0.35f;
            float inner = Mathf.Max(0.1f, radius - margin);
            float low = margin;
            float high = Mathf.Max(low + 0.1f, height - margin);
            Vector3 basePosition = transform.position;

            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Mathf.Sqrt(Random.value) * inner;
                emit.position = basePosition + new Vector3(Mathf.Cos(angle) * distance, Mathf.Lerp(low, high, Random.value),
                    Mathf.Sin(angle) * distance);
                emit.velocity = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(0f, 0.03f), Random.Range(-0.04f, 0.04f));
                emit.startSize = size * Random.Range(0.85f, 1.2f);
                emit.startLifetime = duration * Random.Range(0.93f, 1f);
                emit.rotation = Random.Range(0f, 360f);
                emit.angularVelocity = Random.Range(-6f, 6f);
                float shade = Random.Range(0.7f, 0.85f);
                emit.startColor = new Color(shade, shade, shade, Random.Range(0.8f, 0.95f));
                system.Emit(emit, 1);
            }
        }

        /// <summary>0 = fora da fumaça, 1 = bem dentro (borda macia; sobe no começo e some no fim).</summary>
        public float DensityAt(Vector3 point)
        {
            Vector3 local = point - transform.position;
            if (local.y < -0.3f) return 0f;

            float horizontal = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float side = 1f - Mathf.Clamp01((horizontal - (radius - edgeSoftness)) / edgeSoftness);
            float top = 1f - Mathf.Clamp01((local.y - (height - edgeSoftness)) / edgeSoftness);

            float age = Time.time - startTime;
            float strength = Mathf.Min(Mathf.Clamp01(age / FadeInTime), Mathf.Clamp01((duration - age) / FadeOutTime));
            return side * top * strength;
        }
    }
}
