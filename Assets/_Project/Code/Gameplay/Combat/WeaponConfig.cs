using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Grupos do inventário (B): o que vai na tecla 1, na 2 e no G.</summary>
    public enum LoadoutCategory
    {
        Primary,   // 1: UMP, AWP ou a 12
        Secondary, // 2: pistola
        Gadget     // G: granada, flash ou smoke
    }

    /// <summary>Que arma é (e a posição dela na lista do PlayerWeapons).</summary>
    public enum WeaponSlot
    {
        Primary = 0,   // UMP (primária: tecla 1)
        Secondary = 1, // pistola (secundária: tecla 2)
        Melee = 2,     // faca (tecla 3)
        Gadget = 3,    // parede de gelo (tecla E)
        Sniper = 4,    // AWP (primária pelo inventário)
        Medkit = 5,    // kit médico (tecla 4)
        Shotgun = 6,   // a 12 (primária pelo inventário)
        Frag = 7,      // granada (G pelo inventário)
        Flashbang = 8, // flash (G pelo inventário)
        Smoke = 9      // smoke (G pelo inventário)
    }

    /// <summary>Como a arma ataca.</summary>
    public enum WeaponKind
    {
        Hitscan, // tiro instantâneo (raio)
        Melee,   // golpe de perto
        Gadget,     // equipamento (ex.: barreira de gelo)
        Consumable, // item de usar (kit médico): ao sacar já começa a usar
        Throwable   // de jogar (granada, flash, smoke)
    }

    /// <summary>O que a coisa jogada faz ao estourar.</summary>
    public enum ThrowableType
    {
        Frag,  // granada: dano em círculo
        Flash, // flash: tela branca (mais perto = mais branco)
        Smoke  // smoke: nuvem que tapa a visão
    }

    /// <summary>Peça da arma que se mexe sozinha depois do tiro (só visual).</summary>
    public enum WeaponAction
    {
        None,     // nada a cada tiro (UMP: a alavanca só é puxada na recarga)
        Blowback, // pistola: o ferrolho corre para trás e volta a cada tiro
        Pump,     // a 12: a mão esquerda bombeia depois de cada tiro
        Bolt      // AWP: a mão direita puxa o ferrolho depois de cada tiro
    }

    /// <summary>
    /// Números de uma arma. Cada arma do jogo = um arquivo destes (Config/Weapons).
    /// Editável no Inspector com o jogo rodando.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Weapon", fileName = "WeaponConfig")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Header("Identidade")]
        public string displayName = "UMP";
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

        [Header("Dano por parte do corpo (multiplicador do dano)")]
        [Tooltip("Cabeça (2 = dobro: um tiro na cabeça vale dois).")]
        public float headshotMultiplier = 2f;

        [Tooltip("Peito (parte de cima do tronco).")]
        public float chestMultiplier = 1f;

        [Tooltip("Barriga (parte de baixo do tronco).")]
        public float stomachMultiplier = 1f;

        [Tooltip("Braços.")]
        public float armMultiplier = 0.75f;

        [Tooltip("Pernas.")]
        public float legMultiplier = 0.75f;

        [Header("Luneta (sniper)")]
        [Tooltip("Campo de visão na luneta (graus). 0 = arma sem luneta (mira de ombro normal).")]
        public float scopeFieldOfView = 0f;

        [Tooltip("Imprecisão na luneta, parado (graus). Sem luneta vale spreadDegrees.")]
        public float scopedSpreadDegrees = 0f;

        [Tooltip("Imprecisão extra andando/correndo (graus a mais na velocidade de corrida).")]
        public float movingSpreadDegrees = 0f;

        [Tooltip("Velocidade andando na luneta (0.55 = 55%).")]
        [Range(0.1f, 1f)] public float scopedMoveSpeedMultiplier = 1f;

        [Tooltip("Depois do tiro, sai da luneta por este tempo (ferrolho) e volta se ainda estiver mirando.")]
        public float unscopeAfterShotTime = 0f;

        [Header("Recoil (coice da mira)")]
        [Tooltip("Quanto a mira sobe a cada tiro (graus).")]
        public float recoilVertical = 0.5f;

        [Tooltip("Quanto a mira vai para os lados a cada tiro (graus, segue um padrão fixo — dá para aprender).")]
        public float recoilHorizontal = 0.3f;

        [Tooltip("Máximo que a mira sobe numa rajada (graus). Depois disso só balança para os lados.")]
        public float recoilMaxVertical = 7f;

        [Tooltip("Segundos que o coice leva para entrar (0 = de uma vez; um pouco deixa mais suave).")]
        public float recoilKickTime = 0.05f;

        [Tooltip("Depois de parar de atirar, espera isto (s) e a mira volta sozinha.")]
        public float recoilRecoveryDelay = 0.12f;

        [Tooltip("Velocidade com que a mira volta (graus por segundo).")]
        public float recoilRecoverySpeed = 12f;

        [Tooltip("Coice agachado (0.8 = 80%).")]
        public float recoilCrouchFactor = 0.8f;

        [Tooltip("Coice deitado.")]
        public float recoilProneFactor = 0.6f;

        [Tooltip("Coice no ar / na parede.")]
        public float recoilAirFactor = 1.3f;

        [Tooltip("Coice mirando (botão direito / luneta), em relação ao normal. 0,6 = 40% menos coice.")]
        [Range(0f, 1f)] public float recoilAimFactor = 0.6f;

        [Tooltip("Quanto a arma inclina para cima na mão a cada tiro (só visual, graus).")]
        public float recoilVisualTilt = 4f;

        [Header("Munição (só armas de tiro)")]
        [Tooltip("Balas no pente.")]
        public int magazineSize = 30;

        [Tooltip("Balas de reserva (fora do pente).")]
        public int reserveAmmo = 120;

        [Tooltip("TESTE: balas infinitas — o pente nunca esvazia. O R ainda faz a recarga inteira (para ver a animação).")]
        public bool infiniteMagazine = true;

        [Tooltip("Reserva infinita: o pente acaba e recarrega, mas a reserva nunca zera.")]
        public bool infiniteReserve = true;

        [Tooltip("Segundos para recarregar.")]
        public float reloadTime = 2f;

        [Tooltip("Recarrega sozinho quando o pente esvazia.")]
        public bool autoReloadWhenEmpty = true;

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

        [Tooltip("Tamanho do clarão do cano, da luz e da fumacinha (1 = UMP).")]
        public float muzzleFlashScale = 1f;

        [Tooltip("Comprimento da cápsula que sai da arma (m). 0 = não solta cápsula.")]
        public float shellLength = 0f;

        [Tooltip("Cor da cápsula (latão; a 12 é vermelha).")]
        public Color shellColor = new Color(0.86f, 0.66f, 0.3f);

        [Header("Mecanismo (só visual)")]
        [Tooltip("O que mexe depois do tiro: pistola = ferrolho volta sozinho; a 12 = bombeia; AWP = ferrolho na mão.")]
        public WeaponAction action = WeaponAction.None;

        [Tooltip("A 12 e AWP: segundos depois do tiro para começar a bombear / puxar o ferrolho.")]
        public float actionDelay = 0.2f;

        [Tooltip("A 12 e AWP: duração do movimento (s).")]
        public float actionDuration = 0.45f;

        [Tooltip("Quanto a peça corre para trás (m): ferrolho, telha da 12 ou alavanca da UMP na recarga.")]
        public float actionTravel = 0.05f;

        [Tooltip("Recarga de cartucho em cartucho (a 12): quantas vezes a mão põe um cartucho. 0 = troca o pente.")]
        public int reloadShellInserts = 0;

        [Header("Chumbo (a 12)")]
        [Tooltip("Quantos chumbos por tiro (1 = arma normal). Cada chumbo dá o dano da arma.")]
        public int pellets = 1;

        [Tooltip("A partir desta distância (m) o dano começa a cair (0 = não cai).")]
        public float falloffStart = 0f;

        [Tooltip("Nesta distância (m) o dano chega ao mínimo.")]
        public float falloffEnd = 0f;

        [Tooltip("Dano mínimo longe, em relação ao normal (0,25 = 25%).")]
        [Range(0f, 1f)] public float falloffMinFactor = 1f;

        [Header("Granada / flash / smoke (arremesso)")]
        public ThrowableType throwableType = ThrowableType.Frag;

        [Tooltip("Velocidade do arremesso longo (clique esquerdo), m/s.")]
        public float throwSpeed = 16f;

        [Tooltip("Velocidade do arremesso curto (clique direito), m/s.")]
        public float shortThrowSpeed = 7f;

        [Tooltip("Quantos graus o arremesso sai acima da mira.")]
        public float throwUpAngle = 8f;

        [Tooltip("Segundos do arremesso até estourar.")]
        public float fuseTime = 2.5f;

        [Tooltip("Tempo do braço depois de soltar; depois volta para a arma.")]
        public float throwRecoveryTime = 0.3f;

        [Tooltip("Raio do efeito (m). Granada: 4,4 (círculo de 8,8 m). Smoke: 8,8 (nuvem de 17,6 m).")]
        public float effectRadius = 4.4f;

        [Tooltip("Granada: dano no centro do círculo.")]
        public float maxDamage = 100f;

        [Tooltip("Granada: dano na borda do círculo.")]
        public float minDamage = 30f;

        [Tooltip("Flash: até esta distância (m) ainda cega. Mais perto = mais branco e por mais tempo.")]
        public float flashMaxDistance = 40f;

        [Tooltip("Flash: até esta distância (m) a tela fica TODA branca e o jogo fica surdo pelo tempo inteiro.")]
        public float flashFullBlindDistance = 12f;

        [Tooltip("Flash: segundos de tela toda branca (perto). Com o tempo de voltar dá ~4 s.")]
        public float flashMaxBlindTime = 3f;

        [Tooltip("Flash: segundos para a tela e o som voltarem ao normal.")]
        public float flashFadeTime = 1f;

        [Tooltip("Smoke: altura da nuvem (m).")]
        public float smokeHeight = 5.2f;

        [Tooltip("Smoke: quanto tempo a nuvem dura (s).")]
        public float smokeDuration = 15f;

        [Tooltip("Material das partículas de fumaça (o menu Rogue preenche).")]
        public Material particleMaterial;

        [Tooltip("Material do brilho: fogo da granada e clarão da flash (o menu Rogue preenche).")]
        public Material glowMaterial;

        [Header("Item (kit médico)")]
        [Tooltip("Segundos usando até curar (anda devagar nesse tempo; rolar, deslizar, parede ou beirada cancelam).")]
        public float useTime = 0f;

        [Tooltip("Quanto da vida máxima cura (0,4 = 40%).")]
        [Range(0f, 1f)] public float healPercent = 0f;

        [Tooltip("Quantos tem no começo.")]
        public int itemCount = 0;

        [Tooltip("Para testes: não gasta.")]
        public bool infiniteItems = true;

        [Header("Sons (o menu Rogue preenche; dá para trocar aqui)")]
        public SoundCue fireSound = new SoundCue();
        public SoundCue equipSound = new SoundCue();
        [Tooltip("Clique sem bala nenhuma.")]
        public SoundCue dryFireSound = new SoundCue();
        [Tooltip("Recarga: tirando o pente.")]
        public SoundCue reloadOutSound = new SoundCue();
        [Tooltip("Recarga: pondo o pente.")]
        public SoundCue reloadInSound = new SoundCue();
        [Tooltip("Recarga: engatilhando / soltando o ferrolho.")]
        public SoundCue reloadRackSound = new SoundCue();
        [Tooltip("Faca: golpe no ar.")]
        public SoundCue swingSound = new SoundCue();
        [Tooltip("Faca: acertou algo.")]
        public SoundCue meleeHitSound = new SoundCue();
        [Tooltip("Item: terminou de usar (kit médico: curou).")]
        public SoundCue useCompleteSound = new SoundCue();
        [Tooltip("Granada: quicando no chão/parede.")]
        public SoundCue bounceSound = new SoundCue();
        [Tooltip("Granada: estouro (explosão, flash ou smoke).")]
        public SoundCue detonateSound = new SoundCue();
        [Tooltip("Flash: zumbido no ouvido de quem ficou cego.")]
        public SoundCue ringSound = new SoundCue();

        [HideInInspector]
        [Tooltip("Versão dos valores padrão já aplicados (o menu Rogue atualiza configs antigas).")]
        public int defaultsVersion;

        public bool UsesAmmo => kind == WeaponKind.Hitscan && magazineSize > 0;

        public bool HasScope => scopeFieldOfView > 0f;

        /// <summary>Recarga de cartucho em cartucho: parte da recarga em que a mão põe os cartuchos.</summary>
        public const float ShellReloadStart = 0.08f;
        public const float ShellReloadEnd = 0.8f;

        /// <summary>
        /// Momento (fração da recarga) em que o cartucho número <paramref name="index"/> entra na arma.
        /// A animação e o som usam a mesma conta.
        /// </summary>
        public float ShellInsertMark(int index)
        {
            int count = Mathf.Max(1, reloadShellInserts);
            return ShellReloadStart + (index + 0.85f) * (ShellReloadEnd - ShellReloadStart) / count;
        }

        /// <summary>Segundos depois do tiro em que a cápsula sai: na hora, na bombeada (a 12) ou no ferrolho (AWP).</summary>
        public float ShellEjectDelay
        {
            get
            {
                switch (action)
                {
                    case WeaponAction.Pump: return actionDelay + actionDuration * 0.42f;
                    case WeaponAction.Bolt: return actionDelay + actionDuration * 0.5f;
                    default: return 0f;
                }
            }
        }

        /// <summary>Quanto do dano sobra nesta distância (a 12 perde dano longe; as outras não).</summary>
        public float FalloffAt(float distance)
        {
            if (falloffEnd <= falloffStart) return 1f;
            return Mathf.Lerp(1f, falloffMinFactor, Mathf.InverseLerp(falloffStart, falloffEnd, distance));
        }

        /// <summary>Multiplicador de dano da parte do corpo atingida.</summary>
        public float ZoneMultiplier(HitZoneKind zone)
        {
            switch (zone)
            {
                case HitZoneKind.Head: return headshotMultiplier;
                case HitZoneKind.Chest: return chestMultiplier;
                case HitZoneKind.Stomach: return stomachMultiplier;
                case HitZoneKind.Arms: return armMultiplier;
                case HitZoneKind.Legs: return legMultiplier;
                default: return 1f;
            }
        }
    }
}
