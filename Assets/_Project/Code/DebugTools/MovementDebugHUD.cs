using Rogue.Gameplay;
using UnityEngine;

namespace Rogue.DebugTools
{
    /// <summary>
    /// Painel de debug na tela: mostra o que a simulação está fazendo.
    /// Quando algo "parecer estranho", olhe aqui antes de mexer nos números.
    /// Usa OnGUI (sistema antigo e simples de UI) de propósito: é só para testes.
    /// </summary>
    public sealed class MovementDebugHUD : MonoBehaviour
    {
        private static readonly string[] SlotKeys = { "1", "2", "3", "4" };

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private bool showControls = true;

        private GUIStyle boxStyle;
        private GUIStyle crosshairStyle;
        private float smoothedFps;

        private void Update()
        {
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            smoothedFps = Mathf.Lerp(smoothedFps, fps, 0.1f);
        }

        private void OnGUI()
        {
            if (motor == null || motor.Config == null) return;

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 16,
                    alignment = TextAnchor.UpperLeft,
                    richText = true,
                    padding = new RectOffset(10, 10, 8, 8)
                };
                crosshairStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            PlayerSimState s = motor.State;
            MovementConfig cfg = motor.Config;

            float bhopLeft = s.Grounded && s.SpeedMultiplier > 1f
                ? Mathf.Max(0f, cfg.bhopWindow - s.TimeSinceLanded)
                : 0f;

            string modeLine = "";
            if (s.Mode == MotorMode.WallRun)
                modeLine = $"<b>Parede:</b> {s.WallRunTimer:0.00} / {cfg.wallRunDuration:0.0} s\n";
            else if (s.Mode == MotorMode.Slide)
                modeLine = $"<b>Slide:</b> {s.SlideTimer:0.00} / {s.SlideDuration:0.00} s\n";
            else if (s.Mode == MotorMode.Roll)
                modeLine = $"<b>Cambalhota:</b> {s.RollTimer:0.00} / {s.RollDuration:0.00} s\n";
            else if (s.Mode == MotorMode.LedgeHang)
                modeLine = "<b>Pendurado:</b> Espaço sobe · Shift/S solta\n";
            else if (s.Mode == MotorMode.LedgeClimb)
                modeLine = $"<b>Subindo:</b> fase {s.ClimbPhase + 1}/2" + (s.QuickMantle ? " (rápido)" : "") + "\n";
            else if (s.Prone)
                modeLine = "<b>Deitado:</b> Ctrl ou Espaço levanta\n";

            string text =
                $"<b>Estado:</b> {s.Id}\n" +
                modeLine +
                $"<b>Vel. horizontal:</b> {s.HorizontalSpeed:0.00} m/s   <b>Arma:</b> x{motor.WeaponSpeedMultiplier:0.00}\n" +
                $"<b>Vel. vertical:</b> {s.Velocity.y:0.00} m/s   <b>No chão:</b> {(s.Grounded ? "sim" : "não")}\n" +
                $"<b>Multiplicador:</b> x{s.SpeedMultiplier:0.00}   <b>Bhop:</b> {bhopLeft:0.00} s\n" +
                $"<b>Altura:</b> {s.Height:0.00} m\n" +
                WeaponLine() +
                $"<b>FPS:</b> {smoothedFps:0}   <b>Tick:</b> {1f / Time.fixedDeltaTime:0} Hz";

            GUI.Box(new Rect(10, 10, 420, 230), text, boxStyle);

            // Mira no centro da tela. Fica vermelha e vira "x" por um instante quando acerta algo.
            bool recentHit = weapons != null && Time.time - weapons.LastHitTime < 0.12f;
            Color previousColor = GUI.color;
            GUI.color = recentHit ? Color.red : Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 15, Screen.height * 0.5f - 15, 30, 30), recentHit ? "x" : "+", crosshairStyle);
            GUI.color = previousColor;

            if (showControls)
            {
                const string controls =
                    "<b>Controles</b>\n" +
                    "WASD mover · Espaço pular · Shift agachar (segurar) · Ctrl deitar · C slide · Alt cambalhota\n" +
                    "Bhop: pule para frente e pule de novo até 0,5 s depois de tocar o chão\n" +
                    "No ar: qualquer tecla empurra para aquela direção\n" +
                    "Parede: Shift + Espaço perto da parede (3 s) · Espaço na parede = salto direcional\n" +
                    "Beirada: pule de frente para o bloco · Espaço sobe · Shift/S solta\n" +
                    "1 rifle · 2 pistola · 3 karambit · 4 gelo · Q troca rápida\n" +
                    "Botão esquerdo atira/golpeia/coloca o gelo · Botão direito mira\n" +
                    "Esc solta o mouse · clique na tela para prender";
                GUI.Box(new Rect(10, Screen.height - 215, 760, 205), controls, boxStyle);
            }
        }

        private string WeaponLine()
        {
            if (weapons == null || weapons.CurrentConfig == null) return "";

            WeaponConfig current = weapons.CurrentConfig;
            string line = $"<b>Arma:</b> [{SlotKeys[weapons.CurrentSlot]}] {current.displayName}";
            if (weapons.IsEquipping) line += " (sacando...)";

            WeaponBinding previous = weapons.GetSlot(weapons.PreviousSlot);
            if (previous != null && previous.IsValid)
                line += $"   <b>Q:</b> {previous.config.displayName}";
            line += "\n";

            if (current.kind == WeaponKind.Gadget && weapons.IceWall != null)
            {
                IceWallPlacer ice = weapons.IceWall;
                string where = ice.HasValidPlacement ? "pode colocar" : "lugar inválido";
                line += $"<b>Gelo:</b> {where} · {ice.PlacementDistance:0.0}/{ice.MaxRange:0.0} m · ativos {ice.ActiveCount}/{(ice.Config != null ? ice.Config.maxActiveWalls : 0)}\n";
            }

            return line;
        }
    }
}
