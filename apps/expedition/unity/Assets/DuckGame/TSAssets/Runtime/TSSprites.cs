using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.TS
{
    /// <summary>
    /// Hand-curated lookup from gameplay meaning to the Trials Survivors sprites that
    /// ACTUALLY depict it.
    ///
    /// Everything here was verified by eye on a dark contact sheet, not guessed from the
    /// filename: e.g. UI_Elements_02_1 is a snowflake, UI_Elements_02_3 is claw marks, and
    /// AchievementsAtlas_75..78 are roman numerals II..V. Sprites that belong to unrelated
    /// things (console button glyphs, loading spinners, star-sign portraits, brand plates)
    /// are deliberately not indexed so nothing gets used out of place.
    ///
    /// The table is filled by an editor pass (TSIconWiring) with real sprite references.
    /// </summary>
    public sealed class TSSprites : MonoBehaviour
    {
        public static TSSprites Instance { get; private set; }

        [Serializable]
        public struct Entry
        {
            public string key;
            public Sprite sprite;
        }

        [SerializeField] Entry[] entries = Array.Empty<Entry>();

        readonly Dictionary<string, Sprite> map = new Dictionary<string, Sprite>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildMap();
        }

        void BuildMap()
        {
            map.Clear();
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (string.IsNullOrEmpty(e.key) || e.sprite == null) continue;
                map[e.key] = e.sprite;
            }
        }

        public void SetTable(Entry[] table) { entries = table; BuildMap(); }
        public int Count => map.Count;

        public Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return map.TryGetValue(key, out var s) ? s : null;
        }

        public bool Has(string key) => Get(key) != null;

        // ---------- stable gameplay keys ----------

        public const string ElementIce = "element.ice";
        public const string ElementFire = "element.fire";
        public const string ElementLightning = "element.lightning";
        public const string ElementWitch = "element.witch";

        public const string RiskNormal = "risk.normal";
        public const string RiskElite = "risk.elite";
        public const string RiskBoss = "risk.boss";
        public const string RiskUnknown = "risk.unknown";

        public const string StatHealth = "stat.health";
        public const string StatHealthUp = "stat.healthup";
        public const string StatCoin = "stat.coin";
        public const string StatKill = "stat.kill";
        public const string StatSpeed = "stat.speed";
        public const string StatRate = "stat.rate";
        public const string StatCount = "stat.count";
        public const string StatChest = "stat.chest";

        public const string AffixIce = "affix.ice";
        public const string AffixFire = "affix.fire";
        public const string AffixLightning = "affix.lightning";
        public const string AffixWitch = "affix.witch";
        public const string AffixSpeed = "affix.speed";
        public const string AffixRate = "affix.rate";
        public const string AffixCount = "affix.count";
        public const string AffixMax = "affix.max";
        public const string AffixTaken = "affix.taken";
        public const string CardMask = "card.mask";

        public const string CardStorm = "card.storm";
        public const string CardFire = "card.fire";
        public const string CardFrost = "card.frost";
        public const string CardBolt = "card.bolt";
        public const string CardChakram = "card.chakram";
        public const string CardSpread = "card.spread";

        public const string FacilityBank = "facility.bank";
        public const string FacilityFly = "facility.fly";
        public const string FacilityChest = "facility.chest";
        public const string FacilityLock = "facility.lock";
        public const string FacilityUpgrade = "facility.upgrade";
        public const string FacilityWarning = "facility.warning";

        public const string KeyF = "key.f";
        public const string KeyEsc = "key.esc";
        public const string MouseLeft = "key.mouseleft";

        public const string RouteBackdrop = "route.backdrop";
        public const string RouteLine = "route.line";
        public const string RouteSignFrame = "route.signframe";
        public const string RouteGlow = "route.glow";

        /// <summary>Depth badge 1..6 (depth beyond 6 reuses the last one).</summary>
        public string DepthBadge(int depth)
        {
            int i = Mathf.Clamp(depth, 1, 6);
            return "depth." + i;
        }
    }
}
