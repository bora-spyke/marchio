using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ScreensController : MonoBehaviour
    {
        VisualElement root, main, clear, fill, power, fail, victory;
        VisualElement clearLines, clearStack;
        Label clearTitle, clearHp, fillTitle, powerTitle, victoryScore;
        Button reviveBtn;

        GameManager Gm => GameManager.I;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement.Q("screens");
            main = root.Q("main");
            clear = root.Q("clear");
            fill = root.Q("fill");
            power = root.Q("power");
            fail = root.Q("fail");
            victory = root.Q("victory");
            foreach (var overlay in new[] { main, clear, fill, power, fail, victory }) SafeArea.Apply(overlay);

            UiKit.ShellButton(root.Q("main-buttons"), "Play", () => Gm.StartRun(), "btn--play", "btn-shell--play");

            clearTitle = root.Q<Label>("clear-title");
            clearLines = root.Q("clear-lines");
            clearStack = root.Q("clear-stack");
            clearHp = root.Q<Label>("clear-hp");
            UiKit.Button(root.Q("clear-buttons"), "CONTINUE", () => Gm.ContinueFromClear(), "btn--primary");

            fillTitle = root.Q<Label>("fill-title");
            powerTitle = root.Q<Label>("power-title");

            reviveBtn = UiKit.ShellButton(root.Q("fail-buttons"), "Revive", () => Gm.Revive(), "btn--revive");
            UiKit.ShellButton(root.Q("fail-buttons"), "Retry", () => Gm.ToMenu(), "btn--retry");

            victoryScore = root.Q<Label>("victory-score");
            UiKit.Button(root.Q("victory-buttons"), "MAIN MENU", () => Gm.ToMenu(), "btn--primary");
        }

        void Start()
        {
            Gm.ModeChanged += OnMode;
            OnMode(Gm.Mode);
        }

        void OnDestroy()
        {
            if (GameManager.I != null) GameManager.I.ModeChanged -= OnMode;
        }

        void OnMode(GameMode mode)
        {
            Show(main, mode == GameMode.Menu);
            Show(clear, mode == GameMode.LevelClear);
            Show(fill, mode == GameMode.FillUpgrade);
            Show(power, mode == GameMode.PowerUp);
            Show(fail, mode == GameMode.Fail);
            Show(victory, mode == GameMode.Victory);
            switch (mode)
            {
                case GameMode.LevelClear: BuildClear(); break;
                case GameMode.FillUpgrade:
                    fillTitle.text = $"STAGE {Gm.Run.Level} COMPLETE";
                    UiKit.FillCards(root.Q("fill-cards"), Gm.Upgrades.Fill, id => Gm.PickFill(id));
                    break;
                case GameMode.PowerUp:
                    powerTitle.text = $"STAGE {Gm.Run.Level + 1}";
                    UiKit.FillCards(root.Q("power-cards"), Gm.Upgrades.Power, id => Gm.PickPower(id));
                    break;
                case GameMode.Fail: BuildFail(); break;
                case GameMode.Victory: victoryScore.text = $"RUN SCORE: {Gm.Run.RunScore:0}"; break;
            }
        }

        static void Show(VisualElement overlay, bool visible)
        {
            if (visible)
            {
                overlay.style.display = DisplayStyle.Flex;
                overlay.schedule.Execute(() => overlay.AddToClassList("overlay--visible")).ExecuteLater(16);
            }
            else
            {
                overlay.RemoveFromClassList("overlay--visible");
                overlay.schedule.Execute(() =>
                {
                    if (!overlay.ClassListContains("overlay--visible")) overlay.style.display = DisplayStyle.None;
                }).ExecuteLater(260);
            }
        }

        void BuildClear()
        {
            var gm = Gm;
            var run = gm.Run;
            clearTitle.text = $"STAGE {run.Level} COMPLETE";
            clearLines.Clear();
            UiKit.Line(clearLines, "LEVEL SCORE", out var scoreValue);
            var bonusRow = UiKit.Line(clearLines, $"+{gm.Preset.completionBonus * 100f:0}% COMPLETION BONUS", out var bonusValue);
            bonusRow.AddToClassList("line--bonus");
            bonusRow.style.opacity = 0f;
            UiKit.CountUp(scoreValue, 0f, run.LastLevelScore, 600);
            bonusValue.text = "";
            clearLines.schedule.Execute(() =>
            {
                bonusRow.style.opacity = 1f;
                UiKit.CountUp(bonusValue, 0f, run.LastBonus, 500, "+");
            }).ExecuteLater(700);
            UiKit.FillStackRow(clearStack, gm.Upgrades);
            clearHp.text = $"HP {gm.Player.Hp:0} / {gm.PlayerMaxHp:0}" + (run.HealedOnClear ? $"   ·   HEALED +{gm.Preset.healAmount * 100f:0}%" : "");
        }

        void BuildFail()
        {
            var run = Gm.Run;
            reviveBtn.parent.style.display = run.RevivesLeft > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            reviveBtn.text = run.RevivesLeft > 1 ? $"Revive ({run.RevivesLeft})" : "Revive";
        }
    }
}
