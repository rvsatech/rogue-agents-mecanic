using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>Ajustes da câmera em 3ª pessoa (editáveis no Inspector com o jogo rodando).</summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Camera", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Normal")]
        [Tooltip("Campo de visão em graus. Maior = vê mais dos lados, sensação de mais velocidade.")]
        [Range(50f, 110f)] public float fieldOfView = 75f;

        [Tooltip("Distância da câmera atrás do personagem, em metros.")]
        public float distance = 3.2f;

        [Tooltip("Deslocamento para a direita (câmera de ombro).")]
        public float shoulderOffset = 0.65f;

        [Header("Mirando (botão direito)")]
        [Range(40f, 100f)] public float aimFieldOfView = 62f;
        public float aimDistance = 2.3f;
        public float aimShoulderOffset = 0.75f;

        [Tooltip("Quão rápido entra/sai da mira.")]
        public float aimLerpSpeed = 10f;

        [Header("Altura do ponto de giro")]
        [Tooltip("Altura do ponto que a câmera segue, em pé (metros a partir do pé).")]
        public float standPivotHeight = 1.6f;

        [Tooltip("Altura do ponto que a câmera segue, agachado. Perto da altura em pé = a câmera quase não desce.")]
        public float crouchPivotHeight = 1.3f;

        [Tooltip("Altura do ponto que a câmera segue, deitado.")]
        public float pronePivotHeight = 0.75f;

        [Tooltip("Velocidade (m/s) com que a câmera sobe/desce ao agachar e levantar.")]
        public float pivotHeightSpeed = 2.5f;

        [Header("Colisão")]
        [Tooltip("Raio usado para impedir a câmera de atravessar paredes.")]
        public float collisionRadius = 0.2f;

        [Tooltip("Distância mínima da câmera ao ponto de giro quando bate em algo.")]
        public float minDistance = 0.1f;

        [Tooltip("Com o que a câmera colide (tudo menos o Player).")]
        public LayerMask collisionMask = ~0;
    }
}
