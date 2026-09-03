using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public enum UpgradeId { FillDamage, BurningFill, FreezeFill, ElectricBorder, HealFill, BiggerMultiplier }

    public sealed class UpgradeManager : MonoBehaviour
    {
        public const int Count = 6;

        static readonly string[] Names =
        {
            "Fill Damage", "Burning Fill", "Freeze Fill", "Electric Border", "Heal Fill", "Bigger Multiplier"
        };

        static readonly string[] Descriptions =
        {
            "+%30 ilmek hasarı",
            "İlmek isabetleri 3 sn yanma verir",
            "İlmek isabetleri 2 sn %50 yavaşlatır",
            "Sınıra yakın düşmanlar da hasar alır (her seviye menzili büyütür)",
            "İlmekle düşman öldürünce +5 can (seviye başına)",
            "Büyük alan çarpanı artar"
        };

        static readonly string[] ShortNames = { "Fill+", "Burn", "Freeze", "E.Border", "Heal", "BigMult" };

        readonly int[] levels = new int[Count];
        readonly List<UpgradeId> choices = new List<UpgradeId>(3);
        readonly List<UpgradeId> pool = new List<UpgradeId>(Count);

        public IReadOnlyList<UpgradeId> Choices => choices;

        public void ResetState()
        {
            for (int i = 0; i < Count; i++) levels[i] = 0;
            choices.Clear();
        }

        public int Level(UpgradeId id) => levels[(int)id];

        public void Apply(UpgradeId id) => levels[(int)id]++;

        public void RollThree()
        {
            pool.Clear();
            for (int i = 0; i < Count; i++) pool.Add((UpgradeId)i);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            choices.Clear();
            for (int i = 0; i < 3; i++) choices.Add(pool[i]);
        }

        public static string NameOf(UpgradeId id) => Names[(int)id];
        public static string DescriptionOf(UpgradeId id) => Descriptions[(int)id];
        public static string ShortNameOf(UpgradeId id) => ShortNames[(int)id];

        public static Color AccentOf(UpgradeId id, GameConfig cfg)
        {
            switch (id)
            {
                case UpgradeId.FillDamage: return cfg.fast;
                case UpgradeId.BurningFill: return cfg.chaser;
                case UpgradeId.FreezeFill: return cfg.trail;
                case UpgradeId.ElectricBorder: return cfg.electricBorderSpark;
                case UpgradeId.HealFill: return cfg.ranged;
                default: return cfg.player;
            }
        }
    }
}
