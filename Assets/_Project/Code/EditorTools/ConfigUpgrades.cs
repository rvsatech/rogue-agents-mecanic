using Rogue.Gameplay;
using UnityEngine;

namespace Rogue.EditorTools
{
    /// <summary>
    /// Atualiza arquivos de configuração antigos para os valores novos (uma vez por versão).
    /// Usado pelo menu Rogue e, sozinho, toda vez que a Unity termina de compilar (ConfigAutoUpgrader).
    /// Quando um valor padrão mudar: subir a versão aqui e escrever o passo novo (nunca apagar os antigos).
    /// </summary>
    public static class ConfigUpgrades
    {
        public const string ConfigFolder = "Assets/_Project/Config";
        public const string WeaponConfigFolder = "Assets/_Project/Config/Weapons";

        public const int MovementVersion = 10;
        public const int WeaponVersion = 10;
        public const int IceWallVersion = 2;
        public const int CameraVersion = 3;

        /// <summary>CameraConfig. Retorna true se mudou algo.</summary>
        public static bool UpgradeCamera(CameraConfig config)
        {
            if (config == null || config.defaultsVersion >= CameraVersion) return false;

            if (config.defaultsVersion < 1)
            {
                // v1: tudo maior na tela — FOV 75° -> 60° (~33% maior); mira com zoom de verdade (62° -> 48°).
                config.fieldOfView = 60f;
                config.aimFieldOfView = 48f;
            }

            if (config.defaultsVersion < 2)
            {
                // v2 (estilo Rogue Agents): na parede a câmera se afasta (sem zoom: MovementConfig.wallRunAllowAim).
                config.wallRunDistanceBoost = 0.9f;
            }

            if (config.defaultsVersion < 3)
            {
                // v3: mira mais perto do personagem; tremor no tiro; na parede o personagem fica mais no meio da tela.
                config.aimDistance = 1.45f;
                config.aimShoulderOffset = 0.62f;
                config.aimFieldOfView = 44f;
                config.wallRunShoulderOffset = 0.2f;
                config.aimShotTremor = 0.55f;
                config.hipShotTremor = 0.2f;
                config.tremorRecovery = 16f;
            }

            config.defaultsVersion = CameraVersion;
            return true;
        }

