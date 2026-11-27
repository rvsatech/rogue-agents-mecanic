using System;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Uma barreira de gelo já colocada: bloqueia tiros, não pode ser escalada (layer NonClimbable),
    /// tem vida (quebra com tiros) e some sozinha depois de um tempo.
    /// Fica num pool: quando quebra/some, é desativada e reaproveitada na próxima colocação.
    /// </summary>
    public sealed class IceWall : MonoBehaviour, IDamageable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float spawnPopDuration = 0.08f;

        private Renderer[] renderers;
        private MaterialPropertyBlock propertyBlock;
        private Color baseColor = new Color(0.6f, 0.9f, 1f);
        private Color damagedColor = new Color(0.3f, 0.55f, 0.75f);
        private float health;
        private float maxHealth;
        private float expireAt;
        private float spawnTime;
        private float flashTimer;
        private bool alive;

        public bool IsAlive => alive;
        public float Health => health;
        public float MaxHealth => maxHealth;

        /// <summary>Avisa o colocador para devolver a barreira ao pool.</summary>
        public event Action<IceWall> Broken;

        public void Activate(float hitPoints, float lifetimeSeconds, Color color)
        {
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();

            baseColor = color;
            damagedColor = new Color(color.r * 0.5f, color.g * 0.6f, color.b * 0.75f);
            maxHealth = Mathf.Max(1f, hitPoints);
            health = maxHealth;
            spawnTime = Time.time;
            expireAt = Time.time + Mathf.Max(0.1f, lifetimeSeconds);
            flashTimer = 0f;
            alive = true;
            gameObject.SetActive(true);
            UpdateVisual();
        }

        public void TakeDamage(float amount)
        {
            if (!alive) return;
            health -= amount;
            flashTimer = 0.06f;
            if (health <= 0f) Break();
        }

        public void Break()
        {
            if (!alive) return;
            alive = false;
            gameObject.SetActive(false);
            Broken?.Invoke(this);
        }

        private void Update()
        {
            if (!alive) return;

            if (Time.time >= expireAt)
            {
                Break();
                return;
            }

            flashTimer -= Time.deltaTime;
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            // "Pop" de entrada: cresce do chão para cima bem rápido (barreira instantânea).
            float grow = spawnPopDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - spawnTime) / spawnPopDuration);
            transform.localScale = new Vector3(1f, Mathf.Lerp(0.25f, 1f, grow), 1f);

            // Pisca branco ao levar tiro e escurece conforme perde vida.
            Color color = flashTimer > 0f
                ? Color.white
                : Color.Lerp(damagedColor, baseColor, health / maxHealth);

            foreach (Renderer r in renderers)
            {
                r.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(ColorId, color);
                propertyBlock.SetColor(BaseColorId, color);
                r.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
