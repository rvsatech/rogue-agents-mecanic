using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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
    /// o jogador (agente escolhido + armas + gelo + sons), a câmera e o HUD.
    /// "Rogue > Criar mapa Pátio Industrial (3v3)" monta o mesmo jogador no mapa novo (SandboxBuilder.Patio.cs).
    /// Também cria/atualiza os arquivos de configuração. Pode rodar de novo quando quiser.
    /// </summary>
    public static partial class SandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox_Movement.unity";
        private const string ConfigFolder = ConfigUpgrades.ConfigFolder;
        private const string WeaponConfigFolder = ConfigUpgrades.WeaponConfigFolder;
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string SoundFolder = "Assets/_Project/Audio/SFX";
        private const string CharacterModelPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/AnimationLibrary_Godot_Standard.gltf";
        /// <summary>Animações da UAL2 (slide, subida, arremesso) já com os ossos do manequim (só animação, sem malha).</summary>
        private const string Ual2ClipsPath = "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary2/UAL2_Standard_Selecao.glb";




        [MenuItem("Rogue/Criar cena Sandbox_Movement")]
        public static void Build() => BuildScene(false);

        /// <summary>Monta a cena: arena de teste (patio = false) ou o mapa Pátio Industrial (patio = true).</summary>
        private static void BuildScene(bool patio)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // ---------- Layers ----------
            int groundLayer = EnsureLayer("Ground");
            int climbableLayer = EnsureLayer("Climbable");
            int nonClimbableLayer = EnsureLayer("NonClimbable");
            int playerLayer = EnsureLayer("Player");
            int targetLayer = EnsureLayer("Target");
            int iceWallLayer = EnsureLayer("IceWall");
            EnsureLayer("Projectile");

            int environmentMask = ~(1 << playerLayer);

            // ---------- Materiais ----------
            Material mGround = GetMaterial("M_Ground", new Color(0.36f, 0.37f, 0.39f));
            Material mWall = GetMaterial("M_Wall", new Color(0.26f, 0.42f, 0.62f));
            Material mBox = GetMaterial("M_Box", new Color(0.85f, 0.55f, 0.2f));
            Material mNoClimb = GetMaterial("M_NaoEscalavel", new Color(0.55f, 0.3f, 0.65f));
            Material mBot = GetMaterial("M_Bot", new Color(0.85f, 0.3f, 0.25f));
            Material mMarker = GetMaterial("M_Marker", new Color(0.95f, 0.95f, 0.95f));
            Material mPlayer = GetMaterial("M_Player", new Color(0.2f, 0.75f, 0.45f));
            // Armas com brilho de metal (o reflexo da luz mostra as peças mexendo).
            Material mDark = GetMaterial("M_Dark", new Color(0.1f, 0.1f, 0.12f), 0.45f, 0.2f);
            Material mGun = GetMaterial("M_Gun", new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.35f);
            Material mSteel = GetMaterial("M_Steel", new Color(0.78f, 0.8f, 0.83f), 0.85f);
            Material mGadget = GetMaterial("M_Gadget", new Color(0.45f, 0.85f, 1f), 0.7f);
            Material mSniper = GetMaterial("M_Sniper", new Color(0.27f, 0.31f, 0.22f), 0.42f, 0.15f);
            Material mMedkit = GetMaterial("M_KitMedico", new Color(0.93f, 0.93f, 0.9f));
            Material mMedkitCross = GetMaterial("M_KitMedico_Cruz", new Color(0.85f, 0.1f, 0.1f));
            Material mShotgunWood = GetMaterial("M_Doze_Madeira", new Color(0.36f, 0.22f, 0.12f), 0.35f);
            Material mFrag = GetMaterial("M_Granada", new Color(0.28f, 0.33f, 0.2f));
            Material mFlashCan = GetMaterial("M_Flash", new Color(0.72f, 0.74f, 0.78f));
            Material mSmokeCan = GetMaterial("M_Smoke", new Color(0.86f, 0.86f, 0.84f));
            Material mBandBlue = GetMaterial("M_Faixa_Azul", new Color(0.2f, 0.45f, 0.95f));
            Material mBandGreen = GetMaterial("M_Faixa_Verde", new Color(0.25f, 0.75f, 0.35f));
            Texture2D particleTexture = GetParticleTexture();
            Material mSmokeParticle = GetParticleMaterial("M_Particula_Fumaca", particleTexture, false);
            Material mGlowParticle = GetParticleMaterial("M_Particula_Brilho", particleTexture, true);
            Material mThrowLine = GetLineMaterial("M_Linha_Arremesso");
            Material mMuzzleFlash = GetParticleMaterial("M_Particula_Clarao", GetFlashTexture(), true);
            Material mShell = GetShellMaterial("M_Capsula");
            Material mRound = GetMaterial("M_Cartucho", new Color(0.75f, 0.12f, 0.1f), 0.5f);
            Material mIceWall = GetMaterial("M_IceWall", new Color(0.62f, 0.9f, 1f), 0.9f);
            Material mIcePreview = GetTransparentMaterial("M_IceWall_Preview", new Color(0.5f, 0.9f, 1f, 0.35f));
            Material mIcePreviewInvalid = GetTransparentMaterial("M_IceWall_PreviewInvalid", new Color(1f, 0.25f, 0.2f, 0.35f));
            Material mTracer = GetUnlitMaterial("M_Tracer", new Color(1f, 0.85f, 0.35f));

            // ---------- Configs ----------
            MovementConfig movementConfig = LoadOrCreate<MovementConfig>($"{ConfigFolder}/MovementConfig.asset", out _);
            CameraConfig cameraConfig = LoadOrCreate<CameraConfig>($"{ConfigFolder}/CameraConfig.asset", out _);
            IceWallConfig iceWallConfig = LoadOrCreate<IceWallConfig>($"{ConfigFolder}/IceWallConfig.asset", out _);

            movementConfig.environmentMask = environmentMask;
            // Não dá para agarrar, escalar, pular por cima nem correr na parede: bloco roxo, alvos/bots e o gelo.
            movementConfig.nonClimbableMask = (1 << nonClimbableLayer) | (1 << targetLayer) | (1 << iceWallLayer);
            movementConfig.noWallRunMask = 1 << iceWallLayer;
            ConfigUpgrades.UpgradeMovement(movementConfig);
            ConfigUpgrades.UpgradeCamera(cameraConfig);
            cameraConfig.collisionMask = environmentMask;

            iceWallConfig.wallMaterial = mIceWall;
            iceWallConfig.previewMaterial = mIcePreview;
            iceWallConfig.invalidPreviewMaterial = mIcePreviewInvalid;

            EnsureFolder(WeaponConfigFolder);
            // A arma principal virou a UMP: renomeia o arquivo antigo (as ligações continuam valendo).
            RenameAssetIfExists($"{WeaponConfigFolder}/Rifle.asset", "UMP");
            WeaponConfig rifleConfig = GetWeaponConfig("UMP", WeaponSlot.Primary, environmentMask);
            WeaponConfig pistolConfig = GetWeaponConfig("Pistola", WeaponSlot.Secondary, environmentMask);
            WeaponConfig knifeConfig = GetWeaponConfig("Karambit", WeaponSlot.Melee, environmentMask);
            WeaponConfig iceConfig = GetWeaponConfig("Gelo", WeaponSlot.Gadget, environmentMask);
            WeaponConfig awpConfig = GetWeaponConfig("AWP", WeaponSlot.Sniper, environmentMask);
            WeaponConfig medkitConfig = GetWeaponConfig("KitMedico", WeaponSlot.Medkit, environmentMask);
            WeaponConfig shotgunConfig = GetWeaponConfig("Doze", WeaponSlot.Shotgun, environmentMask);
            WeaponConfig fragConfig = GetWeaponConfig("Granada", WeaponSlot.Frag, environmentMask);
            WeaponConfig flashConfig = GetWeaponConfig("Flash", WeaponSlot.Flashbang, environmentMask);
            WeaponConfig smokeConfig = GetWeaponConfig("Smoke", WeaponSlot.Smoke, environmentMask);
            foreach (WeaponConfig throwable in new[] { fragConfig, flashConfig, smokeConfig })
            {
                throwable.particleMaterial = mSmokeParticle;
                throwable.glowMaterial = mGlowParticle;
                EditorUtility.SetDirty(throwable);
            }
            ConfigUpgrades.UpgradeIceWall(iceWallConfig, rifleConfig);

            SoundBank soundBank = LoadOrCreate<SoundBank>($"{ConfigFolder}/SoundBank.asset", out _);
            SetupSounds(soundBank, rifleConfig, pistolConfig, knifeConfig, iceConfig, awpConfig, medkitConfig);
            SetupSoundsV2(shotgunConfig, fragConfig, flashConfig, smokeConfig);

            EditorUtility.SetDirty(movementConfig);
            EditorUtility.SetDirty(cameraConfig);
            EditorUtility.SetDirty(iceWallConfig);

            // ---------- Cena nova ----------
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- Personagens (agentes e skins) ----------
            // Só DEPOIS da cena nova e sem salvar no meio: trocar de cena tira da memória os arquivos já salvos
            // que nada usa, e um ScriptableObject (config, lista de agentes) tirado da memória vira "null" na
            // variável para sempre — a cena saía com todas as configs vazias.
            CharacterRoster roster = BuildCharacterLibrary(false);
            CharacterDefinition firstAgent = roster != null ? roster.Get(0) : null;
            if (movementConfig == null || cameraConfig == null || soundBank == null || rifleConfig == null)
                Debug.LogError("[Rogue] As configs sumiram da memória ao criar a cena. Rode o menu Rogue de novo.");

            Vector3 spawn = new Vector3(0f, 0.05f, -4f);
            if (patio)
            {
                spawn = BuildPatioMap(groundLayer, climbableLayer, nonClimbableLayer, targetLayer, mBot,
                    movementConfig.standHeight);
            }
            else
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);
                RenderSettings.fog = false;

                BuildArena(groundLayer, climbableLayer, nonClimbableLayer, targetLayer,
                    mGround, mWall, mBox, mNoClimb, mBot, mMarker, movementConfig.standHeight);
            }

            // ---------- Jogador (física) ----------
            var playerGo = new GameObject("Player");
            playerGo.layer = playerLayer;
            playerGo.transform.position = spawn;

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

            // Modelo 3D: o primeiro agente (troca no menu B). Sem agentes, o manequim CC0; sem ele, a cápsula.
            Transform model = firstAgent != null
                ? CreateAgentModel(visualGo.transform, firstAgent, 0, movementConfig.standHeight)
                : CreateCharacterModel(visualGo.transform, movementConfig.standHeight);
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

            Transform rifle = CreateUmp(weaponRoot, mGun, mDark, out Transform rifleMuzzle, out Transform rifleSupport,
                out Transform rifleMagazine, out Transform rifleAction, out Transform rifleEject);
            Transform pistol = CreatePistol(weaponRoot, mGun, mDark, out Transform pistolMuzzle, out Transform pistolSupport,
                out Transform pistolMagazine, out Transform pistolAction, out Transform pistolEject);
            Transform knife = CreateKarambit(weaponRoot, mSteel, mDark);
            Transform iceGadget = CreateIceGadget(weaponRoot, mGadget, mDark);
            Transform awp = CreateSniper(weaponRoot, mSniper, mDark, out Transform awpMuzzle, out Transform awpSupport,
                out Transform awpMagazine, out Transform awpAction, out Transform awpEject);
            Transform medkit = CreateMedkit(weaponRoot, mMedkit, mMedkitCross, mDark, out Transform medkitSupport);
            Transform shotgun = CreateShotgun(weaponRoot, mShotgunWood, mDark, out Transform shotgunMuzzle,
                out Transform shotgunSupport, out Transform shotgunAction, out Transform shotgunEject);
            Transform fragModel = CreateFragGrenade(weaponRoot, mFrag, mDark);
            Transform flashModel = CreateCanGrenade("Flash", weaponRoot, mFlashCan, mBandBlue, mDark);
            Transform smokeModel = CreateCanGrenade("Smoke", weaponRoot, mSmokeCan, mBandGreen, mDark);

            // ---------- Câmera ----------
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = playerGo.transform.position + new Vector3(0.65f, 1.6f, -3.2f);

            var rig = camGo.AddComponent<ThirdPersonCameraRig>();
            var scope = camGo.AddComponent<ScopeOverlay>();
            camGo.AddComponent<ScreenEffectsOverlay>(); // tela branca da flash e cinza dentro da smoke
            Assign(scope, "rig", rig);
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

            // ---------- Vida (150; o kit médico cura) ----------
            var health = playerGo.AddComponent<PlayerHealth>();

            // ---------- Armas (lógica) ----------
            var weapons = playerGo.AddComponent<PlayerWeapons>();
            var recoil = playerGo.AddComponent<WeaponRecoil>();
            Assign(recoil, "weapons", weapons);
            Assign(recoil, "input", reader);
            Assign(recoil, "motor", motor);
            Assign(weapons, "motor", motor);
            Assign(weapons, "aimCamera", cam);
            Assign(weapons, "tracerMaterial", mTracer);
            Assign(weapons, "iceWallPlacer", icePlacer);
            Assign(weapons, "health", health);
            AssignWeapon(weapons, "primary", rifleConfig, rifle, rifleMuzzle, rifleSupport, rifleMagazine, rifleAction, rifleEject);
            AssignWeapon(weapons, "secondary", pistolConfig, pistol, pistolMuzzle, pistolSupport, pistolMagazine, pistolAction, pistolEject);
            AssignWeapon(weapons, "melee", knifeConfig, knife, null, null);
            AssignWeapon(weapons, "gadget", iceConfig, iceGadget, null, null);
            AssignWeapon(weapons, "sniper", awpConfig, awp, awpMuzzle, awpSupport, awpMagazine, awpAction, awpEject);
            AssignWeapon(weapons, "medkit", medkitConfig, medkit, null, medkitSupport);
            AssignWeapon(weapons, "shotgun", shotgunConfig, shotgun, shotgunMuzzle, shotgunSupport, null, shotgunAction, shotgunEject);
            AssignWeapon(weapons, "frag", fragConfig, fragModel, null, null);
            AssignWeapon(weapons, "flashbang", flashConfig, flashModel, null, null);
            AssignWeapon(weapons, "smoke", smokeConfig, smokeModel, null, null);

            // Efeitos do tiro: clarão, luz, fumacinha, faíscas, cápsulas e pente vazio caindo.
            var weaponFx = playerGo.AddComponent<WeaponFx>();
            Assign(weaponFx, "weapons", weapons);
            Assign(weaponFx, "motor", motor);
            Assign(weaponFx, "flashMaterial", mMuzzleFlash);
            Assign(weaponFx, "smokeMaterial", mSmokeParticle);
            Assign(weaponFx, "sparkMaterial", mGlowParticle);
            Assign(weaponFx, "shellMaterial", mShell);
            Assign(weaponFx, "shellMesh", GetPrimitiveMesh(PrimitiveType.Cylinder));
            AssignInt(weaponFx, "shellCollisionMask", environmentMask);

            // Caminho do arremesso na tela (com granada na mão).
            var throwPreview = playerGo.AddComponent<ThrowPreview>();
            Assign(throwPreview, "weapons", weapons);
            Assign(throwPreview, "lineMaterial", mThrowLine);

            Assign(visual, "weapons", weapons);
            Assign(rig, "weapons", weapons); // zoom da mira só com arma de fogo

            // ---------- Animação do modelo ----------
            var animator = visualGo.AddComponent<CharacterModelAnimator>();
            Assign(animator, "motor", motor);
            Assign(animator, "input", reader);
            Assign(animator, "weapons", weapons);
            Assign(animator, "visual", visual);
            Assign(animator, "model", model);
            if (model != null)
                Assign(animator, "animationComponent", model.GetComponentInChildren<Animation>());
            AssignArray(animator, "extraClips", LoadExtraClips());
            AssignArray(animator, "baseClips", LoadBaseClips());
            AssignFloat(animator, "hipsScale", firstAgent != null ? firstAgent.hipsScale : 1f);
            AssignString(animator, "animationHostPath", firstAgent != null ? firstAgent.animationHostPath : "");
            Assign(animator, "fx", weaponFx);
            Assign(animator, "roundMaterial", mRound);

            // Agente e skin (menu B troca; a escolha fica salva).
            var playerCharacter = visualGo.AddComponent<PlayerCharacter>();
            Assign(playerCharacter, "roster", roster);
            Assign(playerCharacter, "modelAnimator", animator);
            Assign(playerCharacter, "visual", visual);
            AssignFloat(playerCharacter, "standHeight", movementConfig.standHeight);
            Assign(playerCharacter, "currentModel", firstAgent != null ? model : null);
            AssignInt(playerCharacter, "builtCharacter", firstAgent != null ? 0 : -1);
            AssignInt(playerCharacter, "builtSkin", 0);

            // Poeira dos movimentos (pouso, slide, cambalhota, parede, beirada, passos rápidos).
            var movementFx = playerGo.AddComponent<MovementFx>();
            Assign(movementFx, "motor", motor);
            Assign(movementFx, "modelAnimator", animator);
            Assign(movementFx, "dustMaterial", mSmokeParticle);

            // ---------- HUD ----------
            // ---------- Som ----------
            new GameObject("AudioService").AddComponent<AudioService>();
            var playerAudio = playerGo.AddComponent<PlayerAudio>();
            Assign(playerAudio, "bank", soundBank);
            Assign(playerAudio, "motor", motor);
            Assign(playerAudio, "weapons", weapons);
            Assign(playerAudio, "iceWallPlacer", icePlacer);
            Assign(playerAudio, "modelAnimator", animator);

            var hud = new GameObject("DebugHUD").AddComponent<MovementDebugHUD>();
            Assign(hud, "motor", motor);
            Assign(hud, "weapons", weapons);
            Assign(hud, "health", health);
            AssignInt(hud, "builtSceneVersion", MovementDebugHUD.CurrentSceneVersion);

            var inventoryMenu = hud.gameObject.AddComponent<InventoryMenu>(); // B: armas, agente e skin
            Assign(inventoryMenu, "weapons", weapons);
            Assign(inventoryMenu, "character", playerCharacter);

            var training = hud.gameObject.AddComponent<TrainingHUD>();
            Assign(training, "weapons", weapons);
            Assign(training, "recoil", recoil);
            Assign(training, "viewCamera", cam);

            // ---------- Salvar ----------
            string scenePath = patio ? PatioScenePath : ScenePath;
            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Rogue] Cena {Path.GetFileNameWithoutExtension(scenePath)} criada. Aperte Play e clique na tela do jogo.");
        }

        // ================= Arena =================

        private static void BuildArena(int groundLayer, int climbableLayer, int nonClimbableLayer, int targetLayer,
            Material mGround, Material mWall, Material mBox, Material mNoClimb, Material mBot, Material mMarker,
            float standHeight)
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

            // Bloco de exemplo NÃO escalável (roxo): não dá para agarrar nem correr na parede
            Block("Bloco_NaoEscalavel", env, new Vector3(-4f, 1f, -6f), new Vector3(3f, 2f, 0.6f), mNoClimb, nonClimbableLayer);

            // Campo de tiro com bots parados (mira, dano, recoil)
            BuildTrainingRange(env, targetLayer, climbableLayer, groundLayer, mBox, mBot, mMarker, standHeight);
        }

        // ================= Configs =================

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
                    case WeaponSlot.Primary: // UMP: submetralhadora automática, cadência média
                        config.kind = WeaponKind.Hitscan;
                        config.moveSpeedMultiplier = 1f;
                        config.firingGroundSpeedPenalty = 0.2f;
                        config.equipTime = 0.5f;
                        config.automatic = true;
                        config.fireRate = 7f;
                        config.damage = 14f;
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
                        config.equipTime = 0.5f;
                        config.automatic = false;
                        config.fireRate = 6.5f;
                        config.damage = 20f;
                        config.range = 120f;
                        config.spreadDegrees = 0.45f;
                        config.airSpreadDegrees = 1.2f;
                        config.aimHoldOffset = new Vector3(0.04f, -0.02f, 0.38f);
                        config.twoHanded = true;
                        config.recoilDistance = 0.04f;
                        config.tracerWidth = 0.022f;
                        break;

                    case WeaponSlot.Sniper: // AWP: ferrolho, luneta no botão direito, pesada
                        config.kind = WeaponKind.Hitscan;
                        config.moveSpeedMultiplier = 0.85f;
                        config.firingGroundSpeedPenalty = 0.2f;
                        config.equipTime = 0.5f;
                        config.automatic = false;
                        config.fireRate = 0.7f;          // 1 tiro a cada ~1,4 s
                        config.damage = 100f;
                        config.range = 300f;
                        config.spreadDegrees = 6f;       // sem luneta: bem imprecisa
                        config.airSpreadDegrees = 12f;
                        config.scopeFieldOfView = 16f;
                        config.scopedSpreadDegrees = 0f; // na luneta, parado: exata
                        config.movingSpreadDegrees = 5f; // andando/correndo: erra
                        config.scopedMoveSpeedMultiplier = 0.55f;
                        config.unscopeAfterShotTime = 0.95f;
                        config.aimHoldOffset = new Vector3(0.07f, -0.07f, 0.25f);
                        config.twoHanded = true;
                        config.recoilDistance = 0.12f;
                        config.tracerWidth = 0.05f;
                        break;

                    case WeaponSlot.Shotgun: // a 12: escopeta de bomba, 8 chumbos, forte de perto e fraca longe
                        config.displayName = "A 12";
                        config.kind = WeaponKind.Hitscan;
                        config.moveSpeedMultiplier = 0.95f;
                        config.firingGroundSpeedPenalty = 0.2f;
                        config.equipTime = 0.5f;
                        config.automatic = false;
                        config.fireRate = 1.1f;          // bombeia entre os tiros (~0,9 s)
                        config.damage = 14f;             // por chumbo
                        config.pellets = 8;
                        config.range = 40f;
                        config.spreadDegrees = 5f;
                        config.airSpreadDegrees = 7f;
                        config.falloffStart = 6f;        // até 6 m dano cheio
                        config.falloffEnd = 22f;         // em 22 m sobra 25%
                        config.falloffMinFactor = 0.25f;
                        config.aimHoldOffset = new Vector3(0.07f, -0.08f, 0.28f);
                        config.twoHanded = true;
                        config.recoilDistance = 0.1f;
                        config.tracerWidth = 0.015f;
                        break;

                    case WeaponSlot.Frag:      // granada: 2,5 s, dano em círculo de 8,8 m
                    case WeaponSlot.Flashbang: // flash: 1,6 s, tela branca e surdo (~4 s)
                    case WeaponSlot.Smoke:     // smoke: 1,8 s, nuvem de 17,6 m x 5,2 m por 15 s
                        config.displayName = slot == WeaponSlot.Frag ? "Granada" : slot == WeaponSlot.Flashbang ? "Flash" : "Smoke";
                        config.kind = WeaponKind.Throwable;
                        config.throwableType = slot == WeaponSlot.Frag ? ThrowableType.Frag
                            : slot == WeaponSlot.Flashbang ? ThrowableType.Flash : ThrowableType.Smoke;
                        config.moveSpeedMultiplier = 1f;
                        config.firingGroundSpeedPenalty = 0f;
                        config.equipTime = 0.35f;
                        config.automatic = false;
                        config.damage = 0f;
                        config.throwSpeed = 16f;
                        config.shortThrowSpeed = 7f;
                        config.throwUpAngle = 8f;
                        config.fuseTime = slot == WeaponSlot.Frag ? 2.5f : slot == WeaponSlot.Flashbang ? 1.6f : 1.8f;
                        config.throwRecoveryTime = 0.3f;
                        config.effectRadius = slot == WeaponSlot.Smoke ? 8.8f : 4.4f;
                        config.maxDamage = 100f;
                        config.minDamage = 30f;
                        config.flashMaxDistance = 40f;
                        config.flashFullBlindDistance = 12f;
                        config.flashMaxBlindTime = 3f;
                        config.flashFadeTime = 1f;
                        config.smokeHeight = 5.2f;
                        config.smokeDuration = 15f;
                        config.itemCount = slot == WeaponSlot.Flashbang ? 2 : 1;
                        config.infiniteItems = true;
                        config.aimHoldOffset = new Vector3(0.18f, 0.02f, 0.24f);
                        config.twoHanded = false;
                        break;

                    case WeaponSlot.Medkit: // kit médico (E): 2,5 s andando devagar, cura 40% da vida
                        config.displayName = "Kit médico";
                        config.kind = WeaponKind.Consumable;
                        config.moveSpeedMultiplier = 0.5f;
                        config.firingGroundSpeedPenalty = 0f;
                        config.equipTime = 0.3f;
                        config.automatic = false;
                        config.damage = 0f;
                        config.useTime = 2.5f;
                        config.healPercent = 0.4f;
                        config.itemCount = 3;
                        config.infiniteItems = true;
                        config.aimHoldOffset = new Vector3(0.1f, -0.2f, 0.3f);
                        config.twoHanded = true;
                        break;

                    case WeaponSlot.Melee: // karambit: golpe por clique, +15% de velocidade
                        config.kind = WeaponKind.Melee;
                        config.moveSpeedMultiplier = 1.15f;
                        config.firingGroundSpeedPenalty = 0f;
                        config.equipTime = 0.15f;
                        config.automatic = false;
                        config.fireRate = 2.5f;
                        config.damage = 40f;
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
                        config.equipTime = 0f;
                        config.automatic = false;
                        config.fireRate = 1.25f;
                        config.damage = 0f;
                        config.aimHoldOffset = new Vector3(0.16f, -0.06f, 0.36f);
                        config.twoHanded = false;
                        break;
                }
            }

            ConfigUpgrades.UpgradeWeapon(config, slot);
            EditorUtility.SetDirty(config);
            return config;
        }

        // ================= Campo de treino =================

        /// <summary>
        /// Campo de tiro à direita (leste) do início: bots parados a 8, 16, 24 e 32 m da linha de tiro —
        /// em pé, agachados, atrás de uma cobertura baixa (só cabeça e ombros aparecem) e em cima de uma
        /// plataforma — e um bot perto do início para testar a faca.
        /// </summary>
        private static void BuildTrainingRange(Transform env, int targetLayer, int climbableLayer, int groundLayer,
            Material mBox, Material mBot, Material mMarker, float standHeight)
        {
            Transform range = new GameObject("Campo_de_Tiro").transform;
            range.SetParent(env, false);

            Vector3 spawn = new Vector3(0f, 0f, -4f);

            Block("Linha_de_Tiro", range, new Vector3(6f, 0.01f, -4f), new Vector3(0.25f, 0.02f, 18f), mMarker,
                groundLayer, collider: false);
            Block("Cobertura_Baixa", range, new Vector3(20.9f, 0.6f, -4f), new Vector3(0.4f, 1.2f, 1.6f), mBox, climbableLayer);
            Block("Plataforma", range, new Vector3(30f, 0.75f, 2f), new Vector3(2.4f, 1.5f, 2.4f), mBox, climbableLayer);

            CreateTrainingBot(range, "Bot_08m", new Vector3(14f, 0f, -9f), false, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_08m_Agachado", new Vector3(14f, 0f, 0f), true, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_16m", new Vector3(22f, 0f, -10f), false, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_16m_Cobertura", new Vector3(22f, 0f, -4f), false, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_16m_Agachado", new Vector3(22f, 0f, 2f), true, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_24m", new Vector3(30f, 0f, -8f), false, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_24m_Alto", new Vector3(30f, 1.5f, 2f), false, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_32m", new Vector3(38f, 0f, -4f), false, spawn, mBot, targetLayer, standHeight);
            CreateTrainingBot(range, "Bot_Faca", new Vector3(-6f, 0f, 1f), false, spawn, mBot, targetLayer, standHeight);
        }

        /// <summary>
        /// Um bot parado virado para o início. As zonas (cabeça, peito, barriga, braços, pernas) são medidas no modelo,
        /// na pose da animação (em pé ou agachado), e ficam fixas — o bot não sai do lugar.
        /// </summary>
        private static void CreateTrainingBot(Transform parent, string name, Vector3 position, bool crouch,
            Vector3 lookAt, Material botMaterial, int layer, float standHeight)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            Vector3 toward = lookAt - position;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.01f) root.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up);

            string poseClip = crouch ? "Crouch_Idle_Loop" : "Idle_Loop";
            float height = crouch ? standHeight * 0.68f : standHeight;
            float s = standHeight / 1.8f;
            var colliders = new List<Collider>();
            Animation animation = null;

            Transform model = CreateCharacterModel(root, standHeight);
            if (model != null)
            {
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
                {
                    var materials = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++) materials[i] = botMaterial;
                    r.sharedMaterials = materials;
                }

                animation = model.GetComponentInChildren<Animation>();
                AnimationClip pose = animation != null ? animation.GetClip(poseClip) : null;
                if (pose != null) pose.SampleAnimation(animation.gameObject, 0f); // pose de referência para medir

                Transform head = FindDeep(model, "DEF-head");
                Transform neck = FindDeep(model, "DEF-neck");
                Transform midSpine = FindDeep(model, "DEF-spine.002");
                Transform hips = FindDeep(model, "DEF-hips");
                Transform footL = FindDeep(model, "DEF-foot.L");
                Transform footR = FindDeep(model, "DEF-foot.R");
                if (head != null && neck != null && hips != null && footL != null && footR != null)
                {
                    Vector3 headUp = head.position - neck.position;
                    headUp = headUp.sqrMagnitude > 0.0001f ? headUp.normalized : Vector3.up;
                    Vector3 headCenter = head.position + headUp * (0.1f * s);
                    Vector3 feet = (footL.position + footR.position) * 0.5f;
                    // Divisa peito/barriga: no meio da coluna (fim das costelas).
                    Vector3 waist = midSpine != null ? midSpine.position : Vector3.Lerp(hips.position, neck.position, 0.45f);

                    colliders.Add(SphereZone(root, "Zona_Cabeca", headCenter, 0.13f * s, HitZoneKind.Head, layer));
                    colliders.Add(CapsuleZone(root, "Zona_Peito", waist, neck.position, 0.2f * s, HitZoneKind.Chest, layer));
                    colliders.Add(CapsuleZone(root, "Zona_Barriga", hips.position, waist, 0.19f * s, HitZoneKind.Stomach, layer));
                    colliders.Add(CapsuleZone(root, "Zona_Pernas", feet, hips.position - Vector3.up * (0.08f * s), 0.17f * s,
                        HitZoneKind.Legs, layer));
                    AddArmZones(root, model, "L", 0.065f * s, layer, colliders);
                    AddArmZones(root, model, "R", 0.065f * s, layer, colliders);
                    height = headCenter.y + 0.13f * s - position.y;
                }
            }

            if (colliders.Count == 0)
            {
                // Sem modelo (ou ossos com outro nome): zonas nas alturas padrão.
                float headY = crouch ? 1.15f : 1.66f;
                colliders.Add(SphereZone(root, "Zona_Cabeca", position + Vector3.up * headY * s, 0.13f * s, HitZoneKind.Head, layer));
                colliders.Add(CapsuleZone(root, "Zona_Peito", position + Vector3.up * (crouch ? 0.8f : 1.22f) * s,
                    position + Vector3.up * (crouch ? 1.0f : 1.5f) * s, 0.2f * s, HitZoneKind.Chest, layer));
                colliders.Add(CapsuleZone(root, "Zona_Barriga", position + Vector3.up * (crouch ? 0.6f : 0.95f) * s,
                    position + Vector3.up * (crouch ? 0.8f : 1.22f) * s, 0.19f * s, HitZoneKind.Stomach, layer));
                colliders.Add(CapsuleZone(root, "Zona_Pernas", position, position + Vector3.up * (crouch ? 0.55f : 0.88f) * s,
                    0.17f * s, HitZoneKind.Legs, layer));
            }

            var bot = root.gameObject.AddComponent<TrainingBot>();
            AssignFloat(bot, "height", height);
            AssignBool(bot, "crouched", crouch);
            AssignString(bot, "poseClip", poseClip);
            if (animation != null) Assign(bot, "animationComponent", animation);
            AssignArray(bot, "hitColliders", colliders.ToArray());
        }

        /// <summary>Braço (ombro → cotovelo → mão) em duas cápsulas, seguindo a pose medida.</summary>
        private static void AddArmZones(Transform root, Transform model, string side, float radius, int layer,
            List<Collider> colliders)
        {
            Transform upper = FindDeep(model, $"DEF-upper_arm.{side}");
            Transform fore = FindDeep(model, $"DEF-forearm.{side}");
            Transform hand = FindDeep(model, $"DEF-hand.{side}");
            if (upper == null || fore == null || hand == null) return;

            string name = side == "L" ? "Esquerdo" : "Direito";
            colliders.Add(CapsuleZone(root, $"Zona_Braco_{name}", upper.position, fore.position, radius, HitZoneKind.Arms, layer));
            colliders.Add(CapsuleZone(root, $"Zona_Antebraco_{name}", fore.position, hand.position, radius * 0.9f,
                HitZoneKind.Arms, layer));
        }

        private static Collider SphereZone(Transform root, string name, Vector3 center, float radius, HitZoneKind kind, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(root, false);
            go.transform.position = center;
            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = radius;
            go.AddComponent<HitZone>().kind = kind;
            return sphere;
        }

        private static Collider CapsuleZone(Transform root, string name, Vector3 a, Vector3 b, float radius,
            HitZoneKind kind, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(root, false);
            go.transform.position = (a + b) * 0.5f;
            Vector3 axis = b - a;
            if (axis.sqrMagnitude > 0.000001f)
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1; // eixo Y
            capsule.radius = radius;
            capsule.height = axis.magnitude + radius * 2f;
            go.AddComponent<HitZone>().kind = kind;
            return capsule;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ================= Sons =================

        /// <summary>
        /// Liga os arquivos de som (pasta Audio/SFX) aos sons do jogo pelo PREFIXO do nome:
        /// passo_01.wav, passo_02.wav... viram as variações do som "passo".
        /// - Para trocar/adicionar: ponha arquivos com o mesmo prefixo na pasta e rode o menu de novo.
        /// - Volume/tom/3D só são aplicados quando o som ainda está vazio (depois vale o que você ajustar).
        /// </summary>
        /// <summary>Sons da 12 e das granadas.</summary>
        private static void SetupSoundsV2(WeaponConfig shotgun, WeaponConfig frag, WeaponConfig flash, WeaponConfig smoke)
        {
            Dictionary<string, List<AudioClip>> clips = LoadSoundClips();

            Cue(shotgun.fireSound, clips, "doze_tiro",               0.95f, 0.96f, 1.04f, 1f, 150f);
            Cue(shotgun.equipSound, clips, "sacar_arma",             0.50f, 0.85f, 0.92f, 1f, 20f);
            Cue(shotgun.dryFireSound, clips, "sem_bala",             0.50f, 0.85f, 0.95f, 1f, 15f);
            Cue(shotgun.reloadOutSound, clips, "recarga_tira",       0.55f, 1.05f, 1.15f, 1f, 20f);
            Cue(shotgun.reloadInSound, clips, "recarga_poe",         0.60f, 1.10f, 1.20f, 1f, 20f);
            Cue(shotgun.reloadRackSound, clips, "recarga_engatilha", 0.70f, 0.80f, 0.88f, 1f, 20f);
            EditorUtility.SetDirty(shotgun);

            foreach (WeaponConfig grenade in new[] { frag, flash, smoke })
            {
                Cue(grenade.equipSound, clips, "granada_pino",       0.50f, 0.95f, 1.05f, 1f, 15f);
                Cue(grenade.swingSound, clips, "faca_golpe",         0.45f, 0.80f, 0.90f, 1f, 15f); // arremesso
                Cue(grenade.bounceSound, clips, "granada_quica",     0.55f, 0.90f, 1.10f, 1f, 25f);
            }
            Cue(frag.detonateSound, clips, "granada_explosao",       1.00f, 0.95f, 1.05f, 1f, 120f);
            Cue(flash.detonateSound, clips, "flash_estouro",         0.95f, 0.97f, 1.03f, 1f, 100f);
            Cue(flash.ringSound, clips, "flash_zumbido",             0.60f, 1.00f, 1.00f, 0f, 10f);
            Cue(smoke.detonateSound, clips, "smoke_estouro",         0.70f, 0.95f, 1.05f, 1f, 40f);
            EditorUtility.SetDirty(frag);
            EditorUtility.SetDirty(flash);
            EditorUtility.SetDirty(smoke);
        }

        private static void SetupSounds(SoundBank bank, WeaponConfig rifle, WeaponConfig pistol,
            WeaponConfig knife, WeaponConfig gadget, WeaponConfig sniper, WeaponConfig medkit)
        {
            Dictionary<string, List<AudioClip>> clips = LoadSoundClips();

            // Movimento                                    volume  tom min/máx   3D   alcance
            Cue(bank.footstep, clips, "passo",               0.32f, 0.90f, 1.10f, 1f, 25f);
            Cue(bank.jump, clips, "pulo",                    0.40f, 0.94f, 1.06f, 1f, 25f);
            Cue(bank.land, clips, "aterrissagem",            0.55f, 0.94f, 1.06f, 1f, 30f);
            Cue(bank.slideStart, clips, "slide_inicio",      0.50f, 0.95f, 1.05f, 1f, 25f);
            Cue(bank.slideLoop, clips, "slide_loop",         0.30f, 1.00f, 1.00f, 1f, 25f);
            Cue(bank.roll, clips, "cambalhota",              0.50f, 0.95f, 1.05f, 1f, 25f);
            Cue(bank.cloth, clips, "roupa",                  0.30f, 0.92f, 1.08f, 1f, 15f);
            Cue(bank.proneDown, clips, "deitar",             0.50f, 0.95f, 1.05f, 1f, 20f);
            Cue(bank.proneUp, clips, "levantar",             0.40f, 0.95f, 1.05f, 1f, 20f);
            Cue(bank.ledgeGrab, clips, "agarrar",            0.50f, 0.95f, 1.05f, 1f, 25f);
            Cue(bank.wallRunStart, clips, "parede",          0.40f, 0.95f, 1.05f, 1f, 25f);
            // Voz
            Cue(bank.voiceEffort, clips, "voz_esforco",      0.50f, 0.96f, 1.04f, 1f, 25f);
            Cue(bank.voiceShort, clips, "voz_curta",         0.45f, 0.95f, 1.05f, 1f, 25f);
            // Acertos
            Cue(bank.bulletImpact, clips, "impacto",         0.50f, 0.90f, 1.10f, 1f, 40f);
            Cue(bank.bulletHitTarget, clips, "acerto_alvo",  0.60f, 0.95f, 1.05f, 1f, 40f);
            Cue(bank.hitMarker, clips, "hitmarker",          0.35f, 0.98f, 1.02f, 0f, 10f);
            Cue(bank.headshotMarker, clips, "hitmarker_cabeca", 0.45f, 0.98f, 1.02f, 0f, 10f);
            Cue(bank.killConfirm, clips, "abate",            0.50f, 0.98f, 1.02f, 0f, 10f);
            // Gelo
            Cue(bank.iceCreate, clips, "gelo_criar",         0.70f, 0.95f, 1.05f, 1f, 45f);
            Cue(bank.iceImpact, clips, "gelo_impacto",       0.50f, 0.92f, 1.08f, 1f, 40f);
            Cue(bank.iceBreak, clips, "gelo_quebrar",        0.80f, 0.95f, 1.05f, 1f, 50f);
            EditorUtility.SetDirty(bank);

            // Rifle
            Cue(rifle.fireSound, clips, "rifle_tiro",               0.75f, 0.96f, 1.04f, 1f, 120f);
            Cue(rifle.equipSound, clips, "sacar_arma",              0.45f, 0.95f, 1.05f, 1f, 20f);
            Cue(rifle.dryFireSound, clips, "sem_bala",              0.50f, 0.95f, 1.05f, 1f, 15f);
            Cue(rifle.reloadOutSound, clips, "recarga_tira",        0.60f, 0.96f, 1.04f, 1f, 20f);
            Cue(rifle.reloadInSound, clips, "recarga_poe",          0.60f, 0.96f, 1.04f, 1f, 20f);
            Cue(rifle.reloadRackSound, clips, "recarga_engatilha",  0.60f, 0.96f, 1.04f, 1f, 20f);
            // Pistola: mesmos sons de recarga, mais agudos (peças menores)
            Cue(pistol.fireSound, clips, "pistola_tiro",            0.70f, 0.96f, 1.04f, 1f, 100f);
            Cue(pistol.equipSound, clips, "sacar_arma",             0.40f, 1.08f, 1.16f, 1f, 20f);
            Cue(pistol.dryFireSound, clips, "sem_bala",             0.50f, 1.10f, 1.20f, 1f, 15f);
            Cue(pistol.reloadOutSound, clips, "recarga_tira",       0.55f, 1.12f, 1.20f, 1f, 20f);
            Cue(pistol.reloadInSound, clips, "recarga_poe",         0.55f, 1.12f, 1.20f, 1f, 20f);
            Cue(pistol.reloadRackSound, clips, "recarga_engatilha", 0.55f, 1.15f, 1.25f, 1f, 20f);
            // Faca
            Cue(knife.equipSound, clips, "sacar_faca",              0.60f, 0.96f, 1.04f, 1f, 20f);
            Cue(knife.swingSound, clips, "faca_golpe",              0.50f, 0.92f, 1.08f, 1f, 20f);
            Cue(knife.meleeHitSound, clips, "faca_acerto",          0.60f, 0.94f, 1.06f, 1f, 25f);
            // Gelo (gadget)
            Cue(gadget.equipSound, clips, "sacar_gelo",             0.45f, 0.96f, 1.04f, 1f, 20f);
            // AWP: tiro pesado (o som já traz o ferrolho), peças mais graves
            Cue(sniper.fireSound, clips, "awp_tiro",                0.90f, 0.97f, 1.03f, 1f, 200f);
            Cue(sniper.equipSound, clips, "sacar_arma",             0.50f, 0.86f, 0.92f, 1f, 20f);
            Cue(sniper.dryFireSound, clips, "sem_bala",             0.50f, 0.90f, 0.98f, 1f, 15f);
            Cue(sniper.reloadOutSound, clips, "recarga_tira",       0.60f, 0.84f, 0.90f, 1f, 20f);
            Cue(sniper.reloadInSound, clips, "recarga_poe",         0.60f, 0.84f, 0.90f, 1f, 20f);
            Cue(sniper.reloadRackSound, clips, "recarga_engatilha", 0.65f, 0.82f, 0.88f, 1f, 20f);
            EditorUtility.SetDirty(sniper);
            // Kit médico: zíper ao pegar, toque suave ao curar
            Cue(medkit.equipSound, clips, "kit_abrir",             0.55f, 0.95f, 1.05f, 1f, 15f);
            Cue(medkit.useCompleteSound, clips, "kit_cura",        0.55f, 0.98f, 1.02f, 0.3f, 15f);
            EditorUtility.SetDirty(medkit);

            EditorUtility.SetDirty(rifle);
            EditorUtility.SetDirty(pistol);
            EditorUtility.SetDirty(knife);
            EditorUtility.SetDirty(gadget);
        }

        private static void Cue(SoundCue cue, Dictionary<string, List<AudioClip>> clips, string prefix,
            float volume, float minPitch, float maxPitch, float spatialBlend, float maxDistance)
        {
            if (cue == null) return;
            bool firstTime = !cue.HasClips;
            if (clips.TryGetValue(prefix, out List<AudioClip> found) && found.Count > 0)
                cue.clips = found.ToArray();
            if (firstTime)
                cue.Set(volume, minPitch, maxPitch, spatialBlend, maxDistance);
        }

        /// <summary>Todos os sons da pasta, agrupados pelo prefixo ("passo_03" vira "passo").</summary>
        private static Dictionary<string, List<AudioClip>> LoadSoundClips()
        {
            var result = new Dictionary<string, List<AudioClip>>();
            if (!AssetDatabase.IsValidFolder(SoundFolder))
            {
                Debug.LogWarning($"[Rogue] Pasta de sons não encontrada ({SoundFolder}). O jogo roda sem som.");
                return result;
            }

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { SoundFolder });
            var paths = new List<string>();
            foreach (string guid in guids) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);

            foreach (string path in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                Match match = Regex.Match(fileName, @"^(.*)_(\d+)$");
                string prefix = match.Success ? match.Groups[1].Value : fileName;

                if (!result.TryGetValue(prefix, out List<AudioClip> list))
                    result[prefix] = list = new List<AudioClip>();
                list.Add(clip);
            }

            Debug.Log($"[Rogue] Sons: {paths.Count} arquivos em {result.Count} grupos.");
            return result;
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
        /// <summary>
        /// UMP de blocos: corpo quadrado, cano curto, carregador reto, coronha vazada e trilho em cima.
        /// O ponto de giro é o cabo (mão direita).
        /// </summary>
        private static Transform CreateUmp(Transform parent, Material body, Material dark, out Transform muzzle,
            out Transform supportGrip, out Transform magazine, out Transform action, out Transform ejectionPort)
        {
            var gun = new GameObject("UMP").transform;
            gun.SetParent(parent, false);

            Part("Corpo", gun, new Vector3(0f, 0.03f, 0.13f), new Vector3(0.065f, 0.105f, 0.36f), body);
            Part("Guarda_Mao", gun, new Vector3(0f, 0.01f, 0.27f), new Vector3(0.06f, 0.07f, 0.1f), dark);
            Part("Cano", gun, new Vector3(0f, 0.045f, 0.36f), new Vector3(0.032f, 0.032f, 0.1f), dark);
            Part("Cabo", gun, new Vector3(0f, -0.06f, 0f), new Vector3(0.045f, 0.11f, 0.05f), dark, new Vector3(-15f, 0f, 0f));
            magazine = Part("Carregador", gun, new Vector3(0f, -0.09f, 0.15f), new Vector3(0.04f, 0.16f, 0.06f), dark,
                new Vector3(8f, 0f, 0f));
            Part("Trilho", gun, new Vector3(0f, 0.095f, 0.12f), new Vector3(0.025f, 0.02f, 0.22f), dark);
            Part("Coronha_Cima", gun, new Vector3(0f, 0.045f, -0.17f), new Vector3(0.025f, 0.025f, 0.24f), dark);
            Part("Coronha_Baixo", gun, new Vector3(0f, -0.02f, -0.2f), new Vector3(0.025f, 0.025f, 0.18f), dark);
            Part("Coronha_Apoio", gun, new Vector3(0f, 0.01f, -0.29f), new Vector3(0.03f, 0.1f, 0.03f), dark);
            // Alavanca de engatilhar (lado esquerdo, na frente): a mão esquerda puxa na recarga.
            action = Part("Manivela", gun, new Vector3(-0.042f, 0.06f, 0.22f), new Vector3(0.025f, 0.014f, 0.03f), dark);

            muzzle = CreateMuzzle(gun, new Vector3(0f, 0.045f, 0.42f));
            ejectionPort = CreatePoint("Janela_Capsula", gun, new Vector3(0.036f, 0.05f, 0.1f));

            supportGrip = new GameObject("Apoio_MaoEsquerda").transform;
            supportGrip.SetParent(gun, false);
            supportGrip.localPosition = new Vector3(0f, -0.025f, 0.25f);

            return gun;
        }

        /// <summary>A 12 de blocos: coronha e telha de madeira, cano comprido com o tubo de cartuchos embaixo.</summary>
        private static Transform CreateShotgun(Transform parent, Material wood, Material dark, out Transform muzzle,
            out Transform supportGrip, out Transform action, out Transform ejectionPort)
        {
            var gun = new GameObject("Doze").transform;
            gun.SetParent(parent, false);

            Part("Coronha", gun, new Vector3(0f, -0.02f, -0.2f), new Vector3(0.05f, 0.1f, 0.26f), wood, new Vector3(6f, 0f, 0f));
            Part("Cabo", gun, new Vector3(0f, -0.05f, -0.03f), new Vector3(0.045f, 0.09f, 0.06f), wood, new Vector3(-20f, 0f, 0f));
            Part("Corpo", gun, new Vector3(0f, 0.02f, 0.08f), new Vector3(0.055f, 0.075f, 0.22f), dark);
            Part("Cano", gun, new Vector3(0f, 0.035f, 0.43f), new Vector3(0.032f, 0.032f, 0.5f), dark);
            Part("Tubo", gun, new Vector3(0f, -0.005f, 0.38f), new Vector3(0.03f, 0.03f, 0.4f), dark);
            // Telha (a peça que bombeia): a mão esquerda segura nela e ela corre para trás depois de cada tiro.
            action = Part("Telha", gun, new Vector3(0f, -0.005f, 0.32f), new Vector3(0.05f, 0.05f, 0.15f), wood);
            Part("Mira", gun, new Vector3(0f, 0.055f, 0.66f), new Vector3(0.01f, 0.015f, 0.01f), dark);

            muzzle = CreateMuzzle(gun, new Vector3(0f, 0.035f, 0.69f));
            ejectionPort = CreatePoint("Janela_Capsula", gun, new Vector3(0.03f, 0.03f, 0.1f));

            supportGrip = new GameObject("Apoio_MaoEsquerda").transform;
            supportGrip.SetParent(gun, false);
            supportGrip.localPosition = new Vector3(0f, -0.03f, 0.32f);
            return gun;
        }

        /// <summary>Granada de mão: bola verde-oliva com espoleta e alavanca.</summary>
        private static Transform CreateFragGrenade(Transform parent, Material body, Material dark)
        {
            var grenade = new GameObject("Granada").transform;
            grenade.SetParent(parent, false);
            Primitive(PrimitiveType.Sphere, "Corpo", grenade, Vector3.zero, new Vector3(0.07f, 0.085f, 0.07f), body);
            Part("Espoleta", grenade, new Vector3(0f, 0.05f, 0f), new Vector3(0.025f, 0.025f, 0.025f), dark);
            Part("Alavanca", grenade, new Vector3(0.018f, 0.03f, 0f), new Vector3(0.008f, 0.06f, 0.018f), dark,
                new Vector3(0f, 0f, -12f));
            return grenade;
        }

        /// <summary>Flash/smoke: lata com faixa colorida (azul = flash, verde = smoke) e espoleta em cima.</summary>
        private static Transform CreateCanGrenade(string grenadeName, Transform parent, Material body, Material band, Material dark)
        {
            var grenade = new GameObject(grenadeName).transform;
            grenade.SetParent(parent, false);
            Primitive(PrimitiveType.Cylinder, "Corpo", grenade, Vector3.zero, new Vector3(0.055f, 0.055f, 0.055f), body);
            Primitive(PrimitiveType.Cylinder, "Faixa", grenade, new Vector3(0f, 0.02f, 0f), new Vector3(0.058f, 0.008f, 0.058f), band);
            Part("Espoleta", grenade, new Vector3(0f, 0.065f, 0f), new Vector3(0.025f, 0.02f, 0.025f), dark);
            return grenade;
        }

        private static void Primitive(PrimitiveType type, string partName, Transform parent, Vector3 localPosition,
            Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        /// <summary>Textura de bolinha macia (fumaça e brilho das granadas), gerada uma vez e salva como asset.</summary>
        private static Texture2D GetParticleTexture()
        {
            string path = $"{MaterialFolder}/T_Particula.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;

            const int size = 64;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "T_Particula", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    alpha = alpha * alpha * (3f - 2f * alpha) * (0.65f + 0.35f * Mathf.PerlinNoise(x * 0.11f, y * 0.11f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>
        /// Textura do clarão do cano: miolo forte + 4 pontas grandes e 4 finas (estrela). Gerada uma vez e salva.
        /// A partícula gira a cada tiro, então a estrela nunca fica igual.
        /// </summary>
        private static Texture2D GetFlashTexture()
        {
            string path = $"{MaterialFolder}/T_Clarao.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;

            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "T_Clarao", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float core = Mathf.Exp(-(r * r) / (0.18f * 0.18f));
                    float glow = Mathf.Exp(-(r * r) / (0.42f * 0.42f)) * 0.35f;
                    float main = Mathf.Pow(Mathf.Abs(Mathf.Cos(2f * angle)), 28f);
                    float thin = Mathf.Pow(Mathf.Abs(Mathf.Sin(2f * angle)), 48f) * 0.55f;
                    float reach = 0.8f + 0.2f * Mathf.PerlinNoise(angle * 3f + 10f, 0.5f); // pontas de tamanhos diferentes
                    float fade = Mathf.Clamp01(1f - r / reach);
                    float spikes = Mathf.Max(main, thin) * fade * fade;

                    float alpha = Mathf.Clamp01(core + glow + spikes) * Mathf.Clamp01((1f - r) * 4f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>Material das cápsulas (partículas com forma de cilindro): metal que aceita a cor de cada uma.</summary>
        private static Material GetShellMaterial(string materialName)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Particles/Standard Surface");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = Color.white; // a cor (latão / vermelho) vem de cada cápsula
            material.SetFloat("_Metallic", 0.7f);
            material.SetFloat("_Glossiness", 0.65f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Malha de uma forma pronta da Unity (cilindro, cubo...).</summary>
        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        /// <summary>Animações da UAL2 (slide, subida da beirada, arremesso), importadas como Legacy.</summary>
        private static Object[] LoadExtraClips()
        {
            if (AssetDatabase.LoadMainAssetAtPath(Ual2ClipsPath) == null)
            {
                Debug.LogWarning($"[Rogue] Animações da UAL2 não encontradas em {Ual2ClipsPath}: o slide e a subida usam as poses antigas.");
                return new Object[0];
            }

            EnsureLegacyAnimationImport(Ual2ClipsPath);
            var clips = new List<Object>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(Ual2ClipsPath))
            {
                if (asset is AnimationClip clip && clip.legacy && !clip.name.StartsWith("__preview__"))
                    clips.Add(clip);
            }
            if (clips.Count == 0)
                Debug.LogWarning("[Rogue] O arquivo da UAL2 não tem animações Legacy (veja o Animation Method no Inspector).");
            else
                Debug.Log($"[Rogue] Animações da UAL2: {clips.Count} ({string.Join(", ", clips.ConvertAll(c => c.name))}).");
            return clips.ToArray();
        }

        /// <summary>Material de partícula: transparente (fumaça) ou somando luz (fogo/clarão).</summary>
        private static Material GetParticleMaterial(string materialName, Texture texture, bool additive)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
                if (shader == null) shader = Shader.Find(additive ? "Mobile/Particles/Additive" : "Mobile/Particles/Alpha Blended");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Material de linha que usa a cor de cada ponta (caminho do arremesso).</summary>
        private static Material GetLineMaterial(string materialName)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Renomeia um arquivo de config (mantém o mesmo id, então nada perde a ligação).</summary>
        private static void RenameAssetIfExists(string oldPath, string newName)
        {
            string newPath = oldPath.Substring(0, oldPath.LastIndexOf('/') + 1) + newName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(oldPath) == null) return;
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(newPath) != null) return;

            string error = AssetDatabase.RenameAsset(oldPath, newName);
            if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"[Rogue] Não deu para renomear {oldPath}: {error}");
        }

        /// <summary>
        /// AWP de blocos (verde oliva): coronha, corpo, cano longo com freio, luneta, ferrolho e carregador.
        /// O ponto de giro é o cabo (mão direita).
        /// </summary>
        private static Transform CreateSniper(Transform parent, Material body, Material dark, out Transform muzzle,
            out Transform supportGrip, out Transform magazine, out Transform action, out Transform ejectionPort)
        {
            var gun = new GameObject("AWP").transform;
            gun.SetParent(parent, false);

            Part("Coronha", gun, new Vector3(0f, -0.01f, -0.2f), new Vector3(0.06f, 0.12f, 0.26f), body);
            Part("Corpo", gun, new Vector3(0f, 0.03f, 0.12f), new Vector3(0.07f, 0.09f, 0.42f), body);
            Part("Cano", gun, new Vector3(0f, 0.045f, 0.62f), new Vector3(0.03f, 0.03f, 0.6f), dark);
            Part("Freio", gun, new Vector3(0f, 0.045f, 0.94f), new Vector3(0.045f, 0.045f, 0.06f), dark);
            Part("Cabo", gun, new Vector3(0f, -0.06f, 0f), new Vector3(0.045f, 0.12f, 0.055f), dark, new Vector3(-18f, 0f, 0f));
            magazine = Part("Carregador", gun, new Vector3(0f, -0.05f, 0.16f), new Vector3(0.05f, 0.09f, 0.08f), dark);
            Part("Luneta", gun, new Vector3(0f, 0.13f, 0.14f), new Vector3(0.05f, 0.05f, 0.3f), dark);
            Part("Luneta_Frente", gun, new Vector3(0f, 0.13f, 0.3f), new Vector3(0.065f, 0.065f, 0.04f), dark);
            Part("Luneta_Tras", gun, new Vector3(0f, 0.13f, -0.01f), new Vector3(0.06f, 0.06f, 0.04f), dark);
            // Ferrolho: alavanca do lado direito (a mão direita levanta, puxa e empurra depois de cada tiro).
            action = Part("Ferrolho", gun, new Vector3(0.05f, 0.05f, 0.02f), new Vector3(0.05f, 0.015f, 0.015f), dark);

            muzzle = CreateMuzzle(gun, new Vector3(0f, 0.045f, 0.98f));
            ejectionPort = CreatePoint("Janela_Capsula", gun, new Vector3(0.038f, 0.06f, 0.08f));

            supportGrip = new GameObject("Apoio_MaoEsquerda").transform;
            supportGrip.SetParent(gun, false);
            supportGrip.localPosition = new Vector3(0f, -0.015f, 0.22f);

            return gun;
        }

        /// <summary>Pistola de blocos: ferrolho, corpo e cabo. A mão esquerda apoia por baixo da direita.</summary>
        private static Transform CreatePistol(Transform parent, Material body, Material dark, out Transform muzzle,
            out Transform supportGrip, out Transform magazine, out Transform action, out Transform ejectionPort)
        {
            var pistol = new GameObject("Pistola").transform;
            pistol.SetParent(parent, false);

            // Ferrolho: corre para trás a cada tiro; na recarga a mão esquerda puxa.
            action = Part("Ferrolho", pistol, new Vector3(0f, 0.035f, 0.06f), new Vector3(0.032f, 0.035f, 0.17f), dark);
            Part("Corpo", pistol, new Vector3(0f, 0.01f, 0.05f), new Vector3(0.03f, 0.025f, 0.14f), body);
            Part("Cabo", pistol, new Vector3(0f, -0.045f, -0.005f), new Vector3(0.03f, 0.1f, 0.045f), dark, new Vector3(-12f, 0f, 0f));
            // Carregador dentro do cabo (a base aparece um pouco embaixo); sai na mão esquerda ao recarregar.
            magazine = Part("Carregador", pistol, new Vector3(0f, -0.074f, 0.001f), new Vector3(0.024f, 0.07f, 0.034f), body, new Vector3(-12f, 0f, 0f));

            muzzle = CreateMuzzle(pistol, new Vector3(0f, 0.035f, 0.15f));
            ejectionPort = CreatePoint("Janela_Capsula", pistol, new Vector3(0.02f, 0.045f, 0.05f));

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

        /// <summary>
        /// Kit médico: caixa branca com cruz vermelha (em cima e na frente) e alça escura.
        /// O ponto de giro é a mão direita, no lado direito da caixa; a esquerda segura o outro lado.
        /// </summary>
        private static Transform CreateMedkit(Transform parent, Material body, Material cross, Material dark,
            out Transform supportGrip)
        {
            var kit = new GameObject("KitMedico").transform;
            kit.SetParent(parent, false);

            const float half = 0.1f; // metade da largura
            Vector3 center = new Vector3(-half, 0f, 0f);
            Part("Caixa", kit, center, new Vector3(half * 2f, 0.11f, 0.12f), body);
            Part("Alca", kit, center + new Vector3(0f, 0.065f, 0f), new Vector3(0.09f, 0.02f, 0.025f), dark);
            Part("Cruz_Cima_A", kit, center + new Vector3(0f, 0.0565f, 0f), new Vector3(0.09f, 0.004f, 0.03f), cross);
            Part("Cruz_Cima_B", kit, center + new Vector3(0f, 0.0565f, 0f), new Vector3(0.03f, 0.004f, 0.09f), cross);
            Part("Cruz_Frente_A", kit, center + new Vector3(0f, 0f, 0.0615f), new Vector3(0.08f, 0.026f, 0.004f), cross);
            Part("Cruz_Frente_B", kit, center + new Vector3(0f, 0f, 0.0615f), new Vector3(0.026f, 0.08f, 0.004f), cross);

            supportGrip = new GameObject("Apoio_MaoEsquerda").transform;
            supportGrip.SetParent(kit, false);
            supportGrip.localPosition = new Vector3(-half * 2f, 0f, 0f);
            return kit;
        }

        /// <summary>Ponta do cano (de onde sai o tiro e o clarão do WeaponFx).</summary>
        private static Transform CreateMuzzle(Transform gun, Vector3 localPosition)
        {
            return CreatePoint("Cano_Saida", gun, localPosition);
        }

        /// <summary>Ponto vazio preso na arma (cano, janela da cápsula...).</summary>
        private static Transform CreatePoint(string pointName, Transform parent, Vector3 localPosition)
        {
            Transform point = new GameObject(pointName).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            return point;
        }

        private static Transform Part(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material,
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
            return go.transform;
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

        private static Material GetMaterial(string name, Color color, float smoothness = -1f, float metallic = -1f)
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
            if (metallic >= 0f) material.SetFloat("_Metallic", metallic);
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

        private static SerializedProperty FindField(SerializedObject serialized, Object component, string fieldName)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
                Debug.LogError($"[Rogue] Campo '{fieldName}' não encontrado em {component.GetType().Name}.");
            return property;
        }

        private static void AssignFloat(Object component, string fieldName, float value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = FindField(serialized, component, fieldName);
            if (property == null) return;
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignBool(Object component, string fieldName, bool value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = FindField(serialized, component, fieldName);
            if (property == null) return;
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignString(Object component, string fieldName, string value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = FindField(serialized, component, fieldName);
            if (property == null) return;
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(Object component, string fieldName, Object[] values)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = FindField(serialized, component, fieldName);
            if (property == null) return;
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignInt(Object component, string fieldName, int value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError($"[Rogue] Campo '{fieldName}' não encontrado em {component.GetType().Name}.");
                return;
            }
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignWeapon(Object weapons, string slotField, WeaponConfig config, Transform model,
            Transform muzzle, Transform supportGrip, Transform magazine = null, Transform action = null,
            Transform ejectionPort = null)
        {
            Assign(weapons, $"{slotField}.config", config);
            Assign(weapons, $"{slotField}.model", model);
            Assign(weapons, $"{slotField}.muzzle", muzzle);
            Assign(weapons, $"{slotField}.muzzleFlash", null); // o clarão agora é do WeaponFx (partículas + luz)
            Assign(weapons, $"{slotField}.supportGrip", supportGrip);
            Assign(weapons, $"{slotField}.magazine", magazine);
            Assign(weapons, $"{slotField}.action", action);
            Assign(weapons, $"{slotField}.ejectionPort", ejectionPort);
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
