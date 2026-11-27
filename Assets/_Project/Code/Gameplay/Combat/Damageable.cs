using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Alvo de treino com vida. Pisca branco ao ser atingido, some ao zerar a vida e
    /// volta depois de um tempo.
    /// </summary>
    public sealed class Damageable : MonoBehaviour, IDamageable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float respawnDelay = 2f;
        [SerializeField] private Color flashColor = Color.white;

        private Renderer cachedRenderer;
        private Collider cachedCollider;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor;
        private float flashTimer;

        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsAlive => Health > 0f;
        public HitSurface Surface => HitSurface.Target;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            cachedCollider = GetComponent<Collider>();
            propertyBlock = new MaterialPropertyBlock();
            baseColor = cachedRenderer != null && cachedRenderer.sharedMaterial != null
                ? cachedRenderer.sharedMaterial.color
                : Color.red;
            Health = maxHealth;
        }

        public void TakeDamage(float amount, HitZoneKind zone)
        {
            if (!IsAlive) return;

            Health -= amount;
            flashTimer = 0.08f;

            if (Health <= 0f)
            {
                Health = 0f;
                SetVisible(false);
                Invoke(nameof(Respawn), respawnDelay);
            }
        }

        private void Respawn()
        {
            Health = maxHealth;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (cachedRenderer != null) cachedRenderer.enabled = visible;
            if (cachedCollider != null) cachedCollider.enabled = visible;
        }

        private void Update()
        {
            if (cachedRenderer == null) return;

            flashTimer -= Time.deltaTime;
            Color color = flashTimer > 0f ? flashColor : baseColor;

            // MaterialPropertyBlock: muda a cor só deste objeto sem criar material novo (performance).
            cachedRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorId, color);
            propertyBlock.SetColor(BaseColorId, color);
            cachedRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
