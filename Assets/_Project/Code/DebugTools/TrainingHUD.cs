using Rogue.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rogue.DebugTools
{
    /// <summary>
    /// Campo de treino (só para testes):
    /// - Números de dano saindo de onde acertou (amarelo = cabeça, cinza = pernas, azul = gelo, "ABATE" ao matar).
    /// - Barra de vida em cima de cada bot.
    /// - Painel: tiros, acertos (precisão), cabeça, dano, DPS, abates, tempo para matar (TTK) e o recoil da rajada.
    /// - F3 zera o painel e levanta os bots.
    /// Só escuta eventos (PlayerWeapons, TrainingBot): não mexe no jogo.
    /// </summary>
    public sealed class TrainingHUD : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private WeaponRecoil recoil;
        [SerializeField] private Camera viewCamera;

        [Tooltip("Janela (s) do DPS: dano dos últimos N segundos ÷ N.")]
        [SerializeField] private float dpsWindow = 3f;

        [Tooltip("Distância máxima para mostrar a barra de vida dos bots.")]
        [SerializeField] private float barMaxDistance = 70f;

        [Tooltip("Quanto tempo o número de dano fica na tela (s).")]
        [SerializeField] private float popupLifetime = 0.9f;

        private struct Popup
        {
            public Vector3 Position;
            public string Text;
            public Color Color;
            public float Born;
            public int Size;
        }

        private readonly Popup[] popups = new Popup[48];
        private int nextPopup;

        private readonly float[] damageTimes = new float[512];
        private readonly float[] damageValues = new float[512];
        private int nextDamage;

        private int shots;
        private int hits;
        private int headshots;
        private int kills;
        private float totalDamage;
        private float lastTimeToKill = -1f;
        private float bestTimeToKill = -1f;

        private GUIStyle popupStyle;
        private GUIStyle panelStyle;
        private GUIStyle barTextStyle;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            for (int i = 0; i < popups.Length; i++) popups[i].Born = -999f;
            ClearDamageHistory();
        }

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.Fired += OnFired;
                weapons.Hit += OnHit;
            }
            TrainingBot.Killed += OnKilled;
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.Fired -= OnFired;
                weapons.Hit -= OnHit;
            }
            TrainingBot.Killed -= OnKilled;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
                ResetAll();
        }

        // ===================== Eventos =====================

        private void OnFired(WeaponConfig cfg, Vector3 muzzle) => shots++;

        private void OnHit(WeaponHitInfo hit)
        {
            if (hit.Damage <= 0f) return;

            bool onBot = hit.Surface == HitSurface.Target;
            if (onBot)
            {
                bool gunShot = !hit.Melee && hit.Weapon != null && hit.Weapon.kind == WeaponKind.Hitscan;
                if (gunShot) hits++; // precisão só conta tiro (faca e granada não)
                if (hit.Headshot) headshots++;
                totalDamage += hit.Damage;
                damageTimes[nextDamage] = Time.time;
                damageValues[nextDamage] = hit.Damage;
                nextDamage = (nextDamage + 1) % damageTimes.Length;
            }

            Color color = hit.Surface == HitSurface.Ice ? new Color(0.55f, 0.9f, 1f)
                : hit.Headshot ? new Color(1f, 0.85f, 0.15f)
                : hit.Zone == HitZoneKind.Legs || hit.Zone == HitZoneKind.Arms ? new Color(0.78f, 0.78f, 0.78f)
                : Color.white;
            AddPopup(hit.Point, hit.Damage.ToString("0"), color, hit.Headshot ? 26 : 20);

            if (hit.Killed && onBot)
                AddPopup(hit.Point + Vector3.up * 0.35f, "ABATE", new Color(1f, 0.3f, 0.25f), 24);
        }

        private void OnKilled(TrainingBot bot, float timeToKill)
        {
            kills++;
            lastTimeToKill = timeToKill;
            if (bestTimeToKill < 0f || timeToKill < bestTimeToKill) bestTimeToKill = timeToKill;
        }

        private void ResetAll()
        {
            shots = hits = headshots = kills = 0;
            totalDamage = 0f;
            lastTimeToKill = bestTimeToKill = -1f;
            ClearDamageHistory();
            TrainingBot.ResetAll();
        }

        private void ClearDamageHistory()
        {
            for (int i = 0; i < damageTimes.Length; i++) damageTimes[i] = -999f;
        }

        private void AddPopup(Vector3 position, string text, Color color, int size)
        {
            // Espalha um pouco para os números não ficarem um em cima do outro numa rajada.
            Vector3 jitter = new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(0f, 0.1f), Random.Range(-0.12f, 0.12f));
            popups[nextPopup] = new Popup
            {
                Position = position + jitter,
                Text = text,
                Color = color,
                Born = Time.time,
                Size = size
            };
            nextPopup = (nextPopup + 1) % popups.Length;
        }

        // ===================== Desenho =====================

        private void OnGUI()
        {
            if (viewCamera == null) return;
            EnsureStyles();
            DrawBotBars();
            DrawPopups();
            DrawPanel();
        }

        private void EnsureStyles()
        {
            if (popupStyle != null) return;
            popupStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 20
            };
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                alignment = TextAnchor.UpperLeft,
                richText = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            barTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
        }

        private void DrawBotBars()
        {
            Vector3 cameraPosition = viewCamera.transform.position;
            for (int i = 0; i < TrainingBot.All.Count; i++)
            {
                TrainingBot bot = TrainingBot.All[i];
                Vector3 top = bot.transform.position + Vector3.up * (bot.Height + 0.3f);
                float distance = Vector3.Distance(cameraPosition, top);
                if (distance > barMaxDistance) continue;

                Vector3 screen = viewCamera.WorldToScreenPoint(top);
                if (screen.z <= 0f) continue;

                float width = Mathf.Clamp(900f / Mathf.Max(1f, distance), 36f, 90f);
                const float barHeight = 7f;
                float x = screen.x - width * 0.5f;
                float y = Screen.height - screen.y - barHeight;

                float fraction = bot.MaxHealth > 0f ? bot.Health / bot.MaxHealth : 0f;
                Color fill = bot.IsAlive
                    ? Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.9f, 0.3f), fraction)
                    : new Color(0.45f, 0.45f, 0.45f);

                DrawRect(new Rect(x - 1f, y - 1f, width + 2f, barHeight + 2f), new Color(0f, 0f, 0f, 0.6f));
                DrawRect(new Rect(x, y, width * (bot.IsAlive ? fraction : 1f), barHeight), fill);
                GUI.Label(new Rect(x, y - 15f, width, 14f), bot.IsAlive ? bot.Health.ToString("0") : "caído", barTextStyle);
            }
        }

        private void DrawPopups()
        {
            float now = Time.time;
            Color previous = GUI.color;
            for (int i = 0; i < popups.Length; i++)
            {
                float age = now - popups[i].Born;
                if (age < 0f || age >= popupLifetime) continue;

                float t = age / popupLifetime;
                Vector3 world = popups[i].Position + Vector3.up * (0.7f * t);
                Vector3 screen = viewCamera.WorldToScreenPoint(world);
                if (screen.z <= 0f) continue;

                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                popupStyle.fontSize = popups[i].Size;
                var rect = new Rect(screen.x - 60f, Screen.height - screen.y - 16f, 120f, 32f);

                GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f); // sombra
                GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), popups[i].Text, popupStyle);
                Color c = popups[i].Color;
                c.a = alpha;
                GUI.color = c;
                GUI.Label(rect, popups[i].Text, popupStyle);
            }
            GUI.color = previous;
        }

        private void DrawPanel()
        {
            float now = Time.time;
            float recent = 0f;
            for (int i = 0; i < damageTimes.Length; i++)
                if (now - damageTimes[i] <= dpsWindow) recent += damageValues[i];
            float dps = dpsWindow > 0f ? recent / dpsWindow : 0f;

            float accuracy = shots > 0 ? 100f * hits / shots : 0f;
            float headRate = hits > 0 ? 100f * headshots / hits : 0f;
            string ttk = lastTimeToKill >= 0f ? $"{lastTimeToKill:0.00} s (melhor {bestTimeToKill:0.00} s)" : "-";
            string recoilLine = recoil != null
                ? $"\nRecoil: tiro {recoil.SprayIndex} da rajada · mira subiu {recoil.AccumulatedPitch:0.0}°"
                : "";

            string text =
                "<b>Campo de treino</b>  (F3 zera e levanta os bots)\n" +
                $"Tiros: {shots}   Acertos: {hits} ({accuracy:0}%)   Cabeça: {headshots} ({headRate:0}%)\n" +
                $"Dano: {totalDamage:0}   DPS ({dpsWindow:0} s): {dps:0}\n" +
                $"Abates: {kills}   Tempo para matar: {ttk}" +
                recoilLine;

            GUI.Box(new Rect(Screen.width - 480f, 90f, 470f, recoil != null ? 116f : 96f), text, panelStyle);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
