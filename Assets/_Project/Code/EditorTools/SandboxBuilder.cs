using System.Collections.Generic;
using Rogue.DebugTools;
using Rogue.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.EditorTools
{
    /// <summary>
    /// Menu "Rogue > Criar cena Sandbox_Movement": monta sozinho a arena de teste
    /// (chão, paredes de wall-run, degraus de escalada, túnel baixo, rampa, alvos),
    /// o jogador (manequim + 4 armas + gelo), a câmera e o HUD.
    /// Também cria/atualiza os arquivos de configuração. Pode rodar de novo quando quiser.
    /// </summary>
    public static class SandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox_Movement.unity";
        private const string ConfigFolder = "Assets/_Project/Config";
        private const string WeaponConfigFolder = "Assets/_Project/Config/Weapons";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string CharacterModelPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Godot_Standard.gltf";

        /// <summary>Sobe quando os valores padrão do MovementConfig mudam (atualiza configs antigas uma vez).</summary>
        private const int MovementDefaultsVersion = 2;

        [MenuItem("Rogue/Criar cena Sandbox_Movement")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // ---------- Layers ----------
            int groundLayer = EnsureLayer("Ground");
            int climbableLayer = EnsureLayer("Climbable");
            int nonClimbableLayer = EnsureLayer("NonClimbable");
            int playerLayer = EnsureLayer("Player");
            int targetLayer = EnsureLayer("Target");
            EnsureLayer("Projectile");

            int environmentMask = ~(1 << playerLayer);

            // ---------- Materiais ----------
            Material mGround = GetMaterial("M_Ground", new Color(0.36f, 0.37f, 0.39f));
            Material mWall = GetMaterial("M_Wall", new Color(0.26f, 0.42f, 0.62f));
            Material mBox = GetMaterial("M_Box", new Color(0.85f, 0.55f, 0.2f));
            Material mIce = GetMaterial("M_Ice", new Color(0.55f, 0.9f, 1f));
            Material mTarget = GetMaterial("M_Target", new Color(0.85f, 0.18f, 0.18f));
            Material mMarker = GetMaterial("M_Marker", new Color(0.95f, 0.95f, 0.95f));
            Material mPlayer = GetMaterial("M_Player", new Color(0.2f, 0.75f, 0.45f));
            Material mDark = GetMaterial("M_Dark", new Color(0.1f, 0.1f, 0.12f));
            Material mGun = GetMaterial("M_Gun", new Color(0.16f, 0.17f, 0.19f));
            Material mSteel = GetMaterial("M_Steel", new Color(0.78f, 0.8f, 0.83f), 0.85f);
            Material mGadget = GetMaterial("M_Gadget", new Color(0.45f, 0.85f, 1f), 0.7f);
            Material mIceWall = GetMaterial("M_IceWall", new Color(0.62f, 0.9f, 1f), 0.9f);
            Material mIcePreview = GetTransparentMaterial("M_IceWall_Preview", new Color(0.5f, 0.9f, 1f, 0.35f));
            Material mIcePreviewInvalid = GetTransparentMaterial("M_IceWall_PreviewInvalid", new Color(1f, 0.25f, 0.2f, 0.35f));
            Material mTracer = GetUnlitMaterial("M_Tracer", new Color(1f, 0.85f, 0.35f));
            Material mFlash = GetUnlitMaterial("M_MuzzleFlash", new Color(1f, 0.75f, 0.2f));

            // ---------- Configs ----------
            MovementConfig movementConfig = LoadOrCreate<MovementConfig>($"{ConfigFolder}/MovementConfig.asset", out _);
            CameraConfig cameraConfig = LoadOrCreate<CameraConfig>($"{ConfigFolder}/CameraConfig.asset", out _);
            IceWallConfig iceWallConfig = LoadOrCreate<IceWallConfig>($"{ConfigFolder}/IceWallConfig.asset", out _);

            movementConfig.environmentMask = environmentMask;
            // Gelo e alvos (que representam jogadores) não podem ser escalados nem usados no wall run.
            movementConfig.nonClimbableMask = (1 << nonClimbableLayer) | (1 << targetLayer);
            UpgradeMovementDefaults(movementConfig);
            cameraConfig.collisionMask = environmentMask;

            iceWallConfig.wallMaterial = mIceWall;
            iceWallConfig.previewMaterial = mIcePreview;
            iceWallConfig.invalidPreviewMaterial = mIcePreviewInvalid;

            EnsureFolder(WeaponConfigFolder);
            WeaponConfig rifleConfig = GetWeaponConfig("Rifle", WeaponSlot.Primary, environmentMask);
            WeaponConfig pistolConfig = GetWeaponConfig("Pistola", WeaponSlot.Secondary, environmentMask);
            WeaponConfig knifeConfig = GetWeaponConfig("Karambit", WeaponSlot.Melee, environmentMask);
            WeaponConfig iceConfig = GetWeaponConfig("Gelo", WeaponSlot.Gadget, environmentMask);

            EditorUtility.SetDirty(movementConfig);
            EditorUtility.SetDirty(cameraConfig);
            EditorUtility.SetDirty(iceWallConfig);

            // ---------- Cena nova ----------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);

            BuildArena(groundLayer, climbableLayer, nonClimbableLayer, targetLayer,
                mGround, mWall, mBox, mIce, mTarget, mMarker);

            // ---------- Jogador (física) ----------
            var playerGo = new GameObject("Player");
            playerGo.layer = playerLayer;
            playerGo.transform.position = new Vector3(0f, 0.05f, -4f);

            var controller = playerGo.AddComponent<CharacterController>();
            controller.height = movementConfig.standHeight;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            controller.skinWidth = 0.04f;
            controller.minMoveDistance = 0f;

            var reader = playerGo.AddComponent<PlayerInputReader>();
            var motor = playerGo.AddComponent<PlayerMotor>();
            Assign(motor, "config", movementConfig);
            Assign(motor, "input", reader);

            // ---------- Jogador (visual interpolado) ----------
            var visualGo = new GameObject("PlayerVisual");
            visualGo.transform.position = playerGo.transform.position;

            var visual = visualGo.AddComponent<PlayerVisual>();
            Assign(visual, "motor", motor);
            Assign(visual, "input", reader);

            // Modelo 3D (manequim CC0). Se não existir, usa a cápsula provisória.
            Transform model = CreateCharacterModel(visualGo.transform, movementConfig.standHeight);
            if (model == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Corpo";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(visualGo.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
                body.GetComponent<MeshRenderer>().sharedMaterial = mPlayer;
                Assign(visual, "body", body.transform);
            }

            // ---------- Armas (modelos de blocos; o animator coloca na mão ou no coldre) ----------
            Transform weaponRoot = new GameObject("Armas").transform;
            weaponRoot.SetParent(visualGo.transform, false);

            Transform rifle = CreateRifle(weaponRoot, mGun, mDark, mFlash,
                out Transform rifleMuzzle, out GameObject rifleFlash, out Transform rifleSupport);
            Transform pistol = CreatePistol(weaponRoot, mGun, mDark, mFlash,
                out Transform pistolMuzzle, out GameObject pistolFlash, out Transform pistolSupport);
            Transform knife = CreateKarambit(weaponRoot, mSteel, mDark);
            Transform iceGadget = CreateIceGadget(weaponRoot, mGadget, mDark);

            // ---------- Câmera ----------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = playerGo.transform.position + new Vector3(0.65f, 1.6f, -3.2f);

            var rig = camGo.AddComponent<ThirdPersonCameraRig>();
            Assign(rig, "config", cameraConfig);
            Assign(rig, "input", reader);
            Assign(rig, "motor", motor);
            Assign(rig, "visual", visual);
            Assign(rig, "target", visualGo.transform);

            // ---------- Gelo (barreira) ----------
            var icePlacer = playerGo.AddComponent<IceWallPlacer>();
            Assign(icePlacer, "config", iceWallConfig);
            Assign(icePlacer, "motor", motor);
            Assign(icePlacer, "aimCamera", cam);

            // ---------- Armas (lógica) ----------
            var weapons = playerGo.AddComponent<PlayerWeapons>();
            Assign(weapons, "motor", motor);
            Assign(weapons, "aimCamera", cam);
            Assign(weapons, "tracerMaterial", mTracer);
            Assign(weapons, "iceWallPlacer", icePlacer);
            AssignWeapon(weapons, "primary", rifleConfig, rifle, rifleMuzzle, rifleFlash, rifleSupport);
            AssignWeapon(weapons, "secondary", pistolConfig, pistol, pistolMuzzle, pistolFlash, pistolSupport);
            AssignWeapon(weapons, "melee", knifeConfig, knife, null, null, null);
            AssignWeapon(weapons, "gadget", iceConfig, iceGadget, null, null, null);

            Assign(visual, "weapons", weapons);

            // ---------- Animação do modelo ----------
            var animator = visualGo.AddComponent<CharacterModelAnimator>();
            Assign(animator, "motor", motor);
            Assign(animator, "input", reader);
            Assign(animator, "weapons", weapons);
            Assign(animator, "visual", visual);
            Assign(animator, "model", model);
            if (model != null)
                Assign(animator, "animationComponent", model.GetComponentInChildren<Animation>());

            // ---------- HUD ----------
            var hud = new GameObject("DebugHUD").AddComponent<MovementDebugHUD>();
            Assign(hud, "motor", motor);
            Assign(hud, "weapons", weapons);

            // ---------- Salvar ----------
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Rogue] Cena Sandbox_Movement criada. Aperte Play e clique na tela do jogo.");
        }

        // ================= Arena =================

        private static void BuildArena(int groundLayer, int climbableLayer, int nonClimbableLayer, int targetLayer,
            Material mGround, Material mWall, Material mBox, Material mIce, Material mTarget, Material mMarker)
        {
            Transform env = new GameObject("Environment").transform;

            // Chão 80 x 80 m
            Block("Chao", env, new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), mGround, groundLayer);

            // Pista de bhop: marcas a cada 5 m (sem colisão)
            Transform lane = new GameObject("Pista_Bhop").transform;
            lane.SetParent(env, false);
            for (int i = 0; i <= 7; i++)
            {
                float z = i * 5f;
                Block($"Marca_{z}m_E", lane, new Vector3(-2f, 0.01f, z), new Vector3(0.6f, 0.02f, 0.15f), mMarker, groundLayer, collider: false);
                Block($"Marca_{z}m_D", lane, new Vector3(2f, 0.01f, z), new Vector3(0.6f, 0.02f, 0.15f), mMarker, groundLayer, collider: false);
            }

            // Corredor de wall-run (duas paredes altas e paralelas)
            Transform wallRun = new GameObject("WallRun").transform;
            wallRun.SetParent(env, false);
            Block("Parede_Esquerda", wallRun, new Vector3(-16f, 2.5f, 12f), new Vector3(0.5f, 5f, 24f), mWall, climbableLayer);
            Block("Parede_Direita", wallRun, new Vector3(-10f, 2.5f, 12f), new Vector3(0.5f, 5f, 24f), mWall, climbableLayer);
            Block("Parede_Solo", wallRun, new Vector3(12f, 2.5f, 15f), new Vector3(0.5f, 5f, 20f), mWall, climbableLayer);

            // Degraus de escalada
            // 0,5 a 1 m: sobe pulando · 1,5 m: sobe direto (mantle) · 2 a 3 m: fica pendurado · 3,5 m: não alcança
            Transform steps = new GameObject("Escalada").transform;
            steps.SetParent(env, false);
            float[] heights = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f };
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                Block($"Caixa_{h}m", steps, new Vector3(-9f + i * 3.5f, h * 0.5f, -12f), new Vector3(2f, h, 2f), mBox, climbableLayer);
            }

            // Túnel baixo (1,3 m de altura, 6 m de comprimento): só passa agachado ou deslizando
            Transform tunnel = new GameObject("Tunel_Agachar").transform;
            tunnel.SetParent(env, false);
            Block("Lateral_E", tunnel, new Vector3(5f, 0.65f, 15f), new Vector3(0.3f, 1.3f, 6f), mWall, climbableLayer);
            Block("Lateral_D", tunnel, new Vector3(7f, 0.65f, 15f), new Vector3(0.3f, 1.3f, 6f), mWall, climbableLayer);
            Block("Teto", tunnel, new Vector3(6f, 1.45f, 15f), new Vector3(2.3f, 0.3f, 6f), mWall, climbableLayer);

            // Rampa
            Block("Rampa", env, new Vector3(18f, 1.6f, -14f), new Vector3(4f, 0.3f, 10f), mBox, groundLayer, new Vector3(-20f, 0f, 0f));

            // Bloco de gelo de exemplo (NÃO escalável)
            Block("Gelo_Exemplo", env, new Vector3(-4f, 1f, -6f), new Vector3(3f, 2f, 0.6f), mIce, nonClimbableLayer);

            // Alvos de tiro (pistola: 3 tiros · rifle: 5 · faca: 2 golpes)
            Transform targets = new GameObject("Alvos").transform;
            targets.SetParent(env, false);
            Vector3[] targetPositions =
            {
                new Vector3(8f, 0.9f, 30f), new Vector3(-6f, 0.9f, 32f), new Vector3(0f, 0.9f, 37f),
                new Vector3(4f, 0.9f, 10f), new Vector3(-3f, 0.9f, 14f)
            };
            for (int i = 0; i < targetPositions.Length; i++)
            {
                GameObject target = Block($"Alvo_{i + 1}", targets, targetPositions[i], new Vector3(0.6f, 1.8f, 0.6f),
                    mTarget, targetLayer, isStatic: false);
                target.AddComponent<Damageable>();
            }
        }

        // ================= Configs =================

        /// <summary>Atualiza uma vez os números que mudaram de padrão em versões anteriores do protótipo.</summary>
        private static void UpgradeMovementDefaults(MovementConfig config)
        {
            if (config.defaultsVersion >= MovementDefaultsVersion) return;

            config.airStrafeAcceleration = 14f;
            config.slideDistance = 6f;
            config.wallRunDuration = 3f;
            config.wallRunSinkGravity = 0.5f;
            config.wallRunMaxSinkSpeed = 0.35f;
            config.hangHandHeight = 1.8f;
            config.defaultsVersion = MovementDefaultsVersion;
            Debug.Log("[Rogue] MovementConfig atualizado para os novos valores padrão.");
        }

        /// <summary>Carrega (ou cria com os valores iniciais) o arquivo de uma arma.</summary>
        private static WeaponConfig GetWeaponConfig(string name, WeaponSlot slot, int hitMask)
        {
            WeaponConfig config = LoadOrCreate<WeaponConfig>($"{WeaponConfigFolder}/{name}.asset", out bool created);
            config.hitMask = hitMask;

            if (created)
            {
                config.displayName = name;
                config.slot = slot;

                switch (slot)
                {
                    case WeaponSlot.Primary: // rifle automático
                        config.kind = WeaponKind.Hitscan;
                        config.moveSpeedMultiplier = 1f;
                        config.firingGroundSpeedPenalty = 0.2f;
                        config.equipTime = 0.4f;
                        config.automatic = true;
                        config.fireRate = 10f;
                        config.damage = 20f;
                        config.range = 150f;
                        config.spreadDegrees = 0.6f;
                        config.airSpreadDegrees = 1.5f;
                        config.aimHoldOffset = new Vector3(0.07f, -0.08f, 0.3f);
                        config.twoHanded = true;
                        config.recoilDistance = 0.06f;
                        config.tracerWidth = 0.03f;
                        break;

                    case WeaponSlot.Secondary: // pistola: um tiro por clique, +10% de velocidade
                        config.kind = WeaponKind.Hitscan;
                        config.moveSpeedMultiplier = 1.1f;
                        config.firingGroundSpeedPenalty = 0.2f;
                        config.equipTime = 0.25f;
                        config.automatic = false;
                        config.fireRate = 6.5f;
                        config.damage = 34f;
                        config.range = 120f;
                        config.spreadDegrees = 0.45f;
                        config.airSpreadDegrees = 1.2f;
                        config.aimHoldOffset = new Vector3(0.04f, -0.02f, 0.38f);
                        config.twoHanded = true;
                        config.recoilDistance = 0.04f;
                        config.tracerWidth = 0.022f;
                        break;

                    case WeaponSlot.Melee: // karambit: golpe por clique, +15% de velocidade
                        config.kind = WeaponKind.Melee;
                        config.moveSpeedMultiplier = 1.15f;
                        config.firingGroundSpeedPenalty = 0f;
                        config.equipTime = 0.15f;
                        config.automatic = false;
                        config.fireRate = 2.5f;
                        config.damage = 55f;
                        config.meleeRange = 1.9f;
                        config.meleeRadius = 0.45f;
                        config.meleeAttackDuration = 0.32f;
                        config.meleeHitTime = 0.1f;
                        config.aimHoldOffset = new Vector3(0.18f, -0.05f, 0.3f);
                        config.twoHanded = false;
                        break;

                    default: // gelo
                        config.kind = WeaponKind.Gadget;
                        config.moveSpeedMultiplier = 1f;
                        config.firingGroundSpeedPenalty = 0f;
                        config.equipTime = 0.2f;
                        config.automatic = false;
                        config.fireRate = 1.25f;
                        config.damage = 0f;
                        config.aimHoldOffset = new Vector3(0.16f, -0.06f, 0.36f);
                        config.twoHanded = false;
                        break;
                }
            }

            EditorUtility.SetDirty(config);
            return config;
        }

        // ================= Modelos =================

        /// <summary>Coloca o modelo 3D do personagem, com a altura certa e o pé no chão.</summary>
        private static Transform CreateCharacterModel(Transform parent, float targetHeight)
        {
            EnsureLegacyAnimationImport(CharacterModelPath);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Rogue] Modelo não encontrado em {CharacterModelPath}. Usando cápsula provisória.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Modelo";
            Transform model = instance.transform;
            model.SetParent(parent, false);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;

            // Mede a altura do modelo e escala para a altura do personagem.
            if (TryGetBounds(instance, out Bounds bounds) && bounds.size.y > 0.001f)
            {
                float scale = targetHeight / bounds.size.y;
                model.localScale = Vector3.one * scale;

                // Recalcula e encosta o pé no chão.
                TryGetBounds(instance, out bounds);
                model.position += Vector3.up * (parent.position.y - bounds.min.y);
            }

            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = ShadowCastingMode.On;

            return model;
        }

        /// <summary>
        /// O glTFast importa animações como Mecanim por padrão no Editor. Este projeto usa o sistema
        /// Legacy (componente Animation), então o builder troca a opção "Animation Method" para Legacy.
        /// </summary>
        private static void EnsureLegacyAnimationImport(string path)
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning($"[Rogue] Arquivo do modelo ainda não importado: {path}");
                return;
            }

            var serialized = new SerializedObject(importer);
            SerializedProperty method = serialized.FindProperty("importSettings.animationMethod");
            if (method == null || method.propertyType != SerializedPropertyType.Enum)
            {
                Debug.LogWarning("[Rogue] Não encontrei a opção 'Animation Method' do glTFast. " +
                                 "Selecione o arquivo do modelo e mude para Legacy no Inspector.");
                return;
            }

            int legacyIndex = System.Array.IndexOf(method.enumNames, "Legacy");
            if (legacyIndex < 0 || method.enumValueIndex == legacyIndex) return;

            method.enumValueIndex = legacyIndex;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            importer.SaveAndReimport();
            Debug.Log("[Rogue] Modelo reimportado com animações Legacy.");
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            bounds = default;
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        /// <summary>Rifle de blocos: corpo, cano, cabo, carregador, mira. O ponto de giro é o cabo (mão direita).</summary>
        private static Transform CreateRifle(Transform parent, Material body, Material dark, Material flash,
            out Transform muzzle, out GameObject muzzleFlash, out Transform supportGrip)
        {
            var gun = new GameObject("Rifle").transform;
            gun.SetParent(parent, false);

            Part("Corpo", gun, new Vector3(0f, 0.03f, 0.15f), new Vector3(0.07f, 0.1f, 0.38f), body);
            Part("Cano", gun, new Vector3(0f, 0.05f, 0.42f), new Vector3(0.035f, 0.035f, 0.2f), dark);
            Part("Cabo", gun, new Vector3(0f, -0.06f, 0.02f), new Vector3(0.05f, 0.12f, 0.06f), dark, new Vector3(-15f, 0f, 0f));
            Part("Carregador", gun, new Vector3(0f, -0.07f, 0.17f), new Vector3(0.045f, 0.14f, 0.07f), dark);
            Part("Mira", gun, new Vector3(0f, 0.1f, 0.12f), new Vector3(0.03f, 0.03f, 0.08f), dark);

            muzzle = CreateMuzzle(gun, new Vector3(0f, 0.05f, 0.53f), flash, new Vector3(0.12f, 0.12f, 0.2f), out muzzleFlash);

            supportGrip = new GameObject("Apoio_MaoEsquerda").transform;
            supportGrip.SetParent(gun, false);
            supportGrip.localPosition = new Vector3(0f, -0.02f, 0.16f);

            return gun;
        }

        /// <summary>Pistola de blocos: ferrolho, corpo e cabo. A mão esquerda apoia por baixo da direita.</summary>
        private static Transform CreatePistol(Transform parent, Material body, Material dark, Material flash,
            out Transform muzzle, out GameObject muzzleFlash, out Transform supportGrip)
        {
            var pistol = new GameObject("Pistola").transform;
            pistol.SetParent(parent, false);

            Part("Ferrolho", pistol, new Vector3(0f, 0.035f, 0.06f), new Vector3(0.032f, 0.035f, 0.17f), dark);
            Part("Corpo", pistol, new Vector3(0f, 0.01f, 0.05f), new Vector3(0.03f, 0.025f, 0.14f), body);
            Part("Cabo", pistol, new Vector3(0f, -0.045f, -0.005f), new Vector3(0.03f, 0.1f, 0.045f), dark, new Vector3(-12f, 0f, 0f));

            muzzle = CreateMuzzle(pistol, new Vector3(0f, 0.035f, 0.15f), flash, new Vector3(0.08f, 0.08f, 0.12f), out muzzleFlash);

            supportGrip = new GameObject("Apoio_MaoEsquerda").transform;
            supportGrip.SetParent(pistol, false);
            supportGrip.localPosition = new Vector3(-0.02f, -0.05f, 0f);

            return pistol;
        }

        /// <summary>Karambit: cabo com anel atrás e lâmina curva em gancho (3 pedaços inclinados).</summary>
        private static Transform CreateKarambit(Transform parent, Material steel, Material dark)
        {
            var knife = new GameObject("Karambit").transform;
            knife.SetParent(parent, false);

            Part("Cabo", knife, new Vector3(0f, 0f, 0f), new Vector3(0.025f, 0.03f, 0.1f), dark);
            Part("Anel", knife, new Vector3(0f, 0f, -0.07f), new Vector3(0.008f, 0.045f, 0.045f), steel);
            Part("Lamina_1", knife, new Vector3(0f, 0.005f, 0.085f), new Vector3(0.006f, 0.022f, 0.05f), steel);
            Part("Lamina_2", knife, new Vector3(0f, -0.008f, 0.123f), new Vector3(0.006f, 0.02f, 0.045f), steel, new Vector3(25f, 0f, 0f));
            Part("Lamina_3", knife, new Vector3(0f, -0.03f, 0.152f), new Vector3(0.006f, 0.016f, 0.04f), steel, new Vector3(55f, 0f, 0f));

            return knife;
        }

        /// <summary>Gelo: cilindro azul com tampa escura (cabe na mão).</summary>
        private static Transform CreateIceGadget(Transform parent, Material ice, Material dark)
        {
            var gadget = new GameObject("Gelo").transform;
            gadget.SetParent(parent, false);

            GameObject canister = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            canister.name = "Corpo";
            Object.DestroyImmediate(canister.GetComponent<Collider>());
            canister.transform.SetParent(gadget, false);
            canister.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // deitado ao longo da "frente"
            canister.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            canister.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);
            canister.GetComponent<MeshRenderer>().sharedMaterial = ice;

            Part("Tampa", gadget, new Vector3(0f, 0f, 0.115f), new Vector3(0.05f, 0.05f, 0.02f), dark);
            return gadget;
        }

        private static Transform CreateMuzzle(Transform gun, Vector3 localPosition, Material flash, Vector3 flashSize,
            out GameObject muzzleFlash)
        {
            Transform muzzle = new GameObject("Cano_Saida").transform;
            muzzle.SetParent(gun, false);
            muzzle.localPosition = localPosition;

            muzzleFlash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            muzzleFlash.name = "Clarao";
            Object.DestroyImmediate(muzzleFlash.GetComponent<Collider>());
            muzzleFlash.transform.SetParent(muzzle, false);
            muzzleFlash.transform.localScale = flashSize;
            muzzleFlash.GetComponent<MeshRenderer>().sharedMaterial = flash;
            muzzleFlash.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            muzzleFlash.SetActive(false);
            return muzzle;
        }

        private static void Part(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material,
            Vector3? euler = null)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size,
            Material material, int layer, Vector3? euler = null, bool collider = true, bool isStatic = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.layer = layer;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = isStatic; // objetos parados: a Unity otimiza o desenho deles
            return go;
        }

        // ================= Utilidades do editor =================

        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // Layers 0 a 5 são da Unity; a partir da 6 são livres.
            for (int i = 6; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }

            Debug.LogError($"[Rogue] Sem espaço para criar a layer '{layerName}'.");
            return 0;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static Material GetMaterial(string name, Color color, float smoothness = -1f)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // Funciona tanto no pipeline padrão (Built-in) quanto no URP.
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Standard");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (smoothness >= 0f)
            {
                material.SetFloat("_Glossiness", smoothness); // Standard (Built-in)
                material.SetFloat("_Smoothness", smoothness); // URP Lit
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Material transparente (prévia do gelo).</summary>
        private static Material GetTransparentMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetUnlitMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Unlit")
                    : Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Liga uma referência no Inspector por código (do jeito que a Unity salva).</summary>
        private static void Assign(Object component, string fieldName, Object value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[Rogue] Campo '{fieldName}' não encontrado em {component.GetType().Name}.");
                return;
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignWeapon(Object weapons, string slotField, WeaponConfig config, Transform model,
            Transform muzzle, GameObject muzzleFlash, Transform supportGrip)
        {
            Assign(weapons, $"{slotField}.config", config);
            Assign(weapons, $"{slotField}.model", model);
            Assign(weapons, $"{slotField}.muzzle", muzzle);
            Assign(weapons, $"{slotField}.muzzleFlash", muzzleFlash);
            Assign(weapons, $"{slotField}.supportGrip", supportGrip);
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path))
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
