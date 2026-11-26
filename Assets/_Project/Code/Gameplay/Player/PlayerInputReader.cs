using Rogue.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Lê teclado e mouse (Input System) e monta o PlayerInputFrame que o motor consome.
    /// Os botões de "apertou" ficam guardados (latch) até o próximo tick consumir,
    /// assim nenhum aperto se perde, mesmo com FPS alto e tick fixo.
    /// Por enquanto as teclas estão no código; quando houver menu de rebind,
    /// elas migram para um arquivo .inputactions em Assets/_Project/Input.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Header("Mouse")]
        [Tooltip("Graus girados por pixel de movimento do mouse.")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool invertY;
        [SerializeField] private float minPitch = -70f;
        [SerializeField] private float maxPitch = 80f;

        private InputAction move;
        private InputAction look;
        private InputAction jump;
        private InputAction crouch;
        private InputAction slide;
        private InputAction roll;
        private InputAction fire;
        private InputAction aim;
        private InputAction ability;

        private bool jumpLatch;
        private bool slideLatch;
        private bool rollLatch;
        private bool abilityLatch;

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        private void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            look = new InputAction("Look", InputActionType.PassThrough, "<Mouse>/delta");
            jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
            slide = new InputAction("Slide", InputActionType.Button, "<Keyboard>/c");
            roll = new InputAction("Roll", InputActionType.Button, "<Keyboard>/leftShift");
            fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            ability = new InputAction("Ability", InputActionType.Button, "<Keyboard>/e");

            Yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            move.Enable(); look.Enable(); jump.Enable(); crouch.Enable(); slide.Enable();
            roll.Enable(); fire.Enable(); aim.Enable(); ability.Enable();
            LockCursor(true);
        }

        private void OnDisable()
        {
            move.Disable(); look.Disable(); jump.Disable(); crouch.Disable(); slide.Disable();
            roll.Disable(); fire.Disable(); aim.Disable(); ability.Disable();
            LockCursor(false);
        }

        private void OnDestroy()
        {
            move.Dispose(); look.Dispose(); jump.Dispose(); crouch.Dispose(); slide.Dispose();
            roll.Dispose(); fire.Dispose(); aim.Dispose(); ability.Dispose();
        }

        private void Update()
        {
            HandleCursor();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = look.ReadValue<Vector2>();
                Yaw = Mathf.Repeat(Yaw + delta.x * mouseSensitivity, 360f);
                float pitchDelta = delta.y * mouseSensitivity * (invertY ? 1f : -1f);
                Pitch = Mathf.Clamp(Pitch + pitchDelta, minPitch, maxPitch);
            }

            if (jump.WasPressedThisFrame()) jumpLatch = true;
            if (slide.WasPressedThisFrame()) slideLatch = true;
            if (roll.WasPressedThisFrame()) rollLatch = true;
            if (ability.WasPressedThisFrame()) abilityLatch = true;
        }

        /// <summary>Entrega os comandos para o tick atual e limpa os "apertou".</summary>
        public PlayerInputFrame ConsumeFrame()
        {
            var frame = new PlayerInputFrame
            {
                Move = move.ReadValue<Vector2>(),
                Yaw = Yaw,
                Pitch = Pitch,
                JumpPressed = jumpLatch,
                JumpHeld = jump.IsPressed(),
                CrouchHeld = crouch.IsPressed(),
                SlidePressed = slideLatch,
                RollPressed = rollLatch,
                FireHeld = fire.IsPressed(),
                AimHeld = aim.IsPressed(),
                AbilityPressed = abilityLatch
            };

            jumpLatch = slideLatch = rollLatch = abilityLatch = false;
            return frame;
        }

        // Esc solta o mouse; clicar na tela do jogo prende de novo.
        private void HandleCursor()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                LockCursor(false);
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                LockCursor(true);
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
