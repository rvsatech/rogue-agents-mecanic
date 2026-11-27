using System;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Vida do jogador (150, igual aos bots). Leva dano pelo IDamageable (no futuro: tiros de outros jogadores)
    /// e cura com o kit médico. Não regenera sozinha (a cura é o kit).
    /// Ainda não tem morte/renascer: para testes (cannotDie) a vida para em 1 — a granada e o F4 nunca matam.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 150f;

        [Tooltip("Para testes (ainda não tem morte/renascer): a vida nunca passa de 1.")]
        [SerializeField] private bool cannotDie = true;

        public float Max => maxHealth;
        public float Current { get; private set; }
        /// <summary>0 = sem vida, 1 = cheia.</summary>
        public float Fraction => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsAlive => Current > 0f;
        public bool IsFull => Current >= maxHealth - 0.01f;
        public HitSurface Surface => HitSurface.Target;

        public float LastDamageTime { get; private set; } = -999f;
        public float LastHealTime { get; private set; } = -999f;

        public event Action<float> Damaged;
        public event Action<float> Healed;
        public event Action Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(float amount, HitZoneKind zone)
        {
            if (!IsAlive || amount <= 0f) return;

            Current = Mathf.Max(cannotDie ? Mathf.Min(1f, Current) : 0f, Current - amount);
            LastDamageTime = Time.time;
            Damaged?.Invoke(amount);
            if (!IsAlive) Died?.Invoke();
        }

        /// <summary>Cura até o máximo. Retorna quanto curou de verdade.</summary>
        public float Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return 0f;

            float before = Current;
            Current = Mathf.Min(maxHealth, Current + amount);
            float healed = Current - before;
            if (healed > 0f)
            {
                LastHealTime = Time.time;
                Healed?.Invoke(healed);
            }
            return healed;
        }

        /// <summary>Volta com a vida cheia.</summary>
        public void ResetHealth()
        {
            Current = maxHealth;
        }
    }
}
