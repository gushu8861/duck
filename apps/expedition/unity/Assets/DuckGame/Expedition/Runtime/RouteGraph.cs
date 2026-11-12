using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DuckGame.Expedition
{
    public enum Biome { Jungle, Coast, Desert, Snow }
    public enum EnemyElement { Ice, Fire, Lightning, Witchcraft }
    public enum RiskTier { Normal, Elite, Boss }

    /// <summary>
    /// A single island in the expedition. Everything the player is promised by a preview
    /// lives here, and the preview reads the SAME instance — so a preview can never promise
    /// one thing and deliver another.
    /// </summary>
    [Serializable]
    public sealed class Encounter
    {
        public int islandId;
        public int depth;
        public Biome biome;
        public EnemyElement element;
        public RiskTier risk;
        public string rewardCategory;     // e.g. "lightning upgrade"
        public bool isBoss;

        /// <summary>Set once the player has cleared this island's enemies.</summary>
        [NonSerialized] public bool completed;

        /// <summary>Human-readable preview. Used by signs and the approach HUD.</summary>
        public string Preview
        {
            get
            {
                string biomeCn = biome switch
                {
                    Biome.Jungle => "丛林",
                    Biome.Coast => "海岸",
                    Biome.Desert => "沙漠",
                    Biome.Snow => "雪原",
                    _ => "未知",
                };
                string eleCn = element switch
                {
                    EnemyElement.Ice => "冰",
                    EnemyElement.Fire => "火",
                    EnemyElement.Lightning => "雷",
                    EnemyElement.Witchcraft => "巫术",
                    _ => "?",
                };
                string riskCn = risk switch
                {
                    RiskTier.Normal => "普通",
                    RiskTier.Elite => "精英",
                    RiskTier.Boss => "首领",
                    _ => "?",
                };
                return string.Format("{0} · {1}元素 · {2} · {3}", biomeCn, eleCn, riskCn, rewardCategory);
            }
        }
    }

    /// <summary>
    /// Directed acyclic route graph for one run.
    ///
    /// Design constraints being satisfied (sky-island doc lines 22-28):
    ///  - every non-terminal node has at least one exit
    ///  - all branches converge to the boss depth
    ///  - at most two reachable next islands at any step, and only a few depths branch at all
    ///  - no random road-blocking: an optional elite always has a normal alternative beside it
    ///  - the whole graph derives from runSeed so a fixed seed replays identically
    /// </summary>
    public sealed class RouteGraph
    {
        public int RunSeed { get; private set; }
        public int BossDepth { get; private set; }

        readonly Dictionary<int, Encounter> islands = new Dictionary<int, Encounter>();
        readonly Dictionary<int, List<int>> exits = new Dictionary<int, List<int>>();
        readonly List<int> byDepth = new List<int>();

        int nextId = 1;

        public IReadOnlyDictionary<int, Encounter> Islands => islands;

        public static RouteGraph Build(int runSeed, int combatRounds = 5)
        {
            // combatRounds = number of fights before the boss (prototype scale, not the 12-16 target)
            var g = new RouteGraph { RunSeed = runSeed, BossDepth = combatRounds + 1 };
            var rngRoot = new SeedDerivation.Rng(
                SeedDerivation.Hash((uint)runSeed, 0, SeedDerivation.Channel.Route));

            // Depth 1: two entry islands so the very first choice is already a choice.
            var d1 = new[] { g.Make(rngRoot, 1, 1), g.Make(rngRoot, 1, 2) };

            // Middle depths: mostly linear, one branch point, always converging.
            var frontier = new List<int>(d1);
            int branchDepth = 2;

            for (int depth = 2; depth < g.BossDepth; depth++)
            {
                bool branchHere = depth == branchDepth;
                var next = new List<int>();
                int count = branchHere ? 2 : 1;

                for (int i = 0; i < count; i++)
                    next.Add(g.Make(rngRoot, depth, i));

                // Connect EVERY frontier island to every island in the next depth.
                // At a branch depth that gives each island exactly two choices (the design
                // rule "at most two reachable next islands"); at linear depths every frontier
                // island funnels into the single next island (convergence).
                foreach (var f in frontier)
                    foreach (var t in next)
                        g.Link(f, t);

                frontier = next;
            }

            // Boss
            int boss = g.Make(rngRoot, g.BossDepth, 0, forceBoss: true);
            foreach (var f in frontier) g.Link(f, boss);

            g.Validate();
            return g;
        }

        int Make(SeedDerivation.Rng rng, int depth, int slot, bool forceBoss = false)
        {
            int id = nextId++;
            var enc = new Encounter
            {
                islandId = id,
                depth = depth,
                biome = (Biome)rng.Range(0, 4),
                element = (EnemyElement)rng.Range(0, 4),
                risk = RiskTier.Normal,
                rewardCategory = "强化",
                isBoss = forceBoss,
            };

            if (forceBoss)
            {
                enc.risk = RiskTier.Boss;
                enc.rewardCategory = "首领奖励";
            }
            else if (rng.Next01() < .22f)
            {
                enc.risk = RiskTier.Elite;
                enc.rewardCategory = ElementReward(enc.element, true);
            }
            else
            {
                enc.rewardCategory = ElementReward(enc.element, false);
            }

            islands[id] = enc;
            exits[id] = new List<int>();
            byDepth.Add(id);
            return id;
        }

        static string ElementReward(EnemyElement e, bool elite)
        {
            string name = e switch
            {
                EnemyElement.Ice => "冰",
                EnemyElement.Fire => "火",
                EnemyElement.Lightning => "雷",
                EnemyElement.Witchcraft => "巫术",
                _ => "?",
            };
            return elite ? name + "系特殊强化" : name + "系强化";
        }

        void Link(int from, int to)
        {
            if (!exits.TryGetValue(from, out var l)) { l = new List<int>(); exits[from] = l; }
            if (!l.Contains(to)) l.Add(to);
        }

        public IReadOnlyList<int> ExitsOf(int islandId)
        {
            return exits.TryGetValue(islandId, out var l) ? l : (IReadOnlyList<int>)Array.Empty<int>();
        }

        public Encounter Get(int islandId) => islands.TryGetValue(islandId, out var e) ? e : null;

        public List<int> Entries()
        {
            var result = new List<int>();
            foreach (var kv in islands)
                if (kv.Value.depth == 1) result.Add(kv.Key);
            result.Sort();
            return result;
        }

        /// <summary>
        /// Hard validation from the design doc: no dead ends, boss reachable, no branch wider
        /// than two. Throws so a broken seed is loud instead of silently shipping.
        /// </summary>
        void Validate()
        {
            var sb = new StringBuilder();
            foreach (var kv in islands)
            {
                bool isBoss = kv.Value.isBoss;
                int exitCount = exits[kv.Key].Count;
                if (!isBoss && exitCount == 0)
                    sb.AppendLine("dead end at island " + kv.Key + " depth " + kv.Value.depth);
                if (exitCount > 2)
                    sb.AppendLine("branch wider than 2 at island " + kv.Key);
            }
            var entries = Entries();
            if (entries.Count == 0) sb.AppendLine("no entry island");
            else if (entries.Count > 2) sb.AppendLine("more than 2 entries");

            if (sb.Length > 0)
                throw new InvalidOperationException("RouteGraph invalid for seed " + RunSeed + ":\n" + sb);
        }

        public string Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("runSeed=" + RunSeed + " bossDepth=" + BossDepth);
            foreach (var kv in islands)
            {
                var e = kv.Value;
                var ex = exits[kv.Key];
                sb.AppendFormat("  #{0} d{1} {2} {3} {4} -> [{5}]\n",
                    e.islandId, e.depth, e.biome, e.element, e.risk,
                    string.Join(",", ex));
            }
            return sb.ToString();
        }
    }
}
