using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Um passo (pé encostou no chão ou na parede). Vem da animação; usado pelos sons.</summary>
    public struct FootstepInfo
    {
        public Vector3 Position;
        public float Speed;
        public bool Crouching;
        public bool OnWall;
    }
}
