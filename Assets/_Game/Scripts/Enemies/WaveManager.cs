using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public sealed class WaveManager : MonoBehaviour
    {
        readonly List<EnemyTypeSO> spawnQueue = new List<EnemyTypeSO>(64);
        float spawnTimer;
        float waveClearTimer;

        public int Wave { get; private set; }
        public float WaveHpMult { get; private set; } = 1f;
        public bool WaveClearing { get; private set; }
        public int BossSpawnedWave { get; private set; }
        public BossController ActiveBoss { get; private set; }

        GameManager Gm => GameManager.I;
        GameConfig Cfg => GameManager.I.Config;

        public void ResetState()
        {
            spawnQueue.Clear();
            spawnTimer = 0f;
            waveClearTimer = 0f;
            Wave = 0;
            WaveHpMult = 1f;
            WaveClearing = false;
            BossSpawnedWave = 0;
            ActiveBoss = null;
        }

        public void StartWave(int n)
        {
            Wave = n;
            WaveHpMult = 1f + (n - 1) * Cfg.waveHpScalePerWave;
            spawnQueue.Clear();
            if (Cfg.spawnEnemies)
            {
                Gm.WaveTable.Compose(n, spawnQueue);
                Shuffle(spawnQueue);
            }
            spawnTimer = 0f;
            WaveClearing = false;
        }

        static void Shuffle(List<EnemyTypeSO> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public void Tick(float dt)
        {
            if (!Cfg.spawnEnemies) return;
            if (spawnQueue.Count > 0)
            {
                spawnTimer -= dt * 1000f;
                if (spawnTimer <= 0f)
                {
                    spawnTimer = Cfg.enemySpawnStaggerMs;
                    var type = spawnQueue[spawnQueue.Count - 1];
                    spawnQueue.RemoveAt(spawnQueue.Count - 1);
                    Spawn(type, SpawnPoint());
                }
            }

            if (!WaveClearing && spawnQueue.Count == 0 && Gm.Enemies.Count == 0 && Wave > 0)
            {
                var bossType = Gm.WaveTable.BossAfter(Wave);
                if (bossType != null && BossSpawnedWave != Wave) StartBossFight(bossType);
                else BeginClear();
            }

            if (WaveClearing)
            {
                waveClearTimer -= dt * 1000f;
                if (waveClearTimer <= 0f)
                {
                    WaveClearing = false;
                    if (Wave % Cfg.upgradeEveryNWaves == 0) Gm.OpenUpgradeScreen();
                    else StartWave(Wave + 1);
                }
            }
        }

        void BeginClear()
        {
            WaveClearing = true;
            waveClearTimer = Cfg.waveClearDelayMs;
        }

        Vector2 SpawnPoint()
        {
            var center = Gm.Player.Pos;
            var half = Gm.Cam.HalfExtents;
            float pad = Cfg.spawnPadPx;
            float minX = center.x - half.x, minY = center.y - half.y;
            float w = half.x * 2f, h = half.y * 2f;
            switch (Random.Range(0, 4))
            {
                case 0: return new Vector2(minX + Random.value * w, minY - pad);
                case 1: return new Vector2(minX + Random.value * w, minY + h + pad);
                case 2: return new Vector2(minX - pad, minY + Random.value * h);
                default: return new Vector2(minX + w + pad, minY + Random.value * h);
            }
        }

        Enemy Spawn(EnemyTypeSO type, Vector2 pos)
        {
            var en = Gm.GetEnemy(type);
            en.Init(type, pos, WaveHpMult);
            Gm.Enemies.Add(en);
            return en;
        }

        void StartBossFight(EnemyTypeSO bossType)
        {
            BossSpawnedWave = Wave;
            var center = Gm.Player.Pos;
            Gm.ActivateBossArena(center);
            var spawnPos = center + new Vector2(0f, Cfg.bossArenaRadius * 0.6f);
            ActiveBoss = Spawn(bossType, spawnPos) as BossController;
            Gm.Fx.Burst(spawnPos, bossType.color, 24);
        }

        public void OnBossKilled()
        {
            ActiveBoss = null;
            BeginClear();
        }

        public void TickEnemies(float dt)
        {
            var gm = Gm;
            var cfg = Cfg;
            var player = gm.Player;
            for (int i = gm.Enemies.Count - 1; i >= 0; i--)
            {
                var en = gm.Enemies[i];
                if (en.Dead) { gm.ReleaseEnemy(en); continue; }
                en.Tick(dt);
                if (en.Dead) { gm.ReleaseEnemy(en); continue; }

                if (!en.IgnoresBarriers)
                {
                    bool touching = false;
                    var barriers = gm.Barriers.Active;
                    for (int b = 0; b < barriers.Count; b++)
                        if (barriers[b].PushOut(en, dt)) touching = true;
                    if (touching) en.ApplyBarrierDamage(cfg.barrierDps * dt);
                    if (en.Dead) { gm.ReleaseEnemy(en); continue; }
                }

                if (player.Invuln <= 0f && Vector2.Distance(en.Pos, player.Pos) < en.Radius + cfg.playerRadius)
                    player.TakeDamage(en.ContactDamage);
            }
        }

        public void ApplySeparation(float dt)
        {
            var list = Gm.Enemies;
            float force = Cfg.enemySeparationForce;
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                var a = list[i];
                for (int j = i + 1; j < n; j++)
                {
                    var b = list[j];
                    var d = b.Pos - a.Pos;
                    float minDist = a.Radius + b.Radius;
                    float d2 = d.sqrMagnitude;
                    if (d2 >= minDist * minDist) continue;
                    if (d2 < 1e-6f)
                    {
                        float ang = i * 2.399963f + j * 1.618034f;
                        d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.01f;
                        d2 = d.sqrMagnitude;
                    }
                    float dist = Mathf.Sqrt(d2);
                    float push = Mathf.Min(minDist - dist, force * dt) * 0.5f;
                    var nrm = d / dist;
                    a.SetPos(a.Pos - nrm * push);
                    b.SetPos(b.Pos + nrm * push);
                }
            }
        }
    }
}
