using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// "Modo" do motor: cada modo tem a sua própria física (arquivo PlayerMotor.&lt;Modo&gt;.cs).
    /// </summary>
    public enum MotorMode
    {
        Locomotion,  // andar, correr, agachar, deitar, pular, cair
        Slide,       // deslizando no chão
        Roll,        // cambalhota
        WallRun,     // correndo na parede
        LedgeHang,   // pendurado na beirada
        LedgeClimb   // subindo a beirada
    }

    /// <summary>Estados de movimento (aparecem no HUD e escolhem a animação).</summary>
    public enum MovementStateId
    {
        Idle,
        Running,
        Airborne,
        Crouching,
        Prone,
        Sliding,
        Rolling,
        WallRunning,
        LedgeHanging,
        Climbing
    }

    /// <summary>
    /// Tudo o que a simulação precisa lembrar entre um tick e outro.
    /// Struct simples para, no futuro, ser enviado pela rede, salvo em replay
    /// ou comparado entre cliente e servidor.
    /// </summary>
    [System.Serializable]
    public struct PlayerSimState
    {
        public MotorMode Mode;
        public MovementStateId Id;
        public Vector3 Velocity;

        public bool Grounded;
        public bool JumpedSinceGrounded;

        /// <summary>Tempo desde que saiu do chão (coyote time).</summary>
        public float TimeSinceGrounded;

        /// <summary>Tempo desde que tocou o chão (janela do bhop).</summary>
        public float TimeSinceLanded;

        /// <summary>Quanto tempo o pulo apertado ainda "vale" (jump buffer).</summary>
        public float JumpBufferTimer;

        /// <summary>1 = normal. 1.15 = bônus de pulo para frente ativo.</summary>
        public float SpeedMultiplier;

        public bool Crouching;

        /// <summary>Deitado de bruços (Ctrl).</summary>
        public bool Prone;

        /// <summary>Altura atual da cápsula.</summary>
        public float Height;

        // ---------- Cambalhota ----------
        public Vector3 RollDirection;
        public float RollTimer;
        public float RollDuration;
        public float RollStartSpeed;
        public float RollEndSpeed;
        public float RollCooldown;
        /// <summary>Alt apertado um pouco antes de tocar o chão ainda vale.</summary>
        public float RollBufferTimer;

        // ---------- Slide ----------
        public Vector3 SlideDirection;
        public float SlideTimer;
        public float SlideDuration;
        public float SlideStartSpeed;
        public float SlideEndSpeed;
        public float SlideCooldown;

        // ---------- Wall run ----------
        /// <summary>Direção que sai da parede (aponta para o jogador).</summary>
        public Vector3 WallNormal;
        /// <summary>Direção da corrida ao longo da parede.</summary>
        public Vector3 WallTangent;
        public float WallRunTimer;
        public float WallRunSpeed;
        public float WallRunCooldown;
        /// <summary>Última parede usada (não pode repetir a mesma antes de tocar o chão).</summary>
        public Vector3 LastWallNormal;
        public bool HasLastWall;

        // ---------- Beirada (pendurar / subir) ----------
        public float LedgeTopY;
        /// <summary>Direção que sai da parede da beirada (aponta para o jogador).</summary>
        public Vector3 LedgeNormal;
        /// <summary>Ponto da parede logo abaixo da beirada.</summary>
        public Vector3 LedgeWallPoint;
        /// <summary>Onde o jogador fica em pé depois de subir.</summary>
        public Vector3 LedgeStandPoint;
        public float LedgeTimer;
        public float LedgeCooldown;
        /// <summary>0 = subindo, 1 = passando por cima da beirada.</summary>
        public int ClimbPhase;
        /// <summary>Obstáculo baixo: sobe direto, sem ficar pendurado.</summary>
        public bool QuickMantle;

        public float HorizontalSpeed => new Vector2(Velocity.x, Velocity.z).magnitude;

        /// <summary>+1 = parede do lado direito da corrida, -1 = lado esquerdo.</summary>
        public float WallSide
        {
            get
            {
                Vector3 right = Vector3.Cross(Vector3.up, WallTangent);
                return Vector3.Dot(-WallNormal, right) >= 0f ? 1f : -1f;
            }
        }

        public static PlayerSimState Create(float height)
        {
            return new PlayerSimState
            {
                Mode = MotorMode.Locomotion,
                Id = MovementStateId.Idle,
                SpeedMultiplier = 1f,
                Height = height,
                TimeSinceLanded = 999f
            };
        }
    }
}
