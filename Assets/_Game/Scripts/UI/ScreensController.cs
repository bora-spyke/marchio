using UnityEngine;
using UnityEngine.UIElements;

namespace Marchio
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class ScreensController : MonoBehaviour
    {
        VisualElement root, menu, upgrade, over, cards;
        Label upgradeTitle, overSub, menuCta, overCta;
        bool pulse;

        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement.Q("screens");
            menu = root.Q("menu");
            upgrade = root.Q("upgrade");
            over = root.Q("over");
            cards = root.Q("cards");
            upgradeTitle = root.Q<Label>("upgrade-title");
            overSub = root.Q<Label>("over-sub");
            menuCta = root.Q<Label>("menu-cta");
            overCta = root.Q<Label>("over-cta");
            menu.RegisterCallback<PointerDownEvent>(_ => GameManager.I.OnScreenTap());
            over.RegisterCallback<PointerDownEvent>(_ => GameManager.I.OnScreenTap());
            root.schedule.Execute(() =>
            {
                pulse = !pulse;
                menuCta.EnableInClassList("cta--pulse", pulse);
                overCta.EnableInClassList("cta--pulse", pulse);
            }).Every(700);
        }

        void Start()
        {
            GameManager.I.ModeChanged += OnMode;
            OnMode(GameManager.I.Mode);
        }

        void OnDestroy()
        {
            if (GameManager.I != null) GameManager.I.ModeChanged -= OnMode;
        }

        void OnMode(GameMode mode)
        {
            Show(menu, mode == GameMode.Menu);
            Show(upgrade, mode == GameMode.Upgrade);
            Show(over, mode == GameMode.Over);
            if (mode == GameMode.Upgrade) BuildCards();
            if (mode == GameMode.Over)
            {
                var gm = GameManager.I;
                overSub.text = $"ulaşılan dalga {gm.Waves.Wave}   en iyi {gm.Best}";
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

        void BuildCards()
        {
            var gm = GameManager.I;
            upgradeTitle.text = $"DALGA {gm.Waves.Wave} TAMAMLANDI";
            cards.Clear();
            int index = 0;
            foreach (var id in gm.Upgrades.Choices)
            {
                var card = new Button();
                card.AddToClassList("card");
                var badge = new VisualElement();
                badge.AddToClassList("card-badge");
                badge.style.backgroundColor = UpgradeManager.AccentOf(id, gm.Config);
                var text = new VisualElement();
                text.AddToClassList("card-text");
                var name = new Label(UpgradeManager.NameOf(id));
                name.AddToClassList("card-name");
                var desc = new Label(UpgradeManager.DescriptionOf(id));
                desc.AddToClassList("card-desc");
                text.Add(name);
                text.Add(desc);
                card.Add(badge);
                card.Add(text);
                var chosen = id;
                card.clicked += () => gm.PickUpgrade(chosen);
                cards.Add(card);
                card.schedule.Execute(() => card.AddToClassList("card--in")).ExecuteLater(60 + index * 70);
                index++;
            }
        }
    }
}
