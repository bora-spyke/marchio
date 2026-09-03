using System;
using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    [Serializable]
    public struct SpawnEntry
    {
        public EnemyTypeSO type;
        public int count;
    }

    [Serializable]
    public struct ScalingRule
    {
        public EnemyTypeSO type;
        public int baseCount;
        public float perWave;
    }

    [Serializable]
    public sealed class WaveEntry
    {
        public SpawnEntry[] spawns = Array.Empty<SpawnEntry>();
        public EnemyTypeSO bossAfter;
    }

    [CreateAssetMenu(menuName = "Marchio/Wave Table", fileName = "WaveTable")]
    public sealed class WaveTableSO : ScriptableObject
    {
        public WaveEntry[] waves = Array.Empty<WaveEntry>();
        public ScalingRule[] beyondTable = Array.Empty<ScalingRule>();

        public void Compose(int wave, List<EnemyTypeSO> into)
        {
            into.Clear();
            if (wave >= 1 && wave <= waves.Length)
            {
                foreach (var s in waves[wave - 1].spawns)
                    for (int i = 0; i < s.count; i++) into.Add(s.type);
                return;
            }
            int extra = wave - waves.Length;
            foreach (var r in beyondTable)
            {
                int n = r.baseCount + Mathf.FloorToInt(extra * r.perWave);
                for (int i = 0; i < n; i++) into.Add(r.type);
            }
        }

        public EnemyTypeSO BossAfter(int wave)
        {
            return wave >= 1 && wave <= waves.Length ? waves[wave - 1].bossAfter : null;
        }
    }
}
