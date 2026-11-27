using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Boneco de treino PARADO, para testar mira, dano e recoil:
    /// - Vida 150 (a mesma que os jogadores vão ter). Cabeça, peito, barriga, braços e pernas são colisores
    ///   com HitZone (o multiplicador de dano vem da arma).
    /// - Ao levar tiro: pisca e faz a reação (Hit_Head na cabeça, Hit_Chest no resto).
    /// - Ficando regenDelay sem levar dano, a vida volta cheia (bom para medir o tempo para matar).
    /// - Ao morrer: animação Death01, para de levar tiro e levanta de novo depois de respawnDelay.
    /// - Guarda o tempo para matar (TTK: do 1º dano até a morte) e avisa no evento Killed.
    /// </summary>
    public sealed class TrainingBot : MonoBehaviour, IDamageable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Todos os bots ativos (para o HUD desenhar as barras de vida sem procurar na cena).</summary>
        public static readonly List<TrainingBot> All = new List<TrainingBot>();

        /// <summary>Um bot morreu (bot, tempo para matar em segundos).</summary>
        public static event Action<TrainingBot, float> Killed;

        [Tooltip("Vida do bot (a mesma que os jogadores vão ter).")]
        [SerializeField] private float botHealth = 150f;

        [Tooltip("Segundos sem levar dano para a vida voltar cheia.")]
        [SerializeField] private float regenDelay = 3f;

        [Tooltip("Segundos caído antes de levantar de novo.")]
        [SerializeField] private float respawnDelay = 2.5f;

        [Tooltip("Altura do topo da cabeça (a barra de vida fica um pouco acima).")]
        [SerializeField] private float height = 1.8f;

        [Header("Modelo e animações")]
        [SerializeField] private Animation animationComponent;
        [SerializeField] private string poseClip = "Idle_Loop";
        [SerializeField] private string hitBodyClip = "Hit_Chest";
        [SerializeField] private string hitHeadClip = "Hit_Head";
        [SerializeField] private string deathClip = "Death01";

        [Tooltip("Agachado: não faz a reação de tiro (as reações são em pé).")]
        [SerializeField] private bool crouched;

        [Tooltip("Colisores de cabeça/corpo/pernas (desligam enquanto está caído).")]
        [SerializeField] private Collider[] hitColliders;

        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = new Color(0.85f, 0.3f, 0.25f);
        private float flashTimer;
        private Color flashColor = Color.white;
        private float lastDamageTime = -999f;
        private float firstDamageTime = -1f;
        private float deathTime;
        private float lastReactionTime = -999f;

        public float Health { get; private set; }
        public float MaxHealth => botHealth;
        public float Height => height;
        public bool IsAlive => Health > 0f;
        public HitSurface Surface => HitSurface.Target;

        /// <summary>Último tempo para matar (s), ou -1.</summary>
        public float LastTimeToKill { get; private set; } = -1f;

        private void Awake()
        {
            Health = botHealth;
            renderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();
            if (renderers.Length > 0 && renderers[0].sharedMaterial != null)
                baseColor = renderers[0].sharedMaterial.color;
        }

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        private void Start()
        {
            PlayPose(0f);
        }

        public void TakeDamage(float amount, HitZoneKind zone)
        {
            if (!IsAlive || amount <= 0f) return;

            if (firstDamageTime < 0f) firstDamageTime = Time.time;
            lastDamageTime = Time.time;
            Health = Mathf.Max(0f, Health - amount);

            flashTimer = 0.07f;
            flashColor = zone == HitZoneKind.Head ? new Color(1f, 0.85f, 0.2f) : Color.white;

            if (Health <= 0f)
            {
                Die();
                return;
            }

            // Reação (não reinicia a cada tiro da rajada, senão treme).
            if (!crouched && Time.time - lastReactionTime > 0.25f)
            {
                lastReactionTime = Time.time;
                PlayOneShot(zone == HitZoneKind.Head ? hitHeadClip : hitBodyClip);
            }
        }

        /// <summary>Vida cheia e em pé na hora (F3 no campo de treino).</summary>
        public void ResetBot()
        {
            Health = botHealth;
            firstDamageTime = -1f;
            LastTimeToKill = -1f;
            SetColliders(true);
            PlayPose(0.05f);
        }

        /// <summary>Levanta e enche a vida de todos os bots.</summary>
        public static void ResetAll()
        {
            for (int i = 0; i < All.Count; i++) All[i].ResetBot();
        }

        private void Die()
        {
            LastTimeToKill = firstDamageTime >= 0f ? Time.time - firstDamageTime : 0f;
            firstDamageTime = -1f;
            deathTime = Time.time;
            SetColliders(false);

            if (!PlayOneShot(deathClip, clamp: true))
                transform.localScale = new Vector3(1f, 0.2f, 1f); // sem animação de morte: "achata"

            Killed?.Invoke(this, LastTimeToKill);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                if (Time.time - deathTime >= respawnDelay)
                {
                    transform.localScale = Vector3.one;
                    ResetBot();
                }
            }
            else if (Health < botHealth && Time.time - lastDamageTime >= regenDelay)
            {
                Health = botHealth; // ficou um tempo sem levar dano: vida cheia
                firstDamageTime = -1f;
            }

            UpdateFlash();
        }

        private void UpdateFlash()
        {
            if (renderers == null) return;
            if (flashTimer <= 0f && flashTimer > -1f)
            {
                flashTimer = -1f; // volta a cor normal uma vez só
                ApplyColor(baseColor);
                return;
            }
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                ApplyColor(flashColor);
            }
        }

        private void ApplyColor(Color color)
        {
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorId, color);
                propertyBlock.SetColor(BaseColorId, color);
                r.SetPropertyBlock(propertyBlock);
            }
        }

        private void SetColliders(bool on)
        {
            if (hitColliders == null) return;
            foreach (Collider c in hitColliders)
                if (c != null) c.enabled = on;
        }

        private void PlayPose(float fade)
        {
            if (animationComponent == null) return;
            AnimationState pose = animationComponent[poseClip];
            if (pose == null) return;
            pose.wrapMode = WrapMode.Loop;
            if (fade > 0f) animationComponent.CrossFade(poseClip, fade);
            else animationComponent.Play(poseClip);
        }

        /// <summary>Toca uma animação uma vez e volta para a pose. Retorna false se ela não existe.</summary>
        private bool PlayOneShot(string clip, bool clamp = false)
        {
            if (animationComponent == null || string.IsNullOrEmpty(clip)) return false;
            AnimationState state = animationComponent[clip];
            if (state == null) return false;

            state.wrapMode = clamp ? WrapMode.ClampForever : WrapMode.Once;
            state.time = 0f;
            animationComponent.CrossFade(clip, 0.05f);
            if (!clamp)
            {
                AnimationState back = animationComponent.CrossFadeQueued(poseClip, 0.15f, QueueMode.CompleteOthers);
                if (back != null) back.wrapMode = WrapMode.Loop;
            }
            return true;
        }
    }
}
