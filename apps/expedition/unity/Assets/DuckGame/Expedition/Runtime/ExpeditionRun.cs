using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Runtime state for one expedition run.
    ///
    /// Route selection is deliberately two-phase (design doc line 26):
    ///   Preview()/Approach() never commit. Only CommitRoute() locks the choice, so walking
    ///   past a facility or grazing a trigger volume cannot accidentally pick an island.
    /// </summary>
    public sealed class ExpeditionRun : MonoBehaviour
    {
        public static ExpeditionRun Instance { get; private set; }

        [Header("Run configuration")]
        public int runSeed = 20261111;
        public int combatRounds = 5;          // prototype scale; design target is 12-16

        [Header("State (read only at runtime)")]
        [SerializeField] int currentIslandId;
        [SerializeField] int committedNextId = -1;
        [SerializeField] int clearedIslands;
        [SerializeField] bool bossCleared;

        public RouteGraph Graph { get; private set; }
        public Encounter Current { get; private set; }
        public int ClearedIslands => clearedIslands;
        public bool BossCleared => bossCleared;

        /// <summary>Raised whenever the run advances so HUD and facilities can refresh.</summary>
        public event Action OnRunChanged;

        /// <summary>The island the player has walked up to but not committed to.</summary>
        public int PreviewedIslandId { get; private set; } = -1;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (Graph == null) BeginRun(runSeed);
        }

        public void BeginRun(int seed)
        {
            runSeed = seed;
            Graph = RouteGraph.Build(seed, combatRounds);
            clearedIslands = 0;
            bossCleared = false;
            committedNextId = -1;
            PreviewedIslandId = -1;

            var entries = Graph.Entries();
            // First island is picked by the same RNG channel as route, so it is stable per seed.
            var rng = new SeedDerivation.Rng(
                SeedDerivation.Hash((uint)seed, 0, SeedDerivation.Channel.Route, 99));
            GoTo(entries[rng.Range(0, entries.Count)]);
        }

        void GoTo(int islandId)
        {
            currentIslandId = islandId;
            Current = Graph.Get(islandId);
            committedNextId = -1;
            PreviewedIslandId = -1;
            OnRunChanged?.Invoke();
        }

        // ---------- preview (non-committing) ----------

        /// <summary>Show info for an island without locking anything in.</summary>
        public void Preview(int islandId)
        {
            if (Graph == null) return;
            if (Graph.Get(islandId) == null) return;

            // Only islands that are actually reachable from here may be previewed.
            var exits = Graph.ExitsOf(currentIslandId);
            bool reachable = false;
            for (int i = 0; i < exits.Count; i++)
                if (exits[i] == islandId) { reachable = true; break; }
            if (!reachable) return;

            PreviewedIslandId = islandId;
            OnRunChanged?.Invoke();
        }

        public void ClearPreview()
        {
            if (PreviewedIslandId == -1) return;
            PreviewedIslandId = -1;
            OnRunChanged?.Invoke();
        }

        // ---------- commit (locks the route) ----------

        /// <summary>Called only by the departure facility when the player actively confirms.</summary>
        public bool CommitRoute(int islandId)
        {
            if (Graph == null) return false;
            var exits = Graph.ExitsOf(currentIslandId);
            bool reachable = false;
            for (int i = 0; i < exits.Count; i++)
                if (exits[i] == islandId) { reachable = true; break; }
            if (!reachable) return false;
            if (committedNextId == islandId) return false;   // already committed

            committedNextId = islandId;
            OnRunChanged?.Invoke();
            return true;
        }

        public int CommittedNextId => committedNextId;

        // ---------- combat progress ----------

        /// <summary>
        /// Called when the current island's enemies are all dead. Grants exactly one reward
        /// opportunity per island, and only once.
        /// </summary>
        public bool CompleteIsland()
        {
            if (Current == null) return false;
            if (Current.completed) return false;

            Current.completed = true;
            clearedIslands++;
            if (Current.isBoss) bossCleared = true;
            OnRunChanged?.Invoke();
            return true;
        }

        /// <summary>Move to the committed island. Travel is a safe, scripted hop.</summary>
        public bool Depart()
        {
            if (committedNextId < 0) return false;
            if (!Current.completed) return false;
            GoTo(committedNextId);
            return true;
        }

        public string DebugDump()
        {
            if (Graph == null) return "(no graph)";
            return Graph.Dump() +
                   "\ncurrent=" + currentIslandId +
                   " (depth " + (Current != null ? Current.depth : -1) + ")" +
                   "\ncommitted=" + committedNextId +
                   "\ncleared=" + clearedIslands +
                   "\nbossCleared=" + bossCleared;
        }
    }
}
