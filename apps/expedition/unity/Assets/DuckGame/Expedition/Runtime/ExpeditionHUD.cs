using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuckGame.Expedition
{
    /// <summary>
    /// UGUI replacement for the old IMGUI HUD.
    ///
    /// Why this exists: OnGUI is not part of the rendered frame, so it never appeared in
    /// captures and could not be sized against the real display. A Canvas with
    /// CanvasScaler.ScaleWithScreenSize renders in-frame, scales identically at 720p/1080p/4K,
    /// and can be captured and reviewed.
    ///
    /// Layout mirrors the imported Trials Survivors UI: dark plates, warm gold hairlines,
    /// keycap hints, high-contrast text.
    /// </summary>
    public sealed class ExpeditionHUD : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.Tab;

        Canvas canvas;
        RectTransform root;
        Font font;

        Text txtTitle, txtDepth, txtElement, txtRisk, txtReward, txtState;
        Text txtGold, txtBuild, txtKills;
        Image hpFill, hpBack;
        Text txtHp;
        RectTransform routePanel, routeContent;
        Text txtRouteTitle, txtHint;
        RectTransform rewardOverlay, rewardRow;
        Text txtRewardTitle;

        // imported-art widgets (null when the key was not wired)
        Image iconElement, iconRisk, iconDepth, iconHealth, iconCoin, iconKill, iconKey, iconChest;

        readonly List<GameObject> spawn = new List<GameObject>();

        void Awake()
        {
            BuildCanvas();
            BuildStatusPanel();
            BuildVitalsPanel();
            BuildRoutePanel();
            BuildHintBar();
            BuildRewardOverlay();
        }

        void Start()
        {
            // Art-dependent widgets are created here, not in Awake: their sprites come from
            // TSSprites, whose own Awake may not have run yet while this component is building
            // its canvas. Doing it in Awake produced a HUD with zero icons and no error.
            AttachIcons();
        }

        bool iconsAttached;

        void AttachIcons()
        {
            if (iconsAttached || !UIArt.Ready) return;
            iconsAttached = true;
            AttachIconsInternal();
        }

        Font GetFont()
        {
            if (font != null) return font;
            // Prefer a bundled/system CJK font so Chinese labels render.
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "Arial" }, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        // ---------------- construction ----------------

        void BuildCanvas()
        {
            var go = new GameObject("ExpeditionCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();

            // ScreenSpaceOverlay: the UI is composited AFTER the camera's post-processing, so
            // bloom/tonemapping never soften the text. A ScreenSpaceCamera canvas would be
            // graded along with the world (which is what blurred every label in an earlier
            // pass), and a second UI camera does not composite into a RenderTexture capture.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            root = go.GetComponent<RectTransform>();
        }

        static void ApplyLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                ApplyLayerRecursive(t.GetChild(i), layer);
        }

        RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot,
                            Vector2 pos, Vector2 size, Color? fill = null, bool gold = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = fill ?? new Color(.055f, .055f, .075f, .93f);

            if (gold) AddBorder(rt, new Color(.96f, .80f, .38f, .55f));
            return rt;
        }

        void AddBorder(RectTransform parent, Color c, float thickness = 1.5f)
        {
            Color[] sides = { c, c, c, c };
            // top, bottom, left, right as thin images
            var top = NewImage(parent, "bT", new Color(c.r, c.g, c.b, c.a));
            var brt = top.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(.5f, 1f);
            brt.offsetMin = new Vector2(0f, -thickness);
            brt.offsetMax = Vector2.zero;

            var bot = NewImage(parent, "bB", c);
            var bbt = bot.rectTransform;
            bbt.anchorMin = new Vector2(0f, 0f); bbt.anchorMax = new Vector2(1f, 0f);
            bbt.pivot = new Vector2(.5f, 0f);
            bbt.offsetMin = Vector2.zero;
            bbt.offsetMax = new Vector2(0f, thickness);

            var lef = NewImage(parent, "bL", c);
            var blt = lef.rectTransform;
            blt.anchorMin = new Vector2(0f, 0f); blt.anchorMax = new Vector2(0f, 1f);
            blt.pivot = new Vector2(0f, .5f);
            blt.offsetMin = Vector2.zero;
            blt.offsetMax = new Vector2(thickness, 0f);

            var rig = NewImage(parent, "bR", c);
            var rrt = rig.rectTransform;
            rrt.anchorMin = new Vector2(1f, 0f); rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(1f, .5f);
            rrt.offsetMin = new Vector2(-thickness, 0f);
            rrt.offsetMax = Vector2.zero;
        }

        Image NewImage(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Text NewText(Transform parent, string name, string content, int size,
                     TextAnchor anchor, Color color, Vector2 anchorMin, Vector2 anchorMax,
                     Vector2 offsetMin, Vector2 offsetMax, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var t = go.GetComponent<Text>();
            t.font = GetFont();
            t.fontSize = size;
            t.text = content;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void BuildStatusPanel()
        {
            // top-left status block
            var p = Panel(root, "StatusPanel",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -28f), new Vector2(360f, 176f));

            txtTitle = NewText(p, "Title", "", 22, TextAnchor.UpperLeft,
                new Color(.96f, .95f, .90f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -50f), new Vector2(-18f, -18f), true);

            txtDepth = NewText(p, "Depth", "", 15, TextAnchor.UpperRight,
                new Color(.66f, .68f, .74f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -46f), new Vector2(-18f, -24f));

            txtElement = NewText(p, "Element", "", 16, TextAnchor.MiddleLeft,
                new Color(.48f, .80f, 1f), new Vector2(0, 1), new Vector2(.5f, 1),
                new Vector2(18f, -84f), new Vector2(-4f, -54f), true);

            txtRisk = NewText(p, "Risk", "", 16, TextAnchor.MiddleRight,
                new Color(1f, .84f, .28f), new Vector2(.5f, 1), new Vector2(1, 1),
                new Vector2(4f, -84f), new Vector2(-18f, -54f), true);

            txtReward = NewText(p, "Reward", "", 14, TextAnchor.UpperLeft,
                new Color(.66f, .68f, .74f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -118f), new Vector2(-18f, -88f));

            txtState = NewText(p, "State", "", 17, TextAnchor.UpperLeft,
                new Color(1f, .77f, .42f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -152f), new Vector2(-18f, -122f), true);
        }

        /// <summary>
        /// Imported glyphs: element, risk and depth badge. Runs from Start so TSSprites is
        /// ready. Text stays as the fallback, so a missing sprite degrades to the old look
        /// instead of an empty slot.
        /// </summary>
        void AttachIconsInternal()
        {
            var p = root.Find("StatusPanel") as RectTransform;
            if (p != null)
            {
                // Vertical alignment note: txtElement occupies 54..84 px below the panel top
                // (MiddleLeft), so its centre is 69. An icon with pivot (0,1) must start at
                // 69 - size/2. Placing it at -78 made it 24 px too low and it collided with the
                // reward line at 88..118.
                const float elementRowCentre = 69f;

                iconElement = UIArt.Icon(p, DuckGame.TS.TSSprites.ElementIce,
                    new Vector2(0, 1), new Vector2(16f, -(elementRowCentre - 12f)), new Vector2(24f, 24f));
                if (iconElement != null)
                    txtElement.rectTransform.offsetMin = new Vector2(46f, txtElement.rectTransform.offsetMin.y);

                iconRisk = UIArt.Icon(p, DuckGame.TS.TSSprites.RiskNormal,
                    new Vector2(1, 1), new Vector2(-30f, -(elementRowCentre - 11f)), new Vector2(22f, 22f));
                if (iconRisk != null)
                {
                    iconRisk.rectTransform.pivot = new Vector2(1f, 1f);
                    txtRisk.rectTransform.offsetMax = new Vector2(-56f, txtRisk.rectTransform.offsetMax.y);
                }

                iconDepth = UIArt.Icon(p, DuckGame.TS.TSSprites.RiskUnknown,
                    new Vector2(1, 1), new Vector2(-18f, -26f), new Vector2(20f, 20f));
                if (iconDepth != null)
                {
                    iconDepth.rectTransform.pivot = new Vector2(1f, 1f);
                    txtDepth.rectTransform.offsetMax = new Vector2(-44f, txtDepth.rectTransform.offsetMax.y);
                }
            }

            var v = root.Find("VitalsPanel") as RectTransform;
            if (v != null)
            {
                iconHealth = UIArt.Icon(v, DuckGame.TS.TSSprites.StatHealth,
                    new Vector2(0, 1), new Vector2(16f, 8f), new Vector2(18f, 18f));

                iconCoin = UIArt.Icon(v, DuckGame.TS.TSSprites.StatCoin,
                    new Vector2(0, 1), new Vector2(16f, -50f), new Vector2(20f, 20f));
                if (iconCoin != null)
                    txtGold.rectTransform.offsetMin = new Vector2(42f, txtGold.rectTransform.offsetMin.y);

                iconKill = UIArt.Icon(v, DuckGame.TS.TSSprites.StatKill,
                    new Vector2(0, 0), new Vector2(16f, 40f), new Vector2(16f, 16f));
                if (iconKill != null)
                    txtKills.rectTransform.offsetMin = new Vector2(38f, txtKills.rectTransform.offsetMin.y);
            }
        }

        void BuildVitalsPanel()
        {
            var p = Panel(root, "VitalsPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(28f, 28f), new Vector2(330f, 140f));

            // health bar
            var back = NewImage(p, "HpBack", new Color(.08f, .08f, .10f, .95f));
            var br = back.rectTransform;
            br.anchorMin = new Vector2(0, 1); br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(.5f, 1f);
            br.offsetMin = new Vector2(18f, -46f);
            br.offsetMax = new Vector2(-18f, -22f);
            hpBack = back;

            var fill = NewImage(p, "HpFill", new Color(.50f, .90f, .58f));
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0, 1); fr.anchorMax = new Vector2(1, 1);
            fr.pivot = new Vector2(0f, 1f);
            fr.offsetMin = new Vector2(18f, -46f);
            fr.offsetMax = new Vector2(-18f, -22f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            hpFill = fill;

            txtHp = NewText(p, "HpText", "", 15, TextAnchor.MiddleCenter,
                Color.white, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -46f), new Vector2(-18f, -22f), true);

            txtGold = NewText(p, "Gold", "", 18, TextAnchor.UpperLeft,
                new Color(1f, .82f, .29f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -78f), new Vector2(-18f, -50f), true);

            txtBuild = NewText(p, "Build", "", 14, TextAnchor.UpperLeft,
                new Color(.66f, .68f, .74f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -104f), new Vector2(-18f, -80f));

            txtKills = NewText(p, "Kills", "", 14, TextAnchor.UpperLeft,
                new Color(.66f, .68f, .74f), new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(18f, 16f), new Vector2(-18f, 40f));
        }

        void BuildRoutePanel()
        {
            routePanel = Panel(root, "RoutePanel",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -28f), new Vector2(340f, 220f));

            txtRouteTitle = NewText(routePanel, "RouteTitle", "可前往", 21, TextAnchor.UpperLeft,
                new Color(.96f, .95f, .90f), new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18f, -50f), new Vector2(-18f, -18f), true);

            var content = new GameObject("RouteContent", typeof(RectTransform));
            content.transform.SetParent(routePanel, false);
            routeContent = content.GetComponent<RectTransform>();
            routeContent.anchorMin = new Vector2(0, 0);
            routeContent.anchorMax = new Vector2(1, 1);
            routeContent.offsetMin = new Vector2(18f, 18f);
            routeContent.offsetMax = new Vector2(-18f, -56f);

            routePanel.gameObject.SetActive(false);
        }

        void BuildHintBar()
        {
            var p = Panel(root, "HintBar",
                new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                new Vector2(0f, 34f), new Vector2(420f, 46f));

            // keycap
            var kc = NewImage(p, "Keycap", new Color(.14f, .14f, .18f, .96f));
            var kr = kc.rectTransform;
            kr.anchorMin = new Vector2(0f, .5f); kr.anchorMax = new Vector2(0f, .5f);
            kr.pivot = new Vector2(0f, .5f);
            kr.anchoredPosition = new Vector2(16f, 0f);
            kr.sizeDelta = new Vector2(38f, 26f);

            NewText(p, "KeycapText", "F", 15, TextAnchor.MiddleCenter,
                Color.white, new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(16f, -13f), new Vector2(54f, 13f), true);

            txtHint = NewText(p, "HintText", "", 16, TextAnchor.MiddleLeft,
                new Color(.96f, .95f, .90f), new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(66f, 0f), new Vector2(-16f, 0f));

            p.gameObject.SetActive(false);
            spawn.Add(p.gameObject);
        }

        void BuildRewardOverlay()
        {
            // full-screen dim
            var dim = NewImage(root, "RewardDim", new Color(0f, 0f, 0f, .68f));
            var dr = dim.rectTransform;
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
            dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
            rewardOverlay = dim.rectTransform;

            txtRewardTitle = NewText(rewardOverlay, "RewardTitle", "选择一项强化", 34,
                TextAnchor.MiddleCenter, new Color(.96f, .95f, .90f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-300f, 196f), new Vector2(300f, 252f), true);

            // Subtitle: what to do, matching the reference game's control hints.
            NewText(rewardOverlay, "RewardSub", "按 1 / 2 / 3 或点击卡牌", 16,
                TextAnchor.MiddleCenter, new Color(.66f, .68f, .74f),
                new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-300f, 168f), new Vector2(300f, 194f));

            // Manual positioning instead of a LayoutGroup: with childControlWidth=false the
            // group still ignored spacing here, and explicit anchors are unambiguous.
            var row = new GameObject("RewardRow", typeof(RectTransform));
            row.transform.SetParent(rewardOverlay, false);
            rewardRow = row.GetComponent<RectTransform>();
            rewardRow.anchorMin = new Vector2(.5f, .5f);
            rewardRow.anchorMax = new Vector2(.5f, .5f);
            rewardRow.pivot = new Vector2(.5f, .5f);
            rewardRow.anchoredPosition = new Vector2(0f, -18f);
            rewardRow.sizeDelta = new Vector2(1000f, 300f);

            rewardOverlay.gameObject.SetActive(false);
        }

        // ---------------- per-frame update ----------------

        void Update()
        {
            if (Input.GetKeyDown(toggleKey) && canvas != null)
                canvas.enabled = !canvas.enabled;

            var rewards = RewardSystem.Instance;
            if (rewards != null && rewards.Choosing)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) rewards.Pick(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2)) rewards.Pick(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3)) rewards.Pick(2);
            }
        }

        void LateUpdate()
        {
            var run = ExpeditionRun.Instance;
            if (run == null || run.Current == null) { HideAll(); return; }

            // Self-healing: if TSSprites became ready after Start, build the icons now.
            if (!iconsAttached) AttachIcons();

            var e = run.Current;

            txtTitle.text = UIMetrics.BiomeName(e.biome);
            txtDepth.text = "深度 " + e.depth + " / " + run.Graph.BossDepth;
            txtElement.text = UIMetrics.ElementName(e.element) + "元素";
            txtElement.color = UIMetrics.ElementColor(e.element);
            txtRisk.text = UIMetrics.RiskName(e.risk);
            txtRisk.color = UIMetrics.RiskColor(e.risk);
            txtReward.text = "奖励  " + e.rewardCategory;

            // swap the imported glyphs to match this island
            if (iconElement != null)
                UIArt.Assign(iconElement, UIArt.ElementKey(e.element), UIMetrics.ElementColor(e.element));
            if (iconRisk != null)
                UIArt.Assign(iconRisk, UIArt.RiskKey(e), UIMetrics.RiskColor(e.risk));

            // depth badge art only exists for 2..6 (verified roman numerals)
            if (iconDepth != null)
            {
                string dk = UIArt.DepthKey(e.depth);
                if (dk != null) UIArt.Assign(iconDepth, dk, new Color(.86f, .87f, .92f));
                else iconDepth.enabled = false;
            }

            if (e.completed)
            {
                txtState.text = "● 已清岛";
                txtState.color = new Color(.50f, .88f, .54f);
            }
            else
            {
                txtState.text = "● 战斗中";
                txtState.color = new Color(1f, .77f, .42f);
            }

            // vitals
            var hp = PlayerHealth.Instance;
            if (hp != null)
            {
                float pct = hp.maxHealth > 0f ? hp.Health / hp.maxHealth : 0f;
                hpFill.fillAmount = pct;
                hpFill.color = pct > .6f ? new Color(.50f, .90f, .58f)
                              : pct > .3f ? new Color(.98f, .66f, .28f)
                              : new Color(.95f, .42f, .40f);
                txtHp.text = Mathf.RoundToInt(hp.Health) + " / " + Mathf.RoundToInt(hp.maxHealth);
            }

            var mb = MerchantBridge.Instance;
            var eco = DuckGame.TS.Economy.Instance;
            int gold = mb != null ? mb.CurrentGold() : (eco != null ? eco.metaCurrency : 0);
            txtGold.text = "◆ " + gold + " 金币";

            var rw = RewardSystem.Instance;
            if (rw != null)
                txtBuild.text = "弹速 x" + rw.ProjectileSpeedMultiplier().ToString("F2")
                              + "   射速 x" + rw.FireRateMultiplier().ToString("F2");

            var cb = CombatBridge.Instance;
            txtKills.text = "击杀 " + (cb != null ? cb.TotalKills : 0)
                          + "    清岛 " + run.ClearedIslands;

            // route
            UpdateRoute(run);

            // hint
            bool showHint = e.completed && run.CommittedNextId >= 0;
            if (spawn.Count > 0 && spawn[0] != null) spawn[0].SetActive(showHint);
            if (showHint)
                txtHint.text = "走到跳板按 F 出发";

            // reward
            if (rw != null && rw.Choosing) ShowReward(rw);
            else if (rewardOverlay != null) rewardOverlay.gameObject.SetActive(false);
        }

        void UpdateRoute(ExpeditionRun run)
        {
            var exits = run.Graph.ExitsOf(run.Current.islandId);
            bool show = run.Current.completed && exits.Count > 0;
            routePanel.gameObject.SetActive(show);
            if (!show) return;

            // rebuild rows only when the island changes
            if (routeContent.childCount != exits.Count || routeContent.GetChild(0).name != "row_" + exits[0])
            {
                for (int i = routeContent.childCount - 1; i >= 0; i--)
                    Destroy(routeContent.GetChild(i).gameObject);

                float rowH = 62f;
                for (int i = 0; i < exits.Count; i++)
                {
                    var enc = run.Graph.Get(exits[i]);
                    if (enc == null) continue;

                    var rowGo = new GameObject("row_" + exits[i], typeof(RectTransform), typeof(Image));
                    rowGo.transform.SetParent(routeContent, false);
                    var rt = rowGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -i * (rowH + 8f));
                    rt.sizeDelta = new Vector2(0f, rowH);

                    var img = rowGo.GetComponent<Image>();
                    img.color = new Color(.10f, .10f, .13f, .92f);

                    // accent stripe
                    var stripe = NewImage(rt, "stripe", UIMetrics.ElementColor(enc.element));
                    var sr = stripe.rectTransform;
                    sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(0, 1);
                    sr.pivot = new Vector2(0, .5f);
                    sr.sizeDelta = new Vector2(4f, 0f);

                    NewText(rt, "name", UIMetrics.BiomeName(enc.biome) + "  " + UIMetrics.ElementName(enc.element),
                        18, TextAnchor.UpperLeft, new Color(.96f, .95f, .90f),
                        new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(14f, -30f), new Vector2(-14f, -6f), true);

                    NewText(rt, "detail", UIMetrics.RiskName(enc.risk) + " · " + enc.rewardCategory,
                        14, TextAnchor.UpperLeft, new Color(.66f, .68f, .74f),
                        new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(14f, -54f), new Vector2(-14f, -30f));
                }
            }

            // committed highlight
            for (int i = 0; i < routeContent.childCount && i < exits.Count; i++)
            {
                var img = routeContent.GetChild(i).GetComponent<Image>();
                if (img == null) continue;
                bool committed = run.CommittedNextId == exits[i];
                img.color = committed ? new Color(.16f, .36f, .22f, .95f)
                                      : new Color(.10f, .10f, .13f, .92f);
            }
        }

        void ShowReward(RewardSystem rw)
        {
            rewardOverlay.gameObject.SetActive(true);

            if (rewardRow.childCount != rw.Current.Count)
            {
                for (int i = rewardRow.childCount - 1; i >= 0; i--)
                    Destroy(rewardRow.GetChild(i).gameObject);

                const float cardW = 300f;
                const float cardH = 306f;
                const float gap = 26f;
                int n = rw.Current.Count;
                float totalW = cardW * n + gap * (n - 1);
                float startX = -totalW * .5f + cardW * .5f;

                for (int i = 0; i < n; i++)
                {
                    var u = rw.Current[i];
                    var card = new GameObject("card_" + i, typeof(RectTransform), typeof(Image));
                    card.transform.SetParent(rewardRow, false);
                    var cr = card.GetComponent<RectTransform>();
                    cr.anchorMin = new Vector2(.5f, .5f);
                    cr.anchorMax = new Vector2(.5f, .5f);
                    cr.pivot = new Vector2(.5f, .5f);
                    cr.anchoredPosition = new Vector2(startX + i * (cardW + gap), 0f);
                    cr.sizeDelta = new Vector2(cardW, cardH);

                    var img = card.GetComponent<Image>();
                    img.color = new Color(.075f, .075f, .095f, .98f);

                    AddBorder(cr, new Color(u.tint.r, u.tint.g, u.tint.b, .55f), 1.5f);

                    // Real spell-card art. Vertical order on the card, top to bottom:
                    //   0..46   index badge (+ top stripe)
                    //   56..186 the imported card illustration
                    //   188..218 title
                    //   220..276 description
                    //   274..306 bottom action strip
                    // The illustration must sit in its own band: with the earlier layout it
                    // was still occupying 56..150 while the title was drawn at 72..112, so the
                    // art showed through the text and both became unreadable.
                    var faceKey = UIArt.CardFaceKey(u.kind, u.affix);
                    if (UIArt.Sprite(faceKey) != null)
                    {
                        var face = NewImage(cr, "face", new Color(1f, 1f, 1f, .95f));
                        face.sprite = UIArt.Sprite(faceKey);
                        face.preserveAspect = false;
                        var fr0 = face.rectTransform;
                        fr0.anchorMin = new Vector2(0, 1); fr0.anchorMax = new Vector2(1, 1);
                        fr0.pivot = new Vector2(.5f, 1f);
                        fr0.offsetMin = new Vector2(12f, -186f);
                        fr0.offsetMax = new Vector2(-12f, -56f);
                    }

                    // corner badge: element/stat glyph matching this upgrade
                    var affixKey = UIArt.AffixKey(u.kind, u.affix);
                    var corner = UIArt.Icon(cr, affixKey,
                        new Vector2(1, 1), new Vector2(-14f, -14f), new Vector2(26f, 26f), u.tint);
                    if (corner != null) corner.rectTransform.pivot = new Vector2(1f, 1f);

                    // accent top stripe
                    var stripe = NewImage(cr, "stripe", u.tint);
                    var sr = stripe.rectTransform;
                    sr.anchorMin = new Vector2(0, 1); sr.anchorMax = new Vector2(1, 1);
                    sr.pivot = new Vector2(.5f, 1f);
                    sr.sizeDelta = new Vector2(0f, 5f);

                    // index badge
                    var badge = NewImage(cr, "badge",
                        new Color(u.tint.r * .30f, u.tint.g * .30f, u.tint.b * .30f, .98f));
                    var br = badge.rectTransform;
                    br.anchorMin = new Vector2(.5f, 1); br.anchorMax = new Vector2(.5f, 1);
                    br.pivot = new Vector2(.5f, 1f);
                    br.anchoredPosition = new Vector2(0f, -14f);
                    br.sizeDelta = new Vector2(40f, 40f);

                    NewText(cr, "num", (i + 1).ToString(), 22, TextAnchor.MiddleCenter,
                        Color.white, new Vector2(.5f, 1), new Vector2(.5f, 1),
                        new Vector2(-20f, -54f), new Vector2(20f, -14f), true);

                    NewText(cr, "title", u.title, 21, TextAnchor.UpperCenter, u.tint,
                        new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(14f, -222f), new Vector2(-14f, -190f), true);

                    NewText(cr, "desc", u.description, 14, TextAnchor.UpperCenter,
                        new Color(.82f, .84f, .88f), new Vector2(0, 1), new Vector2(1, 1),
                        new Vector2(14f, -272f), new Vector2(-14f, -224f));

                    int stacks = rw.Stacks(u.id);
                    if (stacks > 0)
                        NewText(cr, "stack", "已有 " + stacks + " 层", 12, TextAnchor.LowerCenter,
                            new Color(.62f, .82f, 1f), new Vector2(0, 0), new Vector2(1, 0),
                            new Vector2(14f, 36f), new Vector2(-14f, 58f));

                    // bottom action strip
                    var foot = NewImage(cr, "foot",
                        new Color(u.tint.r * .18f, u.tint.g * .18f, u.tint.b * .18f, .95f));
                    var fr2 = foot.rectTransform;
                    fr2.anchorMin = new Vector2(0, 0); fr2.anchorMax = new Vector2(1, 0);
                    fr2.pivot = new Vector2(.5f, 0f);
                    fr2.sizeDelta = new Vector2(0f, 32f);

                    NewText(cr, "footText", "选择", 15, TextAnchor.MiddleCenter,
                        u.tint, new Vector2(0, 0), new Vector2(1, 0),
                        new Vector2(0f, 0f), new Vector2(0f, 32f), true);

                    int idx = i;
                    var btn = card.AddComponent<Button>();
                    btn.targetGraphic = img;
                    var colors = btn.colors;
                    colors.normalColor = Color.white;
                    colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
                    colors.pressedColor = new Color(.85f, .85f, .85f, 1f);
                    btn.colors = colors;
                    btn.onClick.AddListener(() => rw.Pick(idx));
                }
            }
        }

        void HideAll()
        {
            if (routePanel != null) routePanel.gameObject.SetActive(false);
            if (spawn.Count > 0 && spawn[0] != null) spawn[0].SetActive(false);
        }
    }
}
