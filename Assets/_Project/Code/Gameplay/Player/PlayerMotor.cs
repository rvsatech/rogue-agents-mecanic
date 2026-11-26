using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// "Cérebro" do movimento. Roda em tick fixo (FixedUpdate) e move a cápsula
    /// com CharacterController: o código decide a velocidade exata a cada tick,
    /// sem física "solta" empurrando o personagem. É isso que dá controle preciso e sem delay.
    ///
    /// A parte visual (boneco e câmera) NÃO fica aqui: ela lê PreviousPosition/CurrentPosition
    /// e interpola, para ficar suave mesmo com o monitor em 144 Hz e a simulação em 60 Hz.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerMotor : MonoBehaviour
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

            // 1) Relógios internos
            state.JumpBufferTimer = frame.JumpPressed
                ? config.jumpBufferTime
                : Mathf.Max(0f, state.JumpBufferTimer - dt);
            state.TimeSinceGrounded = state.Grounded ? 0f : state.TimeSinceGrounded + dt;
            state.TimeSinceLanded += dt;

            // A janela do bhop acabou com o pé no chão: perde o bônus.
            if (state.Grounded && state.SpeedMultiplier > 1f && state.TimeSinceLanded > config.bhopWindow)
                state.SpeedMultiplier = 1f;

            // 2) Agachar
            UpdateCrouch(frame, dt);

            // 3) Velocidade horizontal (chão ou ar)
            Vector3 wishDir = WishDirection(frame);
            Vector3 flat = new Vector3(state.Velocity.x, 0f, state.Velocity.z);
            float vy = state.Velocity.y;

            flat = state.Grounded ? GroundMove(flat, wishDir, dt) : AirMove(flat, wishDir, dt);

            // 4) Pulo ou gravidade
            bool canJump = state.Grounded ||
                           (!state.JumpedSinceGrounded && state.TimeSinceGrounded <= config.coyoteTime);

            if (state.JumpBufferTimer > 0f && canJump)
                Jump(frame, ref flat, ref vy);
            else if (state.Grounded)
                vy = -config.groundStickForce;
            else
                vy = Mathf.Max(vy - config.gravity * dt, -config.maxFallSpeed);

            // 5) Mover de fato
            Vector3 velocity = new Vector3(flat.x, vy, flat.z);
            CollisionFlags flags = controller.Move(velocity * dt);

            // Bateu a cabeça: para de subir.
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f)
                vy = 0f;

            // Bateu de lado numa parede: fica só com a parte da velocidade que realmente andou
            // (evita "acumular" velocidade contra a parede).
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = (transform.position - PreviousPosition) / dt;
                flat = new Vector3(actual.x, 0f, actual.z);
            }

            // 6) Chão
            bool wasGrounded = state.Grounded;
            state.Grounded = controller.isGrounded && vy <= 0f;

            if (state.Grounded && !wasGrounded)
            {
                state.TimeSinceLanded = 0f;
                state.JumpedSinceGrounded = false;
            }

            state.Velocity = new Vector3(flat.x, vy, flat.z);
            state.Id = ResolveStateId(flat);
            CurrentPosition = transform.position;
        }

        private Vector3 WishDirection(in PlayerInputFrame frame)
        {
            Vector3 local = new Vector3(frame.Move.x, 0f, frame.Move.y);
            if (local.sqrMagnitude > 1f) local.Normalize();
            return Quaternion.Euler(0f, frame.Yaw, 0f) * local;
        }

        private Vector3 GroundMove(Vector3 flat, Vector3 wishDir, float dt)
        {
            float targetSpeed = state.Crouching
                ? config.crouchSpeed
                : config.runSpeed * state.SpeedMultiplier;

            bool hasInput = wishDir.sqrMagnitude > 0.0001f;
            float rate = hasInput ? config.groundAcceleration : config.groundDeceleration;
            return Vector3.MoveTowards(flat, wishDir * targetSpeed, rate * dt);
        }

        /// <summary>
        /// No ar: soltar as teclas NÃO tira velocidade. Segurar uma direção curva a trajetória
        /// (air strafe): A/D + girar o mouse faz o boneco virar no ar.
        /// </summary>
        private Vector3 AirMove(Vector3 flat, Vector3 wishDir, float dt)
        {
            if (wishDir.sqrMagnitude < 0.0001f)
                return flat;

            Vector3 wish = wishDir.normalized;
            float cap = config.runSpeed * state.SpeedMultiplier;
            float speed = flat.magnitude;

            if (speed > 0.1f)
            {
                Vector3 dir = flat / speed;

                if (Vector3.Dot(dir, wish) > -0.3f)
                {
                    // Curva no ar mantendo a velocidade.
                    float maxRadians = config.airSteerDegreesPerSecond * Mathf.Deg2Rad * dt;
                    flat = Vector3.RotateTowards(dir, wish, maxRadians, 0f) * speed;
                }
                else
                {
                    // Segurando para trás: freio aéreo suave.
                    return Vector3.MoveTowards(flat, wish * cap, config.airAcceleration * dt);
                }
            }

            // Abaixo da velocidade de corrida: ganha velocidade até ela (nunca passa disso no ar).
            if (flat.magnitude < cap)
                flat = Vector3.MoveTowards(flat, wish * cap, config.airAcceleration * dt);

            return flat;
        }

        private void Jump(in PlayerInputFrame frame, ref Vector3 flat, ref float vy)
        {
            bool forward = frame.Move.y >= config.forwardInputThreshold;
            float boost = 1f + config.forwardJumpBoost;

            if (forward)
            {
                bool chainAlive = state.SpeedMultiplier > 1f; // ainda dentro da janela do bhop

                if (!chainAlive)
                {
                    state.SpeedMultiplier = boost;
                    flat *= boost;
                }
                else if (config.stackBhop)
                {
                    float next = Mathf.Min(state.SpeedMultiplier * boost, config.maxSpeedMultiplier);
                    flat *= next / state.SpeedMultiplier;
                    state.SpeedMultiplier = next;
                }
                // Senão: mantém a velocidade aumentada (bhop sem acumular).
            }
            else
            {
                // Pulo de lado/para trás não ganha bônus, mas também não perde velocidade.
                state.SpeedMultiplier = 1f;
            }

            vy = Mathf.Sqrt(2f * config.gravity * config.jumpHeight);

            state.JumpBufferTimer = 0f;
            state.JumpedSinceGrounded = true;
            state.Grounded = false;
        }

        private void UpdateCrouch(in PlayerInputFrame frame, float dt)
        {
            if (frame.CrouchHeld)
                state.Crouching = true;
            else if (state.Crouching && HasHeadroomToStand())
                state.Crouching = false;

            float target = state.Crouching ? config.crouchHeight : config.standHeight;
            if (!Mathf.Approximately(state.Height, target))
            {
                state.Height = Mathf.MoveTowards(state.Height, target, config.heightChangeSpeed * dt);
                ApplyHeight(state.Height);
            }
        }

        /// <summary>Tem espaço acima para ficar em pé? (ex.: saindo de um túnel baixo)</summary>
        private bool HasHeadroomToStand()
        {
            float radius = controller.radius * 0.95f;
            Vector3 pos = transform.position;
            Vector3 bottom = pos + Vector3.up * (radius + 0.1f);
            Vector3 top = pos + Vector3.up * (config.standHeight - radius);
            return !Physics.CheckCapsule(bottom, top, radius, config.environmentMask, QueryTriggerInteraction.Ignore);
        }

        private void ApplyHeight(float height)
        {
            controller.height = height;
            controller.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private MovementStateId ResolveStateId(Vector3 flat)
        {
            if (!state.Grounded) return MovementStateId.Airborne;
            if (state.Crouching) return MovementStateId.Crouching;
            return flat.sqrMagnitude > 0.04f ? MovementStateId.Running : MovementStateId.Idle;
        }
    }
}
