using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Marchio.Tests
{
    public class WaveTableTests
    {
        EnemyTypeSO chaser, fast, ranged, boss;
        WaveTableSO table;
        readonly List<EnemyTypeSO> buffer = new List<EnemyTypeSO>();

        [SetUp]
        public void SetUp()
        {
            chaser = ScriptableObject.CreateInstance<EnemyTypeSO>();
            fast = ScriptableObject.CreateInstance<EnemyTypeSO>();
            ranged = ScriptableObject.CreateInstance<EnemyTypeSO>();
            boss = ScriptableObject.CreateInstance<EnemyTypeSO>();
            table = ScriptableObject.CreateInstance<WaveTableSO>();
            table.waves = new[]
            {
                new WaveEntry { spawns = new[] { new SpawnEntry { type = chaser, count = 7 } } },
                new WaveEntry { spawns = new[] { new SpawnEntry { type = chaser, count = 8 }, new SpawnEntry { type = fast, count = 3 } } },
                new WaveEntry { spawns = new[] { new SpawnEntry { type = chaser, count = 7 }, new SpawnEntry { type = fast, count = 4 }, new SpawnEntry { type = ranged, count = 3 } }, bossAfter = boss }
            };
            table.beyondTable = new[]
            {
                new ScalingRule { type = chaser, baseCount = 7, perWave = 1.5f },
                new ScalingRule { type = fast, baseCount = 4, perWave = 1.0f },
                new ScalingRule { type = ranged, baseCount = 3, perWave = 0.8f }
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in new Object[] { chaser, fast, ranged, boss, table }) Object.DestroyImmediate(o);
        }

        int Count(EnemyTypeSO t) => buffer.Count(x => x == t);

        [Test]
        public void TableWavesMatchPrototype()
        {
            table.Compose(1, buffer);
            Assert.AreEqual((7, 0, 0), (Count(chaser), Count(fast), Count(ranged)));
            table.Compose(3, buffer);
            Assert.AreEqual((7, 4, 3), (Count(chaser), Count(fast), Count(ranged)));
        }

        [Test]
        public void BeyondTableScalesLikePrototype()
        {
            table.Compose(6, buffer);
            Assert.AreEqual((11, 7, 5), (Count(chaser), Count(fast), Count(ranged)));
        }

        [Test]
        public void BossOnlyAfterFlaggedWave()
        {
            Assert.IsNull(table.BossAfter(2));
            Assert.AreSame(boss, table.BossAfter(3));
            Assert.IsNull(table.BossAfter(4));
        }
    }
}
