using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        VisualElement root;
        VisualElement hpBar, hpFill, trailBar, trailFill, joystick, joystickKnob, stackRow;
        Label stageText, waveInfo, comboChip, drawText;
        int lastWaveKey = -1;
        DamageTextLayer damageLayer;
        InputReader input;
        int lastCombo = -1;
        int lastStackHash = -1;
        int lastLevel = -1;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement.Q("hud");
            SafeArea.Apply(root);
            hpBar = root.Q("hp-bar");
            hpFill = root.Q("hp-fill");
            stackRow = root.Q("stack-row");
            stageText = root.Q<Label>("stage-text");
            waveInfo = root.Q<Label>("wave-info");
            comboChip = root.Q<Label>("combo-chip");
            drawText = root.Q<Label>("draw-text");
            trailBar = root.Q("trail-bar");
            trailFill = root.Q("trail-fill");
            joystick = root.Q("joystick");
            joystickKnob = root.Q("joystick-knob");
            damageLayer = new DamageTextLayer(root.Q("damage-layer"));
        }

        void Start()
        {
            var gm = GameManager.I;
            input = gm.GetComponent<InputReader>();
            gm.DamageText += damageLayer.Spawn;
            gm.ModeChanged += OnMode;
            OnMode(gm.Mode);
        }

        void OnDestroy()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            gm.DamageText -= damageLayer.Spawn;
            gm.ModeChanged -= OnMode;
        }

        void OnMode(GameMode mode)
        {
            root.style.display = mode == GameMode.Play ? DisplayStyle.Flex : DisplayStyle.None;
            if (mode == GameMode.Play) { lastStackHash = -1; lastLevel = -1; lastWaveKey = -1; }
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
                stageText.text = run.IsVictoryLap ? "VICTORY LAP" : "";
            }
            var waves = gm.Waves;
            int waveKey = run.IsVictoryLap ? -2 : waves.WaveIndex * 1000 + waves.RemainingInWave;
            if (waveKey != lastWaveKey)
            {
                lastWaveKey = waveKey;
                if (!run.IsVictoryLap) stageText.text = $"WAVE {Mathf.Min(waves.WaveIndex + 1, Mathf.Max(1, waves.WaveCount))}/{waves.WaveCount}";
                waveInfo.text = run.IsVictoryLap ? "" : $"{waves.RemainingInWave} LEFT";
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
