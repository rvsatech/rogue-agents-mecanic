using System.Collections.Generic;
using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// O que cada coisa jogada faz ao estourar.
    /// - Granada: dano em círculo (raio effectRadius = 4,4 m: círculo de 8,8 m). Mais perto do centro = mais dano
    ///   (maxDamage no centro, minDamage na borda). Parede ou barreira entre a granada e o alvo protege.
    ///   Pega em todo mundo, inclusive em quem jogou (a barreira de gelo também leva dano).
    /// - Flash: deixa a tela branca e o jogo surdo para quem vê o estouro. Até flashFullBlindDistance (12 m): tela toda
    ///   branca por ~4 s; mais longe vai enfraquecendo até flashMaxDistance (40 m). Parede no meio = não cega.
    /// - Smoke: nuvem cilíndrica no chão (raio effectRadius, altura smokeHeight) que dura smokeDuration.
    /// </summary>
    public static class GrenadeEffects
    {
        private static readonly Collider[] OverlapBuffer = new Collider[64];
        private static readonly Dictionary<IDamageable, (float distance, Collider collider)> Targets =
            new Dictionary<IDamageable, (float, Collider)>();

        public static void Explode(Vector3 center, WeaponConfig cfg, PlayerWeapons owner)
        {
            GrenadeVfx.Explosion(center, cfg);
            PlaySound(cfg.detonateSound, center);

            float radius = Mathf.Max(0.1f, cfg.effectRadius);
            int count = Physics.OverlapSphereNonAlloc(center, radius, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

            // Cada alvo leva dano uma vez só (um bot tem vários colisores), pela parte mais perto do centro.
            Targets.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider collider = OverlapBuffer[i];
                IDamageable target = collider.GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                float distance = Vector3.Distance(center, collider.ClosestPoint(center));
                if (!Targets.TryGetValue(target, out var best) || distance < best.distance)
                    Targets[target] = (distance, collider);
            }

            Vector3 from = center + Vector3.up * 0.15f;
            foreach (KeyValuePair<IDamageable, (float distance, Collider collider)> pair in Targets)
            {
                IDamageable target = pair.Key;
                Collider collider = pair.Value.collider;
                if (!HasLineOfSight(from, collider, target, cfg.hitMask)) continue; // parede/barreira na frente protege

                float damage = Mathf.Lerp(cfg.maxDamage, cfg.minDamage, Mathf.Clamp01(pair.Value.distance / radius));
                target.TakeDamage(damage, HitZoneKind.Body);

                // Acertou outro (bot, barreira): número de dano e hitmarker como um tiro. Em si mesmo: só a tela vermelha.
                if (owner != null && !ReferenceEquals(target, owner.Health))
                {
                    owner.ReportHit(new WeaponHitInfo
                    {
                        Weapon = cfg,
                        Point = collider.bounds.center,
                        Surface = target.Surface,
                        Zone = HitZoneKind.Body,
                        Damage = damage,
                        Target = target as Component,
                        Killed = !target.IsAlive
                    });
                }
            }
            Targets.Clear();
        }

        public static void FlashBang(Vector3 position, WeaponConfig cfg)
        {
            GrenadeVfx.Flash(position, cfg);
            PlaySound(cfg.detonateSound, position);

            Camera view = Camera.main;
            if (view == null) return;

            Vector3 eye = view.transform.position;
            float distance = Vector3.Distance(position, eye);
            float maxDistance = Mathf.Max(0.1f, cfg.flashMaxDistance);
            if (distance >= maxDistance) return;

            // Parede no meio: não cega.
            Vector3 from = position + Vector3.up * 0.1f;
            Vector3 delta = eye - from;
            if (Physics.Raycast(from, delta.normalized, delta.magnitude, cfg.hitMask, QueryTriggerInteraction.Ignore))
                return;

            // Perto = tudo branco; depois enfraquece até o alcance máximo.
            float full = Mathf.Clamp(cfg.flashFullBlindDistance, 0f, maxDistance - 0.1f);
            float strength = distance <= full ? 1f : 1f - (distance - full) / (maxDistance - full);
            ScreenEffects.Flash(strength, cfg.flashMaxBlindTime * strength, cfg.flashFadeTime * (0.5f + 0.5f * strength));
            FlashOverlay.PlayRing(cfg.ringSound, strength);
        }

        public static void Smoke(Vector3 position, WeaponConfig cfg)
        {
            Vector3 ground = position;
            if (Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 4f, cfg.hitMask,
                    QueryTriggerInteraction.Ignore))
                ground = hit.point;

            SmokeCloud.Spawn(ground, cfg);
            PlaySound(cfg.detonateSound, ground);
        }

        public static void PlaySound(SoundCue cue, Vector3 position, float volume = 1f)
        {
            AudioService audio = AudioService.Instance;
            if (cue != null && audio != null) audio.Play(cue, position, volume);
        }

        private static bool HasLineOfSight(Vector3 from, Collider targetCollider, IDamageable target, int mask)
        {
            Vector3 delta = targetCollider.bounds.center - from;
            float distance = delta.magnitude;
            if (distance < 0.05f) return true;
            if (!Physics.Raycast(from, delta / distance, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
                return true;
            return hit.collider.GetComponentInParent<IDamageable>() == target; // bateu no próprio alvo
        }
    }
}
