using System.Collections.Generic;
using Rogue.Gameplay;
using UnityEditor;
using UnityEngine;

namespace Rogue.EditorTools
{
    /// <summary>
    /// Toda vez que a Unity termina de compilar, aplica as atualizações de configuração
    /// (MovementConfig, armas, gelo): os números novos valem sem precisar rodar o menu Rogue.
    /// A cena em si (sons, layers, peças das armas) continua vindo de Rogue > Criar cena Sandbox_Movement.
    /// </summary>
    [InitializeOnLoad]
    public static class ConfigAutoUpgrader
    {
        static ConfigAutoUpgrader()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var changed = new List<string>();

            var movement = AssetDatabase.LoadAssetAtPath<MovementConfig>($"{ConfigUpgrades.ConfigFolder}/MovementConfig.asset");
            if (ConfigUpgrades.UpgradeMovement(movement))
            {
                EditorUtility.SetDirty(movement);
                changed.Add("MovementConfig");
            }

            var cameraConfig = AssetDatabase.LoadAssetAtPath<CameraConfig>($"{ConfigUpgrades.ConfigFolder}/CameraConfig.asset");
            if (ConfigUpgrades.UpgradeCamera(cameraConfig))
            {
                EditorUtility.SetDirty(cameraConfig);
                changed.Add("CameraConfig");
            }

            WeaponConfig rifle = null;
            if (AssetDatabase.IsValidFolder(ConfigUpgrades.WeaponConfigFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:WeaponConfig", new[] { ConfigUpgrades.WeaponConfigFolder }))
                {
                    var weapon = AssetDatabase.LoadAssetAtPath<WeaponConfig>(AssetDatabase.GUIDToAssetPath(guid));
                    if (weapon == null) continue;
                    if (weapon.slot == WeaponSlot.Primary) rifle = weapon;
                    if (ConfigUpgrades.UpgradeWeapon(weapon, weapon.slot))
                    {
                        EditorUtility.SetDirty(weapon);
                        changed.Add(weapon.name);
                    }
                }
            }

            var ice = AssetDatabase.LoadAssetAtPath<IceWallConfig>($"{ConfigUpgrades.ConfigFolder}/IceWallConfig.asset");
            if (ConfigUpgrades.UpgradeIceWall(ice, rifle))
            {
                EditorUtility.SetDirty(ice);
                changed.Add("IceWallConfig");
            }

            if (changed.Count == 0) return;

            AssetDatabase.SaveAssets();
            Debug.Log("[Rogue] Configurações atualizadas sozinhas para os valores novos: " + string.Join(", ", changed));
        }
    }
}
