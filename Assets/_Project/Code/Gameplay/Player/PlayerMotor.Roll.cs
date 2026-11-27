using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Cambalhota (Alt): rola para a direção das teclas (8 direções, relativo à câmera).
    /// Sem tecla: rola para onde estava indo (ou para frente da câmera, se parado).
    /// O corpo vira para a direção da cambalhota durante ela (o visual cuida disso).
    /// Passa agachado (cabe embaixo de obstáculos baixos). Não atira rolando.
    /// Depois de rollJumpCancelTime, Espaço cancela a cambalhota em pulo.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        private void StartRoll(in PlayerInputFrame frame, Vector3 flat, Vector3 wishDir)
        {
            Vector3 direction;
            if (wishDir.sqrMagnitude > 0.01f) direction = wishDir.normalized;
            else if (flat.sqrMagnitude > 0.25f) direction = flat.normalized;
            else direction = YawForward(frame);

            float duration = Mathf.Max(0.1f, config.rollDuration);
            float endFactor = Mathf.Clamp(config.rollEndSpeedFactor, 0.1f, 1f);
            // Velocidade cai em linha reta: distância = média das velocidades x tempo.
            float startSpeed = config.rollDistance / (duration * (1f + endFactor) * 0.5f);

            state.Mode = MotorMode.Roll;
            state.RollDirection = direction;
            state.RollDuration = duration;
            state.RollStartSpeed = startSpeed;
            state.RollEndSpeed = startSpeed * endFactor;
            state.RollTimer = 0f;
            state.RollBufferTimer = 0f;
            state.Crouching = true;
            state.SpeedMultiplier = 1f;
        }

        private void SimulateRoll(in PlayerInputFrame frame, float dt)
        {
            // Pulo cancela o fim da cambalhota.
            if (state.JumpBufferTimer > 0f && state.Grounded && state.RollTimer >= config.rollJumpCancelTime)
            {
                EndRoll();
                SimulateLocomotion(frame, dt);
                return;
            }

            state.RollTimer += dt;
            float t = Mathf.Clamp01(state.RollTimer / state.RollDuration);
            float speed = Mathf.Lerp(state.RollStartSpeed, state.RollEndSpeed, t);
            Vector3 flat = state.RollDirection * speed;

            UpdateHeight(config.crouchHeight, dt);

            float vy = state.Grounded
                ? -config.groundStickForce
                : Mathf.Max(state.Velocity.y - config.gravity * dt, -config.maxFallSpeed);

            CollisionFlags flags = controller.Move(new Vector3(flat.x, vy, flat.z) * dt);

            state.Grounded = controller.isGrounded && vy <= 0f;
            state.Velocity = new Vector3(flat.x, vy, flat.z);

            // Rolou para fora de uma borda: vira queda.
            if (!state.Grounded)
            {
                EndRoll();
                return;
            }

            // Bateu de frente em algo: para.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = Flat((transform.position - PreviousPosition) / dt);
                if (Vector3.Dot(actual, state.RollDirection) < speed * 0.3f)
                {
                    state.Velocity = new Vector3(actual.x, vy, actual.z);
                    EndRoll();
                    return;
                }
            }

            if (t >= 1f)
                EndRoll();
        }

        private void EndRoll()
        {
            state.Mode = MotorMode.Locomotion;
            state.RollCooldown = config.rollCooldown;
            // Continua agachado; no próximo tick levanta sozinho se houver espaço e não estiver segurando Shift.
        }
    }
}
