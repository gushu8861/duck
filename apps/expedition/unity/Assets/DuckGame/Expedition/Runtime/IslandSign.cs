using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuckGame.Expedition
{
    /// <summary>
    /// World-space preview board beside a spring pad, drawn with a UGUI world-space Canvas
    /// instead of TextMesh.
    ///
    /// TextMesh rasterises to a fixed-size font atlas and looks smeared at distance; a
    /// WorldSpace Canvas with dynamic font rendering stays crisp and lets the board use real
    /// layout (background plate, gold rule, two text lines) like the reference game's signs.
    ///
    /// The promised encounter is read from RouteGraph every frame, so the board can never
    /// disagree with the island the pad actually flies to.
    /// </summary>
    public sealed class IslandSign : MonoBehaviour
    {
        public int islandId = -1;
        public Transform player;
        public float visibleRadius = 26f;
        public float nearRadius = 10f;

        Canvas canvas;
        Text headline, detail;
        Image plate;
        float refresh;
        bool near;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            // Detach from the pad hierarchy. The pads are scaled and rotated by the builder,
            // and an inherited non-uniform or negative scale flips the Canvas, which is what
            // mirrored the text. A top-level canvas is immune to that.
            transform.SetParent(null, true);

            var go = new GameObject("SignCanvas");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one * 0.006f;
            go.transform.localRotation = Quaternion.identity;

            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320f, 96f);

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.dynamicPixelsPerUnit = 3.2f;

            // dark plate
            var plateGo = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            plateGo.transform.SetParent(go.transform, false);
            var prt = plateGo.GetComponent<RectTransform>();
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            plate = plateGo.GetComponent<Image>();
            plate.color = new Color(.055f, .055f, .075f, .88f);

            // gold rule along the top
            var ruleGo = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            ruleGo.transform.SetParent(go.transform, false);
            var rrt = ruleGo.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0, 1); rrt.anchorMax = new Vector2(1, 1);
            rrt.pivot = new Vector2(.5f, 1f);
            rrt.sizeDelta = new Vector2(0f, 4f);
            var rule = ruleGo.GetComponent<Image>();
            rule.color = new Color(.96f, .80f, .38f, .85f);

            headline = MakeText(go.transform, "Headline", 46, FontStyle.Bold,
                TextAnchor.UpperCenter, new Color(.96f, .95f, .90f),
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(10f, -46f), new Vector2(-10f, -8f));

            detail = MakeText(go.transform, "Detail", 28, FontStyle.Normal,
                TextAnchor.UpperCenter, new Color(.72f, .74f, .80f),
                new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(10f, -84f), new Vector2(-10f, -48f));
        }

        Text MakeText(Transform parent, string name, int size, FontStyle style,
                      TextAnchor anchor, Color color,
                      Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = oMin; rt.offsetMax = oMax;

            var t = go.GetComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "Arial" }, 46);
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void Update()
        {
            refresh -= Time.deltaTime;
            if (refresh > 0f) return;
            refresh = .12f;

            var run = ExpeditionRun.Instance;
            if (run == null || run.Graph == null || islandId < 0) { SetVisible(false); return; }

            var enc = run.Graph.Get(islandId);
            if (enc == null) { SetVisible(false); return; }

            float dist = player != null
                ? Vector3.Distance(player.position, transform.position)
                : 0f;

            near = dist < nearRadius;

            if (dist > visibleRadius) { SetVisible(false); return; }
            SetVisible(true);

            headline.text = UIMetrics.BiomeName(enc.biome) + " · "
                          + UIMetrics.ElementName(enc.element) + " · "
                          + UIMetrics.RiskName(enc.risk);
            headline.color = enc.risk == RiskTier.Boss ? UIMetrics.GoldLine
                           : enc.risk == RiskTier.Elite ? UIMetrics.AccentOrange
                           : UIMetrics.ElementColor(enc.element);

            detail.text = enc.rewardCategory;

            // Billboard: a WorldSpace Canvas faces +Z, so aim FROM the sign TO the player.
            // -dir would aim it away and mirror every glyph.
            if (player != null)
            {
                var dir = player.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        void SetVisible(bool v)
        {
            if (canvas != null && canvas.gameObject.activeSelf != v)
                canvas.gameObject.SetActive(v);
        }

        public bool IsPlayerNear => near;
    }
}
