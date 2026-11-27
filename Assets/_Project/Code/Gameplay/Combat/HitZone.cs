using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Parte do corpo atingida. (Novas partes entram no FIM: o número fica salvo na cena.)</summary>
    public enum HitZoneKind
    {
        Body,    // corpo genérico (sem divisão)
        Head,    // cabeça
        Legs,    // pernas
        Chest,   // peito (do peito para cima)
        Stomach, // barriga (parte de baixo do tronco)
        Arms     // braços
    }

    /// <summary>
    /// Marca um colisor como parte do corpo (cabeça, peito, barriga, braços, pernas). O multiplicador de dano
    /// de cada parte vem da ARMA (WeaponConfig). Colisor sem HitZone = corpo (x1).
    /// </summary>
    public sealed class HitZone : MonoBehaviour
    {
        public HitZoneKind kind = HitZoneKind.Body;

        /// <summary>Parte do corpo de um colisor (sem alocação).</summary>
        public static HitZoneKind KindOf(Collider collider)
        {
            return collider != null && collider.TryGetComponent(out HitZone zone) ? zone.kind : HitZoneKind.Body;
        }
    }
}
