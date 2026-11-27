using System;
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
    ///    - IK dos braços: mãos na arma; golpe de faca em arco; recarga (mão esquerda troca o
    ///      carregador); mãos na beirada.
    ///    - Slide: quadril baixo, tronco para trás, perna da frente esticada.
    ///    - Deitado (barriga para cima): cabeça levantada olhando para os pés/mira, arma sobre a barriga.
    /// 3) Avisa os passos (evento Footstep) quando um pé encosta no chão — o som fica no ritmo da animação.
    /// Animações novas da UAL2 (se o menu Rogue ligou): slide de verdade (desce e fica deslizando), subida da
    /// beirada com as pernas, arremesso com o tronco (só da cintura para cima) e pouso agachando nas quedas grandes.
    /// As armas (sacar/guardar, coice, mecanismo, recarga) ficam em CharacterModelAnimator.Weapons.cs.
    /// Feito para o manequim da Universal Animation Library (Quaternius); os nomes das
    /// animações e dos ossos ficam no Inspector para trocar de modelo depois.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class CharacterModelAnimator : MonoBehaviour
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
        [Tooltip("Andar devagar (modo xiu, lentidão ao levantar).")]
        [SerializeField] private string walkClip = "Walk_Loop";
        [SerializeField] private string runClip = "Jog_Fwd_Loop";
        [SerializeField] private string sprintClip = "Sprint_Loop";
        [SerializeField] private string crouchIdleClip = "Crouch_Idle_Loop";
        [SerializeField] private string crouchMoveClip = "Crouch_Fwd_Loop";
        [SerializeField] private string airClip = "Jump_Loop";
        [Tooltip("Pulo correndo: pose de passada aberta (uma perna na frente, outra atrás), tirada da corrida e quase " +
                 "parada no ar. Abaixo de leapMinSpeed (ou caindo de alto) usa airClip.")]
        [SerializeField] private float leapPoseTime = 0.17f;
        [SerializeField] private float leapClipSpeed = 0.2f;
        [SerializeField] private float leapMinSpeed = 3.5f;
        [SerializeField] private float leapMaxFallSpeed = 9f;
        [SerializeField] private string wallRunClip = "Sprint_Loop";
        [SerializeField] private string hangClip = "Jump_Loop";
        [SerializeField] private string climbClip = "Crouch_Idle_Loop";
        [SerializeField] private string slideClip = "Crouch_Idle_Loop";
        [SerializeField] private string rollClip = "Roll";
        [SerializeField] private string proneClip = "Idle_Loop";
        [SerializeField] private float crossFadeTime = 0.18f;

        [Tooltip("Trecho da animação de cambalhota usado (segundos): pula a preparação e o levantar lento.")]
        [SerializeField] private float rollClipStart = 0.42f;
        [SerializeField] private float rollClipEnd = 1.0f; // termina no fim do giro: o levantar vira a corrida (mistura)

        [Header("Animações novas (UAL2 — usadas se existirem no modelo)")]
        [Tooltip("Clipes adicionados ao modelo ao começar (o menu Rogue liga os da Universal Animation Library 2).")]
        [SerializeField] private AnimationClip[] extraClips;

        [Header("Personagens (troca de modelo no jogo)")]
        [Tooltip("Todas as animações do manequim (o menu Rogue liga). Entram em qualquer modelo trocado no jogo " +
                 "(os agentes do gerador de personagens têm os mesmos ossos do manequim).")]
        [SerializeField] private AnimationClip[] baseClips;

        [Tooltip("Altura do quadril deste corpo ÷ a do manequim das animações. Corpo de perna mais comprida " +
                 "sobe o quadril na mesma proporção (o pé continua no chão).")]
        [SerializeField] private float hipsScale = 1f;

        [Tooltip("Objeto do modelo que recebe o componente Animation quando o modelo não tem um (vazio = a raiz).")]
        [SerializeField] private string animationHostPath = "";

        [Tooltip("Slide: descida rápida para o slide (só o trecho slideStartFrom..slideStartTo) e depois o slide parado.")]
        [SerializeField] private string slideStartClip = "Slide_Start";
        [SerializeField] private string slideLoopClip = "Slide_Loop";
        [SerializeField] private float slideStartFrom = 0.12f;
        [SerializeField] private float slideStartTo = 0.5f;
        [SerializeField] private float slideStartSpeed = 2.4f;

        [Tooltip("Fim do slide: levanta do chão bem rápido (Slide_Exit) e já emenda na corrida.")]
        [SerializeField] private string slideExitClip = "Slide_Exit";
        [SerializeField] private float slideExitFrom = 0.05f;
        [SerializeField] private float slideExitSpeed = 2f;

        [Tooltip("Subida da beirada (mãos na borda e pernas subindo). Toca no tempo que o motor leva para subir.")]
        [SerializeField] private string mantleClip = "ClimbUp_1m";
        [Tooltip("Trecho usado: subida direta (do chão) começa em mantleFrom; do pendurado, em mantleFromHang.")]
        [SerializeField] private float mantleFrom = 0.08f;
        [SerializeField] private float mantleFromHang = 0.18f;
        [SerializeField] private float mantleTo = 0.62f;

        [Tooltip("Pouso de queda grande, parado: agacha e levanta (da UAL1).")]
        [SerializeField] private string landClip = "Jump_Land";
        [Tooltip("Queda (m/s para baixo) a partir da qual o pouso parado agacha. Pulo normal pousa a ~8,4 m/s; " +
                 "11 = queda de uns 2 m.")]
        [SerializeField] private float landHardSpeed = 11f;
        [SerializeField] private float landClipFrom = 0.1f;
        [SerializeField] private float landClipTo = 0.75f;
        [SerializeField] private float landClipSpeed = 1.3f;

        [Tooltip("Arremesso (só da cintura para cima): segurando = braço armado; soltou = tronco vai junto.")]
        [SerializeField] private string throwClip = "OverhandThrow";
        [SerializeField] private float throwWindUpTime = 0.19f;
        [SerializeField] private float throwReleaseTime = 0.3f;
        [SerializeField] private float throwEndTime = 0.8f;

        [Header("Velocidade das animações (m/s em que a animação fica natural)")]
        [SerializeField] private float runAnimationSpeed = 6f;

        [Tooltip("Velocidade (m/s) em que a animação de andar fica natural; abaixo de walkMaxSpeed usa andar em vez de correr.")]
        [SerializeField] private float walkAnimationSpeed = 1.7f;
        [SerializeField] private float walkMaxSpeed = 3f;
        [SerializeField] private float sprintAnimationSpeed = 7.5f;
        [SerializeField] private float crouchAnimationSpeed = 2.4f;

        [Header("Ossos (nomes no modelo)")]
        [SerializeField] private string hipsBone = "DEF-hips";
        [SerializeField] private string spineBone = "DEF-spine.001";
        [SerializeField] private string chestBone = "DEF-spine.003";
        [SerializeField] private string neckBone = "DEF-neck";
        [SerializeField] private string headBone = "DEF-head";
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

        [Tooltip("Quanto a cabeça levanta deitado (de costas) para olhar para frente (graus).")]
        [SerializeField] private float proneHeadPitch = 40f;

        [Tooltip("Deitado de costas: quanto o peito levanta (graus), como quem olha para os pés.")]
        [SerializeField] private float proneChestLift = 10f;

        [Tooltip("Onde a arma fica deitado, em relação ao peito (direita, cima, frente): sobre a barriga.")]
        [SerializeField] private Vector3 proneHoldOffset = new Vector3(0.05f, 0.3f, 0.2f);

        [Header("Parede")]
        [Tooltip("Correndo na parede em combate: quanto o tronco gira para a mira (o corpo fica ao longo da parede).")]
        [SerializeField] private float wallRunTorsoTwist = 90f;

        [Tooltip("Correndo com a parede à esquerda: a mão esquerda vai na parede (a direita segura a arma).")]
        [SerializeField] private bool wallRunHandOnWall = true;

        [Tooltip("Escalando o muro: passos por segundo (mãos e pés alternando).")]
        [SerializeField] private float wallClimbStepRate = 2.6f;

        [Header("Slide")]
        [Tooltip("Deslizando atirando: quanto o tronco gira para a mira, no máximo (as pernas ficam no slide). " +
                 "A arma alcança 30° além disso; mirando mais para trás, a cabeça e o braço viram para trás.")]
        [SerializeField] private float slideTorsoTwist = 90f;

        [Header("Atirando correndo para trás")]
        [Tooltip("Quanto o tronco gira para a direita (o ombro da arma vai para trás).")]
        [SerializeField] private float backShotTorsoTwist = 40f;

        [Tooltip("Quanto a cabeça gira além do tronco (olha para trás por cima do ombro direito).")]
        [SerializeField] private float backShotHeadTurn = 75f;

        [Tooltip("Distância do ombro até a mão com o braço esticado para trás (metros).")]
        [SerializeField] private float backShotArmReach = 0.5f;

        [Header("Passos (som)")]
        [Tooltip("O pé conta como 'no chão' abaixo desta altura (acima da altura dele apoiado), em metros.")]
        [SerializeField] private float footContactHeight = 0.025f;

        [Tooltip("Quanto o pé precisa subir para o próximo passo contar (metros).")]
        [SerializeField] private float footLiftHeight = 0.05f;

        /// <summary>Um pé encostou no chão (ou na parede). Usado pelos sons de passo.</summary>
        public event Action<FootstepInfo> Footstep;

        private float leftFootRest = 0.08f, rightFootRest = 0.08f;
        private bool leftFootDown = true, rightFootDown = true;
        private int wallStepIndex = int.MinValue;

        private Transform hips, spine, chest, neck, head;
        private Transform rUpper, rFore, rHand, lUpper, lFore, lHand;
        private Transform rThigh, rShin, rFoot, lThigh, lShin, lFoot;

        private string currentClip;
        private MotorMode lastMode;
        private bool slideClipPose;
        private bool mantleClipPose;
        private float mantleSpeed = 1f;
        private bool wasGrounded = true;
        private float lowestAirSpeed;
        // Animação de uma vez só parado (pouso forte, levantar do slide): sai na hora se andar/correr.
        private string oneShotClip;
        private float oneShotTimer;
        private float oneShotSpeed = 1f;
        private float oneShotFrom;
        private bool oneShotRestart;
        private bool oneShotAnySpeed;
        private float oneShotAge;
        private float oneShotCrouchGrace;
        private const string ThrowLayerState = "Arremesso_Tronco";
        private const string LeapState = "Salto_Corrida";
        private float climbWeight;
        private float climbPhase;
        private float wallHandWeight;
        private AnimationState throwState;
        private float throwWeight;
        private float throwTime;
        private float legTwist;
        private float torsoTwist;
        private float slideWeight;
        private float hangWeight;
        private float combatWeight;
        private float stowWeight;
        private float reloadWeight;
        private float backShotWeight;
        private readonly HashSet<string> warnedClips = new HashSet<string>();
        // Proporções do corpo: as animações gravam o tamanho dos ossos do manequim; todo frame os ossos
        // voltam para o tamanho do corpo atual (e o quadril sobe/desce na escala dele).
        private Transform[] restBones;
        private Vector3[] restPositions;
        private Vector3 scaledHips;

        /// <summary>Raiz do modelo 3D atual.</summary>
        public Transform Model => model;

        private void Awake()
        {
            // Posição original das peças das armas (carregador, ferrolho, apoio da mão): voltam para lá todo frame.
            InitWeaponParts();
            BindModel(model, hipsScale, animationHostPath);
        }

        /// <summary>
        /// Liga (ou troca, no meio do jogo) o modelo 3D: acha os ossos, coloca as animações no componente
        /// Animation do modelo e guarda o tamanho dos ossos dele. newHipsScale: altura do quadril ÷ a do manequim.
        /// </summary>
        public void BindModel(Transform newModel, float newHipsScale = 1f, string newAnimationHostPath = null)
        {
            model = newModel;
            hipsScale = newHipsScale > 0.01f ? newHipsScale : 1f;
            animationHostPath = newAnimationHostPath ?? "";
            hips = spine = chest = neck = head = null;
            rUpper = rFore = rHand = lUpper = lFore = lHand = null;
            rThigh = rShin = rFoot = lThigh = lShin = lFoot = null;
            animationComponent = null;
            throwState = null;
            throwWeight = 0f;
            throwTime = 0f;
            currentClip = null;
            oneShotClip = null;
            oneShotTimer = 0f;
            restBones = null;
            restPositions = null;
            scaledHips = new Vector3(float.NaN, 0f, 0f);
            warnedClips.Clear();

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
                head = Bone(bones, headBone);
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

            // Altura do tornozelo apoiado (pose inicial, medida do pé do personagem); depois se ajusta sozinha.
            if (lFoot != null) leftFootRest = Mathf.Clamp(lFoot.position.y - transform.position.y, 0.02f, 0.3f);
            if (rFoot != null) rightFootRest = Mathf.Clamp(rFoot.position.y - transform.position.y, 0.02f, 0.3f);

            // Tamanho dos ossos deste corpo (tudo abaixo do quadril), antes de qualquer animação.
            if (hips != null)
            {
                Transform[] below = hips.GetComponentsInChildren<Transform>(true);
                var bones = new List<Transform>(below.Length);
                foreach (Transform t in below)
                {
                    if (t == hips || t.GetComponent<Renderer>() != null) continue;
                    bones.Add(t);
                }
                restBones = bones.ToArray();
                restPositions = new Vector3[restBones.Length];
                for (int i = 0; i < restBones.Length; i++) restPositions[i] = restBones[i].localPosition;
            }

            if (model != null)
            {
                animationComponent = model.GetComponentInChildren<Animation>();
                // Modelo trocado no jogo (agente): não vem com animações; usa as do manequim.
                if (animationComponent == null && baseClips != null && baseClips.Length > 0)
                {
                    Transform host = string.IsNullOrEmpty(animationHostPath) ? null : model.Find(animationHostPath);
                    if (host == null) host = model;
                    // As animações do manequim procuram "Rig/root/DEF-hips/..." a partir de quem tem o Animation.
                    // Modelo importado sem animação e com uma raiz só chega com a raiz = o próprio "Rig":
                    // aí ganha um pai em volta, para os caminhos baterem.
                    if (host == model && model.Find(RigNodeName) == null && model.Find(RootBoneName) != null)
                    {
                        model = WrapModel(model);
                        host = model;
                    }
                    animationComponent = host.gameObject.AddComponent<Animation>();
                }
            }

            if (animationComponent == null)
            {
                Debug.LogWarning("[CharacterModelAnimator] O modelo não tem componente Animation. " +
                                 "Use o menu Rogue > Criar cena Sandbox_Movement (ele configura o import como Legacy).", this);
                return;
            }

            // Clipes do manequim (para modelos trocados) e extras (UAL2): entram no componente de animação do modelo.
            AddClips(baseClips);
            AddClips(extraClips);

            animationComponent.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (AnimationState state in animationComponent)
                state.wrapMode = WrapMode.Loop;

            foreach (string once in new[] { rollClip, slideStartClip, slideExitClip, mantleClip, landClip })
            {
                AnimationState state = animationComponent[once];
                if (state != null) state.wrapMode = WrapMode.ClampForever;
            }

            // Pulo correndo: cópia da corrida com outro nome (pose de passada aberta, quase parada no ar).
            AnimationState sprintSource = HasClip(sprintClip) ? animationComponent[sprintClip] : null;
            if (sprintSource != null && sprintSource.clip != null && animationComponent[LeapState] == null)
            {
                animationComponent.AddClip(sprintSource.clip, LeapState);
                AnimationState leap = animationComponent[LeapState];
                if (leap != null) leap.wrapMode = WrapMode.Loop;
            }

            // Sem as animações novas, o slide e a subida usam as poses feitas por código (como antes).
            slideClipPose = HasClip(slideLoopClip);
            mantleClipPose = HasClip(mantleClip);

            // Arremesso: cópia do clipe numa camada de cima, só do tronco para cima (as pernas continuam correndo).
            AnimationState throwSource = HasClip(throwClip) ? animationComponent[throwClip] : null;
            if (throwSource != null && throwSource.clip != null && spine != null)
            {
                animationComponent.AddClip(throwSource.clip, ThrowLayerState);
                throwState = animationComponent[ThrowLayerState];
                if (throwState != null)
                {
                    throwState.layer = 1;
                    throwState.AddMixingTransform(spine, true);
                    throwState.wrapMode = WrapMode.ClampForever;
                    throwState.weight = 0f;
                    throwState.speed = 0f;
                    throwState.enabled = false;
                }
            }
        }

        private bool HasClip(string clip) => animationComponent != null && !string.IsNullOrEmpty(clip) && animationComponent[clip] != null;

        private const string RigNodeName = "Rig";
        private const string RootBoneName = "root";

        /// <summary>Põe um objeto-pai em volta do "Rig" (mesmo nome, lugar e tamanho) e devolve o pai.</summary>
        private static Transform WrapModel(Transform rig)
        {
            var wrapper = new GameObject(rig.name).transform;
            wrapper.SetParent(rig.parent, false);
            wrapper.localPosition = rig.localPosition;
            wrapper.localRotation = rig.localRotation;
            wrapper.localScale = rig.localScale;
            rig.SetParent(wrapper, false);
            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.identity;
            rig.localScale = Vector3.one;
            rig.name = RigNodeName;
            return wrapper;
        }

        private void AddClips(AnimationClip[] clips)
        {
            if (clips == null) return;
            foreach (AnimationClip clip in clips)
            {
                if (clip == null || animationComponent[clip.name] != null) continue;
                if (!clip.legacy)
                {
                    Debug.LogWarning($"[CharacterModelAnimator] '{clip.name}' não é Legacy (rode o menu Rogue de novo).", this);
                    continue;
                }
                animationComponent.AddClip(clip, clip.name);
            }
        }

        /// <summary>
        /// Depois da animação: cada osso volta para o tamanho do corpo atual (as animações vêm com o tamanho
        /// do manequim) e o quadril sobe/desce na escala do corpo. No manequim não muda nada.
        /// </summary>
        private void ApplyBodyProportions()
        {
            if (restBones == null) return;
            for (int i = 0; i < restBones.Length; i++)
            {
                if (restBones[i] != null) restBones[i].localPosition = restPositions[i];
            }

            if (hips == null || Mathf.Abs(hipsScale - 1f) < 0.0001f) return;
            Vector3 p = hips.localPosition;
            if (p == scaledHips) return; // a animação não mexeu no quadril desde a última vez
            scaledHips = p * hipsScale;
            hips.localPosition = scaledHips;
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
            legTwist = Mathf.MoveTowards(legTwist, targetTwist, 600f * dt);

            // Parede e slide: o corpo segue a parede/o slide; em combate só o tronco gira para a mira (com limite —
            // nunca dá a volta inteira). Mirando para trás no slide, quem vira é a cabeça e o braço (pose de tiro para trás).
            float targetTorso = 0f;
            bool twistMode = s.Mode == MotorMode.WallRun || s.Mode == MotorMode.Slide;
            if (combat && twistMode && input != null && (visual == null || !visual.BackShooting))
            {
                float torsoLimit = s.Mode == MotorMode.WallRun ? wallRunTorsoTwist : slideTorsoTwist;
                Vector3 aimFlat = Quaternion.Euler(0f, input.Yaw, 0f) * Vector3.forward;
                targetTorso = Mathf.Clamp(Vector3.SignedAngle(bodyForward, aimFlat, Vector3.up), -torsoLimit, torsoLimit);
            }
            torsoTwist = Mathf.MoveTowards(torsoTwist, targetTorso, 600f * dt);

            // Pesos das poses feitas por código (sobem/descem suave).
            bool hanging = s.Mode == MotorMode.LedgeHang || s.Mode == MotorMode.LedgeClimb;
            bool rolling = s.Mode == MotorMode.Roll;
            slideWeight = Mathf.MoveTowards(slideWeight, s.Mode == MotorMode.Slide ? 1f : 0f, dt / 0.08f);
            hangWeight = Mathf.MoveTowards(hangWeight, hanging ? 1f : 0f, dt / 0.1f);
            combatWeight = Mathf.MoveTowards(combatWeight, combat ? 1f : 0f, dt / 0.1f);
            bool climbing = s.Mode == MotorMode.WallRun && s.WallClimbing;
            stowWeight = Mathf.MoveTowards(stowWeight, hanging || rolling || climbing ? 1f : 0f, dt / 0.1f);
            climbWeight = Mathf.MoveTowards(climbWeight, climbing ? 1f : 0f, dt / 0.1f);
            if (climbing) climbPhase += dt * wallClimbStepRate;
            bool wallOnLeft = s.Mode == MotorMode.WallRun && !s.WallClimbing && visual != null &&
                              Vector3.Dot(-s.WallNormal, visual.BodyRight) < 0f;
            wallHandWeight = Mathf.MoveTowards(wallHandWeight, wallRunHandOnWall && wallOnLeft ? 1f : 0f, dt / 0.12f);
            bool reloading = weapons != null && weapons.IsReloading;
            reloadWeight = Mathf.MoveTowards(reloadWeight, reloading ? 1f : 0f, dt / 0.12f);
            bool backShooting = visual != null && visual.BackShooting;
            backShotWeight = Mathf.MoveTowards(backShotWeight, backShooting ? 1f : 0f, dt / 0.12f);

            // Pouso: guarda a maior velocidade de queda no ar; pousou forte parado = agacha (animação de pouso).
            if (!s.Grounded) lowestAirSpeed = Mathf.Min(lowestAirSpeed, s.Velocity.y);
            if (s.Grounded && !wasGrounded && s.Mode == MotorMode.Locomotion && -lowestAirSpeed >= landHardSpeed)
                StartOneShot(landClip, landClipFrom, landClipTo, landClipSpeed, anySpeed: false, crouchGrace: 0f);
            if (s.Grounded) lowestAirSpeed = 0f;
            wasGrounded = s.Grounded;

            // Fim do slide no chão: levanta rápido (o motor ainda fica agachado 1 tick antes de levantar).
            if (lastMode == MotorMode.Slide && s.Mode == MotorMode.Locomotion && s.Grounded && slideClipPose)
                StartOneShot(slideExitClip, slideExitFrom, 0.5f, slideExitSpeed, anySpeed: true, crouchGrace: 0.08f);
            if (oneShotTimer > 0f)
            {
                oneShotTimer -= dt;
                oneShotAge += dt;
            }

            if (animationComponent == null)
            {
                lastMode = s.Mode;
                return;
            }

            UpdateThrowLayer(s, dt);

            string clip;
            float clipSpeed = 1f;
            float restartAt = -1f;
            float fade = -1f;

            // Saindo do slide/da subida: troca um pouco mais devagar (sem estalo).
            if ((lastMode == MotorMode.Slide && s.Mode != MotorMode.Slide) ||
                (lastMode == MotorMode.LedgeClimb && s.Mode != MotorMode.LedgeClimb))
                fade = 0.18f;

            switch (s.Mode)
            {
                case MotorMode.Slide:
                    if (slideClipPose)
                    {
                        // Desce rápido para o slide (trecho do Slide_Start) e depois fica no Slide_Loop.
                        AnimationState start = HasClip(slideStartClip) ? animationComponent[slideStartClip] : null;
                        if (start != null && lastMode != MotorMode.Slide)
                        {
                            clip = slideStartClip;
                            clipSpeed = slideStartSpeed;
                            restartAt = slideStartFrom;
                        }
                        else if (start != null && currentClip == slideStartClip && start.time < slideStartTo)
                        {
                            clip = slideStartClip;
                            clipSpeed = slideStartSpeed;
                        }
                        else
                        {
                            clip = slideLoopClip;
                            fade = 0.12f;
                        }
                    }
                    else
                    {
                        clip = slideClip;
                    }
                    break;
                case MotorMode.Roll:
                    clip = rollClip;
                    clipSpeed = (rollClipEnd - rollClipStart) / Mathf.Max(0.05f, s.RollDuration);
                    if (lastMode != MotorMode.Roll) restartAt = rollClipStart;
                    break;
                case MotorMode.WallRun:
                    if (s.WallClimbing)
                    {
                        clip = hangClip; // base parada; mãos e pés alternam no muro por IK (ClimbPose)
                        clipSpeed = 0.5f;
                        break;
                    }
                    clip = wallRunClip;
                    clipSpeed = Mathf.Max(0.8f, s.WallRunSpeed / sprintAnimationSpeed);
                    break;
                case MotorMode.LedgeHang:
                    clip = hangClip;
                    clipSpeed = 0.5f;
                    break;
                case MotorMode.LedgeClimb:
                    if (mantleClipPose)
                    {
                        // Toca a subida no tempo que o motor vai levar (subir até a borda + passar por cima).
                        if (lastMode != MotorMode.LedgeClimb)
                        {
                            float from = s.QuickMantle ? mantleFrom : mantleFromHang;
                            mantleSpeed = (mantleTo - from) / Mathf.Max(0.12f, ExpectedClimbTime(s, cfg));
                            restartAt = from;
                        }
                        clip = mantleClip;
                        clipSpeed = mantleSpeed;
                    }
                    else
                    {
                        clip = s.ClimbPhase == 0 ? hangClip : climbClip;
                    }
                    break;
                case MotorMode.GetUp:
                    // Etapa 1: dobra os joelhos e senta (agachado). Etapa 2: mistura devagar até ficar em pé.
                    if (s.GetUpPhase >= 2)
                    {
                        clip = idleClip;
                        fade = cfg != null ? cfg.crouchToStandTime : 0.5f;
                    }
                    else
                    {
                        clip = crouchIdleClip;
                        fade = 0.3f;
                    }
                    break;
                default:
                    if (s.Prone)
                    {
                        clip = proneClip;
                    }
                    else if (oneShotTimer > 0f && s.Grounded && (oneShotAnySpeed || speed < 2.5f) &&
                             (!s.Crouching || oneShotAge < oneShotCrouchGrace))
                    {
                        // Pouso forte parado (andou/correu, sai na hora) ou levantar do slide: toca uma vez.
                        clip = oneShotClip;
                        clipSpeed = oneShotSpeed;
                        if (oneShotRestart) restartAt = oneShotFrom;
                        oneShotRestart = false;
                    }
                    else if (!s.Grounded)
                    {
                        // Pulo correndo: passada aberta (pernas uma na frente e outra atrás). Caindo de alto: airClip.
                        bool leap = speed >= leapMinSpeed && s.Velocity.y > -leapMaxFallSpeed && HasClip(LeapState);
                        clip = leap ? LeapState : airClip;
                        if (leap)
                        {
                            clipSpeed = leapClipSpeed;
                            if (currentClip != LeapState) animationComponent[LeapState].time = leapPoseTime;
                        }
                    }
                    else if (s.Crouching)
                    {
                        bool moving = speed > 0.3f;
                        clip = moving ? crouchMoveClip : crouchIdleClip;
                        if (moving) clipSpeed = Mathf.Max(0.5f, speed / crouchAnimationSpeed);
                    }
                    else if (speed > 0.3f && (s.QuietWalking || speed < walkMaxSpeed))
                    {
                        clip = walkClip; // devagar: anda de verdade (modo xiu, lentidão ao levantar)
                        clipSpeed = Mathf.Max(0.5f, speed / walkAnimationSpeed);
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

            if (clip != oneShotClip)
            {
                oneShotTimer = 0f; // mexeu: acabou a animação de uma vez
                oneShotRestart = false;
            }

            if (backpedal && (clip == runClip || clip == sprintClip || clip == crouchMoveClip || clip == walkClip))
                clipSpeed = -clipSpeed; // animação de correr tocada ao contrário = correr de ré

            Play(clip, clipSpeed, restartAt, fade);
            lastMode = s.Mode;
        }

        /// <summary>
        /// Começa uma animação de uma vez só (trecho from..to), se ela existir no modelo.
        /// anySpeed: toca mesmo andando. crouchGrace: segundos em que vale mesmo agachado (o motor levanta logo depois).
        /// </summary>
        private void StartOneShot(string clip, float from, float to, float clipSpeed, bool anySpeed, float crouchGrace)
        {
            if (!HasClip(clip)) return;
            oneShotClip = clip;
            oneShotFrom = from;
            oneShotSpeed = Mathf.Max(0.1f, clipSpeed);
            oneShotTimer = Mathf.Max(0.05f, (to - from) / oneShotSpeed);
            oneShotRestart = true;
            oneShotAnySpeed = anySpeed;
            oneShotCrouchGrace = crouchGrace;
            oneShotAge = 0f;
        }

        /// <summary>Tempo que o motor leva na subida: sobe até a borda e anda por cima dela.</summary>
        private float ExpectedClimbTime(in PlayerSimState s, MovementConfig cfg)
        {
            if (cfg == null || motor == null) return 0.5f;
            float multiplier = s.QuickMantle ? cfg.mantleSpeedMultiplier : 1f;
            float rise = Mathf.Max(0.1f, s.LedgeTopY + 0.05f - motor.transform.position.y);
            Vector3 toStand = s.LedgeStandPoint - motor.transform.position;
            float across = new Vector2(toStand.x, toStand.z).magnitude;
            return rise / Mathf.Max(0.1f, cfg.climbUpSpeed * multiplier) +
                   across / Mathf.Max(0.1f, cfg.climbForwardSpeed * multiplier);
        }

        /// <summary>
        /// Arremesso no tronco (camada de cima): segurando o botão, pose de braço armado; soltou, o tronco acompanha
        /// o lançamento. A mão da granada continua no código (UpdateWeapons). Só em pé/andando/no ar.
        /// </summary>
        private void UpdateThrowLayer(in PlayerSimState s, float dt)
        {
            if (throwState == null) return;

            WeaponConfig cfg = weapons != null ? weapons.CurrentConfig : null;
            bool throwable = cfg != null && cfg.kind == WeaponKind.Throwable;
            bool aiming = throwable && weapons.ThrowAiming && !weapons.IsEquipping;
            bool throwing = weapons != null && weapons.IsThrowing;
            bool allowed = s.Mode == MotorMode.Locomotion && !s.Prone && stowWeight < 0.5f;

            float target = allowed && (aiming || throwing) ? 1f : 0f;
            throwWeight = Mathf.MoveTowards(throwWeight, target, dt / (target > throwWeight ? 0.12f : 0.2f));

            if (throwing) throwTime = Mathf.Lerp(throwReleaseTime, throwEndTime, Smooth01(weapons.ThrowProgress));
            else if (aiming) throwTime = throwWindUpTime;

            throwState.enabled = throwWeight > 0.001f;
            throwState.weight = throwWeight;
            throwState.time = throwTime;
            throwState.speed = 0f;
        }

        // ===================== 2) Ajustes por código =====================

        private void LateUpdate()
        {
            if (motor == null || model == null) return;
            ApplyBodyProportions();

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

            // Parede: tronco gira para a mira (o corpo continua ao longo da parede).
            if (spine != null && Mathf.Abs(torsoTwist) > 0.01f)
                spine.rotation = Quaternion.AngleAxis(torsoTwist, up) * spine.rotation;

            // Deitado: a arma fica no cone na frente do corpo (o corpo gira devagar atrás da mira).
            if (proneWeight > 0.5f && visual != null)
            {
                float relative = Mathf.DeltaAngle(visual.BodyYaw, aimYaw);
                float limit = visual.ProneAimFreeAngle + 10f;
                if (Mathf.Abs(relative) > limit)
                    aimDirection = Quaternion.Euler(pitch, visual.BodyYaw + Mathf.Sign(relative) * limit, 0f) * Vector3.forward;
            }

            // Parede e slide: a arma não aponta além do que o tronco alcança (nunca para as costas).
            if (visual != null && (s.Mode == MotorMode.WallRun || (s.Mode == MotorMode.Slide && !visual.BackShooting)))
            {
                float relative = Mathf.DeltaAngle(visual.BodyYaw, aimYaw);
                float limit = (s.Mode == MotorMode.WallRun ? wallRunTorsoTwist : slideTorsoTwist) + 30f;
                if (Mathf.Abs(relative) > limit)
                    aimDirection = Quaternion.Euler(pitch, visual.BodyYaw + Mathf.Sign(relative) * limit, 0f) * Vector3.forward;
            }

            // Slide sem a animação nova: quadril baixo, tronco para trás, pernas por IK na direção do slide.
            if (slideWeight > 0f && hips != null && !slideClipPose)
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

            // Deitado de costas: corpo balança ao rastejar e a cabeça levanta para olhar para frente.
            if (proneWeight > 0f)
            {
                if (hips != null && s.HorizontalSpeed > 0.2f)
                    hips.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * 7f) * 6f * proneWeight, up) * hips.rotation;
                if (neck != null)
                    neck.rotation = Quaternion.AngleAxis(proneHeadPitch * proneWeight, right) * neck.rotation;
            }

            // Correndo para trás atirando: o corpo segue para onde corre; o tronco gira um pouco para a direita e a
            // cabeça olha para trás por cima do ombro direito (o braço da arma aponta para trás em UpdateWeapons).
            if (backShotWeight > 0f)
            {
                float w = Smooth01(backShotWeight);
                if (spine != null) spine.rotation = Quaternion.AngleAxis(backShotTorsoTwist * 0.5f * w, up) * spine.rotation;
                if (chest != null) chest.rotation = Quaternion.AngleAxis(backShotTorsoTwist * 0.5f * w, up) * chest.rotation;
                if (neck != null) neck.rotation = Quaternion.AngleAxis(backShotHeadTurn * 0.45f * w, up) * neck.rotation;
                if (head != null) head.rotation = Quaternion.AngleAxis(backShotHeadTurn * 0.55f * w, up) * head.rotation;
            }

            // Deitado, a arma fica sempre apontada para frente.
            float holdWeight = Mathf.Max(combatWeight, proneWeight);

            // Peito acompanha a mira (cima/baixo). Deitado de costas: o peito só levanta (nunca entra no chão).
            if (chest != null && holdWeight > 0f)
            {
                float chestAngle = pitch * chestAimPitchFactor * holdWeight * (1f - backShotWeight); // para trás: só o braço mira
                if (proneWeight > 0f)
                    chestAngle = Mathf.Lerp(chestAngle, proneChestLift + Mathf.Max(0f, chestAngle), proneWeight);
                chest.rotation = Quaternion.AngleAxis(chestAngle, right) * chest.rotation;
            }

            UpdateWeapons(s, aimDirection, forward, right, up, holdWeight, proneWeight);

            // Mãos na beirada (pendurado / subindo / soltando).
            if (hangWeight > 0f)
                HandsOnLedge(s, up);

            // Escalando o muro: mãos e pés alternando na parede.
            if (climbWeight > 0f)
                ClimbPose(s, root, up);

            // Correndo na parede: mão esquerda na parede (a direita segura a arma).
            if (wallHandWeight > 0f && s.Mode == MotorMode.WallRun)
                HandOnWall(s, forward, up);

            DetectFootsteps(s, root);
        }

        // ===================== Passos =====================

        /// <summary>
        /// No chão: um passo quando o pé desce e encosta (depois de ter subido).
        /// Na parede: um passo a cada meio ciclo da animação de correr.
        /// </summary>
        private void DetectFootsteps(in PlayerSimState s, Vector3 root)
        {
            if (Footstep == null) return;

            if (s.Mode == MotorMode.WallRun && animationComponent != null)
            {
                AnimationState run = animationComponent[wallRunClip];
                if (run != null)
                {
                    int index = Mathf.FloorToInt(run.normalizedTime * 2f);
                    if (wallStepIndex != int.MinValue && index != wallStepIndex)
                    {
                        Vector3 position = (index & 1) == 0 && lFoot != null ? lFoot.position
                            : rFoot != null ? rFoot.position : root;
                        Footstep(new FootstepInfo { Position = position, Speed = s.WallRunSpeed, OnWall = true });
                    }
                    wallStepIndex = index;
                }
                return;
            }
            wallStepIndex = int.MinValue;

            bool walking = s.Mode == MotorMode.Locomotion && s.Grounded && !s.Prone && s.HorizontalSpeed > 0.5f;
            if (!walking || lFoot == null || rFoot == null)
            {
                // Parado, no ar, deitado...: os pés contam como apoiados (o 1º passo vem depois de levantar o pé).
                leftFootDown = true;
                rightFootDown = true;
                return;
            }

            float dt = Time.deltaTime;
            CheckFoot(lFoot, ref leftFootDown, ref leftFootRest, root, s, dt);
            CheckFoot(rFoot, ref rightFootDown, ref rightFootRest, root, s, dt);
        }

        private void CheckFoot(Transform foot, ref bool down, ref float rest, Vector3 root, in PlayerSimState s, float dt)
        {
            float height = foot.position.y - root.y;
            rest = Mathf.Min(rest + 0.05f * dt, height); // aprende a altura do pé apoiado

            if (down)
            {
                if (height > rest + footLiftHeight) down = false;
            }
            else if (height < rest + footContactHeight)
            {
                down = true;
                Footstep?.Invoke(new FootstepInfo
                {
                    Position = foot.position,
                    Speed = s.HorizontalSpeed,
                    Crouching = s.Crouching,
                    OnWall = false
                });
            }
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

        /// <summary>Escalando: mãos alternam no alto do muro, pés alternam embaixo (joelhos dobrados para fora).</summary>
        private void ClimbPose(in PlayerSimState s, Vector3 root, Vector3 up)
        {
            Vector3 n = s.WallNormal;
            Vector3 wallRight = Vector3.Cross(up, -n);
            Vector3 probe = root + up * 1.2f + n * 0.3f;
            Vector3 wall = Physics.Raycast(probe, -n, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + n * 0.04f
                : root - n * 0.4f;
            wall.y = root.y;

            float a = Mathf.Sin(climbPhase * Mathf.PI * 2f) * 0.14f;
            float w = climbWeight;
            if (rUpper != null)
                TwoBoneIK.Solve(rUpper, rFore, rHand, wall + up * (1.85f + a) + wallRight * 0.22f,
                    rUpper.position - up * 0.4f + n * 0.25f + wallRight * 0.35f, w);
            if (lUpper != null)
                TwoBoneIK.Solve(lUpper, lFore, lHand, wall + up * (1.85f - a) - wallRight * 0.22f,
                    lUpper.position - up * 0.4f + n * 0.25f - wallRight * 0.35f, w);
            if (rThigh != null)
                TwoBoneIK.Solve(rThigh, rShin, rFoot, wall + up * (0.45f - a) + wallRight * 0.16f + n * 0.04f,
                    rThigh.position + n * 0.7f + up * 0.2f + wallRight * 0.25f, w);
            if (lThigh != null)
                TwoBoneIK.Solve(lThigh, lShin, lFoot, wall + up * (0.45f + a) - wallRight * 0.16f + n * 0.04f,
                    lThigh.position + n * 0.7f + up * 0.2f - wallRight * 0.25f, w);
        }

        /// <summary>Correndo na parede (à esquerda): a mão esquerda encosta na parede um pouco à frente do ombro.</summary>
        private void HandOnWall(in PlayerSimState s, Vector3 forward, Vector3 up)
        {
            if (lUpper == null || lFore == null || lHand == null) return;
            Vector3 n = s.WallNormal;
            Vector3 shoulder = lUpper.position;
            Vector3 target = Physics.Raycast(shoulder, -n, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point + n * 0.04f
                : shoulder - n * 0.45f;
            target += forward * 0.22f + up * 0.08f;
            Vector3 pole = lFore.position - up * 0.4f - forward * 0.25f + n * 0.2f;
            TwoBoneIK.Solve(lUpper, lFore, lHand, target, pole, wallHandWeight);
        }

        // ===================== Utilidades =====================

        private void Play(string clip, float speed, float restartAt, float fade = -1f)
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
                animationComponent.CrossFade(clip, fade >= 0f ? fade : crossFadeTime);
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
