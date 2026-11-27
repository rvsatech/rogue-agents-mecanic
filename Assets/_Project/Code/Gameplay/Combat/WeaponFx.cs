using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Efeitos de tiro feitos por código (sem prefab), todos reaproveitados (nada é criado por tiro):
    /// - Clarão do cano: estrela que gira e muda de tamanho a cada tiro + "língua" de fogo para frente
    ///   (+ jatos para os lados nas armas grandes) + luz rapidinha que ilumina em volta. O clarão anda preso no
    ///   cano (correndo, não fica para trás). Na luneta não aparece (seria na frente do olho).
    /// - Fumacinha saindo do cano, faíscas e cápsulas pulando pela janela da arma (quicam no chão).
    ///   A 12 e a AWP soltam a cápsula na bombeada / no ferrolho, não no tiro.
    /// - Pente vazio caindo no chão na recarga (o animator chama DropMagazine).
    /// Tamanhos e cores por arma ficam no WeaponConfig (muzzleFlashScale, shellLength, shellColor).
    /// </summary>
    public sealed class WeaponFx : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private PlayerMotor motor;

        [Header("Materiais (o menu Rogue cria e liga)")]
        [Tooltip("Clarão do cano (somando luz, textura de estrela).")]
        [SerializeField] private Material flashMaterial;

        [Tooltip("Fumaça (transparente, bolinha macia).")]
        [SerializeField] private Material smokeMaterial;

        [Tooltip("Faíscas (somando luz, bolinha macia).")]
        [SerializeField] private Material sparkMaterial;

        [Tooltip("Cápsulas (aceita a cor de cada partícula: latão ou vermelho).")]
        [SerializeField] private Material shellMaterial;

        [Tooltip("Forma da cápsula (cilindro).")]
        [SerializeField] private Mesh shellMesh;

        [Header("Clarão")]
        [Tooltip("Tamanho do clarão da UMP (m). As outras armas multiplicam por muzzleFlashScale.")]
        [SerializeField] private float flashSize = 0.32f;

        [Tooltip("Quanto tempo o clarão fica na tela (s).")]
        [SerializeField] private float flashLifetime = 0.045f;

        [Header("Luz do tiro")]
        [SerializeField] private Color lightColor = new Color(1f, 0.72f, 0.38f);
        [SerializeField] private float lightIntensity = 3.2f;
        [SerializeField] private float lightRange = 6f;
        [SerializeField] private float lightTime = 0.05f;

        [Header("Cápsulas e pentes")]
        [Tooltip("Com o que as cápsulas quicam (tudo menos o Player).")]
        [SerializeField] private LayerMask shellCollisionMask = ~0;

        [Tooltip("Segundos que a cápsula fica no chão.")]
        [SerializeField] private float shellLifetime = 3f;

        [Tooltip("Segundos que o pente vazio fica no chão.")]
        [SerializeField] private float magazineLifetime = 6f;

        [Tooltip("Máximo de pentes vazios no chão ao mesmo tempo.")]
        [SerializeField] private int maxDroppedMagazines = 6;

        private ParticleSystem flashCore;
        private ParticleSystem flashJet;
        private ParticleSystem smoke;
        private ParticleSystem sparks;
        private ParticleSystem shells;
        private Light shotLight;
        private float lightTimer;
        private float lightPeak;

        private float shellPendingAt = -1f;
        private int shellPendingSlot = -1;
        private readonly Queue<GameObject> droppedMagazines = new Queue<GameObject>();

        private void Awake()
        {
            if (weapons == null) weapons = GetComponent<PlayerWeapons>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (shellMesh == null) shellMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");

            if (flashMaterial != null)
            {
                // Presos no cano (espaço local): o clarão acompanha a arma.
                flashCore = CreateSystem("Clarao_Estrela", flashMaterial, 24, ParticleSystemRenderMode.Billboard,
                    ParticleSystemSimulationSpace.Local);
                SetFade(flashCore, 0.05f, 0.55f, 1.25f, 0.8f);
                flashJet = CreateSystem("Clarao_Jato", flashMaterial, 24, ParticleSystemRenderMode.Stretch,
                    ParticleSystemSimulationSpace.Local);
                SetFade(flashJet, 0.05f, 0.6f, 1f, 0.6f);
                var jetRenderer = flashJet.GetComponent<ParticleSystemRenderer>();
                jetRenderer.lengthScale = 2.6f;
                jetRenderer.velocityScale = 0f;
            }

            if (smokeMaterial != null)
            {
                smoke = CreateSystem("Fumaca_Cano", smokeMaterial, 64, ParticleSystemRenderMode.Billboard);
                SetFade(smoke, 0.1f, 0.75f, 0.35f, 2.4f);
                ParticleSystem.LimitVelocityOverLifetimeModule drag = smoke.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.limit = 0.15f;
                drag.dampen = 0.08f;
            }

            if (sparkMaterial != null)
            {
                sparks = CreateSystem("Faiscas", sparkMaterial, 96, ParticleSystemRenderMode.Stretch);
                SetFade(sparks, 0.02f, 0.5f, 1f, 0.4f);
                ParticleSystem.MainModule main = sparks.main;
                main.gravityModifier = 0.6f;
                var sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
                sparkRenderer.lengthScale = 1f;
                sparkRenderer.velocityScale = 0.018f;
            }

            if (shellMaterial != null && shellMesh != null)
            {
                shells = CreateSystem("Capsulas", shellMaterial, 64, ParticleSystemRenderMode.Mesh);
                var shellRenderer = shells.GetComponent<ParticleSystemRenderer>();
                shellRenderer.mesh = shellMesh;
                shellRenderer.shadowCastingMode = ShadowCastingMode.On;
                shellRenderer.receiveShadows = true;

                ParticleSystem.MainModule main = shells.main;
                main.gravityModifier = 1f;
                main.startSize3D = true;
                main.startRotation3D = true;

                ParticleSystem.CollisionModule collision = shells.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.collidesWith = shellCollisionMask;
                collision.bounce = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                collision.dampen = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
                collision.lifetimeLoss = 0f;
                collision.radiusScale = 0.6f;
                collision.quality = ParticleSystemCollisionQuality.Medium;
                collision.enableDynamicColliders = false;

                // Some no fim da vida (diminui), em vez de sumir de repente.
                ParticleSystem.SizeOverLifetimeModule size = shells.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f,
                    new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));
            }

            var lightGo = new GameObject("Luz_Tiro");
            lightGo.transform.SetParent(transform, false);
            shotLight = lightGo.AddComponent<Light>();
            shotLight.type = LightType.Point;
            shotLight.color = lightColor;
            shotLight.range = lightRange;
            shotLight.intensity = 0f;
            shotLight.shadows = LightShadows.None;
            shotLight.enabled = false;
        }

        private void OnEnable()
        {
            if (weapons != null) weapons.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (weapons != null) weapons.Fired -= OnFired;
        }

        private void Update()
        {
            // Luz do tiro: acende forte e apaga rapidinho.
            if (lightTimer > 0f)
            {
                lightTimer -= Time.deltaTime;
                float k = Mathf.Clamp01(lightTimer / Mathf.Max(0.01f, lightTime));
                shotLight.intensity = lightPeak * k * k;
                if (lightTimer <= 0f) shotLight.enabled = false;
            }

            // Cápsula da bombeada (a 12) ou do ferrolho (AWP): sai um tempo depois do tiro.
            if (shellPendingAt > 0f && Time.time >= shellPendingAt)
            {
                shellPendingAt = -1f;
                if (weapons != null && weapons.CurrentSlot == shellPendingSlot && !weapons.IsEquipping)
                {
                    WeaponBinding binding = weapons.GetSlot(shellPendingSlot);
                    if (binding != null && binding.IsValid) EjectShell(binding);
                }
            }
        }

        private void OnFired(WeaponConfig cfg, Vector3 muzzlePosition)
        {
            if (weapons == null || cfg == null) return;
            WeaponBinding binding = weapons.Current;
            if (binding == null || !binding.IsValid || binding.config != cfg) return;

            Transform muzzle = binding.muzzle != null ? binding.muzzle : binding.model;
            Vector3 forward = muzzle.forward;
            Vector3 position = muzzlePosition;
            float scale = Mathf.Max(0.2f, cfg.muzzleFlashScale);

            // Na luneta a câmera está no olho: clarão, fumaça e faíscas ficariam na frente da mira.
            if (!weapons.IsScoped)
            {
                EmitFlash(muzzle, scale);
                EmitSmoke(position, forward, scale);
                EmitSparks(position, forward, scale, cfg.pellets > 1 ? 9 : Mathf.RoundToInt(3 + 2 * scale));
            }

            // Luz que ilumina o chão e as paredes em volta por um instante (vai junto com o cano).
            shotLight.transform.SetParent(muzzle, false);
            shotLight.transform.localPosition = Vector3.forward * 0.12f;
            shotLight.range = lightRange * Mathf.Sqrt(scale);
            lightPeak = lightIntensity * Mathf.Sqrt(scale) * Random.Range(0.85f, 1.15f);
            shotLight.intensity = lightPeak;
            shotLight.enabled = true;
            lightTimer = lightTime;

            // Cápsula: na hora (UMP, pistola) ou na bombeada/ferrolho (a 12, AWP).
            if (cfg.shellLength > 0f)
            {
                float delay = cfg.ShellEjectDelay;
                if (delay <= 0f)
                {
                    EjectShell(binding);
                }
                else
                {
                    shellPendingAt = Time.time + delay;
                    shellPendingSlot = weapons.CurrentSlot;
                }
            }
        }

        // ================= Clarão, fumaça, faíscas =================

        /// <summary>Clarão preso no cano (posições em relação ao cano: Z = para frente).</summary>
        private void EmitFlash(Transform muzzle, float scale)
        {
            var emit = new ParticleSystem.EmitParams();
            Color warm = new Color(1f, Random.Range(0.78f, 0.9f), Random.Range(0.5f, 0.62f), 1f);

            if (flashCore != null)
            {
                flashCore.transform.SetParent(muzzle, false);
                if (!flashCore.isPlaying) flashCore.Play(); // a arma antiga pode ter sido escondida (sistema parado)

                // Estrela de frente: gira e muda de tamanho a cada tiro (nunca fica igual).
                emit.position = Vector3.forward * (0.03f * scale);
                emit.velocity = Vector3.zero;
                emit.startSize = flashSize * scale * Random.Range(0.8f, 1.2f);
                emit.startLifetime = flashLifetime;
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = warm;
                flashCore.Emit(emit, 1);

                // Miolo branco menor (o centro do clarão fica quase branco).
                emit.startSize *= 0.45f;
                emit.rotation = Random.Range(0f, 360f);
                emit.startColor = new Color(1f, 0.97f, 0.9f, 1f);
                flashCore.Emit(emit, 1);
            }

            if (flashJet != null)
            {
                flashJet.transform.SetParent(muzzle, false);
                if (!flashJet.isPlaying) flashJet.Play();

                // "Língua" de fogo para frente (esticada na direção do tiro).
                emit.position = Vector3.forward * (0.05f * scale);
                emit.startSize = flashSize * 0.42f * scale * Random.Range(0.85f, 1.15f);
                emit.startLifetime = flashLifetime * 0.9f;
                emit.rotation = 0f;
                emit.startColor = warm;
                emit.velocity = Vector3.forward * 0.01f; // só dá a direção do esticado
                flashJet.Emit(emit, 1);

                // Armas grandes: jatos para os lados (freio do cano).
                if (scale >= 1.4f)
                {
                    emit.startSize = flashSize * 0.3f * scale;
                    emit.position = Vector3.zero;
                    emit.velocity = Vector3.right * 0.01f;
                    flashJet.Emit(emit, 1);
                    emit.velocity = Vector3.left * 0.01f;
                    flashJet.Emit(emit, 1);
                }
            }
        }

        private void EmitSmoke(Vector3 position, Vector3 forward, float scale)
        {
            if (smoke == null) return;
            var emit = new ParticleSystem.EmitParams();
            int count = scale >= 1.4f ? 4 : 2;
            for (int i = 0; i < count; i++)
            {
                emit.position = position + forward * Random.Range(0.02f, 0.12f) * scale;
                emit.velocity = forward * Random.Range(0.4f, 1.1f) + Vector3.up * Random.Range(0.15f, 0.45f) +
                                Random.insideUnitSphere * 0.15f;
                emit.startSize = Random.Range(0.12f, 0.2f) * scale;
                emit.startLifetime = Random.Range(0.7f, 1.2f);
                emit.rotation = Random.Range(0f, 360f);
                emit.angularVelocity = Random.Range(-40f, 40f);
                float shade = Random.Range(0.55f, 0.7f);
                emit.startColor = new Color(shade, shade, shade, 0.32f);
                smoke.Emit(emit, 1);
            }
        }

        private void EmitSparks(Vector3 position, Vector3 forward, float scale, int count)
        {
            if (sparks == null) return;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = (forward + Random.insideUnitSphere * 0.35f).normalized;
                emit.position = position + forward * 0.04f;
                emit.velocity = direction * Random.Range(7f, 16f);
                emit.startSize = Random.Range(0.012f, 0.022f) * Mathf.Sqrt(scale);
                emit.startLifetime = Random.Range(0.05f, 0.14f);
                emit.rotation = 0f;
                emit.startColor = new Color(1f, Random.Range(0.6f, 0.85f), 0.3f, 1f);
                sparks.Emit(emit, 1);
            }
        }

        // ================= Cápsulas e pentes =================

        /// <summary>Cápsula pulando pela janela da arma (para a direita e para cima, girando), quica no chão.</summary>
        public void EjectShell(WeaponBinding binding)
        {
            if (shells == null || binding == null || !binding.IsValid) return;
            WeaponConfig cfg = binding.config;
            if (cfg.shellLength <= 0f) return;

            Transform gun = binding.model;
            Vector3 port = binding.ejectionPort != null
                ? binding.ejectionPort.position
                : gun.TransformPoint(new Vector3(0.035f, 0.05f, 0.08f));

            Vector3 body = motor != null ? motor.State.Velocity : Vector3.zero;

            var emit = new ParticleSystem.EmitParams();
            // Cartucho da 12 é grosso; cápsulas de bala são finas (no máximo 12 mm).
            float diameter = cfg.reloadShellInserts > 0 ? 0.021f : Mathf.Min(0.012f, cfg.shellLength * 0.45f);
            emit.position = port;
            emit.velocity = gun.right * Random.Range(1.6f, 2.6f) + gun.up * Random.Range(1.2f, 2.2f) -
                            gun.forward * Random.Range(0f, 0.6f) + body;
            // Cilindro da Unity: 1 de diâmetro e 2 de altura (eixo Y) — o Y é metade do comprimento.
            emit.startSize3D = new Vector3(diameter, cfg.shellLength * 0.5f, diameter);
            emit.startLifetime = shellLifetime;
            emit.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            emit.angularVelocity3D = Random.insideUnitSphere * 900f;
            emit.startColor = cfg.shellColor;
            shells.Emit(emit, 1);
        }

        /// <summary>
        /// Recarga: uma cópia do pente sai da arma e cai no chão com física (quica e rola); some depois de um tempo.
        /// </summary>
        public void DropMagazine(Transform magazine, Vector3 velocity)
        {
            if (magazine == null) return;

            GameObject copy = Instantiate(magazine.gameObject, magazine.position, magazine.rotation);
            copy.name = "Pente_Vazio";
            copy.transform.localScale = magazine.lossyScale;
            copy.layer = gameObject.layer; // camada do jogador: tiros e câmera não batem nele
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.forceRenderingOff = false;
            }

            BoxCollider box = copy.GetComponent<BoxCollider>();
            if (box == null) box = copy.AddComponent<BoxCollider>();
            Rigidbody body = copy.AddComponent<Rigidbody>();
            body.mass = 0.25f;
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (weapons != null && weapons.OwnCollider != null) Physics.IgnoreCollision(box, weapons.OwnCollider);

            Destroy(copy, magazineLifetime);
            droppedMagazines.Enqueue(copy);
            while (droppedMagazines.Count > Mathf.Max(1, maxDroppedMagazines))
            {
                GameObject old = droppedMagazines.Dequeue();
                if (old != null) Destroy(old);
            }
        }

        // ================= Montagem =================

        /// <summary>Sistema que só solta partículas quando o código manda (Emit); roda para sempre parado.</summary>
        private ParticleSystem CreateSystem(string systemName, Material material, int maxParticles, ParticleSystemRenderMode mode,
            ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.World)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.maxParticles = maxParticles;
            main.simulationSpace = space;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = mode;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            system.Play();
            return system;
        }

        /// <summary>Aparece em fadeIn e some em fadeOut (frações da vida); muda de tamanho do começo ao fim.</summary>
        private static void SetFade(ParticleSystem system, float fadeIn, float fadeOut, float startSize, float endSize)
        {
            fadeIn = Mathf.Clamp(fadeIn, 0.01f, 0.45f);
            fadeOut = Mathf.Clamp(fadeOut, 0.01f, 0.95f - fadeIn);

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
        }
    }
}
