using UnityEngine;

namespace Rogue.Gameplay
{
    /// <summary>
    /// Granada, flash ou smoke voando: física de verdade (quica no chão e nas paredes) e estoura depois de fuseTime.
    /// O que acontece no estouro fica no GrenadeEffects.
    /// Fica na camada do jogador: os tiros e a câmera não batem nela; não colide com quem jogou.
    /// </summary>
    public sealed class ThrownGrenade : MonoBehaviour
    {
        private static PhysicsMaterial bounceMaterial;

        private WeaponConfig config;
        private PlayerWeapons owner;
        private float fuse;
        private float lastBounceSoundTime = -999f;

        public void Launch(WeaponConfig cfg, PlayerWeapons thrower, Vector3 velocity, Collider ignore)
        {
            config = cfg;
            owner = thrower;
            fuse = Mathf.Max(0.05f, cfg.fuseTime);

            if (bounceMaterial == null)
            {
                bounceMaterial = new PhysicsMaterial("Granada")
                {
                    bounciness = 0.35f,
                    dynamicFriction = 0.5f,
                    staticFriction = 0.6f,
                    bounceCombine = PhysicsMaterialCombine.Average,
                    frictionCombine = PhysicsMaterialCombine.Average
                };
            }

            var sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.06f;
            sphere.sharedMaterial = bounceMaterial;
            if (ignore != null) Physics.IgnoreCollision(sphere, ignore);

            var body = gameObject.AddComponent<Rigidbody>();
            body.mass = 0.4f;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // não atravessa parede fina
            body.linearVelocity = velocity;
            body.angularVelocity = Random.insideUnitSphere * 8f;
        }

        private void Update()
        {
            if (config == null) return;

            fuse -= Time.deltaTime;
            if (fuse > 0f) return;

            Vector3 position = transform.position;
            switch (config.throwableType)
            {
                case ThrowableType.Frag: GrenadeEffects.Explode(position, config, owner); break;
                case ThrowableType.Flash: GrenadeEffects.FlashBang(position, config); break;
                case ThrowableType.Smoke: GrenadeEffects.Smoke(position, config); break;
            }
            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (config == null) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < 1.2f || Time.time - lastBounceSoundTime < 0.08f) return;

            lastBounceSoundTime = Time.time;
            GrenadeEffects.PlaySound(config.bounceSound, transform.position, Mathf.Clamp01(speed / 10f));
        }
    }
}
