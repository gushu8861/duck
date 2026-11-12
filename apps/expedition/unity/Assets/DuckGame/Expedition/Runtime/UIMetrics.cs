using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Resolution-independent UI metrics.
    ///
    /// The previous HUD used fixed pixel sizes, which collapse to unreadable ants on a 4K
    /// display. Everything here is expressed in "reference units" against a 1080p-height
    /// baseline and scaled by the actual screen height, so the layout looks identical at
    /// 720p, 1080p, 1440p and 4K.
    /// </summary>
    public static class UIMetrics
    {
        /// <summary>Height in pixels the layout was designed against.</summary>
        public const float ReferenceHeight = 1080f;

        /// <summary>Uniform scale factor from reference units to screen pixels.</summary>
        public static float Scale =>
            Screen.height <= 0 ? 1f : Mathf.Clamp(Screen.height / ReferenceHeight, .85f, 3.2f);

        public static float Px(float referenceUnits) => referenceUnits * Scale;

        public static int FontSize(int referencePt) => Mathf.RoundToInt(referencePt * Scale);

        public static Rect RectAt(float x, float y, float w, float h) =>
            new Rect(Px(x), Px(y), Px(w), Px(h));

        // ---- palette sampled from the imported Trials Survivors UI art ----
        public static readonly Color PanelBgDark = new Color(.055f, .055f, .075f, .93f);
        public static readonly Color PanelBgLight = new Color(.10f, .10f, .14f, .95f);
        public static readonly Color GoldLine = new Color(.96f, .80f, .38f);
        public static readonly Color GoldDim = new Color(.55f, .44f, .20f);
        public static readonly Color TextPrimary = new Color(.96f, .95f, .90f);
        public static readonly Color TextSecondary = new Color(.66f, .68f, .74f);
        public static readonly Color AccentCyan = new Color(.42f, .86f, .95f);
        public static readonly Color AccentGreen = new Color(.50f, .90f, .58f);
        public static readonly Color AccentRed = new Color(.95f, .42f, .40f);
        public static readonly Color AccentOrange = new Color(.98f, .66f, .28f);

        public static Color ElementColor(EnemyElement e) => e switch
        {
            EnemyElement.Ice => new Color(.48f, .80f, 1f),
            EnemyElement.Fire => new Color(1f, .46f, .26f),
            EnemyElement.Lightning => new Color(.74f, .54f, 1f),
            EnemyElement.Witchcraft => new Color(.62f, .42f, .76f),
            _ => Color.white,
        };

        public static Color RiskColor(RiskTier r) => r switch
        {
            RiskTier.Elite => new Color(1f, .58f, .46f),
            RiskTier.Boss => new Color(1f, .84f, .28f),
            _ => TextPrimary,
        };

        public static string BiomeName(Biome b) => b switch
        {
            Biome.Jungle => "丛林", Biome.Coast => "海岸",
            Biome.Desert => "沙漠", Biome.Snow => "雪原", _ => "未知",
        };

        public static string ElementName(EnemyElement e) => e switch
        {
            EnemyElement.Ice => "冰", EnemyElement.Fire => "火",
            EnemyElement.Lightning => "雷", EnemyElement.Witchcraft => "巫术", _ => "?",
        };

        public static string RiskName(RiskTier r) => r switch
        {
            RiskTier.Elite => "精英", RiskTier.Boss => "首领", _ => "普通",
        };
    }
}
