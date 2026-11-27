using Rogue.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Lê teclado e mouse (Input System) e monta o PlayerInputFrame que o motor e as armas consomem.
    /// Os botões de "apertou" ficam guardados (latch) até o próximo tick consumir,
    /// assim nenhum aperto se perde, mesmo com FPS alto e tick fixo.
    /// Por enquanto as teclas estão no código; quando houver menu de rebind,
    /// elas migram para um arquivo .inputactions em Assets/_Project/Input.
    ///
    /// Teclas:
    ///   WASD mover · Espaço pular · Shift agachar (segurar) · Shift + Espaço perto da parede = wall run
    ///   Ctrl deitar/levantar · C slide (segurar) · Alt cambalhota
    ///   1 rifle · 2 pistola · 3 faca · 4 gelo · Q troca rápida · R recarregar
    ///   Botão esquerdo atirar/usar · Botão direito mirar
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
        private InputAction wallRun;
        private InputAction prone;
        private InputAction slide;
        private InputAction roll;
        private InputAction fire;
        private InputAction aim;
        private InputAction weapon1;
        private InputAction weapon2;
        private InputAction weapon3;
        private InputAction weapon4;
        private InputAction quickSwitch;
        private InputAction reload;
        private InputAction[] allActions;

        private bool jumpLatch;
        private bool proneLatch;
        private bool slideLatch;
        private bool rollLatch;
        private bool fireLatch;
        private bool weapon1Latch;
        private bool weapon2Latch;
        private bool weapon3Latch;
        private bool weapon4Latch;
        private bool quickSwitchLatch;
        private bool reloadLatch;

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
            crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftShift");
            // Mesma tecla do agachar: o contexto decide (Shift + Espaço perto da parede = correr na parede).
            wallRun = new InputAction("WallRun", InputActionType.Button, "<Keyboard>/leftShift");
            prone = new InputAction("Prone", InputActionType.Button, "<Keyboard>/leftCtrl");
            slide = new InputAction("Slide", InputActionType.Button, "<Keyboard>/c");
            roll = new InputAction("Roll", InputActionType.Button, "<Keyboard>/leftAlt");
            fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
            aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            weapon1 = new InputAction("Weapon1", InputActionType.Button, "<Keyboard>/1");
            weapon2 = new InputAction("Weapon2", InputActionType.Button, "<Keyboard>/2");
            weapon3 = new InputAction("Weapon3", InputActionType.Button, "<Keyboard>/3");
            weapon4 = new InputAction("Weapon4", InputActionType.Button, "<Keyboard>/4");
            quickSwitch = new InputAction("QuickSwitch", InputActionType.Button, "<Keyboard>/q");
            reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");

            allActions = new[]
            {
                move, look, jump, crouch, wallRun, prone, slide, roll, fire, aim,
                weapon1, weapon2, weapon3, weapon4, quickSwitch, reload
            };

            Yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            foreach (InputAction action in allActions) action.Enable();
            LockCursor(true);
        }

        private void OnDisable()
        {
            foreach (InputAction action in allActions) action.Disable();
            LockCursor(false);
        }

        private void OnDestroy()
        {
            foreach (InputAction action in allActions) action.Dispose();
        }

        private void Update()
        {
            // O clique que prende o mouse na tela não pode virar tiro.
            bool wasLocked = Cursor.lockState == CursorLockMode.Locked;
            HandleCursor();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = look.ReadValue<Vector2>();
                Yaw = Mathf.Repeat(Yaw + delta.x * mouseSensitivity, 360f);
                float pitchDelta = delta.y * mouseSensitivity * (invertY ? 1f : -1f);
                Pitch = Mathf.Clamp(Pitch + pitchDelta, minPitch, maxPitch);
            }

            if (jump.WasPressedThisFrame()) jumpLatch = true;
            if (prone.WasPressedThisFrame()) proneLatch = true;
            if (slide.WasPressedThisFrame()) slideLatch = true;
            if (roll.WasPressedThisFrame()) rollLatch = true;
            if (wasLocked && fire.WasPressedThisFrame()) fireLatch = true;
            if (weapon1.WasPressedThisFrame()) weapon1Latch = true;
            if (weapon2.WasPressedThisFrame()) weapon2Latch = true;
            if (weapon3.WasPressedThisFrame()) weapon3Latch = true;
            if (weapon4.WasPressedThisFrame()) weapon4Latch = true;
            if (quickSwitch.WasPressedThisFrame()) quickSwitchLatch = true;
            if (reload.WasPressedThisFrame()) reloadLatch = true;
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
                WallRunHeld = wallRun.IsPressed(),
                ProneTogglePressed = proneLatch,
                SlidePressed = slideLatch,
                SlideHeld = slide.IsPressed(),
                ReloadPressed = reloadLatch,
                RollPressed = rollLatch,
                FireHeld = fire.IsPressed() && Cursor.lockState == CursorLockMode.Locked,
                FirePressed = fireLatch,
                AimHeld = aim.IsPressed(),
                Weapon1Pressed = weapon1Latch,
                Weapon2Pressed = weapon2Latch,
                Weapon3Pressed = weapon3Latch,
                Weapon4Pressed = weapon4Latch,
                QuickSwitchPressed = quickSwitchLatch
            };

            jumpLatch = proneLatch = slideLatch = rollLatch = fireLatch = false;
            weapon1Latch = weapon2Latch = weapon3Latch = weapon4Latch = quickSwitchLatch = reloadLatch = false;
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
