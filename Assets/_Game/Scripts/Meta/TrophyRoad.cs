using System;
using UnityEngine;

namespace Marchio
{
    public sealed class TrophyRoad
    {
        const string Key = "marchio.trophy.total";

        readonly RunPreset preset;
        readonly bool persist;

        public float Total { get; private set; }
        public int Claimed { get; private set; }
        public event Action<TrophyNode> Unlocked;

        public TrophyRoad(RunPreset preset, bool persist = true)
        {
            this.preset = preset;
            this.persist = persist;
            Total = persist ? PlayerPrefs.GetFloat(Key, 0f) : 0f;
            Claimed = 0;
            while (HasPending) Claimed++;
        }

        public TrophyNode[] Nodes => preset.nodes;
        public bool HasPending => Claimed < preset.nodes.Length && Total >= preset.nodes[Claimed].threshold;
        public bool HasNext => Claimed < preset.nodes.Length;
        public TrophyNode Next => preset.nodes[Mathf.Min(Claimed, preset.nodes.Length - 1)];
        public float PrevThreshold => Claimed == 0 ? 0f : preset.nodes[Claimed - 1].threshold;

        public float ProgressToNext(float total)
        {
            if (!HasNext) return 1f;
            float prev = PrevThreshold, next = Next.threshold;
            return next <= prev ? 1f : Mathf.Clamp01((total - prev) / (next - prev));
        }

        public float RemainingToNext => HasNext ? Mathf.Max(0f, Next.threshold - Total) : 0f;

        public void Bank(float amount)
        {
            Total += amount;
            if (persist) PlayerPrefs.SetFloat(Key, Total);
        }

        public void Flush()
        {
            if (persist) PlayerPrefs.Save();
        }

        public TrophyNode ClaimNext()
        {
            var node = preset.nodes[Claimed++];
            Unlocked?.Invoke(node);
            return node;
        }

        public void Reset()
        {
            Total = 0f;
            Claimed = 0;
            if (persist) { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
        }

        public bool Has(TrophyReward reward) => Count(reward) > 0;

        public int Count(TrophyReward reward)
        {
            int n = 0;
            for (int i = 0; i < Claimed; i++) if (preset.nodes[i].reward == reward) n++;
            return n;
        }

        public float DamageMult => Has(TrophyReward.DamageBoost) ? preset.step1DamageMult : 1f;
        public float SpeedMult => Has(TrophyReward.SpeedUnlock) ? 1f : preset.speedPreStep2;
        public float MaxHpMult => 1f + (Has(TrophyReward.MaxHpUp) ? preset.microHpBonus : 0f) + (Has(TrophyReward.NewCart) ? preset.step4HpBonus : 0f);
        public float TrailWidthMult => Has(TrophyReward.TrailWiden) ? preset.step3TrailWidthMult : 1f;
        public float TrailLengthMult => Has(TrophyReward.TrailWiden) ? preset.step3TrailLengthMult : 1f;
        public int ExtraRevives => Count(TrophyReward.ExtraRevive);
        public int CartTier => Has(TrophyReward.NewCart) ? 1 : 0;
    }
}
