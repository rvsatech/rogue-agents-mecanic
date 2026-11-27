using System.Collections.Generic;
using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Anima o modelo 3D a partir do estado da simulação.
    /// 1) Escolhe a animação base (parado, correr, sprint, agachar, ar, parede, cambalhota...).
    /// 2) Depois da animação (LateUpdate), ajusta o esqueleto por código:
    ///    - Torção do quadril em combate: pernas para onde anda, tronco para a mira
    ///      (de lado ou de ré atirando para frente; de ré a corrida toca ao contrário).
    ///    - Peito acompanha a mira para cima/baixo.
    ///    - Armas: a da mão (mirando ou abaixada) e as outras nos coldres
    ///      (rifle nas costas, pistola na coxa, faca na cintura, gelo no cinto).
    ///    - IK dos braços: mãos na arma; golpe de faca em arco; mãos na beirada.
    ///    - Slide: quadril baixo, tronco para trás, perna da frente esticada.
    ///    - Deitado: cabeça olhando para frente e corpo balançando ao rastejar.
    /// Feito para o manequim da Universal Animation Library (Quaternius); os nomes das
    /// animações e dos ossos ficam no Inspector para trocar de modelo depois.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CharacterModelAnimator : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private PlayerVisual visual;
        [SerializeField] private Animation animationComponent;

        [Tooltip("Objeto raiz do modelo 3D.")]
        [SerializeField] private Transform model;

        [Header("Animações (nomes dos clipes)")]
        [SerializeField] private string idleClip = "Idle_Loop";
        [SerializeField] private string runClip = "Jog_Fwd_Loop";
        [SerializeField] private string sprintClip = "Sprint_Loop";
        [SerializeField] private string crouchIdleClip = "Crouch_Idle_Loop";
        [SerializeField] private string crouchMoveClip = "Crouch_Fwd_Loop";
        [SerializeField] private string airClip = "Jump_Loop";
        [SerializeField] private string wallRunClip = "Sprint_Loop";
        [SerializeField] private string hangClip = "Jump_Loop";
        [SerializeField] private string climbClip = "Crouch_Idle_Loop";
        [SerializeField] private string slideClip = "Crouch_Idle_Loop";
        [SerializeField] private string rollClip = "Roll";
        [SerializeField] private string proneClip = "Idle_Loop";
        [SerializeField] private float crossFadeTime = 0.12f;

        [Tooltip("Trecho da animação de cambalhota usado (segundos): pula a preparação e o levantar lento.")]
        [SerializeField] private float rollClipStart = 0.42f;
        [SerializeField] private float rollClipEnd = 1.25f;

        [Header("Velocidade das animações (m/s em que a animação fica natural)")]
        [SerializeField] private float runAnimationSpeed = 6f;
        [SerializeField] private float sprintAnimationSpeed = 7.5f;
        [SerializeField] private float crouchAnimationSpeed = 2.4f;

        [Header("Ossos (nomes no modelo)")]
        [SerializeField] private string hipsBone = "DEF-hips";
        [SerializeField] private string spineBone = "DEF-spine.001";
        [SerializeField] private string chestBone = "DEF-spine.003";
        [SerializeField] private string neckBone = "DEF-neck";
        [SerializeField] private string rightUpperArmBone = "DEF-upper_arm.R";
        [SerializeField] private string rightForearmBone = "DEF-forearm.R";
        [SerializeField] private string rightHandBone = "DEF-hand.R";
        [SerializeField] private string leftUpperArmBone = "DEF-upper_arm.L";
        [SerializeField] private string leftForearmBone = "DEF-forearm.L";
        [SerializeField] private string leftHandBone = "DEF-hand.L";
        [SerializeField] private string rightThighBone = "DEF-thigh.R";
        [SerializeField] private string rightShinBone = "DEF-shin.R";
        [SerializeField] private string rightFootBone = "DEF-foot.R";
        [SerializeField] private string leftThighBone = "DEF-thigh.L";
        [SerializeField] private string leftShinBone = "DEF-shin.L";
        [SerializeField] private string leftFootBone = "DEF-foot.L";

        [Header("Ajustes de pose")]
        [Tooltip("Quanto as pernas podem virar em relação ao tronco (graus).")]
        [SerializeField] private float maxLegTwist = 70f;

        [Tooltip("Quanto o peito acompanha a mira para cima/baixo (0 a 1).")]
        [Range(0f, 1f)] [SerializeField] private float chestAimPitchFactor = 0.5f;

        [Tooltip("Altura do quadril durante o slide.")]
        [SerializeField] private float slideHipHeight = 0.42f;

        [Tooltip("Quanto o tronco inclina para trás no slide (graus).")]
        [SerializeField] private float slideLeanBack = 22f;

        [Tooltip("Quanto a cabeça levanta deitado para olhar para frente (graus).")]
        [SerializeField] private float proneHeadPitch = 55f;

        [Tooltip("Quanto a arma vai mais para frente deitado (metros).")]
        [SerializeField] private float proneGunForward = 0.22f;

        private Transform hips, spine, chest, neck;
        private Transform rUpper, rFore, rHand, lUpper, lFore, lHand;
        private Transform rThigh, rShin, rFoot, lThigh, lShin, lFoot;

        private string currentClip;
        private MotorMode lastMode;
        private float legTwist;
        private float slideWeight;
        private float hangWeight;
        private float combatWeight;
        private float stowWeight;
        private readonly HashSet<string> warnedClips = new HashSet<string>();

        private void Awake()
        {
            if (model != null)
            {
                var bones = new Dictionary<string, Transform>();
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);
                }

                hips = Bone(bones, hipsBone);
                spine = Bone(bones, spineBone);
                chest = Bone(bones, chestBone);
                neck = Bone(bones, neckBone);
                rUpper = Bone(bones, rightUpperArmBone);
                rFore = Bone(bones, rightForearmBone);
                rHand = Bone(bones, rightHandBone);
                lUpper = Bone(bones, leftUpperArmBone);
                lFore = Bone(bones, leftForearmBone);
                lHand = Bone(bones, leftHandBone);
                rThigh = Bone(bones, rightThighBone);
                rShin = Bone(bones, rightShinBone);
                rFoot = Bone(bones, rightFootBone);
                lThigh = Bone(bones, leftThighBone);
                lShin = Bone(bones, leftShinBone);
                lFoot = Bone(bones, leftFootBone);
            }

            if (animationComponent == null && model != null)
                animationComponent = model.GetComponentInChildren<Animation>();

            if (animationComponent == null)
            {
                Debug.LogWarning("[CharacterModelAnimator] O modelo não tem componente Animation. " +
                                 "Use o menu Rogue > Criar cena Sandbox_Movement (ele configura o import como Legacy).", this);
                return;
            }

            animationComponent.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationState state in animationComponent)
                state.wrapMode = WrapMode.Loop;

            AnimationState roll = animationComponent[rollClip];
            if (roll != null) roll.wrapMode = WrapMode.ClampForever;
        }

        // ===================== 1) Escolha da animação =====================

        private void Update()
        {
            if (motor == null) return;

            float dt = Time.deltaTime;
            PlayerSimState s = motor.State;
            MovementConfig cfg = motor.Config;
            bool combat = visual != null && visual.InCombat;
            Vector3 bodyForward = visual != null ? visual.BodyForward : transform.forward;

            Vector3 flatVelocity = new Vector3(s.Velocity.x, 0f, s.Velocity.z);
            float speed = flatVelocity.magnitude;

            // Torção das pernas (só em combate: o corpo olha para a mira e não para onde anda).
            float targetTwist = 0f;
            bool backpedal = false;
            if (combat && s.Mode == MotorMode.Locomotion && s.Grounded && !s.Prone && speed > 0.3f)
            {
                float angle = Vector3.SignedAngle(bodyForward, flatVelocity, Vector3.up);
                if (Mathf.Abs(angle) > 100f)
                {
                    // Andando para trás em relação ao corpo: pernas viram "de ré".
                    backpedal = true;
                    angle -= 180f * Mathf.Sign(angle);
                }
                targetTwist = Mathf.Clamp(angle, -maxLegTwist, maxLegTwist);
            }
            else if (combat && s.Mode == MotorMode.Slide)
            {
                // Atirando deslizando: pernas na direção do slide, tronco para a mira.
                targetTwist = Mathf.Clamp(Vector3.SignedAngle(bodyForward, s.SlideDirection, Vector3.up), -85f, 85f);
            }
            else if (combat && s.Mode == MotorMode.WallRun)
            {
                // Atirando na parede: pernas correndo ao longo da parede, tronco para a mira.
                targetTwist = Mathf.Clamp(Vector3.SignedAngle(bodyForward, s.WallTangent, Vector3.up), -maxLegTwist, maxLegTwist);
            }
            legTwist = Mathf.MoveTowards(legTwist, targetTwist, 600f * dt);

            // Pesos das poses feitas por código (sobem/descem suave).
            bool hanging = s.Mode == MotorMode.LedgeHang || s.Mode == MotorMode.LedgeClimb;
            bool rolling = s.Mode == MotorMode.Roll;
            slideWeight = Mathf.MoveTowards(slideWeight, s.Mode == MotorMode.Slide ? 1f : 0f, dt / 0.08f);
            hangWeight = Mathf.MoveTowards(hangWeight, hanging ? 1f : 0f, dt / 0.1f);
            combatWeight = Mathf.MoveTowards(combatWeight, combat ? 1f : 0f, dt / 0.1f);
            stowWeight = Mathf.MoveTowards(stowWeight, hanging || rolling ? 1f : 0f, dt / 0.1f);

            if (animationComponent == null)
            {
                lastMode = s.Mode;
                return;
            }

            string clip;
            float clipSpeed = 1f;
            float restartAt = -1f;

            switch (s.Mode)
            {
                case MotorMode.Slide:
                    clip = slideClip;
                    break;
                case MotorMode.Roll:
                    clip = rollClip;
                    clipSpeed = (rollClipEnd - rollClipStart) / Mathf.Max(0.05f, s.RollDuration);
                    if (lastMode != MotorMode.Roll) restartAt = rollClipStart;
                    break;
                case MotorMode.WallRun:
                    clip = wallRunClip;
                    clipSpeed = Mathf.Max(0.8f, s.WallRunSpeed / sprintAnimationSpeed);
                    break;
                case MotorMode.LedgeHang:
                    clip = hangClip;
                    clipSpeed = 0.5f;
                    break;
                case MotorMode.LedgeClimb:
                    clip = s.ClimbPhase == 0 ? hangClip : climbClip;
                    break;
                default:
                    if (s.Prone)
                    {
                        clip = proneClip;
                    }
                    else if (!s.Grounded)
                    {
                        clip = airClip;
                    }
                    else if (s.Crouching)
                    {
                        bool moving = speed > 0.3f;
                        clip = moving ? crouchMoveClip : crouchIdleClip;
                        if (moving) clipSpeed = Mathf.Max(0.5f, speed / crouchAnimationSpeed);
                    }
                    else if (speed > 0.3f)
                    {
                        bool sprint = speed > cfg.runSpeed * 1.08f;
                        clip = sprint ? sprintClip : runClip;
                        clipSpeed = Mathf.Max(0.6f, speed / (sprint ? sprintAnimationSpeed : runAnimationSpeed));
                    }
                    else
                    {
                        clip = idleClip;
                    }
                    break;
            }

            if (backpedal && (clip == runClip || clip == sprintClip || clip == crouchMoveClip))
                clipSpeed = -clipSpeed; // animação de correr tocada ao contrário = correr de ré

            Play(clip, clipSpeed, restartAt);
            lastMode = s.Mode;
        }

        // ===================== 2) Ajustes por código =====================

        private void LateUpdate()
        {
            if (motor == null || model == null) return;

            PlayerSimState s = motor.State;
            Vector3 up = Vector3.up;
            Vector3 forward = visual != null ? visual.BodyForward : transform.forward;
            Vector3 right = visual != null ? visual.BodyRight : transform.right;
            Vector3 root = visual != null ? visual.InterpolatedPosition : transform.position;
            float proneWeight = visual != null ? visual.ProneWeight : 0f;

            float pitch = input != null ? input.Pitch : 0f;
            float aimYaw = input != null ? input.Yaw : 0f;
            Vector3 aimDirection = Quaternion.Euler(pitch, aimYaw, 0f) * Vector3.forward;

            // Torção: pernas para onde anda, tronco para a mira.
            if (hips != null && spine != null && Mathf.Abs(legTwist) > 0.01f)
            {
                hips.rotation = Quaternion.AngleAxis(legTwist, up) * hips.rotation;
                spine.rotation = Quaternion.AngleAxis(-legTwist, up) * spine.rotation;
            }

            // Slide: quadril baixo, tronco para trás, pernas por IK na direção do slide.
            if (slideWeight > 0f && hips != null)
            {
                Vector3 slideForward = new Vector3(s.SlideDirection.x, 0f, s.SlideDirection.z);
                slideForward = slideForward.sqrMagnitude > 0.0001f ? slideForward.normalized : forward;
                Vector3 slideRight = Vector3.Cross(up, slideForward);

                Vector3 hipPosition = hips.position;
                hipPosition.y = Mathf.Lerp(hipPosition.y, root.y + slideHipHeight, slideWeight);
                hips.position = hipPosition;

                if (spine != null)
                    spine.rotation = Quaternion.AngleAxis(-slideLeanBack * slideWeight, slideRight) * spine.rotation;

                if (rThigh != null)
                {
                    Vector3 rightFootTarget = root + slideForward * 0.75f + slideRight * 0.12f + up * 0.08f;
                    Vector3 rightKneePole = rThigh.position + slideForward * 0.6f + up * 0.6f;
                    TwoBoneIK.Solve(rThigh, rShin, rFoot, rightFootTarget, rightKneePole, slideWeight);
                }
                if (lThigh != null)
                {
                    Vector3 leftFootTarget = root + slideForward * 0.12f - slideRight * 0.18f + up * 0.06f;
                    Vector3 leftKneePole = lThigh.position + slideForward * 0.3f - slideRight * 0.6f + up * 0.2f;
                    TwoBoneIK.Solve(lThigh, lShin, lFoot, leftFootTarget, leftKneePole, slideWeight);
                }
            }

            // Deitado: corpo balança ao rastejar e a cabeça levanta para olhar para frente.
            if (proneWeight > 0f)
            {
                if (hips != null && s.HorizontalSpeed > 0.2f)
                    hips.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 7f) * 6f * proneWeight, up) * hips.rotation;
                if (neck != null)
                    neck.rotation = Quaternion.AngleAxis(-proneHeadPitch * proneWeight, right) * neck.rotation;
            }

            // Deitado, a arma fica sempre apontada para frente.
            float holdWeight = Mathf.Max(combatWeight, proneWeight);

            // Peito acompanha a mira (cima/baixo).
            if (chest != null && holdWeight > 0f)
                chest.rotation = Quaternion.AngleAxis(pitch * chestAimPitchFactor * holdWeight, right) * chest.rotation;

            UpdateWeapons(s, aimDirection, forward, right, up, holdWeight, proneWeight);

            // Mãos na beirada (pendurado / subindo / soltando).
            if (hangWeight > 0f)
                HandsOnLedge(s, up);
        }

        // ===================== Armas =====================

        private void UpdateWeapons(in PlayerSimState s, Vector3 aimDirection, Vector3 forward, Vector3 right, Vector3 up,
            float holdWeight, float proneWeight)
        {
            if (weapons == null) return;

            Vector3 chestPosition = chest != null ? chest.position : transform.position + up * 1.35f;
            int current = weapons.CurrentSlot;
            float draw = Smooth01(weapons.EquipProgress);
            float inHand = draw * (1f - stowWeight);

            // 1) Posiciona todas as armas: a atual na mão (sacando = vindo do coldre), as outras no coldre.
            for (int i = 0; i < PlayerWeapons.SlotCount; i++)
            {
                WeaponBinding binding = weapons.GetSlot(i);
                if (binding == null || !binding.IsValid) continue;

                GetHolsterPose(i, chestPosition, out Vector3 holsterPosition, out Quaternion holsterRotation);

                if (i != current)
                {
                    binding.model.SetPositionAndRotation(holsterPosition, holsterRotation);
                    continue;
                }

                GetHandPose(binding, chestPosition, aimDirection, forward, up, holdWeight, proneWeight,
                    out Vector3 handPosition, out Quaternion handRotation);

                binding.model.SetPositionAndRotation(
                    Vector3.Lerp(holsterPosition, handPosition, inHand),
                    Quaternion.Slerp(holsterRotation, handRotation, inHand));
            }

            // 2) Braços na arma (IK).
            WeaponBinding currentWeapon = weapons.Current;
            if (currentWeapon == null || !currentWeapon.IsValid) return;

            float armWeight = holdWeight * inHand;
            if (currentWeapon.config.kind == WeaponKind.Melee)
            {
                KnifeArms(currentWeapon, chestPosition, aimDirection, right, up, armWeight, inHand);
            }
            else if (armWeight > 0f)
            {
                SolveRightArm(currentWeapon.model.position, aimDirection, right, up, armWeight);
                if (currentWeapon.config.twoHanded && currentWeapon.supportGrip != null)
                    SolveLeftArm(currentWeapon.supportGrip.position, right, up, armWeight);
            }
        }

        /// <summary>Pose da arma na mão: abaixada (seguindo a animação) ou mirando (na frente do peito).</summary>
        private void GetHandPose(WeaponBinding binding, Vector3 chestPosition, Vector3 aimDirection, Vector3 forward,
            Vector3 up, float holdWeight, float proneWeight, out Vector3 position, out Quaternion rotation)
        {
            WeaponConfig cfg = binding.config;

            Vector3 animatedHand = rHand != null ? rHand.position : chestPosition - up * 0.3f;
            Quaternion lowRotation = cfg.kind == WeaponKind.Melee
                ? Quaternion.LookRotation((forward * 0.4f - up * 0.9f).normalized, forward)
                : Quaternion.LookRotation((forward * 0.85f - up * 0.5f).normalized, up);

            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, up);
            Vector3 offset = Vector3.Lerp(cfg.aimHoldOffset, cfg.aimHoldOffset + new Vector3(0f, 0.08f, proneGunForward), proneWeight);
            Vector3 aimPosition = chestPosition + aimRotation * offset;

            if (cfg.kind == WeaponKind.Hitscan)
            {
                float sinceShot = Time.time - weapons.LastShotTime;
                aimPosition -= aimDirection * (cfg.recoilDistance * Mathf.Clamp01(1f - sinceShot / 0.08f));
            }

            position = Vector3.Lerp(animatedHand, aimPosition, holdWeight);
            rotation = Quaternion.Slerp(lowRotation, aimRotation, holdWeight);
        }

        /// <summary>
        /// Coldres (usam os eixos do próprio corpo, então funcionam deitado também):
        /// 1 rifle nas costas · 2 pistola na coxa direita · 3 faca na cintura esquerda · 4 gelo no cinto.
        /// </summary>
        private void GetHolsterPose(int slot, Vector3 chestPosition, out Vector3 position, out Quaternion rotation)
        {
            Vector3 bodyUp = transform.up;
            Vector3 bodyForward = transform.forward;
            Vector3 bodyRight = transform.right;
            Vector3 hipPosition = hips != null ? hips.position : transform.position + bodyUp * 0.9f;

            switch (slot)
            {
                case 0: // rifle nas costas, na diagonal
                    position = chestPosition - bodyForward * 0.2f + bodyUp * 0.02f;
                    rotation = Quaternion.LookRotation((-bodyUp * 0.85f + bodyRight * 0.5f).normalized, -bodyForward);
                    break;
                case 1: // pistola na coxa direita
                    Vector3 thigh = rThigh != null ? rThigh.position : hipPosition + bodyRight * 0.1f;
                    position = thigh + bodyRight * 0.13f - bodyUp * 0.16f + bodyForward * 0.03f;
                    rotation = Quaternion.LookRotation(-bodyUp, bodyForward);
                    break;
                case 2: // faca na cintura esquerda, atrás
                    position = hipPosition - bodyRight * 0.17f - bodyForward * 0.1f + bodyUp * 0.02f;
                    rotation = Quaternion.LookRotation((-bodyUp * 0.7f - bodyForward * 0.3f).normalized, -bodyRight);
                    break;
                default: // gelo no cinto, na frente à esquerda
                    position = hipPosition - bodyRight * 0.12f + bodyForward * 0.13f - bodyUp * 0.04f;
                    rotation = Quaternion.LookRotation(-bodyUp, bodyForward);
                    break;
            }
        }

        /// <summary>
        /// Faca: em guarda (mirando) ou golpe em arco (da direita-cima para a esquerda-baixo,
        /// alternando o lado a cada golpe). A faca acompanha a mão depois do IK.
        /// </summary>
        private void KnifeArms(WeaponBinding knife, Vector3 chestPosition, Vector3 aimDirection, Vector3 right, Vector3 up,
            float guardWeight, float inHand)
        {
            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, up);
            float slashWeight = 0f;
            Vector3 bladeDirection = aimDirection;

            if (weapons.IsMeleeAttacking && inHand > 0f)
            {
                float progress = weapons.MeleeProgress;
                float side = weapons.MeleeAttackCount % 2 == 0 ? 1f : -1f;
                Vector3 start = new Vector3(0.32f * side, 0.28f, 0.25f);
                Vector3 control = new Vector3(0.1f * side, 0.05f, 0.7f);
                Vector3 end = new Vector3(-0.22f * side, -0.18f, 0.5f);

                // 0-15%: prepara · 15-60%: corta · 60-100%: volta.
                float t = Mathf.InverseLerp(0.15f, 0.6f, progress);
                Vector3 local = Bezier(start, control, end, t);
                Vector3 tangent = BezierTangent(start, control, end, t);

                slashWeight = (progress < 0.75f ? 1f : Mathf.InverseLerp(1f, 0.75f, progress)) * inHand;
                SolveRightArm(chestPosition + aimRotation * local, aimDirection, right, up, slashWeight);
                if (tangent.sqrMagnitude > 0.0001f) bladeDirection = aimRotation * tangent.normalized;
            }

            float remainingGuard = guardWeight * (1f - slashWeight);
            if (slashWeight <= 0f && remainingGuard > 0f)
                SolveRightArm(knife.model.position, aimDirection, right, up, remainingGuard);

            // Faca na mão depois do IK.
            if (rHand != null && (slashWeight > 0f || remainingGuard > 0f))
                knife.model.SetPositionAndRotation(rHand.position, Quaternion.LookRotation(bladeDirection, up));
        }

        private void SolveRightArm(Vector3 target, Vector3 aimDirection, Vector3 right, Vector3 up, float weight)
        {
            if (rUpper == null) return;
            Vector3 elbowPole = rUpper.position + right * 0.35f - up * 0.5f - aimDirection * 0.2f;
            TwoBoneIK.Solve(rUpper, rFore, rHand, target, elbowPole, weight);
        }

        private void SolveLeftArm(Vector3 target, Vector3 right, Vector3 up, float weight)
        {
            if (lUpper == null) return;
            Vector3 elbowPole = lUpper.position - right * 0.35f - up * 0.5f;
            TwoBoneIK.Solve(lUpper, lFore, lHand, target, elbowPole, weight);
        }

        private void HandsOnLedge(in PlayerSimState s, Vector3 up)
        {
            if (rUpper == null || lUpper == null) return;

            Vector3 normal = s.LedgeNormal;
            Vector3 edge = new Vector3(s.LedgeWallPoint.x, s.LedgeTopY, s.LedgeWallPoint.z) - normal * 0.06f;
            Vector3 wallRight = Vector3.Cross(up, -normal);

            Vector3 rightTarget = edge + wallRight * 0.22f + up * 0.03f;
            Vector3 leftTarget = edge - wallRight * 0.22f + up * 0.03f;
            Vector3 rightPole = rUpper.position + wallRight * 0.4f - up * 0.3f + normal * 0.3f;
            Vector3 leftPole = lUpper.position - wallRight * 0.4f - up * 0.3f + normal * 0.3f;

            TwoBoneIK.Solve(rUpper, rFore, rHand, rightTarget, rightPole, hangWeight);
            TwoBoneIK.Solve(lUpper, lFore, lHand, leftTarget, leftPole, hangWeight);
        }

        // ===================== Utilidades =====================

        private void Play(string clip, float speed, float restartAt)
        {
            AnimationState state = animationComponent[clip];
            if (state == null)
            {
                if (warnedClips.Add(clip))
                    Debug.LogWarning($"[CharacterModelAnimator] Animação '{clip}' não existe no modelo.", this);
                return;
            }

            if (restartAt >= 0f)
            {
                state.time = restartAt;
                animationComponent.CrossFade(clip, 0.05f);
                currentClip = clip;
            }
            else if (clip != currentClip)
            {
                animationComponent.CrossFade(clip, crossFadeTime);
                currentClip = clip;
            }
            state.speed = speed;
        }

        private Transform Bone(Dictionary<string, Transform> bones, string boneName)
        {
            if (bones.TryGetValue(boneName, out Transform bone)) return bone;
            Debug.LogWarning($"[CharacterModelAnimator] Osso '{boneName}' não encontrado no modelo.", this);
            return null;
        }

        private static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return a * (u * u) + b * (2f * u * t) + c * (t * t);
        }

        private static Vector3 BezierTangent(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            return (b - a) * (2f * (1f - t)) + (c - b) * (2f * t);
        }
    }
}
