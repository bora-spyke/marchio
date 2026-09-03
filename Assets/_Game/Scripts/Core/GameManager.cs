using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public enum GameMode { Menu, Play, Upgrade, Over }

    public sealed class GameManager : MonoBehaviour
    {
        const float Step60 = 1f / 60f;

        public static GameManager I { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] InputReader input;
        [SerializeField] PlayerController player;
        [SerializeField] LoopTrail trail;
        [SerializeField] AutoAttack autoAttack;
        [SerializeField] WaveManager waves;
        [SerializeField] UpgradeManager upgrades;
        [SerializeField] ParticleFx fx;
        [SerializeField] Transform poolRoot;
        [SerializeField] WaveTableSO waveTable;

        [Header("Pooled prefabs")]
        [SerializeField] Projectile enemyProjectilePrefab;
        [SerializeField] Projectile playerProjectilePrefab;
        [SerializeField] Barrier barrierPrefab;
        [SerializeField] DeadTrail deadTrailPrefab;

        public GameConfig Config => config;
        public CameraRig Cam => cameraRig;
        public PlayerController Player => player;
        public LoopTrail Trail => trail;
        public WaveManager Waves => waves;
        public UpgradeManager Upgrades => upgrades;
        public ParticleFx Fx => fx;
        public WaveTableSO WaveTable => waveTable;

        readonly Dictionary<EnemyTypeSO, ObjectPool<Enemy>> enemyPools = new Dictionary<EnemyTypeSO, ObjectPool<Enemy>>();
        public ObjectPool<Projectile> EnemyProjectiles { get; private set; }
        public ObjectPool<Projectile> PlayerProjectiles { get; private set; }
        public ObjectPool<Barrier> Barriers { get; private set; }
        public ObjectPool<DeadTrail> DeadTrails { get; private set; }
        public readonly List<Enemy> Enemies = new List<Enemy>();

        public GameMode Mode { get; private set; } = GameMode.Menu;
        public int Combo { get; private set; }
        public int KillXP { get; private set; }
        public float MaxLoopLength { get; private set; }
        public float Hitstop { get; private set; }
        public float Shake { get; private set; }
        public int Best { get; private set; }
        public float OverTime { get; private set; }
        public bool BossArenaActive { get; private set; }
        public Vector2 BossArenaCenter { get; private set; }

        public event Action<GameMode> ModeChanged;
        public event Action<Vector2, int> DamageText;

        float accumulator;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            EnemyProjectiles = new ObjectPool<Projectile>(enemyProjectilePrefab, poolRoot, 32);
            PlayerProjectiles = new ObjectPool<Projectile>(playerProjectilePrefab, poolRoot, 16);
            Barriers = new ObjectPool<Barrier>(barrierPrefab, poolRoot, 4);
            DeadTrails = new ObjectPool<DeadTrail>(deadTrailPrefab, poolRoot, 4);
            cameraRig.Configure(config);
            ResetRun();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.25f);
            accumulator += dt;
            while (accumulator >= Step60)
            {
                Step(Step60);
                accumulator -= Step60;
            }
        }

        void LateUpdate()
        {
            cameraRig.Follow(player.Pos, Shake);
        }

        void Step(float dt)
        {
            if (Hitstop > 0f)
            {
                Hitstop -= dt * 1000f;
                return;
            }
            if (Shake > 0f) Shake = Mathf.Max(0f, Shake - dt * 20f);
            trail.TickFlash(dt);
            for (int i = Barriers.Active.Count - 1; i >= 0; i--)
                if (!Barriers.Active[i].Tick(dt)) Barriers.Release(Barriers.Active[i]);
            for (int i = DeadTrails.Active.Count - 1; i >= 0; i--)
                if (!DeadTrails.Active[i].Tick(dt)) DeadTrails.Release(DeadTrails.Active[i]);

            if (Mode != GameMode.Play) return;

            var inp = input.Read(dt);
            player.Tick(dt, inp);
            trail.Tick(dt, inp.Draw);
            autoAttack.Tick(dt);
            waves.Tick(dt);
            waves.TickEnemies(dt);
            waves.ApplySeparation(dt);
            TickEnemyProjectiles(dt);
            TickPlayerProjectiles(dt);
            if (player.Hp <= 0f) EndRun();
        }

        void TickEnemyProjectiles(float dt)
        {
            var pool = EnemyProjectiles;
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                var b = pool.Active[i];
                var from = b.Pos;
                b.Advance(dt, player.Pos);
                if (cameraRig.IsOutside(b.Pos, config.projectileDespawnPadPx)) { pool.Release(b); continue; }
                if (!b.FromBoss && SegmentBlockedByBarrier(from, b.Pos, out var hit))
                {
                    fx.Burst(hit, config.loopEdge, 6);
                    pool.Release(b);
                    continue;
                }
                if (player.Invuln <= 0f && Vector2.Distance(b.Pos, player.Pos) < b.Radius + config.playerRadius)
                {
                    pool.Release(b);
                    player.TakeDamage(b.Damage);
                    continue;
                }
                if (b.Homing && b.Life <= 0f)
                {
                    fx.Burst(b.Pos, config.enemyProjectile, 14);
                    pool.Release(b);
                }
            }
        }

        void TickPlayerProjectiles(float dt)
        {
            var pool = PlayerProjectiles;
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                var b = pool.Active[i];
                b.Advance(dt, player.Pos);
                if (cameraRig.IsOutside(b.Pos, config.projectileDespawnPadPx)) { pool.Release(b); continue; }
                for (int e = 0; e < Enemies.Count; e++)
                {
                    var en = Enemies[e];
                    if (en.Dead) continue;
                    if (Vector2.Distance(b.Pos, en.Pos) < b.Radius + en.Radius)
                    {
                        en.ApplyDamage(config.autoAttackDamage, 0.1f, true);
                        pool.Release(b);
                        break;
                    }
                }
            }
        }

        public bool SegmentBlockedByBarrier(Vector2 from, Vector2 to, out Vector2 hit)
        {
            for (int i = 0; i < Barriers.Active.Count; i++)
                if (Barriers.Active[i].BlocksSegment(from, to, out hit)) return true;
            hit = default;
            return false;
        }

        public float EffectiveMaxLoopLength()
        {
            float bonus = Mathf.Min(Combo * config.comboLoopLengthStep, config.comboLoopLengthCapBonus);
            return MaxLoopLength * (1f + bonus);
        }

        public void AddCombo(int count) => Combo += count;

        public void ResetCombo() => Combo = 0;

        public void AddJuice(float hitstopMs, float shake)
        {
            Hitstop = hitstopMs;
            Shake = shake;
        }

        public void ShowDamage(Vector2 pos, float value) => DamageText?.Invoke(pos, Mathf.RoundToInt(value));

        public void OnEnemyKilled(Enemy en)
        {
            if (en.Type.IsBoss)
            {
                BossArenaActive = false;
                waves.OnBossKilled();
                fx.Burst(en.Pos, en.Type.color, 32);
                return;
            }
            KillXP += en.Type.xp;
            UpdateMaxLoopLength();
        }

        void UpdateMaxLoopLength()
        {
            int steps = KillXP / config.loopGrowthXPStep;
            float baseLen = config.BaseMaxLoopLength;
            float uncapped = baseLen * (1f + steps * config.loopGrowthPct);
            float newMax = Mathf.Min(uncapped, baseLen * config.maxLoopLengthGrowthCapMult);
            if (newMax > MaxLoopLength) MaxLoopLength = newMax;
        }

        public Enemy GetEnemy(EnemyTypeSO type)
        {
            if (!enemyPools.TryGetValue(type, out var pool))
            {
                pool = new ObjectPool<Enemy>(type.prefab, poolRoot, type.IsBoss ? 1 : 8);
                enemyPools.Add(type, pool);
            }
            return pool.Get();
        }

        public void ReleaseEnemy(Enemy en)
        {
            Enemies.Remove(en);
            enemyPools[en.Type].Release(en);
        }

        public void ActivateBossArena(Vector2 center)
        {
            BossArenaActive = true;
            BossArenaCenter = center;
        }

        public Vector2 ClampToBossArena(Vector2 p)
        {
            if (!BossArenaActive) return p;
            var d = p - BossArenaCenter;
            float r = config.bossArenaRadius;
            if (d.sqrMagnitude > r * r) return BossArenaCenter + d.normalized * r;
            return p;
        }

        public void OnScreenTap()
        {
            if (Mode == GameMode.Menu) StartRun();
            else if (Mode == GameMode.Over && Time.unscaledTime - OverTime > 0.35f) StartRun();
        }

        public void StartRun()
        {
            ResetRun();
            SetMode(GameMode.Play);
            waves.StartWave(1);
        }

        public void OpenUpgradeScreen()
        {
            upgrades.RollThree();
            SetMode(GameMode.Upgrade);
        }

        public void PickUpgrade(UpgradeId id)
        {
            if (Mode != GameMode.Upgrade) return;
            upgrades.Apply(id);
            SetMode(GameMode.Play);
            waves.StartWave(waves.Wave + 1);
        }

        public void EndRun()
        {
            if (Mode == GameMode.Over) return;
            OverTime = Time.unscaledTime;
            if (waves.Wave > Best) Best = waves.Wave;
            SetMode(GameMode.Over);
        }

        void ResetRun()
        {
            foreach (var pool in enemyPools.Values) pool.ReleaseAll();
            EnemyProjectiles.ReleaseAll();
            PlayerProjectiles.ReleaseAll();
            Barriers.ReleaseAll();
            DeadTrails.ReleaseAll();
            Enemies.Clear();
            Combo = 0;
            KillXP = 0;
            MaxLoopLength = config.BaseMaxLoopLength;
            Hitstop = 0f;
            Shake = 0f;
            BossArenaActive = false;
            accumulator = 0f;
            input.ResetState();
            player.ResetState();
            trail.ResetState();
            autoAttack.ResetState();
            waves.ResetState();
            upgrades.ResetState();
        }

        void SetMode(GameMode mode)
        {
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
