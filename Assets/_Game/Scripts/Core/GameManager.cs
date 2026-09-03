using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public enum GameMode { Menu, Play, LevelClear, FillUpgrade, PowerUp, Fail, Victory }

    public sealed class GameManager : MonoBehaviour
    {
        const float Step60 = 1f / 60f;

        public static GameManager I { get; private set; }

        [SerializeField] GameConfig config;
        [SerializeField] RunPreset preset;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] InputReader input;
        [SerializeField] PlayerController player;
        [SerializeField] LoopTrail trail;
        [SerializeField] AutoAttack autoAttack;
        [SerializeField] WaveManager waves;
        [SerializeField] UpgradeManager upgrades;
        [SerializeField] ParticleFx fx;
        [SerializeField] Transform poolRoot;

        [Header("Pooled prefabs")]
        [SerializeField] Projectile enemyProjectilePrefab;
        [SerializeField] Projectile playerProjectilePrefab;
        [SerializeField] Barrier barrierPrefab;
        [SerializeField] DeadTrail deadTrailPrefab;

        public GameConfig Config => config;
        public RunPreset Preset => preset;
        public CameraRig Cam => cameraRig;
        public PlayerController Player => player;
        public LoopTrail Trail => trail;
        public WaveManager Waves => waves;
        public UpgradeManager Upgrades => upgrades;
        public ParticleFx Fx => fx;
        public Run Run { get; private set; }
        public TrophyRoad Trophy { get; private set; }

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

        public event Action<GameMode> ModeChanged;
        public event Action<Vector2, int> DamageText;
        public event Action<TrophyNode> NodeUnlocked;

        float accumulator;

        public float PlayerMaxHp
        {
            get
            {
                float mult = Trophy.MaxHpMult;
                mult += upgrades.Level(PowerId.IronHull) * preset.ironHullPerStack;
                if (upgrades.Level(PowerId.DevilsBargain) > 0) mult -= preset.devilHpPenalty;
                return Mathf.Max(1f, config.playerMaxHP * mult);
            }
        }

        public float DamageMult
        {
            get
            {
                float mult = Trophy.DamageMult * (1f + upgrades.Level(PowerId.Overload) * preset.overloadPerStack);
                if (upgrades.Level(PowerId.DevilsBargain) > 0) mult *= preset.devilDamageMult;
                return mult;
            }
        }

        public float FireRateMult => 1f + upgrades.Level(PowerId.RapidFeed) * preset.rapidFeedPerStack;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 60;
            EnemyProjectiles = new ObjectPool<Projectile>(enemyProjectilePrefab, poolRoot, 32);
            PlayerProjectiles = new ObjectPool<Projectile>(playerProjectilePrefab, poolRoot, 16);
            Barriers = new ObjectPool<Barrier>(barrierPrefab, poolRoot, 4);
            DeadTrails = new ObjectPool<DeadTrail>(deadTrailPrefab, poolRoot, 4);
            Trophy = new TrophyRoad(preset);
            Run = new Run(preset, Trophy);
            upgrades.Init(config);
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

            Run.LevelTime += dt;
            var inp = input.Read(dt);
            player.Tick(dt, inp);
            trail.Tick(dt, inp.Draw);
            autoAttack.Tick(dt);
            waves.Tick(dt);
            waves.TickEnemies(dt);
            waves.ApplySeparation(dt);
            TickEnemyProjectiles(dt);
            TickPlayerProjectiles(dt);

            if (Mode != GameMode.Play) return;
            if (Trophy.HasPending) ApplyUnlock(Trophy.ClaimNext());
            if (Run.ThresholdReached) CompleteLevel();
            else if (Run.VictoryLapDone) FinishVictoryLap();
        }

        void ApplyUnlock(TrophyNode node)
        {
            player.ApplyLook();
            player.ClampHp();
            trail.ApplyLook();
            if (node.reward == TrophyReward.ExtraRevive) Run.RevivesLeft++;
            fx.Burst(player.Pos, config.electricBorderSpark, 40);
            AddJuice(config.hitstopBaseMs * 2f, config.shakeBase);
            NodeUnlocked?.Invoke(node);
        }

        void TickEnemyProjectiles(float dt)
        {
            var pool = EnemyProjectiles;
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                var b = pool.Active[i];
                var from = b.Pos;
                b.Advance(dt, player.Pos);
                if (b.Expired || cameraRig.IsOutside(b.Pos, config.projectileDespawnPadPx)) { pool.Release(b); continue; }
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
                if (b.Expired || cameraRig.IsOutside(b.Pos, config.projectileDespawnPadPx)) { pool.Release(b); continue; }
                for (int e = 0; e < Enemies.Count; e++)
                {
                    var en = Enemies[e];
                    if (en.Dead || en == b.LastHit) continue;
                    if (Vector2.Distance(b.Pos, en.Pos) < b.Radius + en.Radius)
                    {
                        en.ApplyProjectileHit(b.Damage);
                        var next = b.Bounces > 0 ? NearestEnemyExcept(b.Pos, en, preset.ricochetRangePx) : null;
                        if (next != null) b.Redirect(en, next.Pos);
                        else pool.Release(b);
                        break;
                    }
                }
            }
        }

        Enemy NearestEnemyExcept(Vector2 from, Enemy except, float maxDist)
        {
            Enemy best = null;
            float bestD = maxDist * maxDist;
            for (int i = 0; i < Enemies.Count; i++)
            {
                var en = Enemies[i];
                if (en.Dead || en == except) continue;
                float d = (en.Pos - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = en; }
            }
            return best;
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
            return MaxLoopLength * (1f + bonus) * Trophy.TrailLengthMult;
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
            Run.AddScore(ScoreSource.Kill, en.Type.score);
            if (en.Type.IsBoss)
            {
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

        public void OnScreenTap()
        {
            if (Mode == GameMode.Menu) StartRun();
        }

        public void StartRun()
        {
            ResetRun();
            Run.Start(preset.freeRevives + Trophy.ExtraRevives);
            BeginLevel(1);
        }

        void BeginLevel(int level)
        {
            ClearField();
            Run.BeginLevel(level);
            waves.BeginLevel();
            trail.ResetState();
            autoAttack.ResetState();
            player.ClampHp();
            SetMode(GameMode.Play);
        }

        void CompleteLevel()
        {
            Run.CompleteLevel();
            if (Run.HealsAfter(Run.Level))
            {
                player.Heal(PlayerMaxHp * preset.healAmount);
                Run.HealedOnClear = true;
            }
            Trophy.Flush();
            SetMode(GameMode.LevelClear);
        }

        public void ContinueFromClear()
        {
            if (Mode != GameMode.LevelClear) return;
            if (Run.OffersFillUpgrade(Run.Level))
            {
                upgrades.Fill.RollThree();
                if (upgrades.Fill.Choices.Count > 0) { SetMode(GameMode.FillUpgrade); return; }
            }
            OfferPowerUpOrStart();
        }

        public void PickFill(int id)
        {
            if (Mode != GameMode.FillUpgrade) return;
            upgrades.Fill.Apply(id);
            OfferPowerUpOrStart();
        }

        void OfferPowerUpOrStart()
        {
            int next = Run.Level + 1;
            if (Run.OffersPowerUpBefore(next))
            {
                upgrades.Power.RollThree();
                if (upgrades.Power.Choices.Count > 0) { SetMode(GameMode.PowerUp); return; }
            }
            BeginLevel(next);
        }

        public void PickPower(int id)
        {
            if (Mode != GameMode.PowerUp) return;
            upgrades.Power.Apply(id);
            BeginLevel(Run.Level + 1);
        }

        public void Fail()
        {
            if (Mode != GameMode.Play) return;
            Trophy.Flush();
            SetMode(GameMode.Fail);
        }

        public void Revive()
        {
            if (Mode != GameMode.Fail || Run.RevivesLeft <= 0) return;
            Run.RevivesLeft--;
            player.SetHp(PlayerMaxHp * preset.reviveHp);
            BeginLevel(Run.Level);
        }

        public void ToMenu()
        {
            ResetRun();
            SetMode(GameMode.Menu);
        }

        public void ResetProgress()
        {
            if (Mode != GameMode.Menu) return;
            Trophy.Reset();
            ResetRun();
            SetMode(GameMode.Menu);
        }

        void FinishVictoryLap()
        {
            Trophy.Flush();
            SetMode(GameMode.Victory);
        }

        void ClearField()
        {
            foreach (var pool in enemyPools.Values) pool.ReleaseAll();
            EnemyProjectiles.ReleaseAll();
            PlayerProjectiles.ReleaseAll();
            Barriers.ReleaseAll();
            DeadTrails.ReleaseAll();
            Enemies.Clear();
            Combo = 0;
            Hitstop = 0f;
            Shake = 0f;
            accumulator = 0f;
            input.ResetState();
            waves.ResetState();
        }

        void ResetRun()
        {
            ClearField();
            KillXP = 0;
            MaxLoopLength = config.BaseMaxLoopLength;
            upgrades.ResetState();
            player.ResetState();
            trail.ResetState();
            autoAttack.ResetState();
            Run.Start(preset.freeRevives + Trophy.ExtraRevives);
        }

        void SetMode(GameMode mode)
        {
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
