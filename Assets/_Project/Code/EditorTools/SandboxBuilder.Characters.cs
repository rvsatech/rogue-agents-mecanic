using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Rogue.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.EditorTools
{
    /// <summary>
    /// Personagens jogáveis (agentes e skins): lê Art/Characters/Agentes.json (feito pelo gerador
    /// Tools/CharGen), arruma o import dos modelos e texturas, cria os materiais, um arquivo por agente
    /// (Config/Personagens) e a lista que o menu B usa.
    /// </summary>
    public static partial class SandboxBuilder
    {
        private const string CharactersFolder = "Assets/_Project/Art/Characters";
        private const string CharacterConfigFolder = ConfigFolder + "/Personagens";

#pragma warning disable 0649 // campos preenchidos pelo JsonUtility
        [System.Serializable]
        private class AgentManifest
        {
            public int versao;
            public string cabeloCor;
            public string cabeloNormal;
            public string olhoCor;
            public string olhoNormal;
            public AgentEntry[] agentes;
        }

        [System.Serializable]
        private class AgentEntry
        {
            public string id;
            public string nome;
            public string descricao;
            public string modelo;
            public string normal;
            public float alturaCorpo;
            public float escalaQuadril;
            public string[] pecas;
            public SkinEntry[] skins;
        }

        [System.Serializable]
        private class SkinEntry
        {
            public string id;
            public string nome;
            public string cor;
            public string acessorios;
            public float[] corCabelo;
            public string[] pecas;
            public bool esconderCabelo;
        }
#pragma warning restore 0649

        [MenuItem("Rogue/Atualizar personagens (agentes e skins)")]
        public static void RebuildCharacters()
        {
            CharacterRoster roster = BuildCharacterLibrary();
            if (roster != null)
                Debug.Log($"[Rogue] {roster.Count} agentes prontos em {CharacterConfigFolder}. " +
                          "Recrie a cena (menu Rogue) para o jogador usar a lista nova.");
        }

        /// <summary>
        /// Monta (ou atualiza) os agentes a partir do Agentes.json. Volta null se não tiver agentes.
        /// saveAssets: false quando é parte da montagem da cena (lá o salvamento é no fim, depois da cena nova).
        /// </summary>
        private static CharacterRoster BuildCharacterLibrary(bool saveAssets = true)
        {
            string manifestPath = $"{CharactersFolder}/Agentes.json";
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[Rogue] Sem agentes ({manifestPath} não existe): o jogador usa o manequim.");
                return null;
            }

            var manifest = JsonUtility.FromJson<AgentManifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
            if (manifest == null || manifest.agentes == null || manifest.agentes.Length == 0) return null;

            EnsureFolder(CharacterConfigFolder);
            EnsureFolder($"{CharactersFolder}/Materiais");

            Texture2D hairColor = ImportCharacterTexture(manifest.cabeloCor, false, 2048);
            Texture2D hairNormal = ImportCharacterTexture(manifest.cabeloNormal, true, 2048);
            Texture2D eyeColor = ImportCharacterTexture(manifest.olhoCor, false, 256);
            Texture2D eyeNormal = ImportCharacterTexture(manifest.olhoNormal, true, 256);
            Material eyes = GetCharacterMaterial("M_Olhos", eyeColor, eyeNormal, Color.white, 0.85f);

            AnimationClip[] baseClips = LoadBaseClips();
            var definitions = new List<CharacterDefinition>();
            foreach (AgentEntry agent in manifest.agentes)
            {
                string modelPath = $"{CharactersFolder}/{agent.modelo}";
                EnsureLegacyAnimationImport(modelPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[Rogue] Modelo do agente {agent.nome} não encontrado: {modelPath}");
                    continue;
                }

                Texture2D normal = ImportCharacterTexture(agent.normal, true, 1024);
                string agentSlug = Slug(agent.nome);
                var definition = LoadOrCreate<CharacterDefinition>($"{CharacterConfigFolder}/{agentSlug}.asset", out _);
                definition.id = agent.id;
                definition.displayName = agent.nome;
                definition.description = agent.descricao;
                definition.model = prefab;
                definition.bodyHeight = agent.alturaCorpo;
                definition.hipsScale = agent.escalaQuadril;
                definition.animationHostPath = FindAnimationHost(prefab, baseClips);

                var skins = new List<CharacterSkin>();
                foreach (SkinEntry skin in agent.skins)
                {
                    string baseName = $"M_{agentSlug}_{Slug(skin.nome)}";
                    Texture2D color = ImportCharacterTexture(skin.cor, false, 1024);
                    Color hairTint = skin.corCabelo != null && skin.corCabelo.Length >= 3
                        ? new Color(skin.corCabelo[0], skin.corCabelo[1], skin.corCabelo[2])
                        : Color.gray;
                    Material accessories = null;
                    if (!string.IsNullOrEmpty(skin.acessorios))
                    {
                        Texture2D accessoryTexture = ImportCharacterTexture(skin.acessorios, false, 256);
                        accessories = GetCharacterMaterial($"{baseName}_Acessorios", accessoryTexture, null, Color.white, 0.35f);
                    }

                    skins.Add(new CharacterSkin
                    {
                        id = skin.id,
                        displayName = skin.nome,
                        body = GetCharacterMaterial($"{baseName}_Corpo", color, normal, Color.white, 0.22f),
                        hair = GetCharacterMaterial($"{baseName}_Cabelo", hairColor, hairNormal, hairTint, 0.3f),
                        eyes = eyes,
                        accessories = accessories,
                        parts = skin.pecas ?? new string[0],
                        hideHair = skin.esconderCabelo
                    });
                }

                definition.skins = skins.ToArray();
                EditorUtility.SetDirty(definition);
                definitions.Add(definition);
            }

            if (definitions.Count == 0) return null;
            var roster = LoadOrCreate<CharacterRoster>($"{CharacterConfigFolder}/Personagens.asset", out _);
            roster.characters = definitions.ToArray();
            EditorUtility.SetDirty(roster);
            if (saveAssets) AssetDatabase.SaveAssets();
            return roster;
        }

        /// <summary>Coloca um agente na cena (com a skin escolhida), na altura do jogador.</summary>
        private static Transform CreateAgentModel(Transform parent, CharacterDefinition agent, int skinIndex, float standHeight)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(agent.model);
            instance.name = "Modelo";
            Transform model = instance.transform;
            model.SetParent(parent, false);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one * (standHeight / Mathf.Max(agent.bodyHeight, 0.1f));

            CharacterSkin skin = agent.GetSkin(skinIndex);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.On;
                if (skin == null) continue;
                string part = r.gameObject.name;
                if (part == "Corpo" && skin.body != null) r.sharedMaterial = skin.body;
                else if (part == "Cabelo")
                {
                    if (skin.hair != null) r.sharedMaterial = skin.hair;
                    r.gameObject.SetActive(!skin.hideHair);
                }
                else if (part == "Olhos" && skin.eyes != null) r.sharedMaterial = skin.eyes;
                else if (part.StartsWith("Peca_"))
                {
                    if (skin.accessories != null) r.sharedMaterial = skin.accessories;
                    r.gameObject.SetActive(System.Array.IndexOf(skin.parts, part) >= 0);
                }
            }
            return model;
        }

        /// <summary>
        /// Em qual objeto do modelo o componente Animation tem que ficar para os caminhos das animações do
        /// manequim (ex.: "Rig/root/DEF-hips") acharem os ossos. Volta o caminho a partir da raiz ("" = a raiz).
        /// </summary>
        private static string FindAnimationHost(GameObject prefab, AnimationClip[] clips)
        {
            foreach (AnimationClip clip in clips)
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (string.IsNullOrEmpty(binding.path)) continue;
                    foreach (Transform candidate in prefab.GetComponentsInChildren<Transform>(true))
                    {
                        if (candidate.Find(binding.path) == null) continue;
                        return RelativePath(prefab.transform, candidate);
                    }
                    // Raiz do modelo = o próprio "Rig" (importado sem animação e com uma raiz só): o jogo põe um pai
                    // em volta ao ligar o modelo (CharacterModelAnimator.BindModel). Outro caso = nomes diferentes.
                    if (prefab.transform.Find("root") == null)
                        Debug.LogWarning($"[Rogue] As animações do manequim não acham os ossos de {prefab.name} " +
                                         $"(caminho '{binding.path}'). Confira os nomes dos ossos do modelo.");
                    return "";
                }
            }
            return "";
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root) return "";
            string path = target.name;
            for (Transform t = target.parent; t != null && t != root; t = t.parent) path = $"{t.name}/{path}";
            return path;
        }

        /// <summary>Todas as animações do manequim (entram nos agentes quando o jogo troca de modelo).</summary>
        private static AnimationClip[] LoadBaseClips()
        {
            EnsureLegacyAnimationImport(CharacterModelPath);
            var clips = new List<AnimationClip>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(CharacterModelPath))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) clips.Add(clip);
            }
            return clips.ToArray();
        }

        private static Texture2D ImportCharacterTexture(string relativePath, bool normalMap, int maxSize)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            string path = $"{CharactersFolder}/{relativePath}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Rogue] Textura não encontrada: {path}");
                return null;
            }

            bool changed = false;
            TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType != type) { importer.textureType = type; changed = true; }
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }
            if (!normalMap && !importer.sRGBTexture) { importer.sRGBTexture = true; changed = true; }
            if (importer.anisoLevel < 2) { importer.anisoLevel = 2; changed = true; }
            if (changed) importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material GetCharacterMaterial(string name, Texture2D albedo, Texture2D normal, Color color, float smoothness)
        {
            string path = $"{CharactersFolder}/Materiais/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = GraphicsSettings.currentRenderPipeline != null
                    ? Shader.Find("Universal Render Pipeline/Lit")
                    : Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.mainTexture = albedo;
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.SetTexture("_BumpMap", null);
                material.DisableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>"Faísca" → "Faisca" (nome de arquivo sem acento nem espaço).</summary>
        private static string Slug(string text)
        {
            var builder = new StringBuilder();
            foreach (char c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) builder.Append(c);
            }
            return builder.ToString();
        }
    }
}
