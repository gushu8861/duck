using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DuckGame.Maps.V12
{
    public sealed class HubFacilitiesUI : MonoBehaviour
    {
        public HubFacilitiesController hub;
        Canvas canvas;
        GameObject shop, departure;
        Text prompt, balance, tradeMessage, loadout, preparation, preparationAction;
        Text[] statuses = new Text[3];
        Text[] worldLabels = new Text[2];
        Transform[] anchors;
        Font font;
        static readonly Color Paper = new Color(.95f, .92f, .81f);
        static readonly Color Card = new Color(.075f, .115f, .16f, .985f);

        void Start()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC" }, 24);
            var root = new GameObject("营地设施 · 中文界面", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("Hub EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            prompt = Label(root.transform, "", new Vector2(0, -365), new Vector2(800, 64), 30);
            anchors = new[] { hub.vendingAnchor, hub.departureAnchor };
            for (int i = 0; i < worldLabels.Length; i++)
                worldLabels[i] = Label(root.transform, i == 0 ? "魔法售货机" : "出发门 · 装配准备",
                    Vector2.zero, new Vector2(550, 42), 24);

            shop = Box(root.transform, "魔法售货机", Vector2.zero, new Vector2(1040, 700), Card);
            Label(shop.transform, "魔法售货机", new Vector2(-145, 278), new Vector2(650, 58), 40, TextAnchor.MiddleLeft);
            Label(shop.transform, "营地强化 · 购买后立即生效", new Vector2(-130, 214), new Vector2(680, 40), 24, TextAnchor.MiddleLeft);
            balance = Label(shop.transform, "", new Vector2(310, 214), new Vector2(340, 40), 25, TextAnchor.MiddleRight);
            string[] titles = { "轻盈装填", "顺风法球", "同伴法球" };
            string[] descriptions = { "射速 ×1.2 · 最多3次", "弹速 ×1.25 · 最多3次", "每发法球 +1 · 最多8颗" };
            int[] prices = { 25, 25, 40 };
            for (int i = 0; i < 3; i++)
            {
                var row = Box(shop.transform, titles[i], new Vector2(0, 95 - 126 * i),
                    new Vector2(944, 108), new Color(.13f, .19f, .23f));
                Label(row.transform, titles[i], new Vector2(-290, 23), new Vector2(320, 42), 29, TextAnchor.MiddleLeft);
                Label(row.transform, descriptions[i], new Vector2(-250, -23), new Vector2(400, 36), 22, TextAnchor.MiddleLeft);
                statuses[i] = Label(row.transform, "", new Vector2(100, 0), new Vector2(240, 42), 22);
                int index = i;
                Button(row.transform, prices[i] + " 金币 · 购买", new Vector2(342, 0), new Vector2(205, 62),
                    () => hub.Purchase(index));
            }
            tradeMessage = Label(shop.transform, "", new Vector2(0, -260), new Vector2(940, 48), 24, TextAnchor.MiddleLeft);
            Label(shop.transform, "试玩预算仅本次游玩有效 · Esc / F 关闭", new Vector2(0, -314), new Vector2(900, 36), 22);
            Button(shop.transform, "关闭", new Vector2(408, 280), new Vector2(132, 52), hub.ClosePanel);

            departure = Box(root.transform, "出发装配", Vector2.zero, new Vector2(980, 650), Card);
            Label(departure.transform, "出发门 · 装配准备", new Vector2(-85, 253), new Vector2(700, 58), 40, TextAnchor.MiddleLeft);
            Label(departure.transform, "远征航线尚未接入", new Vector2(0, 172), new Vector2(840, 48), 28);
            loadout = Label(departure.transform, "", new Vector2(0, 40), new Vector2(840, 160), 28, TextAnchor.MiddleLeft);
            preparation = Label(departure.transform, "", new Vector2(0, -115), new Vector2(840, 98), 24);
            preparationAction = Button(departure.transform, "确认装配准备", new Vector2(-165, -245),
                new Vector2(330, 64), () => hub.TogglePreparation());
            Button(departure.transform, "关闭 · 返回营地", new Vector2(210, -245), new Vector2(350, 64), hub.ClosePanel);
            Refresh();
        }

        public void Refresh()
        {
            if (shop == null) return;
            shop.SetActive(hub.isActiveAndEnabled && hub.Panel == HubPanel.Shop);
            departure.SetActive(hub.isActiveAndEnabled && hub.Panel == HubPanel.Departure);
            if (hub.Panel != HubPanel.None)
            {
                prompt.text = "";
                foreach (var label in worldLabels) label.gameObject.SetActive(false);
            }
            balance.text = "金币  " + hub.trade.Gold;
            tradeMessage.text = hub.trade.Message;
            for (int i = 0; i < 3; i++) statuses[i].text = hub.trade.Status(i);
            var shooter = hub.trade.shooter;
            loadout.text = $"射速倍率    ×{shooter.rateMultiplier:0.00}\n弹速            {shooter.config.speed:0.0} 米 / 秒\n每发数量    {shooter.config.projectileCount} 颗                  剩余金币    {hub.trade.Gold}";
            preparation.text = hub.PreparationMessage;
            preparationAction.text = hub.Prepared ? "取消出发准备" : "确认装配准备";
        }

        void OnEnable()
        {
            if (canvas != null) canvas.enabled = true;
            Refresh();
        }

        void OnDisable()
        {
            if (canvas != null) canvas.enabled = false;
        }

        void Update()
        {
            if (prompt == null) return;
            bool free = hub.isActiveAndEnabled && hub.Panel == HubPanel.None;
            prompt.text = free && hub.CanOpenShop() ? "F  使用魔法售货机" :
                free && hub.CanOpenDeparture() ? (hub.Prepared ? "F  已准备 · 检查装配" : "F  检查出发装配") : "";
            for (int i = 0; i < worldLabels.Length; i++)
            {
                Vector3 screen = hub.trade.view.lens.WorldToScreenPoint(anchors[i].position + Vector3.up * 3.3f);
                bool visible = free && screen.z > 0 &&
                    Vector3.Distance(hub.trade.motor.transform.position, anchors[i].position) < 20;
                worldLabels[i].gameObject.SetActive(visible);
                worldLabels[i].transform.position = screen;
            }
        }

        GameObject Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize,
                   TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = font;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Paper;
            label.raycastTarget = false;
            return label;
        }

        Text Button(Transform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = Box(parent, text, position, size, new Color(.24f, .43f, .42f));
            go.AddComponent<Button>().onClick.AddListener(action);
            return Label(go.transform, text, Vector2.zero, size, 23);
        }
    }
}