        /// <summary>MovementConfig. Retorna true se mudou algo.</summary>
        public static bool UpgradeMovement(MovementConfig config)
        {
            if (config == null || config.defaultsVersion >= MovementVersion) return false;

            if (config.defaultsVersion < 2)
            {
                config.airStrafeAcceleration = 14f;
                config.slideDistance = 6f;
                config.wallRunSinkGravity = 0.5f;
                config.wallRunMaxSinkSpeed = 0.35f;
                config.hangHandHeight = 1.8f;
            }

            if (config.defaultsVersion < 3)
            {
                // v3: deitado parado, levantar em etapas, wall run de no máximo 2 s.
                config.proneSpeed = 0f;
                config.proneToCrouchTime = 0.6f;
                config.crouchToStandTime = 0.5f;
                config.getUpStartSpeedFactor = 0.25f;
                config.wallRunDuration = 2f;
                config.wallRunEndPushSpeed = 1.5f;
            }

            if (config.defaultsVersion < 4)
            {
                // v4: lentidão depois de levantar pela metade (1,5 s) e personagem 15% mais rápido.
                config.getUpRecoveryTime = 1.5f;
                config.runSpeed = 7.475f;
            }

            // v5: barreira de gelo volta a não ser escalável (layer IceWall no nonClimbableMask).
            if (config.defaultsVersion < 5)
            {
                int iceLayer = LayerMask.NameToLayer("IceWall");
                if (iceLayer >= 0) config.nonClimbableMask = config.nonClimbableMask.value | (1 << iceLayer);
            }

            if (config.defaultsVersion < 6)
            {
                // v6: cambalhota mais lenta (0,8 s, mesma distância, termina devagar); parede 20% mais rápida.
                config.rollDuration = 0.8f;
                config.rollEndSpeedFactor = 0.3f;
                config.rollJumpCancelTime = 0.47f;
                config.wallRunSpeedFactor = 1.2f;
            }

            // v7: correr na parede dura no máximo 1 s (somando as paredes até tocar o chão); depois cai.
            if (config.defaultsVersion < 7) config.wallRunDuration = 1f;

            if (config.defaultsVersion < 8)
            {
                // v8: cambalhota mais longa (6 m em 0,8 s) e sem freada no fim: termina na velocidade da corrida.
                config.rollDistance = 6f;
                config.rollEndSpeedFactor = 1f;
            }

            if (config.defaultsVersion < 9)
            {
                // v9 (ritmo do Rogue Agents): jogo mais lento e com peso, mas fluido.
                config.runSpeed = 6f;
                config.crouchSpeed = 2.6f;
                config.groundAcceleration = 40f;
                config.groundDeceleration = 50f;
                config.gravity = 25f;
                config.forwardJumpBoost = 0.25f;   // pulo para frente com mais impulso
                config.slideDistance = 5f;
                config.slideMinSpeed = 3f;
                config.rollDistance = 4.5f;
                config.rollDuration = 0.75f;
                config.rollCooldown = 0.15f;       // quase sem espera entre cambalhotas
                config.rollJumpCancelTime = 0.35f;
                config.rollActionCancelTime = 0.4f;
                config.rollMoveCancelTime = 0.55f;
                config.climbUpSpeed = 4f;
                config.climbForwardSpeed = 3.2f;
                config.wallRunAllowAim = false;    // na parede não mira (sem zoom e sem luneta)
            }

            if (config.defaultsVersion < 10)
            {
                // v10: +15% de velocidade, virada e air strafe leves, pulo com mais impulso, cambalhota sem parada.
                config.runSpeed = 6.9f;
                config.crouchSpeed = 3f;
                config.groundAcceleration = 80f;
                config.groundDeceleration = 80f;
                config.airStrafeAcceleration = 45f;
                config.forwardJumpBoost = 0.35f;
                config.rollDistance = 4.2f;
                config.rollDuration = 0.6f;
                config.rollJumpCancelTime = 0.3f;
                config.rollActionCancelTime = 0.3f;
                config.rollMoveCancelTime = 0.48f;
                config.wallClimbSpeed = 4f;
                config.wallClimbMaxHeight = 1.8f;
                config.wallClimbDetectDistance = 0.6f;
            }

            config.defaultsVersion = MovementVersion;
            return true;
        }

        /// <summary>Arquivo de uma arma. Retorna true se mudou algo.</summary>
        public static bool UpgradeWeapon(WeaponConfig config, WeaponSlot slot)
        {
            if (config == null || config.defaultsVersion >= WeaponVersion) return false;

            // v1: munição e recarga.
            if (config.defaultsVersion < 1) SetAmmoDefaults(config, slot);

            // v2: reserva infinita para testes.
            if (config.defaultsVersion < 2) config.infiniteReserve = config.UsesAmmo;

            if (config.defaultsVersion < 3)
            {
                // v3: gelo sai na mão na hora (4 + clique coloca instantâneo) e balas infinitas para testes.
                if (slot == WeaponSlot.Gadget) config.equipTime = 0f;
                config.infiniteMagazine = config.UsesAmmo;
            }

            // v4: dano por parte do corpo e recoil.
            if (config.defaultsVersion < 4) SetZoneAndRecoilDefaults(config, slot);

            // v5: vida 150 e dano 10–20 nas armas comuns; partes novas (peito, barriga, braços); AWP.
            if (config.defaultsVersion < 5) SetDamageV5(config, slot);

            // v6: mais coice no tiro normal, menos mirando; 0,5 s depois de sacar para poder atirar.
            if (config.defaultsVersion < 6) SetRecoilAndEquipV6(config, slot);

            // v7: a principal virou a UMP (submetralhadora): cadência menor (10 -> 7 tiros por segundo).
            if (config.defaultsVersion < 7 && slot == WeaponSlot.Primary)
            {
                config.displayName = "UMP";
                config.fireRate = 7f;
            }

            // v8: flash cega de verdade (tudo branco até 12 m por ~4 s, alcance 40 m, e deixa surdo);
            // granada maior (raio 4,4 m); smoke 4x maior (raio 8,8 m, altura 5,2 m).
            if (config.defaultsVersion < 8)
            {
                if (slot == WeaponSlot.Frag)
                {
                    config.effectRadius = 4.4f;
                }
                else if (slot == WeaponSlot.Flashbang)
                {
                    config.flashMaxDistance = 40f;
                    config.flashFullBlindDistance = 12f;
                    config.flashMaxBlindTime = 3f;
                    config.flashFadeTime = 1f;
                }
                else if (slot == WeaponSlot.Smoke)
                {
                    config.effectRadius = 8.8f;
                    config.smokeHeight = 5.2f;
                }
            }

            // v9: visual das armas — clarão por arma, cápsulas, mecanismo (ferrolho da pistola, bombeada da 12,
            // ferrolho da AWP) e recarga da 12 cartucho por cartucho.
            if (config.defaultsVersion < 9) SetWeaponVisualsV9(config, slot);

            // v10: troca de arma mais lenta e com corpo (guarda, saca e a arma assenta).
            if (config.defaultsVersion < 10)
            {
                if (slot == WeaponSlot.Primary || slot == WeaponSlot.Sniper || slot == WeaponSlot.Shotgun)
                    config.equipTime = 0.75f;
                else if (slot == WeaponSlot.Secondary)
                    config.equipTime = 0.6f;
            }

            config.defaultsVersion = WeaponVersion;
            return true;
        }

