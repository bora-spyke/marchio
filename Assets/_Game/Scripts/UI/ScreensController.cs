using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ScreensController : MonoBehaviour
    {
        VisualElement root, main, clear, fill, power, fail, victory;
        TrophyBarView mainTrophy, clearTrophy, failTrophy, victoryTrophy;
        VisualElement mainRoad, clearLines, clearStack, failStack, victoryRewards;
        Label mainCart, clearTitle, clearHp, fillTitle, powerTitle, failScore, failBonus, victoryScore;
        Button clearContinue, reviveBtn;

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

            mainTrophy = UiKit.TrophyBar(root.Q("main-trophy"));
            mainRoad = root.Q("main-road");
            mainCart = root.Q<Label>("main-cart");
            UiKit.Button(root.Q("main-buttons"), "PLAY", () => Gm.StartRun(), "btn--primary");
            UiKit.Button(root.Q("main-buttons"), "RESET PROGRESS", () => { Gm.ResetProgress(); BuildMain(); }, "btn--ghost");

            clearTitle = root.Q<Label>("clear-title");
            clearLines = root.Q("clear-lines");
            clearTrophy = UiKit.TrophyBar(root.Q("clear-trophy"));
            clearStack = root.Q("clear-stack");
            clearHp = root.Q<Label>("clear-hp");
            clearContinue = UiKit.Button(root.Q("clear-buttons"), "CONTINUE", () => Gm.ContinueFromClear(), "btn--primary");

            fillTitle = root.Q<Label>("fill-title");
            powerTitle = root.Q<Label>("power-title");

            failScore = root.Q<Label>("fail-score");
            failTrophy = UiKit.TrophyBar(root.Q("fail-trophy"));
            failStack = root.Q("fail-stack");
            failBonus = root.Q<Label>("fail-bonus");
            reviveBtn = UiKit.Button(root.Q("fail-buttons"), "REVIVE", () => Gm.Revive(), "btn--primary");
            UiKit.Button(root.Q("fail-buttons"), "RETRY", () => Gm.ToMenu(), "btn--ghost");

            victoryScore = root.Q<Label>("victory-score");
            victoryTrophy = UiKit.TrophyBar(root.Q("victory-trophy"));
            victoryRewards = root.Q("victory-rewards");
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
                case GameMode.Menu: BuildMain(); break;
                case GameMode.LevelClear: BuildClear(); break;
                case GameMode.FillUpgrade:
                    fillTitle.text = $"LEVEL {Gm.Run.Level} COMPLETE";
                    UiKit.FillCards(root.Q("fill-cards"), Gm.Upgrades.Fill, id => Gm.PickFill(id));
                    break;
                case GameMode.PowerUp:
                    powerTitle.text = $"LEVEL {Gm.Run.Level + 1}";
                    UiKit.FillCards(root.Q("power-cards"), Gm.Upgrades.Power, id => Gm.PickPower(id));
                    break;
                case GameMode.Fail: BuildFail(); break;
                case GameMode.Victory: BuildVictory(); break;
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

        void BuildMain()
        {
            var gm = Gm;
            var trophy = gm.Trophy;
            mainTrophy.Set(trophy, trophy.Total);
            mainCart.text = (trophy.CartTier > 0 ? "CART MK II" : "CART MK I")
                + (trophy.Has(TrophyReward.DamageBoost) ? "  ·  SKIN" : "")
                + (trophy.Has(TrophyReward.CartColor) ? "  ·  COLOR" : "")
                + (trophy.Has(TrophyReward.TrailEffect) ? "  ·  TRAIL FX" : "");
            mainRoad.Clear();
            var header = new VisualElement();
            header.AddToClassList("road-header");
            UiKit.Label(header, "FREE", "road-col-title");
            UiKit.Label(header, "PREMIUM", "road-col-title");
            mainRoad.Add(header);
            var nodes = trophy.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var row = new VisualElement();
                row.AddToClassList("node");
                row.AddToClassList(node.macro ? "node--macro" : "node--micro");
                if (i < trophy.Claimed) row.AddToClassList("node--claimed");
                var free = new VisualElement();
                free.AddToClassList("node-free");
                var badge = new VisualElement();
                badge.AddToClassList("node-badge");
                badge.Add(new Label(node.macro ? "★" : "•"));
                var text = new VisualElement();
                text.AddToClassList("node-text");
                UiKit.Label(text, node.title, "node-title");
                UiKit.Label(text, i < trophy.Claimed ? "CLAIMED" : node.threshold.ToString("0") + " TROPHY", "node-sub");
                free.Add(badge);
                free.Add(text);
                var premium = new VisualElement();
                premium.AddToClassList("node-premium");
                row.Add(free);
                row.Add(premium);
                mainRoad.Add(row);
            }
        }

        void BuildClear()
        {
            var gm = Gm;
            var run = gm.Run;
            clearTitle.text = $"LEVEL {run.Level} COMPLETE";
            clearLines.Clear();
            UiKit.Line(clearLines, "LEVEL SCORE", out var scoreValue);
            var bonusRow = UiKit.Line(clearLines, $"+{gm.Preset.completionBonus * 100f:0}% COMPLETION BONUS", out var bonusValue);
            bonusRow.AddToClassList("line--bonus");
            bonusRow.style.opacity = 0f;
            UiKit.CountUp(scoreValue, 0f, run.LastLevelScore, 600);
            bonusValue.text = "";
            float total = gm.Trophy.Total;
            clearTrophy.Set(gm.Trophy, total - run.LastBonus);
            clearLines.schedule.Execute(() =>
            {
                bonusRow.style.opacity = 1f;
                UiKit.CountUp(bonusValue, 0f, run.LastBonus, 500, "+");
                clearTrophy.Animate(gm.Trophy, total - run.LastBonus, total, 700);
            }).ExecuteLater(700);
            UiKit.FillStackRow(clearStack, gm.Upgrades);
            clearHp.text = $"HP {gm.Player.Hp:0} / {gm.PlayerMaxHp:0}" + (run.HealedOnClear ? $"   ·   HEALED +{gm.Preset.healAmount * 100f:0}%" : "");
        }

        void BuildFail()
        {
            var gm = Gm;
            var run = gm.Run;
            failScore.text = $"SCORE EARNED THIS RUN: {run.RunScore:0}";
            failTrophy.Set(gm.Trophy, gm.Trophy.Total);
            failStack.RemoveFromClassList("stack-row--dark");
            UiKit.FillStackRow(failStack, gm.Upgrades);
            failStack.schedule.Execute(() => failStack.AddToClassList("stack-row--dark")).ExecuteLater(900);
            failBonus.text = $"+{gm.Preset.completionBonus * 100f:0}% BONUS MISSED  ({run.MissedBonus:0})";
            reviveBtn.style.display = run.RevivesLeft > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            reviveBtn.text = $"REVIVE  ({run.RevivesLeft} LEFT)";
        }

        void BuildVictory()
        {
            var gm = Gm;
            victoryScore.text = $"RUN SCORE: {gm.Run.RunScore:0}";
            victoryTrophy.Set(gm.Trophy, gm.Trophy.Total);
            victoryRewards.Clear();
            var nodes = gm.Trophy.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!nodes[i].macro) continue;
                var chip = new VisualElement();
                chip.AddToClassList("reward");
                if (i >= gm.Trophy.Claimed) chip.AddToClassList("reward--locked");
                UiKit.Label(chip, "★", "reward-star");
                UiKit.Label(chip, nodes[i].title, "reward-title");
                victoryRewards.Add(chip);
            }
        }
    }
}
