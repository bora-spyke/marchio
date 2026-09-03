using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Marchio.Tests
{
    public class RunTests
    {
        RunPreset preset;
        TrophyRoad trophy;
        Run run;

        [SetUp]
        public void SetUp()
        {
            preset = ScriptableObject.CreateInstance<RunPreset>();
            preset.nodes = new[]
            {
                new TrophyNode { title = "Step 1", threshold = 70f, macro = true, reward = TrophyReward.DamageBoost },
                new TrophyNode { title = "Micro", threshold = 160f, macro = false, reward = TrophyReward.ExtraRevive },
                new TrophyNode { title = "Step 2", threshold = 500f, macro = true, reward = TrophyReward.SpeedUnlock }
            };
            trophy = new TrophyRoad(preset, persist: false);
            run = new Run(preset, trophy);
            run.Start(1);
            run.BeginLevel(1);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(preset);

        [Test]
        public void ThresholdFollowsCurve()
        {
            Assert.AreEqual(180f, preset.Threshold(1), 1e-3f);
            Assert.AreEqual(243f, preset.Threshold(2), 1e-3f);
            Assert.AreEqual(328.05f, preset.Threshold(3), 1e-2f);
            Assert.IsTrue(preset.IsVictoryLap(5));
            Assert.IsFalse(preset.IsVictoryLap(4));
        }

        [Test]
        public void ScoreIsWeightedAndBankedImmediately()
        {
            run.AddScore(ScoreSource.Kill, 10f);
            run.AddScore(ScoreSource.Area, 10f);
            Assert.AreEqual(10f, run.LevelScore, 1e-4f);
            Assert.AreEqual(10f, trophy.Total, 1e-4f);
        }

        [Test]
        public void CompletionBonusBanksButDeathDoesNot()
        {
            run.AddScore(ScoreSource.Kill, 300f);
            Assert.IsTrue(run.ThresholdReached);
            float bonus = run.CompleteLevel();
            Assert.AreEqual(180f * 0.35f, bonus, 1e-3f);
            Assert.AreEqual(180f + bonus, trophy.Total, 1e-3f);
            run.BeginLevel(2);
            run.AddScore(ScoreSource.Kill, 100f);
            Assert.AreEqual(60f * 0.35f, run.MissedBonus, 1e-3f);
            Assert.AreEqual(180f + bonus + 60f, trophy.Total, 1e-3f);
        }

        [Test]
        public void AreaScoreUsesTiersAndStreak()
        {
            Assert.AreEqual(1f, preset.AreaMultiplier(100f));
            Assert.AreEqual(1.5f, preset.AreaMultiplier(4000f));
            Assert.AreEqual(2.5f, preset.AreaMultiplier(12000f));
            Assert.AreEqual(4f, preset.AreaMultiplier(30000f));
            run.Streak = 1;
            Assert.AreEqual(1000f * 0.001f, run.AreaScore(1000f), 1e-4f);
            run.Streak = 9;
            Assert.AreEqual(30000f * 0.001f * 4f * 1.6f, run.AreaScore(30000f), 1e-3f);
        }

        [Test]
        public void TrophyNodesBecomePendingWhenCrossedAndClaimInOrder()
        {
            Assert.IsFalse(trophy.HasPending);
            trophy.Bank(200f);
            Assert.IsTrue(trophy.HasPending);
            Assert.AreEqual("Step 1", trophy.ClaimNext().title);
            Assert.IsTrue(trophy.HasPending);
            Assert.AreEqual("Micro", trophy.ClaimNext().title);
            Assert.IsFalse(trophy.HasPending);
            Assert.AreEqual(1.6f, trophy.DamageMult, 1e-4f);
            Assert.AreEqual(1, trophy.ExtraRevives);
            Assert.AreEqual(0.72f, trophy.SpeedMult, 1e-4f);
            Assert.AreEqual(300f, trophy.RemainingToNext, 1e-4f);
        }

        [Test]
        public void SpawnRateRampsByPhaseAndCaps()
        {
            preset.spawnPhases = new[]
            {
                new SpawnPhase { startS = 0f, rateMult = 1f },
                new SpawnPhase { startS = 15f, rateMult = 1.5f },
                new SpawnPhase { startS = 30f, rateMult = 2f }
            };
            preset.rampPerSAfterLastPhase = 0.1f;
            preset.spawnRampCapS = 90f;
            Assert.AreEqual(1f, preset.SpawnRateMult(5f), 1e-4f);
            Assert.AreEqual(1.5f, preset.SpawnRateMult(20f), 1e-4f);
            Assert.AreEqual(2f, preset.SpawnRateMult(30f), 1e-4f);
            Assert.AreEqual(3f, preset.SpawnRateMult(40f), 1e-4f);
            Assert.AreEqual(8f, preset.SpawnRateMult(90f), 1e-4f);
            Assert.AreEqual(8f, preset.SpawnRateMult(500f), 1e-4f);
        }

        [Test]
        public void CardPoolDropsCappedCardsAndOffersUpToThree()
        {
            var pool = new CardPool(new[]
            {
                new CardDef { Title = "A", Cap = 1 },
                new CardDef { Title = "B", Cap = 2 },
                new CardDef { Title = "C", Cap = 2 },
                new CardDef { Title = "D", Cap = 2 }
            });
            pool.RollThree();
            Assert.AreEqual(3, pool.Choices.Count);
            pool.Apply(0);
            pool.Apply(0);
            Assert.AreEqual(1, pool.Level(0));
            pool.RollThree();
            Assert.AreEqual(3, pool.Choices.Count);
            Assert.IsFalse(pool.Choices.Contains(0));
            pool.Apply(1); pool.Apply(1); pool.Apply(2); pool.Apply(2);
            pool.RollThree();
            Assert.AreEqual(1, pool.Choices.Count);
            Assert.AreEqual(3, pool.Choices[0]);
        }

        [Test]
        public void InterceptLeadsMovingTargetAndFallsBackToDirect()
        {
            var still = AutoAttack.Intercept(new Vector2(200f, 0f), Vector2.zero, 340f);
            Assert.AreEqual(new Vector2(200f, 0f), still);
            var lead = AutoAttack.Intercept(new Vector2(200f, 0f), new Vector2(0f, 90f), 340f);
            float t = lead.magnitude / 340f;
            Assert.AreEqual(90f * t, lead.y, 1e-2f, "aim point equals where the target will be when the bullet arrives");
            Assert.AreEqual(200f, lead.x, 1e-3f);
            var tooFast = AutoAttack.Intercept(new Vector2(200f, 0f), new Vector2(500f, 0f), 340f);
            Assert.AreEqual(new Vector2(200f, 0f), tooFast, "target outrunning the bullet falls back to direct aim");
        }
    }
}
