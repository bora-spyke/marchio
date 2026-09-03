using UnityEngine;

namespace Marchio
{
    public enum EnemyBehavior { Chase, KeepDistance }

    [CreateAssetMenu(menuName = "Marchio/Enemy Type", fileName = "EnemyType")]
    public sealed class EnemyTypeSO : ScriptableObject
    {
        public string displayName = "Chaser";
        public Enemy prefab;
        public EnemyBehavior behavior = EnemyBehavior.Chase;

        [Header("Stats (wave 1 base, HP scales per wave)")]
        public float hp = 50f;
        public float speed = 90f;
        public float radius = 14f;
        public float contactDamage = 10f;
        public int xp = 1;
        public float score = 8f;
        public bool ignoresBarriers;

        [Header("Steering")]
        [Tooltip("Seconds between move/facing target updates")]
        public float retargetS = 1.5f;
        public float turnDegPerS = 540f;

        [Header("Firing")]
        public bool fires = true;
        public Projectile projectilePrefab;
        public float fireIntervalMs = 1900f;
        public float projectileSpeed = 130f;
        public float projectileDamage = 5f;
        public float fireMinDist = 70f;
        public Vector2 initialFireDelayMs = new Vector2(300f, 1100f);

        [Header("Keep Distance behavior")]
        public float preferredDist = 190f;
        public float preferredDistJitter = 40f;
        public float retreatFraction = 0.7f;
    }
}
