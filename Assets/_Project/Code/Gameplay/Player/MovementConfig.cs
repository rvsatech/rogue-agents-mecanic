using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Todos os números de ajuste da movimentação.
    /// ScriptableObject = arquivo de configuração da Unity: edite no Inspector, inclusive com o jogo
    /// rodando, e o efeito é imediato (e continua salvo depois do Stop).
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Movement", fileName = "MovementConfig")]
    public sealed class MovementConfig : ScriptableObject
    {
        [Header("Simulação")]
        [Tooltip("Quantas vezes por segundo a física do personagem é calculada (tick fixo).")]
        [Range(30, 128)] public int tickRate = 60;

        [Tooltip("Com o que o personagem colide nas checagens (tudo menos a layer Player).")]
        public LayerMask environmentMask = ~0;

        [Tooltip("Layers que NÃO podem ser escaladas nem usadas no wall run (ex.: bloco não escalável, alvos).")]
        public LayerMask nonClimbableMask = 0;

        [Tooltip("Layers onde não dá para correr na parede (ex.: barreira de gelo), mas que podem ser escaladas.")]
        public LayerMask noWallRunMask = 0;

        [Header("Chão")]
        [Tooltip("Velocidade de corrida normal, em metros por segundo.")]
        public float runSpeed = 6.9f; // ritmo Rogue Agents (6) + 15%

        [Tooltip("Quão rápido chega na velocidade máxima (m/s²). 40 = ~0,15 s até correr: tem peso, mas responde.")]
        public float groundAcceleration = 80f;

        [Tooltip("Quão rápido para ao soltar as teclas (m/s²). 50 = para em ~0,12 s, sem escorregar.")]
        public float groundDeceleration = 80f;

        [Tooltip("Força que mantém o personagem grudado no chão em descidas.")]
        public float groundStickForce = 2f;

        [Header("Gravidade e pulo")]
        public float gravity = 25f;

        [Tooltip("Altura do pulo em metros.")]
        public float jumpHeight = 1.25f;

        public float maxFallSpeed = 40f;

        [Tooltip("Segundos em que ainda dá para pular depois de sair da borda.")]
        public float coyoteTime = 0.1f;

        [Tooltip("Segundos em que um pulo apertado antes de tocar o chão ainda vale.")]
        public float jumpBufferTime = 0.12f;

        [Header("Bônus de pulo para frente / Bhop")]
        [Tooltip("0.15 = +15% de velocidade ao pular para frente.")]
        [Range(0f, 1f)] public float forwardJumpBoost = 0.35f;

        [Tooltip("Quanto do W precisa estar apertado para contar como 'para frente'.")]
        [Range(0.1f, 1f)] public float forwardInputThreshold = 0.5f;

        [Tooltip("Segundos após tocar o chão para pular de novo e manter o bônus.")]
        public float bhopWindow = 0.5f;

        [Tooltip("Se ligado, cada bhop soma mais 15% (até o limite abaixo).")]
        public bool stackBhop = false;

        [Tooltip("Limite do multiplicador quando stackBhop está ligado.")]
        public float maxSpeedMultiplier = 1.6f;

        [Header("Ar / Air strafe (estilo Valorant)")]
        [Tooltip("Força com que uma tecla empurra o personagem no ar (m/s²). Maior = muda de direção mais rápido no ar.")]
        public float airStrafeAcceleration = 45f;

        [Tooltip("Velocidade total máxima no ar em relação à corrida. 1 = diagonal não fica mais rápida.")]
        [Range(1f, 1.5f)] public float airMaxSpeedFactor = 1.1f;

        [Header("Agachar")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.1f;

        [Tooltip("Velocidade andando agachado. O modo xiu (segurar X) anda nesta mesma velocidade, sem barulho de passo.")]
        public float crouchSpeed = 3f;

        [Tooltip("Metros por segundo que a altura da cápsula muda ao agachar/levantar.")]
        public float heightChangeSpeed = 8f;

        [Header("Deitar (Ctrl)")]
        [Tooltip("Altura da cápsula deitado (mínimo = 2 x raio da cápsula).")]
        public float proneHeight = 0.7f;

        [Tooltip("Velocidade rastejando deitado. 0 = deitado não anda (só gira para mirar).")]
        public float proneSpeed = 0f;

        [Header("Levantar do chão (Ctrl ou Espaço deitado)")]
        [Tooltip("Etapa 1: segundos de deitado até agachado (parado no lugar).")]
        public float proneToCrouchTime = 0.6f;

        [Tooltip("Etapa 2: segundos de agachado até em pé (parado no lugar). Segurando Shift, para no agachado.")]
        public float crouchToStandTime = 0.5f;

        [Tooltip("Etapa 3: segundos, já em pé, até voltar à velocidade normal de corrida.")]
        public float getUpRecoveryTime = 1.5f;

        [Tooltip("Velocidade logo depois de ficar em pé, em relação à normal (0.25 = 25%). Sobe até 100% em getUpRecoveryTime.")]
        [Range(0.1f, 1f)] public float getUpStartSpeedFactor = 0.25f;

        [Header("Slide (segurar C)")]
        [Tooltip("Distância máxima do slide segurando C, em metros (6 = comprimento do túnel azul). Soltar o C para antes.")]
        public float slideDistance = 5f;

        [Tooltip("Velocidade mínima para começar a deslizar (abaixo disso o C só agacha).")]
        public float slideMinSpeed = 3f;

        [Tooltip("Velocidade no início do slide em relação à velocidade atual (ou de corrida, se estiver parado).")]
        public float slideSpeedBoost = 1.15f;

        [Tooltip("Segundos de espera para poder deslizar de novo.")]
        public float slideCooldown = 0.35f;

        [Header("Cambalhota (Alt)")]
        [Tooltip("Distância da cambalhota, em metros.")]
        public float rollDistance = 4.2f;

        [Tooltip("Duração da cambalhota, em segundos (a animação acompanha: 0,8 s = quase a velocidade natural dela).")]
        public float rollDuration = 0.6f;

        [Tooltip("Velocidade no fim em relação ao início (1 = mesma velocidade até o fim: sai correndo na hora).")]
        [Range(0.1f, 1f)] public float rollEndSpeedFactor = 1f;

        [Tooltip("Segundos de espera para rolar de novo.")]
        public float rollCooldown = 0.15f;

        [Tooltip("Alt apertado até esse tempo antes de tocar o chão ainda vale.")]
        public float rollBufferTime = 0.15f;

        [Tooltip("A partir desse tempo da cambalhota, Espaço já cancela em pulo (ou parede, com Shift).")]
        public float rollJumpCancelTime = 0.3f;

        [Tooltip("A partir desse tempo, C emenda um slide e Alt emenda outra cambalhota (sem esperar levantar).")]
        public float rollActionCancelTime = 0.3f;

        [Tooltip("A partir desse tempo (fim do giro, antes de levantar), qualquer tecla de andar, atirar ou mirar já sai correndo.")]
        public float rollMoveCancelTime = 0.48f;

        [Header("Wall run (Shift + Espaço)")]
        [Tooltip("Tempo máximo correndo na parede (segundos). Vale para a SOMA de todas as paredes até tocar o chão: " +
                 "acabou, cai mesmo segurando Shift e Espaço.")]
        public float wallRunDuration = 1f;

        [Tooltip("Quando o tempo acaba, empurra o corpo para longe da parede (m/s) para cair de verdade.")]
        public float wallRunEndPushSpeed = 1.5f;

        [Tooltip("Distância extra (além do corpo) para achar uma parede do lado.")]
        public float wallRunDetectDistance = 0.6f;

        [Tooltip("Velocidade na parede em relação à corrida (1,2 = 20% mais rápido). Se já vier mais rápido, mantém.")]
        public float wallRunSpeedFactor = 1.2f;

        [Tooltip("Impulso para cima ao entrar na parede (m/s).")]
        public float wallRunEntryUpSpeed = 3f;

        [Tooltip("Gravidade enquanto ainda está subindo na parede.")]
        public float wallRunRiseGravity = 6f;

        [Tooltip("Gravidade depois que começa a descer na parede (baixa = desce devagar).")]
        public float wallRunSinkGravity = 0.5f;

        [Tooltip("Velocidade máxima de descida na parede (m/s).")]
        public float wallRunMaxSinkSpeed = 0.35f;

        [Tooltip("Força que mantém o corpo encostado na parede.")]
        public float wallStickSpeed = 1.5f;

        [Tooltip("Altura do salto ao apertar Espaço na parede.")]
        public float wallJumpHeight = 1.2f;

        [Tooltip("Velocidade do salto da parede em relação à velocidade na parede.")]
        public float wallJumpSpeedFactor = 1f;

        [Tooltip("Quanto o salto da parede sempre empurra para longe dela (0 a 1).")]
        [Range(0f, 1f)] public float wallJumpMinAwayFactor = 0.3f;

        [Tooltip("Segundos sem poder entrar em outra parede depois de sair de uma.")]
        public float wallRunCooldown = 0.25f;

        [Tooltip("Desligado (como no Rogue Agents): na parede o botão direito não mira — sem zoom de ombro e sem luneta. O tiro funciona normal.")]
        public bool wallRunAllowAim = false;

        [Header("Escalar muro de frente (botão direito + Espaço de frente para a parede)")]
        [Tooltip("Velocidade de subida (m/s). Diminui um pouco perto do limite.")]
        public float wallClimbSpeed = 4f;

        [Tooltip("Quanto sobe no máximo (m). Com a mão alcançando a beirada (ledgeMaxReach 2,1 m), dá para muros de " +
                 "até ~3,9 m (uns 3 pulos). Uma escalada por vez até tocar o chão.")]
        public float wallClimbMaxHeight = 1.8f;

        [Tooltip("Distância (além do corpo) em que a parede da frente conta.")]
        public float wallClimbDetectDistance = 0.6f;

        [Header("Beirada: pendurar e subir")]
        [Tooltip("Altura máxima (a partir do pé) que a mão alcança. Com o pulo de 1,25 m, alcança blocos de até ~3,3 m.")]
        public float ledgeMaxReach = 2.1f;

        [Tooltip("Beiradas mais baixas que isso (a partir do pé) não são agarradas.")]
        public float ledgeMinHeight = 0.7f;

        [Tooltip("Até essa altura (a partir do pé) sobe direto, sem ficar pendurado.")]
        public float quickMantleMaxHeight = 1.5f;

        [Tooltip("Distância extra (além do corpo) para achar a parede na frente.")]
        public float ledgeDetectDistance = 0.45f;

        [Tooltip("Distância entre o pé e a beirada quando está pendurado (braços quase esticados).")]
        public float hangHandHeight = 1.8f;

        [Tooltip("Velocidade para encaixar na posição de pendurado.")]
        public float hangSnapSpeed = 10f;

        [Tooltip("Tempo mínimo pendurado antes de poder subir.")]
        public float hangMinTime = 0.12f;

        [Tooltip("Velocidade subindo a parede (m/s).")]
        public float climbUpSpeed = 4f;

        [Tooltip("Velocidade passando por cima da beirada (m/s).")]
        public float climbForwardSpeed = 3.2f;

        [Tooltip("Obstáculos baixos sobem mais rápido.")]
        public float mantleSpeedMultiplier = 1.6f;

        [Tooltip("Segundos sem poder agarrar de novo depois de soltar.")]
        public float ledgeRegrabCooldown = 0.4f;

        /// <summary>Altura máxima que o personagem consegue escalar (pulo + alcance da mão).</summary>
        public float MaxClimbHeight => jumpHeight + ledgeMaxReach;

        [HideInInspector]
        [Tooltip("Versão dos valores padrão já aplicados (o menu Rogue atualiza configs antigas).")]
        public int defaultsVersion;
    }
}
