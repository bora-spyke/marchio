using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    public sealed class DamageTextLayer
    {
        sealed class Entry
        {
            public Label Label;
            public Vector2 WorldPos;
            public float Life;
        }

        readonly VisualElement container;
        readonly Stack<Label> free = new Stack<Label>();
        readonly List<Entry> active = new List<Entry>(64);
        readonly Stack<Entry> freeEntries = new Stack<Entry>();

        public DamageTextLayer(VisualElement container)
        {
            this.container = container;
        }

        public void Spawn(Vector2 worldPos, int value)
        {
            var label = free.Count > 0 ? free.Pop() : Create();
            label.text = value.ToString();
            label.style.display = DisplayStyle.Flex;
            label.style.opacity = 1f;
            var e = freeEntries.Count > 0 ? freeEntries.Pop() : new Entry();
            e.Label = label;
            e.WorldPos = worldPos + new Vector2(Random.value * 16f - 8f, 0f);
            e.Life = 0.8f;
            active.Add(e);
        }

        Label Create()
        {
            var l = new Label();
            l.AddToClassList("dmg");
            l.pickingMode = PickingMode.Ignore;
            container.Add(l);
            return l;
        }

        public void Tick(float dt, Camera cam)
        {
            var panel = container.panel;
            if (panel == null) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var e = active[i];
                e.WorldPos.y += dt * 30f;
                e.Life -= dt * 1.6f;
                if (e.Life <= 0f)
                {
                    e.Label.style.display = DisplayStyle.None;
                    free.Push(e.Label);
                    active.RemoveAt(i);
                    freeEntries.Push(e);
                    continue;
                }
                var p = RuntimePanelUtils.CameraTransformWorldToPanel(panel, PolygonMath.ToWorld(e.WorldPos, 4f), cam);
                e.Label.style.left = p.x;
                e.Label.style.top = p.y;
                e.Label.style.opacity = Mathf.Clamp01(e.Life / 0.8f);
            }
        }
    }
}
