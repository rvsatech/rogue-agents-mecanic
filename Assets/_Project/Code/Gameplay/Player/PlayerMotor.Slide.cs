using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Slide curto (C): desliza na direção das teclas no momento do aperto.
    /// Depois que começa, a direção fica travada (girar a câmera não muda o slide).
    /// A velocidade começa alta e cai até a velocidade de agachado, percorrendo slideDistance.
    /// Pulo cancela o slide na hora, mantendo o embalo.
    /// Dá para atirar deslizando: o tronco vira para a mira e as pernas continuam na direção do slide.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        private void StartSlide(in PlayerInputFrame frame, Vector3 flat, Vector3 wishDir)
        {
            Vector3 direction;
            if (wishDir.sqrMagnitude > 0.01f) direction = wishDir.normalized;
            else if (flat.sqrMagnitude > 0.25f) direction = flat.normalized;
            else direction = YawForward(frame);

            float startSpeed = Mathf.Max(flat.magnitude, config.runSpeed * WeaponSpeedMultiplier) * config.slideSpeedBoost;
            float endSpeed = config.crouchSpeed * WeaponSpeedMultiplier;
            float distance = Mathf.Max(0.1f, config.slideDistance);

            state.Mode = MotorMode.Slide;
            state.SlideDirection = direction;
            state.SlideStartSpeed = startSpeed;
            state.SlideEndSpeed = endSpeed;
            // Velocidade cai em linha reta: distância = média das velocidades x tempo.
            state.SlideDuration = 2f * distance / (startSpeed + endSpeed);
            state.SlideTimer = 0f;
            state.Crouching = true;
        }

        private void SimulateSlide(in PlayerInputFrame frame, float dt)
        {
            // Pulo cancela o slide na hora (sem delay), mantendo o embalo.
            if (state.JumpBufferTimer > 0f && state.Grounded)
            {
                EndSlide();
                SimulateLocomotion(frame, dt);
                return;
            }

            state.SlideTimer += dt;
            float t = Mathf.Clamp01(state.SlideTimer / Mathf.Max(0.01f, state.SlideDuration));
            float speed = Mathf.Lerp(state.SlideStartSpeed, state.SlideEndSpeed, t);
            Vector3 flat = state.SlideDirection * speed;

            UpdateHeight(config.crouchHeight, dt);

            float vy = state.Grounded
                ? -config.groundStickForce
                : Mathf.Max(state.Velocity.y - config.gravity * dt, -config.maxFallSpeed);

            CollisionFlags flags = controller.Move(new Vector3(flat.x, vy, flat.z) * dt);

            state.Grounded = controller.isGrounded && vy <= 0f;
            state.Velocity = new Vector3(flat.x, vy, flat.z);

            // Saiu de uma borda deslizando: vira queda, mantendo a velocidade.
            if (!state.Grounded)
            {
                EndSlide();
                return;
            }

            // Bateu em algo de frente: para o slide.
            if ((flags & CollisionFlags.Sides) != 0)
            {
                Vector3 actual = Flat((transform.position - PreviousPosition) / dt);
                if (Vector3.Dot(actual, state.SlideDirection) < speed * 0.5f)
                {
                    state.Velocity = new Vector3(actual.x, vy, actual.z);
                    EndSlide();
                    return;
                }
            }

            if (t >= 1f)
                EndSlide();
        }

        private void EndSlide()
        {
            state.Mode = MotorMode.Locomotion;
            state.SlideCooldown = config.slideCooldown;
            // Continua agachado; no próximo tick levanta sozinho se não estiver segurando Shift
            // e se houver espaço acima (ex.: deslizou para baixo de algo baixo = fica agachado).
        }
    }
}
