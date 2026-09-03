using UnityEngine;

namespace Marchio
{
    public class Enemy : MonoBehaviour, IPoolable
    {
        [SerializeField] protected Transform visualRoot;
        [SerializeField] protected Renderer visualRenderer;
        [SerializeField] protected ParticleSystem hitParticle;
        [SerializeField] protected ParticleSystem deathParticle;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock mpb;

        public EnemyTypeSO Type { get; private set; }
        public Vector2 Pos { get; protected set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Radius { get; private set; }
        public float Speed { get; private set; }
        public float ContactDamage { get; protected set; }
        public bool Dead { get; private set; }
        public Vector2 Velocity { get; private set; }
        public bool IgnoresBarriers => Type.ignoresBarriers;

        protected float steerJitter;
        protected float speedMult;
        float preferredDistJitter;
        float fireTimer;
        float retargetTimer;
        Vector2 heading;
        float hitFlash;
        float slowLeft;
        float burnDps;
        float burnLeft;

        protected GameManager Gm => GameManager.I;
        protected GameConfig Cfg => GameManager.I.Config;

        public virtual void OnSpawn() { }
        public virtual void OnDespawn() { }

        void Awake()
        {
            if (visualRoot == null) visualRoot = transform.Find("Visual") ?? (transform.childCount > 0 ? transform.GetChild(0) : null);
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<Renderer>();
            if (hitParticle == null) hitParticle = FindParticleByName("Hit");
            if (deathParticle == null) deathParticle = FindParticleByName("Death");
        }

        ParticleSystem FindParticleByName(string contains)
        {
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems) if (ps.name.Contains(contains)) return ps;
            return null;
        }

        public void Init(EnemyTypeSO type, Vector2 pos, float hpMult)
        {
            var cfg = Cfg;
            Type = type;
            Pos = pos;
            Dead = false;
            Velocity = Vector2.zero;
            hitFlash = 0f;
            slowLeft = 0f;
            burnDps = 0f;
            burnLeft = 0f;
            Hp = type.hp * hpMult;
            MaxHp = Hp;
            Radius = type.radius;
            Speed = type.speed;
            ContactDamage = type.contactDamage;
            bool boss = type.IsBoss;
            steerJitter = boss ? 0f : Random.Range(-cfg.enemySteerJitterRad, cfg.enemySteerJitterRad);
            speedMult = boss ? 1f : Random.Range(1f - cfg.enemySpeedVariance, 1f + cfg.enemySpeedVariance);
            preferredDistJitter = Random.Range(-type.preferredDistJitter, type.preferredDistJitter);
            fireTimer = Random.Range(type.initialFireDelayMs.x, type.initialFireDelayMs.y);
            retargetTimer = 0f;
            heading = Vector2.zero;
            OnInit();
            ApplyTransform();
        }

        protected virtual void OnInit() { }

        public void SetPos(Vector2 p)
        {
            Pos = p;
        }

        public void Tick(float dt)
        {
            var before = Pos;
            if (hitFlash > 0f) hitFlash -= dt;
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
            ApplyTransform();
        }

        protected virtual void Behave(float dt, float slowFactor)
        {
            float d = Vector2.Distance(Gm.Player.Pos, Pos);
            retargetTimer -= dt;
            if (retargetTimer <= 0f)
            {
                retargetTimer = Cfg.enemyRetargetS;
                heading = ComputeHeading(d);
            }
            Pos += heading * Speed * speedMult * slowFactor * dt;
            if (Type.behavior == EnemyBehavior.SeekTrail) return;
            TickFire(dt, d, Type.behavior != EnemyBehavior.KeepDistance);
        }

        Vector2 ComputeHeading(float distToPlayer)
        {
            var type = Type;
            var toPlayer = Gm.Player.Pos - Pos;
            float d = distToPlayer < 1e-6f ? 1f : distToPlayer;
            float cosj = Mathf.Cos(steerJitter), sinj = Mathf.Sin(steerJitter);
            var steered = new Vector2(toPlayer.x * cosj - toPlayer.y * sinj, toPlayer.x * sinj + toPlayer.y * cosj) / d;

            if (type.behavior == EnemyBehavior.KeepDistance)
            {
                float preferred = type.preferredDist + preferredDistJitter;
                float dir = d > preferred ? 1f : d < preferred * type.retreatFraction ? -1f : 0f;
                return steered * dir;
            }

            if (type.behavior == EnemyBehavior.SeekTrail && Gm.Trail.TryNearestPoint(Pos, out var trailPoint))
            {
                var toTrail = trailPoint - Pos;
                return toTrail.sqrMagnitude > 1e-6f ? toTrail.normalized : steered;
            }

            return steered;
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

        protected void Fire(float speed, float damage)
        {
            var dir = Gm.Player.Pos - Pos;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();
            var p = Gm.EnemyProjectiles.Get();
            p.Init(Pos, dir * speed, Cfg.enemyProjectileRadius, damage, false, false, 0f, Cfg.enemyProjectile);
        }

        public bool ApplyProjectileHit(float dmg)
        {
            if (hitParticle != null) hitParticle.Play();
            return ApplyDamage(dmg, 0.1f, true);
        }

        public bool ApplyDamage(float dmg, float flash, bool showText)
        {
            Hp -= dmg;
            hitFlash = Mathf.Max(hitFlash, flash);
            if (showText) Gm.ShowDamage(Pos, dmg);
            if (Hp <= 0f && !Dead) { Kill(); return true; }
            return false;
        }

        public bool ApplyLoopHit(float dmg)
        {
            var up = Gm.Upgrades;
            int burn = up.Level(UpgradeId.BurningFill);
            if (burn > 0) ApplyBurn(Cfg.burnDpsPerLevel * burn, Cfg.burnDurationS);
            if (up.Level(UpgradeId.FreezeFill) > 0) slowLeft = Cfg.freezeDurationS;
            return ApplyDamage(dmg, 0.15f, true);
        }

        public void ApplyBurn(float dps, float duration)
        {
            burnDps = Mathf.Max(burnDps, dps);
            burnLeft = Mathf.Max(burnLeft, duration);
        }

        public void ApplyBarrierDamage(float dmg)
        {
            ApplyDamage(dmg, 0.08f, false);
        }

        public void Kill()
        {
            if (Dead) return;
            Dead = true;
            if (deathParticle != null) deathParticle.Play();
            Gm.Fx.Burst(Pos, Type.color, 10);
            Gm.OnEnemyKilled(this);
        }

        void FaceVisualToPlayer()
        {
            if (visualRoot == null) return;
            var toPlayer = Gm.Player.Pos - Pos;
            if (toPlayer.sqrMagnitude < 1e-6f) return;
            visualRoot.rotation = Quaternion.LookRotation(new Vector3(toPlayer.x, 0f, toPlayer.y), Vector3.up);
        }

        protected void ApplyTransform()
        {
            transform.position = PolygonMath.ToWorld(Pos);
            FaceVisualToPlayer();
            if (visualRenderer != null)
            {
                mpb ??= new MaterialPropertyBlock();
                var baseColor = Type.color;
                var c = hitFlash > 0f ? Color.white : baseColor;
                if (slowLeft > 0f && hitFlash <= 0f) c = Color.Lerp(baseColor, Cfg.trail, 0.5f);
                mpb.SetColor(BaseColorId, c);
                visualRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
