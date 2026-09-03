using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        VisualElement root;
        VisualElement hpBar, hpFill, thresholdBar, thresholdFill, trailBar, trailFill, joystick, joystickKnob, stackRow, toast;
        Label stageText, thresholdText, comboChip, drawText, toastText;
        DamageTextLayer damageLayer;
        InputReader input;
        int lastCombo = -1;
        int lastStackHash = -1;
        int lastLevel = -1;
        int lastRemaining = -1;
        bool pulse;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement.Q("hud");
            SafeArea.Apply(root);
            hpBar = root.Q("hp-bar");
            hpFill = root.Q("hp-fill");
            stackRow = root.Q("stack-row");
            stageText = root.Q<Label>("stage-text");
            thresholdBar = root.Q("threshold-bar");
            thresholdFill = root.Q("threshold-fill");
            thresholdText = root.Q<Label>("threshold-text");
            comboChip = root.Q<Label>("combo-chip");
            drawText = root.Q<Label>("draw-text");
            trailBar = root.Q("trail-bar");
            trailFill = root.Q("trail-fill");
            joystick = root.Q("joystick");
            joystickKnob = root.Q("joystick-knob");
            toast = root.Q("toast");
            toastText = root.Q<Label>("toast-text");
            damageLayer = new DamageTextLayer(root.Q("damage-layer"));
            root.schedule.Execute(() =>
            {
                pulse = !pulse;
                thresholdBar.EnableInClassList("threshold-bar--pulse", pulse && thresholdBar.ClassListContains("threshold-bar--pressure"));
            }).Every(350);
        }

        void Start()
        {
            var gm = GameManager.I;
            input = gm.GetComponent<InputReader>();
            gm.DamageText += damageLayer.Spawn;
            gm.ModeChanged += OnMode;
            gm.NodeUnlocked += OnUnlock;
            OnMode(gm.Mode);
        }

        void OnDestroy()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            gm.DamageText -= damageLayer.Spawn;
            gm.ModeChanged -= OnMode;
            gm.NodeUnlocked -= OnUnlock;
        }

        void OnMode(GameMode mode)
        {
            root.style.display = mode == GameMode.Play ? DisplayStyle.Flex : DisplayStyle.None;
            if (mode == GameMode.Play) { lastStackHash = -1; lastLevel = -1; lastRemaining = -1; }
        }

        void OnUnlock(TrophyNode node)
        {
            toastText.text = "UNLOCKED  ·  " + node.title.ToUpperInvariant();
            toast.EnableInClassList("toast--macro", node.macro);
            toast.AddToClassList("toast--on");
            toast.schedule.Execute(() => toast.RemoveFromClassList("toast--on")).ExecuteLater(1900);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || root.resolvedStyle.display == DisplayStyle.None) return;
            var cam = gm.Cam.Cam;

            float hpFrac = Mathf.Clamp01(gm.Player.Hp / gm.PlayerMaxHp);
            hpFill.style.width = Length.Percent(hpFrac * 100f);
            hpBar.EnableInClassList("hp-track--low", hpFrac <= 0.3f);

            var run = gm.Run;
            if (run.Level != lastLevel)
            {
                lastLevel = run.Level;
                int total = gm.Preset.levelCount;
                stageText.text = run.IsVictoryLap ? "VICTORY LAP" : total > 0 ? $"LEVEL {run.Level}/{total}" : $"LEVEL {run.Level}";
            }
            if (run.IsVictoryLap)
            {
                float lap = Mathf.Clamp01(run.LevelTime / Mathf.Max(1f, gm.Preset.victoryLapDurationS));
                thresholdFill.style.width = Length.Percent(lap * 100f);
                thresholdBar.RemoveFromClassList("threshold-bar--pressure");
                if (lastRemaining != 0) { lastRemaining = 0; thresholdText.text = "ENJOY THE RIDE"; }
            }
            else
            {
                thresholdFill.style.width = Length.Percent(run.Progress * 100f);
                thresholdBar.EnableInClassList("threshold-bar--pressure", run.Progress >= gm.Preset.thresholdPressureFrac);
                int remaining = Mathf.CeilToInt(run.Remaining);
                if (remaining != lastRemaining)
                {
                    lastRemaining = remaining;
                    thresholdText.text = $"{remaining} TO GO";
                }
            }

            if (gm.Combo != lastCombo)
            {
                lastCombo = gm.Combo;
                comboChip.text = $"COMBO x{gm.Combo}";
                comboChip.EnableInClassList("combo-chip--on", gm.Combo > 0);
                if (gm.Combo > 0) Pop(comboChip);
            }

            bool drawing = gm.Trail.Drawing;
            drawText.EnableInClassList("draw-text--on", drawing);
            trailBar.EnableInClassList("trail-bar--on", drawing);
            if (drawing)
            {
                float frac = Mathf.Clamp01(gm.Trail.PathLength / gm.EffectiveMaxLoopLength());
                trailFill.style.width = Length.Percent(frac * 100f);
                trailFill.EnableInClassList("trail-fill--hot", frac > 0.85f);
            }

            UpdateStack(gm);
            UpdateJoystick();
            damageLayer.Tick(Time.deltaTime, cam);
        }

        void UpdateStack(GameManager gm)
        {
            int hash = 17;
            var up = gm.Upgrades;
            for (int i = 0; i < up.Fill.Count; i++) hash = hash * 31 + up.Fill.Level(i);
            for (int i = 0; i < up.Power.Count; i++) hash = hash * 31 + up.Power.Level(i);
            if (hash == lastStackHash) return;
            lastStackHash = hash;
            UiKit.FillStackRow(stackRow, up);
        }

        void UpdateJoystick()
        {
            if (input == null || !input.JoystickVisible)
            {
                joystick.style.display = DisplayStyle.None;
                return;
            }
            var panel = root.panel;
            var origin = RuntimePanelUtils.ScreenToPanel(panel, FlipY(input.JoystickOrigin));
            var cur = RuntimePanelUtils.ScreenToPanel(panel, FlipY(input.JoystickCurrent));
            joystick.style.display = DisplayStyle.Flex;
            joystick.style.left = origin.x;
            joystick.style.top = origin.y;
            var delta = cur - origin;
            float max = 60f - 17f;
            if (delta.magnitude > max) delta = delta.normalized * max;
            joystickKnob.style.translate = new Translate(delta.x, delta.y);
        }

        static Vector2 FlipY(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);

        static void Pop(VisualElement el)
        {
            el.style.scale = new Scale(new Vector2(1.25f, 1.25f));
            el.schedule.Execute(() => el.style.scale = StyleKeyword.Null).ExecuteLater(40);
        }
    }
}
