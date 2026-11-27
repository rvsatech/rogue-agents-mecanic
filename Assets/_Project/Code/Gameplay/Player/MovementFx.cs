using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Poeira dos movimentos (partículas feitas por código, reaproveitadas):
    /// - Pouso: anel de poeira no pé, maior quanto maior a queda. Pulo: um sopro no chão.
    /// - Slide: rastro de poeira saindo do pé da frente enquanto desliza (mais rápido = mais poeira).
    /// - Cambalhota: poeira no começo e enquanto rola. Deitar: poeira em volta do corpo.
    /// - Parede: poeira no pé a cada passo na parede. Beirada: farelo caindo da borda ao agarrar.
    /// - Correndo rápido: um chute de poeira a cada passo.
    /// Lê o estado do motor (a cada tick, como o som) e os passos do CharacterModelAnimator.
    /// </summary>
    public sealed class MovementFx : MonoBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CharacterModelAnimator modelAnimator;

        [Tooltip("Material da poeira (transparente, bolinha macia).")]
        [SerializeField] private Material dustMaterial;

        [Tooltip("Cor da poeira (concreto).")]
        [SerializeField] private Color dustColor = new Color(0.66f, 0.63f, 0.58f, 0.55f);

        [Header("Pouso")]
        [Tooltip("Queda mínima (m/s para baixo) para levantar poeira ao pousar (pulo normal pousa a ~8,4 m/s).")]
        [SerializeField] private float landMinSpeed = 6f;
        [Tooltip("Queda (m/s) com a poeira máxima.")]
        [SerializeField] private float landMaxSpeed = 16f;

        [Header("Slide e cambalhota")]
        [Tooltip("Partículas por segundo deslizando na velocidade de corrida.")]
        [SerializeField] private float slideDustRate = 34f;
        [SerializeField] private float rollDustRate = 16f;

        [Header("Passos")]
        [Tooltip("Acima desta velocidade (m/s) cada passo chuta um pouco de poeira.")]
        [SerializeField] private float stepDustMinSpeed = 7f;

        private ParticleSystem dust;
        private bool initialized;
        private MotorMode previousMode;
        private bool previousGrounded;
        private bool previousProne;
        private float previousVerticalSpeed;
        private float slideAccumulator;
        private float rollAccumulator;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (dustMaterial != null) dust = CreateDustSystem();
        }

        private void OnEnable()
        {
            if (modelAnimator != null) modelAnimator.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (modelAnimator != null) modelAnimator.Footstep -= OnFootstep;
        }

        private void FixedUpdate()
        {
            if (motor == null || dust == null) return;

            PlayerSimState s = motor.State;
            if (!initialized)
            {
                Remember(s);
                initialized = true;
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 feet = motor.transform.position + Vector3.up * 0.05f;
            Vector3 flatVelocity = new Vector3(s.Velocity.x, 0f, s.Velocity.z);

            // Pouso: quanto maior a queda, mais poeira e mais longe ela vai.
            if (!previousGrounded && s.Grounded && s.Mode == MotorMode.Locomotion)
            {
                float fall = -previousVerticalSpeed;
                if (fall > landMinSpeed)
                {
                    float k = Mathf.InverseLerp(landMinSpeed, landMaxSpeed, fall);
                    Ring(feet, Mathf.RoundToInt(Mathf.Lerp(4f, 16f, k)), Mathf.Lerp(1.2f, 3.2f, k),
                        Mathf.Lerp(0.35f, 0.75f, k), flatVelocity * 0.3f);
                }
            }

            // Pulo do chão: um sopro pequeno.
            if (previousGrounded && !s.Grounded && s.Mode == MotorMode.Locomotion && s.Velocity.y > 3f)
                Ring(feet, 5, 1f, 0.3f, -flatVelocity * 0.1f);

            // Começou uma ação nova.
            if (s.Mode != previousMode)
            {
                switch (s.Mode)
                {
                    case MotorMode.Slide:
                        Ring(feet, 7, 1.6f, 0.4f, flatVelocity * 0.2f);
                        break;
                    case MotorMode.Roll:
                        Ring(feet, 6, 1.3f, 0.4f, flatVelocity * 0.2f);
                        break;
                    case MotorMode.WallRun:
                        Burst(feet - s.WallNormal * 0.3f, 5, s.WallNormal * 1.2f + Vector3.up * 0.4f, 0.35f);
                        break;
                    case MotorMode.LedgeHang:
                    case MotorMode.LedgeClimb:
                        if (previousMode != MotorMode.LedgeHang)
                        {
                            // Farelo caindo da borda onde as mãos agarraram.
                            Vector3 edge = new Vector3(s.LedgeWallPoint.x, s.LedgeTopY, s.LedgeWallPoint.z) +
                                           s.LedgeNormal * 0.05f;
                            Burst(edge, 6, s.LedgeNormal * 0.4f - Vector3.up * 0.3f, 0.22f);
                        }
                        break;
                }
            }

            // Deitou: poeira em volta do corpo.
            if (s.Prone && !previousProne) Ring(feet, 10, 1.2f, 0.45f, Vector3.zero);

            // Deslizando: rastro de poeira no pé da frente.
            if (s.Mode == MotorMode.Slide && s.Grounded)
            {
                float run = motor.Config != null ? Mathf.Max(1f, motor.Config.runSpeed) : 7f;
                slideAccumulator += slideDustRate * Mathf.Clamp01(s.HorizontalSpeed / run) * dt;
                Vector3 direction = flatVelocity.sqrMagnitude > 0.01f ? flatVelocity.normalized : motor.transform.forward;
                while (slideAccumulator >= 1f)
                {
                    slideAccumulator -= 1f;
                    Vector3 side = Vector3.Cross(Vector3.up, direction) * Random.Range(-0.25f, 0.25f);
                    Emit(feet + direction * 0.45f + side, -direction * Random.Range(0.3f, 1f) +
                         Vector3.up * Random.Range(0.3f, 0.8f) + side * 2f, Random.Range(0.25f, 0.5f), Random.Range(0.5f, 0.9f));
                }
            }
            else
            {
                slideAccumulator = 0f;
            }

            // Rolando: um pouco de poeira o tempo todo.
            if (s.Mode == MotorMode.Roll && s.Grounded)
            {
                rollAccumulator += rollDustRate * dt;
                while (rollAccumulator >= 1f)
                {
                    rollAccumulator -= 1f;
                    Emit(feet + Random.insideUnitSphere * 0.25f, Vector3.up * Random.Range(0.2f, 0.6f) - flatVelocity * 0.08f,
                        Random.Range(0.25f, 0.45f), Random.Range(0.5f, 0.8f));
                }
            }
            else
            {
                rollAccumulator = 0f;
            }

            Remember(s);
        }

        private void OnFootstep(FootstepInfo step)
        {
            if (dust == null || motor == null) return;

            if (step.OnWall)
            {
                PlayerSimState s = motor.State;
                Burst(step.Position, 2, s.WallNormal * 0.8f + Vector3.up * 0.2f, 0.22f);
                return;
            }

            if (step.Speed >= stepDustMinSpeed && !step.Crouching)
            {
                Vector3 back = -motor.transform.forward;
                PlayerSimState s = motor.State;
                Vector3 flat = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
                if (flat.sqrMagnitude > 0.01f) back = -flat.normalized;
                Burst(step.Position, 2, back * 0.7f + Vector3.up * 0.3f, 0.22f);
            }
        }

        private void Remember(in PlayerSimState s)
        {
            previousMode = s.Mode;
            previousGrounded = s.Grounded;
            previousProne = s.Prone;
            previousVerticalSpeed = s.Velocity.y;
        }

        // ================= Partículas =================

        /// <summary>Anel de poeira saindo do chão para os lados.</summary>
        private void Ring(Vector3 center, int count, float speed, float size, Vector3 inherit)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / count;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Emit(center + direction * 0.15f, direction * speed * Random.Range(0.7f, 1.2f) + Vector3.up * Random.Range(0.2f, 0.6f) + inherit,
                    size * Random.Range(0.8f, 1.25f), Random.Range(0.6f, 1.1f));
            }
        }

        /// <summary>Punhado de poeira numa direção.</summary>
        private void Burst(Vector3 position, int count, Vector3 velocity, float size)
        {
            for (int i = 0; i < count; i++)
                Emit(position + Random.insideUnitSphere * 0.08f, velocity + Random.insideUnitSphere * 0.4f,
                    size * Random.Range(0.8f, 1.2f), Random.Range(0.45f, 0.8f));
        }

        private void Emit(Vector3 position, Vector3 velocity, float size, float lifetime)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                rotation = Random.Range(0f, 360f),
                angularVelocity = Random.Range(-60f, 60f),
                startColor = dustColor
            };
            dust.Emit(emit, 1);
        }

        private ParticleSystem CreateDustSystem()
        {
            var go = new GameObject("Poeira");
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.maxParticles = 256;
            main.gravityModifier = -0.03f; // sobe bem devagar
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            // Freia: a poeira abre rápido e para no ar.
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 0.25f;
            drag.dampen = 0.1f;

            var alpha = new Gradient();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(alpha);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.2f, 1f), new Keyframe(1f, 2.2f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }
    }
}
