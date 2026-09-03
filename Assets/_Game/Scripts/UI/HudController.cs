using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        VisualElement root;
        VisualElement hpBar, hpFill, bossBar, bossFill, trailBar, trailFill, joystick, joystickKnob;
        Label hpText, upgradesText, waveText, enemyText, comboChip, bossLabel, drawText;
        DamageTextLayer damageLayer;
        InputReader input;
        readonly StringBuilder sb = new StringBuilder(128);
        int lastCombo = -1;
        int lastUpgradeHash = -1;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement.Q("hud");
            hpBar = root.Q("hp-bar");
            hpFill = root.Q("hp-fill");
            hpText = root.Q<Label>("hp-text");
            upgradesText = root.Q<Label>("upgrades-text");
            waveText = root.Q<Label>("wave-text");
            enemyText = root.Q<Label>("enemy-text");
            comboChip = root.Q<Label>("combo-chip");
            bossBar = root.Q("boss-bar");
            bossFill = root.Q("boss-fill");
            bossLabel = root.Q<Label>("boss-label");
            drawText = root.Q<Label>("draw-text");
            trailBar = root.Q("trail-bar");
            trailFill = root.Q("trail-fill");
            joystick = root.Q("joystick");
            joystickKnob = root.Q("joystick-knob");
            damageLayer = new DamageTextLayer(root.Q("damage-layer"));
            SafeArea.Apply(root);
        }

        void Start()
        {
            var gm = GameManager.I;
            input = FindFirstObjectByType<InputReader>();
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
            root.style.display = mode == GameMode.Menu ? DisplayStyle.None : DisplayStyle.Flex;
            if (mode == GameMode.Play) lastUpgradeHash = -1;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || root.resolvedStyle.display == DisplayStyle.None) return;
            var cfg = gm.Config;
            var cam = gm.Cam.Cam;

            float hpFrac = Mathf.Clamp01(gm.Player.Hp / cfg.playerMaxHP);
            hpFill.style.width = Length.Percent(hpFrac * 100f);
            hpBar.EnableInClassList("hp-bar--low", hpFrac <= 0.3f);
            hpText.text = $"{Mathf.Max(0, Mathf.RoundToInt(gm.Player.Hp))}/{cfg.playerMaxHP:0}";

            waveText.text = $"DALGA {gm.Waves.Wave}";
            enemyText.text = $"{gm.Enemies.Count} düşman";

            if (gm.Combo != lastCombo)
            {
                lastCombo = gm.Combo;
                comboChip.text = $"KOMBO x{gm.Combo}";
                comboChip.EnableInClassList("combo-chip--on", gm.Combo > 0);
                if (gm.Combo > 0) Pop(comboChip);
            }

            var boss = gm.Waves.ActiveBoss;
            bool bossAlive = boss != null && !boss.Dead;
            bossBar.style.display = bossAlive ? DisplayStyle.Flex : DisplayStyle.None;
            bossLabel.style.display = bossAlive ? DisplayStyle.Flex : DisplayStyle.None;
            if (bossAlive) bossFill.style.width = Length.Percent(Mathf.Clamp01(boss.Hp / boss.MaxHp) * 100f);

            bool drawing = gm.Trail.Drawing;
            drawText.EnableInClassList("draw-text--on", drawing);
            trailBar.EnableInClassList("trail-bar--on", drawing);
            if (drawing)
            {
                float frac = Mathf.Clamp01(gm.Trail.PathLength / gm.EffectiveMaxLoopLength());
                trailFill.style.width = Length.Percent(frac * 100f);
                trailFill.EnableInClassList("trail-fill--hot", frac > 0.85f);
            }

            UpdateUpgrades(gm);
            UpdateJoystick();
            damageLayer.Tick(Time.deltaTime, cam);
        }

        void UpdateUpgrades(GameManager gm)
        {
            int hash = 17;
            for (int i = 0; i < UpgradeManager.Count; i++) hash = hash * 31 + gm.Upgrades.Level((UpgradeId)i);
            if (hash == lastUpgradeHash) return;
            lastUpgradeHash = hash;
            sb.Clear();
            for (int i = 0; i < UpgradeManager.Count; i++)
            {
                var id = (UpgradeId)i;
                int lvl = gm.Upgrades.Level(id);
                if (lvl <= 0) continue;
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(UpgradeManager.ShortNameOf(id));
                if (id == UpgradeId.ElectricBorder)
                    sb.Append(" x").Append(lvl).Append(" (").Append(Mathf.RoundToInt(gm.Config.ElectricBorderRadius(lvl))).Append("px)");
                else if (lvl > 1)
                    sb.Append(" x").Append(lvl);
            }
            upgradesText.text = sb.ToString();
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
