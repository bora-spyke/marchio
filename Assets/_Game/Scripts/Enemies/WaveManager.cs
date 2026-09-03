using UnityEngine;

namespace Marchio
{
    public sealed class WaveManager : MonoBehaviour
    {
        float spawnAccumulator;

        public Enemy ActiveElite { get; private set; }

        GameManager Gm => GameManager.I;
        GameConfig Cfg => GameManager.I.Config;

        public void ResetState()
        {
            spawnAccumulator = 0f;
            ActiveElite = null;
        }

        public void BeginLevel()
        {
            spawnAccumulator = 0f;
            ActiveElite = null;
        }

        float HpMult => 1f + (Gm.Run.Level - 1) * Gm.Preset.hpScalePerLevel;

        public void Tick(float dt)
        {
            if (!Cfg.spawnEnemies) return;
            var run = Gm.Run;
            var preset = Gm.Preset;
            float rate = preset.baseSpawnPerS * preset.SpawnRateMult(run.LevelTime);
            if (run.IsVictoryLap) rate *= preset.victoryLapDensity;
            spawnAccumulator += rate * dt;
            while (spawnAccumulator >= 1f)
            {
                spawnAccumulator -= 1f;
                var type = PickType(preset, run.LevelTime);
                if (type != null) Spawn(type, SpawnPoint(), HpMult);
            }

            if (!run.IsVictoryLap && !run.EliteSpawned && preset.eliteType != null && run.Level >= preset.eliteFromLevel && run.Progress >= preset.eliteAtThresholdFrac)
            {
                run.EliteSpawned = true;
                var pos = SpawnPoint();
                ActiveElite = Spawn(preset.eliteType, pos, HpMult * preset.eliteHpMult);
                Gm.Fx.Burst(pos, preset.eliteType.color, 24);
            }
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

                if (player.Invuln <= 0f && trail.Touches(en.Pos, en.Radius + cutRadius))
                {
                    if (liveWire > 0) en.ApplyBurn(gm.Preset.liveWireBurnDps * liveWire, cfg.burnDurationS);
                    gm.Fx.Burst(en.Pos, cfg.trail, 12);
                    player.TakeDamage(cfg.trailCutDamage);
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
