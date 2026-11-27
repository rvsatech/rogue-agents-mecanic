using UnityEngine;
using UnityEngine.Rendering;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Com granada, flash ou smoke na mão: desenha o caminho do arremesso e, onde ela bate primeiro, o círculo do
    /// efeito (granada e smoke: 2,2 m de raio; flash: só um ponto). Segurando o botão direito mostra o arremesso curto.
    /// Usa a mesma conta do arremesso de verdade (PlayerWeapons.GetThrowStart). Não mostra os quiques.
    /// </summary>
    [DefaultExecutionOrder(200)] // depois da câmera: o caminho usa a mira da tela
    public sealed class ThrowPreview : MonoBehaviour
    {
        [SerializeField] private PlayerWeapons weapons;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float stepTime = 0.03f;
        [SerializeField] private int maxSteps = 100;
        [SerializeField] private int circleSegments = 40;

        private LineRenderer arc;
        private LineRenderer circle;

        private void Awake()
        {
            if (weapons == null) weapons = GetComponent<PlayerWeapons>();
            arc = CreateLine("Arremesso_Caminho", 0.025f);
            circle = CreateLine("Arremesso_Area", 0.045f);
            circle.loop = true;
            circle.positionCount = circleSegments;
        }

        private LineRenderer CreateLine(string lineName, float width)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = width;
            line.endWidth = width;
            line.sharedMaterial = lineMaterial;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private void LateUpdate()
        {
            WeaponConfig cfg = weapons != null ? weapons.CurrentConfig : null;
            if (cfg == null || cfg.kind != WeaponKind.Throwable || !weapons.ThrowPreviewActive ||
                !weapons.GetThrowStart(cfg, weapons.ShortThrowHeld, out Vector3 position, out Vector3 velocity))
            {
                arc.enabled = false;
                circle.enabled = false;
                return;
            }

            // Caminho até o primeiro lugar em que bate.
            Vector3 gravity = Physics.gravity;
            arc.positionCount = maxSteps + 1;
            arc.SetPosition(0, position);
            int count = 1;
            bool landed = false;
            RaycastHit landing = default;
            for (int i = 0; i < maxSteps; i++)
            {
                Vector3 next = position + velocity * stepTime + gravity * (0.5f * stepTime * stepTime);
                velocity += gravity * stepTime;
                Vector3 delta = next - position;
                float length = delta.magnitude;
                if (length > 0.0001f && Physics.Raycast(position, delta / length, out landing, length, cfg.hitMask,
                        QueryTriggerInteraction.Ignore))
                {
                    arc.SetPosition(count++, landing.point);
                    landed = true;
                    break;
                }
                arc.SetPosition(count++, next);
                position = next;
            }
            arc.positionCount = count;

            Color color = ColorOf(cfg);
            arc.startColor = new Color(color.r, color.g, color.b, 0.15f);
            arc.endColor = new Color(color.r, color.g, color.b, 0.85f);
            arc.enabled = true;

            if (!landed)
            {
                circle.enabled = false;
                return;
            }

            // Círculo do efeito deitado na superfície onde bateu.
            float radius = cfg.throwableType == ThrowableType.Flash ? 0.35f : cfg.effectRadius;
            Vector3 normal = landing.normal;
            Vector3 tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            Vector3 center = landing.point + normal * 0.03f;
            circle.positionCount = circleSegments;
            for (int i = 0; i < circleSegments; i++)
            {
                float angle = i / (float)circleSegments * Mathf.PI * 2f;
                circle.SetPosition(i, center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius);
            }
            circle.startColor = new Color(color.r, color.g, color.b, 0.9f);
            circle.endColor = circle.startColor;
            circle.enabled = true;
        }

        private static Color ColorOf(WeaponConfig cfg)
        {
            switch (cfg.throwableType)
            {
                case ThrowableType.Frag: return new Color(1f, 0.45f, 0.2f);
                case ThrowableType.Flash: return new Color(1f, 1f, 0.8f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }
}
