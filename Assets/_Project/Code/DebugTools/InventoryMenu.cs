using System.Collections.Generic;
using Rogue.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rogue.DebugTools
{
    /// <summary>
    /// Inventário (B): escolhe a arma da tecla 1 (UMP, AWP ou a 12), a da tecla 2 (pistola) e o gadget do G
    /// (granada, flash ou smoke). Faca (3), kit médico (4) e parede de gelo (E) ficam sempre com você.
    /// Embaixo: o agente (personagem) e a skin dele — troca na hora e fica salvo para a próxima vez.
    /// Com o menu aberto o mouse fica solto e o clique não atira. B ou Esc fecham.
    /// </summary>
    public sealed class InventoryMenu : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private PlayerCharacter character;

        private readonly List<int> options = new List<int>();
        private bool open;
        private GUIStyle boxStyle;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || weapons == null) return;

            if (keyboard.bKey.wasPressedThisFrame) SetOpen(!open);
            else if (open && keyboard.escapeKey.wasPressedThisFrame) SetOpen(false);
        }

        private void OnDisable()
        {
            if (open) SetOpen(false);
        }

        private void SetOpen(bool value)
        {
            open = value;
            PlayerInputReader.SetMenuOpen(value);
        }

        private void OnGUI()
        {
            if (!open || weapons == null) return;

            GUI.depth = -20; // na frente do HUD
            EnsureStyles();

            bool hasCharacters = character != null && character.Roster != null && character.Roster.Count > 0;
            float width = hasCharacters ? 720f : 620f;
            float height = hasCharacters ? 610f : 360f;
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUI.Box(area, "", boxStyle);

            float x = area.x + 22f;
            float y = area.y + 14f;
            GUI.Label(new Rect(area.x, y, width, 36f), "<b>INVENTÁRIO</b>", titleStyle);
            y += 46f;
            y = DrawRow(x, y, "<b>1 · Primária</b>", LoadoutCategory.Primary);
            y = DrawRow(x, y, "<b>2 · Secundária</b>", LoadoutCategory.Secondary);
            y = DrawRow(x, y, "<b>G · Gadget</b>  (segura o botão e solta para jogar)", LoadoutCategory.Gadget);
            if (hasCharacters) y = DrawCharacters(x, y, width - 44f);
            GUI.Label(new Rect(x, y + 4f, width - 44f, 48f),
                "Sempre com você: 3 faca · 4 kit médico · E parede de gelo\nB ou Esc fecha", labelStyle);
        }

        /// <summary>Agente (personagem) e skin. Troca na hora; a escolha fica salva.</summary>
        private float DrawCharacters(float x, float y, float rowWidth)
        {
            CharacterRoster roster = character.Roster;
            GUI.Label(new Rect(x, y, rowWidth, 24f), "<b>Agente</b>", labelStyle);
            y += 26f;
            const float gap = 8f;
            float buttonWidth = Mathf.Min(150f, (rowWidth - gap * (roster.Count - 1)) / roster.Count);
            for (int i = 0; i < roster.Count; i++)
            {
                CharacterDefinition definition = roster.Get(i);
                if (definition == null) continue;
                string text = i == character.CharacterIndex ? $"<b>► {definition.displayName} ◄</b>" : definition.displayName;
                if (GUI.Button(new Rect(x + i * (buttonWidth + gap), y, buttonWidth, 36f), text, buttonStyle))
                    character.Select(i, 0);
            }
            y += 42f;

            CharacterDefinition current = character.Current;
            if (current == null) return y;
            GUI.Label(new Rect(x, y, rowWidth, 22f), current.description, labelStyle);
            y += 26f;
            GUI.Label(new Rect(x, y, rowWidth, 24f), "<b>Skin</b>", labelStyle);
            y += 26f;
            for (int i = 0; i < current.SkinCount; i++)
            {
                CharacterSkin skin = current.GetSkin(i);
                string text = i == character.SkinIndex ? $"<b>► {skin.displayName} ◄</b>" : skin.displayName;
                if (GUI.Button(new Rect(x + i * (180f + gap), y, 180f, 36f), text, buttonStyle))
                    character.Select(character.CharacterIndex, i);
            }
            return y + 46f;
        }

        private float DrawRow(float x, float y, string title, LoadoutCategory category)
        {
            GUI.Label(new Rect(x, y, 560f, 24f), title, labelStyle);
            y += 26f;

            weapons.GetOptions(category, options);
            int chosen = weapons.GetChoice(category);
            const float buttonWidth = 180f;
            const float gap = 10f;
            for (int i = 0; i < options.Count; i++)
            {
                int slot = options[i];
                WeaponBinding binding = weapons.GetSlot(slot);
                string text = binding.config.displayName;
                if (binding.config.kind == WeaponKind.Throwable)
                    text += binding.config.infiniteItems ? "  ∞" : $"  x{weapons.GetItemCount(slot)}";
                if (slot == chosen) text = $"<b>► {text} ◄</b>";

                var rect = new Rect(x + i * (buttonWidth + gap), y, buttonWidth, 40f);
                if (GUI.Button(rect, text, buttonStyle)) weapons.Choose(category, slot);
            }
            return y + 54f;
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;
            boxStyle = new GUIStyle(GUI.skin.box);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, richText = true };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 17, richText = true };
        }
    }
}
