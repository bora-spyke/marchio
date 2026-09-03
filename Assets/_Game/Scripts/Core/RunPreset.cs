using System;
using UnityEngine;

namespace Marchio
{
    public enum TrophyReward { DamageBoost, CartColor, MaxHpUp, SpeedUnlock, ExtraRevive, TrailWiden, TrailEffect, NewCart }

    [Serializable]
    public struct TrophyNode
    {
        public string title;
        public float threshold;
        public bool macro;
        public TrophyReward reward;
    }

    [Serializable]
    public struct SpawnWeight
    {
        public EnemyTypeSO type;
        public float weight;
    }

    [Serializable]
    public struct SpawnPhase
    {
        public float startS;
        public float rateMult;
        public SpawnWeight[] weights;
    }

    [CreateAssetMenu(menuName = "Marchio/Run Preset", fileName = "RunPreset")]
    public sealed class RunPreset : ScriptableObject
    {
        [Header("Levels")]
        public float baseThreshold = 180f;
        public float scoreCurveMultiplier = 1.35f;
        public int levelCount = 4;
        public float victoryLapDurationS = 30f;
        public float victoryLapDensity = 0.35f;
        public float completionBonus = 0.35f;
        public int healEveryNLevels = 2;
        public float healAmount = 0.5f;
        public int freeRevives = 1;
        public float reviveHp = 0.5f;
        public int powerUpUnlockLevel = 2;
        public int fillUpgradeLastLevel = 4;
        public float thresholdPressureFrac = 0.85f;

        [Header("Score")]
        public float scoreWeightKill = 0.6f;
        public float scoreWeightArea = 0.4f;
        public float areaScorePerPx2 = 0.001f;
        public float[] areaTierPx2 = { 4000f, 12000f, 25000f };
        public float[] areaTierMult = { 1f, 1.5f, 2.5f, 4f };
        public float[] streakMult = { 1f, 1.2f, 1.4f, 1.6f };

        [Header("Speed")]
        public float speedPreStep2 = 0.72f;

        [Header("Spawning")]
        public float baseSpawnPerS = 0.8f;
        public SpawnPhase[] spawnPhases = Array.Empty<SpawnPhase>();
        public float rampPerSAfterLastPhase = 0.03f;
        public float spawnRampCapS = 90f;
        public float hpScalePerLevel = 0.06f;

        [Header("Trophy Road")]
        public TrophyNode[] nodes = Array.Empty<TrophyNode>();
        public float step1DamageMult = 1.6f;
        public float microHpBonus = 0.10f;
        public float step3TrailWidthMult = 1.5f;
        public float step3TrailLengthMult = 1.3f;
        public float step4HpBonus = 0.25f;

        [Header("Power-ups")]
        public float overloadPerStack = 0.5f;
        public float rapidFeedPerStack = 0.5f;
        public float liveWireBurnDps = 8f;
        public float ricochetRangePx = 320f;
        public float devilDamageMult = 2f;
        public float devilHpPenalty = 0.3f;
        public float ironHullPerStack = 0.2f;

        public float Threshold(int level) => baseThreshold * Mathf.Pow(scoreCurveMultiplier, level - 1);
        public bool IsVictoryLap(int level) => levelCount > 0 && level > levelCount;

        public float AreaMultiplier(float areaPx2)
        {
            int tier = 0;
            while (tier < areaTierPx2.Length && areaPx2 >= areaTierPx2[tier]) tier++;
            return areaTierMult[Mathf.Min(tier, areaTierMult.Length - 1)];
        }

        public float StreakMultiplier(int consecutiveClaims)
        {
            if (streakMult.Length == 0) return 1f;
            return streakMult[Mathf.Clamp(consecutiveClaims - 1, 0, streakMult.Length - 1)];
        }

        public float SpawnRateMult(float levelTime)
        {
            float t = Mathf.Min(levelTime, spawnRampCapS);
            int idx = PhaseIndex(t);
            if (idx < 0) return 1f;
            float mult = spawnPhases[idx].rateMult;
            if (idx == spawnPhases.Length - 1) mult += (t - spawnPhases[idx].startS) * rampPerSAfterLastPhase;
            return mult;
        }

        public int PhaseIndex(float levelTime)
        {
            int idx = -1;
            for (int i = 0; i < spawnPhases.Length; i++)
                if (spawnPhases[i].startS <= levelTime) idx = i;
            return idx;
        }
    }
}
