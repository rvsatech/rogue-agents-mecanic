using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Parkour vertical (agarrar beirada):
    /// - No ar, indo de frente para uma parede, se a beirada estiver ao alcance da mão,
    ///   o personagem agarra. Só alcança o que o pulo permite (blocos muito altos não).
    /// - Obstáculos baixos (até quickMantleMaxHeight): sobe direto, sem ficar pendurado.
    /// - Obstáculos altos: fica PENDURADO com as mãos na beirada.
    ///     Espaço = sobe de vez.   Shift ou S = solta.
    /// - Nada com a layer NonClimbable (gelo) pode ser agarrado.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        private struct LedgeInfo
        {
            public float TopY;
            public float Height;
            public Vector3 Normal;
            public Vector3 WallPoint;
            public Vector3 StandPoint;
        }

        private bool TryFindLedge(Vector3 wishDir, out LedgeInfo ledge)
        {
            ledge = default;

            // Só agarra se estiver segurando uma tecla na direção da parede.
            Vector3 direction = Flat(wishDir);
            if (direction.sqrMagnitude < 0.01f) return false;
            direction.Normalize();

            float radius = controller.radius;
            Vector3 feet = transform.position;
            int mask = config.environmentMask;

            // 1) Tem parede na frente? (raio na altura da cintura)
            Vector3 waist = feet + Vector3.up * Mathf.Max(controller.stepOffset + 0.15f, 0.5f);
            if (!Physics.Raycast(waist, direction, out RaycastHit wall, radius + config.ledgeDetectDistance,
                    mask, QueryTriggerInteraction.Ignore))
                return false;

            if (IsNonClimbable(wall.collider)) return false;
            if (Mathf.Abs(wall.normal.y) > 0.3f) return false;

            Vector3 normal = Flat(wall.normal);
            if (normal.sqrMagnitude < 0.0001f) return false;
            normal.Normalize();

            // Precisa estar indo de frente para a parede.
            if (Vector3.Dot(direction, -normal) < 0.5f) return false;

            // 2) Onde fica o topo? Desce um raio do alcance máximo da mão, um pouco "dentro" do bloco.
            float reachY = feet.y + config.ledgeMaxReach;
            Vector3 probe = wall.point - normal * 0.15f;
            probe.y = reachY + 0.05f;

            // Se o ponto de partida já está dentro de algo, a parede continua acima do alcance.
            if (Physics.CheckSphere(probe, 0.05f, mask, QueryTriggerInteraction.Ignore)) return false;

            float minY = feet.y + config.ledgeMinHeight;
            float castDistance = probe.y - minY;
            if (castDistance <= 0f) return false;

            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, castDistance, mask, QueryTriggerInteraction.Ignore))
                return false;

            if (top.normal.y < 0.7f) return false;          // topo precisa ser "chão"
            if (IsNonClimbable(top.collider)) return false;

            // 3) Cabe em cima (pelo menos agachado)?
            Vector3 stand = new Vector3(wall.point.x, top.point.y, wall.point.z) - normal * (radius + 0.2f);
            float checkRadius = radius * 0.95f;
            Vector3 capsuleBottom = stand + Vector3.up * (checkRadius + 0.05f);
            Vector3 capsuleTop = stand + Vector3.up * Mathf.Max(checkRadius + 0.05f, config.crouchHeight - checkRadius);
            if (Physics.CheckCapsule(capsuleBottom, capsuleTop, checkRadius, mask, QueryTriggerInteraction.Ignore))
                return false;

            ledge = new LedgeInfo
            {
                TopY = top.point.y,
                Height = top.point.y - feet.y,
                Normal = normal,
                WallPoint = wall.point,
                StandPoint = stand
            };
            return true;
        }

        private void StartLedge(in LedgeInfo ledge)
        {
            state.LedgeTopY = ledge.TopY;
            state.LedgeNormal = ledge.Normal;
            state.LedgeWallPoint = ledge.WallPoint;
            state.LedgeStandPoint = ledge.StandPoint;
            state.LedgeTimer = 0f;
            state.ClimbPhase = 0;
            state.Velocity = Vector3.zero;
            state.Grounded = false;
            state.Crouching = false;
            state.SpeedMultiplier = 1f;

            state.QuickMantle = ledge.Height <= config.quickMantleMaxHeight;
            state.Mode = state.QuickMantle ? MotorMode.LedgeClimb : MotorMode.LedgeHang;
        }

        /// <summary>Posição dos pés quando pendurado: encostado na parede, mãos na beirada.</summary>
        private Vector3 HangPosition()
        {
            Vector3 wall = Flat(state.LedgeWallPoint) + state.LedgeNormal * (controller.radius + 0.03f);
            return new Vector3(wall.x, state.LedgeTopY - config.hangHandHeight, wall.z);
        }

        private void SimulateLedgeHang(in PlayerInputFrame frame, float dt)
        {
            state.LedgeTimer += dt;
            UpdateHeight(config.standHeight, dt);

            // Encaixa na posição de pendurado.
            Vector3 toTarget = HangPosition() - transform.position;
            controller.Move(Vector3.ClampMagnitude(toTarget, config.hangSnapSpeed * dt));
            state.Velocity = Vector3.zero;
            state.Grounded = false;

            // Soltar: Shift ou S.
            if (state.LedgeTimer > 0.1f && (frame.CrouchHeld || frame.Move.y < -0.5f))
            {
                state.Mode = MotorMode.Locomotion;
                state.LedgeCooldown = config.ledgeRegrabCooldown;
                return;
            }

            // Subir: Espaço.
            if (state.JumpBufferTimer > 0f && state.LedgeTimer >= config.hangMinTime)
            {
                state.JumpBufferTimer = 0f;
                state.Mode = MotorMode.LedgeClimb;
                state.ClimbPhase = 0;
                state.LedgeTimer = 0f;
                state.QuickMantle = false;
            }
        }

        private void SimulateLedgeClimb(in PlayerInputFrame frame, float dt)
        {
            state.LedgeTimer += dt;
            state.Grounded = false;
            state.Velocity = Vector3.zero;

            // Passa pela beirada agachado (cabe em lugares baixos).
            state.Crouching = true;
            UpdateHeight(config.crouchHeight, dt);

            float speedMultiplier = state.QuickMantle ? config.mantleSpeedMultiplier : 1f;
            Vector3 position = transform.position;

            if (state.ClimbPhase == 0)
            {
                // Fase 0: sobe reto (e encosta na parede) até o pé passar da beirada.
                float targetY = state.LedgeTopY + 0.05f;
                float up = Mathf.Min(targetY - position.y, config.climbUpSpeed * speedMultiplier * dt);
                Vector3 toWall = Flat(HangPosition() - position);
                Vector3 side = Vector3.ClampMagnitude(toWall, config.climbUpSpeed * speedMultiplier * dt);
                controller.Move(new Vector3(side.x, Mathf.Max(0f, up), side.z));

                if (transform.position.y >= targetY - 0.01f)
                    state.ClimbPhase = 1;
            }
            else
            {
                // Fase 1: passa por cima da beirada até o ponto de ficar em pé.
                Vector3 toStand = Flat(state.LedgeStandPoint - position);
                controller.Move(Vector3.ClampMagnitude(toStand, config.climbForwardSpeed * speedMultiplier * dt));

                if (Flat(state.LedgeStandPoint - transform.position).sqrMagnitude < 0.0025f)
                    FinishClimb();
            }

            // Segurança: se travou em algo, termina mesmo assim.
            if (state.Mode == MotorMode.LedgeClimb && state.LedgeTimer > 1.5f)
                FinishClimb();
        }

        private void FinishClimb()
        {
            state.Mode = MotorMode.Locomotion;
            state.LedgeCooldown = 0.3f;
            state.Velocity = -state.LedgeNormal * 1f; // pequeno passo para frente
            state.SpeedMultiplier = 1f;
            // Continua agachado: no próximo tick levanta sozinho se houver espaço e não estiver segurando Shift.
        }
    }
}
