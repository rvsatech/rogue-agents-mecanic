using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Todos os números de ajuste da movimentação.
    /// É um ScriptableObject: um arquivo de configuração da Unity que você edita no Inspector,
    /// inclusive com o jogo rodando, e o efeito é imediato.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Movement", fileName = "MovementConfig")]
    public sealed class MovementConfig : ScriptableObject
    {
        [Header("Simulação")]
        [Tooltip("Quantas vezes por segundo a física do personagem é calculada (tick fixo).")]
        [Range(30, 128)] public int tickRate = 60;

        [Tooltip("Com o que o personagem colide nas checagens (tudo menos a layer Player).")]
        public LayerMask environmentMask = ~0;

        [Header("Chão")]
        [Tooltip("Velocidade de corrida normal, em metros por segundo.")]
        public float runSpeed = 6.5f;

        [Tooltip("Quão rápido chega na velocidade máxima. Alto = resposta instantânea.")]
        public float groundAcceleration = 90f;

        [Tooltip("Quão rápido para ao soltar as teclas. Alto = para seco, sem 'sabão'.")]
        public float groundDeceleration = 110f;

        [Tooltip("Força que mantém o personagem grudado no chão em descidas.")]
        public float groundStickForce = 2f;

        [Header("Gravidade e pulo")]
        public float gravity = 28f;

        [Tooltip("Altura do pulo em metros.")]
        public float jumpHeight = 1.25f;

        public float maxFallSpeed = 40f;

        [Tooltip("Segundos em que ainda dá para pular depois de sair da borda.")]
        public float coyoteTime = 0.1f;

        [Tooltip("Segundos em que um pulo apertado antes de tocar o chão ainda vale.")]
        public float jumpBufferTime = 0.12f;

        [Header("Bônus de pulo para frente / Bhop")]
        [Tooltip("0.15 = +15% de velocidade ao pular para frente.")]
        [Range(0f, 1f)] public float forwardJumpBoost = 0.15f;

        [Tooltip("Quanto do W precisa estar apertado para contar como 'para frente'.")]
        [Range(0.1f, 1f)] public float forwardInputThreshold = 0.5f;

        [Tooltip("Segundos após tocar o chão para pular de novo e manter o bônus.")]
        public float bhopWindow = 0.5f;

        [Tooltip("Se ligado, cada bhop soma mais 15% (até o limite abaixo).")]
        public bool stackBhop = false;

        [Tooltip("Limite do multiplicador quando stackBhop está ligado.")]
        public float maxSpeedMultiplier = 1.6f;

        [Header("Ar / Air strafe")]
        [Tooltip("Graus por segundo que o personagem consegue curvar no ar segurando as teclas e girando o mouse.")]
        public float airSteerDegreesPerSecond = 220f;

        [Tooltip("Aceleração no ar quando está abaixo da velocidade de corrida (ou freando ao segurar para trás).")]
        public float airAcceleration = 20f;

        [Header("Agachar")]
        public float standHeight = 1.8f;
        public float crouchHeight = 1.1f;

        [Tooltip("Velocidade andando agachado.")]
        public float crouchSpeed = 3.2f;

        [Tooltip("Metros por segundo que a altura muda ao agachar/levantar.")]
        public float heightChangeSpeed = 8f;
    }
}
