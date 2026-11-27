using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Modo normal: andar, correr, agachar, deitar, pular, cair e air strafe.</summary>
    public sealed partial class PlayerMotor
    {
        private void SimulateLocomotion(in PlayerInputFrame frame, float dt)
        {
            Vector3 wishDir = WishDirection(frame);
            Vector3 flat = Flat(state.Velocity);
            float vy = state.Velocity.y;

            // Deitado: tem regras próprias (parado, só gira; Ctrl/Espaço começa a levantar).
            if (state.Prone)
            {
                SimulateProne(frame, wishDir, dt, false);
                return;
            }

            // 1) Shift + Espaço perto de uma parede = wall run (tem prioridade sobre o pulo normal).
            //    Gastou o tempo de parede (1 s): só volta a valer depois de tocar o chão.
            if (frame.WallRunHeld && state.JumpBufferTimer > 0f && state.WallRunCooldown <= 0f &&
                state.WallRunAirTime < config.wallRunDuration &&
                TryFindRunnableWall(frame, flat, wishDir, out RaycastHit wallHit, out Vector3 runDirection))
            {
                StartWallRun(wallHit, flat, runDirection);
                SimulateWallRun(frame, dt);
                return;
            }

            // 1b) Botão direito + Espaço de frente para um muro = escala (uma vez até tocar o chão).
            if (frame.AimHeld && state.JumpBufferTimer > 0f && !state.WallClimbUsed && state.WallRunCooldown <= 0f &&
                TryFindClimbableWall(frame, wishDir, out RaycastHit climbHit))
            {
                StartWallClimb(climbHit);
                SimulateWallRun(frame, dt);
                return;
            }

            // 2) Ctrl no chão = deitar.
            if (frame.ProneTogglePressed && state.Grounded)
            {
                state.Prone = true;
                state.Crouching = false;
                SimulateProne(frame, wishDir, dt, true); // o mesmo Ctrl não pode levantar de novo neste tick
                return;
            }

            // 3) Alt no chão = cambalhota (para a direção das teclas).
            if (state.RollBufferTimer > 0f && state.Grounded && state.RollCooldown <= 0f)
            {
                StartRoll(frame, flat, wishDir);
                SimulateRoll(frame, dt);
                return;
            }

            // 4) C no chão = slide (precisa de velocidade). Segurar C ao aterrissar também desliza.
            bool slideRequested = frame.SlidePressed ||
                                  (frame.SlideHeld && state.TimeSinceLanded <= 0.05f);
            if (slideRequested && state.Grounded && state.SlideCooldown <= 0f &&
                flat.magnitude >= config.slideMinSpeed)
            {
                StartSlide(frame, flat, wishDir);
                SimulateSlide(frame, dt);
                return;
            }

            // 5) Agachar: Shift segurado (ou C segurado sem velocidade / depois do slide). Só no chão.
            UpdateCrouch((frame.CrouchHeld || frame.SlideHeld) && state.Grounded, dt);

            // Modo xiu (X segurado): anda devagar e sem barulho de passo.
            state.QuietWalking = frame.QuietHeld && state.Grounded;

            // 6) Velocidade horizontal.
            flat = state.Grounded ? GroundMove(flat, wishDir, dt) : AirMove(frame, flat, dt);

            // 7) Pulo ou gravidade.
            bool canJump = state.Grounded ||
                           (!state.JumpedSinceGrounded && state.TimeSinceGrounded <= config.coyoteTime);

            if (state.JumpBufferTimer > 0f && canJump)
                Jump(frame, ref flat, ref vy);
            else if (state.Grounded)
                vy = -config.groundStickForce;
            else
                vy = Mathf.Max(vy - config.gravity * dt, -config.maxFallSpeed);

            // 8) Mover de fato.
            MoveAndCollide(ref flat, ref vy, dt);

            // 9) No ar, indo de frente para uma parede: tenta agarrar a beirada.
            if (!state.Grounded && state.LedgeCooldown <= 0f && !frame.CrouchHeld &&
                TryFindLedge(wishDir, out LedgeInfo ledge))
            {
                StartLedge(ledge);
            }
        }

        /// <summary>Move a cápsula, trata batidas na cabeça/paredes e detecta o pouso.</summary>
        private void MoveAndCollide(ref Vector3 flat, ref float vy, float dt)
        {
            CollisionFlags flags = controller.Move(new Vector3(flat.x, vy, flat.z) * dt);

            if ((flags & CollisionFlags.Above) != 0 && vy > 0f)
                vy = 0f;

            // Bateu de lado numa parede: fica só com a velocidade que realmente andou.
            if ((flags & CollisionFlags.Sides) != 0)
                flat = Flat((transform.position - PreviousPosition) / dt);

            bool wasGrounded = state.Grounded;
            state.Grounded = controller.isGrounded && vy <= 0f;
            if (state.Grounded && !wasGrounded) OnLanded();

            state.Velocity = new Vector3(flat.x, vy, flat.z);
        }

        private Vector3 GroundMove(Vector3 flat, Vector3 wishDir, float dt)
        {
            float targetSpeed;
            if (state.Prone)
                targetSpeed = config.proneSpeed;
            else if (state.Crouching)
                targetSpeed = config.crouchSpeed;
            else
                targetSpeed = config.runSpeed * state.SpeedMultiplier * (1f - FiringGroundPenalty); // atirando correndo: -20%

            if (state.QuietWalking && !state.Crouching && !state.Prone)
                targetSpeed = config.crouchSpeed; // modo xiu: mesma velocidade do andar agachado

            targetSpeed *= WeaponSpeedMultiplier; // faca +15%, pistola +10%

            if (!state.Prone)
                targetSpeed *= GetUpSpeedFactor;  // acabou de levantar: volta ao normal em 2 s

            bool hasInput = wishDir.sqrMagnitude > 0.0001f;
            float rate = hasInput ? config.groundAcceleration : config.groundDeceleration;
            return Vector3.MoveTowards(flat, wishDir * targetSpeed, rate * dt);
        }

        /// <summary>
        /// Controle aéreo estilo Valorant, POR EIXO (relativo à câmera):
        /// - A/D mexem só na velocidade lateral; W/S mexem só na da frente.
        /// - Correndo + pulo + só A: a velocidade da frente continua e a lateral cresce = diagonal.
        /// - Correndo + pulo + W + A: frente e lado ao mesmo tempo.
        /// - Parado + pulo + A: vai para a esquerda.
        /// - Sem tecla nenhuma: o impulso continua igual.
        /// - Tecla oposta (ex.: S indo para frente) freia aquele eixo.
        /// </summary>
        private Vector3 AirMove(in PlayerInputFrame frame, Vector3 flat, float dt)
        {
            Vector2 move = frame.Move;
            if (move.sqrMagnitude < 0.0001f)
                return flat;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            float speedBefore = flat.magnitude;

            // Velocidade "do ponto de vista da câmera": x = lado, z = frente.
            Quaternion yaw = Quaternion.Euler(0f, frame.Yaw, 0f);
            Vector3 local = Quaternion.Inverse(yaw) * flat;

            float axisSpeed = config.runSpeed * state.SpeedMultiplier * WeaponSpeedMultiplier * GetUpSpeedFactor;
            float step = config.airStrafeAcceleration * dt;

            if (Mathf.Abs(move.x) > 0.01f)
                local.x = Mathf.MoveTowards(local.x, move.x * axisSpeed, step);
            if (Mathf.Abs(move.y) > 0.01f)
                local.z = Mathf.MoveTowards(local.z, move.y * axisSpeed, step);

            flat = yaw * local;

            // Limite de velocidade total no ar: a da corrida/bhop (x fator) ou a que já tinha, o que for maior.
            float limit = Mathf.Max(axisSpeed * config.airMaxSpeedFactor, speedBefore);
            float speed = flat.magnitude;
            if (speed > limit)
                flat *= limit / speed;

            return flat;
        }

        private void Jump(in PlayerInputFrame frame, ref Vector3 flat, ref float vy)
        {
            bool forward = frame.Move.y >= config.forwardInputThreshold;
            float boost = 1f + config.forwardJumpBoost;

            if (forward)
            {
                bool chainAlive = state.SpeedMultiplier > 1f; // ainda dentro da janela do bhop

                if (!chainAlive)
                {
                    // Impulso só no pulo: vai a corrida x boost (não acumula com o que sobrou do pulo anterior).
                    state.SpeedMultiplier = boost;
                    float jumpSpeed = config.runSpeed * WeaponSpeedMultiplier * GetUpSpeedFactor * boost;
                    Vector3 direction = flat.sqrMagnitude > 0.25f ? flat.normalized : WishDirection(frame);
                    if (direction.sqrMagnitude > 0.01f)
                        flat = direction.normalized * Mathf.Max(flat.magnitude, jumpSpeed);
                }
                else if (config.stackBhop)
                {
                    float next = Mathf.Min(state.SpeedMultiplier * boost, config.maxSpeedMultiplier);
                    flat *= next / state.SpeedMultiplier;
                    state.SpeedMultiplier = next;
                }
                // Senão: mantém a velocidade aumentada (bhop sem acumular).
            }
            else
            {
                // Pulo de lado/para trás não ganha bônus, mas também não perde velocidade.
                state.SpeedMultiplier = 1f;
            }

            vy = Mathf.Sqrt(2f * config.gravity * config.jumpHeight);

            state.JumpBufferTimer = 0f;
            state.JumpedSinceGrounded = true;
            state.Grounded = false;
        }

        private void UpdateCrouch(bool wantCrouch, float dt)
        {
            if (wantCrouch)
                state.Crouching = true;
            else if (state.Crouching && HasHeadroom(config.standHeight))
                state.Crouching = false;

            UpdateHeight(state.Crouching ? config.crouchHeight : config.standHeight, dt);
        }

        /// <summary>
        /// Deitado de barriga para cima: NÃO anda (proneSpeed 0), só gira para mirar; atira com mais precisão.
        /// Ctrl ou Espaço começa a levantar em etapas (PlayerMotor.GetUp): deitado -> agachado -> em pé -> lento.
        /// </summary>
        private void SimulateProne(in PlayerInputFrame frame, Vector3 wishDir, float dt, bool justEntered)
        {
            bool wantsUp = !justEntered && (frame.ProneTogglePressed || state.JumpBufferTimer > 0f);
            if (wantsUp && state.Grounded && HasHeadroom(config.crouchHeight))
            {
                StartGetUp();
                SimulateGetUp(frame, dt);
                return;
            }

            if (state.Grounded)
                UpdateHeight(config.proneHeight, dt);
            else
                state.Prone = false; // o chão sumiu: sai do deitado e cai

            // Parado (proneSpeed 0): freia o que sobrou. Com proneSpeed > 0 volta a rastejar.
            Vector3 flat = GroundMove(Flat(state.Velocity), wishDir, dt);
            float vy = state.Grounded ? -config.groundStickForce : state.Velocity.y - config.gravity * dt;
            MoveAndCollide(ref flat, ref vy, dt);
        }
    }
}
