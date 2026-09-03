using UnityEngine;

namespace Marchio
{
    public enum EnemyKind { Chaser, Fast, Ranged, Boss }

    public class Enemy : MonoBehaviour, IPoolable
    {
        [SerializeField] protected Transform visualRoot;
        [SerializeField] protected Renderer visualRenderer;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock mpb;

        public EnemyKind Kind { get; protected set; }
        public Vector2 Pos { get; protected set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Radius { get; private set; }
        public float Speed { get; private set; }
        public float ContactDamage { get; protected set; }
        public bool Dead { get; private set; }
        public bool IgnoresBarriers => Kind == EnemyKind.Boss;

        protected float steerJitter;
        protected float speedMult;
        float preferredDistJitter;
        float fireTimer;
        float hitFlash;
        float slowLeft;
        float burnDps;
        float burnLeft;
        Color baseColor;

        protected GameManager Gm => GameManager.I;
        protected GameConfig Cfg => GameManager.I.Config;

        public virtual void OnSpawn() { }
        public virtual void OnDespawn() { }

        public void Init(EnemyKind kind, Vector2 pos, float hpMult)
        {
            var cfg = Cfg;
            Kind = kind;
            Pos = pos;
            Dead = false;
            hitFlash = 0f;
            slowLeft = 0f;
            burnDps = 0f;
            burnLeft = 0f;
            steerJitter = Random.Range(-cfg.enemySteerJitterRad, cfg.enemySteerJitterRad);
            speedMult = Random.Range(1f - cfg.enemySpeedVariance, 1f + cfg.enemySpeedVariance);
            preferredDistJitter = 0f;
            switch (kind)
            {
                case EnemyKind.Chaser:
                    SetStats(cfg.chaserHP * hpMult, cfg.chaserR, cfg.chaserSpeed);
                    fireTimer = 300f + Random.value * 800f;
                    break;
                case EnemyKind.Fast:
                    SetStats(cfg.fastHP * hpMult, cfg.fastR, cfg.fastSpeed);
                    fireTimer = 300f + Random.value * 800f;
                    break;
                case EnemyKind.Ranged:
                    SetStats(cfg.rangedHP * hpMult, cfg.rangedR, cfg.rangedSpeed);
                    fireTimer = 400f + Random.value * 600f;
                    preferredDistJitter = Random.Range(-cfg.rangedPreferredDistJitter, cfg.rangedPreferredDistJitter);
                    break;
                case EnemyKind.Boss:
                    SetStats(cfg.bossHP * hpMult, cfg.bossR, cfg.bossSpeed);
                    steerJitter = 0f;
                    speedMult = 1f;
                    break;
            }
            ContactDamage = kind == EnemyKind.Boss ? cfg.bossContactDamage : cfg.playerContactDamage;
            baseColor = cfg.EnemyColor(kind);
            if (visualRoot != null) visualRoot.localScale = Vector3.one * Radius * 2f;
            OnInit();
            ApplyTransform();
        }

        protected virtual void OnInit() { }

        void SetStats(float hp, float radius, float speed)
        {
            Hp = hp;
            MaxHp = hp;
            Radius = radius;
            Speed = speed;
        }

        public void SetPos(Vector2 p)
        {
            Pos = p;
        }

        public void Tick(float dt)
        {
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
            ApplyTransform();
        }

        protected virtual void Behave(float dt, float slowFactor)
        {
            var cfg = Cfg;
            var toPlayer = Gm.Player.Pos - Pos;
            float d = toPlayer.magnitude;
            if (d < 1e-6f) d = 1f;
            float cosj = Mathf.Cos(steerJitter), sinj = Mathf.Sin(steerJitter);
            var steered = new Vector2(toPlayer.x * cosj - toPlayer.y * sinj, toPlayer.x * sinj + toPlayer.y * cosj) / d;

            if (Kind == EnemyKind.Ranged)
            {
                float preferred = cfg.rangedPreferredDist + preferredDistJitter;
                float dir = d > preferred ? 1f : d < preferred * 0.7f ? -1f : 0f;
                Pos += steered * dir * Speed * speedMult * slowFactor * dt;
                fireTimer -= dt * 1000f;
                if (fireTimer <= 0f)
                {
                    fireTimer = cfg.rangedFireIntervalMs;
                    Fire(cfg.rangedProjectileSpeed, cfg.rangedProjectileDamage);
                }
                return;
            }

            Pos += steered * Speed * speedMult * slowFactor * dt;
            bool fast = Kind == EnemyKind.Fast;
            fireTimer -= dt * 1000f;
            if (fireTimer <= 0f)
            {
                fireTimer = fast ? cfg.fastFireIntervalMs : cfg.chaserFireIntervalMs;
                float minDist = fast ? cfg.fastFireMinDist : cfg.chaserFireMinDist;
                if (d > minDist)
                    Fire(fast ? cfg.fastProjectileSpeed : cfg.chaserProjectileSpeed,
                         fast ? cfg.fastProjectileDamage : cfg.chaserProjectileDamage);
            }
        }

        protected void Fire(float speed, float damage)
        {
            var dir = Gm.Player.Pos - Pos;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();
            var p = Gm.EnemyProjectiles.Get();
            p.Init(Pos, dir * speed, Cfg.enemyProjectileRadius, damage, false, false, 0f, Cfg.enemyProjectile);
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
            if (burn > 0)
            {
                burnDps = Cfg.burnDpsPerLevel * burn;
                burnLeft = Cfg.burnDurationS;
            }
            if (up.Level(UpgradeId.FreezeFill) > 0) slowLeft = Cfg.freezeDurationS;
            return ApplyDamage(dmg, 0.15f, true);
        }

        public void ApplyBarrierDamage(float dmg)
        {
            ApplyDamage(dmg, 0.08f, false);
        }

        public void Kill()
        {
            if (Dead) return;
            Dead = true;
            Gm.Fx.Burst(Pos, baseColor, 10);
            Gm.OnEnemyKilled(this);
        }

        protected void ApplyTransform()
        {
            transform.position = PolygonMath.ToWorld(Pos);
            if (visualRenderer != null)
            {
                mpb ??= new MaterialPropertyBlock();
                var c = hitFlash > 0f ? Color.white : baseColor;
                if (slowLeft > 0f && hitFlash <= 0f) c = Color.Lerp(baseColor, Cfg.trail, 0.5f);
                mpb.SetColor(BaseColorId, c);
                visualRenderer.SetPropertyBlock(mpb);
            }
        }
    }
}
