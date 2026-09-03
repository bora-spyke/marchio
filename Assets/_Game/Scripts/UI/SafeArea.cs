using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    public static class SafeArea
    {
        public static void Apply(VisualElement root)
        {
            root.RegisterCallback<GeometryChangedEvent>(_ => Pad(root));
            Pad(root);
        }

        static void Pad(VisualElement root)
        {
            var panel = root.panel;
            if (panel == null || Screen.height <= 0) return;
            var safe = Screen.safeArea;
            float scale = root.resolvedStyle.height / Screen.height;
            if (float.IsNaN(scale) || scale <= 0f) return;
            root.style.paddingTop = (Screen.height - safe.yMax) * scale;
            root.style.paddingBottom = safe.yMin * scale;
            root.style.paddingLeft = safe.xMin * scale;
            root.style.paddingRight = (Screen.width - safe.xMax) * scale;
        }
    }
}
