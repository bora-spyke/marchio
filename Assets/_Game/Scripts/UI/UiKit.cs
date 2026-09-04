using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    public static class UiKit
    {
        public static string Roman(int n)
        {
            switch (n)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                default: return n.ToString();
            }
        }

        public static Label Label(VisualElement parent, string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            parent.Add(l);
            return l;
        }

        public static Button Button(VisualElement parent, string text, Action onClick, string extraClass = null)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            if (extraClass != null) b.AddToClassList(extraClass);
            parent.Add(b);
            return b;
        }

        public static VisualElement Line(VisualElement parent, string name, out Label value)
        {
            var row = new VisualElement();
            row.AddToClassList("line");
            var n = new Label(name);
            n.AddToClassList("line-name");
            value = new Label("");
            value.AddToClassList("line-value");
            row.Add(n);
            row.Add(value);
            parent.Add(row);
            return row;
        }

        public static void CountUp(Label label, float from, float to, int ms, string prefix = "")
        {
            float start = Time.unscaledTime;
            label.text = prefix + Mathf.RoundToInt(from);
            label.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.unscaledTime - start) * 1000f / Mathf.Max(1, ms));
                float eased = 1f - (1f - t) * (1f - t);
                label.text = prefix + Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            }).Every(16).Until(() => Time.unscaledTime - start >= ms / 1000f + 0.02f);
        }

        public static Button Card(CardDef def, int level, Action onClick)
        {
            var card = new Button(onClick);
            card.AddToClassList("card");
            if (def.Distinct) card.AddToClassList("card--distinct");
            var bgLeft = new VisualElement();
            bgLeft.AddToClassList("card-bg");
            bgLeft.AddToClassList("card-bg--left");
            var bgRight = new VisualElement();
            bgRight.AddToClassList("card-bg");
            bgRight.AddToClassList("card-bg--right");
            card.Add(bgLeft);
            card.Add(bgRight);
            var text = new VisualElement();
            text.AddToClassList("card-text");
            var title = new Label(def.Title);
            title.AddToClassList("card-name");
            var desc = new Label(def.Description);
            desc.AddToClassList("card-desc");
            text.Add(title);
            text.Add(desc);
            card.Add(text);
            if (!string.IsNullOrEmpty(def.IconClass))
            {
                var icon = new VisualElement();
                icon.AddToClassList("card-icon");
                icon.AddToClassList(def.IconClass);
                card.Add(icon);
            }
            else
            {
                var badge = new VisualElement();
                badge.AddToClassList("card-badge");
                badge.style.backgroundColor = def.Accent;
                card.Add(badge);
            }
            var stack = new Label(Roman(level + 1) + " / " + Roman(def.Cap));
            stack.AddToClassList("card-stack");
            card.Add(stack);
            return card;
        }

        public static void FillCards(VisualElement container, CardPool pool, Action<int> pick)
        {
            container.Clear();
            int index = 0;
            foreach (var id in pool.Choices)
            {
                int chosen = id;
                var card = Card(pool.Def(id), pool.Level(id), () => pick(chosen));
                container.Add(card);
                card.schedule.Execute(() => card.AddToClassList("card--in")).ExecuteLater(60 + index * 70);
                index++;
            }
        }

        public static void FillStackRow(VisualElement row, UpgradeManager up)
        {
            row.Clear();
            AppendChips(row, up.Power);
            AppendChips(row, up.Fill);
            if (row.childCount == 0) Label(row, "NO STACK", "stack-empty");
        }

        static void AppendChips(VisualElement row, CardPool pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int lvl = pool.Level(i);
                if (lvl <= 0) continue;
                var def = pool.Def(i);
                var chip = new VisualElement();
                chip.AddToClassList("stack-chip");
                var dot = new VisualElement();
                dot.AddToClassList("stack-dot");
                dot.style.backgroundColor = def.Accent;
                var text = new Label(def.Title + " " + Roman(lvl));
                text.AddToClassList("stack-text");
                chip.Add(dot);
                chip.Add(text);
                row.Add(chip);
            }
        }
    }
}
