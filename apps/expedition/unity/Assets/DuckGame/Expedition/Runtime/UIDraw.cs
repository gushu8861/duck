using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Small drawing helpers shared by every expedition panel.
    ///
    /// All sizes go through UIMetrics so the result is identical at any resolution. Panels use
    /// a dark slab with a gold hairline border, matching the imported Trials Survivors UI art
    /// (dark plate, warm gold rule, high-contrast text) rather than Unity's default grey skin.
    /// </summary>
    public static class UIDraw
    {
        static Texture2D white;
        static readonly Dictionary<int, Texture2D> gradients = new Dictionary<int, Texture2D>();
        static GUIStyle titleStyle, bodyStyle, smallStyle, bigStyle;
        static int cachedScale;

        public static Texture2D White
        {
            get
            {
                if (white == null)
                {
                    white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    white.SetPixel(0, 0, Color.white);
                    white.Apply();
                    white.hideFlags = HideFlags.HideAndDontSave;
                }
                return white;
            }
        }

        public static void EnsureStyles()
        {
            int s = Mathf.RoundToInt(UIMetrics.Scale * 10f);
            if (titleStyle != null && s == cachedScale) return;
            cachedScale = s;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIMetrics.FontSize(17),
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = false,
            };
            titleStyle.normal.textColor = UIMetrics.TextPrimary;

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIMetrics.FontSize(14),
                richText = true,
                wordWrap = true,
            };
            bodyStyle.normal.textColor = UIMetrics.TextPrimary;

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIMetrics.FontSize(12),
                richText = true,
                wordWrap = true,
            };
            smallStyle.normal.textColor = UIMetrics.TextSecondary;

            bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = UIMetrics.FontSize(26),
                fontStyle = FontStyle.Bold,
                richText = true,
                alignment = TextAnchor.MiddleCenter,
            };
            bigStyle.normal.textColor = UIMetrics.TextPrimary;
        }

        public static GUIStyle Title => titleStyle;
        public static GUIStyle Body => bodyStyle;
        public static GUIStyle Small => smallStyle;
        public static GUIStyle Big => bigStyle;

        public static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = prev;
        }

        /// <summary>Vertical gradient texture, cached per resolution-independent height.</summary>
        public static Texture2D Gradient(Color top, Color bottom, int height = 64)
        {
            int key = height * 397 ^ top.GetHashCode() ^ (bottom.GetHashCode() << 3);
            if (gradients.TryGetValue(key, out var t) && t != null) return t;

            t = new Texture2D(1, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                float k = y / (float)(height - 1);
                t.SetPixel(0, y, Color.Lerp(bottom, top, k));
            }
            t.Apply();
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            gradients[key] = t;
            return t;
        }

        /// <summary>The project's standard panel: dark slab, gold hairline, soft top highlight.</summary>
        public static void Panel(Rect r, bool emphasise = false)
        {
            // drop shadow
            Fill(new Rect(r.x + 3f, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, .35f));
            // body
            Fill(r, emphasise ? UIMetrics.PanelBgLight : UIMetrics.PanelBgDark);
            // top highlight band for depth
            Fill(new Rect(r.x, r.y, r.width, Mathf.Max(1f, UIMetrics.Px(2f))),
                new Color(1f, 1f, 1f, .07f));
            // gold hairline border
            var gold = emphasise ? UIMetrics.GoldLine : UIMetrics.GoldDim;
            float t = Mathf.Max(1f, UIMetrics.Px(1.5f));
            Fill(new Rect(r.x, r.y, r.width, t), gold);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), gold);
            Fill(new Rect(r.x, r.y, t, r.height), gold);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), gold);
        }

        /// <summary>Pill-shaped bar used for health and progress.</summary>
        public static void Bar(Rect r, float pct, Color fill, Color back)
        {
            Fill(r, back);
            float w = Mathf.Clamp01(pct) * r.width;
            if (w > 0f) Fill(new Rect(r.x, r.y, w, r.height), fill);
            // top sheen
            Fill(new Rect(r.x, r.y, r.width, Mathf.Max(1f, r.height * .35f)),
                new Color(1f, 1f, 1f, .12f));
            float t = Mathf.Max(1f, UIMetrics.Px(1f));
            Fill(new Rect(r.x, r.y, r.width, t), new Color(0f, 0f, 0f, .5f));
            Fill(new Rect(r.x, r.yMax - t, r.width, t), new Color(0f, 0f, 0f, .5f));
        }

        /// <summary>Keycap badge, e.g. [F]. Matches the reference game's control hints.</summary>
        public static void Keycap(Rect r, string key)
        {
            Fill(r, new Color(.14f, .14f, .18f, .96f));
            float t = Mathf.Max(1f, UIMetrics.Px(1f));
            Fill(new Rect(r.x, r.y, r.width, t), new Color(1f, 1f, 1f, .22f));
            Fill(new Rect(r.x, r.y, t, r.height), new Color(1f, 1f, 1f, .18f));
            Fill(new Rect(r.xMax - t, r.y, t, r.height), new Color(0f, 0f, 0f, .35f));
            Fill(new Rect(r.x, r.yMax - t, r.width, t), new Color(0f, 0f, 0f, .45f));

            var style = new GUIStyle(Small)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = UIMetrics.FontSize(12),
                fontStyle = FontStyle.Bold,
            };
            style.normal.textColor = UIMetrics.TextPrimary;
            GUI.Label(r, key, style);
        }

        public static void Text(Rect r, string s, GUIStyle style) => GUI.Label(r, s, style);

        public static void TextShadow(Rect r, string s, GUIStyle style)
        {
            var prev = style.normal.textColor;
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, .75f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), s, shadow);
            GUI.Label(r, s, style);
        }
    }
}
