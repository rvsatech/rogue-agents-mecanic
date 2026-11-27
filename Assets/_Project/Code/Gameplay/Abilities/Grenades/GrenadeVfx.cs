using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Efeitos visuais das granadas feitos por código (sem prefab): bolas de fogo e fumaça com partículas e uma luz
    /// que apaga rapidinho. As partículas usam os materiais do WeaponConfig (o menu Rogue cria e liga).
    /// </summary>
    public static class GrenadeVfx
    {
        /// <summary>Granada: fogo laranja rápido + fumaça escura subindo + luz.</summary>
        public static void Explosion(Vector3 center, WeaponConfig cfg)
        {
            float scale = Mathf.Max(0.5f, cfg.effectRadius / 2.2f);
            var root = new GameObject("Explosao");
            root.transform.position = center;

            if (cfg.glowMaterial != null)
            {
                ParticleSystem fire = CreateSystem(Child(root, "Fogo"), cfg.glowMaterial, 0.6f, 32, 0.02f, 0.6f, 0.45f, 1.35f);
                var emit = new ParticleSystem.EmitParams();
                for (int i = 0; i < 14; i++)
                {
                    Vector3 direction = Random.insideUnitSphere.normalized;
                    emit.position = center + Vector3.up * 0.3f + Random.insideUnitSphere * (0.5f * scale);
                    emit.velocity = (direction * Random.Range(1.5f, 5f) + Vector3.up * 1.2f) * scale;
                    emit.startSize = Random.Range(1.2f, 2.2f) * scale;
                    emit.startLifetime = Random.Range(0.25f, 0.45f);
                    emit.rotation = Random.Range(0f, 360f);
                    emit.startColor = new Color(1f, Random.Range(0.45f, 0.7f), 0.15f, 1f);
                    fire.Emit(emit, 1);
                }
            }

            if (cfg.particleMaterial != null)
            {
                ParticleSystem smoke = CreateSystem(Child(root, "Fumaca"), cfg.particleMaterial, 2.2f, 32, 0.05f, 0.5f, 0.6f, 1.9f);
                var emit = new ParticleSystem.EmitParams();
                for (int i = 0; i < 16; i++)
                {
                    Vector3 direction = Random.insideUnitSphere.normalized;
                    emit.position = center + Vector3.up * 0.4f + Random.insideUnitSphere * (0.7f * scale);
                    emit.velocity = (direction * Random.Range(0.5f, 2f) + Vector3.up * Random.Range(0.6f, 1.6f)) * scale;
                    emit.startSize = Random.Range(1f, 1.8f) * scale;
                    emit.startLifetime = Random.Range(1.2f, 2f);
                    emit.rotation = Random.Range(0f, 360f);
                    emit.angularVelocity = Random.Range(-30f, 30f);
                    float shade = Random.Range(0.18f, 0.3f);
                    emit.startColor = new Color(shade, shade, shade, 0.85f);
                    smoke.Emit(emit, 1);
                }
            }

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 10f * scale;
            light.intensity = 6f;
            light.shadows = LightShadows.None;
            root.AddComponent<EffectLifetime>().Init(2.5f, light, 0.3f);
        }

        /// <summary>Flash: clarão branco bem rápido + luz forte.</summary>
        public static void Flash(Vector3 position, WeaponConfig cfg)
        {
            var root = new GameObject("Clarao");
            root.transform.position = position;

            if (cfg.glowMaterial != null)
            {
                ParticleSystem glow = CreateSystem(Child(root, "Brilho"), cfg.glowMaterial, 0.35f, 8, 0.02f, 0.7f, 0.6f, 1.6f);
                var emit = new ParticleSystem.EmitParams();
                for (int i = 0; i < 5; i++)
                {
                    emit.position = position + Vector3.up * 0.15f + Random.insideUnitSphere * 0.2f;
                    emit.startSize = Random.Range(2.5f, 3.5f);
                    emit.startLifetime = Random.Range(0.15f, 0.25f);
                    emit.rotation = Random.Range(0f, 360f);
                    emit.startColor = new Color(1f, 1f, 0.95f, 1f);
                    glow.Emit(emit, 1);
                }
            }

            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 1f, 0.95f);
            light.range = 15f;
            light.intensity = 8f;
            light.shadows = LightShadows.None;
            root.AddComponent<EffectLifetime>().Init(0.6f, light, 0.2f);
        }

        /// <summary>
        /// Sistema de partículas pronto para receber partículas uma a uma (Emit): sem emissão sozinha, some e
        /// aparece pela vida da partícula (fadeIn/fadeOut = fração da vida) e muda de tamanho ao longo da vida.
        /// </summary>
        public static ParticleSystem CreateSystem(GameObject host, Material material, float lifetime, int maxParticles,
            float fadeIn, float fadeOut, float startSize, float endSize)
        {
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            fadeIn = Mathf.Clamp(fadeIn, 0.01f, 0.45f);
            fadeOut = Mathf.Clamp(fadeOut, 0.01f, 0.95f - fadeIn);

            ParticleSystem.MainModule main = system.main;
            main.duration = Mathf.Max(0.05f, lifetime);
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = lifetime;
            main.startSpeed = 0f;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var alpha = new Gradient();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn),
                    new GradientAlphaKey(1f, 1f - fadeOut), new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(alpha);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, startSize), new Keyframe(fadeIn, 1f), new Keyframe(1f, endSize)));

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 4f; // pode cobrir a tela toda (câmera dentro da fumaça)
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
