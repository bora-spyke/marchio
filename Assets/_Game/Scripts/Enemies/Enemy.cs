using UnityEngine;

namespace Marchio
{
    public sealed class Enemy : MonoBehaviour, IPoolable
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] ParticleSystem hitParticle;
        [SerializeField] ParticleSystem deathParticle;

        const float DeathFxTimeoutS = 4f;

        public EnemyTypeSO Type { get; private set; }
        public Vector2 Pos { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Radius { get; private set; }
        public float Speed { get; private set; }
        public float ContactDamage { get; private set; }
        public bool Dead { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool IgnoresBarriers => Type.ignoresBarriers;

        float steerJitter;
        float speedMult;
        float preferredDistJitter;
        float fireTimer;
        float retargetTimer;
        Vector2 heading;
        Vector2 facing;
        float slowLeft;
        float burnDps;
        float burnLeft;
        float deathTimer;

        GameManager Gm => GameManager.I;
        GameConfig Cfg => GameManager.I.Config;

        public void OnSpawn() { }
        public void OnDespawn() { }

        void Awake()
        {
            if (visualRoot == null) visualRoot = transform.Find("Visual") ?? (transform.childCount > 0 ? transform.GetChild(0) : null);
            if (hitParticle == null) hitParticle = FindParticleByName("Hit");
            if (deathParticle == null) deathParticle = FindParticleByName("Death");
        }

        ParticleSystem FindParticleByName(string contains)
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) if (ps.name.Contains(contains)) return ps;
            return null;
        }

        public void Init(EnemyTypeSO type, Vector2 pos, float hpMult)
        {
            var cfg = Cfg;
            Type = type;
            Pos = pos;
            Dead = false;
            Velocity = Vector2.zero;
            slowLeft = 0f;
            burnDps = 0f;
            burnLeft = 0f;
            deathTimer = 0f;
            Hp = type.hp * hpMult;
            MaxHp = Hp;
            Radius = type.radius;
            Speed = type.speed;
            ContactDamage = type.contactDamage;
            steerJitter = Random.Range(-cfg.enemySteerJitterRad, cfg.enemySteerJitterRad);
            speedMult = Random.Range(1f - cfg.enemySpeedVariance, 1f + cfg.enemySpeedVariance);
            preferredDistJitter = Random.Range(-type.preferredDistJitter, type.preferredDistJitter);
            fireTimer = Random.Range(type.initialFireDelayMs.x, type.initialFireDelayMs.y);
            retargetTimer = 0f;
            heading = Vector2.zero;
            facing = (Gm.Player.Pos - pos).normalized;
            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
            if (hitParticle != null) hitParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (deathParticle != null) deathParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyTransform();
        }

        public void SetPos(Vector2 p)
        {
            Pos = p;
        }

        public void Tick(float dt)
        {
            var before = Pos;
            if (burnLeft > 0f)
            {
                Hp -= burnDps * dt;
                burnLeft -= dt;
                if (Hp <= 0f) { Kill(); return; }
            }
            float slowFactor = 1f;
            if (slowLeft > 0f)
            {
                slowFactor = Cfg.freezeSlowFactor;
                slowLeft -= dt;
            }
            Behave(dt, slowFactor);
            Velocity = (Pos - before) / dt;
            ApplyTransform(dt);
        }

        public bool TickDead(float dt)
        {
            deathTimer += dt;
            if (deathTimer >= DeathFxTimeoutS) return true;
            return deathParticle == null || !deathParticle.IsAlive(true);
        }

        void Behave(float dt, float slowFactor)
        {
            float d = Vector2.Distance(Gm.Player.Pos, Pos);
            retargetTimer -= dt;
            if (retargetTimer <= 0f)
            {
                retargetTimer = Type.retargetS;
                heading = ComputeHeading(d);
            }
            Pos += heading * Speed * speedMult * slowFactor * dt;
            TickFire(dt, d, Type.behavior != EnemyBehavior.KeepDistance);
        }

        Vector2 ComputeHeading(float distToPlayer)
        {
            var type = Type;
            var toPlayer = Gm.Player.Pos - Pos;
            float d = distToPlayer < 1e-6f ? 1f : distToPlayer;
            float cosj = Mathf.Cos(steerJitter), sinj = Mathf.Sin(steerJitter);
            var steered = new Vector2(toPlayer.x * cosj - toPlayer.y * sinj, toPlayer.x * sinj + toPlayer.y * cosj) / d;
            facing = toPlayer / d;
            if (type.behavior != EnemyBehavior.KeepDistance) return steered;
            float preferred = type.preferredDist + preferredDistJitter;
            float dir = d > preferred ? 1f : d < preferred * type.retreatFraction ? -1f : 0f;
            return steered * dir;
        }

        void TickFire(float dt, float distToPlayer, bool respectMinDist)
        {
            var type = Type;
            if (!type.fires) return;
            fireTimer -= dt * 1000f;
            if (fireTimer > 0f) return;
            fireTimer = type.fireIntervalMs;
            if (!respectMinDist || distToPlayer > type.fireMinDist) Fire(type.projectileSpeed, type.projectileDamage);
        }

        void Fire(float speed, float damage)
        {
            var dir = Gm.Player.Pos - Pos;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();
            Gm.EnemyProjectilesFor(Type.projectilePrefab).Get().Init(Pos, dir * speed, Cfg.enemyProjectileRadius, damage);
        }

        public bool ApplyProjectileHit(float dmg) => ApplyDamage(dmg, true);

        public bool ApplyDamage(float dmg, bool showFx)
        {
            if (Dead) return false;
            Hp -= dmg;
            if (showFx)
            {
                Gm.ShowDamage(Pos, dmg);
                if (hitParticle != null) hitParticle.Play(true);
            }
            if (Hp <= 0f) { Kill(); return true; }
            return false;
        }

        public bool ApplyLoopHit(float dmg)
        {
            var up = Gm.Upgrades;
            int burn = up.Level(UpgradeId.BurningFill);
            if (burn > 0) ApplyBurn(Cfg.burnDpsPerLevel * burn, Cfg.burnDurationS);
            if (up.Level(UpgradeId.FreezeFill) > 0) slowLeft = Cfg.freezeDurationS;
            return ApplyDamage(dmg, true);
        }

        public void ApplyBurn(float dps, float duration)
        {
            burnDps = Mathf.Max(burnDps, dps);
            burnLeft = Mathf.Max(burnLeft, duration);
        }

        public void ApplyBarrierDamage(float dmg) => ApplyDamage(dmg, false);

        public void Kill()
        {
            if (Dead) return;
            Dead = true;
            deathTimer = 0f;
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            if (hitParticle != null) hitParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (deathParticle != null) deathParticle.Play(true);
            Gm.OnEnemyKilled(this);
        }

        // dt < 0 snaps; facing only changes on retarget, so the visual no longer tracks the player every frame
        void ApplyTransform(float dt = -1f)
        {
            transform.position = PolygonMath.ToWorld(Pos);
            if (visualRoot == null || facing.sqrMagnitude < 1e-6f) return;
            var target = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.y), Vector3.up);
            visualRoot.rotation = dt < 0f ? target : Quaternion.RotateTowards(visualRoot.rotation, target, Type.turnDegPerS * dt);
        }
    }
}