        /// <summary>v9: números só de visual (não mudam o jogo): clarão, cápsula, mecanismo e recarga da 12.</summary>
        private static void SetWeaponVisualsV9(WeaponConfig config, WeaponSlot slot)
        {
            var brass = new Color(0.86f, 0.66f, 0.3f);
            config.shellColor = brass;
            config.reloadShellInserts = 0;
            config.actionDelay = 0.2f;
            config.actionDuration = 0.45f;

            switch (slot)
            {
                case WeaponSlot.Primary: // UMP: sem peça mexendo no tiro; a alavanca é puxada na recarga
                    config.action = WeaponAction.None;
                    config.actionTravel = 0.05f;
                    config.muzzleFlashScale = 1f;
                    config.shellLength = 0.022f;
                    break;
                case WeaponSlot.Secondary: // pistola: ferrolho corre para trás a cada tiro
                    config.action = WeaponAction.Blowback;
                    config.actionTravel = 0.028f;
                    config.muzzleFlashScale = 0.75f;
                    config.shellLength = 0.019f;
                    break;
                case WeaponSlot.Sniper: // AWP: ferrolho manual depois de cada tiro (no tempo do som do tiro: puxa ~0,55 s)
                    config.action = WeaponAction.Bolt;
                    config.actionDelay = 0.4f;
                    config.actionDuration = 0.5f;
                    config.actionTravel = 0.09f;
                    config.muzzleFlashScale = 1.5f;
                    config.shellLength = 0.06f;
                    break;
                case WeaponSlot.Shotgun: // a 12: bombeia depois de cada tiro (no tempo do som: ~0,45 s); recarga põe 4 cartuchos
                    config.action = WeaponAction.Pump;
                    config.actionDelay = 0.42f;
                    config.actionDuration = 0.27f;
                    config.actionTravel = 0.09f;
                    config.muzzleFlashScale = 1.8f;
                    config.shellLength = 0.07f;
                    config.shellColor = new Color(0.75f, 0.12f, 0.1f);
                    config.reloadShellInserts = 4;
                    break;
                default: // faca, gelo, kit e granadas: sem tiro
                    config.action = WeaponAction.None;
                    config.actionTravel = 0f;
                    config.muzzleFlashScale = 1f;
                    config.shellLength = 0f;
                    break;
            }
        }

