using NUnit.Framework;
using UnityEngine;

namespace Marchio.Tests
{
    public class LoopDamageTests
    {
        GameConfig cfg;

        [SetUp]
        public void SetUp() => cfg = ScriptableObject.CreateInstance<GameConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(cfg);

        [Test]
        public void SmallAreaNoEnemiesIsBaseDamage()
        {
            Assert.AreEqual(25f, LoopDamage.Compute(cfg, 1000f, 0, 0, 0), 1e-4f);
        }

        [Test]
        public void MediumAreaMultiplies1point5()
        {
            Assert.AreEqual(37.5f, LoopDamage.Compute(cfg, 9000f, 0, 0, 0), 1e-4f);
        }

        [Test]
        public void LargeAreaWithBiggerMultiplierLevel2()
        {
            Assert.AreEqual(25f * 3f, LoopDamage.Compute(cfg, 25000f, 0, 0, 2), 1e-4f);
        }

        [Test]
        public void MultiKillBonusesDoNotStack()
        {
            Assert.AreEqual(25f * 1.25f, LoopDamage.Compute(cfg, 100f, 3, 0, 0), 1e-4f);
            Assert.AreEqual(25f * 1.5f, LoopDamage.Compute(cfg, 100f, 5, 0, 0), 1e-4f);
        }

        [Test]
        public void FillDamageAdds30PercentPerLevel()
        {
            Assert.AreEqual(25f * 1.6f, LoopDamage.Compute(cfg, 100f, 0, 2, 0), 1e-4f);
        }

        [Test]
        public void ElectricBorderRadiusGrowsPerLevel()
        {
            Assert.AreEqual(0f, cfg.ElectricBorderRadius(0));
            Assert.AreEqual(42f, cfg.ElectricBorderRadius(1));
            Assert.AreEqual(72f, cfg.ElectricBorderRadius(3));
        }

        [Test]
        public void DerivedLoopLengthsMatchPrototype()
        {
            Assert.AreEqual(90f, cfg.MinLoopLength, 1e-4f);
            Assert.AreEqual(15f, cfg.CloseRadius, 1e-4f);
            Assert.AreEqual(552f, cfg.BaseMaxLoopLength, 1e-3f);
        }
    }
}
