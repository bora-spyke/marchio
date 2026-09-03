using System.Collections.Generic;
using UnityEngine;

namespace Marchio
{
    public sealed class CardDef
    {
        public string Title;
        public string Description;
        public int Cap;
        public Color Accent;
        public bool Distinct;
    }

    public sealed class CardPool
    {
        readonly CardDef[] defs;
        readonly int[] levels;
        readonly List<int> choices = new List<int>(3);
        readonly List<int> available = new List<int>(8);

        public CardPool(CardDef[] defs)
        {
            this.defs = defs;
            levels = new int[defs.Length];
        }

        public int Count => defs.Length;
        public IReadOnlyList<int> Choices => choices;
        public CardDef Def(int id) => defs[id];
        public int Level(int id) => levels[id];
        public bool IsCapped(int id) => levels[id] >= defs[id].Cap;

        public int TotalStacks
        {
            get { int n = 0; for (int i = 0; i < levels.Length; i++) n += levels[i]; return n; }
        }

        public void Reset()
        {
            for (int i = 0; i < levels.Length; i++) levels[i] = 0;
            choices.Clear();
        }

        public void Apply(int id)
        {
            if (!IsCapped(id)) levels[id]++;
        }

        public void RollThree()
        {
            available.Clear();
            for (int i = 0; i < defs.Length; i++) if (!IsCapped(i)) available.Add(i);
            for (int i = available.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (available[i], available[j]) = (available[j], available[i]);
            }
            choices.Clear();
            for (int i = 0; i < available.Count && i < 3; i++) choices.Add(available[i]);
        }
    }
}
