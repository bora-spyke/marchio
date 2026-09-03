using UnityEngine;

namespace Marchio
{
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        ParticleSystem[] particles;
        bool fading;

        public Vector2 Pos { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float Radius { get; private set; }
        public float Damage { get; private set; }
        public int Bounces { get; private set; }
        public Enemy LastHit { get; private set; }
        public float Age { get; private set; }
        public bool Expired => Age >= GameManager.I.Config.projectileLifeS;

        void Awake()
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void OnSpawn() { }
        public void OnDespawn() { }

        public void Init(Vector2 pos, Vector2 velocity, float radius, float damage)
        {
            Pos = pos;
            Velocity = velocity;
            Radius = radius;
            Damage = damage;
            Bounces = 0;
            LastHit = null;
            Age = 0f;
            fading = false;
            transform.localScale = Vector3.one;
            for (int i = 0; i < particles.Length; i++) particles[i].Play(false);
            Apply();
        }

        public void SetBounces(int bounces) => Bounces = bounces;

        public void Redirect(Enemy hit, Vector2 towards)
        {
            LastHit = hit;
            Bounces--;
            var d = towards - Pos;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
            Velocity = d.normalized * Velocity.magnitude;
        }

        public void Advance(float dt)
        {
            Pos += Velocity * dt;
            Age += dt;
            TickFade();
            Apply();
        }

        void TickFade()
        {
            var cfg = GameManager.I.Config;
            float left = cfg.projectileLifeS - Age;
            if (left > cfg.projectileFadeS) return;
            if (!fading)
            {
                fading = true;
                for (int i = 0; i < particles.Length; i++) particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            transform.localScale = Vector3.one * Mathf.Clamp01(left / cfg.projectileFadeS);
        }

        void Apply()
        {
            transform.position = PolygonMath.ToWorld(Pos, 2f);
        }
    }
}
