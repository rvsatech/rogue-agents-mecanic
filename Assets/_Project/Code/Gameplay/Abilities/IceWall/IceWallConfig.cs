using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Números da barreira de gelo (estilo gel do Free Fire).
    /// NÃO dá para escalar, pular por cima nem correr nela (layer IceWall no nonClimbableMask).
    /// A vida é medida em pentes de uma arma de referência (padrão: meio pente do rifle).
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Ice Wall", fileName = "IceWallConfig")]
    public sealed class IceWallConfig : ScriptableObject
    {
        [Header("Tamanho")]
        [Tooltip("Largura da barreira (comprimento da curva), em metros.")]
        public float width = 4.4f;

        [Tooltip("Altura em metros (não dá para escalar nem pular por cima).")]
        public float height = 2.6f;

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
        [Tooltip("Arma usada para medir a vida da barreira (padrão: rifle).")]
        public WeaponConfig healthReferenceWeapon;

        [Tooltip("Vida em pentes da arma de referência (0.5 = quebra com meio pente).")]
        public float healthInMagazines = 0.5f;

        [Tooltip("Vida usada só se não houver arma de referência.")]
        public float health = 300f;

        [Tooltip("Segundos até a barreira sumir sozinha.")]
        public float lifetime = 60f;

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

        [HideInInspector]
        [Tooltip("Versão dos valores padrão já aplicados (o menu Rogue atualiza configs antigas).")]
        public int defaultsVersion;

        /// <summary>Vida final: dano da arma de referência x balas no pente x pentes.</summary>
        public float MaxHealth
        {
            get
            {
                WeaponConfig weapon = healthReferenceWeapon;
                if (weapon == null || weapon.magazineSize <= 0) return health;
                return weapon.damage * weapon.magazineSize * healthInMagazines;
            }
        }
    }
}
