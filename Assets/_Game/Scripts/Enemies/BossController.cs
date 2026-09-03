using UnityEngine;

namespace Marchio
{
    public enum BossPhase { Chase, Telegraph, Dash, Recover }
    public enum BossAttack { Dash, Burst, Homing }

    public sealed class BossController : Enemy
    {
        [SerializeField] LineRenderer telegraphRing;
        [SerializeField] int ringSegments = 48;

        public BossPhase Phase { get; private set; }
        public BossAttack NextAttack { get; private set; }

        float attackTimer;
        float telegraphTimer;
        float telegraphTotal;
        float dashTimer;
        float recoverTimer;
        Vector2 dashVelocity;
        Vector2 target;

        protected override void OnInit()
        {
            Phase = BossPhase.Chase;
            NextAttack = BossAttack.Dash;
            attackTimer = Cfg.bossAttackCooldownMs;
            telegraphRing.positionCount = 0;
        }

        public override void OnDespawn()
        {
            telegraphRing.positionCount = 0;
        }

        protected override void Behave(float dt, float slowFactor)
        {
            var cfg = Cfg;
            var playerPos = Gm.Player.Pos;
            switch (Phase)
            {
                case BossPhase.Chase:
                {
                    var d = playerPos - Pos;
                    float len = d.magnitude;
                    if (len > 1e-6f) Pos += d / len * Speed * slowFactor * dt;
                    attackTimer -= dt * 1000f;
                    if (attackTimer <= 0f)
                    {
                        Phase = BossPhase.Telegraph;
                        telegraphTotal = TelegraphMs(NextAttack);
                        telegraphTimer = telegraphTotal;
                        target = playerPos;
                    }
                    break;
                }
                case BossPhase.Telegraph:
                {
                    telegraphTimer -= dt * 1000f;
                    DrawRing(1f - telegraphTimer / telegraphTotal);
                    if (telegraphTimer <= 0f)
                    {
                        telegraphRing.positionCount = 0;
                        if (NextAttack == BossAttack.Dash)
                        {
                            var d = target - Pos;
                            if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
                            dashVelocity = d.normalized * cfg.bossDashSpeed;
                            dashTimer = cfg.bossDashDurationMs;
                            ContactDamage = cfg.bossDashContactDamage;
                            Phase = BossPhase.Dash;
                        }
                        else if (NextAttack == BossAttack.Burst)
                        {
                            FireBurst();
                            Phase = BossPhase.Recover;
                            recoverTimer = cfg.bossBurstRecoverMs;
                        }
                        else
                        {
                            FireHoming();
                            Phase = BossPhase.Recover;
                            recoverTimer = cfg.bossHomingRecoverMs;
                        }
                    }
                    break;
                }
                case BossPhase.Dash:
                {
                    Pos += dashVelocity * dt;
                    dashTimer -= dt * 1000f;
                    if (Random.value < 0.6f) Gm.Fx.Burst(Pos, cfg.chaser, 1);
                    if (dashTimer <= 0f)
                    {
                        Phase = BossPhase.Chase;
                        ContactDamage = cfg.bossContactDamage;
                        attackTimer = cfg.bossAttackCooldownMs;
                        NextAttack = PickNextAttack(NextAttack);
                    }
                    break;
                }
                case BossPhase.Recover:
                {
                    recoverTimer -= dt * 1000f;
                    if (recoverTimer <= 0f)
                    {
                        Phase = BossPhase.Chase;
                        attackTimer = cfg.bossAttackCooldownMs;
                        NextAttack = PickNextAttack(NextAttack);
                    }
                    break;
                }
            }
            Pos = Gm.ClampToBossArena(Pos);
        }

        float TelegraphMs(BossAttack a)
        {
            return a == BossAttack.Dash ? Cfg.bossDashTelegraphMs
                : a == BossAttack.Burst ? Cfg.bossBurstTelegraphMs
                : Cfg.bossHomingTelegraphMs;
        }

        public static BossAttack PickNextAttack(BossAttack prev)
        {
            int roll = Random.Range(0, 2);
            for (int i = 0; i < 3; i++)
            {
                var a = (BossAttack)i;
                if (a == prev) continue;
                if (roll == 0) return a;
                roll--;
            }
            return prev;
        }

        void FireBurst()
        {
            var cfg = Cfg;
            int n = cfg.bossBurstCount;
            for (int i = 0; i < n; i++)
            {
                float ang = (float)i / n * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * cfg.bossBurstProjSpeed;
                Gm.EnemyProjectiles.Get().Init(Pos, v, cfg.bossBurstProjectileRadius, cfg.bossBurstProjDamage, true, false, 0f, cfg.enemyProjectile);
            }
            Gm.Fx.Burst(Pos, cfg.ranged, 18);
        }

        void FireHoming()
        {
            var cfg = Cfg;
            var d = Gm.Player.Pos - Pos;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.right;
            var v = d.normalized * cfg.bossHomingSpeed;
            Gm.EnemyProjectiles.Get().Init(Pos, v, cfg.bossHomingProjectileRadius, cfg.bossHomingDamage, true, true, cfg.bossHomingLifeS, cfg.enemyProjectile);
            Gm.Fx.Burst(Pos, cfg.ranged, 12);
        }

        void DrawRing(float progress)
        {
            float r = Radius + 10f + progress * 70f;
            telegraphRing.loop = true;
            telegraphRing.positionCount = ringSegments;
            for (int i = 0; i < ringSegments; i++)
            {
                float a = (float)i / ringSegments * Mathf.PI * 2f;
                telegraphRing.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 1.2f, Mathf.Sin(a) * r));
            }
            var c = Cfg.telegraph;
            c.a = 0.35f + progress * 0.65f;
            telegraphRing.startColor = c;
            telegraphRing.endColor = c;
        }
    }
}
