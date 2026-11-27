using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Wall run: segurando Shift e apertando Espaço perto de uma parede do lado, o personagem
    /// corre na parede 20% mais rápido que a corrida, por até wallRunDuration (1 s) — somando todas as paredes
    /// até tocar o chão. Solta o Shift ou acabou o tempo = cai (mesmo segurando Shift e Espaço).
    /// Espaço de novo na parede = salto direcional para onde as teclas apontam.
    /// Dá para atirar correndo na parede (o tiro não depende do modo).
    /// Não funciona em superfícies NonClimbable nem na barreira de gelo, nem na mesma parede duas vezes sem tocar o chão.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        /// <summary>Procura uma parede dos lados (ou nas diagonais da frente) para correr.</summary>
        private bool TryFindRunnableWall(in PlayerInputFrame frame, Vector3 flat, Vector3 wishDir,
            out RaycastHit best, out Vector3 moveDirection)
        {
            best = default;

            if (flat.sqrMagnitude > 0.25f) moveDirection = flat.normalized;
            else if (wishDir.sqrMagnitude > 0.01f) moveDirection = wishDir.normalized;
            else moveDirection = YawForward(frame);

            Vector3 right = Vector3.Cross(Vector3.up, moveDirection);
            Vector3 origin = transform.position + Vector3.up * (state.Height * 0.5f);
            float distance = controller.radius + config.wallRunDetectDistance;

            bool found = false;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                Vector3 direction;
                if (i == 0) direction = right;
                else if (i == 1) direction = -right;
                else if (i == 2) direction = (moveDirection + right).normalized;
                else direction = (moveDirection - right).normalized;

                if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance,
                        config.environmentMask, QueryTriggerInteraction.Ignore))
                    continue;

                if (!IsRunnableWall(hit, moveDirection, true)) continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit;
                    found = true;
                }
            }

            return found;
        }

        private bool IsRunnableWall(in RaycastHit hit, Vector3 moveDirection, bool checkRepeat)
        {
            if (Mathf.Abs(hit.normal.y) > 0.3f) return false;   // precisa ser parede (quase vertical)
            if (IsNonClimbable(hit.collider)) return false;     // bloco não escalável / alvos
            if (IsNoWallRun(hit.collider)) return false;        // barreira de gelo

            Vector3 normal = Flat(hit.normal).normalized;

            // Parede bem de frente não é corrida lateral.
            if (Mathf.Abs(Vector3.Dot(normal, moveDirection)) > 0.85f) return false;

            // Não repete a mesma parede antes de tocar o chão.
            if (checkRepeat && state.HasLastWall && Vector3.Dot(normal, state.LastWallNormal) > 0.9f) return false;

            return true;
        }

        private void StartWallRun(in RaycastHit hit, Vector3 flat, Vector3 moveDirection)
        {
            Vector3 normal = Flat(hit.normal).normalized;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up).normalized;
            if (Vector3.Dot(tangent, moveDirection) < 0f) tangent = -tangent;

            float speed = Mathf.Max(Vector3.Dot(flat, tangent), config.runSpeed * config.wallRunSpeedFactor * WeaponSpeedMultiplier * GetUpSpeedFactor);

            state.Mode = MotorMode.WallRun;
            state.WallNormal = normal;
            state.WallTangent = tangent;
            state.WallRunSpeed = speed;
            state.WallRunTimer = 0f;
            state.Velocity = tangent * speed + Vector3.up * config.wallRunEntryUpSpeed;

            state.JumpBufferTimer = 0f;
            state.Grounded = false;
            state.JumpedSinceGrounded = true;
            state.Crouching = false;
            state.HasLastWall = true;
            state.LastWallNormal = normal;
        }

        private void SimulateWallRun(in PlayerInputFrame frame, float dt)
        {
            state.WallRunTimer += dt;
            state.WallRunAirTime += dt;
            UpdateHeight(config.standHeight, dt);

            // Espaço na parede: salto direcional (o mesmo Espaço que iniciou não conta).
            if (state.JumpBufferTimer > 0f && state.WallRunTimer > 0.05f)
            {
                WallJump(frame, dt);
                return;
            }

            // Soltou o Shift ou acabou o tempo (1 s somando as paredes desde o chão): cai.
            if (!frame.WallRunHeld || state.WallRunAirTime >= config.wallRunDuration)
            {
                // Desgruda da parede (um empurrãozinho para fora) e mantém a velocidade ao longo dela.
                state.Velocity += state.WallNormal * config.wallRunEndPushSpeed;
                EndWallRun();
                SimulateLocomotion(frame, dt);
                return;
            }

            // Continua encostado na parede? (a parede pode acabar ou fazer curva)
            Vector3 origin = transform.position + Vector3.up * (state.Height * 0.5f);
            float distance = controller.radius + config.wallRunDetectDistance + 0.2f;
            if (!Physics.Raycast(origin, -state.WallNormal, out RaycastHit hit, distance,
                    config.environmentMask, QueryTriggerInteraction.Ignore) ||
                !IsRunnableWall(hit, state.WallTangent, false))
            {
                EndWallRun();
                SimulateLocomotion(frame, dt);
                return;
            }

            Vector3 normal = Flat(hit.normal).normalized;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up).normalized;
            if (Vector3.Dot(tangent, state.WallTangent) < 0f) tangent = -tangent;
            state.WallNormal = normal;
            state.WallTangent = tangent;

            // Sobe um pouco no começo e depois desce devagar.
            float vy = state.Velocity.y;
            float gravity = vy > 0f ? config.wallRunRiseGravity : config.wallRunSinkGravity;
            vy = Mathf.Max(vy - gravity * dt, -config.wallRunMaxSinkSpeed);

            Vector3 along = tangent * state.WallRunSpeed;
            Vector3 stick = -normal * config.wallStickSpeed;
            CollisionFlags flags = controller.Move((along + stick + Vector3.up * vy) * dt);

            if ((flags & CollisionFlags.Above) != 0 && vy > 0f)
                vy = 0f;

            state.Velocity = along + Vector3.up * vy;

            // Encostou no chão: acabou.
            if (controller.isGrounded && vy <= 0f)
            {
                state.Grounded = true;
                OnLanded();
                EndWallRun();
                return;
            }

            // Bateu numa quina/obstáculo na frente: acabou.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = Flat((transform.position - PreviousPosition) / dt);
                if (Vector3.Dot(actual, tangent) < state.WallRunSpeed * 0.5f)
                {
                    state.Velocity = new Vector3(actual.x, vy, actual.z);
                    EndWallRun();
                }
            }
        }

        /// <summary>Salto da parede para onde as teclas apontam (sempre um pouco para longe da parede).</summary>
        private void WallJump(in PlayerInputFrame frame, float dt)
        {
            Vector3 wish = WishDirection(frame);
            Vector3 normal = state.WallNormal;
            Vector3 direction;

            if (wish.sqrMagnitude < 0.01f)
            {
                // Sem tecla: para frente e para longe da parede.
                direction = (normal * 0.7f + state.WallTangent).normalized;
            }
            else
            {
                direction = wish.normalized;
                float away = Vector3.Dot(direction, normal);
                if (away < config.wallJumpMinAwayFactor)
                    direction = Flat(direction + normal * (config.wallJumpMinAwayFactor - away)).normalized;
            }

            float speed = Mathf.Max(state.WallRunSpeed, config.runSpeed * state.SpeedMultiplier * WeaponSpeedMultiplier * GetUpSpeedFactor) * config.wallJumpSpeedFactor;
            Vector3 flat = direction * speed;
            float vy = Mathf.Sqrt(2f * config.gravity * config.wallJumpHeight);

            state.Mode = MotorMode.Locomotion;
            state.WallRunCooldown = config.wallRunCooldown;
            state.JumpBufferTimer = 0f;
            state.Grounded = false;
            state.JumpedSinceGrounded = true;
            state.Velocity = new Vector3(flat.x, vy, flat.z);

            controller.Move(state.Velocity * dt);
        }

        private void EndWallRun()
        {
            state.Mode = MotorMode.Locomotion;
            state.WallRunCooldown = config.wallRunCooldown;
        }
    }
}
