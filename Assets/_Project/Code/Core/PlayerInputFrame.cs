using UnityEngine;

namespace Rogue.Core
{
    /// <summary>
    /// "Pacote de comandos" do jogador para UM tick da simulação.
    /// O motor do personagem só enxerga isto, nunca o teclado/mouse diretamente.
    /// Quando o jogo for online, é este pacote que viaja pela rede até o servidor,
    /// e o mesmo código de movimento roda lá.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>x = A/D (-1 a 1), y = S/W (-1 a 1).</summary>
        public Vector2 Move;

        /// <summary>Para onde a câmera olha na horizontal, em graus.</summary>
        public float Yaw;

        /// <summary>Para onde a câmera olha na vertical, em graus (negativo = para cima).</summary>
        public float Pitch;

        /// <summary>Pulo foi apertado desde o último tick.</summary>
        public bool JumpPressed;
        public bool JumpHeld;

        public bool CrouchHeld;

        /// <summary>Slide foi apertado desde o último tick.</summary>
        public bool SlidePressed;

        /// <summary>Rolamento foi apertado desde o último tick.</summary>
        public bool RollPressed;

        public bool FireHeld;
        public bool AimHeld;

        /// <summary>Habilidade (barreira de gelo) foi apertada desde o último tick.</summary>
        public bool AbilityPressed;
    }
}
