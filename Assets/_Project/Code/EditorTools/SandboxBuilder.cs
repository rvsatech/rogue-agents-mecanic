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
    /// o jogador, a câmera e o HUD. Pode rodar de novo quando quiser recriar a cena.
    /// </summary>
    public static class SandboxBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox_Movement.unity";
        private const string ConfigFolder = "Assets/_Project/Config";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";

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

            // ---------- Configs ----------
            MovementConfig movementConfig = LoadOrCreate<MovementConfig>($"{ConfigFolder}/MovementConfig.asset");
            CameraConfig cameraConfig = LoadOrCreate<CameraConfig>($"{ConfigFolder}/CameraConfig.asset");
            movementConfig.environmentMask = environmentMask;
            cameraConfig.collisionMask = environmentMask;
            EditorUtility.SetDirty(movementConfig);
            EditorUtility.SetDirty(cameraConfig);

            // ---------- Materiais ----------
            Material mGround = GetMaterial("M_Ground", new Color(0.36f, 0.37f, 0.39f));
            Material mWall = GetMaterial("M_Wall", new Color(0.26f, 0.42f, 0.62f));
            Material mBox = GetMaterial("M_Box", new Color(0.85f, 0.55f, 0.2f));
            Material mIce = GetMaterial("M_Ice", new Color(0.55f, 0.9f, 1f));
            Material mTarget = GetMaterial("M_Target", new Color(0.85f, 0.18f, 0.18f));
            Material mMarker = GetMaterial("M_Marker", new Color(0.95f, 0.95f, 0.95f));
            Material mPlayer = GetMaterial("M_Player", new Color(0.2f, 0.75f, 0.45f));
            Material mDark = GetMaterial("M_Dark", new Color(0.1f, 0.1f, 0.12f));

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

            // Degraus de escalada (alturas diferentes para testar o mantle)
            Transform steps = new GameObject("Escalada").transform;
            steps.SetParent(env, false);
            float[] heights = { 0.5f, 1f, 1.5f, 2f, 2.5f, 3.5f };
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                Block($"Caixa_{h}m", steps, new Vector3(-9f + i * 3.5f, h * 0.5f, -12f), new Vector3(2f, h, 2f), mBox, climbableLayer);
            }

            // Túnel baixo (1,3 m): só passa agachado
            Transform tunnel = new GameObject("Tunel_Agachar").transform;
            tunnel.SetParent(env, false);
            Block("Lateral_E", tunnel, new Vector3(5f, 0.65f, 15f), new Vector3(0.3f, 1.3f, 6f), mWall, climbableLayer);
            Block("Lateral_D", tunnel, new Vector3(7f, 0.65f, 15f), new Vector3(0.3f, 1.3f, 6f), mWall, climbableLayer);
            Block("Teto", tunnel, new Vector3(6f, 1.45f, 15f), new Vector3(2.3f, 0.3f, 6f), mWall, climbableLayer);

            // Rampa
            Block("Rampa", env, new Vector3(18f, 1.6f, -14f), new Vector3(4f, 0.3f, 10f), mBox, groundLayer, new Vector3(-20f, 0f, 0f));

            // Bloco de gelo de exemplo (NÃO escalável)
            Block("Gelo_Exemplo", env, new Vector3(-4f, 1f, -6f), new Vector3(3f, 2f, 0.6f), mIce, nonClimbableLayer);

            // Alvos de tiro (para a mecânica 10)
            Transform targets = new GameObject("Alvos").transform;
            targets.SetParent(env, false);
            Block("Alvo_1", targets, new Vector3(8f, 0.9f, 30f), new Vector3(0.6f, 1.8f, 0.6f), mTarget, targetLayer, isStatic: false);
            Block("Alvo_2", targets, new Vector3(-6f, 0.9f, 32f), new Vector3(0.6f, 1.8f, 0.6f), mTarget, targetLayer, isStatic: false);
            Block("Alvo_3", targets, new Vector3(0f, 0.9f, 37f), new Vector3(0.6f, 1.8f, 0.6f), mTarget, targetLayer, isStatic: false);

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

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Corpo";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(visualGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<MeshRenderer>().sharedMaterial = mPlayer;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Frente";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(visualGo.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.45f, 0.35f);
            marker.transform.localScale = new Vector3(0.35f, 0.15f, 0.25f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = mDark;

            var visual = visualGo.AddComponent<PlayerVisual>();
            Assign(visual, "motor", motor);
            Assign(visual, "input", reader);
            Assign(visual, "body", body.transform);
            Assign(visual, "facingMarker", marker.transform);

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
            Assign(rig, "target", visualGo.transform);

            // ---------- HUD ----------
            var hud = new GameObject("DebugHUD").AddComponent<MovementDebugHUD>();
            Assign(hud, "motor", motor);

            // ---------- Salvar ----------
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[Rogue] Cena Sandbox_Movement criada. Aperte Play e clique na tela do jogo.");
        }

        // ================= Helpers =================

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

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static Material GetMaterial(string name, Color color)
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
