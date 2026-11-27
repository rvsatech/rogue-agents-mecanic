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

        [Tooltip("Peça que se mexe (ferrolho da pistola/AWP, telha da 12, alavanca da UMP). Opcional, só visual.")]
        public Transform action;

        [Tooltip("Janela por onde sai a cápsula (lado direito). Opcional, só visual.")]
        public Transform ejectionPort;

        public bool IsValid => config != null && model != null;
    }

    /// <summary>
    /// Armas do jogador, com INVENTÁRIO (B escolhe o que vai em cada tecla):
    /// 1 primária (UMP, AWP ou a 12) · 2 secundária (pistola) · 3 faca · 4 kit médico · E parede de gelo ·
    /// G gadget (granada, flash ou smoke). Q volta para a arma anterior. O que não está escolhido fica guardado
    /// (não aparece no corpo). Granadas: SEGURA o botão (o caminho aparece) e SOLTA para jogar — esquerdo longe,
    /// direito perto; depois volta para a arma.
    /// A 12 solta vários chumbos por tiro (pellets) e perde dano longe (falloff).
    /// E = kit médico: guarda a arma, usa por useTime (anda devagar) e cura healPercent da vida; depois volta
    /// sozinho para a arma. E de novo, trocar de arma, rolar, deslizar, parede ou beirada cancelam.
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
        public const int SlotCount = 10;
        /// <summary>Posição do kit médico na lista (tecla 4; não entra no Q).</summary>
        public const int MedkitSlot = 5;
        /// <summary>Posição da faca (tecla 3) e da parede de gelo (tecla E) na lista.</summary>
        public const int KnifeSlot = 2;
        public const int IceWallSlot = 3;

        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Material tracerMaterial;
        [SerializeField] private IceWallPlacer iceWallPlacer;
        [SerializeField] private PlayerHealth health;

        [Header("Armas (teclas 1, 2, 3, 4)")]
        [SerializeField] private WeaponBinding primary = new WeaponBinding();
        [SerializeField] private WeaponBinding secondary = new WeaponBinding();
        [SerializeField] private WeaponBinding melee = new WeaponBinding();
        [SerializeField] private WeaponBinding gadget = new WeaponBinding();
        [SerializeField] private WeaponBinding sniper = new WeaponBinding();

        [Header("Item (tecla 4)")]
        [SerializeField] private WeaponBinding medkit = new WeaponBinding();

        [Header("A 12 (primária pelo inventário)")]
        [SerializeField] private WeaponBinding shotgun = new WeaponBinding();

        [Header("Granadas (G, a escolhida no inventário)")]
        [SerializeField] private WeaponBinding frag = new WeaponBinding();
        [SerializeField] private WeaponBinding flashbang = new WeaponBinding();
        [SerializeField] private WeaponBinding smoke = new WeaponBinding();

        [Header("Inventário (B): posição na lista acima")]
        [Tooltip("Tecla 1: 0 = UMP, 4 = AWP, 6 = a 12.")]
        [SerializeField] private int primaryChoice = 0;
        [Tooltip("Tecla 2: 1 = pistola.")]
        [SerializeField] private int secondaryChoice = 1;
        [Tooltip("Tecla G: 7 = granada, 8 = flash, 9 = smoke.")]
        [SerializeField] private int gadgetChoice = 7;

        [Tooltip("Depois de colocar o gelo, volta sozinho para a arma anterior.")]
        [SerializeField] private bool returnToPreviousAfterGadget = true;

        [Tooltip("Clique guardado por este tempo se ainda não der para agir (ex.: 4 + clique juntos: coloca o gelo " +
                 "assim que ele estiver na mão). Vale para pistola, faca e gelo.")]
        [SerializeField] private float fireBufferTime = 0.2f;

        private static readonly Collider[] OverlapBuffer = new Collider[16];
        private static readonly Dictionary<IDamageable, WeaponHitInfo> PelletHits = new Dictionary<IDamageable, WeaponHitInfo>();

        private WeaponBinding[] slots;
        private int currentSlot;
        private int previousSlot;
        private float equipTimer;
        private float fireBuffer;
        private float scopeBlockedUntil;
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
        private int[] itemCount;
        private float itemUseTimer;
        private int itemReturnSlot;
        private int itemPreviousSlot;
        private int lastWeaponSlot;
        private int holsterSlot = -1;
        private float throwTimer;
        private int throwButton; // segurando para jogar: 0 nada, 1 esquerdo (longe), 2 direito (perto)
        private float throwDuration;
        private Collider ownCollider;
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

        /// <summary>
        /// Trocando de arma: a que estava na mão e volta para o coldre no começo da troca (-1 = nenhuma:
        /// mão vazia, como depois de jogar a granada). Só visual — o tempo de sacar não muda.
        /// </summary>
        public int HolsterSlot => equipTimer > 0f ? holsterSlot : -1;

        /// <summary>Colisor do próprio jogador (pentes e cápsulas caindo não batem nele).</summary>
        public Collider OwnCollider => ownCollider;

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
        /// <summary>Terminou de usar um item (config, quanto curou).</summary>
        public event Action<WeaponConfig, float> ItemUsed;
        /// <summary>Jogou uma granada/flash/smoke.</summary>
        public event Action<WeaponConfig> Thrown;
        /// <summary>Mudou o que está escolhido no inventário.</summary>
        public event Action LoadoutChanged;

        /// <summary>Zoom de ombro do botão direito: só arma de fogo SEM luneta (faca e gelo não; AWP usa a luneta).</summary>
        public bool CanAimZoom
        {
            get
            {
                WeaponConfig cfg = CurrentConfig;
                return cfg != null && cfg.kind == WeaponKind.Hitscan && !cfg.HasScope;
            }
        }

        /// <summary>
        /// Na luneta: arma com luneta + botão direito, mãos livres, já sacada, sem recarregar e com o ferrolho pronto.
        /// </summary>
        public bool IsScoped { get; private set; }

        [Tooltip("Deslizando: o espalhamento do tiro é multiplicado por isso (tiro pior).")]
        [SerializeField] private float slideSpreadMultiplier = 2f;

        [Tooltip("Mirando (zoom de ombro): a velocidade de andar é multiplicada por isso (0,85 = 15% mais lento).")]
        [SerializeField] private float aimMoveSpeedMultiplier = 0.85f;

        /// <summary>
        /// O botão direito não mira (sem zoom/luneta): deslizando, rolando, escalando e na parede
        /// (com wallRunAllowAim desligado). A câmera usa o mesmo.
        /// </summary>
        public bool AimBlocked
        {
            get
            {
                if (motor == null) return false;
                MotorMode mode = motor.State.Mode;
                if (mode == MotorMode.Slide || mode == MotorMode.Roll) return true;
                return mode == MotorMode.WallRun &&
                       (motor.State.WallClimbing || (motor.Config != null && !motor.Config.wallRunAllowAim));
            }
        }

        /// <summary>Deslizando (tiro pior e coice maior).</summary>
        public bool IsSliding => motor != null && motor.State.Mode == MotorMode.Slide;

        /// <summary>Mirando com arma de fogo: zoom de ombro (rifle/pistola) ou luneta (AWP). Dá menos coice.</summary>
        public bool IsAiming { get; private set; }

        /// <summary>Usando um item (kit médico) agora.</summary>
        public bool IsUsingItem => CurrentConfig != null && CurrentConfig.kind == WeaponKind.Consumable;
        /// <summary>0 = começou a usar, 1 = terminou.</summary>
        public float ItemUseProgress
        {
            get
            {
                WeaponConfig cfg = CurrentConfig;
                return cfg == null || cfg.useTime <= 0f ? 0f : Mathf.Clamp01(itemUseTimer / cfg.useTime);
            }
        }
        public float ItemUseTimeLeft => CurrentConfig != null ? Mathf.Max(0f, CurrentConfig.useTime - itemUseTimer) : 0f;
        public int GetItemCount(int slot) => itemCount != null && slot >= 0 && slot < itemCount.Length ? itemCount[slot] : 0;
        public PlayerHealth Health => health;

        /// <summary>Acabou de jogar uma granada (o braço termina o movimento e depois volta para a arma).</summary>
        public bool IsThrowing => throwTimer > 0f;
        /// <summary>0 = soltou agora, 1 = terminou o movimento do braço.</summary>
        public float ThrowProgress => throwDuration <= 0f ? 1f : 1f - Mathf.Clamp01(throwTimer / throwDuration);
        /// <summary>Segurando o botão com a granada na mão (braço armado; soltou = joga).</summary>
        public bool ThrowAiming => throwButton != 0 && throwTimer <= 0f;
        /// <summary>Segurando para jogar: mostra o caminho do arremesso.</summary>
        public bool ThrowPreviewActive
        {
            get
            {
                WeaponConfig cfg = CurrentConfig;
                return ThrowAiming && cfg != null && cfg.kind == WeaponKind.Throwable && equipTimer <= 0f &&
                       motor != null && HandsFree();
            }
        }
        /// <summary>Segurando o botão direito: arremesso curto.</summary>
        public bool ShortThrowHeld => throwButton == 2;

        /// <summary>Apertou E mas não deu (vida cheia, sem kit...): o HUD mostra o motivo por um instante.</summary>
        public float ItemRefusedTime { get; private set; } = -999f;
        public string ItemRefusedReason { get; private set; } = "";

        public bool IsReloading => reloadTimer > 0f;
        /// <summary>0 = começou a recarregar, 1 = terminou.</summary>
        public float ReloadProgress => reloadDuration <= 0f ? 0f : 1f - Mathf.Clamp01(reloadTimer / reloadDuration);
        public float ReloadTimeLeft => Mathf.Max(0f, reloadTimer);

        public int GetMagazine(int slot) => magazine != null && slot >= 0 && slot < magazine.Length ? magazine[slot] : 0;
        public int GetReserve(int slot) => reserve != null && slot >= 0 && slot < reserve.Length ? reserve[slot] : 0;

        public float LastShotTime { get; private set; } = -999f;
        /// <summary>Arma (posição na lista) do último tiro: o ferrolho/bombeada só anima nela.</summary>
        public int LastShotSlot { get; private set; } = -1;
        public float LastHeadshotTime { get; private set; } = -999f;
        public float LastKillTime { get; private set; } = -999f;
        public float LastHitTime { get; private set; } = -999f;
        /// <summary>Último tiro, golpe ou uso do gelo (o corpo fica virado para a mira um tempo depois).</summary>
        public float LastActionTime { get; private set; } = -999f;

        public WeaponBinding GetSlot(int index)
        {
            return slots != null && index >= 0 && index < slots.Length ? slots[index] : null;
        }

        private void Awake()
        {
            slots = new[] { primary, secondary, melee, gadget, sniper, medkit, shotgun, frag, flashbang, smoke };
            if (motor != null) ownCollider = motor.GetComponent<Collider>();
            if (aimCamera == null) aimCamera = Camera.main;
            if (health == null && !TryGetComponent(out health)) health = gameObject.AddComponent<PlayerHealth>();

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

            // Pentes cheios e itens no começo.
            magazine = new int[SlotCount];
            reserve = new int[SlotCount];
            itemCount = new int[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponBinding binding = slots[i];
                if (binding == null || !binding.IsValid) continue;
                if (IsItemKind(binding.config.kind)) itemCount[i] = Mathf.Max(0, binding.config.itemCount);
                if (!binding.config.UsesAmmo) continue;
                magazine[i] = binding.config.magazineSize;
                reserve[i] = Mathf.Max(0, binding.config.reserveAmmo);
            }

            primaryChoice = ValidChoice(LoadoutCategory.Primary, primaryChoice);
            secondaryChoice = ValidChoice(LoadoutCategory.Secondary, secondaryChoice);
            gadgetChoice = ValidChoice(LoadoutCategory.Gadget, gadgetChoice);

            currentSlot = IsValidSlot(primaryChoice) ? primaryChoice : FirstValidSlot(0);
            lastWeaponSlot = currentSlot;
            previousSlot = IsValidSlot(secondaryChoice) ? secondaryChoice : FirstValidSlot(1);
            if (previousSlot == currentSlot) previousSlot = FirstValidSlot(currentSlot + 1);
            RefreshModelVisibility(); // o que não está escolhido no inventário não aparece no corpo
        }

        private int FirstValidSlot(int start)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                int index = (start + i) % SlotCount;
                WeaponBinding binding = GetSlot(index);
                if (binding != null && binding.IsValid && !IsItemKind(binding.config.kind)) return index;
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

            // Luneta (sniper): depois do tiro sai por um tempo (ferrolho) e volta se ainda estiver mirando.
            // Na parede (MovementConfig.wallRunAllowAim desligado) o botão direito não mira: sem luneta e sem zoom.
            bool aimHeld = input.AimHeld && !AimBlocked;
            IsScoped = cfg != null && cfg.HasScope && aimHeld && canAct && !IsReloading &&
                       Time.time >= scopeBlockedUntil;
            IsAiming = cfg != null && cfg.kind == WeaponKind.Hitscan && aimHeld && (!cfg.HasScope || IsScoped);

            // Clique guardado: se não deu para agir neste tick, vale ainda por fireBufferTime.
            if (input.FirePressed) fireBuffer = fireBufferTime;
            else fireBuffer = Mathf.Max(0f, fireBuffer - dt);
            bool firePressed = fireBuffer > 0f;

            if (cfg != null)
            {
                float interval = 1f / Mathf.Max(0.1f, cfg.fireRate);

                switch (cfg.kind)
                {
                    case WeaponKind.Hitscan:
                    {
                        if (input.ReloadPressed && HandsFree() && equipTimer <= 0f)
                            StartReload();

                        bool wantsFire = cfg.automatic ? input.FireHeld : firePressed;
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
                                if (!cfg.infiniteMagazine) magazine[currentSlot]--; // teste: balas infinitas
                                fireBuffer = 0f;
                                if (cfg.unscopeAfterShotTime > 0f)
                                {
                                    scopeBlockedUntil = Time.time + cfg.unscopeAfterShotTime; // ferrolho
                                    IsScoped = false;
                                }
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
                        if (canAct && firePressed && cooldown <= 0f && attackTimer <= 0f)
                        {
                            StartMelee(cfg);
                            cooldown = interval;
                            fireBuffer = 0f;
                        }
                        break;

                    case WeaponKind.Consumable:
                        UpdateItemUse(cfg, canAct, dt);
                        break;

                    case WeaponKind.Throwable:
                        UpdateThrowable(current, canAct, input, dt);
                        break;

                    case WeaponKind.Gadget:
                        cooldown = Mathf.Max(0f, cooldown - dt);
                        if (canAct && firePressed && cooldown <= 0f &&
                            iceWallPlacer != null && iceWallPlacer.TryPlace())
                        {
                            cooldown = interval;
                            fireBuffer = 0f;
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
            if (input.MedkitPressed)
            {
                TryUseMedkit();
                return;
            }

            int requested = -1;
            if (input.Weapon1Pressed) requested = primaryChoice;
            else if (input.Weapon2Pressed) requested = secondaryChoice;
            else if (input.Weapon3Pressed) requested = KnifeSlot;
            else if (input.IceWallPressed) requested = IceWallSlot;
            else if (input.GadgetPressed) requested = gadgetChoice;
            else if (input.QuickSwitchPressed) requested = previousSlot;

            if (requested < 0 || requested == currentSlot || requested == MedkitSlot) return;

            WeaponBinding binding = GetSlot(requested);
            if (binding == null || !binding.IsValid) return;
            if (binding.config.kind == WeaponKind.Throwable && !binding.config.infiniteItems && itemCount[requested] <= 0)
            {
                RefuseItem($"Sem {binding.config.displayName.ToLowerInvariant()}");
                return;
            }
            Equip(requested);
        }

        private static bool IsItemKind(WeaponKind kind) => kind == WeaponKind.Consumable || kind == WeaponKind.Throwable;

        /// <param name="handEmpty">A mão já está vazia (a granada foi jogada): nada para guardar no coldre.</param>
        private void Equip(int slot, bool handEmpty = false)
        {
            // Saindo no meio do arremesso: a granada já foi embora (a mão está vazia).
            if (throwTimer > 0f) handEmpty = true;
            throwTimer = 0f;
            throwButton = 0;

            // A arma que estava na mão volta para o coldre no começo da troca (só se estiver no corpo).
            int leaving = currentSlot;
            WeaponBinding leavingBinding = GetSlot(leaving);
            holsterSlot = !handEmpty && leaving != slot && leavingBinding != null && leavingBinding.IsValid &&
                          IsInLoadout(leaving)
                ? leaving
                : -1;

            WeaponBinding entering = GetSlot(slot);
            if (entering != null && entering.IsValid &&
                (entering.config.kind == WeaponKind.Hitscan || entering.config.kind == WeaponKind.Melee))
                lastWeaponSlot = slot;

            if (slot == MedkitSlot)
            {
                // Pegando o kit: guarda para onde voltar (o Q continua apontando para a mesma arma de antes).
                itemReturnSlot = currentSlot;
                itemPreviousSlot = previousSlot;
            }
            else if (currentSlot == MedkitSlot)
            {
                // Saindo do kit: o Q nunca aponta para o kit.
                previousSlot = slot == itemReturnSlot ? itemPreviousSlot : itemReturnSlot;
            }
            else
            {
                previousSlot = currentSlot;
            }
            currentSlot = slot;
            itemUseTimer = 0f;

            WeaponConfig cfg = slots[slot].config;
            equipDuration = cfg != null ? Mathf.Max(0f, cfg.equipTime) : 0f;
            equipTimer = equipDuration;
            attackTimer = 0f;
            meleeHitPending = false;
            cooldown = 0f;
            CancelReload();
            HideFlash();

            RefreshModelVisibility();
            if (cfg != null) Equipped?.Invoke(cfg);
        }

        // ================= Granada, flash e smoke (7, 8, 9) =================

        private void UpdateThrowable(WeaponBinding binding, bool canAct, in PlayerInputFrame input, float dt)
        {
            // Já soltou: o braço termina o movimento e volta para a arma.
            if (throwTimer > 0f)
            {
                throwTimer -= dt;
                if (throwTimer <= 0f) ReturnFromThrow();
                return;
            }

            WeaponConfig cfg = binding.config;
            if (!canAct || (!cfg.infiniteItems && itemCount[currentSlot] <= 0))
            {
                throwButton = 0;
                return;
            }

            // Segura para armar (o caminho aparece na tela) e solta para jogar.
            if (throwButton == 0)
            {
                if (input.FireHeld) throwButton = 1;      // esquerdo: longe
                else if (input.AimHeld) throwButton = 2;  // direito: perto
                return;
            }

            bool stillHeld = throwButton == 1 ? input.FireHeld : input.AimHeld;
            if (stillHeld) return;

            bool shortThrow = throwButton == 2;
            throwButton = 0;
            Throw(binding, shortThrow);
        }

        private void Throw(WeaponBinding binding, bool shortThrow)
        {
            WeaponConfig cfg = binding.config;
            if (!GetThrowStart(cfg, shortThrow, out Vector3 origin, out Vector3 velocity)) return;

            // A granada que voa é uma cópia do modelo da mão (a da mão some até voltar para a arma).
            GameObject projectile = Instantiate(binding.model.gameObject, origin, binding.model.rotation);
            projectile.name = cfg.displayName;
            projectile.layer = gameObject.layer; // camada do jogador: tiros e câmera não batem nela
            projectile.AddComponent<ThrownGrenade>().Launch(cfg, this, velocity, ownCollider);

            if (!cfg.infiniteItems) itemCount[currentSlot] = Mathf.Max(0, itemCount[currentSlot] - 1);
            throwDuration = Mathf.Max(0.05f, cfg.throwRecoveryTime);
            throwTimer = throwDuration;
            RefreshModelVisibility(); // a da mão some (foi embora voando)
            fireBuffer = 0f;
            LastActionTime = Time.time;
            Thrown?.Invoke(cfg);
        }

        /// <summary>Guarda a mão e volta para a última arma (UMP, pistola, faca, AWP ou a 12).</summary>
        private void ReturnFromThrow()
        {
            throwTimer = 0f;
            RefreshModelVisibility();

            int back = lastWeaponSlot;
            WeaponBinding target = GetSlot(back);
            if (target == null || !target.IsValid || IsItemKind(target.config.kind)) back = FirstValidSlot(0);
            if (back != currentSlot) Equip(back, handEmpty: true); // a granada foi embora: só saca a arma
        }

        /// <summary>
        /// De onde a granada sai e com que velocidade: do ombro direito, na direção da mira (até onde a mira aponta),
        /// um pouco para cima, somando metade da velocidade do corpo. O caminho na tela usa a mesma conta.
        /// </summary>
        public bool GetThrowStart(WeaponConfig cfg, bool shortThrow, out Vector3 origin, out Vector3 velocity)
        {
            origin = velocity = Vector3.zero;
            if (cfg == null || motor == null) return false;

            Vector3 aim = AimDirection();
            Vector3 flatAim = new Vector3(aim.x, 0f, aim.z);
            flatAim = flatAim.sqrMagnitude > 0.0001f ? flatAim.normalized : motor.transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, flatAim).normalized;
            origin = motor.transform.position + Vector3.up * (motor.State.Height * 0.85f) + right * 0.25f + flatAim * 0.25f;

            Vector3 target = origin + aim * 50f;
            if (aimCamera != null)
            {
                Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                target = Physics.Raycast(ray.origin, ray.direction, out RaycastHit hit, 60f, cfg.hitMask,
                    QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(60f);
            }

            Vector3 direction = target - origin;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : aim;
            if (Vector3.Dot(direction, aim) < 0.5f) direction = aim; // mira em algo colado na câmera

            Vector3 axis = Vector3.Cross(Vector3.up, direction);
            if (axis.sqrMagnitude > 0.0001f)
                direction = Quaternion.AngleAxis(-cfg.throwUpAngle, axis.normalized) * direction;

            Vector3 body = motor.State.Velocity;
            velocity = direction * (shortThrow ? cfg.shortThrowSpeed : cfg.throwSpeed) + new Vector3(body.x, 0f, body.z) * 0.5f;
            return true;
        }

        /// <summary>Acerto que não veio de um tiro (ex.: granada): avisa o HUD e os sons como um tiro.</summary>
        public void ReportHit(WeaponHitInfo info)
        {
            if (info.Damage > 0f) LastHitTime = Time.time;
            if (info.Killed) LastKillTime = Time.time;
            Hit?.Invoke(info);
        }

        // ================= Inventário (B) =================

        /// <summary>Grupo do inventário de uma arma (null = fica sempre: faca, gelo e kit médico).</summary>
        public static LoadoutCategory? CategoryOf(WeaponConfig cfg)
        {
            if (cfg == null) return null;
            if (cfg.kind == WeaponKind.Throwable) return LoadoutCategory.Gadget;
            if (cfg.kind == WeaponKind.Hitscan)
                return cfg.slot == WeaponSlot.Secondary ? LoadoutCategory.Secondary : LoadoutCategory.Primary;
            return null;
        }

        /// <summary>Posições da lista que podem ir neste grupo (o menu do inventário mostra estas).</summary>
        public void GetOptions(LoadoutCategory category, List<int> result)
        {
            result.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponBinding binding = GetSlot(i);
                if (binding != null && binding.IsValid && CategoryOf(binding.config) == category) result.Add(i);
            }
        }

        public int GetChoice(LoadoutCategory category)
        {
            switch (category)
            {
                case LoadoutCategory.Primary: return primaryChoice;
                case LoadoutCategory.Secondary: return secondaryChoice;
                default: return gadgetChoice;
            }
        }

        /// <summary>Escolhe a arma de um grupo. Se estava com a antiga na mão, já troca para a nova.</summary>
        public void Choose(LoadoutCategory category, int slot)
        {
            WeaponBinding binding = GetSlot(slot);
            if (binding == null || !binding.IsValid || CategoryOf(binding.config) != category) return;

            int old = GetChoice(category);
            if (old == slot) return;

            if (category == LoadoutCategory.Primary) primaryChoice = slot;
            else if (category == LoadoutCategory.Secondary) secondaryChoice = slot;
            else gadgetChoice = slot;

            if (previousSlot == old) previousSlot = slot;
            if (lastWeaponSlot == old) lastWeaponSlot = slot;
            if (currentSlot == old) Equip(slot);
            RefreshModelVisibility();
            LoadoutChanged?.Invoke();
        }

        /// <summary>Está no corpo agora (1, 2, 3, 4, E ou G)? O resto fica guardado no inventário.</summary>
        public bool IsInLoadout(int slot)
        {
            WeaponBinding binding = GetSlot(slot);
            if (binding == null || !binding.IsValid) return false;
            LoadoutCategory? category = CategoryOf(binding.config);
            return category == null || GetChoice(category.Value) == slot;
        }

        /// <summary>Tecla que pega esta arma agora ("B" = guardada no inventário).</summary>
        public string KeyOf(int slot)
        {
            WeaponBinding binding = GetSlot(slot);
            if (binding == null || !binding.IsValid) return "";
            switch (binding.config.kind)
            {
                case WeaponKind.Melee: return "3";
                case WeaponKind.Gadget: return "E";
                case WeaponKind.Consumable: return "4";
            }
            LoadoutCategory? category = CategoryOf(binding.config);
            if (category == null || GetChoice(category.Value) != slot) return "B";
            return category == LoadoutCategory.Primary ? "1" : category == LoadoutCategory.Secondary ? "2" : "G";
        }

        private bool IsValidSlot(int slot)
        {
            WeaponBinding binding = GetSlot(slot);
            return binding != null && binding.IsValid;
        }

        /// <summary>Se a escolha salva não serve (cena antiga, arma que não existe), pega a primeira do grupo.</summary>
        private int ValidChoice(LoadoutCategory category, int slot)
        {
            WeaponBinding binding = GetSlot(slot);
            if (binding != null && binding.IsValid && CategoryOf(binding.config) == category) return slot;
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponBinding candidate = GetSlot(i);
                if (candidate != null && candidate.IsValid && CategoryOf(candidate.config) == category) return i;
            }
            return slot;
        }

        /// <summary>No corpo só aparece o que está escolhido; a granada que acabou de ser jogada some da mão.</summary>
        private void RefreshModelVisibility()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                WeaponBinding binding = GetSlot(i);
                if (binding == null || binding.model == null) continue;
                bool show = IsInLoadout(i) && !(i == currentSlot && throwTimer > 0f);
                if (binding.model.gameObject.activeSelf != show) binding.model.gameObject.SetActive(show);
            }
        }

        // ================= Kit médico (4) =================

        /// <summary>E: começa a usar o kit médico (E de novo cancela).</summary>
        private void TryUseMedkit()
        {
            if (currentSlot == MedkitSlot)
            {
                ReturnFromItem();
                return;
            }

            WeaponBinding binding = GetSlot(MedkitSlot);
            if (binding == null || !binding.IsValid || health == null) return;

            if (!health.IsAlive) return;
            if (health.IsFull) { RefuseItem("Vida cheia"); return; }
            if (!binding.config.infiniteItems && itemCount[MedkitSlot] <= 0) { RefuseItem("Sem kit médico"); return; }
            if (ItemBlockedByMovement()) { RefuseItem("Não dá para usar agora"); return; }

            Equip(MedkitSlot);
        }

        /// <summary>Usando o kit: conta o tempo (só com as mãos livres e já sacado) e cura no fim.</summary>
        private void UpdateItemUse(WeaponConfig cfg, bool canAct, float dt)
        {
            // Rolou, deslizou, foi para a parede ou pendurou: cancela e volta para a arma.
            if (health == null || !health.IsAlive || ItemBlockedByMovement())
            {
                ReturnFromItem();
                return;
            }

            if (!canAct) return; // ainda sacando o kit

            itemUseTimer += dt;
            if (itemUseTimer < cfg.useTime) return;

            float healed = health.Heal(health.Max * cfg.healPercent);
            if (!cfg.infiniteItems) itemCount[currentSlot] = Mathf.Max(0, itemCount[currentSlot] - 1);
            ItemUsed?.Invoke(cfg, healed);
            ReturnFromItem();
        }

        /// <summary>Guarda o item e volta para a arma de antes.</summary>
        private void ReturnFromItem()
        {
            itemUseTimer = 0f;
            int back = itemReturnSlot;
            WeaponBinding binding = GetSlot(back);
            if (back == MedkitSlot || binding == null || !binding.IsValid) back = FirstValidSlot(0);
            if (back != currentSlot) Equip(back);
        }

        private bool ItemBlockedByMovement()
        {
            MotorMode mode = motor.State.Mode;
            return mode == MotorMode.Roll || mode == MotorMode.Slide || mode == MotorMode.WallRun ||
                   mode == MotorMode.LedgeHang || mode == MotorMode.LedgeClimb;
        }

        private void RefuseItem(string reason)
        {
            ItemRefusedReason = reason;
            ItemRefusedTime = Time.time;
        }

        // ================= Recarga =================

        /// <summary>Começa a recarregar. Retorna false se não precisa ou não dá (pente cheio / sem reserva).</summary>
        private bool StartReload()
        {
            WeaponConfig cfg = CurrentConfig;
            if (cfg == null || !cfg.UsesAmmo || IsReloading) return false;
            // Pente cheio: não recarrega (com balas infinitas de teste o R recarrega mesmo assim, para ver a animação).
            if (magazine[currentSlot] >= cfg.magazineSize && !cfg.infiniteMagazine) return false;
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
            return mode != MotorMode.LedgeHang && mode != MotorMode.LedgeClimb && mode != MotorMode.Roll &&
                   !(mode == MotorMode.WallRun && motor.State.WallClimbing); // escalando: mãos no muro
        }

        private bool CanAct()
        {
            return HandsFree() && equipTimer <= 0f && Cursor.lockState == CursorLockMode.Locked;
        }

        private void ApplyMovementModifiers(bool firingGun)
        {
            WeaponConfig cfg = CurrentConfig;
            float scoped = cfg != null && IsScoped ? cfg.scopedMoveSpeedMultiplier : 1f;
            float aiming = IsAiming && !IsScoped ? aimMoveSpeedMultiplier : 1f; // mirando: -15%
            motor.WeaponSpeedMultiplier = cfg != null ? cfg.moveSpeedMultiplier * scoped * aiming : 1f;
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
            if (cfg.HasScope && IsScoped && steady) spread = cfg.scopedSpreadDegrees; // luneta: preciso parado
            if (s.Prone) spread *= cfg.proneSpreadFactor;
            if (s.Mode == MotorMode.Slide) spread = Mathf.Max(spread, cfg.spreadDegrees) * slideSpreadMultiplier;
            if (cfg.movingSpreadDegrees > 0f && motor.Config != null && motor.Config.runSpeed > 0f)
                spread += cfg.movingSpreadDegrees * Mathf.Clamp01(s.HorizontalSpeed / motor.Config.runSpeed);

            Vector3 baseDirection = (aimPoint - muzzlePosition).normalized;
            if (cfg.pellets > 1)
            {
                FirePellets(cfg, muzzlePosition, baseDirection, spread); // a 12: vários chumbos
            }
            else
            {
                Vector3 direction = ApplySpread(baseDirection, spread);
                Vector3 end = muzzlePosition + direction * cfg.range;

                if (Physics.Raycast(muzzlePosition, direction, out RaycastHit hit, cfg.range, cfg.hitMask,
                        QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    var info = new WeaponHitInfo { Weapon = cfg, Point = hit.point, Surface = HitSurface.Generic };
                    if (target != null && target.IsAlive)
                        ApplyDamage(target, cfg, HitZone.KindOf(hit.collider), hit.distance, ref info);
                    Hit?.Invoke(info);
                }

                SpawnTracer(muzzlePosition, end, cfg);
            }

            if (binding.muzzleFlash != null)
            {
                HideFlash();
                activeFlash = binding.muzzleFlash;
                activeFlash.SetActive(true);
                flashTimer = cfg.muzzleFlashDuration;
            }

            LastShotTime = Time.time;
            LastShotSlot = currentSlot;
            LastActionTime = Time.time;
            Fired?.Invoke(cfg, muzzlePosition);
        }

        /// <summary>
        /// A 12: cada chumbo é um raio com a sua própria imprecisão e dá o dano da arma (com a parte do corpo e a
        /// queda de dano pela distância). Um alvo atingido por vários chumbos vira UM acerto com o dano somado
        /// (cabeça se algum chumbo pegou na cabeça).
        /// </summary>
        private void FirePellets(WeaponConfig cfg, Vector3 muzzle, Vector3 baseDirection, float spread)
        {
            PelletHits.Clear();
            int impacts = 0;

            for (int i = 0; i < cfg.pellets; i++)
            {
                Vector3 direction = ApplySpread(baseDirection, spread);
                Vector3 end = muzzle + direction * cfg.range;

                if (Physics.Raycast(muzzle, direction, out RaycastHit hit, cfg.range, cfg.hitMask, QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                    if (target != null && target.IsAlive)
                    {
                        HitZoneKind zone = HitZone.KindOf(hit.collider);
                        float amount = cfg.damage * cfg.ZoneMultiplier(zone) * cfg.FalloffAt(hit.distance);
                        target.TakeDamage(amount, zone);

                        PelletHits.TryGetValue(target, out WeaponHitInfo info);
                        if (info.Weapon == null)
                        {
                            info.Weapon = cfg;
                            info.Point = hit.point;
                            info.Surface = target.Surface;
                            info.Target = target as Component;
                            info.Zone = zone;
                        }
                        info.Damage += amount;
                        if (zone == HitZoneKind.Head)
                        {
                            info.Zone = HitZoneKind.Head;
                            info.Point = hit.point;
                        }
                        info.Killed = !target.IsAlive;
                        PelletHits[target] = info;
                    }
                    else if (target == null && impacts < 3)
                    {
                        impacts++; // poucos sons de impacto por tiro (não vira barulho)
                        Hit?.Invoke(new WeaponHitInfo { Weapon = cfg, Point = hit.point, Surface = HitSurface.Generic });
                    }
                }

                SpawnTracer(muzzle, end, cfg);
            }

            foreach (WeaponHitInfo info in PelletHits.Values)
            {
                LastHitTime = Time.time;
                if (info.Zone == HitZoneKind.Head) LastHeadshotTime = Time.time;
                if (info.Killed) LastKillTime = Time.time;
                Hit?.Invoke(info);
            }
            PelletHits.Clear();
        }

        /// <summary>Dano com o multiplicador da parte do corpo e a distância; preenche o acerto (dano, cabeça, abate).</summary>
        private void ApplyDamage(IDamageable target, WeaponConfig cfg, HitZoneKind zone, float distance, ref WeaponHitInfo info)
        {
            float amount = cfg.damage * cfg.ZoneMultiplier(zone) * cfg.FalloffAt(distance);
            info.Surface = target.Surface;
            info.Zone = zone;
            info.Damage = amount;
            info.Target = target as Component;

            target.TakeDamage(amount, zone);
            info.Killed = !target.IsAlive;

            LastHitTime = Time.time;
            if (zone == HitZoneKind.Head) LastHeadshotTime = Time.time;
            if (info.Killed) LastKillTime = Time.time;
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
            Collider touchedCollider = null;
            bool touched = false;
            Vector3 point = origin + direction * cfg.meleeRange;

            if (Physics.SphereCast(origin, cfg.meleeRadius, direction, out RaycastHit hit, cfg.meleeRange,
                    cfg.hitMask, QueryTriggerInteraction.Ignore))
            {
                touched = true;
                point = hit.point;
                touchedCollider = hit.collider;
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
                        touchedCollider = OverlapBuffer[i];
                        point = OverlapBuffer[i].ClosestPoint(origin);
                        break;
                    }
                }
            }

            if (target != null && target.IsAlive)
            {
                var info = new WeaponHitInfo { Weapon = cfg, Point = point, Melee = true };
                ApplyDamage(target, cfg, HitZone.KindOf(touchedCollider), 0f, ref info);
                Hit?.Invoke(info);
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
