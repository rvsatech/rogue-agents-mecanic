using Rogue.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Armas no corpo (parte do CharacterModelAnimator), tudo por código depois da animação:
    /// - SACAR: a mão vai até o coldre, pega e traz a arma em arco. Das costas a arma sai por cima do ombro
    ///   direito, de cano para cima, e desce para a frente. GUARDAR: o mesmo caminho ao contrário.
    ///   Trocando de arma, a anterior volta para o coldre primeiro (holsterShare do tempo de sacar) — o tempo
    ///   para poder atirar não muda.
    /// - COICE com mola: a arma vem para trás, sobe e gira um pouco a cada tiro; o peito sacode junto.
    ///   Numa rajada o coice soma (a arma "trabalha" na mão). Mirando, coice menor (recoilAimFactor).
    /// - MECANISMO: o ferrolho da pistola corre a cada tiro; a 12 bombeia (mão esquerda leva a telha para trás e
    ///   para frente); a AWP puxa o ferrolho (mão direita sai do cabo, levanta, puxa, empurra e volta).
    /// - RECARGA: inclina a arma, a mão esquerda tira o pente (o vazio CAI no chão), pega outro no cinto, encaixa
    ///   com um tapa e puxa o ferrolho/alavanca. A 12 põe cartucho por cartucho e bombeia no fim.
    /// - FACA: guarda e golpe em arco.
    /// </summary>
    public sealed partial class CharacterModelAnimator
    {
        [Header("Sacar e guardar")]
        [Tooltip("Parte do tempo de sacar usada para guardar no coldre a arma anterior (0,4 = 40%).")]
        [Range(0f, 0.8f)] [SerializeField] private float holsterShare = 0.4f;

        [Tooltip("Trocando de arma: quanto o peito inclina para frente no meio da troca (graus).")]
        [SerializeField] private float swapChestDip = 9f;
        private bool drawSettled = true;

        [Header("Coice da arma (mola)")]
        [Tooltip("Mola do coice: maior = volta mais rápido.")]
        [SerializeField] private float kickStiffness = 420f;

        [Tooltip("Amortecimento: maior = volta sem balançar.")]
        [SerializeField] private float kickDamping = 26f;

        [Tooltip("Quanto do coice vai para o peito (o tronco sacode junto).")]
        [Range(0f, 1f)] [SerializeField] private float kickChestShare = 0.3f;

        [Header("Recarga")]
        [Tooltip("Momentos da recarga (fração): tira o pente, encaixa o novo, puxa o ferrolho. Iguais aos sons (PlayerAudio).")]
        [SerializeField] private Vector3 reloadMarks = new Vector3(0.2f, 0.7f, 0.86f);

        [Tooltip("Efeitos das armas (o pente vazio cai no chão).")]
        [SerializeField] private WeaponFx fx;

        [Tooltip("Material do cartucho na mão (recarga da 12).")]
        [SerializeField] private Material roundMaterial;

        // Sacar: até ReachEnd a arma fica no coldre (a mão está indo buscar); depois sai em arco até a mão.
        private const float ReachEnd = 0.28f;

        // Coice: uma parte entra na hora e outra vem pela velocidade (pico em ~15 ms, volta com a mola).
        private const float KickInstant = 0.95f;
        private const float KickVelocity = 8f;

        private Vector3[] magazineRestPosition;
        private Quaternion[] magazineRestRotation;
        private Vector3[] actionRestPosition;
        private Quaternion[] actionRestRotation;
        private Vector3[] gripRestPosition;
        private Renderer[][] magazineRenderers;
        private bool[] magazineHidden;
        private Transform heldRound;

        private float lastShotSeen = -999f;
        private Vector3 kickPosition;
        private Vector3 kickPositionVelocity;
        private Vector3 kickAngles;
        private Vector3 kickAngleVelocity;
        private Vector3 actionTilt;
        private int reloadSlotSeen = -1;
        private float previousReloadProgress;
        private bool magazineDropped;

        // ===================== Preparação =====================

        /// <summary>Guarda a posição original das peças (carregador, ferrolho/telha, apoio da mão esquerda).</summary>
        private void InitWeaponParts()
        {
            int count = PlayerWeapons.SlotCount;
            magazineRestPosition = new Vector3[count];
            magazineRestRotation = new Quaternion[count];
            actionRestPosition = new Vector3[count];
            actionRestRotation = new Quaternion[count];
            gripRestPosition = new Vector3[count];
            magazineRenderers = new Renderer[count][];
            magazineHidden = new bool[count];

            for (int i = 0; i < count; i++)
            {
                WeaponBinding binding = weapons != null ? weapons.GetSlot(i) : null;
                if (binding == null) continue;
                if (binding.magazine != null)
                {
                    magazineRestPosition[i] = binding.magazine.localPosition;
                    magazineRestRotation[i] = binding.magazine.localRotation;
                    magazineRenderers[i] = binding.magazine.GetComponentsInChildren<Renderer>(true);
                }
                if (binding.action != null)
                {
                    actionRestPosition[i] = binding.action.localPosition;
                    actionRestRotation[i] = binding.action.localRotation;
                }
                if (binding.supportGrip != null) gripRestPosition[i] = binding.supportGrip.localPosition;
            }

            if (weapons != null) lastShotSeen = weapons.LastShotTime;

            // Cartucho na mão (recarga da 12): cilindro pequeno, sem colisão nem sombra.
            if (roundMaterial != null)
            {
                GameObject round = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                round.name = "Cartucho_Mao";
                Destroy(round.GetComponent<Collider>());
                round.transform.SetParent(transform, false);
                round.transform.localScale = new Vector3(0.021f, 0.035f, 0.021f);
                var renderer = round.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = roundMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                round.SetActive(false);
                heldRound = round.transform;
            }
        }

        /// <summary>Peças de volta ao lugar (a recarga e o mecanismo mexem nelas de novo neste frame).</summary>
        private void ResetWeaponParts()
        {
            for (int i = 0; i < PlayerWeapons.SlotCount; i++)
            {
                WeaponBinding binding = weapons.GetSlot(i);
                if (binding == null) continue;
                if (binding.magazine != null)
                {
                    binding.magazine.SetLocalPositionAndRotation(magazineRestPosition[i], magazineRestRotation[i]);
                    SetMagazineVisible(i, true);
                }
                if (binding.action != null)
                    binding.action.SetLocalPositionAndRotation(actionRestPosition[i], actionRestRotation[i]);
                if (binding.supportGrip != null) binding.supportGrip.localPosition = gripRestPosition[i];
            }

            if (heldRound != null && heldRound.gameObject.activeSelf) heldRound.gameObject.SetActive(false);
            actionTilt = Vector3.zero;
        }

        private void SetMagazineVisible(int slot, bool visible)
        {
            if (magazineHidden[slot] == !visible) return;
            magazineHidden[slot] = !visible;
            Renderer[] renderers = magazineRenderers[slot];
            if (renderers == null) return;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = visible;
        }

        // ===================== Armas =====================

        private void UpdateWeapons(in PlayerSimState s, Vector3 aimDirection, Vector3 forward, Vector3 right, Vector3 up,
            float holdWeight, float proneWeight)
        {
            if (weapons == null) return;

            float dt = Time.deltaTime;
            int current = weapons.CurrentSlot;
            WeaponBinding equipped = weapons.Current;
            WeaponConfig cfg = equipped != null && equipped.IsValid ? equipped.config : null;

            ResetWeaponParts();
            UpdateKick(cfg, dt);

            // O peito sacode junto com o coice (antes do IK: os braços acompanham).
            if (chest != null && kickChestShare > 0f)
                chest.rotation = Quaternion.AngleAxis(kickAngles.x * kickChestShare, right) * chest.rotation;

            Vector3 chestPosition = chest != null ? chest.position : transform.position + up * 1.35f;

            // Tempo da troca: guarda a anterior (putAway 0 -> 1) e depois saca a nova (draw 0 -> 1).
            bool equipping = weapons.IsEquipping;
            float progress = weapons.EquipProgress;
            int holster = weapons.HolsterSlot;
            WeaponBinding leaving = holster >= 0 && holster != current ? weapons.GetSlot(holster) : null;
            if (leaving == null || !leaving.IsValid) holster = -1;
            float split = holster >= 0 ? holsterShare : 0f;
            float putAway = holster >= 0 ? Mathf.Clamp01(progress / Mathf.Max(0.01f, split)) : 1f;
            float draw = equipping ? Mathf.Clamp01((progress - split) / Mathf.Max(0.01f, 1f - split)) : 1f;
            float inHand = 1f - stowWeight; // pendurado/rolando: a arma vai para o coldre

            // Troca de arma com corpo: o peito abaixa um pouco enquanto guarda/saca e a arma "assenta" no fim.
            if (equipping && chest != null)
                chest.rotation = Quaternion.AngleAxis(swapChestDip * Mathf.Sin(progress * Mathf.PI), right) * chest.rotation;
            if (equipping && draw < 0.9f) drawSettled = false;
            else if (!drawSettled && (!equipping || draw >= 0.97f))
            {
                drawSettled = true;
                if (cfg != null && cfg.kind == WeaponKind.Hitscan)
                    Nudge(new Vector3(0f, -0.03f, -0.015f), new Vector3(7f, 0f, 4f)); // cano desce e volta (mola)
            }

            // Recarregando: a arma vem para a frente do peito (mesmo sem estar mirando).
            bool isGun = cfg != null && cfg.kind == WeaponKind.Hitscan;
            float gunReload = isGun ? reloadWeight : 0f;
            if (isGun) holdWeight = Mathf.Max(holdWeight, gunReload);

            // Correndo para trás atirando: a arma vai para trás do ombro direito, apontando para a mira (só a mão direita).
            float backShot = isGun ? Smooth01(backShotWeight) * (1f - gunReload) : 0f;

            // Kit médico: as duas mãos seguram o kit na frente da barriga, olhando para baixo (não segue a mira).
            bool usingItem = cfg != null && cfg.kind == WeaponKind.Consumable;
            if (usingItem)
            {
                holdWeight = 1f;
                aimDirection = (forward * 0.9f - up * 0.45f).normalized;
            }

            // Mecanismo depois do tiro (pistola, 12, AWP): mexe as peças e diz para onde a mão direita vai (ferrolho).
            float boltHand = 0f;
            Vector3 boltTarget = Vector3.zero;
            if (isGun && !equipping && !weapons.IsReloading && current == weapons.LastShotSlot)
                AnimateAction(equipped, current, cfg, Time.time - weapons.LastShotTime, out boltHand, out boltTarget);

            // 1) Armas no lugar: a atual na mão (sacando = saindo do coldre em arco), a anterior voltando para o
            //    coldre (mesmo caminho ao contrário), as outras no coldre.
            for (int i = 0; i < PlayerWeapons.SlotCount; i++)
            {
                WeaponBinding binding = weapons.GetSlot(i);
                if (binding == null || !binding.IsValid) continue;

                GetHolsterPose(i, chestPosition, out Vector3 holsterPosition, out Quaternion holsterRotation);
                bool isCurrent = i == current;
                bool goingAway = i == holster && putAway < 1f;
                if (!isCurrent && !goingAway)
                {
                    binding.model.SetPositionAndRotation(holsterPosition, holsterRotation);
                    continue;
                }

                GetHandPose(binding, chestPosition, aimDirection, forward, up, holdWeight, proneWeight,
                    isCurrent ? gunReload : 0f, out Vector3 handPosition, out Quaternion handRotation);

                if (isCurrent)
                {
                    if (backShot > 0f && rUpper != null)
                    {
                        Vector3 backPosition = rUpper.position + aimDirection * backShotArmReach;
                        handPosition = Vector3.Lerp(handPosition, backPosition, backShot);
                        handRotation = Quaternion.Slerp(handRotation, Quaternion.LookRotation(aimDirection, up), backShot);
                    }

                    // Granada: segurando o botão, o braço fica armado atrás da cabeça; soltou, estica para frente.
                    if (binding.config.kind == WeaponKind.Throwable)
                    {
                        Vector3 windUp = chestPosition + right * 0.22f + up * 0.3f - forward * 0.12f;
                        Vector3 release = chestPosition + right * 0.1f + forward * 0.55f - up * 0.12f;
                        if (weapons.IsThrowing)
                            handPosition = Vector3.Lerp(windUp, release, Smooth01(weapons.ThrowProgress));
                        else if (weapons.ThrowAiming)
                            handPosition = windUp;
                    }

                    // Coice (mola) e balanço do mecanismo (bombeada / ferrolho).
                    if (isGun)
                    {
                        handPosition += Quaternion.LookRotation(aimDirection, up) * kickPosition;
                        handRotation *= Quaternion.Euler(kickAngles + actionTilt);
                    }
                }

                float t = isCurrent ? draw : Mathf.Lerp(1f, ReachEnd, putAway); // guardando: da mão até o coldre
                DrawPath(i, t, chestPosition, holsterPosition, holsterRotation, handPosition, handRotation,
                    out Vector3 position, out Quaternion rotation);

                if (isCurrent && inHand < 1f)
                {
                    position = Vector3.Lerp(holsterPosition, position, inHand);
                    rotation = Quaternion.Slerp(holsterRotation, rotation, inHand);
                }
                binding.model.SetPositionAndRotation(position, rotation);
            }

            if (equipped == null || !equipped.IsValid) return;
            float armWeight = holdWeight * inHand;

            // 2a) Guardando a anterior: a mão direita leva a arma até o coldre (a esquerda solta logo no começo).
            if (holster >= 0 && putAway < 1f)
            {
                SolveDrawArm(leaving.model.position, aimDirection, forward, right, up, inHand);
                if (leaving.config.twoHanded && leaving.supportGrip != null)
                {
                    float leftWeight = armWeight * (1f - Smooth01(putAway / 0.35f));
                    if (leftWeight > 0f) SolveLeftArm(leaving.supportGrip.position, right, up, leftWeight);
                }
                return;
            }

            // 2b) Sacando: a mão vai até o coldre, pega e traz (a esquerda pega no fim).
            if (equipping && draw < 1f)
            {
                Vector3 grip = equipped.model.position;
                Vector3 handTarget = grip;
                if (draw < ReachEnd)
                {
                    Vector3 start = holster >= 0 ? leaving.model.position : rHand != null ? rHand.position : grip;
                    handTarget = Vector3.Lerp(start, grip, Smooth01(draw / ReachEnd));
                }
                // No fim a mão volta a seguir a pose normal (abaixada ou mirando) sem estalo.
                float drawWeight = draw > 0.85f ? Mathf.Lerp(1f, holdWeight, Smooth01((draw - 0.85f) / 0.15f)) : 1f;
                SolveDrawArm(handTarget, aimDirection, forward, right, up, drawWeight * inHand);
                if (equipped.config.twoHanded && equipped.supportGrip != null && draw > 0.75f)
                {
                    float leftWeight = Mathf.Max(armWeight, drawWeight * inHand) * Smooth01((draw - 0.75f) / 0.25f);
                    if (leftWeight > 0f) SolveLeftArm(equipped.supportGrip.position, right, up, leftWeight);
                }
                return;
            }

            // 2c) Arma pronta: braços na arma (IK).
            if (equipped.config.kind == WeaponKind.Melee)
            {
                KnifeArms(equipped, chestPosition, aimDirection, right, up, armWeight, inHand);
                return;
            }

            // Mão direita no cabo (ou no ferrolho da AWP, que funciona mesmo com a arma abaixada).
            float rightWeight = Mathf.Max(armWeight, boltHand * inHand);
            if (rightWeight > 0f)
            {
                Vector3 rightTarget = boltHand > 0f
                    ? Vector3.Lerp(equipped.model.position, boltTarget, boltHand)
                    : equipped.model.position;
                SolveRightArm(rightTarget, aimDirection, right, up, rightWeight);
            }

            if (armWeight > 0f && equipped.config.twoHanded && equipped.supportGrip != null)
            {
                if (gunReload > 0f)
                {
                    ReloadArms(equipped, current, equipped.config, aimDirection, right, up, armWeight);
                }
                else
                {
                    float leftWeight = armWeight * (1f - backShot); // atirando para trás: só a mão direita
                    if (leftWeight > 0f) SolveLeftArm(equipped.supportGrip.position, right, up, leftWeight);
                }
            }
        }

        // ===================== Sacar / guardar =====================

        /// <summary>
        /// Caminho da arma do coldre (t = 0) até a mão (t = 1). Até ReachEnd fica no coldre (a mão está indo buscar);
        /// depois faz uma curva passando pela "saída" (das costas: em cima do ombro direito) até a mão.
        /// </summary>
        private void DrawPath(int slot, float t, Vector3 chestPosition, Vector3 holsterPosition, Quaternion holsterRotation,
            Vector3 handPosition, Quaternion handRotation, out Vector3 position, out Quaternion rotation)
        {
            if (t >= 1f)
            {
                position = handPosition;
                rotation = handRotation;
                return;
            }
            if (t <= ReachEnd)
            {
                position = holsterPosition;
                rotation = holsterRotation;
                return;
            }

            GetExitPose(slot, chestPosition, holsterPosition, holsterRotation, handRotation,
                out Vector3 exitPosition, out Quaternion exitRotation);

            float u = Smooth01((t - ReachEnd) / (1f - ReachEnd));
            Vector3 control = exitPosition * 2f - (holsterPosition + handPosition) * 0.5f; // a curva passa pela saída
            position = Bezier(holsterPosition, control, handPosition, u);
            rotation = u < 0.5f
                ? Quaternion.Slerp(holsterRotation, exitRotation, u * 2f)
                : Quaternion.Slerp(exitRotation, handRotation, u * 2f - 1f);
        }

        /// <summary>
        /// Meio do caminho entre o coldre e a mão. Costas: em cima do ombro direito, de cano para cima e topo da arma
        /// para a direita — assim a arma gira por cima do ombro (nunca pelo meio do corpo). Outros coldres: um pouco
        /// acima do coldre, já meio virada para a mão.
        /// </summary>
        private void GetExitPose(int slot, Vector3 chestPosition, Vector3 holsterPosition, Quaternion holsterRotation,
            Quaternion handRotation, out Vector3 position, out Quaternion rotation)
        {
            Vector3 bodyUp = transform.up;
            Vector3 bodyForward = transform.forward;
            Vector3 bodyRight = transform.right;

            if (IsBackHolster(slot))
            {
                Vector3 shoulder = rUpper != null ? rUpper.position : chestPosition + bodyRight * 0.18f + bodyUp * 0.12f;
                position = shoulder + bodyUp * 0.2f - bodyForward * 0.06f + bodyRight * 0.02f;
                rotation = Quaternion.LookRotation(bodyUp, bodyRight);
                return;
            }

            position = holsterPosition + bodyUp * 0.14f + bodyForward * 0.06f;
            rotation = Quaternion.Slerp(holsterRotation, handRotation, 0.5f);
        }

        /// <summary>Primárias (UMP, AWP, a 12): ficam nas costas.</summary>
        private static bool IsBackHolster(int slot) => slot == 0 || slot == 4 || slot == 6;

        /// <summary>Braço direito buscando/levando a arma: o cotovelo sobe quando a mão vai para trás do ombro.</summary>
        private void SolveDrawArm(Vector3 target, Vector3 aimDirection, Vector3 forward, Vector3 right, Vector3 up, float weight)
        {
            if (rUpper == null || weight <= 0f) return;
            Vector3 shoulder = rUpper.position;
            float behind = Smooth01(Vector3.Dot(shoulder - target, forward) / 0.15f +
                                    Vector3.Dot(target - shoulder, up) / 0.3f);
            Vector3 normalPole = shoulder + right * 0.35f - up * 0.5f - aimDirection * 0.2f;
            Vector3 raisedPole = shoulder + up * 0.5f + right * 0.3f + forward * 0.2f;
            TwoBoneIK.Solve(rUpper, rFore, rHand, target, Vector3.Lerp(normalPole, raisedPole, behind), weight);
        }

        // ===================== Coice =====================

        private void UpdateKick(WeaponConfig cfg, float dt)
        {
            if (weapons.LastShotTime > lastShotSeen)
            {
                lastShotSeen = weapons.LastShotTime;
                if (cfg != null && cfg.kind == WeaponKind.Hitscan && weapons.LastShotSlot == weapons.CurrentSlot)
                {
                    float aim = weapons.IsAiming ? cfg.recoilAimFactor : weapons.IsSliding ? 1.6f : 1f; // deslizando: coice maior
                    float back = cfg.recoilDistance * aim;
                    float tilt = cfg.recoilVisualTilt * aim;
                    Nudge(new Vector3(Random.Range(-0.12f, 0.12f) * back, 0.2f * back, -back),
                        new Vector3(-tilt, Random.Range(-0.25f, 0.25f) * tilt, Random.Range(-0.4f, 0.4f) * tilt));
                }
            }

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.004f), 1, 12);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                kickPositionVelocity += (-kickStiffness * kickPosition - kickDamping * kickPositionVelocity) * h;
                kickPosition += kickPositionVelocity * h;
                kickAngleVelocity += (-kickStiffness * kickAngles - kickDamping * kickAngleVelocity) * h;
                kickAngles += kickAngleVelocity * h;
            }
            kickPosition = Vector3.ClampMagnitude(kickPosition, 0.25f);
            kickAngles = Vector3.ClampMagnitude(kickAngles, 45f);
        }

        /// <summary>
        /// Empurrão na arma: posição em metros (direita, cima, frente da mira) e giro em graus (x negativo = cano
        /// para cima). Usado no tiro, no tapa do pente e no ferrolho.
        /// </summary>
        private void Nudge(Vector3 position, Vector3 angles)
        {
            kickPosition += position * KickInstant;
            kickPositionVelocity += position * KickVelocity;
            kickAngles += angles * KickInstant;
            kickAngleVelocity += angles * KickVelocity;
        }

        // ===================== Mecanismo =====================

        /// <summary>Peças que mexem depois do tiro. Na AWP diz para onde a mão direita vai (bolinha do ferrolho).</summary>
        private void AnimateAction(WeaponBinding gun, int slot, WeaponConfig cfg, float since,
            out float boltHand, out Vector3 boltTarget)
        {
            boltHand = 0f;
            boltTarget = gun.model.position;

            switch (cfg.action)
            {
                case WeaponAction.Blowback:
                {
                    if (gun.action == null) return;
                    // Corre para trás em 12 ms e volta em 60 ms.
                    float back = since < 0.012f ? since / 0.012f : 1f - Mathf.Clamp01((since - 0.012f) / 0.06f);
                    if (back > 0f)
                        gun.action.localPosition = actionRestPosition[slot] + Vector3.back * (cfg.actionTravel * back);
                    break;
                }
                case WeaponAction.Pump:
                {
                    float u = (since - cfg.actionDelay) / Mathf.Max(0.05f, cfg.actionDuration);
                    if (u <= 0f || u >= 1f) return;
                    SetPump(gun, slot, cfg, PumpStroke(u));
                    actionTilt = new Vector3(4f, 0f, -7f) * Mathf.Sin(u * Mathf.PI); // a arma balança na bombeada
                    break;
                }
                case WeaponAction.Bolt:
                {
                    float u = (since - cfg.actionDelay) / Mathf.Max(0.05f, cfg.actionDuration);
                    if (u <= 0f || u >= 1f) return;
                    BoltPose(gun, slot, cfg, u, out boltHand, out boltTarget);
                    actionTilt = new Vector3(0f, 0f, 10f) * boltHand; // deita um pouco: o ferrolho aparece
                    break;
                }
            }
        }

        /// <summary>Bombeada: para trás em 45% do tempo e para frente no resto.</summary>
        private static float PumpStroke(float u) => u < 0.45f ? Smooth01(u / 0.45f) : 1f - Smooth01((u - 0.45f) / 0.55f);

        /// <summary>Telha da 12 para trás (a mão esquerda vai junto: o apoio dela anda com a telha).</summary>
        private void SetPump(WeaponBinding gun, int slot, WeaponConfig cfg, float stroke)
        {
            Vector3 shift = Vector3.back * (cfg.actionTravel * stroke);
            if (gun.action != null) gun.action.localPosition = actionRestPosition[slot] + shift;
            if (gun.supportGrip != null) gun.supportGrip.localPosition = gripRestPosition[slot] + shift;
        }

        /// <summary>
        /// Ferrolho da AWP (u de 0 a 1): mão vai (0-20%), levanta (20-30%), puxa (30-50%), empurra (50-70%),
        /// abaixa (70-80%) e a mão volta para o cabo (80-100%).
        /// </summary>
        private void BoltPose(WeaponBinding gun, int slot, WeaponConfig cfg, float u, out float hand, out Vector3 target)
        {
            hand = u < 0.2f ? Smooth01(u / 0.2f) : u > 0.8f ? 1f - Smooth01((u - 0.8f) / 0.2f) : 1f;
            float lift = Smooth01(Mathf.InverseLerp(0.2f, 0.3f, u)) * (1f - Smooth01(Mathf.InverseLerp(0.7f, 0.8f, u)));
            float travel = Smooth01(Mathf.InverseLerp(0.3f, 0.5f, u)) * (1f - Smooth01(Mathf.InverseLerp(0.5f, 0.7f, u)));

            Transform part = gun.action;
            if (part == null)
            {
                target = gun.model.position;
                return;
            }

            // Gira em volta do eixo do cano (alavanca sobe) e corre para trás.
            Vector3 rest = actionRestPosition[slot];
            var pivot = new Vector3(0f, rest.y, rest.z);
            Quaternion turn = Quaternion.AngleAxis(65f * lift, Vector3.forward);
            part.localPosition = pivot + turn * (rest - pivot) + Vector3.back * (cfg.actionTravel * travel);
            part.localRotation = turn * actionRestRotation[slot];
            target = part.TransformPoint(new Vector3(0.5f, 0f, 0f)); // ponta da alavanca
        }

        // ===================== Recarga =====================

        private void ReloadArms(WeaponBinding weapon, int slot, WeaponConfig cfg, Vector3 aimDirection, Vector3 right,
            Vector3 up, float weight)
        {
            float p = weapons.ReloadProgress;

            // Recarga nova (ou outra arma): o pente vazio ainda não caiu.
            if (slot != reloadSlotSeen || p < previousReloadProgress - 0.05f) magazineDropped = false;
            reloadSlotSeen = slot;

            if (cfg.reloadShellInserts > 0) ShellReload(weapon, slot, cfg, p, right, up, weight);
            else MagazineReload(weapon, slot, cfg, p, aimDirection, right, up, weight);

            previousReloadProgress = p;
        }

        /// <summary>
        /// Troca de pente: a mão esquerda tira o pente (o vazio cai), pega outro no cinto, encaixa (tapa) e puxa o
        /// ferrolho/alavanca. Na AWP quem puxa o ferrolho é a mão direita.
        /// </summary>
        private void MagazineReload(WeaponBinding weapon, int slot, WeaponConfig cfg, float p, Vector3 aimDirection,
            Vector3 right, Vector3 up, float weight)
        {
            Transform gun = weapon.model;
            Vector3 gunDown = -gun.up;
            Vector3 grip = weapon.supportGrip.position;
            Vector3 well = weapon.magazine != null ? gun.TransformPoint(magazineRestPosition[slot]) : grip + gunDown * 0.08f;
            Vector3 pulled = well + gunDown * 0.14f;
            Vector3 below = well + gunDown * 0.12f;
            Vector3 pouch = PouchPosition();
            bool boltRack = cfg.action == WeaponAction.Bolt;
            float travel = Mathf.Max(0.03f, cfg.actionTravel);
            Vector3 rack = weapon.action != null
                ? weapon.action.position
                : gun.TransformPoint(new Vector3(-0.04f, 0.06f, 0.15f));
            Vector3 rackBack = rack - gun.forward * travel;

            float outMark = reloadMarks.x;
            float inMark = reloadMarks.y;
            float rackMark = reloadMarks.z;
            float grab = outMark * 0.6f;                           // mão chega no pente
            float fetch = Mathf.Lerp(outMark, inMark, 0.44f);      // pega o pente novo no cinto
            float align = inMark - 0.08f;                          // pente novo embaixo do encaixe
            float reachRack = Mathf.Lerp(inMark, rackMark, 0.5f);  // mão chega no ferrolho/alavanca

            Vector3 target;
            bool magazineInHand = false;
            bool magazineAway = false;
            if (p < grab)
            {
                target = Vector3.Lerp(grip, well, Smooth01(p / grab));
            }
            else if (p < outMark)
            {
                target = Vector3.Lerp(well, pulled, Smooth01((p - grab) / (outMark - grab)));
                magazineInHand = true;
            }
            else if (p < fetch)
            {
                target = Vector3.Lerp(pulled, pouch, Smooth01((p - outMark) / (fetch - outMark)));
                magazineAway = true;
            }
            else if (p < align)
            {
                target = Vector3.Lerp(pouch, below, Smooth01((p - fetch) / (align - fetch)));
                magazineInHand = true;
            }
            else if (p < inMark)
            {
                target = Vector3.Lerp(below, well, Smooth01((p - align) / (inMark - align)));
                magazineInHand = true;
            }
            else if (boltRack)
            {
                target = Vector3.Lerp(well, grip, Smooth01((p - inMark) / (1f - inMark)));
            }
            else if (p < reachRack)
            {
                target = Vector3.Lerp(well, rack, Smooth01((p - inMark) / (reachRack - inMark)));
            }
            else if (p < rackMark)
            {
                target = Vector3.Lerp(rack, rackBack, Smooth01((p - reachRack) / (rackMark - reachRack)));
            }
            else
            {
                target = Vector3.Lerp(rackBack, grip, Smooth01((p - rackMark) / (1f - rackMark)));
            }

            SolveLeftArm(target, right, up, weight);

            if (weapon.magazine != null)
            {
                // Tirou: uma cópia do pente cai no chão (física); o da arma some até a mão trazer o novo do cinto.
                if (!magazineDropped && p >= outMark)
                {
                    magazineDropped = true;
                    if (fx != null)
                    {
                        if (lHand != null)
                            weapon.magazine.SetPositionAndRotation(lHand.position - gunDown * 0.04f,
                                gun.rotation * magazineRestRotation[slot]);
                        Vector3 body = motor != null ? motor.State.Velocity : Vector3.zero;
                        fx.DropMagazine(weapon.magazine, gunDown * 0.6f + body);
                    }
                }

                if (magazineAway)
                {
                    SetMagazineVisible(slot, false);
                }
                else if (magazineInHand && lHand != null)
                {
                    weapon.magazine.SetPositionAndRotation(lHand.position - gunDown * 0.04f,
                        gun.rotation * magazineRestRotation[slot]);
                }
            }

            // Tapa no pente novo e puxada do ferrolho: a arma dá um tranco.
            if (previousReloadProgress < inMark && p >= inMark)
                Nudge(new Vector3(0f, 0.012f, 0f), new Vector3(-5f, 0f, 2f));
            if (!boltRack && previousReloadProgress < rackMark && p >= rackMark)
                Nudge(new Vector3(0f, 0f, -0.015f), new Vector3(-3f, 0f, 0f));

            // Ferrolho/alavanca corre para trás junto com a mão e volta solto.
            if (!boltRack && weapon.action != null && p >= reachRack)
            {
                float pull = p < rackMark
                    ? Smooth01((p - reachRack) / (rackMark - reachRack))
                    : 1f - Mathf.Clamp01((p - rackMark) / 0.03f);
                if (pull > 0f) weapon.action.localPosition = actionRestPosition[slot] + Vector3.back * (travel * pull);
            }

            // AWP: depois de encaixar o pente, a mão direita puxa o ferrolho.
            if (boltRack && p > inMark)
            {
                BoltPose(weapon, slot, cfg, Mathf.InverseLerp(inMark, 1f, p), out float hand, out Vector3 boltTarget);
                if (hand > 0f)
                    SolveRightArm(Vector3.Lerp(weapon.model.position, boltTarget, hand), aimDirection, right, up, weight * hand);
            }
        }

        /// <summary>
        /// A 12: a arma deita de lado, a mão esquerda busca um cartucho no cinto e empurra pela entrada de baixo,
        /// reloadShellInserts vezes; no fim volta para a telha e bombeia.
        /// </summary>
        private void ShellReload(WeaponBinding weapon, int slot, WeaponConfig cfg, float p, Vector3 right, Vector3 up, float weight)
        {
            Transform gun = weapon.model;
            Vector3 port = gun.TransformPoint(new Vector3(0f, -0.045f, 0.1f));
            Vector3 below = port - gun.up * 0.07f;
            Vector3 pouch = PouchPosition();
            int count = Mathf.Max(1, cfg.reloadShellInserts);
            float start = WeaponConfig.ShellReloadStart;
            float end = WeaponConfig.ShellReloadEnd;
            float rackMark = reloadMarks.z;

            // Bombeada no fim: a telha (e o apoio da mão esquerda) corre para trás e volta.
            if (p >= rackMark) SetPump(weapon, slot, cfg, PumpStroke(Mathf.InverseLerp(rackMark, 0.98f, p)));
            Vector3 grip = weapon.supportGrip.position;

            Vector3 target;
            bool round = false;
            if (p < start)
            {
                target = Vector3.Lerp(grip, pouch, Smooth01(p / start));
            }
            else if (p < end)
            {
                float span = (end - start) / count;
                int index = Mathf.Min(count - 1, Mathf.FloorToInt((p - start) / span));
                float u = (p - start - index * span) / span;
                Vector3 from = index == 0 ? pouch : port;
                if (u < 0.35f)
                {
                    target = Vector3.Lerp(from, pouch, Smooth01(u / 0.35f));
                }
                else if (u < 0.75f)
                {
                    target = Vector3.Lerp(pouch, below, Smooth01((u - 0.35f) / 0.4f));
                    round = true;
                }
                else if (u < 0.88f)
                {
                    target = Vector3.Lerp(below, port, Smooth01((u - 0.75f) / 0.13f));
                    round = true;
                }
                else
                {
                    target = port;
                }
            }
            else if (p < rackMark)
            {
                target = Vector3.Lerp(port, grip, Smooth01((p - end) / (rackMark - end)));
            }
            else
            {
                target = grip;
            }

            SolveLeftArm(target, right, up, weight);

            // Cartucho vermelho na mão, deitado como na arma.
            if (round && heldRound != null && lHand != null)
            {
                heldRound.gameObject.SetActive(true);
                heldRound.SetPositionAndRotation(lHand.position + gun.forward * 0.01f, gun.rotation * Quaternion.Euler(90f, 0f, 0f));
            }

            // Cada cartucho que entra dá um toquinho na arma; a bombeada no fim, um tranco.
            for (int i = 0; i < count; i++)
            {
                float mark = cfg.ShellInsertMark(i);
                if (previousReloadProgress < mark && p >= mark) Nudge(new Vector3(0f, 0.006f, 0f), new Vector3(-1.5f, 0f, 0f));
            }
            if (previousReloadProgress < rackMark && p >= rackMark) Nudge(new Vector3(0f, 0f, -0.01f), new Vector3(-2f, 0f, 0f));
        }

        /// <summary>Bolsa de pentes/cartuchos: no cinto, na frente do quadril esquerdo.</summary>
        private Vector3 PouchPosition()
        {
            Vector3 hipPosition = hips != null ? hips.position : transform.position + transform.up * 0.9f;
            return hipPosition - transform.right * 0.17f + transform.forward * 0.08f + transform.up * 0.02f;
        }

        // ===================== Poses =====================

        /// <summary>Pose da arma na mão: abaixada (seguindo a animação) ou mirando (na frente do peito).</summary>
        private void GetHandPose(WeaponBinding binding, Vector3 chestPosition, Vector3 aimDirection, Vector3 forward,
            Vector3 up, float holdWeight, float proneWeight, float reload, out Vector3 position, out Quaternion rotation)
        {
            WeaponConfig cfg = binding.config;

            Vector3 animatedHand = rHand != null ? rHand.position : chestPosition - up * 0.3f;
            Quaternion lowRotation = cfg.kind == WeaponKind.Melee
                ? Quaternion.LookRotation((forward * 0.4f - up * 0.9f).normalized, forward)
                : Quaternion.LookRotation((forward * 0.85f - up * 0.5f).normalized, up);

            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, up);
            Vector3 offset = Vector3.Lerp(cfg.aimHoldOffset, proneHoldOffset, proneWeight);

            // Recarregando: arma inclinada (cano para baixo e de lado) e mais perto do corpo.
            // A 12 deita mais de lado (mostra a entrada dos cartuchos embaixo).
            if (reload > 0f)
            {
                bool shells = cfg.reloadShellInserts > 0;
                offset += new Vector3(-0.03f, -0.06f, -0.08f) * reload;
                aimRotation *= Quaternion.Euler((shells ? 14f : 28f) * reload, 0f, (shells ? -48f : -22f) * reload);
            }

            Vector3 aimPosition = chestPosition + Quaternion.LookRotation(aimDirection, up) * offset;
            position = Vector3.Lerp(animatedHand, aimPosition, holdWeight);
            rotation = Quaternion.Slerp(lowRotation, aimRotation, holdWeight);
        }

        /// <summary>
        /// Coldres (usam os eixos do próprio corpo, então funcionam deitado também):
        /// primária (UMP, AWP ou a 12) nas costas na diagonal — cabo atrás do ombro direito, cano para baixo à esquerda ·
        /// 2 pistola na coxa direita · 3 faca na cintura esquerda · E gelo no cinto · 4 kit médico numa bolsa atrás,
        /// no quadril direito · G granada, flash e smoke no cinto, na frente à direita.
        /// </summary>
        private void GetHolsterPose(int slot, Vector3 chestPosition, out Vector3 position, out Quaternion rotation)
        {
            Vector3 bodyUp = transform.up;
            Vector3 bodyForward = transform.forward;
            Vector3 bodyRight = transform.right;
            Vector3 hipPosition = hips != null ? hips.position : transform.position + bodyUp * 0.9f;

            switch (slot)
            {
                case 0: // UMP
                case 4: // AWP (comprida: fica um pouco mais alta)
                case 6: // a 12
                {
                    // Deitada nas costas: cano para baixo e para a esquerda, topo da arma para cima e para a esquerda.
                    Vector3 barrel = (-bodyUp * 0.85f - bodyRight * 0.5f).normalized;
                    Vector3 top = (-bodyRight * 0.85f + bodyUp * 0.5f).normalized;
                    float lift = slot == 4 ? 0.06f : 0f;
                    position = chestPosition - bodyForward * 0.2f + bodyRight * 0.1f - bodyUp * (0.06f - lift);
                    rotation = Quaternion.LookRotation(barrel, top);
                    break;
                }
                case 1: // pistola na coxa direita
                    Vector3 thigh = rThigh != null ? rThigh.position : hipPosition + bodyRight * 0.1f;
                    position = thigh + bodyRight * 0.13f - bodyUp * 0.16f + bodyForward * 0.03f;
                    rotation = Quaternion.LookRotation(-bodyUp, bodyForward);
                    break;
                case 2: // faca na cintura esquerda, atrás
                    position = hipPosition - bodyRight * 0.17f - bodyForward * 0.1f + bodyUp * 0.02f;
                    rotation = Quaternion.LookRotation((-bodyUp * 0.7f - bodyForward * 0.3f).normalized, -bodyRight);
                    break;
                case 7: // granada, flash e smoke: no cinto, na frente à direita, lado a lado
                case 8:
                case 9:
                    position = hipPosition + bodyForward * 0.15f + bodyRight * (0.06f + 0.065f * (slot - 7)) + bodyUp * 0.06f;
                    rotation = Quaternion.LookRotation(bodyForward, bodyUp);
                    break;
                case 5: // kit médico: bolsa atrás, no quadril direito (a cruz vermelha aparece para a câmera)
                    position = hipPosition + bodyRight * 0.04f - bodyForward * 0.2f + bodyUp * 0.02f;
                    rotation = Quaternion.LookRotation(-bodyForward, bodyUp);
                    break;
                default: // gelo no cinto, na frente à esquerda
                    position = hipPosition - bodyRight * 0.12f + bodyForward * 0.13f - bodyUp * 0.04f;
                    rotation = Quaternion.LookRotation(-bodyUp, bodyForward);
                    break;
            }
        }

        // ===================== Faca e IK dos braços =====================

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
                var start = new Vector3(0.32f * side, 0.28f, 0.25f);
                var control = new Vector3(0.1f * side, 0.05f, 0.7f);
                var end = new Vector3(-0.22f * side, -0.18f, 0.5f);

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
    }
}
