using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Qual agente e qual skin o jogador está usando. Troca o modelo na hora (menu B) e lembra a escolha
    /// entre partidas (PlayerPrefs). As animações, armas e efeitos continuam os mesmos: o animador religa
    /// os ossos do modelo novo (todos os agentes têm os ossos do manequim).
    /// </summary>
    public sealed class PlayerCharacter : MonoBehaviour
    {
        private const string PrefCharacter = "rogue.personagem";
        private const string PrefSkin = "rogue.skin";

        [SerializeField] private CharacterRoster roster;
        [SerializeField] private CharacterModelAnimator modelAnimator;
        [SerializeField] private PlayerVisual visual;

        [Tooltip("Altura do jogador (m): o modelo é escalado para ela.")]
        [SerializeField] private float standHeight = 1.8f;

        [Tooltip("Modelo que já está na cena (montado pelo menu Rogue) e qual agente/skin ele é.")]
        [SerializeField] private Transform currentModel;
        [SerializeField] private int builtCharacter = -1;
        [SerializeField] private int builtSkin;

        /// <summary>Mudou de agente ou de skin.</summary>
        public event Action Changed;

        public CharacterRoster Roster => roster;
        public int CharacterIndex { get; private set; } = -1;
        public int SkinIndex { get; private set; }
        public CharacterDefinition Current => roster != null ? roster.Get(CharacterIndex) : null;

        private void Awake()
        {
            CharacterIndex = builtCharacter;
            SkinIndex = builtSkin;
        }

        private void Start()
        {
            // O animador pode ter posto um objeto-pai em volta do modelo (BindModel): a raiz certa é a dele.
            if (modelAnimator != null && modelAnimator.Model != null && currentModel != null) currentModel = modelAnimator.Model;
            if (roster == null || roster.Count == 0) return;
            int character = PlayerPrefs.GetInt(PrefCharacter, Mathf.Max(builtCharacter, 0));
            int skin = PlayerPrefs.GetInt(PrefSkin, builtSkin);
            Select(character, skin, false);
        }

        /// <summary>Escolhe agente e skin (troca o modelo se o agente mudou). save: lembra para a próxima vez.</summary>
        public void Select(int character, int skin, bool save = true)
        {
            if (roster == null || roster.Count == 0) return;
            character = Mathf.Clamp(character, 0, roster.Count - 1);
            CharacterDefinition definition = roster.Get(character);
            if (definition == null || definition.model == null) return;
            skin = definition.SkinCount > 0 ? Mathf.Clamp(skin, 0, definition.SkinCount - 1) : 0;

            if (character != CharacterIndex || currentModel == null) SwapModel(definition);
            CharacterIndex = character;
            SkinIndex = skin;
            ApplySkin(definition.GetSkin(skin));

            if (save)
            {
                PlayerPrefs.SetInt(PrefCharacter, character);
                PlayerPrefs.SetInt(PrefSkin, skin);
                PlayerPrefs.Save();
            }
            Changed?.Invoke();
        }

        public void NextCharacter(int step)
        {
            if (roster == null || roster.Count == 0) return;
            int count = roster.Count;
            Select(((Mathf.Max(CharacterIndex, 0) + step) % count + count) % count, 0);
        }

        public void NextSkin(int step)
        {
            CharacterDefinition definition = Current;
            if (definition == null || definition.SkinCount == 0) return;
            int count = definition.SkinCount;
            Select(CharacterIndex, ((SkinIndex + step) % count + count) % count);
        }

        private void SwapModel(CharacterDefinition definition)
        {
            Transform parent = visual != null ? visual.transform : transform;
            if (currentModel != null)
            {
                // Sai da hierarquia já (o Destroy só acontece no fim do frame).
                currentModel.gameObject.SetActive(false);
                currentModel.SetParent(null, false);
                Destroy(currentModel.gameObject);
            }

            GameObject instance = Instantiate(definition.model, parent);
            instance.name = "Modelo";
            Transform model = instance.transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            float height = definition.bodyHeight > 0.1f ? definition.bodyHeight : standHeight;
            model.localScale = Vector3.one * (standHeight / height);
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.On;

            currentModel = model;
            if (modelAnimator != null)
            {
                modelAnimator.BindModel(model, definition.hipsScale, definition.animationHostPath);
                if (modelAnimator.Model != null) currentModel = modelAnimator.Model;
            }
            if (visual != null) visual.RefreshRenderers();
        }

        private void ApplySkin(CharacterSkin skin)
        {
            if (currentModel == null || skin == null) return;
            foreach (Renderer r in currentModel.GetComponentsInChildren<Renderer>(true))
            {
                string part = r.gameObject.name;
                if (part == "Corpo") SetMaterial(r, skin.body);
                else if (part == "Cabelo")
                {
                    SetMaterial(r, skin.hair);
                    r.gameObject.SetActive(!skin.hideHair);
                }
                else if (part == "Olhos") SetMaterial(r, skin.eyes);
                else if (part.StartsWith("Peca_", StringComparison.Ordinal))
                {
                    SetMaterial(r, skin.accessories);
                    r.gameObject.SetActive(skin.parts != null && Array.IndexOf(skin.parts, part) >= 0);
                }
            }
            if (visual != null) visual.RefreshRenderers();
        }

        private static void SetMaterial(Renderer renderer, Material material)
        {
            if (material != null) renderer.sharedMaterial = material;
        }
    }
}
