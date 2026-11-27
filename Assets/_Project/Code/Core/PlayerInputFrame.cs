using UnityEngine;

namespace Rogue.Core
{
    /// <summary>
    /// "Pacote de comandos" do jogador para UM tick da simulação.
    /// O motor e as armas só enxergam isto, nunca o teclado/mouse diretamente.
    /// Quando o jogo for online, é este pacote que viaja pela rede até o servidor,
    /// e o mesmo código de movimento roda lá.
    /// "Pressed" = apertou desde o último tick. "Held" = está segurando.
    /// </summary>
    public struct PlayerInputFrame
    {
        /// <summary>x = A/D (-1 a 1), y = S/W (-1 a 1).</summary>
        public Vector2 Move;

        /// <summary>Para onde a câmera olha na horizontal, em graus.</summary>
        public float Yaw;

        /// <summary>Para onde a câmera olha na vertical, em graus (negativo = para cima).</summary>
        public float Pitch;

        public bool JumpPressed;
        public bool JumpHeld;

        /// <summary>Shift segurado: agachar.</summary>
        public bool CrouchHeld;

        /// <summary>Shift segurado (mesma tecla do agachar): com Espaço perto de uma parede, corre na parede.</summary>
        public bool WallRunHeld;

        /// <summary>Ctrl esquerdo: deitar / levantar.</summary>
        public bool ProneTogglePressed;

        /// <summary>C apertado: começa o slide (se estiver rápido o bastante).</summary>
        public bool SlidePressed;

        /// <summary>C segurado: o slide continua enquanto segura; soltar para o slide.</summary>
        public bool SlideHeld;

        /// <summary>R: recarregar.</summary>
        public bool ReloadPressed;

        /// <summary>X segurado: modo xiu (anda devagar e sem barulho de passo).</summary>
        public bool QuietHeld;

        /// <summary>Alt (toque): cambalhota.</summary>
        public bool RollPressed;

        /// <summary>Botão esquerdo segurado (armas automáticas).</summary>
        public bool FireHeld;

        /// <summary>Botão esquerdo clicado (pistola, faca e gelo: um uso por clique).</summary>
        public bool FirePressed;

        /// <summary>Botão direito segurado: mirar.</summary>
        public bool AimHeld;

        /// <summary>1: arma primária (a escolhida no inventário: UMP, AWP ou a 12).</summary>
        public bool Weapon1Pressed;

        /// <summary>2: arma secundária (pistola).</summary>
        public bool Weapon2Pressed;

        /// <summary>3: faca.</summary>
        public bool Weapon3Pressed;

        /// <summary>4: usar o kit médico (4 de novo cancela).</summary>
        public bool MedkitPressed;

        /// <summary>E: parede de gelo (proteção).</summary>
        public bool IceWallPressed;

        /// <summary>G: gadget escolhido no inventário (granada, flash ou smoke).</summary>
        public bool GadgetPressed;

        /// <summary>Botão direito apertado neste tick (granadas: arremesso curto).</summary>
        public bool AimPressed;

        /// <summary>Q: volta para a arma anterior.</summary>
        public bool QuickSwitchPressed;
    }
}