        /// <summary>Barreira de gelo. Retorna true se mudou algo.</summary>
        public static bool UpgradeIceWall(IceWallConfig config, WeaponConfig rifle)
        {
            if (config == null) return false;

            bool changed = false;
            if (config.healthReferenceWeapon == null && rifle != null)
            {
                config.healthReferenceWeapon = rifle; // vida = meio pente do rifle
                changed = true;
            }

            if (config.defaultsVersion < IceWallVersion)
            {
                if (config.defaultsVersion < 1)
                {
                    // v1: meio pente, quebra sozinha em 1 minuto.
                    config.healthInMagazines = 0.5f;
                    config.health = 300f;
                    config.lifetime = 60f;
                }

                // v2: não escalável de novo e um pouco maior.
                config.height = 2.6f;
                config.width = 4.4f;

                config.defaultsVersion = IceWallVersion;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Dano (v5), com vida 150: rifle 14 e pistola 20 (cabeça x2 = "um tiro na cabeça vale dois"),
        /// faca 40 (4 golpes), AWP 100: do peito para cima mata com 1 tiro; barriga, braços e pernas com 2.
        /// </summary>
        private static void SetDamageV5(WeaponConfig config, WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Primary:
                    config.damage = 14f;
                    SetZones(config, head: 2f, chest: 1f, stomach: 1f, arms: 0.75f, legs: 0.75f);
                    break;
                case WeaponSlot.Secondary:
                    config.damage = 20f;
                    SetZones(config, head: 2f, chest: 1f, stomach: 1f, arms: 0.75f, legs: 0.75f);
                    break;
                case WeaponSlot.Melee:
                    config.damage = 40f;
                    SetZones(config, head: 1f, chest: 1f, stomach: 1f, arms: 1f, legs: 1f);
                    break;
                case WeaponSlot.Sniper:
                    config.damage = 100f;
                    // cabeça 200 e peito 160: 1 tiro (vida 150) · barriga/braços/pernas 80: 2 tiros
                    SetZones(config, head: 2f, chest: 1.6f, stomach: 0.8f, arms: 0.8f, legs: 0.8f);
                    break;
                case WeaponSlot.Shotgun:
                    config.damage = 14f; // por chumbo (8 chumbos)
                    SetZones(config, head: 2f, chest: 1f, stomach: 1f, arms: 0.75f, legs: 0.75f);
                    break;
                default:
                    SetZones(config, head: 1f, chest: 1f, stomach: 1f, arms: 1f, legs: 1f);
                    break;
            }
        }

        /// <summary>
        /// v6: coice ~33% maior no tiro normal e 40% menor mirando (recoilAimFactor 0,6);
        /// armas de fogo levam 0,5 s depois de sacar para poder atirar (faca e gelo não mudam).
        /// </summary>
        private static void SetRecoilAndEquipV6(WeaponConfig config, WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Primary:
                    config.recoilVertical = 0.6f;
                    config.recoilHorizontal = 0.4f;
                    config.recoilMaxVertical = 9f;
                    config.recoilVisualTilt = 5f;
                    config.recoilAimFactor = 0.6f;
                    config.equipTime = 0.5f;
                    break;
                case WeaponSlot.Secondary:
                    config.recoilVertical = 2.1f;
                    config.recoilHorizontal = 0.45f;
                    config.recoilMaxVertical = 8f;
                    config.recoilVisualTilt = 12f;
                    config.recoilAimFactor = 0.6f;
                    config.equipTime = 0.5f;
                    break;
                case WeaponSlot.Sniper:
                    config.recoilVertical = 4.5f;
                    config.recoilHorizontal = 0.6f;
                    config.recoilMaxVertical = 4.5f;
                    config.recoilVisualTilt = 16f;
                    config.recoilAimFactor = 0.6f;
                    config.equipTime = 0.5f;
                    break;
                case WeaponSlot.Shotgun:
                    config.recoilVertical = 4f;
                    config.recoilHorizontal = 0.8f;
                    config.recoilMaxVertical = 4f;
                    config.recoilVisualTilt = 18f;
                    config.recoilAimFactor = 0.6f;
                    config.equipTime = 0.5f;
                    break;
            }
        }

        private static void SetZones(WeaponConfig config, float head, float chest, float stomach, float arms, float legs)
        {
            config.headshotMultiplier = head;
            config.chestMultiplier = chest;
            config.stomachMultiplier = stomach;
            config.armMultiplier = arms;
            config.legMultiplier = legs;
        }

