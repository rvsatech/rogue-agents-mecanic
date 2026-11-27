using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Um acerto de arma (tiro ou faca). Vai no evento PlayerWeapons.Hit para sons e efeitos.</summary>
    public struct WeaponHitInfo
    {
        public WeaponConfig Weapon;
        public Vector3 Point;
        public HitSurface Surface;
        public bool Melee;
    }
}
