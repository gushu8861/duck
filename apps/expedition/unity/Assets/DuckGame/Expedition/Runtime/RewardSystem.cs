using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    public enum UpgradeKind { ProjectileSpeed, FireRate, ExtraProjectile, ElementAffix }
    public enum ElementForAffix { None, Ice, Fire, Lightning, Witchcraft }

    [Serializable]
    public sealed class UpgradeOption
    {
        public string id;
        public string title;
        public string description;
        public UpgradeKind kind;
        public ElementForAffix affix;
        public float magnitude;      // meaning depends on kind
        public int maxStacks = 3;
        public Color tint;

        public bool IsRelevantTo(EnemyElement mainElement)
        {
            // Design doc: at least one offered option must relate to the player's main element.
            if (kind != UpgradeKind.ElementAffix) return true;
            return affix == ToAffix(mainElement);
        }

        static ElementForAffix ToAffix(EnemyElement e) => e switch
        {
            EnemyElement.Ice => ElementForAffix.Ice,
            EnemyElement.Fire => ElementForAffix.Fire,
            EnemyElement.Lightning => ElementForAffix.Lightning,
            EnemyElement.Witchcraft => ElementForAffix.Witchcraft,
            _ => ElementForAffix.None,
        };
    }

    /// <summary>
    /// Post-clear reward selection: exactly one choice per cleared island, three options,
    /// at least one tied to the current main element, and stacks capped so the pool never
    /// runs dry (design doc lines 58-60).
    /// </summary>
    public sealed class RewardSystem : MonoBehaviour
    {
        public static RewardSystem Instance { get; private set; }

        /// <summary>All upgrade definitions. Kept small on purpose for the prototype.</summary>
        readonly List<UpgradeOption> catalogue = new List<UpgradeOption>();
        readonly Dictionary<string, int> stacks = new Dictionary<string, int>();

        public bool Choosing { get; private set; }
        public IReadOnlyList<UpgradeOption> Current { get; private set; } = Array.Empty<UpgradeOption>();

        public event Action OnOfferReady;
        public event Action<UpgradeOption> OnPicked;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildCatalogue();
        }

        void BuildCatalogue()
        {
            catalogue.Add(new UpgradeOption
            {
                id = "proj_speed", title = "顺风法球",
                description = "法球飞行速度 +25%（最多 3 层）",
                kind = UpgradeKind.ProjectileSpeed, magnitude = 1.25f, tint = new Color(.35f, .7f, 1f),
            });
            catalogue.Add(new UpgradeOption
            {
                id = "fire_rate", title = "轻盈装填",
                description = "射速 +20%（最多 3 层）",
                kind = UpgradeKind.FireRate, magnitude = 1.20f, tint = new Color(1f, .75f, .3f),
            });
            catalogue.Add(new UpgradeOption
            {
                id = "extra_proj", title = "同伴法球",
                description = "每发 +1 颗（总数最多 8）",
                kind = UpgradeKind.ExtraProjectile, magnitude = 1f, maxStacks = 5, tint = new Color(.75f, .5f, 1f),
            });
            catalogue.Add(new UpgradeOption
            {
                id = "affix_ice", title = "霜附魔",
                description = "主法球附带冰减速",
                kind = UpgradeKind.ElementAffix, affix = ElementForAffix.Ice, magnitude = .25f,
                tint = new Color(.45f, .8f, 1f),
            });
            catalogue.Add(new UpgradeOption
            {
                id = "affix_fire", title = "燃附魔",
                description = "主法球附带灼烧",
                kind = UpgradeKind.ElementAffix, affix = ElementForAffix.Fire, magnitude = .25f,
                tint = new Color(1f, .45f, .25f),
            });
            catalogue.Add(new UpgradeOption
            {
                id = "affix_lightning", title = "雷附魔",
                description = "主法球附带连锁",
                kind = UpgradeKind.ElementAffix, affix = ElementForAffix.Lightning, magnitude = .25f,
                tint = new Color(.7f, .5f, 1f),
            });
            catalogue.Add(new UpgradeOption
            {
                id = "affix_witch", title = "咒附魔",
                description = "主法球附带巫术标记",
                kind = UpgradeKind.ElementAffix, affix = ElementForAffix.Witchcraft, magnitude = .25f,
                tint = new Color(.55f, .4f, .7f),
            });
        }

        public int Stacks(string id) => stacks.TryGetValue(id, out int n) ? n : 0;

        /// <summary>Roll three options for this clear. Deterministic per island + seed.</summary>
        public void OfferFor(Encounter island, EnemyElement mainElement)
        {
            if (island == null) return;

            var rng = new SeedDerivation.Rng(
                SeedDerivation.Hash((uint)ExpeditionRun.Instance.runSeed, island.islandId,
                    SeedDerivation.Channel.Reward));

            // Pool: everything not maxed out.
            var pool = new List<UpgradeOption>();
            foreach (var u in catalogue)
                if (Stacks(u.id) < u.maxStacks) pool.Add(u);

            if (pool.Count == 0)
            {
                // Design doc: never leave an all-invalid reward screen.
                pool = new List<UpgradeOption>(catalogue);
            }

            var picks = new List<UpgradeOption>();

            // 1. Guarantee at least one element-relevant option.
            var relevant = pool.FindAll(u => u.IsRelevantTo(mainElement));
            if (relevant.Count > 0) picks.Add(relevant[rng.Range(0, relevant.Count)]);

            // 2. Fill up to three with distinct entries.
            int guard = 0;
            while (picks.Count < 3 && guard++ < 64)
            {
                var u = pool[rng.Range(0, pool.Count)];
                if (!picks.Contains(u)) picks.Add(u);
            }

            Current = picks;
            Choosing = true;

            // Card-shuffle for the roll, then the selection theme. Both clips are the original
            // game's card / power-up music, so the reward screen sounds like a reward screen.
            var cat = DuckGame.TS.TSAudioCatalogue.Instance;
            if (cat != null && cat.Has(DuckGame.TS.TSSoundMap.UiCardshuffle))
                cat.Play(DuckGame.TS.TSSoundMap.UiCardshuffle, .6f);

            DuckGame.TS.TSAudioCatalogue.Instance?.PlayMusic(DuckGame.TS.TSSoundMap.MusicReward, .45f);

            OnOfferReady?.Invoke();
        }

        public void Pick(int index)
        {
            if (!Choosing || index < 0 || index >= Current.Count) return;
            var u = Current[index];
            stacks[u.id] = Stacks(u.id) + 1;
            Choosing = false;
            Current = Array.Empty<UpgradeOption>();

            var cat = DuckGame.TS.TSAudioCatalogue.Instance;
            if (cat != null && cat.Has(DuckGame.TS.TSSoundMap.UiCarddraw))
                cat.Play(DuckGame.TS.TSSoundMap.UiCarddraw, .7f);

            OnPicked?.Invoke(u);
        }

        /// <summary>
        /// Grant an upgrade outside the island-clear flow (starting loadout, meta unlock,
        /// scripted reward). Respects the same stack cap as <see cref="OfferFor"/>.
        /// Returns false when the id is unknown or already maxed.
        /// </summary>
        public bool GrantUpgrade(string id)
        {
            var u = catalogue.Find(o => o.id == id);
            if (u == null || Stacks(u.id) >= u.maxStacks) return false;
            stacks[u.id] = Stacks(u.id) + 1;

            var cat = DuckGame.TS.TSAudioCatalogue.Instance;
            if (cat != null && cat.Has(DuckGame.TS.TSSoundMap.UiLevelup))
                cat.Play(DuckGame.TS.TSSoundMap.UiLevelup, .7f);

            OnPicked?.Invoke(u);
            return true;
        }

        // ---------- aggregated effect queries ----------

        public float ProjectileSpeedMultiplier()
        {
            float m = 1f;
            foreach (var u in catalogue)
                if (u.kind == UpgradeKind.ProjectileSpeed)
                    m *= Mathf.Pow(u.magnitude, Stacks(u.id));
            return m;
        }

        public float FireRateMultiplier()
        {
            float m = 1f;
            foreach (var u in catalogue)
                if (u.kind == UpgradeKind.FireRate)
                    m *= Mathf.Pow(u.magnitude, Stacks(u.id));
            return m;
        }

        public int ExtraProjectiles()
        {
            int n = 0;
            foreach (var u in catalogue)
                if (u.kind == UpgradeKind.ExtraProjectile)
                    n += Stacks(u.id);
            return n;
        }

        public bool HasAffix(ElementForAffix a)
        {
            foreach (var u in catalogue)
                if (u.kind == UpgradeKind.ElementAffix && u.affix == a && Stacks(u.id) > 0) return true;
            return false;
        }
    }
}
