using UnityEngine;

namespace Marchio
{
    [CreateAssetMenu(menuName = "Marchio/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Screen")]
        public float referenceHeightPx = 844f;

        [Header("Player")]
        public float playerRadius = 15f;
        public float playerMaxHP = 100f;
        public float playerSpeed = 320f;
        public float playerAccel = 2300f;
        public float playerDecel = 2900f;
        public float playerInvulnMs = 500f;
        public float playerContactDamage = 10f;

        [Header("Trail / Loop")]
        public float trailMinDist = 7f;
        public float minLoopLengthMult = 3f;
        public float closeRadiusMult = 0.5f;
        public float loopFlashMs = 380f;
        public float maxLoopLengthMult = 18.4f;
        public int loopGrowthXPStep = 3;
        public float loopGrowthPct = 0.06f;
        public float maxLoopLengthGrowthCapMult = 2.0f;
        public int loopGrowthXPChaser = 1;
        public int loopGrowthXPFast = 1;
        public int loopGrowthXPRanged = 2;
        public float comboLoopLengthStep = 0.10f;
        public float comboLoopLengthCapBonus = 1.5f;

        [Header("Barrier")]
        public float barrierDurationMs = 3000f;
        public float barrierDps = 30f;
        public float deadTrailMs = 2500f;

        [Header("Touch Input")]
        public float touchAutoResumeMs = 1000f;
        public float joystickRadius = 60f;
        public float joystickDeadzone = 4f;
        

        [Header("Loop Attack")]
        public float baseLoopDamage = 25f;
        public float areaSmallMaxPx2 = 9000f;
        public float areaMediumMaxPx2 = 25000f;
        public float areaMultSmall = 1.0f;
        public float areaMultMedium = 1.5f;
        public float areaMultLarge = 2.0f;
        public int multiKillThreshold1 = 3;
        public float multiKillBonus1 = 0.25f;
        public int multiKillThreshold2 = 5;
        public float multiKillBonus2 = 0.5f;
        public float electricBorderRadius = 42f;
        public float electricBorderRadiusStep = 15f;
        public float electricBorderDamageMult = 0.5f;
        public float fillDamagePerLevel = 0.30f;
        public float burnDpsPerLevel = 5f;
        public float burnDurationS = 3f;
        public float freezeDurationS = 2f;
        public float freezeSlowFactor = 0.5f;
        public float healPerLevel = 5f;
        public float biggerMultiplierPerLevel = 0.5f;

        [Header("Auto Attack")]
        public float autoAttackCooldownMs = 450f;
        public float autoAttackDamage = 13f;
        public float autoAttackProjectileSpeed = 340f;

        [Header("Chaser")]
        public float chaserHP = 50f;
        public float chaserSpeed = 90f;
        public float chaserR = 14f;
        public float chaserFireIntervalMs = 1900f;
        public float chaserProjectileSpeed = 130f;
        public float chaserProjectileDamage = 5f;
        public float chaserFireMinDist = 70f;

        [Header("Fast")]
        public float fastHP = 30f;
        public float fastSpeed = 150f;
        public float fastR = 12f;
        public float fastFireIntervalMs = 1700f;
        public float fastProjectileSpeed = 150f;
        public float fastProjectileDamage = 5f;
        public float fastFireMinDist = 70f;

        [Header("Ranged")]
        public float rangedHP = 60f;
        public float rangedSpeed = 60f;
        public float rangedR = 15f;
        public float rangedPreferredDist = 190f;
        public float rangedFireIntervalMs = 1400f;
        public float rangedProjectileSpeed = 150f;
        public float rangedProjectileDamage = 8f;

        [Header("Waves")]
        public float waveHpScalePerWave = 0.06f;
        public float waveClearDelayMs = 1000f;
        public float enemySpawnStaggerMs = 160f;
        public float enemySteerJitterRad = 0.40f;
        public float enemySpeedVariance = 0.20f;
        public float rangedPreferredDistJitter = 40f;
        public float enemySeparationForce = 220f;
        public float spawnPadPx = 34f;
        public int upgradeEveryNWaves = 3;
        public int bossWave = 3;

        [Header("Boss")]
        public float bossHP = 1300f;
        public float bossR = 34f;
        public float bossSpeed = 70f;
        public float bossContactDamage = 18f;
        public float bossDashContactDamage = 30f;
        public float bossAttackCooldownMs = 2200f;
        public float bossDashTelegraphMs = 550f;
        public float bossDashSpeed = 480f;
        public float bossDashDurationMs = 450f;
        public float bossBurstTelegraphMs = 450f;
        public int bossBurstCount = 12;
        public float bossBurstProjSpeed = 170f;
        public float bossBurstProjDamage = 16f;
        public float bossBurstRecoverMs = 300f;
        public float bossHomingTelegraphMs = 500f;
        public float bossHomingSpeed = 210f;
        public float bossHomingTurnRate = 2.6f;
        public float bossHomingDamage = 16f;
        public float bossHomingLifeS = 2f;
        public float bossHomingRecoverMs = 350f;
        public float bossArenaRadius = 820f;

        [Header("Projectiles")]
        public float enemyProjectileRadius = 5f;
        public float playerProjectileRadius = 4f;
        public float bossBurstProjectileRadius = 6f;
        public float bossHomingProjectileRadius = 7f;
        public float projectileDespawnPadPx = 40f;

        [Header("Juice")]
        public float hitstopBaseMs = 50f;
        public float shakeBase = 3f;

        [Header("Colors")]
        public Color bg = Hex("#05070d");
        public Color player = Hex("#4de3ff");
        public Color trail = Hex("#7dfcff");
        public Color loopFill = new Color(125f / 255f, 252f / 255f, 1f, 0.22f);
        public Color loopEdge = Hex("#7dfcff");
        public Color chaser = Hex("#ff2e88");
        public Color fast = Hex("#ffb020");
        public Color ranged = Hex("#a06bff");
        public Color enemyProjectile = Hex("#c98bff");
        public Color playerProjectile = Hex("#4de3ff");
        public Color electricBorderSpark = Hex("#fff2a8");
        public Color hpBad = Hex("#ff4d6d");
        public Color telegraph = Hex("#ff4d6d");

        public float PlayerWidth => playerRadius * 2f;
        public float MinLoopLength => PlayerWidth * minLoopLengthMult;
        public float CloseRadius => PlayerWidth * closeRadiusMult;
        public float BaseMaxLoopLength => PlayerWidth * maxLoopLengthMult;

        public float ElectricBorderRadius(int level)
        {
            if (level <= 0) return 0f;
            return electricBorderRadius + (level - 1) * electricBorderRadiusStep;
        }

        public Color EnemyColor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Fast: return fast;
                case EnemyKind.Ranged: return ranged;
                default: return chaser;
            }
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
