using UnityEngine;

namespace Marchio
{
    public sealed class AutoAttack : MonoBehaviour
    {
        float cooldown;

        public void ResetState() => cooldown = 0f;

        public static Vector2 Intercept(Vector2 relPos, Vector2 targetVel, float bulletSpeed)
        {
            float a = Vector2.Dot(targetVel, targetVel) - bulletSpeed * bulletSpeed;
            float b = 2f * Vector2.Dot(relPos, targetVel);
            float c = Vector2.Dot(relPos, relPos);
            float t;
            if (Mathf.Abs(a) < 1e-4f)
            {
                if (Mathf.Abs(b) < 1e-6f) return relPos;
                t = -c / b;
            }
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return relPos;
                float root = Mathf.Sqrt(disc);
                float t1 = (-b - root) / (2f * a);
                float t2 = (-b + root) / (2f * a);
                t = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
            }
            if (t <= 0f) return relPos;
            return relPos + targetVel * t;
        }

        public void Tick(float dt)
        {
            var gm = GameManager.I;
            if (cooldown > 0f) cooldown -= dt * 1000f;
            if (cooldown > 0f || gm.Enemies.Count == 0) return;

            Enemy nearest = null;
            float best = float.PositiveInfinity;
            var from = gm.Player.Pos;
            for (int i = 0; i < gm.Enemies.Count; i++)
            {
                var en = gm.Enemies[i];
                if (en.Dead) continue;
                float d = (en.Pos - from).sqrMagnitude;
                if (d < best) { best = d; nearest = en; }
            }
            if (nearest == null) return;

            var cfg = gm.Config;
            var dir = Intercept(nearest.Pos - from, nearest.Velocity, cfg.autoAttackProjectileSpeed);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();
            var p = gm.PlayerProjectiles.Get();
            p.Init(from, dir * cfg.autoAttackProjectileSpeed, cfg.playerProjectileRadius, cfg.autoAttackDamage * gm.DamageMult, false, false, 0f, cfg.playerProjectile);
            p.SetBounces(gm.Upgrades.Level(PowerId.Ricochet));
            cooldown = cfg.autoAttackCooldownMs / gm.FireRateMult;
        }
    }
}
