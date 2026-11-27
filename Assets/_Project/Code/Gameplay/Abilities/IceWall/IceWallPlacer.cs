using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Coloca barreiras de gelo (tecla 4 + clique).
    /// - Com o gelo na mão, mostra uma PRÉVIA TRANSPARENTE onde a barreira vai aparecer
    ///   (azul = pode colocar, vermelha = não pode).
    /// - Alcance: rangeInSlides x distância do slide (padrão 2 slides).
    /// - Altura 2,3 m (dá para escalar). Vida = meio pente do rifle. Largura e curva no IceWallConfig.
    /// - A barreira é levemente curvada para dentro (lado côncavo virado para quem colocou),
    ///   protegendo também de tiros pelos lados.
    /// - Barreiras ficam num pool (reaproveitadas), no máximo maxActiveWalls ao mesmo tempo.
    /// - Ao quebrar (tiro ou 1 minuto), solta pedaços de gelo (IceShatter).
    /// </summary>
    [DefaultExecutionOrder(200)] // depois da câmera: a prévia usa a posição final da câmera no frame
    public sealed class IceWallPlacer : MonoBehaviour
    {
        [SerializeField] private IceWallConfig config;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Camera aimCamera;

        private Transform preview;
        private Renderer[] previewRenderers;
        private bool previewShowsValid = true;

        private readonly List<IceWall> activeWalls = new List<IceWall>();
        private readonly Stack<IceWall> pool = new Stack<IceWall>();
        private IceShatter shatter;

        private bool hasPlacement;
        private bool placementValid;
        private Vector3 placementPosition;
        private Quaternion placementRotation = Quaternion.identity;
        private float placementDistance;
        private float nextPlaceTime;
        private int wallLayer;

        /// <summary>Barreira colocada (posição no chão). Para sons/efeitos.</summary>
        public event Action<Vector3> WallPlaced;

        /// <summary>Barreira quebrou (tiro ou tempo). Posição no meio da barreira.</summary>
        public event Action<Vector3> WallBroken;

        /// <summary>Ligado pelo PlayerWeapons quando o gelo está na mão.</summary>
        public bool PreviewActive { get; set; }

        public bool HasValidPlacement => hasPlacement && placementValid;
        public float PlacementDistance => placementDistance;
        public int ActiveCount => activeWalls.Count;
        public IceWallConfig Config => config;
        public float CooldownRemaining => Mathf.Max(0f, nextPlaceTime - Time.time);
        /// <summary>Segundos até a barreira mais antiga quebrar sozinha (0 = nenhuma).</summary>
        public float OldestWallTimeLeft => activeWalls.Count > 0 ? activeWalls[0].TimeLeft : 0f;

        /// <summary>Alcance máximo em metros (2 slides por padrão).</summary>
        public float MaxRange => config != null && motor != null && motor.Config != null
            ? config.rangeInSlides * motor.Config.slideDistance
            : 10f;

        /// <summary>Altura da barreira (do IceWallConfig).</summary>
        public float WallHeight => config != null ? Mathf.Max(0.5f, config.height) : 2.3f;

        private void Awake()
        {
            if (aimCamera == null) aimCamera = Camera.main;

            // Layer própria: dá para escalar, mas não para correr na parede.
            wallLayer = LayerMask.NameToLayer("IceWall");
            if (wallLayer < 0) wallLayer = LayerMask.NameToLayer("NonClimbable");
            if (wallLayer < 0) wallLayer = 0;

            if (config != null && motor != null)
            {
                GameObject previewObject = BuildWall("IceWall_Previa", true);
                preview = previewObject.transform;
                previewRenderers = previewObject.GetComponentsInChildren<Renderer>(true);
                previewObject.SetActive(false);

                var shatterObject = new GameObject("IceWall_Pedacos");
                shatter = shatterObject.AddComponent<IceShatter>();
                shatter.Init(config.wallMaterial);
            }
        }

        private void LateUpdate()
        {
            if (preview == null) return;

            if (!PreviewActive)
            {
                hasPlacement = false;
                if (preview.gameObject.activeSelf) preview.gameObject.SetActive(false);
                return;
            }

            hasPlacement = ComputePlacement(out placementPosition, out placementRotation, out placementValid, out placementDistance);

            preview.gameObject.SetActive(hasPlacement);
            if (!hasPlacement) return;

            preview.SetPositionAndRotation(placementPosition, placementRotation);
            bool showValid = placementValid && CooldownRemaining <= 0f;
            if (showValid != previewShowsValid)
            {
                Material material = showValid ? config.previewMaterial : config.invalidPreviewMaterial;
                foreach (Renderer r in previewRenderers) r.sharedMaterial = material;
                previewShowsValid = showValid;
            }
        }

        /// <summary>
        /// Coloca a barreira onde a mira aponta. O lugar é calculado AGORA (não espera a prévia do frame):
        /// apertar 4 + clique juntos já coloca. Retorna false se não deu.
        /// </summary>
        public bool TryPlace()
        {
            if (config == null || Time.time < nextPlaceTime) return false;

            hasPlacement = ComputePlacement(out placementPosition, out placementRotation, out placementValid,
                out placementDistance);
            if (!HasValidPlacement) return false;

            IceWall wall = GetWall();
            wall.transform.SetPositionAndRotation(placementPosition, placementRotation);
            Color color = config.wallMaterial != null ? config.wallMaterial.color : new Color(0.6f, 0.9f, 1f);
            wall.Activate(config.MaxHealth, config.lifetime, color);
            activeWalls.Add(wall);
            WallPlaced?.Invoke(placementPosition);

            // Passou do máximo: a mais antiga quebra.
            while (activeWalls.Count > Mathf.Max(1, config.maxActiveWalls))
                activeWalls[0].Break();

            nextPlaceTime = Time.time + config.cooldown;
            return true;
        }

        /// <summary>
        /// Onde a barreira vai: segue a mira, limitada entre minDistance e o alcance máximo,
        /// sempre apoiada no chão e virada (lado côncavo) para o jogador.
        /// </summary>
        private bool ComputePlacement(out Vector3 position, out Quaternion rotation, out bool valid, out float distance)
        {
            position = default;
            rotation = Quaternion.identity;
            valid = false;
            distance = 0f;
            if (aimCamera == null || motor == null || motor.Config == null) return false;

            int mask = motor.Config.environmentMask;
            Vector3 player = motor.transform.position;

            // 1) Para onde a mira aponta.
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float skip = Mathf.Max(0f, Vector3.Dot(player + Vector3.up * 1.2f - ray.origin, ray.direction));
            Vector3 start = ray.origin + ray.direction * skip;
            Vector3 target = Physics.Raycast(start, ray.direction, out RaycastHit aimHit, 100f, mask, QueryTriggerInteraction.Ignore)
                ? aimHit.point
                : start + ray.direction * 100f;

            // 2) Distância no plano do chão, limitada ao alcance.
            Vector3 flat = target - player;
            flat.y = 0f;
            Vector3 direction;
            if (flat.sqrMagnitude > 0.0001f) direction = flat.normalized;
            else
            {
                direction = new Vector3(ray.direction.x, 0f, ray.direction.z);
                if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
                direction.Normalize();
            }

            distance = Mathf.Clamp(flat.magnitude, config.minDistance, MaxRange);
            Vector3 probe = player + direction * distance + Vector3.up * (WallHeight + 1f);

            // Lado côncavo (frente da barreira) virado para o jogador.
            rotation = Quaternion.LookRotation(-direction, Vector3.up);

            // 3) Chão embaixo do ponto.
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit ground, WallHeight + 6f, mask, QueryTriggerInteraction.Ignore))
            {
                position = player + direction * distance;
                return true; // mostra a prévia vermelha
            }

            position = ground.point;
            valid = Vector3.Angle(ground.normal, Vector3.up) <= config.maxGroundSlope;
            return true;
        }

        private IceWall GetWall()
        {
            if (pool.Count > 0) return pool.Pop();

            GameObject wallObject = BuildWall("IceWall", false);
            IceWall wall = wallObject.AddComponent<IceWall>();
            wall.Broken += OnWallBroken;
            return wall;
        }

        private void OnWallBroken(IceWall wall)
        {
            activeWalls.Remove(wall);
            pool.Push(wall);
            if (shatter != null) shatter.Play(wall);
            WallBroken?.Invoke(wall.transform.position + Vector3.up * (WallHeight * 0.5f));
        }

        /// <summary>
        /// Monta a barreira com blocos em arco. A raiz fica no chão, no centro da barreira,
        /// com a "frente" (Z) virada para o jogador; as pontas do arco vêm para frente (côncava).
        /// </summary>
        private GameObject BuildWall(string objectName, bool isPreview)
        {
            var root = new GameObject(objectName);

            float height = WallHeight;
            float width = Mathf.Max(0.5f, config.width);
            int count = Mathf.Max(1, config.segments);
            float arc = config.curveDegrees * Mathf.Deg2Rad;
            bool straight = arc < 0.001f;
            float radius = straight ? 0f : width / arc;
            float segmentWidth = straight ? width / count : 2f * radius * Mathf.Sin(arc / (2f * count));

            for (int i = 0; i < count; i++)
            {
                Vector3 localPosition;
                float yawDegrees;

                if (straight)
                {
                    localPosition = new Vector3(-width * 0.5f + (i + 0.5f) * segmentWidth, 0f, 0f);
                    yawDegrees = 0f;
                }
                else
                {
                    float angle = -arc * 0.5f + (i + 0.5f) * arc / count;
                    localPosition = new Vector3(radius * Mathf.Sin(angle), 0f, radius - radius * Mathf.Cos(angle));
                    yawDegrees = -angle * Mathf.Rad2Deg; // cada bloco virado para o centro da curva
                }

                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = $"Bloco_{i + 1}";
                segment.transform.SetParent(root.transform, false);
                segment.transform.localPosition = localPosition + Vector3.up * (height * 0.5f);
                segment.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
                segment.transform.localScale = new Vector3(segmentWidth * 1.04f, height, config.thickness);

                Renderer segmentRenderer = segment.GetComponent<Renderer>();
                if (isPreview)
                {
                    Destroy(segment.GetComponent<Collider>()); // prévia não bloqueia nada
                    segmentRenderer.sharedMaterial = config.previewMaterial;
                    segmentRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                else
                {
                    segment.layer = wallLayer;
                    segmentRenderer.sharedMaterial = config.wallMaterial;
                }
            }

            return root;
        }
    }
}
