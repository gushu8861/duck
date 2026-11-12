using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.TS
{
    /// <summary>
    /// Currency + upgrade economy modelled on the Trials Survivors split
    /// (ARPGEntity_Module_InGameCurrency + shop modules). Two wallets on purpose:
    ///   - RunBudget : per-run only, reset every expedition (the hub vending machine spends this)
    ///   - Meta      : permanent, unlocks options rather than stacking raw power
    ///
    /// That matches the design doc: permanent resources unlock choices, the per-run budget
    /// buys temporary boosts, and the base loadout never needs permanent currency to be usable.
    /// </summary>
    public sealed class Economy : MonoBehaviour
    {
        public static Economy Instance { get; private set; }

        [Header("Per-run budget (reset each expedition)")]
        public int runBudget = 100;

        [Header("Permanent meta currency")]
        public int metaCurrency;

        public event Action OnChanged;

        public int RunSpent { get; private set; }
        public int RunRemaining => Mathf.Max(0, runBudget - RunSpent);

        /// <summary>Permanent unlocks already purchased, keyed by upgrade id.</summary>
        readonly HashSet<string> unlocked = new HashSet<string>();

        /// <summary>How many times each run-scoped upgrade was bought this run.</summary>
        readonly Dictionary<string, int> runPurchases = new Dictionary<string, int>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ---------- run budget ----------

        public int PurchaseCount(string id)
        {
            return runPurchases.TryGetValue(id, out int n) ? n : 0;
        }

        public bool TrySpendRun(int cost, string id, int maxTimes = 99)
        {
            if (cost < 0 || string.IsNullOrEmpty(id)) return false;
            if (PurchaseCount(id) >= maxTimes) return false;
            if (RunRemaining < cost) return false;

            RunSpent += cost;
            runPurchases[id] = PurchaseCount(id) + 1;
            OnChanged?.Invoke();
            return true;
        }

        public void ResetRun()
        {
            RunSpent = 0;
            runPurchases.Clear();
            OnChanged?.Invoke();
        }

        // ---------- meta currency ----------

        public void GrantMeta(int amount)
        {
            if (amount <= 0) return;
            metaCurrency += amount;
            OnChanged?.Invoke();
        }

        public bool Unlock(string id, int cost)
        {
            if (string.IsNullOrEmpty(id) || unlocked.Contains(id)) return false;
            if (metaCurrency < cost) return false;
            metaCurrency -= cost;
            unlocked.Add(id);
            OnChanged?.Invoke();
            return true;
        }

        public bool IsUnlocked(string id) => unlocked.Contains(id);
    }
}
