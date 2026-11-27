using System;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Um som do jogo: lista de variações (sorteia uma, sem repetir a última), volume e variação de tom.
    /// Trocar o som: arraste outros arquivos para "clips" — ou coloque arquivos na pasta Audio/SFX com o
    /// mesmo prefixo (ex.: passo_07.wav) e rode o menu Rogue de novo.
    /// </summary>
    [Serializable]
    public sealed class SoundCue
    {
        [Tooltip("Variações do som (sorteia uma por vez).")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 0.7f;

        [Tooltip("Tom mínimo e máximo (1 = original). Variar um pouco evita som repetitivo.")]
        public Vector2 pitch = new Vector2(0.95f, 1.05f);

        [Tooltip("0 = som 2D (interface), 1 = som 3D (vem do lugar onde aconteceu).")]
        [Range(0f, 1f)] public float spatialBlend = 1f;

        [Tooltip("Distância máxima em que ainda se ouve (metros).")]
        public float maxDistance = 35f;

        [NonSerialized] private int lastIndex = -1;

        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>Sorteia uma variação, nunca a mesma duas vezes seguidas.</summary>
        public AudioClip Pick()
        {
            if (!HasClips) return null;
            if (clips.Length == 1) return clips[0];

            int index;
            if (lastIndex < 0 || lastIndex >= clips.Length)
            {
                index = UnityEngine.Random.Range(0, clips.Length);
            }
            else
            {
                index = UnityEngine.Random.Range(0, clips.Length - 1);
                if (index >= lastIndex) index++;
            }

            lastIndex = index;
            return clips[index];
        }

        public void Set(float newVolume, float minPitch, float maxPitch, float newSpatialBlend, float newMaxDistance)
        {
            volume = newVolume;
            pitch = new Vector2(minPitch, maxPitch);
            spatialBlend = newSpatialBlend;
            maxDistance = newMaxDistance;
        }
    }
}
