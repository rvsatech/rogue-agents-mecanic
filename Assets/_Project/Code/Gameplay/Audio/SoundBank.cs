using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Sons gerais (movimento, voz, impactos, gelo). Os sons de cada arma ficam no próprio WeaponConfig
    /// (arma nova = arquivo novo com os sons dela).
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [Header("Geral")]
        [Range(0f, 1f)] public float masterVolume = 1f;

        [Tooltip("Sons do próprio jogador: quanto são 3D (0 = 2D, claro e centralizado).")]
        [Range(0f, 1f)] public float localPlayerSpatialBlend = 0.2f;

        [Header("Movimento")]
        public SoundCue footstep = new SoundCue();
        public SoundCue jump = new SoundCue();
        public SoundCue land = new SoundCue();
        public SoundCue slideStart = new SoundCue();
        [Tooltip("Som contínuo enquanto desliza (toca em loop).")]
        public SoundCue slideLoop = new SoundCue();
        public SoundCue roll = new SoundCue();
        [Tooltip("Roupa: agachar e rastejar.")]
        public SoundCue cloth = new SoundCue();
        public SoundCue proneDown = new SoundCue();
        public SoundCue proneUp = new SoundCue();
        public SoundCue ledgeGrab = new SoundCue();
        public SoundCue wallRunStart = new SoundCue();

        [Header("Voz (esforço)")]
        [Tooltip("Subindo a beirada.")]
        public SoundCue voiceEffort = new SoundCue();
        [Tooltip("Agarrar, pulo da parede, levantar.")]
        public SoundCue voiceShort = new SoundCue();

        [Header("Acertos")]
        public SoundCue bulletImpact = new SoundCue();
        public SoundCue bulletHitTarget = new SoundCue();
        [Tooltip("'Tic' de interface ao acertar um alvo.")]
        public SoundCue hitMarker = new SoundCue();
        [Tooltip("'Tic' mais agudo ao acertar a cabeça.")]
        public SoundCue headshotMarker = new SoundCue();
        [Tooltip("Confirmação de abate.")]
        public SoundCue killConfirm = new SoundCue();

        [Header("Barreira de gelo")]
        public SoundCue iceCreate = new SoundCue();
        public SoundCue iceImpact = new SoundCue();
        public SoundCue iceBreak = new SoundCue();
    }
}
