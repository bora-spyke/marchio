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
        public float trailHitRadius = 6f;
        public float maxLoopLengthMult = 18.4f;
        public int loopGrowthXPStep = 3;
        public float loopGrowthPct = 0.06f;
        public float maxLoopLengthGrowthCapMult = 2.0f;
        public float comboLoopLengthStep = 0.10f;
        public float comboLoopLengthCapBonus = 1.5f;

        [Header("Barrier")]
        public float barrierDurationMs = 3000f;
        public float deadTrailMs = 2500f;

        [Header("Soul Stone")]
        public float soulstonePickupRadiusMult = 2f; // pickup radius = player diameter * this

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
        public float hitStunS = 0.15f;
        public float healPerLevel = 5f;
        public float biggerMultiplierPerLevel = 0.5f;

        [Header("Auto Attack")]
        public float autoAttackCooldownMs = 450f;
        public float autoAttackDamage = 13f;
        public float autoAttackProjectileSpeed = 340f;
        public float autoAttackRangePx = 320f;

        [Header("Waves")]
        public bool spawnEnemies = true;
        public float enemySteerJitterRad = 0.40f;
        public float enemySpeedVariance = 0.20f;
        public float rangedPreferredDistJitter = 40f;
        public float enemySeparationForce = 220f;
        public float spawnPadPx = 34f;

        [Header("Projectiles")]
        public float enemyProjectileRadius = 5f;
        public float playerProjectileRadius = 4f;
        public float projectileDespawnPadPx = 40f;
        public float projectileLifeS = 4f;
        public float projectileFadeS = 0.6f;

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
        public Color skin1 = Hex("#ffd84d");
        public Color cartColorVariant = Hex("#ff7ad9");
        public Color trailVariant = Hex("#b8ff5c");
        public Color cart2 = Hex("#f4f7ff");
        public float cart2ScaleMult = 1.25f;
        public Color enemyProjectile = Hex("#c98bff");
        public Color playerProjectile = Hex("#4de3ff");
        public Color electricBorderSpark = Hex("#fff2a8");
        public Color hpBad = Hex("#ff4d6d");
        public Color telegraph = Hex("#ff4d6d");
        public Color groundTint = new Color(1f, 1f, 1f, 0.7f);
        public float groundTilePx = 100f;

        public float PlayerWidth => playerRadius * 2f;
        public float MinLoopLength => PlayerWidth * minLoopLengthMult;
        public float CloseRadius => PlayerWidth * closeRadiusMult;
        public float BaseMaxLoopLength => PlayerWidth * maxLoopLengthMult;

        public float ElectricBorderRadius(int level)
        {
            if (level <= 0) return 0f;
            return electricBorderRadius + (level - 1) * electricBorderRadiusStep;
        }


        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
