using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Números da barreira de gelo (estilo gel do Free Fire).
    /// A altura não fica aqui: é sempre o limite que o personagem consegue escalar
    /// (pulo + alcance da mão, do MovementConfig) + extraHeight.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Ice Wall", fileName = "IceWallConfig")]
    public sealed class IceWallConfig : ScriptableObject
    {
        [Header("Tamanho")]
        [Tooltip("Largura da barreira (comprimento da curva), em metros.")]
        public float width = 4f;

        [Tooltip("Altura extra além do limite de escalada (0 = exatamente o limite).")]
        public float extraHeight = 0f;

        [Tooltip("Espessura, em metros.")]
        public float thickness = 0.35f;

        [Tooltip("Quanto a barreira curva para dentro, em graus (0 = reta). Protege dos tiros pelo lado.")]
        [Range(0f, 90f)] public float curveDegrees = 40f;

        [Tooltip("Quantos blocos formam a curva (mais = curva mais lisa).")]
        [Range(1, 12)] public int segments = 7;

        [Header("Colocação")]
        [Tooltip("Alcance medido em slides (2 = duas vezes a distância do slide).")]
        public float rangeInSlides = 2f;

        [Tooltip("Distância mínima do jogador, em metros.")]
        public float minDistance = 1.6f;

        [Tooltip("Inclinação máxima do chão onde dá para colocar, em graus.")]
        public float maxGroundSlope = 35f;

        [Header("Vida e duração")]
        public float health = 600f;

        [Tooltip("Segundos até a barreira sumir sozinha.")]
        public float lifetime = 25f;

        [Tooltip("Máximo de barreiras ao mesmo tempo (a mais antiga some).")]
        public int maxActiveWalls = 3;

        [Tooltip("Segundos de espera entre uma colocação e outra.")]
        public float cooldown = 0.8f;

        [Header("Visual")]
        public Material wallMaterial;
        [Tooltip("Prévia transparente (lugar válido).")]
        public Material previewMaterial;
        [Tooltip("Prévia transparente (lugar inválido).")]
        public Material invalidPreviewMaterial;
    }
}
