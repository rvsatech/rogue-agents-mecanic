using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Recoil (coice da mira): a cada tiro a mira sobe e vai um pouco para os lados, num PADRÃO FIXO por arma
    /// (sempre igual: dá para aprender a compensar).
    /// - O coice entra suave (recoilKickTime). O 1º tiro sai onde você mirou; o coice vale para os próximos.
    /// - Sobe até recoilMaxVertical numa rajada; depois só balança para os lados.
    /// - Parou de atirar: depois de recoilRecoveryDelay a mira volta sozinha (recoilRecoverySpeed).
    /// - Puxar o mouse para baixo durante a rajada compensa (e a volta não passa do ponto).
    /// - Mirando (botão direito / luneta) dá menos coice (recoilAimFactor).
    /// - Agachado/deslizando, deitado e no ar mudam o coice. Todos os números ficam no WeaponConfig.
    /// Só gira a mira (Yaw/Pitch do PlayerInputReader); o tiro usa a mira normalmente.
    /// </summary>
    [DefaultExecutionOrder(-90)] // depois do leitor do mouse (-100), antes da câmera (LateUpdate)
    public sealed class WeaponRecoil : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;

        private WeaponConfig recoilWeapon;
        private float pendingPitch;      // coice que ainda vai entrar (graus para cima)
        private float pendingYaw;
        private float accumulatedPitch;  // quanto a mira está acima do ponto original (graus)
        private float accumulatedYaw;
        private float lastShotTime = -999f;
        private int sprayIndex;

        /// <summary>Quanto a mira subiu na rajada atual (graus).</summary>
        public float AccumulatedPitch => accumulatedPitch;

        /// <summary>Número do tiro na rajada atual (0 = primeiro).</summary>
        public int SprayIndex => sprayIndex;

        private void Awake()
        {
            if (weapons == null) weapons = GetComponent<PlayerWeapons>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
        }

        private void OnEnable()
        {
            if (weapons != null) weapons.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (weapons != null) weapons.Fired -= OnFired;
        }

        private void OnFired(WeaponConfig cfg, Vector3 muzzle)
        {
            if (cfg == null || (cfg.recoilVertical <= 0f && cfg.recoilHorizontal <= 0f)) return;

            // Trocou de arma ou a mira já voltou: começa uma rajada nova (padrão do início).
            bool recovered = accumulatedPitch <= 0.05f && Mathf.Abs(accumulatedYaw) <= 0.05f && pendingPitch <= 0.01f;
            if (cfg != recoilWeapon || recovered) sprayIndex = 0;
            recoilWeapon = cfg;
            lastShotTime = Time.time;

            float stance = StanceFactor(cfg);
            if (weapons != null && weapons.IsAiming) stance *= cfg.recoilAimFactor; // mirando: menos coice

            // Para cima, até o máximo da rajada.
            float room = Mathf.Max(0f, cfg.recoilMaxVertical - accumulatedPitch - pendingPitch);
            float up = Mathf.Min(cfg.recoilVertical * stance, room);

            // Para os lados: padrão fixo por arma (suave de um tiro para o outro).
            float pattern = Mathf.PerlinNoise(sprayIndex * 0.37f, PatternSeed(cfg)) * 2f - 1f;
            float side = cfg.recoilHorizontal * stance * Mathf.Clamp(pattern, -1f, 1f);
            if (room <= 0f) side *= 1.5f; // no topo da rajada, balança mais

            pendingPitch += up;
            pendingYaw += side;
            sprayIndex++;
        }

        private void Update()
        {
            if (input == null) return;
            float dt = Time.deltaTime;
            WeaponConfig cfg = recoilWeapon;

            // 1) Coice entrando (suave, ~95% em recoilKickTime).
            if (pendingPitch > 0f || Mathf.Abs(pendingYaw) > 0f)
            {
                float kickTime = cfg != null ? cfg.recoilKickTime : 0f;
                float k = kickTime <= 0.001f ? 1f : 1f - Mathf.Exp(-dt * 3f / kickTime);
                float p = pendingPitch * k;
                float y = pendingYaw * k;
                if (pendingPitch - p < 0.002f) p = pendingPitch;
                if (Mathf.Abs(pendingYaw - y) < 0.002f) y = pendingYaw;

                input.AddLook(y, -p); // pitch negativo = para cima
                accumulatedPitch += p;
                accumulatedYaw += y;
                pendingPitch -= p;
                pendingYaw -= y;
            }

            // 2) Compensação: puxar o mouse para baixo (ou contra o desvio) desconta do coice acumulado.
            Vector2 mouse = input.LastMouseLook;
            if (mouse.y > 0f && accumulatedPitch > 0f)
                accumulatedPitch = Mathf.Max(0f, accumulatedPitch - mouse.y);
            if (accumulatedYaw != 0f && mouse.x != 0f && Mathf.Sign(mouse.x) != Mathf.Sign(accumulatedYaw))
                accumulatedYaw = Mathf.MoveTowards(accumulatedYaw, 0f, Mathf.Abs(mouse.x));

            // 3) Parou de atirar: a mira volta sozinha para o ponto original.
            if (cfg != null && pendingPitch <= 0.001f && Time.time - lastShotTime >= cfg.recoilRecoveryDelay)
            {
                float step = cfg.recoilRecoverySpeed * dt;
                float down = Mathf.Min(step, accumulatedPitch);
                float back = Mathf.Clamp(-accumulatedYaw, -step, step);
                if (down > 0f || back != 0f)
                {
                    input.AddLook(back, down);
                    accumulatedPitch -= down;
                    accumulatedYaw += back;
                }
            }
        }

        private float StanceFactor(WeaponConfig cfg)
        {
            if (motor == null) return 1f;
            PlayerSimState s = motor.State;
            if (s.Prone) return cfg.recoilProneFactor;
            if (!s.Grounded) return cfg.recoilAirFactor; // no ar e na parede
            if (s.Crouching || s.Mode == MotorMode.Slide) return cfg.recoilCrouchFactor;
            return 1f;
        }

        /// <summary>Semente fixa por arma (o mesmo nome dá sempre o mesmo padrão).</summary>
        private static float PatternSeed(WeaponConfig cfg)
        {
            string key = cfg.displayName ?? string.Empty;
            int hash = 17;
            for (int i = 0; i < key.Length; i++) hash = hash * 31 + key[i];
            return (hash & 0xFFFF) * 0.013f;
        }
    }
}
