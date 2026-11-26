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
        [SerializeField] private PlayerMotor motor;
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

            string text =
                $"<b>Estado:</b> {s.Id}\n" +
                $"<b>Vel. horizontal:</b> {s.HorizontalSpeed:0.00} m/s\n" +
                $"<b>Vel. vertical:</b> {s.Velocity.y:0.00} m/s\n" +
                $"<b>No chão:</b> {(s.Grounded ? "sim" : "não")}\n" +
                $"<b>Multiplicador:</b> x{s.SpeedMultiplier:0.00}\n" +
                $"<b>Janela bhop:</b> {bhopLeft:0.00} s\n" +
                $"<b>Agachado:</b> {(s.Crouching ? "sim" : "não")}  (altura {s.Height:0.00} m)\n" +
                $"<b>FPS:</b> {smoothedFps:0}   <b>Tick:</b> {1f / Time.fixedDeltaTime:0} Hz";

            GUI.Box(new Rect(10, 10, 330, 200), text, boxStyle);

            // Mira no centro da tela.
            GUI.Label(new Rect(Screen.width * 0.5f - 15, Screen.height * 0.5f - 15, 30, 30), "+", crosshairStyle);

            if (showControls)
            {
                const string controls =
                    "<b>Controles</b>\n" +
                    "WASD mover · Espaço pular · Ctrl agachar (segurar)\n" +
                    "Bhop: pule para frente e pule de novo até 0,5 s depois de tocar o chão\n" +
                    "Air strafe: no ar, segure A ou D (+W) e gire o mouse\n" +
                    "Esc solta o mouse · clique na tela para prender";
                GUI.Box(new Rect(10, Screen.height - 125, 560, 115), controls, boxStyle);
            }
        }
    }
}
