using Rogue.Core;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Levantar do chão (Ctrl ou Espaço deitado), em etapas que levam tempo — parado no lugar:
    /// 1) deitado -> agachado (proneToCrouchTime);
    /// 2) agachado -> em pé (crouchToStandTime). Segurando Shift ou sem espaço acima, para no agachado;
    /// 3) depois, anda devagar e volta à velocidade normal aos poucos (getUpRecoveryTime).
    /// Dá para atirar enquanto levanta; pular, rolar, deslizar e correr na parede não.
    /// </summary>
    public sealed partial class PlayerMotor
    {
        private void StartGetUp()
        {
            state.Mode = MotorMode.GetUp;
            state.Prone = false;
            state.Crouching = true;
            state.GetUpPhase = 1;
            state.GetUpPhaseTimer = 0f;
            state.JumpBufferTimer = 0f;
            state.RollBufferTimer = 0f;
        }

        private void SimulateGetUp(in PlayerInputFrame frame, float dt)
        {
            state.GetUpPhaseTimer += dt;
            state.JumpBufferTimer = 0f; // nada de pular no meio do levantar
            state.RollBufferTimer = 0f;

            if (state.GetUpPhase == 1)
            {
                // Etapa 1: senta e fica agachado.
                float t = state.GetUpPhaseTimer / Mathf.Max(0.01f, config.proneToCrouchTime);
                UpdateHeight(Mathf.Lerp(config.proneHeight, config.crouchHeight, Mathf.Clamp01(t)), dt);

                if (t >= 1f)
                {
                    bool stayCrouched = frame.CrouchHeld || frame.SlideHeld || !HasHeadroom(config.standHeight);
                    if (stayCrouched)
                    {
                        FinishGetUp();
                    }
                    else
                    {
                        state.GetUpPhase = 2;
                        state.GetUpPhaseTimer = 0f;
                    }
                }
            }
            else
            {
                // Etapa 2: fica em pé devagar.
                float t = state.GetUpPhaseTimer / Mathf.Max(0.01f, config.crouchToStandTime);
                if (!HasHeadroom(config.standHeight))
                {
                    FinishGetUp(); // teto baixo: termina agachado
                }
                else
                {
                    UpdateHeight(Mathf.Lerp(config.crouchHeight, config.standHeight, Mathf.Clamp01(t)), dt);
                    if (t >= 1f)
                    {
                        state.Crouching = false;
                        FinishGetUp();
                    }
                }
            }

            // Parado no lugar (freia o que sobrou), com gravidade normal.
            Vector3 flat = Vector3.MoveTowards(Flat(state.Velocity), Vector3.zero, config.groundDeceleration * dt);
            float vy = state.Grounded
                ? -config.groundStickForce
                : Mathf.Max(state.Velocity.y - config.gravity * dt, -config.maxFallSpeed);
            MoveAndCollide(ref flat, ref vy, dt);

            // O chão sumiu no meio: cancela o levantar e segue no ar.
            if (!state.Grounded && state.Mode == MotorMode.GetUp)
            {
                state.Mode = MotorMode.Locomotion;
                state.GetUpPhase = 0;
                state.GetUpPhaseTimer = 0f;
            }
        }

        /// <summary>Terminou de levantar: começa a lentidão que passa aos poucos.</summary>
        private void FinishGetUp()
        {
            state.Mode = MotorMode.Locomotion;
            state.GetUpPhase = 0;
            state.GetUpPhaseTimer = 0f;
            state.GetUpTimer = config.getUpRecoveryTime;
        }
    }
}
