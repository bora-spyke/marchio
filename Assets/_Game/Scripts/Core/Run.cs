using UnityEngine;

namespace Marchio
{
    public enum ScoreSource { Kill, Area }

    public sealed class Run
    {
        readonly RunPreset preset;
        readonly TrophyRoad trophy;

        public int Level { get; private set; }
        public float LevelScore { get; private set; }
        public float Threshold { get; private set; }
        public float LevelTime { get; set; }
        public int Streak { get; set; }
        public int RevivesLeft { get; set; }
        public bool EliteSpawned { get; set; }
        public float RunScore { get; private set; }
        public float LastLevelScore { get; private set; }
        public float LastBonus { get; private set; }
        public bool HealedOnClear { get; set; }

        public Run(RunPreset preset, TrophyRoad trophy)
        {
            this.preset = preset;
            this.trophy = trophy;
        }

        public bool IsVictoryLap => preset.IsVictoryLap(Level);
        public float Progress => Threshold > 0f ? Mathf.Clamp01(LevelScore / Threshold) : 0f;
        public float Remaining => Mathf.Max(0f, Threshold - LevelScore);
        public bool ThresholdReached => !IsVictoryLap && Threshold > 0f && LevelScore >= Threshold;
        public bool VictoryLapDone => IsVictoryLap && LevelTime >= preset.victoryLapDurationS;
        public float MissedBonus => LevelScore * preset.completionBonus;

        public void Start(int revives)
        {
            Level = 0;
            RunScore = 0f;
            RevivesLeft = revives;
            LastLevelScore = 0f;
            LastBonus = 0f;
        }

        public void BeginLevel(int level)
        {
            Level = level;
            LevelScore = 0f;
            LevelTime = 0f;
            Streak = 0;
            EliteSpawned = false;
            HealedOnClear = false;
            Threshold = IsVictoryLap ? 0f : preset.Threshold(level);
        }

        public float AddScore(ScoreSource source, float raw)
        {
            float weight = source == ScoreSource.Kill ? preset.scoreWeightKill : preset.scoreWeightArea;
            float amount = raw * weight;
            LevelScore += amount;
            RunScore += amount;
            trophy.Bank(amount);
            return amount;
        }

        public float AreaScore(float areaPx2)
        {
            return areaPx2 * preset.areaScorePerPx2 * preset.AreaMultiplier(areaPx2) * preset.StreakMultiplier(Streak);
        }

        public float CompleteLevel()
        {
            LastLevelScore = LevelScore;
            LastBonus = LevelScore * preset.completionBonus;
            RunScore += LastBonus;
            trophy.Bank(LastBonus);
            return LastBonus;
        }

        public bool HealsAfter(int level) => preset.healEveryNLevels > 0 && level % preset.healEveryNLevels == 0;
        public bool OffersFillUpgrade(int clearedLevel) => clearedLevel <= preset.fillUpgradeLastLevel;
        public bool OffersPowerUpBefore(int nextLevel) => nextLevel >= preset.powerUpUnlockLevel;
    }
}
