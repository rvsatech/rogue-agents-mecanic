using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Ajustes da câmera em 3ª pessoa (editáveis no Inspector com o jogo rodando).</summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Camera", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Tooltip("Campo de visão em graus. Maior = vê mais dos lados, sensação de mais velocidade.")]
        [Range(50f, 110f)] public float fieldOfView = 75f;

        [Tooltip("Distância da câmera atrás do personagem, em metros.")]
        public float distance = 3.2f;

        [Tooltip("Deslocamento para a direita (câmera de ombro).")]
        public float shoulderOffset = 0.65f;

        [Tooltip("Altura do ponto de giro em relação à altura atual do personagem (0.9 = 90%).")]
        [Range(0.5f, 1.2f)] public float pivotHeightFactor = 0.9f;

        [Tooltip("Raio usado para impedir a câmera de atravessar paredes.")]
        public float collisionRadius = 0.2f;

        [Tooltip("Com o que a câmera colide (tudo menos o Player).")]
        public LayerMask collisionMask = ~0;
    }
}
