using System;
using UnityEngine;
using UnityEngine.UI;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Small helpers so the HUD can use the imported art without hand-wiring sprites in the
    /// inspector. Everything resolves through TSSprites, whose table is filled by an editor
    /// pass from the hand-verified mapping in docs/asset-mapping.md.
    ///
    /// If the sprite is absent the callers keep their text-only look, so the HUD never breaks
    /// just because an art key was not wired.
    /// </summary>
    public static class UIArt
    {
        /// <summary>
        /// Resolve the sprite database even when this runs before its Awake.
        ///
        /// ExpeditionHUD builds its canvas in Awake, and component Awake order between
        /// ExpeditionSystems and TS_Sprites is not guaranteed. Asking TSSprites.Instance alone
        /// returned null there, so every icon silently failed to be created and the HUD fell
        /// back to text-only. FindObjectOfType covers the not-yet-enabled case.
        /// </summary>
        public static DuckGame.TS.TSSprites DB
        {
            get
            {
                var db = DuckGame.TS.TSSprites.Instance;
                if (db == null) db = UnityEngine.Object.FindObjectOfType<DuckGame.TS.TSSprites>();
                return db;
            }
        }

        public static bool Ready => DB != null && DB.Count > 0;

        public static Sprite Sprite(string key)
        {
            var db = DB;
            return db != null ? db.Get(key) : null;
        }

        public static bool Has(string key) => Sprite(key) != null;

        /// <summary>Create an icon Image under a parent, or return null when the art is missing.</summary>
        public static Image Icon(Transform parent, string key, Vector2 anchor, Vector2 pos,
                                 Vector2 size, Color? tint = null)
        {
            var spr = Sprite(key);
            if (spr == null) return null;

            var go = new GameObject("icon_" + key, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = spr;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = tint ?? Color.white;
            return img;
        }

        /// <summary>Attach/refresh an icon Image that already exists on a GameObject.</summary>
        public static bool Assign(Image img, string key, Color? tint = null)
        {
            if (img == null) return false;
            var spr = Sprite(key);
            if (spr == null) { img.enabled = false; return false; }
            img.enabled = true;
            img.sprite = spr;
            img.preserveAspect = true;
            img.color = tint ?? Color.white;
            return true;
        }

        // ---------- element / risk key resolution ----------

        public static string ElementKey(EnemyElement e) => e switch
        {
            EnemyElement.Ice => DuckGame.TS.TSSprites.ElementIce,
            EnemyElement.Fire => DuckGame.TS.TSSprites.ElementFire,
            EnemyElement.Lightning => DuckGame.TS.TSSprites.ElementLightning,
            EnemyElement.Witchcraft => DuckGame.TS.TSSprites.ElementWitch,
            _ => DuckGame.TS.TSSprites.ElementFire,
        };

        public static string RiskKey(Encounter enc)
        {
            if (enc == null) return DuckGame.TS.TSSprites.RiskUnknown;
            if (enc.isBoss) return DuckGame.TS.TSSprites.RiskBoss;
            if (enc.risk == RiskTier.Elite) return DuckGame.TS.TSSprites.RiskElite;
            return DuckGame.TS.TSSprites.RiskNormal;
        }

        public static string DepthKey(int depth)
        {
            var db = DB;
            if (db == null) return null;
            string k = db.DepthBadge(depth);
            return db.Has(k) ? k : null;
        }

        /// <summary>Corner badge for an upgrade kind, matching the reward card.</summary>
        public static string AffixKey(UpgradeKind kind, ElementForAffix affix)
        {
            switch (kind)
            {
                case UpgradeKind.ProjectileSpeed: return DuckGame.TS.TSSprites.AffixSpeed;
                case UpgradeKind.FireRate: return DuckGame.TS.TSSprites.AffixRate;
                case UpgradeKind.ExtraProjectile: return DuckGame.TS.TSSprites.AffixCount;
                case UpgradeKind.ElementAffix:
                    return affix switch
                    {
                        ElementForAffix.Ice => DuckGame.TS.TSSprites.AffixIce,
                        ElementForAffix.Fire => DuckGame.TS.TSSprites.AffixFire,
                        ElementForAffix.Lightning => DuckGame.TS.TSSprites.AffixLightning,
                        ElementForAffix.Witchcraft => DuckGame.TS.TSSprites.AffixWitch,
                        _ => DuckGame.TS.TSSprites.AffixSpeed,
                    };
                default: return DuckGame.TS.TSSprites.AffixSpeed;
            }
        }

        /// <summary>Card face for an upgrade, so the三选一 reads as art rather than a colour block.</summary>
        public static string CardFaceKey(UpgradeKind kind, ElementForAffix affix)
        {
            switch (kind)
            {
                case UpgradeKind.ExtraProjectile: return DuckGame.TS.TSSprites.CardChakram;
                case UpgradeKind.ElementAffix:
                    return affix switch
                    {
                        ElementForAffix.Ice => DuckGame.TS.TSSprites.CardFrost,
                        ElementForAffix.Fire => DuckGame.TS.TSSprites.CardFire,
                        ElementForAffix.Lightning => DuckGame.TS.TSSprites.CardStorm,
                        ElementForAffix.Witchcraft => DuckGame.TS.TSSprites.CardSpread,
                        _ => DuckGame.TS.TSSprites.CardBolt,
                    };
                default: return DuckGame.TS.TSSprites.CardBolt;
            }
        }
    }
}
