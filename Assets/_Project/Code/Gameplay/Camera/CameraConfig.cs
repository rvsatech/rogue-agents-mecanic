using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Ajustes da câmera em 3ª pessoa (editáveis no Inspector com o jogo rodando — o valor fica salvo).
    /// O que decide o "tamanho" de tudo na tela é o campo de visão (FOV): menor = tudo maior (zoom),
    /// igual para o personagem e para o mundo. A distância aproxima só o personagem.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Config/Camera", fileName = "CameraConfig")]
    public sealed class CameraConfig : ScriptableObject
    {
        [Header("Normal")]
        [Tooltip("Campo de visão vertical em graus. MENOR = tudo maior na tela; maior = vê mais dos lados.")]
        [Range(40f, 110f)] public float fieldOfView = 60f;

        [Tooltip("Distância da câmera atrás do personagem, em metros.")]
        public float distance = 3.2f;

        [Tooltip("Deslocamento para a direita (câmera de ombro).")]
        public float shoulderOffset = 0.65f;

        [Header("Mirando (botão direito)")]
        [Tooltip("Campo de visão mirando (botão direito). Menor que o normal = zoom.")]
        [Range(30f, 100f)] public float aimFieldOfView = 44f;
        public float aimDistance = 1.45f;
        public float aimShoulderOffset = 0.62f;

        [Tooltip("Quão rápido entra/sai da mira.")]
        public float aimLerpSpeed = 10f;

        [Header("Luneta (AWP)")]
        [Tooltip("Quão rápido entra/sai da luneta (o zoom de cada arma fica no WeaponConfig).")]
        public float scopeLerpSpeed = 12f;

        [Header("Sensibilidade do mouse (em relação à normal)")]
        [Tooltip("Mirando com rifle/pistola (botão direito). 0,6 = 40% mais lenta.")]
        [Range(0.1f, 1f)] public float aimSensitivity = 0.6f;

        [Tooltip("Na luneta o mouse fica mais lento na proporção do zoom; isto multiplica em cima " +
                 "(1 = só o zoom, menor = ainda mais lenta).")]
        [Range(0.1f, 2f)] public float scopeSensitivity = 1f;

        [Header("Altura do ponto de giro")]
        [Tooltip("Altura do ponto que a câmera segue, em pé (metros a partir do pé).")]
        public float standPivotHeight = 1.6f;

        [Tooltip("Altura do ponto que a câmera segue, agachado. Perto da altura em pé = a câmera quase não desce.")]
        public float crouchPivotHeight = 1.3f;

        [Tooltip("Altura do ponto que a câmera segue, deitado.")]
        public float pronePivotHeight = 0.75f;

        [Tooltip("Velocidade (m/s) com que a câmera sobe/desce ao agachar e levantar.")]
        public float pivotHeightSpeed = 2.5f;

        [Header("Sensação de movimento")]
        [Tooltip("FOV a mais correndo acima da velocidade de corrida (graus, cheio a 1,5x a corrida).")]
        public float speedFovBoost = 5f;

        [Tooltip("FOV a mais deslizando (graus).")]
        public float slideFovBoost = 7f;

        [Tooltip("FOV a mais correndo na parede (graus).")]
        public float wallRunFovBoost = 5f;

        [Tooltip("Quão rápido o FOV extra entra e sai.")]
        public float fovBoostSpeed = 6f;

        [Tooltip("Correndo na parede: a câmera inclina para longe da parede (graus).")]
        public float wallRunTilt = 6f;

        [Tooltip("Pouso: quanto a câmera afunda numa queda grande (metros).")]
        public float landDip = 0.22f;

        [Tooltip("Queda (m/s para baixo) a partir da qual a câmera afunda ao pousar (pulo normal pousa a ~8,4 m/s).")]
        public float landDipMinSpeed = 9f;

        [Tooltip("Queda (m/s) com o afundar máximo.")]
        public float landDipMaxSpeed = 18f;

        [Tooltip("Tranco da câmera a cada tiro (1 = normal, 0 = desliga). Na luneta não treme.")]
        public float shotShake = 1f;

        [Header("Correndo na parede")]
        [Tooltip("Na parede a câmera se afasta esses metros (vê mais em volta), como no Rogue Agents.")]
        public float wallRunDistanceBoost = 0.9f;
        // Mirar na parede (zoom/luneta) é regra de movimento: MovementConfig.wallRunAllowAim.

        [Tooltip("Na parede o ombro da câmera vai para cá (0 = personagem no meio da tela).")]
        public float wallRunShoulderOffset = 0.2f;

        [Header("Tremor do tiro (gira a câmera um pouquinho; só visual)")]
        [Tooltip("Graus por tiro mirando (botão direito).")]
        public float aimShotTremor = 0.55f;

        [Tooltip("Graus por tiro sem mirar.")]
        public float hipShotTremor = 0.2f;

        [Tooltip("Quão rápido o tremor some (maior = some mais rápido).")]
        public float tremorRecovery = 16f;

        [Header("Colisão")]
        [Tooltip("Raio usado para impedir a câmera de atravessar paredes.")]
        public float collisionRadius = 0.2f;

        [Tooltip("Distância mínima da câmera ao ponto de giro quando bate em algo.")]
        public float minDistance = 0.1f;

        [Tooltip("Com o que a câmera colide (tudo menos o Player).")]
        public LayerMask collisionMask = ~0;

        [HideInInspector]
        [Tooltip("Versão dos valores padrão já aplicados (atualiza sozinho ao compilar).")]
        public int defaultsVersion;
    }
}
