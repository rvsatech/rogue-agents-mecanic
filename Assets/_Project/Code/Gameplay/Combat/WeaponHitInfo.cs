using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Um acerto de arma (tiro ou faca). Vai no evento PlayerWeapons.Hit para sons, efeitos e HUD.</summary>
    public struct WeaponHitInfo
    {
        public WeaponConfig Weapon;
        public Vector3 Point;
        public HitSurface Surface;
        public bool Melee;

        /// <summary>Dano causado (0 se acertou parede/chão).</summary>
        public float Damage;
        public HitZoneKind Zone;
        public bool Headshot => Zone == HitZoneKind.Head && Damage > 0f;

        /// <summary>Esse acerto matou o alvo.</summary>
        public bool Killed;

        /// <summary>O que levou o dano (bot, gelo...), ou null.</summary>
        public Component Target;
    }
}
