using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Sons do personagem. Só ESCUTA o jogo (eventos e mudanças de estado) — não muda nada na simulação.
    /// No online, o mesmo componente toca os sons dos outros jogadores a partir do estado recebido.
    /// - Movimento: passos (na hora em que o pé encosta, vindo da animação), pulo, aterrissagem,
    ///   slide (contínuo enquanto segura C), cambalhota, agachar/deitar/levantar/rastejar,
    ///   agarrar beirada, subir (voz de esforço), correr na parede, pulo da parede.
    /// - Armas: tiro, sacar, recarga (tira pente, põe pente, engatilha), clique sem bala, golpe da faca,
    ///   impactos (parede, alvo, gelo) e o "tic" de acerto.
    /// - Gelo: criar e quebrar.
    /// </summary>
    [DefaultExecutionOrder(-30)] // depois do motor (-50) e das armas (-40): lê o estado já atualizado no tick
    public sealed class PlayerAudio : MonoBehaviour
    {
        [SerializeField] private SoundBank bank;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private IceWallPlacer iceWallPlacer;
        [SerializeField] private CharacterModelAnimator modelAnimator;

        [Tooltip("Jogador deste computador: os sons dele soam quase 2D (claros). Desligue para os outros jogadores.")]
        [SerializeField] private bool isLocalPlayer = true;

        [Header("Ajustes")]
        [Tooltip("Velocidade de queda (m/s) a partir da qual a aterrissagem faz som.")]
        [SerializeField] private float landMinSpeed = 2.5f;

        [Tooltip("Velocidade de queda (m/s) com o som de aterrissagem no máximo.")]
        [SerializeField] private float landMaxSpeed = 14f;

        [Tooltip("Distância rastejando (m) entre um som de roupa e outro.")]
        [SerializeField] private float crawlStepDistance = 0.6f;

        [Tooltip("Momentos da recarga (0 a 1) em que toca: tirar pente, pôr pente, engatilhar.")]
        [SerializeField] private Vector3 reloadSoundMarks = new Vector3(0.2f, 0.7f, 0.86f);

        private bool initialized;
        private MotorMode previousMode;
        private bool previousGrounded;
        private bool previousProne;
        private bool previousCrouching;
        private float previousVerticalSpeed;
        private float previousReloadProgress;
        private float crawlDistance;

        private AudioSource slideSource;
        private float slideVolume;

        private float LocalBlend => isLocalPlayer && bank != null ? bank.localPlayerSpatialBlend : -1f;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (weapons == null) weapons = GetComponent<PlayerWeapons>();
            if (iceWallPlacer == null) iceWallPlacer = GetComponent<IceWallPlacer>();

            // Fonte própria para o som contínuo do slide (fica no jogador, acompanha o movimento).
            var slideObject = new GameObject("Som_Slide");
            slideObject.transform.SetParent(transform, false);
            slideSource = slideObject.AddComponent<AudioSource>();
            slideSource.playOnAwake = false;
            slideSource.loop = true;
            slideSource.dopplerLevel = 0f;
            slideSource.rolloffMode = AudioRolloffMode.Linear;
            slideSource.minDistance = 2f;
            slideSource.maxDistance = 30f;
        }

        private void OnEnable()
        {
            if (weapons != null)
            {
                weapons.Fired += OnFired;
                weapons.DryFired += OnDryFired;
                weapons.Equipped += OnEquipped;
                weapons.MeleeSwung += OnMeleeSwung;
                weapons.Hit += OnHit;
            }
            if (iceWallPlacer != null)
            {
                iceWallPlacer.WallPlaced += OnWallPlaced;
                iceWallPlacer.WallBroken += OnWallBroken;
            }
            if (modelAnimator != null)
                modelAnimator.Footstep += OnFootstep;
        }

        private void OnDisable()
        {
            if (weapons != null)
            {
                weapons.Fired -= OnFired;
                weapons.DryFired -= OnDryFired;
                weapons.Equipped -= OnEquipped;
                weapons.MeleeSwung -= OnMeleeSwung;
                weapons.Hit -= OnHit;
            }
            if (iceWallPlacer != null)
            {
                iceWallPlacer.WallPlaced -= OnWallPlaced;
                iceWallPlacer.WallBroken -= OnWallBroken;
            }
            if (modelAnimator != null)
                modelAnimator.Footstep -= OnFootstep;
        }

        // ===================== Movimento (a cada tick, depois do motor) =====================

        private void FixedUpdate()
        {
            if (motor == null || bank == null) return;

            PlayerSimState s = motor.State;
            if (!initialized)
            {
                Remember(s);
                initialized = true;
                return;
            }

            Vector3 feet = motor.transform.position;
            Vector3 chest = feet + Vector3.up * (s.Height * 0.8f);

            // Pulo: saiu do chão subindo, ou pulou da parede.
            bool jumpedFromGround = previousGrounded && !s.Grounded && s.Mode == MotorMode.Locomotion && s.Velocity.y > 3f;
            bool jumpedFromWall = previousMode == MotorMode.WallRun && s.Mode == MotorMode.Locomotion && s.Velocity.y > 3f;
            if (jumpedFromGround || jumpedFromWall) Play(bank.jump, feet);
            if (jumpedFromWall) Play(bank.voiceShort, chest, 0.8f);

            // Aterrissagem: mais forte quanto maior a queda.
            if (!previousGrounded && s.Grounded)
            {
                float fall = -previousVerticalSpeed;
                if (fall > landMinSpeed)
                    Play(bank.land, feet, Mathf.Lerp(0.35f, 1f, Mathf.InverseLerp(landMinSpeed, landMaxSpeed, fall)));
            }

            // Começou uma ação nova.
            if (s.Mode != previousMode)
            {
                switch (s.Mode)
                {
                    case MotorMode.Slide:
                        Play(bank.slideStart, feet);
                        break;
                    case MotorMode.Roll:
                        Play(bank.roll, feet);
                        break;
                    case MotorMode.WallRun:
                        Play(bank.wallRunStart, feet);
                        break;
                    case MotorMode.LedgeHang:
                        Play(bank.ledgeGrab, chest);
                        Play(bank.voiceShort, chest, 0.7f);
                        break;
                    case MotorMode.LedgeClimb:
                        if (previousMode != MotorMode.LedgeHang) Play(bank.ledgeGrab, chest); // subida rápida
                        Play(bank.voiceEffort, chest, s.QuickMantle ? 0.75f : 1f, s.QuickMantle ? 1.12f : 1f);
                        break;
                }
            }

            // Deitar / levantar / agachar.
            if (s.Prone && !previousProne)
            {
                Play(bank.proneDown, feet);
            }
            else if (!s.Prone && previousProne)
            {
                Play(bank.proneUp, feet);
                Play(bank.voiceShort, chest, 0.45f, 0.92f);
            }
            else if (s.Mode == MotorMode.Locomotion && previousMode == MotorMode.Locomotion && s.Grounded &&
                     !s.Prone && !previousProne && s.Crouching != previousCrouching)
            {
                Play(bank.cloth, chest, 0.6f);
            }

            // Rastejando: roupa arrastando no chão.
            if (s.Prone && s.Grounded && s.HorizontalSpeed > 0.2f)
            {
                crawlDistance += s.HorizontalSpeed * Time.fixedDeltaTime;
                if (crawlDistance >= crawlStepDistance)
                {
                    crawlDistance = 0f;
                    Play(bank.cloth, feet, 0.5f, 0.9f);
                }
            }
            else
            {
                crawlDistance = crawlStepDistance * 0.6f;
            }

            UpdateReloadSounds(chest);
            Remember(s);
        }

        private void Remember(in PlayerSimState s)
        {
            previousMode = s.Mode;
            previousGrounded = s.Grounded;
            previousProne = s.Prone;
            previousCrouching = s.Crouching;
            previousVerticalSpeed = s.Velocity.y;
        }

        /// <summary>Recarga: sons nos momentos em que a mão tira o pente, põe o pente e engatilha.</summary>
        private void UpdateReloadSounds(Vector3 position)
        {
            if (weapons == null || !weapons.IsReloading)
            {
                previousReloadProgress = 0f;
                return;
            }

            WeaponConfig cfg = weapons.CurrentConfig;
            float progress = weapons.ReloadProgress;
            if (cfg != null)
            {
                if (Crossed(previousReloadProgress, progress, reloadSoundMarks.x)) Play(cfg.reloadOutSound, position);
                if (Crossed(previousReloadProgress, progress, reloadSoundMarks.y)) Play(cfg.reloadInSound, position);
                if (Crossed(previousReloadProgress, progress, reloadSoundMarks.z)) Play(cfg.reloadRackSound, position);
            }
            previousReloadProgress = progress;
        }

        private static bool Crossed(float before, float after, float mark) => before < mark && after >= mark;

        // ===================== Slide contínuo (a cada frame, com fade) =====================

        private void Update()
        {
            if (slideSource == null || motor == null || bank == null) return;

            PlayerSimState s = motor.State;
            bool sliding = s.Mode == MotorMode.Slide;
            float target = sliding ? bank.slideLoop.volume * bank.masterVolume : 0f;
            slideVolume = Mathf.MoveTowards(slideVolume, target, Time.deltaTime / 0.08f);

            if (slideVolume > 0.001f)
            {
                if (!slideSource.isPlaying)
                {
                    AudioClip clip = bank.slideLoop.Pick();
                    if (clip == null) return;
                    slideSource.clip = clip;
                    slideSource.spatialBlend = LocalBlend >= 0f ? LocalBlend : bank.slideLoop.spatialBlend;
                    slideSource.time = UnityEngine.Random.Range(0f, clip.length * 0.9f);
                    slideSource.Play();
                }
                slideSource.volume = slideVolume;
                // Desliza mais rápido = som um pouco mais agudo.
                slideSource.pitch = Mathf.Lerp(0.85f, 1.1f, Mathf.InverseLerp(2f, 8f, s.HorizontalSpeed));
            }
            else if (slideSource.isPlaying)
            {
                slideSource.Stop();
            }
        }

        // ===================== Eventos =====================

        private void OnFootstep(FootstepInfo step)
        {
            if (bank == null) return;
            float volume = Mathf.Lerp(0.4f, 1f, Mathf.InverseLerp(1.5f, 8f, step.Speed));
            if (step.Crouching) volume *= 0.45f;
            if (step.OnWall) volume *= 0.9f;
            Play(bank.footstep, step.Position, volume, step.OnWall ? 1.06f : 1f);
        }

        private void OnFired(WeaponConfig cfg, Vector3 muzzle) => Play(cfg.fireSound, muzzle);

        private void OnDryFired(WeaponConfig cfg) => Play(cfg.dryFireSound, Chest());

        private void OnEquipped(WeaponConfig cfg) => Play(cfg.equipSound, Chest());

        private void OnMeleeSwung(WeaponConfig cfg) => Play(cfg.swingSound, Chest());

        private void OnHit(WeaponHitInfo hit)
        {
            if (bank == null) return;

            switch (hit.Surface)
            {
                case HitSurface.Ice:
                    PlayWorld(bank.iceImpact, hit.Point);
                    break;
                case HitSurface.Target:
                    if (hit.Melee) PlayWorld(hit.Weapon.meleeHitSound, hit.Point);
                    else PlayWorld(bank.bulletHitTarget, hit.Point);
                    if (isLocalPlayer) PlayWorld(bank.hitMarker, hit.Point);
                    break;
                default:
                    if (hit.Melee) PlayWorld(hit.Weapon.meleeHitSound, hit.Point, 0.6f);
                    else PlayWorld(bank.bulletImpact, hit.Point);
                    break;
            }
        }

        private void OnWallPlaced(Vector3 position)
        {
            if (bank != null) PlayWorld(bank.iceCreate, position + Vector3.up);
        }

        private void OnWallBroken(Vector3 position)
        {
            if (bank != null) PlayWorld(bank.iceBreak, position);
        }

        // ===================== Tocar =====================

        private Vector3 Chest()
        {
            if (motor == null) return transform.position;
            return motor.transform.position + Vector3.up * (motor.State.Height * 0.8f);
        }

        /// <summary>Som do próprio personagem (quase 2D se for o jogador local).</summary>
        private void Play(SoundCue cue, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (cue == null || bank == null) return;
            AudioService.Instance.Play(cue, position, volume * bank.masterVolume, pitch, LocalBlend);
        }

        /// <summary>Som do mundo (3D: vem do lugar do impacto).</summary>
        private void PlayWorld(SoundCue cue, Vector3 position, float volume = 1f)
        {
            if (cue == null || bank == null) return;
            AudioService.Instance.Play(cue, position, volume * bank.masterVolume);
        }
    }
}
