using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Efeito de quebra da barreira de gelo: pedaços voam para os lados, caem e somem.
    /// - Um pool fixo de pedaços (cubos sem colisor e sem sombra), reaproveitados em fila.
    /// - Só visual: não mexe na simulação (por isso pode usar Random à vontade).
    /// - Sem pedaço no ar, o Update desliga sozinho (custo zero parado).
    /// </summary>
    public sealed class IceShatter : MonoBehaviour
    {
        [Tooltip("Total de pedaços no pool (compartilhado entre todas as quebras).")]
        [SerializeField] private int poolSize = 48;

        [Tooltip("Pedaços por bloco da barreira.")]
        [SerializeField] private int shardsPerSegment = 4;

        [Tooltip("Quanto tempo os pedaços ficam visíveis (segundos).")]
        [SerializeField] private float lifetime = 0.7f;

        [SerializeField] private float gravity = 16f;
        [SerializeField] private float burstSpeed = 3f;

        [Tooltip("Tamanho mínimo e máximo dos pedaços (metros).")]
        [SerializeField] private Vector2 sizeRange = new Vector2(0.12f, 0.32f);

        private Transform[] shards;
        private Vector3[] velocity;
        private Vector3[] spin;
        private Vector3[] baseScale;
        private float[] age;
        private int next;

        /// <summary>Cria os pedaços (só uma vez).</summary>
        public void Init(Material material)
        {
            if (shards != null) return;

            int count = Mathf.Max(1, poolSize);
            shards = new Transform[count];
            velocity = new Vector3[count];
            spin = new Vector3[count];
            baseScale = new Vector3[count];
            age = new float[count];

            for (int i = 0; i < count; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = $"Pedaco_{i + 1}";
                Destroy(shard.GetComponent<Collider>());
                Renderer shardRenderer = shard.GetComponent<Renderer>();
                if (material != null) shardRenderer.sharedMaterial = material;
                shardRenderer.shadowCastingMode = ShadowCastingMode.Off;
                shard.transform.SetParent(transform, false);
                shard.SetActive(false);
                shards[i] = shard.transform;
            }

            enabled = false;
        }

        /// <summary>Solta pedaços a partir dos blocos da barreira (chamado quando ela quebra).</summary>
        public void Play(IceWall wall)
        {
            if (shards == null || wall == null) return;

            Transform root = wall.transform;
            for (int s = 0; s < root.childCount; s++)
            {
                Transform segment = root.GetChild(s);
                Vector3 outward = segment.forward;

                for (int k = 0; k < shardsPerSegment; k++)
                {
                    int i = next;
                    next = (next + 1) % shards.Length;

                    // Ponto aleatório na face do bloco.
                    Vector3 local = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(-0.45f, 0.45f), 0f);
                    float side = Random.value < 0.5f ? -1f : 1f;
                    float size = Random.Range(sizeRange.x, sizeRange.y);

                    Transform shard = shards[i];
                    shard.SetPositionAndRotation(segment.TransformPoint(local), Random.rotation);
                    baseScale[i] = new Vector3(size, size * Random.Range(0.6f, 1.4f), size * 0.5f);
                    shard.localScale = baseScale[i];

                    velocity[i] = (outward * (side * Random.Range(0.4f, 1f)) + Random.insideUnitSphere * 0.5f +
                                   Vector3.up * Random.Range(0.2f, 0.9f)) * burstSpeed;
                    spin[i] = Random.insideUnitSphere * 540f;
                    age[i] = 0f;
                    shard.gameObject.SetActive(true);
                }
            }

            enabled = true;
        }

        private void Update()
        {
            if (shards == null) return;

            float dt = Time.deltaTime;
            bool anyFlying = false;

            for (int i = 0; i < shards.Length; i++)
            {
                Transform shard = shards[i];
                if (!shard.gameObject.activeSelf) continue;

                age[i] += dt;
                float t = age[i] / Mathf.Max(0.01f, lifetime);
                if (t >= 1f)
                {
                    shard.gameObject.SetActive(false);
                    continue;
                }

                anyFlying = true;
                velocity[i].y -= gravity * dt;
                shard.position += velocity[i] * dt;
                shard.rotation = Quaternion.Euler(spin[i] * dt) * shard.rotation;
                shard.localScale = baseScale[i] * (1f - t * t); // encolhe no fim
            }

            if (!anyFlying) enabled = false;
        }
    }
}
