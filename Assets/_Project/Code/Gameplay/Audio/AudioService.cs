using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Toca os efeitos sonoros com um pool fixo de AudioSources (nada é criado/destruído durante o jogo).
    /// - Sorteia variação e tom do SoundCue.
    /// - Se todas as vozes estiverem ocupadas, reaproveita a que começou há mais tempo.
    /// - Um por cena (o menu Rogue cria). Se faltar, cria sozinho no primeiro som.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class AudioService : MonoBehaviour
    {
        [Tooltip("Quantos sons podem tocar ao mesmo tempo.")]
        [SerializeField] private int voiceCount = 24;

        [Tooltip("Distância até onde o som 3D fica no volume máximo.")]
        [SerializeField] private float minDistance = 2f;

        private AudioSource[] sources;
        private float[] startTimes;
        private int cursor;

        private static AudioService instance;

        public static AudioService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<AudioService>();
                    if (instance == null)
                        instance = new GameObject("AudioService").AddComponent<AudioService>();
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            EnsureSources();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// Toca um som em uma posição. spatialBlend &lt; 0 = usa o do SoundCue.
        /// Retorna a fonte usada (ou null se o som não tem arquivo).
        /// </summary>
        public AudioSource Play(SoundCue cue, Vector3 position, float volumeScale = 1f, float pitchScale = 1f,
            float spatialBlend = -1f)
        {
            if (cue == null || !cue.HasClips || volumeScale <= 0f) return null;
            AudioClip clip = cue.Pick();
            if (clip == null) return null;

            EnsureSources();
            int index = NextSource();
            AudioSource source = sources[index];

            source.transform.position = position;
            source.clip = clip;
            source.loop = false;
            source.volume = Mathf.Clamp01(cue.volume * volumeScale);
            source.pitch = UnityEngine.Random.Range(cue.pitch.x, cue.pitch.y) * pitchScale;
            source.spatialBlend = spatialBlend >= 0f ? spatialBlend : cue.spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(minDistance + 1f, cue.maxDistance);
            source.Play();

            startTimes[index] = Time.time;
            return source;
        }

        /// <summary>Uma fonte livre; se não houver, a que começou há mais tempo.</summary>
        private int NextSource()
        {
            int count = sources.Length;
            for (int i = 0; i < count; i++)
            {
                int index = (cursor + i) % count;
                if (!sources[index].isPlaying)
                {
                    cursor = (index + 1) % count;
                    return index;
                }
            }

            int oldest = 0;
            for (int i = 1; i < count; i++)
                if (startTimes[i] < startTimes[oldest]) oldest = i;
            return oldest;
        }

        private void EnsureSources()
        {
            if (sources != null) return;

            int count = Mathf.Max(4, voiceCount);
            sources = new AudioSource[count];
            startTimes = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Voz_{i + 1:00}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                sources[i] = source;
            }
        }
    }
}
