using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public enum GameMode { Menu, Intro, Play, LevelClear, FillUpgrade, PowerUp, Fail, Victory }

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
        [SerializeField] SoulStone soulStonePrefab;

        public GameConfig Config => config;
        public RunPreset Preset => preset;
        public CameraRig Cam => cameraRig;
        public PlayerController Player => player;
        public LoopTrail Trail => trail;
        public WaveManager Waves => waves;
        public UpgradeManager Upgrades => upgrades;
        public ParticleFx Fx => fx;
        public Run Run { get; private set; }

        readonly Dictionary<EnemyTypeSO, ObjectPool<Enemy>> enemyPools = new Dictionary<EnemyTypeSO, ObjectPool<Enemy>>();
        readonly Dictionary<Projectile, ObjectPool<Projectile>> enemyProjectilePools = new Dictionary<Projectile, ObjectPool<Projectile>>();
        public readonly List<ObjectPool<Projectile>> EnemyProjectilePools = new List<ObjectPool<Projectile>>();
        public ObjectPool<Projectile> PlayerProjectiles { get; private set; }
        readonly Dictionary<SoulStone, ObjectPool<SoulStone>> soulStonePools = new Dictionary<SoulStone, ObjectPool<SoulStone>>();
        public readonly List<ObjectPool<SoulStone>> SoulStonePools = new List<ObjectPool<SoulStone>>();
        public readonly List<Enemy> Enemies = new List<Enemy>();

        public GameMode Mode { get; private set; } = GameMode.Menu;
        public int Combo { get; private set; }
        public int KillXP { get; private set; }
        public float MaxLoopLength { get; private set; }
        public float Hitstop { get; private set; }
        public float Shake { get; private set; }

        public event Action<GameMode> ModeChanged;
        public event Action<Vector2, int> DamageText;

        float accumulator;

        public float PlayerMaxHp
        {
            get
            {
                float mult = 1f + upgrades.Level(PowerId.IronHull) * preset.ironHullPerStack;
                if (upgrades.Level(PowerId.DevilsBargain) > 0) mult -= preset.devilHpPenalty;
                return Mathf.Max(1f, config.playerMaxHP * mult);
            }
        }

        public float DamageMult
        {
            get
            {
                float mult = 1f + upgrades.Level(PowerId.Overload) * preset.overloadPerStack;
                if (upgrades.Level(PowerId.DevilsBargain) > 0) mult *= preset.devilDamageMult;
                return mult;
            }
        }

        public float FireRateMult => 1f + upgrades.Level(PowerId.RapidFeed) * preset.rapidFeedPerStack;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = Application.platform == RuntimePlatform.WebGLPlayer ? -1 : 60;
            PlayerProjectiles = new ObjectPool<Projectile>(playerProjectilePrefab, poolRoot, 16);
            Run = new Run(preset);
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
            if (Mode == GameMode.Menu || Mode == GameMode.Intro) return;
            cameraRig.Follow(player.Pos, Shake);
        }

        void SnapCameraToIntro()
        {
            var pose = player.IntroCameraPose;
            if (pose != null)
            {
                var rot = pose.rotation;
                if ((rot * Vector3.forward).y > -0.15f) rot = Quaternion.LookRotation(player.transform.position - pose.position, Vector3.up);
                cameraRig.SnapTo(pose.position, rot);
            }
            else cameraRig.Follow(player.Pos, 0f);
        }

        void Step(float dt)
        {
            if (Hitstop > 0f)
            {
                Hitstop -= dt * 1000f;
                return;
            }
            if (Shake > 0f) Shake = Mathf.Max(0f, Shake - dt * 20f);

            if (Mode == GameMode.Intro)
            {
                bool done = cameraRig.TickTransition(dt, player.Pos);
                player.SetIntroProgress(cameraRig.TransitionProgress);
                if (done) SetMode(GameMode.Play);
                return;
            }
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
            TickSoulStones(dt);

            if (Mode != GameMode.Play) return;
            if (Run.LevelCleared) CompleteLevel();
            else if (Run.VictoryLapDone) FinishVictoryLap();
        }

        public ObjectPool<Projectile> EnemyProjectilesFor(Projectile prefab)
        {
            if (prefab == null) prefab = enemyProjectilePrefab;
            if (!enemyProjectilePools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<Projectile>(prefab, poolRoot, 16);
                enemyProjectilePools.Add(prefab, pool);
                EnemyProjectilePools.Add(pool);
            }
            return pool;
        }

        void TickEnemyProjectiles(float dt)
        {
            for (int p = 0; p < EnemyProjectilePools.Count; p++) TickEnemyProjectiles(EnemyProjectilePools[p], dt);
        }

        void TickEnemyProjectiles(ObjectPool<Projectile> pool, float dt)
        {
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                var b = pool.Active[i];
                b.Advance(dt);
                if (b.Expired || cameraRig.IsOutside(b.Pos, config.projectileDespawnPadPx)) { pool.Release(b); continue; }
                if (player.Invuln <= 0f && Vector2.Distance(b.Pos, player.Pos) < b.Radius + config.playerRadius)
                {
                    pool.Release(b);
                    player.TakeDamage(b.Damage);
                }
            }
        }

        void TickPlayerProjectiles(float dt)
        {
            var pool = PlayerProjectiles;
            for (int i = pool.Active.Count - 1; i >= 0; i--)
            {
                var b = pool.Active[i];
                b.Advance(dt);
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
            Run.AddScore(ScoreSource.Kill, en.Type.score);
            KillXP += en.Type.xp;
            UpdateMaxLoopLength();
            var stonePrefab = en.SoulStonePrefab != null ? en.SoulStonePrefab : soulStonePrefab;
            if (stonePrefab != null) SoulStonesFor(stonePrefab).Get().Init(en.Pos);
        }

        public ObjectPool<SoulStone> SoulStonesFor(SoulStone prefab)
        {
            if (prefab == null) prefab = soulStonePrefab;
            if (!soulStonePools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<SoulStone>(prefab, poolRoot, 4);
                soulStonePools.Add(prefab, pool);
                SoulStonePools.Add(pool);
            }
            return pool;
        }

        void TickSoulStones(float dt)
        {
            float pickupRadius = config.PlayerWidth * config.soulstonePickupRadiusMult;
            for (int p = 0; p < SoulStonePools.Count; p++)
            {
                var pool = SoulStonePools[p];
                for (int i = pool.Active.Count - 1; i >= 0; i--)
                {
                    var s = pool.Active[i];
                    if (!s.Collecting && Vector2.Distance(s.Pos, player.Pos) <= pickupRadius) s.BeginCollect();
                    if (s.Tick(dt, player.Pos))
                    {
                        fx.Burst(s.Pos, config.trail, 10);
                        AddJuice(0f, config.shakeBase * 0.3f);
                        pool.Release(s);
                    }
                }
            }
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
                pool = new ObjectPool<Enemy>(type.prefab, poolRoot, 8);
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
            Run.Start(preset.freeRevives);
            BeginLevel(1, true);
        }

        void BeginLevel(int level, bool intro = false)
        {
            ClearField();
            Run.BeginLevel(level);
            waves.BeginLevel();
            trail.ResetState();
            autoAttack.ResetState();
            player.ClampHp();
            if (intro)
            {
                cameraRig.BeginTransitionToGameplay(config.introTransitionS);
                SetMode(GameMode.Intro);
            }
            else SetMode(GameMode.Play);
        }

        void CompleteLevel()
        {
            Run.CompleteLevel();
            if (Run.HealsAfter(Run.Level))
            {
                player.Heal(PlayerMaxHp * preset.healAmount);
                Run.HealedOnClear = true;
            }
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

        void FinishVictoryLap()
        {
            SetMode(GameMode.Victory);
        }

        void ClearField()
        {
            foreach (var pool in enemyPools.Values) pool.ReleaseAll();
            foreach (var pool in EnemyProjectilePools) pool.ReleaseAll();
            PlayerProjectiles.ReleaseAll();
            foreach (var pool in SoulStonePools) pool.ReleaseAll();
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
            Run.Start(preset.freeRevives);
            SnapCameraToIntro();
        }

        void SetMode(GameMode mode)
        {
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }
    }
}
