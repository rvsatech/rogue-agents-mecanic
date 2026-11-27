using System;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Um agente jogável: modelo 3D, medidas do corpo e skins.
    /// Os arquivos são criados pelo menu Rogue a partir de Art/Characters/Agentes.json (gerador de personagens).
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Personagem", fileName = "Personagem")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Tooltip("Modelo 3D do agente (.glb com os mesmos ossos do manequim).")]
        public GameObject model;

        [Tooltip("Altura do corpo no modelo (sem cabelo e chapéu): o modelo é escalado até a altura do jogador.")]
        public float bodyHeight = 1.8f;

        [Tooltip("Altura do quadril deste corpo ÷ a do manequim das animações (perna mais comprida = quadril mais alto).")]
        public float hipsScale = 1f;

        [Tooltip("Onde fica o componente Animation, a partir da raiz do modelo (vazio = na raiz). O menu Rogue mede.")]
        public string animationHostPath = "";

        public CharacterSkin[] skins = Array.Empty<CharacterSkin>();

        public int SkinCount => skins != null ? skins.Length : 0;

        public CharacterSkin GetSkin(int index) =>
            skins != null && index >= 0 && index < skins.Length ? skins[index] : null;
    }

    /// <summary>Visual de uma skin: materiais e quais peças opcionais (chapéu, fone...) aparecem.</summary>
    [Serializable]
    public sealed class CharacterSkin
    {
        public string id;
        public string displayName;
        public Material body;
        public Material hair;
        public Material eyes;
        public Material accessories;

        [Tooltip("Peças opcionais ligadas nesta skin (nome do objeto no modelo, ex.: Peca_Gorro). As outras somem.")]
        public string[] parts = Array.Empty<string>();

        [Tooltip("Esconde o cabelo (e a sobrancelha) — balaclava.")]
        public bool hideHair;
    }
}
