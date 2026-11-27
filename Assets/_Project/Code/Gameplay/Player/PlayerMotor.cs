using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// "Cérebro" do movimento. Roda em tick fixo (FixedUpdate) e move a cápsula com
    /// CharacterController: o código decide a velocidade exata a cada tick, sem física "solta"
    /// empurrando o personagem. É isso que dá controle preciso e sem delay.
    ///
    /// O motor é dividido por MODO, um arquivo para cada (partial class):
    ///   PlayerMotor.cs              -> este arquivo: ciclo do tick e funções comuns
    ///   PlayerMotor.Locomotion.cs   -> andar, correr, agachar, deitar, pular, air strafe
    ///   PlayerMotor.Slide.cs        -> slide
    ///   PlayerMotor.Roll.cs         -> cambalhota
    ///   PlayerMotor.WallRun.cs      -> correr na parede e salto da parede
    ///   PlayerMotor.Ledge.cs        -> agarrar beirada, ficar pendurado e subir
    ///
    /// A parte visual (boneco e câmera) NÃO fica aqui: ela lê PreviousPosition/CurrentPosition
    /// e o State, e interpola, para ficar suave em qualquer FPS.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public sealed partial class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private MovementConfig config;
        [SerializeField] private PlayerInputReader input;

        private CharacterController controller;
        private PlayerSimState state;

        public MovementConfig Config => config;
        public PlayerSimState State => state;
        public PlayerInputFrame LastInput { get; private set; }

        /// <summary>Posição no começo do último tick (para interpolar o visual).</summary>
        public Vector3 PreviousPosition { get; private set; }

        /// <summary>Posição no fim do último tick.</summary>
        public Vector3 CurrentPosition { get; private set; }

        public float Radius => controller != null ? controller.radius : 0.35f;

        /// <summary>
        /// Multiplicador de velocidade da arma na mão (faca 1.15, pistola 1.10, rifle 1.0).
        /// Quem define é o PlayerWeapons; vale para chão, ar, parede e slide.
        /// </summary>
        public float WeaponSpeedMultiplier { get; set; } = 1f;

        /// <summary>Perda de velocidade correndo no chão enquanto atira (0.2 = -20%). Definido pelo PlayerWeapons.</summary>
        public float FiringGroundPenalty { get; set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (config == null)
            {
                Debug.LogError("[PlayerMotor] Falta o MovementConfig no Inspector.", this);
                enabled = false;
                return;
            }

            Time.fixedDeltaTime = 1f / config.tickRate;
            state = PlayerSimState.Create(config.standHeight);
            ApplyHeight(state.Height);
            PreviousPosition = CurrentPosition = transform.position;
        }

        private void FixedUpdate()
        {
            PlayerInputFrame frame = input != null ? input.ConsumeFrame() : default;
            Simulate(frame, Time.fixedDeltaTime);
        }

        /// <summary>
        /// Um passo da simulação. Mesma entrada + mesmo estado = mesmo resultado.
        /// No online, servidor e cliente chamam esta mesma função.
        /// </summary>
        public void Simulate(in PlayerInputFrame frame, float dt)
        {
            LastInput = frame;
            PreviousPosition = transform.position;

            UpdateTimers(frame, dt);

            switch (state.Mode)
            {
                case MotorMode.Slide: SimulateSlide(frame, dt); break;
                case MotorMode.Roll: SimulateRoll(frame, dt); break;
                case MotorMode.WallRun: SimulateWallRun(frame, dt); break;
                case MotorMode.LedgeHang: SimulateLedgeHang(frame, dt); break;
                case MotorMode.LedgeClimb: SimulateLedgeClimb(frame, dt); break;
                default: SimulateLocomotion(frame, dt); break;
            }

            state.Id = ResolveStateId();
            CurrentPosition = transform.position;
        }

        private void UpdateTimers(in PlayerInputFrame frame, float dt)
        {
            state.JumpBufferTimer = frame.JumpPressed
                ? config.jumpBufferTime
                : Mathf.Max(0f, state.JumpBufferTimer - dt);

            state.TimeSinceGrounded = state.Grounded ? 0f : state.TimeSinceGrounded + dt;
            state.TimeSinceLanded += dt;

            state.SlideCooldown = Mathf.Max(0f, state.SlideCooldown - dt);
            state.RollCooldown = Mathf.Max(0f, state.RollCooldown - dt);
            state.RollBufferTimer = frame.RollPressed
                ? config.rollBufferTime
                : Mathf.Max(0f, state.RollBufferTimer - dt);
            state.WallRunCooldown = Mathf.Max(0f, state.WallRunCooldown - dt);
            state.LedgeCooldown = Mathf.Max(0f, state.LedgeCooldown - dt);
            state.GetUpTimer = Mathf.Max(0f, state.GetUpTimer - dt);

            // A janela do bhop acabou com o pé no chão: perde o bônus.
            if (state.Mode == MotorMode.Locomotion && state.Grounded &&
                state.SpeedMultiplier > 1f && state.TimeSinceLanded > config.bhopWindow)
            {
                state.SpeedMultiplier = 1f;
            }
        }

        /// <summary>Chamado no tick em que o pé encosta no chão.</summary>
        private void OnLanded()
        {
            state.TimeSinceLanded = 0f;
            state.JumpedSinceGrounded = false;
            state.HasLastWall = false; // pode usar a mesma parede de novo
        }

        private MovementStateId ResolveStateId()
        {
            switch (state.Mode)
            {
                case MotorMode.Slide: return MovementStateId.Sliding;
                case MotorMode.Roll: return MovementStateId.Rolling;
                case MotorMode.WallRun: return MovementStateId.WallRunning;
                case MotorMode.LedgeHang: return MovementStateId.LedgeHanging;
                case MotorMode.LedgeClimb: return MovementStateId.Climbing;
            }

            if (!state.Grounded) return MovementStateId.Airborne;
            if (state.Prone) return MovementStateId.Prone;
            if (state.Crouching) return MovementStateId.Crouching;
            return state.HorizontalSpeed > 0.2f ? MovementStateId.Running : MovementStateId.Idle;
        }

        // ================= Funções comuns =================

        /// <summary>Direção desejada pelas teclas, relativa para onde a câmera olha.</summary>
        private static Vector3 WishDirection(in PlayerInputFrame frame)
        {
            Vector3 local = new Vector3(frame.Move.x, 0f, frame.Move.y);
            if (local.sqrMagnitude > 1f) local.Normalize();
            return Quaternion.Euler(0f, frame.Yaw, 0f) * local;
        }

        /// <summary>Frente da câmera no plano do chão.</summary>
        private static Vector3 YawForward(in PlayerInputFrame frame)
        {
            return Quaternion.Euler(0f, frame.Yaw, 0f) * Vector3.forward;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private bool IsNonClimbable(Collider collider)
        {
            return collider != null && (config.nonClimbableMask.value & (1 << collider.gameObject.layer)) != 0;
        }

        /// <summary>
        /// Depois de levantar do chão: começa devagar (getUpStartSpeedFactor) e volta ao normal
        /// em getUpRecoveryTime (2 s). 1 = velocidade normal.
        /// </summary>
        public float GetUpSpeedFactor
        {
            get
            {
                if (config == null || state.GetUpTimer <= 0f || config.getUpRecoveryTime <= 0f) return 1f;
                float recovered = 1f - state.GetUpTimer / config.getUpRecoveryTime;
                return Mathf.Lerp(config.getUpStartSpeedFactor, 1f, recovered);
            }
        }

        private bool IsNoWallRun(Collider collider)
        {
            return collider != null && (config.noWallRunMask.value & (1 << collider.gameObject.layer)) != 0;
        }

        private void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>Muda a altura da cápsula aos poucos; só cresce se tiver espaço acima.</summary>
        private void UpdateHeight(float targetHeight, float dt)
        {
            if (targetHeight > state.Height && !HasHeadroom(targetHeight))
                targetHeight = state.Height;

            if (Mathf.Approximately(state.Height, targetHeight)) return;

            state.Height = Mathf.MoveTowards(state.Height, targetHeight, config.heightChangeSpeed * dt);
            ApplyHeight(state.Height);
        }

        /// <summary>Tem espaço acima para a cápsula ficar com essa altura?</summary>
        private bool HasHeadroom(float height)
        {
            float radius = controller.radius * 0.95f;
            Vector3 pos = transform.position;
            Vector3 bottom = pos + Vector3.up * (radius + 0.1f);
            Vector3 top = pos + Vector3.up * Mathf.Max(radius + 0.1f, height - radius);
            return !Physics.CheckCapsule(bottom, top, radius, config.environmentMask, QueryTriggerInteraction.Ignore);
        }
    }
}
