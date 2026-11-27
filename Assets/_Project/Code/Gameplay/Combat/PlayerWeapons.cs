using System;
using System.Collections.Generic;
using Rogue.Core;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>Uma arma do jogador: o arquivo de números (config) + as peças do modelo 3D.</summary>
    [Serializable]
    public sealed class WeaponBinding
    {
        public WeaponConfig config;

        [Tooltip("Modelo 3D da arma.")]
        public Transform model;

        [Tooltip("Ponta do cano (de onde sai o tiro). Vazio na faca e no gelo.")]
        public Transform muzzle;

        public GameObject muzzleFlash;

        [Tooltip("Ponto onde a mão esquerda segura (armas de duas mãos).")]
        public Transform supportGrip;

        [Tooltip("Carregador (peça que sai na mão esquerda ao recarregar). Opcional.")]
        public Transform magazine;

        public bool IsValid => config != null && model != null;
    }

    /// <summary>
    /// Armas do jogador: 1 rifle, 2 pistola, 3 faca (karambit), 4 gelo. Q volta para a arma anterior.
    /// - Sacar leva equipTime (não ataca enquanto isso).
    /// - Rifle: automático (segura o botão). Pistola: um tiro por clique. Faca: golpe por clique.
    /// - Armas de tiro têm pente e reserva; R recarrega; pente vazio recarrega sozinho.
    ///   Trocar de arma cancela a recarga; pendurado/subindo também.
    /// - Gelo: mostra a prévia transparente; clique coloca a barreira.
    /// - A arma na mão muda a velocidade (faca +15%, pistola +10%) e atirar correndo no chão tira 20%.
    /// - Não ataca pendurado, subindo, rolando ou sacando. Ataca correndo, pulando, agachado,
    ///   deitado, deslizando e na parede.
    /// Roda no tick fixo logo depois do motor, lendo o mesmo PlayerInputFrame.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PlayerWeapons : MonoBehaviour
    {
        public const int SlotCount = 4;

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private IceWallPlacer iceWallPlacer;

        [Header("Armas (teclas 1, 2, 3, 4)")]
        [SerializeField] private WeaponBinding primary = new WeaponBinding();
        [SerializeField] private WeaponBinding secondary = new WeaponBinding();
        [SerializeField] private WeaponBinding melee = new WeaponBinding();
        [SerializeField] private WeaponBinding gadget = new WeaponBinding();

        [Tooltip("Depois de colocar o gelo, volta sozinho para a arma anterior.")]
        [SerializeField] private bool returnToPreviousAfterGadget = true;

        private static readonly Collider[] OverlapBuffer = new Collider[16];

        private WeaponBinding[] slots;
        private int currentSlot;
        private int previousSlot;
        private float equipTimer;
        private float equipDuration;
        private float cooldown;
        private float attackTimer;
        private float attackDuration;
        private bool meleeHitPending;
        private int[] magazine;
        private int[] reserve;
        private float reloadTimer;
        private float reloadDuration;
        private int reloadSlot = -1;
        private float flashTimer;
        private GameObject activeFlash;
        private ObjectPool<LineRenderer> tracerPool;
        private readonly List<(LineRenderer line, float until)> activeTracers = new List<(LineRenderer, float)>();

        public int CurrentSlot => currentSlot;
        public int PreviousSlot => previousSlot;
        public WeaponBinding Current => GetSlot(currentSlot);
        public WeaponConfig CurrentConfig => Current != null ? Current.config : null;
        public IceWallPlacer IceWall => iceWallPlacer;

        public bool IsEquipping => equipTimer > 0f;
        /// <summary>0 = começou a sacar, 1 = arma pronta.</summary>
        public float EquipProgress => equipDuration <= 0f ? 1f : 1f - Mathf.Clamp01(equipTimer / equipDuration);

        public bool IsMeleeAttacking => attackTimer > 0f;
        /// <summary>0 = começo do golpe, 1 = fim.</summary>
        public float MeleeProgress => attackDuration <= 0f ? 1f : 1f - Mathf.Clamp01(attackTimer / attackDuration);
        public int MeleeAttackCount { get; private set; }

        // ===== Eventos (sons/efeitos escutam; a lógica do jogo não depende deles) =====
        /// <summary>Atirou (arma, posição do cano).</summary>
        public event Action<WeaponConfig, Vector3> Fired;
        /// <summary>Tentou atirar sem bala nenhuma (clique seco).</summary>
        public event Action<WeaponConfig> DryFired;
        /// <summary>Começou a sacar uma arma.</summary>
        public event Action<WeaponConfig> Equipped;
        /// <summary>Golpe da faca começou.</summary>
        public event Action<WeaponConfig> MeleeSwung;
        /// <summary>Tiro ou faca acertou algo.</summary>
        public event Action<WeaponHitInfo> Hit;

        /// <summary>Zoom do botão direito: só com arma de fogo na mão (faca e gelo não).</summary>
        public bool CanAimZoom
        {
            get
            {
                WeaponConfig cfg = CurrentConfig;
                return cfg != null && cfg.kind == WeaponKind.Hitscan;
            }
        }

        public bool IsReloading => reloadTimer > 0f;
        /// <summary>0 = começou a recarregar, 1 = terminou.</summary>
        public float ReloadProgress => reloadDuration <= 0f ? 0f : 1f - Mathf.Clamp01(reloadTimer / reloadDuration);
        public float ReloadTimeLeft => Mathf.Max(0f, reloadTimer);

        public int GetMagazine(int slot) => magazine != null && slot >= 0 && slot < magazine.Length ? magazine[slot] : 0;
        public int GetReserve(int slot) => reserve != null && slot >= 0 && slot < reserve.Length ? reserve[slot] : 0;

        public float LastShotTime { get; private set; } = -999f;
        public float LastHitTime { get; private set; } = -999f;
        /// <summary>Último tiro, golpe ou uso do gelo (o corpo fica virado para a mira um tempo depois).</summary>
        public float LastActionTime { get; private set; } = -999f;

        public WeaponBinding GetSlot(int index)
        {
            return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
        }

        private void Awake()
        {
            slots = new[] { primary, secondary, melee, gadget };
            if (aimCamera == null) aimCamera = Camera.main;

            foreach (WeaponBinding binding in slots)
            {
                if (binding != null && binding.muzzleFlash != null)
                    binding.muzzleFlash.SetActive(false);
            }

            // Pool: reaproveita os rastros de tiro em vez de criar e destruir (performance).
            tracerPool = new ObjectPool<LineRenderer>(
                CreateTracer,
                line => line.gameObject.SetActive(true),
                line => line.gameObject.SetActive(false),
                line => Destroy(line.gameObject),
                collectionCheck: false,
                defaultCapacity: 16,
                maxSize: 64);

            // Pentes cheios no começo.
            magazine = new int[SlotCount];
            reserve = new int[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponBinding binding = slots[i];
                if (binding == null || !binding.IsValid || !binding.config.UsesAmmo) continue;
                magazine[i] = binding.config.magazineSize;
                reserve[i] = Mathf.Max(0, binding.config.reserveAmmo);
            }

            currentSlot = FirstValidSlot(0);
            previousSlot = FirstValidSlot(1);
            if (previousSlot == currentSlot) previousSlot = FirstValidSlot(currentSlot + 1);
        }

        private int FirstValidSlot(int start)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                int index = (start + i) % SlotCount;
                WeaponBinding binding = GetSlot(index);
                if (binding != null && binding.IsValid) return index;
            }
            return 0;
        }

        private void FixedUpdate()
        {
            if (motor == null || slots == null) return;

            float dt = Time.fixedDeltaTime;
            PlayerInputFrame input = motor.LastInput;

            HandleSwitching(input);

            equipTimer = Mathf.Max(0f, equipTimer - dt);
            UpdateMelee(dt);
            UpdateReload(dt);

            // Mãos na beirada: cancela a recarga.
            MotorMode mode = motor.State.Mode;
            if (mode == MotorMode.LedgeHang || mode == MotorMode.LedgeClimb)
                CancelReload();

            WeaponBinding current = Current;
            WeaponConfig cfg = current != null && current.IsValid ? current.config : null;
            bool canAct = cfg != null && CanAct();
            bool firingGun = false;

            if (cfg != null)
            {
                float interval = 1f / Mathf.Max(0.1f, cfg.fireRate);

                switch (cfg.kind)
                {
                    case WeaponKind.Hitscan:
                    {
                        if (input.ReloadPressed && HandsFree() && equipTimer <= 0f)
                            StartReload();

                        bool wantsFire = cfg.automatic ? input.FireHeld : input.FirePressed;
                        bool canShoot = canAct && !IsReloading;

                        if (cfg.automatic)
                        {
                            cooldown -= dt;
                            // Só guarda o "resto" do tempo se for atirar agora
                            // (senão, depois de recarregar/sacar segurando o botão, sairia uma rajada).
                            bool firingNow = canShoot && wantsFire && magazine[currentSlot] > 0;
                            if (!firingNow && cooldown < 0f) cooldown = 0f;
                        }
                        else
                        {
                            cooldown = Mathf.Max(0f, cooldown - dt);
                        }

                        if (canShoot && wantsFire && cooldown <= 0f)
                        {
                            if (magazine[currentSlot] > 0)
                            {
                                FireHitscan(current);
                                magazine[currentSlot]--;
                                if (cfg.automatic) cooldown += interval;
                                else cooldown = interval;

                                if (magazine[currentSlot] == 0 && cfg.autoReloadWhenEmpty)
                                    StartReload();
                            }
                            else
                            {
                                // Pente vazio: recarrega. Sem reserva: só o clique seco.
                                if (!StartReload() && input.FirePressed)
                                    DryFired?.Invoke(cfg);
                            }
                        }

                        firingGun = canShoot && input.FireHeld && magazine[currentSlot] > 0;
                        break;
                    }

                    case WeaponKind.Melee:
                        cooldown = Mathf.Max(0f, cooldown - dt);
                        if (canAct && input.FirePressed && cooldown <= 0f && attackTimer <= 0f)
                        {
                            StartMelee(cfg);
                            cooldown = interval;
                        }
                        break;

                    case WeaponKind.Gadget:
                        cooldown = Mathf.Max(0f, cooldown - dt);
                        if (canAct && input.FirePressed && cooldown <= 0f &&
                            iceWallPlacer != null && iceWallPlacer.TryPlace())
                        {
                            cooldown = interval;
                            LastActionTime = Time.time;
                            if (returnToPreviousAfterGadget && previousSlot != currentSlot)
                                Equip(previousSlot);
                        }
                        break;
                }
            }

            // Prévia do gelo só com o gelo na mão (e com as mãos livres).
            if (iceWallPlacer != null)
            {
                WeaponConfig now = CurrentConfig;
                iceWallPlacer.PreviewActive = now != null && now.kind == WeaponKind.Gadget && HandsFree();
            }

            ApplyMovementModifiers(firingGun);
        }

        private void Update()
        {
            if (activeFlash != null && flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f) HideFlash();
            }

            for (int i = activeTracers.Count - 1; i >= 0; i--)
            {
                if (Time.time >= activeTracers[i].until)
                {
                    tracerPool.Release(activeTracers[i].line);
                    activeTracers.RemoveAt(i);
                }
            }
        }

        // ================= Troca de arma =================

        private void HandleSwitching(in PlayerInputFrame input)
        {
            int requested = -1;
            if (input.Weapon1Pressed) requested = 0;
            else if (input.Weapon2Pressed) requested = 1;
            else if (input.Weapon3Pressed) requested = 2;
            else if (input.Weapon4Pressed) requested = 3;
            else if (input.QuickSwitchPressed) requested = previousSlot;

            if (requested < 0 || requested == currentSlot) return;

            WeaponBinding binding = GetSlot(requested);
            if (binding != null && binding.IsValid)
                Equip(requested);
        }

        private void Equip(int slot)
        {
            previousSlot = currentSlot;
            currentSlot = slot;

            WeaponConfig cfg = slots[slot].config;
            equipDuration = cfg != null ? Mathf.Max(0f, cfg.equipTime) : 0f;
            equipTimer = equipDuration;
            attackTimer = 0f;
            meleeHitPending = false;
            cooldown = 0f;
            CancelReload();
            HideFlash();

            if (cfg != null) Equipped?.Invoke(cfg);
        }

        // ================= Recarga =================

        /// <summary>Começa a recarregar. Retorna false se não precisa ou não dá (pente cheio / sem reserva).</summary>
        private bool StartReload()
        {
            WeaponConfig cfg = CurrentConfig;
            if (cfg == null || !cfg.UsesAmmo || IsReloading) return false;
            if (magazine[currentSlot] >= cfg.magazineSize) return false;           // pente cheio
            if (!cfg.infiniteReserve && reserve[currentSlot] <= 0) return false;    // sem reserva

            reloadSlot = currentSlot;
            reloadDuration = Mathf.Max(0.05f, cfg.reloadTime);
            reloadTimer = reloadDuration;
            cooldown = 0f;
            return true;
        }

        private void UpdateReload(float dt)
        {
            if (reloadTimer <= 0f) return;
            reloadTimer -= dt;
            if (reloadTimer <= 0f) FinishReload();
        }

        private void FinishReload()
        {
            reloadTimer = 0f;
            WeaponBinding binding = GetSlot(reloadSlot);
            if (binding == null || !binding.IsValid) return;

            WeaponConfig cfg = binding.config;
            int needed = cfg.magazineSize - magazine[reloadSlot];
            int taken = cfg.infiniteReserve ? needed : Mathf.Min(needed, reserve[reloadSlot]);
            magazine[reloadSlot] += taken;
            if (!cfg.infiniteReserve) reserve[reloadSlot] -= taken;
        }

        private void CancelReload()
        {
            reloadTimer = 0f;
        }

        private bool HandsFree()
        {
            MotorMode mode = motor.State.Mode;
            return mode != MotorMode.LedgeHang && mode != MotorMode.LedgeClimb && mode != MotorMode.Roll;
        }

        private bool CanAct()
        {
            return HandsFree() && equipTimer <= 0f && Cursor.lockState == CursorLockMode.Locked;
        }

        private void ApplyMovementModifiers(bool firingGun)
        {
            WeaponConfig cfg = CurrentConfig;
            motor.WeaponSpeedMultiplier = cfg != null ? cfg.moveSpeedMultiplier : 1f;
            motor.FiringGroundPenalty = firingGun && cfg != null ? cfg.firingGroundSpeedPenalty : 0f;
        }

        private Vector3 AimDirection()
        {
            PlayerInputFrame input = motor.LastInput;
            return Quaternion.Euler(input.Pitch, input.Yaw, 0f) * Vector3.forward;
        }

        // ================= Tiro =================

        /// <summary>
        /// Tiro hitscan (raio instantâneo).
        /// 1) Um raio sai do centro da tela para descobrir ONDE você está mirando.
        /// 2) Outro raio sai do cano até esse ponto: é ele que acerta de verdade
        ///    (uma parede na frente da arma bloqueia o tiro, mesmo com a mira livre).
        /// </summary>
        private void FireHitscan(WeaponBinding binding)
        {
            WeaponConfig cfg = binding.config;
            if (aimCamera == null) return;

            Vector3 muzzlePosition = binding.muzzle != null
                ? binding.muzzle.position
                : motor.transform.position + Vector3.up * 1.4f;

            Ray cameraRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float skip = Mathf.Max(0f, Vector3.Dot(muzzlePosition - cameraRay.origin, cameraRay.direction));
            Vector3 aimStart = cameraRay.origin + cameraRay.direction * skip;

            Vector3 aimPoint = Physics.Raycast(aimStart, cameraRay.direction, out RaycastHit aimHit,
                cfg.range, cfg.hitMask, QueryTriggerInteraction.Ignore)
                ? aimHit.point
                : aimStart + cameraRay.direction * cfg.range;

            PlayerSimState s = motor.State;
            bool steady = s.Grounded || s.Mode == MotorMode.WallRun;
            float spread = steady ? cfg.spreadDegrees : cfg.airSpreadDegrees;
            if (s.Prone) spread *= cfg.proneSpreadFactor;

            Vector3 direction = ApplySpread((aimPoint - muzzlePosition).normalized, spread);
            Vector3 end = muzzlePosition + direction * cfg.range;

            if (Physics.Raycast(muzzlePosition, direction, out RaycastHit hit, cfg.range, cfg.hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                HitSurface surface = HitSurface.Generic;
                if (target != null && target.IsAlive)
                {
                    surface = target.Surface;
                    target.TakeDamage(cfg.damage);
                    LastHitTime = Time.time;
                }
                Hit?.Invoke(new WeaponHitInfo { Weapon = cfg, Point = hit.point, Surface = surface, Melee = false });
            }

            SpawnTracer(muzzlePosition, end, cfg);

            if (binding.muzzleFlash != null)
            {
                HideFlash();
                activeFlash = binding.muzzleFlash;
                activeFlash.SetActive(true);
                flashTimer = cfg.muzzleFlashDuration;
            }

            LastShotTime = Time.time;
            LastActionTime = Time.time;
            Fired?.Invoke(cfg, muzzlePosition);
        }

        private static Vector3 ApplySpread(Vector3 direction, float degrees)
        {
            if (degrees <= 0f) return direction;
            Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            Quaternion basis = Quaternion.LookRotation(direction);
            return (basis * new Vector3(offset.x, offset.y, 1f)).normalized;
        }

        // ================= Faca =================

        private void StartMelee(WeaponConfig cfg)
        {
            attackDuration = Mathf.Max(0.05f, cfg.meleeAttackDuration);
            attackTimer = attackDuration;
            meleeHitPending = true;
            MeleeAttackCount++;
            LastActionTime = Time.time;
            MeleeSwung?.Invoke(cfg);
        }

        private void UpdateMelee(float dt)
        {
            if (attackTimer <= 0f) return;

            WeaponConfig cfg = CurrentConfig;
            float elapsed = attackDuration - attackTimer;
            if (meleeHitPending && cfg != null && elapsed >= cfg.meleeHitTime)
            {
                meleeHitPending = false;
                DoMeleeHit(cfg);
            }

            attackTimer = Mathf.Max(0f, attackTimer - dt);
        }

        private void DoMeleeHit(WeaponConfig cfg)
        {
            Vector3 origin = motor.transform.position + Vector3.up * (motor.State.Height * 0.75f);
            Vector3 direction = AimDirection();
            IDamageable target = null;
            bool touched = false;
            Vector3 point = origin + direction * cfg.meleeRange;

            if (Physics.SphereCast(origin, cfg.meleeRadius, direction, out RaycastHit hit, cfg.meleeRange,
                    cfg.hitMask, QueryTriggerInteraction.Ignore))
            {
                touched = true;
                point = hit.point;
                target = hit.collider.GetComponentInParent<IDamageable>();
            }

            // Alvo colado no corpo (o SphereCast não acha o que já começa dentro da esfera).
            if (target == null)
            {
                int count = Physics.OverlapSphereNonAlloc(origin + direction * (cfg.meleeRange * 0.5f),
                    cfg.meleeRadius + cfg.meleeRange * 0.25f, OverlapBuffer, cfg.hitMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    IDamageable candidate = OverlapBuffer[i].GetComponentInParent<IDamageable>();
                    if (candidate != null && candidate.IsAlive)
                    {
                        target = candidate;
                        touched = true;
                        point = OverlapBuffer[i].ClosestPoint(origin);
                        break;
                    }
                }
            }

            if (target != null && target.IsAlive)
            {
                HitSurface surface = target.Surface;
                target.TakeDamage(cfg.damage);
                LastHitTime = Time.time;
                Hit?.Invoke(new WeaponHitInfo { Weapon = cfg, Point = point, Surface = surface, Melee = true });
            }
            else if (touched)
            {
                Hit?.Invoke(new WeaponHitInfo { Weapon = cfg, Point = point, Surface = HitSurface.Generic, Melee = true });
            }
        }

        // ================= Efeitos =================

        private void HideFlash()
        {
            if (activeFlash != null) activeFlash.SetActive(false);
            activeFlash = null;
            flashTimer = 0f;
        }

        private void SpawnTracer(Vector3 from, Vector3 to, WeaponConfig cfg)
        {
            LineRenderer line = tracerPool.Get();
            line.startWidth = cfg.tracerWidth;
            line.endWidth = cfg.tracerWidth * 0.5f;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            activeTracers.Add((line, Time.time + cfg.tracerDuration));
        }

        private LineRenderer CreateTracer()
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.sharedMaterial = tracerMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.SetActive(false);
            return line;
        }
    }
}
