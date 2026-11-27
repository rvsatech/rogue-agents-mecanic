using Rogue.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

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
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private bool showControls = true;

        [Tooltip("F4: dano de teste no próprio personagem (nunca mata) — para testar o kit médico.")]
        [SerializeField] private float testDamage = 30f;

        /// <summary>
        /// Versão da cena montada pelo menu Rogue. Sobe quando a cena ganha peças novas (sons, layers, armas...).
        /// Se a cena aberta for mais velha, o HUD mostra um aviso vermelho.
        /// </summary>
        public const int CurrentSceneVersion = 13;

        [Tooltip("Preenchido pelo menu Rogue ao montar a cena (não mexer).")]
        [SerializeField] private int builtSceneVersion;

        private GUIStyle boxStyle;
        private GUIStyle crosshairStyle;
        private GUIStyle ammoStyle;
        private GUIStyle warningStyle;
        private GUIStyle barStyle;
        private GUIStyle smallRightStyle;
        private GUIStyle centerStyle;
        private float smoothedFps;

        private void Update()
        {
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            smoothedFps = Mathf.Lerp(smoothedFps, fps, 0.1f);

            if (health == null && weapons != null) health = weapons.Health;

            // F4: leva dano de teste (deixa pelo menos 1 de vida).
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f4Key.wasPressedThisFrame && health != null && health.Current > 1f)
                health.TakeDamage(Mathf.Min(testDamage, health.Current - 1f), HitZoneKind.Body);
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
                ammoStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 34,
                    alignment = TextAnchor.LowerRight,
                    richText = true
                };
                warningStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter,
                    richText = true,
                    wordWrap = true
                };
                barStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    richText = true
                };
                smallRightStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.LowerRight,
                    richText = true
                };
                centerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    richText = true
                };
            }

            DrawFlashes();

            // Cena montada por uma versão antiga do menu Rogue: avisa (faltam sons, layers, peças...).
            if (builtSceneVersion < CurrentSceneVersion)
            {
                Color before = GUI.color;
                GUI.color = new Color(1f, 0.35f, 0.3f);
                GUI.Box(new Rect(Screen.width * 0.5f - 340f, 10f, 680f, 70f),
                    "<b>CENA DESATUALIZADA</b>: saia do Play e rode o menu <b>Rogue → Criar cena Sandbox_Movement</b>\n" +
                    "(sem isso faltam o inventário (B), as granadas e a 12)", warningStyle);
                GUI.color = before;
            }

            PlayerSimState s = motor.State;
            MovementConfig cfg = motor.Config;

            float bhopLeft = s.Grounded && s.SpeedMultiplier > 1f
                ? Mathf.Max(0f, cfg.bhopWindow - s.TimeSinceLanded)
                : 0f;

            string modeLine = "";
            if (s.Mode == MotorMode.WallRun)
                modeLine = $"<b>Parede:</b> {s.WallRunAirTime:0.00} / {cfg.wallRunDuration:0.0} s\n";
            else if (s.Mode == MotorMode.Slide)
                modeLine = $"<b>Slide:</b> {s.SlideTimer:0.00} / {s.SlideDuration:0.00} s\n";
            else if (s.Mode == MotorMode.Roll)
                modeLine = $"<b>Cambalhota:</b> {s.RollTimer:0.00} / {s.RollDuration:0.00} s\n";
            else if (s.Mode == MotorMode.LedgeHang)
                modeLine = "<b>Pendurado:</b> Espaço sobe · Shift/S solta\n";
            else if (s.Mode == MotorMode.LedgeClimb)
                modeLine = $"<b>Subindo:</b> fase {s.ClimbPhase + 1}/2" + (s.QuickMantle ? " (rápido)" : "") + "\n";
            else if (s.Mode == MotorMode.GetUp)
                modeLine = $"<b>Levantando:</b> etapa {s.GetUpPhase}/2 " +
                           (s.GetUpPhase == 1 ? "(deitado → agachado)" : "(agachado → em pé)") + $" {s.GetUpPhaseTimer:0.0} s\n";
            else if (s.Prone)
                modeLine = "<b>Deitado (de costas, parado):</b> Ctrl ou Espaço levanta\n";
            else if (s.QuietWalking)
                modeLine = "<b>Modo xiu:</b> devagar e sem barulho de passo\n";
            else if (motor.GetUpSpeedFactor < 0.999f)
                modeLine = $"<b>Recuperando:</b> velocidade {motor.GetUpSpeedFactor * 100f:0}% (normal em {s.GetUpTimer:0.0} s)\n";

            string text =
                $"<b>Estado:</b> {s.Id}\n" +
                modeLine +
                $"<b>Vel. horizontal:</b> {s.HorizontalSpeed:0.00} m/s   <b>Arma:</b> x{motor.WeaponSpeedMultiplier:0.00}\n" +
                $"<b>Vel. vertical:</b> {s.Velocity.y:0.00} m/s   <b>No chão:</b> {(s.Grounded ? "sim" : "não")}\n" +
                $"<b>Multiplicador:</b> x{s.SpeedMultiplier:0.00}   <b>Bhop:</b> {bhopLeft:0.00} s\n" +
                $"<b>Altura:</b> {s.Height:0.00} m\n" +
                WeaponLine() +
                $"<b>FPS:</b> {smoothedFps:0}   <b>Tick:</b> {1f / Time.fixedDeltaTime:0} Hz";

            GUI.Box(new Rect(10, 10, 440, 250), text, boxStyle);

            // Mira no centro da tela. Vira "x" por um instante quando acerta: vermelho no corpo, amarelo na cabeça.
            bool recentHit = weapons != null && Time.time - weapons.LastHitTime < 0.12f;
            bool recentHead = weapons != null && Time.time - weapons.LastHeadshotTime < 0.15f;
            Color previousColor = GUI.color;
            GUI.color = recentHead ? new Color(1f, 0.85f, 0.15f) : recentHit ? Color.red : Color.white;
            if (weapons == null || !weapons.IsScoped) // na luneta a mira é a da luneta
            {
                // Tremor da mira logo depois do tiro (mais forte mirando).
                Vector2 jitter = Vector2.zero;
                if (weapons != null && Time.time - weapons.LastShotTime < 0.1f)
                    jitter = UnityEngine.Random.insideUnitCircle * (weapons.IsAiming ? 3f : 1.5f);
                GUI.Label(new Rect(Screen.width * 0.5f - 15 + jitter.x, Screen.height * 0.5f - 15 + jitter.y, 30, 30),
                    recentHit ? "x" : "+", crosshairStyle);
            }
            GUI.color = previousColor;

            DrawAmmo();
            DrawHealth();
            DrawItemUse();

            if (showControls)
            {
                const string controls =
                    "<b>Controles</b>\n" +
                    "WASD mover · Espaço pular · Shift agachar (segurar) · X modo xiu (segurar) · Ctrl deitar/levantar · C slide (segurar) · Alt cambalhota\n" +
                    "Bhop: pule para frente e pule de novo até 0,5 s depois de tocar o chão\n" +
                    "No ar: qualquer tecla empurra para aquela direção\n" +
                    "Parede: Shift + Espaço perto da parede (1 s, 20% mais rápido) · Espaço na parede = salto direcional\n" +
                    "Beirada: pule de frente para o bloco · Espaço sobe · Shift/S solta\n" +
                    "1 primária · 2 secundária · 3 faca · 4 kit médico · E parede de gelo · G gadget · B inventário · Q troca rápida · R recarregar\n" +
                    "G: segura o botão (o caminho aparece) e solta para jogar — esquerdo longe, direito perto · AWP: botão direito = luneta\n" +
                    "F4 leva 30 de dano (teste do kit médico)\n" +
                    "Botão esquerdo atira/golpeia/coloca o gelo · Botão direito mira (zoom só na UMP/pistola)\n" +
                    "Campo de treino (bots à direita do início): F3 zera e levanta os bots\n" +
                    "Esc solta o mouse · clique na tela para prender";
                GUI.Box(new Rect(10, Screen.height - 280, 900, 270), controls, boxStyle);
            }
        }

        /// <summary>Contador de balas no canto inferior direito.</summary>
        private void DrawAmmo()
        {
            if (weapons == null || weapons.CurrentConfig == null) return;

            WeaponConfig cfg = weapons.CurrentConfig;
            string text;
            Color color = Color.white;

            if (cfg.UsesAmmo)
            {
                int slot = weapons.CurrentSlot;
                int magazine = weapons.GetMagazine(slot);
                string reserve = cfg.infiniteReserve || cfg.infiniteMagazine ? "∞" : weapons.GetReserve(slot).ToString();

                if (weapons.IsReloading)
                {
                    text = $"{cfg.displayName}\n<size=22>RECARREGANDO {weapons.ReloadTimeLeft:0.0} s</size>";
                }
                else
                {
                    string inMagazine = cfg.infiniteMagazine ? "∞" : magazine.ToString();
                    text = $"{cfg.displayName}\n{inMagazine} <size=22>/ {reserve}</size>";
                    if (magazine == 0) color = Color.red;
                    else if (magazine <= cfg.magazineSize / 4) color = Color.yellow;
                }
            }
            else if (cfg.kind == WeaponKind.Throwable)
            {
                string count = cfg.infiniteItems ? "∞" : weapons.GetItemCount(weapons.CurrentSlot).ToString();
                text = $"{cfg.displayName}\n<size=34><b>{count}</b></size>";
            }
            else if (cfg.kind == WeaponKind.Consumable)
            {
                text = $"{cfg.displayName}\n<size=22>CURANDO {weapons.ItemUseTimeLeft:0.0} s</size>";
                color = new Color(0.5f, 1f, 0.6f);
            }
            else
            {
                text = cfg.displayName;
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(new Rect(Screen.width - 340, Screen.height - 120, 320, 110), text, ammoStyle);
            GUI.color = previous;
        }

        /// <summary>Barra de vida (acima do contador de balas) e quantos kits médicos tem.</summary>
        private void DrawHealth()
        {
            if (health == null) return;

            const float width = 320f;
            const float height = 26f;
            float x = Screen.width - 340f;
            float y = Screen.height - 156f;
            float fraction = health.Fraction;
            Texture2D white = Texture2D.whiteTexture;
            Color before = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, width, height), white);
            GUI.color = fraction > 0.6f ? new Color(0.3f, 0.85f, 0.4f)
                : fraction > 0.3f ? new Color(1f, 0.78f, 0.2f)
                : new Color(1f, 0.3f, 0.25f);
            GUI.DrawTexture(new Rect(x + 2f, y + 2f, (width - 4f) * fraction, height - 4f), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, width, height), $"<b>VIDA {Mathf.CeilToInt(health.Current)} / {health.Max:0}</b>", barStyle);

            WeaponBinding kit = weapons != null ? weapons.GetSlot(PlayerWeapons.MedkitSlot) : null;
            if (kit != null && kit.IsValid)
            {
                string count = kit.config.infiniteItems ? "∞" : weapons.GetItemCount(PlayerWeapons.MedkitSlot).ToString();
                GUI.Label(new Rect(x, y - 26f, width, 24f), $"<b>E</b> kit médico: {count}", smallRightStyle);
            }

            GUI.color = before;
        }

        /// <summary>Barra "Curando..." no meio da tela (ou o motivo de não ter usado o kit).</summary>
        private void DrawItemUse()
        {
            if (weapons == null) return;

            Rect bar = new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.62f, 300f, 24f);
            Texture2D white = Texture2D.whiteTexture;
            Color before = GUI.color;

            if (weapons.IsUsingItem)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(bar, white);
                GUI.color = new Color(0.3f, 0.9f, 0.45f);
                GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * weapons.ItemUseProgress, bar.height - 4f), white);
                GUI.color = Color.white;
                GUI.Label(bar, $"<b>Curando... {weapons.ItemUseTimeLeft:0.0} s</b>", barStyle);
            }
            else if (Time.time - weapons.ItemRefusedTime < 1.2f)
            {
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(bar.x - 50f, bar.y, bar.width + 100f, bar.height), $"<b>{weapons.ItemRefusedReason}</b>", centerStyle);
            }

            GUI.color = before;
        }

        /// <summary>Tela pisca vermelha ao levar dano e verde ao curar.</summary>
        private void DrawFlashes()
        {
            if (health == null) return;

            float sinceDamage = Time.time - health.LastDamageTime;
            float sinceHeal = Time.time - health.LastHealTime;
            Color before = GUI.color;
            if (sinceDamage < 0.35f)
            {
                GUI.color = new Color(1f, 0f, 0f, 0.3f * (1f - sinceDamage / 0.35f));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            if (sinceHeal < 0.5f)
            {
                GUI.color = new Color(0.2f, 1f, 0.4f, 0.2f * (1f - sinceHeal / 0.5f));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = before;
        }

        private string WeaponLine()
        {
            if (weapons == null || weapons.CurrentConfig == null) return "";

            WeaponConfig current = weapons.CurrentConfig;
            string line = $"<b>Arma:</b> [{weapons.KeyOf(weapons.CurrentSlot)}] {current.displayName}";
            if (weapons.IsEquipping) line += " (sacando...)";
            if (weapons.IsReloading) line += $" (recarregando {weapons.ReloadTimeLeft:0.0} s)";
            if (weapons.IsUsingItem) line += $" (curando {weapons.ItemUseTimeLeft:0.0} s)";

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

            if (weapons.IceWall != null && weapons.IceWall.ActiveCount > 0)
                line += $"<b>Barreiras:</b> {weapons.IceWall.ActiveCount} · a mais antiga quebra em {weapons.IceWall.OldestWallTimeLeft:0} s\n";

            return line;
        }
    }
}
