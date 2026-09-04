using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public sealed class WaveManager : MonoBehaviour
    {
        readonly List<EnemyTypeSO> spawnQueue = new List<EnemyTypeSO>(64);
        LevelConfig level;
        float spawnTimer;
        float clearTimer;
        float lapAccumulator;
        int waveTotal;

        public int WaveIndex { get; private set; }
        public int WaveCount { get; private set; }

        GameManager Gm => GameManager.I;
        GameConfig Cfg => GameManager.I.Config;

        public float WaveProgress
        {
            get
            {
                if (waveTotal <= 0) return 0f;
                return Mathf.Clamp01(1f - (spawnQueue.Count + LiveEnemies()) / (float)waveTotal);
            }
        }

        public void ResetState()
        {
            spawnQueue.Clear();
            level = null;
            spawnTimer = 0f;
            clearTimer = 0f;
            lapAccumulator = 0f;
            waveTotal = 0;
            WaveIndex = 0;
            WaveCount = 0;
        }

        public void BeginLevel()
        {
            ResetState();
            var run = Gm.Run;
            if (run.IsVictoryLap || Gm.Preset.levels.Length == 0) return;
            level = Gm.Preset.LevelFor(run.Level);
            WaveCount = level.waves.Length;
            if (WaveCount > 0) StartWave(0);
        }

        void StartWave(int index)
        {
            WaveIndex = index;
            spawnQueue.Clear();
            foreach (var s in level.waves[index].spawns)
                for (int i = 0; i < s.count; i++) if (s.type != null) spawnQueue.Add(s.type);
            for (int i = spawnQueue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (spawnQueue[i], spawnQueue[j]) = (spawnQueue[j], spawnQueue[i]);
            }
            waveTotal = spawnQueue.Count;
            spawnTimer = 0f;
            clearTimer = 0f;
        }

        float HpMult => 1f + (Gm.Run.Level - 1) * Gm.Preset.hpScalePerLevel;

        public void Tick(float dt)
        {
            if (!Cfg.spawnEnemies) return;
            var run = Gm.Run;
            if (run.IsVictoryLap) { TickVictoryLap(dt); return; }
            if (level == null || run.LevelCleared) return;

            if (spawnQueue.Count > 0)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    spawnTimer = level.waves[WaveIndex].spawnIntervalS;
                    var type = spawnQueue[spawnQueue.Count - 1];
                    spawnQueue.RemoveAt(spawnQueue.Count - 1);
                    Spawn(type, SpawnPoint(), HpMult);
                }
                return;
            }

            if (LiveEnemies() > 0) { clearTimer = 0f; return; }
            clearTimer += dt;
            if (clearTimer < Gm.Preset.waveClearDelayS) return;
            if (WaveIndex + 1 < WaveCount) StartWave(WaveIndex + 1);
            else { WaveIndex = WaveCount; run.LevelCleared = true; }
        }

        void TickVictoryLap(float dt)
        {
            var preset = Gm.Preset;
            float rate = preset.baseSpawnPerS * preset.SpawnRateMult(Gm.Run.LevelTime) * preset.victoryLapDensity;
            lapAccumulator += rate * dt;
            while (lapAccumulator >= 1f)
            {
                lapAccumulator -= 1f;
                var type = PickType(preset, Gm.Run.LevelTime);
                if (type != null) Spawn(type, SpawnPoint(), HpMult);
            }
        }

        int LiveEnemies()
        {
            int n = 0;
            var list = Gm.Enemies;
            for (int i = 0; i < list.Count; i++) if (!list[i].Dead) n++;
            return n;
        }

        static EnemyTypeSO PickType(RunPreset preset, float levelTime)
        {
            int idx = preset.PhaseIndex(levelTime);
            if (idx < 0) return null;
            var weights = preset.spawnPhases[idx].weights;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i].weight;
            float roll = Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i].weight;
                if (roll <= 0f) return weights[i].type;
            }
            return weights.Length > 0 ? weights[weights.Length - 1].type : null;
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

        Enemy Spawn(EnemyTypeSO type, Vector2 pos, float hpMult)
        {
            var en = Gm.GetEnemy(type);
            en.Init(type, pos, hpMult);
            Gm.Enemies.Add(en);
            return en;
        }

        public void TickEnemies(float dt)
        {
            var gm = Gm;
            var cfg = Cfg;
            var player = gm.Player;
            var trail = gm.Trail;
            float cutRadius = cfg.trailHitRadius * gm.Trophy.TrailWidthMult;
            int liveWire = gm.Upgrades.Level(PowerId.LiveWire);
            for (int i = gm.Enemies.Count - 1; i >= 0; i--)
            {
                var en = gm.Enemies[i];
                if (en.Dead)
                {
                    if (en.TickDead(dt)) gm.ReleaseEnemy(en);
                    continue;
                }
                en.Tick(dt);
                if (en.Dead) continue;

                if (!en.IgnoresBarriers)
                {
                    var barriers = gm.Barriers.Active;
                    for (int b = 0; b < barriers.Count; b++)
                        barriers[b].PushOut(en, dt);
                }

                if (liveWire > 0 && trail.Touches(en.Pos, en.Radius + cutRadius))
                    en.ApplyBurn(gm.Preset.liveWireBurnDps * liveWire, cfg.burnDurationS);

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
                if (a.Dead) continue;
                for (int j = i + 1; j < n; j++)
                {
                    var b = list[j];
                    if (b.Dead) continue;
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
