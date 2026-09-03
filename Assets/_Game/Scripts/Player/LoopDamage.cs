using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public static class LoopDamage
    {
        static readonly List<Enemy> insideBuffer = new List<Enemy>(64);

        public static float Compute(GameConfig cfg, float area, int insideCount, int fillDamageLevel, int biggerMultiplierLevel)
        {
            float areaMult = cfg.areaMultSmall;
            if (area >= cfg.areaMediumMaxPx2) areaMult = cfg.areaMultLarge + biggerMultiplierLevel * cfg.biggerMultiplierPerLevel;
            else if (area >= cfg.areaSmallMaxPx2) areaMult = cfg.areaMultMedium;

            float multiKill = 0f;
            if (insideCount >= cfg.multiKillThreshold2) multiKill = cfg.multiKillBonus2;
            else if (insideCount >= cfg.multiKillThreshold1) multiKill = cfg.multiKillBonus1;

            float fillMult = 1f + fillDamageLevel * cfg.fillDamagePerLevel;
            return cfg.baseLoopDamage * areaMult * (1f + multiKill) * fillMult;
        }

        public static void Resolve(List<Vector2> poly)
        {
            var gm = GameManager.I;
            var cfg = gm.Config;
            var up = gm.Upgrades;

            gm.Barriers.Get().Init(poly);

            insideBuffer.Clear();
            for (int i = 0; i < gm.Enemies.Count; i++)
            {
                var en = gm.Enemies[i];
                if (!en.Dead && PolygonMath.PointInPolygon(en.Pos, poly)) insideBuffer.Add(en);
            }
            int count = insideBuffer.Count;
            float area = PolygonMath.Area(poly);
            float total = Compute(cfg, area, count, up.Level(UpgradeId.FillDamage), up.Level(UpgradeId.BiggerMultiplier));

            int killedInside = 0;
            for (int i = 0; i < insideBuffer.Count; i++)
                if (insideBuffer[i].ApplyLoopHit(total)) killedInside++;

            float borderR = cfg.ElectricBorderRadius(up.Level(UpgradeId.ElectricBorder));
            if (borderR > 0f)
            {
                for (int i = 0; i < gm.Enemies.Count; i++)
                {
                    var en = gm.Enemies[i];
                    if (en.Dead || insideBuffer.Contains(en)) continue;
                    if (PolygonMath.DistToBoundary(en.Pos, poly) <= borderR)
                    {
                        en.ApplyLoopHit(total * cfg.electricBorderDamageMult);
                        gm.Fx.Burst(en.Pos, cfg.electricBorderSpark, 6);
                    }
                }
            }

            int heal = up.Level(UpgradeId.HealFill);
            if (heal > 0 && killedInside > 0)
            {
                gm.Player.Heal(cfg.healPerLevel * heal);
                gm.Fx.Burst(gm.Player.Pos, cfg.ranged, 8);
            }

            if (count > 0) gm.AddCombo(count);

            var centroid = PolygonMath.Centroid(poly);
            float magnitude = count == 0 ? 0f : count >= 5 ? 2f : count >= 3 ? 1f : 0.5f;
            gm.AddJuice(cfg.hitstopBaseMs * (1f + magnitude), cfg.shakeBase * (1f + magnitude));
            gm.Fx.BurstAlongPath(poly, cfg.loopEdge, 10 + count * 6);
            for (int i = 0; i < insideBuffer.Count; i++)
                gm.Fx.BurstToward(insideBuffer[i].Pos, centroid, cfg.loopEdge, 5);
        }
    }
}
