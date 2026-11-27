using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Em qual tecla/espaço a arma fica.</summary>
    public enum WeaponSlot
    {
        Primary = 0,   // 1: arma principal (rifle)
        Secondary = 1, // 2: pistola
        Melee = 2,     // 3: faca
        Gadget = 3     // 4: gelo
    }

    /// <summary>Como a arma ataca.</summary>
    public enum WeaponKind
    {
        Hitscan, // tiro instantâneo (raio)
        Melee,   // golpe de perto
        Gadget   // equipamento (ex.: barreira de gelo)
    }

    /// <summary>
    /// Números de uma arma. Cada arma do jogo = um arquivo destes (Config/Weapons).
    /// Editável no Inspector com o jogo rodando.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Weapon", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Header("Identidade")]
        public string displayName = "Rifle";
        public WeaponSlot slot = WeaponSlot.Primary;
        public WeaponKind kind = WeaponKind.Hitscan;

        [Header("Movimento")]
        [Tooltip("Velocidade de movimento com esta arma na mão. 1.15 = +15%.")]
        [Range(0.5f, 1.5f)] public float moveSpeedMultiplier = 1f;

        [Tooltip("Perda de velocidade correndo no chão enquanto atira. 0.2 = -20%.")]
        [Range(0f, 1f)] public float firingGroundSpeedPenalty = 0.2f;

        [Header("Troca de arma")]
        [Tooltip("Segundos para sacar a arma (não ataca enquanto isso).")]
        public float equipTime = 0.35f;

        [Header("Ataque")]
        [Tooltip("Ligado: segura o botão para atirar sem parar. Desligado: um ataque por clique.")]
        public bool automatic = true;

        [Tooltip("Ataques por segundo (máximo).")]
        public float fireRate = 10f;

        public float damage = 20f;

        [Header("Tiro (hitscan)")]
        [Tooltip("Alcance máximo em metros.")]
        public float range = 150f;

        [Tooltip("Imprecisão em graus (cone em volta da mira).")]
        public float spreadDegrees = 0.6f;

        [Tooltip("Imprecisão extra quando está no ar.")]
        public float airSpreadDegrees = 1.5f;

        [Tooltip("Multiplicador da imprecisão deitado (0.5 = metade).")]
        [Range(0f, 1f)] public float proneSpreadFactor = 0.5f;

        [Tooltip("O que o ataque acerta (tudo menos o Player).")]
        public LayerMask hitMask = ~0;

        [Header("Corpo a corpo (faca)")]
        public float meleeRange = 1.9f;
        public float meleeRadius = 0.45f;

        [Tooltip("Duração do golpe (animação).")]
        public float meleeAttackDuration = 0.32f;

        [Tooltip("Momento do golpe em que ele acerta (segundos).")]
        public float meleeHitTime = 0.1f;

        [Header("Pose")]
        [Tooltip("Onde a arma fica ao mirar, em relação ao peito (direita, cima, frente).")]
        public Vector3 aimHoldOffset = new Vector3(0.07f, -0.08f, 0.3f);

        [Tooltip("Segura com as duas mãos.")]
        public bool twoHanded = true;

        [Tooltip("Coice visual a cada tiro (metros).")]
        public float recoilDistance = 0.06f;

        [Header("Efeitos")]
        [Tooltip("Quanto tempo o rastro do tiro fica visível.")]
        public float tracerDuration = 0.05f;
        public float tracerWidth = 0.03f;

        [Tooltip("Quanto tempo o clarão do cano fica aceso.")]
        public float muzzleFlashDuration = 0.04f;
    }
}
