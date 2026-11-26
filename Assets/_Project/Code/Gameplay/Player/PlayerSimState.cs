using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Estados de movimento (aparecem no HUD de debug). Novos estados entram aqui.</summary>
    public enum MovementStateId
    {
        Idle,
        Running,
        Airborne,
        Crouching
    }

    /// <summary>
    /// Tudo o que a simulação precisa lembrar entre um tick e outro.
    /// Fica num struct simples para, no futuro, poder ser enviado pela rede,
    /// salvo em replay ou comparado entre cliente e servidor.
    /// </summary>
    [System.Serializable]
    public struct PlayerSimState
    {
        public MovementStateId Id;
        public Vector3 Velocity;

        public bool Grounded;
        public bool JumpedSinceGrounded;

        /// <summary>Tempo desde que saiu do chão (usado no coyote time).</summary>
        public float TimeSinceGrounded;

        /// <summary>Tempo desde que tocou o chão (usado na janela do bhop).</summary>
        public float TimeSinceLanded;

        /// <summary>Quanto tempo o pulo apertado ainda "vale" (jump buffer).</summary>
        public float JumpBufferTimer;

        /// <summary>1 = normal. 1.15 = bônus de pulo para frente ativo.</summary>
        public float SpeedMultiplier;

        public bool Crouching;

        /// <summary>Altura atual da cápsula (muda ao agachar).</summary>
        public float Height;

        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;

        public static PlayerSimState Create(float height)
        {
            return new PlayerSimState
            {
                Id = MovementStateId.Idle,
                SpeedMultiplier = 1f,
                Height = height,
                TimeSinceLanded = 999f
            };
        }
    }
}