        /// <summary>Dano por parte do corpo e recoil (v4) por tipo de arma.</summary>
        private static void SetZoneAndRecoilDefaults(WeaponConfig config, WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Primary: // rifle: sobe aos poucos, padrão para os lados, volta médio
                    config.headshotMultiplier = 2f;
                    config.legMultiplier = 0.75f;
                    config.recoilVertical = 0.45f;
                    config.recoilHorizontal = 0.3f;
                    config.recoilMaxVertical = 7f;
                    config.recoilKickTime = 0.05f;
                    config.recoilRecoveryDelay = 0.12f;
                    config.recoilRecoverySpeed = 12f;
                    config.recoilVisualTilt = 4f;
                    break;
                case WeaponSlot.Secondary: // pistola: coice forte por tiro, volta rápido
                    config.headshotMultiplier = 2f;
                    config.legMultiplier = 0.75f;
                    config.recoilVertical = 1.6f;
                    config.recoilHorizontal = 0.35f;
                    config.recoilMaxVertical = 6f;
                    config.recoilKickTime = 0.04f;
                    config.recoilRecoveryDelay = 0.08f;
                    config.recoilRecoverySpeed = 22f;
                    config.recoilVisualTilt = 10f;
                    break;
                case WeaponSlot.Shotgun: // a 12: coice forte por tiro
                    config.headshotMultiplier = 2f;
                    config.legMultiplier = 0.75f;
                    config.recoilVertical = 3f;
                    config.recoilHorizontal = 0.6f;
                    config.recoilMaxVertical = 3f;
                    config.recoilKickTime = 0.05f;
                    config.recoilRecoveryDelay = 0.25f;
                    config.recoilRecoverySpeed = 12f;
                    config.recoilVisualTilt = 16f;
                    break;
                case WeaponSlot.Sniper: // AWP: um coice forte por tiro, volta devagar
                    config.recoilVertical = 3.5f;
                    config.recoilHorizontal = 0.5f;
                    config.recoilMaxVertical = 3.5f;
                    config.recoilKickTime = 0.06f;
                    config.recoilRecoveryDelay = 0.3f;
                    config.recoilRecoverySpeed = 10f;
                    config.recoilVisualTilt = 14f;
                    break;
                default: // faca e gelo: sem coice, sem bônus por parte do corpo
                    config.headshotMultiplier = 1f;
                    config.legMultiplier = 1f;
                    config.recoilVertical = 0f;
                    config.recoilHorizontal = 0f;
                    config.recoilVisualTilt = 0f;
                    break;
            }

            config.recoilCrouchFactor = 0.8f;
            config.recoilProneFactor = 0.6f;
            config.recoilAirFactor = 1.3f;
        }

        /// <summary>Munição e recarga (v1) por tipo de arma.</summary>
        private static void SetAmmoDefaults(WeaponConfig config, WeaponSlot slot)
        {
            switch (slot)
            {
                case WeaponSlot.Primary:   // rifle: 30 no pente, 120 de reserva, 2 s para recarregar
                    config.magazineSize = 30;
                    config.reserveAmmo = 120;
                    config.reloadTime = 2f;
                    break;
                case WeaponSlot.Secondary: // pistola: 12 no pente, 48 de reserva, 1,4 s
                    config.magazineSize = 12;
                    config.reserveAmmo = 48;
                    config.reloadTime = 1.4f;
                    break;
                case WeaponSlot.Shotgun:   // a 12: 5 cartuchos, 30 de reserva, 2,5 s
                    config.magazineSize = 5;
                    config.reserveAmmo = 30;
                    config.reloadTime = 2.5f;
                    break;
                case WeaponSlot.Sniper:    // AWP: 5 no pente, 20 de reserva, 3,2 s
                    config.magazineSize = 5;
                    config.reserveAmmo = 20;
                    config.reloadTime = 3.2f;
                    break;
                default:                   // faca e gelo não usam bala
                    config.magazineSize = 0;
                    config.reserveAmmo = 0;
                    break;
            }

            config.autoReloadWhenEmpty = true;
        }
    }
}
