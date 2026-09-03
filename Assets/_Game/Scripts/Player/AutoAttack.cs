using UnityEngine;

namespace Marchio
{
    public sealed class AutoAttack : MonoBehaviour
    {
        float cooldown;

        public void ResetState() => cooldown = 0f;

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
            var dir = nearest.Pos - from;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();
            var p = gm.PlayerProjectiles.Get();
            p.Init(from, dir * cfg.autoAttackProjectileSpeed, cfg.playerProjectileRadius, cfg.autoAttackDamage, false, false, 0f, cfg.playerProjectile);
            cooldown = cfg.autoAttackCooldownMs;
        }
    }
}
