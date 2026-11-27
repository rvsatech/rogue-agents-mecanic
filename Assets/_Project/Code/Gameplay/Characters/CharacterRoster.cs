using System;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Lista dos agentes que aparecem para escolher (menu B).</summary>
    [CreateAssetMenu(menuName = "Rogue/Lista de personagens", fileName = "Personagens")]
    public sealed class CharacterRoster : ScriptableObject
    {
        public CharacterDefinition[] characters = Array.Empty<CharacterDefinition>();

        public int Count => characters != null ? characters.Length : 0;

        public CharacterDefinition Get(int index) =>
            characters != null && index >= 0 && index < characters.Length ? characters[index] : null;
    }
}
