using UnityEngine;

namespace Marchio
{
    public enum UpgradeId { FillDamage, BurningFill, FreezeFill, ElectricBorder, HealFill, BiggerMultiplier }
    public enum PowerId { Overload, RapidFeed, LiveWire, Ricochet, DevilsBargain, IronHull }

    public sealed class UpgradeManager : MonoBehaviour
    {
        public CardPool Fill { get; private set; }
        public CardPool Power { get; private set; }

        public void Init(GameConfig cfg)
        {
            Fill = new CardPool(new[]
            {
                new CardDef { Title = "Fill Damage", Description = "+30% Fill damage.", Cap = 2, Accent = cfg.fast },
                new CardDef { Title = "Burning Fill", Description = "Fill ignites enemies for 3s.", Cap = 2, Accent = cfg.chaser },
                new CardDef { Title = "Freeze Fill", Description = "Fill slows enemies by half for 2s.", Cap = 2, Accent = cfg.trail },
                new CardDef { Title = "Electric Border", Description = "Enemies near the edge take damage too.", Cap = 2, Accent = cfg.electricBorderSpark },
                new CardDef { Title = "Heal Fill", Description = "Fill kills restore 5 HP.", Cap = 2, Accent = cfg.ranged },
                new CardDef { Title = "Bigger Multiplier", Description = "Large-area multiplier grows.", Cap = 2, Accent = cfg.player }
            });
            Power = new CardPool(new[]
            {
                new CardDef { Title = "Overload", Description = "Your shots hit 50% harder.", Cap = 3, Accent = cfg.fast },
                new CardDef { Title = "Rapid Feed", Description = "Fire 50% more rounds.", Cap = 3, Accent = cfg.playerProjectile },
                new CardDef { Title = "Live Wire", Description = "Your trail burns whatever touches it.", Cap = 2, Accent = cfg.trail },
                new CardDef { Title = "Ricochet", Description = "Shots bounce to one more enemy.", Cap = 2, Accent = cfg.electricBorderSpark },
                new CardDef { Title = "Devil's Bargain", Description = "Double damage. Lose 30% max health.", Cap = 1, Accent = cfg.hpBad, Distinct = true },
                new CardDef { Title = "Iron Hull", Description = "+20% max health.", Cap = 2, Accent = cfg.ranged }
            });
        }

        public void ResetState()
        {
            Fill.Reset();
            Power.Reset();
        }

        public int Level(UpgradeId id) => Fill.Level((int)id);
        public int Level(PowerId id) => Power.Level((int)id);
    }
}
