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
    ///   X modo xiu (segurar: devagar e sem barulho)
    ///   1 rifle · 2 pistola · 3 faca · 4 gelo · 5 AWP · Q troca rápida · R recarregar
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
        private InputAction medkit;
        private InputAction iceWall;
        private InputAction gadget;
        private InputAction quickSwitch;
        private InputAction reload;
        private InputAction quietWalk;
        private InputAction[] allActions;

        private bool jumpLatch;
        private bool proneLatch;
        private bool slideLatch;
        private bool rollLatch;
        private bool fireLatch;
        private bool weapon1Latch;
        private bool weapon2Latch;
        private bool weapon3Latch;
        private bool medkitLatch;
        private bool iceWallLatch;
        private bool gadgetLatch;
        private bool aimLatch;
        private bool quickSwitchLatch;
        private bool reloadLatch;

        /// <summary>Menu aberto (inventário): o mouse fica solto e clicar não prende nem atira.</summary>
        public static bool MenuOpen { get; private set; }

        /// <summary>Abre/fecha um menu: solta o mouse ao abrir e prende de novo ao fechar.</summary>
        public static void SetMenuOpen(bool open)
        {
            MenuOpen = open;
            LockCursor(!open);
        }

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        /// <summary>
        /// Multiplica a sensibilidade do mouse (a câmera põe menor no zoom/luneta, para a mira não ficar nervosa).
        /// </summary>
        public float LookSensitivityScale { get; set; } = 1f;

        /// <summary>Quanto o MOUSE girou a mira neste frame (graus; y positivo = para baixo).</summary>
        public Vector2 LastMouseLook { get; private set; }

        /// <summary>Gira a mira por código (ex.: coice da arma). pitchDelta positivo = para baixo.</summary>
        public void AddLook(float yawDelta, float pitchDelta)
        {
            Yaw = Mathf.Repeat(Yaw + yawDelta, 360f);
            Pitch = Mathf.Clamp(Pitch + pitchDelta, minPitch, maxPitch);
        }

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
            medkit = new InputAction("Medkit", InputActionType.Button, "<Keyboard>/4");
            iceWall = new InputAction("IceWall", InputActionType.Button, "<Keyboard>/e");
            gadget = new InputAction("Gadget", InputActionType.Button, "<Keyboard>/g");
            quickSwitch = new InputAction("QuickSwitch", InputActionType.Button, "<Keyboard>/q");
            reload = new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
            quietWalk = new InputAction("QuietWalk", InputActionType.Button, "<Keyboard>/x");

            allActions = new[]
            {
                move, look, jump, crouch, wallRun, prone, slide, roll, fire, aim,
                weapon1, weapon2, weapon3, medkit, iceWall, gadget,
                quickSwitch, reload, quietWalk
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

            LastMouseLook = Vector2.zero;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 delta = look.ReadValue<Vector2>();
                float sensitivity = mouseSensitivity * LookSensitivityScale;
                float yawDelta = delta.x * sensitivity;
                float pitchDelta = delta.y * sensitivity * (invertY ? 1f : -1f);
                Yaw = Mathf.Repeat(Yaw + yawDelta, 360f);
                Pitch = Mathf.Clamp(Pitch + pitchDelta, minPitch, maxPitch);
                LastMouseLook = new Vector2(yawDelta, pitchDelta);
            }

            if (jump.WasPressedThisFrame()) jumpLatch = true;
            if (prone.WasPressedThisFrame()) proneLatch = true;
            if (slide.WasPressedThisFrame()) slideLatch = true;
            if (roll.WasPressedThisFrame()) rollLatch = true;
            if (wasLocked && fire.WasPressedThisFrame()) fireLatch = true;
            if (weapon1.WasPressedThisFrame()) weapon1Latch = true;
            if (weapon2.WasPressedThisFrame()) weapon2Latch = true;
            if (weapon3.WasPressedThisFrame()) weapon3Latch = true;
            if (medkit.WasPressedThisFrame()) medkitLatch = true;
            if (iceWall.WasPressedThisFrame()) iceWallLatch = true;
            if (gadget.WasPressedThisFrame()) gadgetLatch = true;
            if (aim.WasPressedThisFrame()) aimLatch = true;
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
                QuietHeld = quietWalk.IsPressed(),
                RollPressed = rollLatch,
                FireHeld = fire.IsPressed() && Cursor.lockState == CursorLockMode.Locked,
                FirePressed = fireLatch,
                AimHeld = aim.IsPressed(),
                Weapon1Pressed = weapon1Latch,
                Weapon2Pressed = weapon2Latch,
                Weapon3Pressed = weapon3Latch,
                MedkitPressed = medkitLatch,
                IceWallPressed = iceWallLatch,
                GadgetPressed = gadgetLatch,
                AimPressed = aimLatch,
                QuickSwitchPressed = quickSwitchLatch
            };

            jumpLatch = proneLatch = slideLatch = rollLatch = fireLatch = false;
            weapon1Latch = weapon2Latch = weapon3Latch = medkitLatch = iceWallLatch = gadgetLatch = false;
            quickSwitchLatch = reloadLatch = aimLatch = false;
            return frame;
        }

        // Esc solta o mouse; clicar na tela do jogo prende de novo.
        private void HandleCursor()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (MenuOpen) return; // menu aberto: o clique é do menu

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
