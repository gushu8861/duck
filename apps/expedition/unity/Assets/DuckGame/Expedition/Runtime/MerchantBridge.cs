using System;
using UnityEngine;
using DuckGame.TS;
using DuckGame.Maps.V08;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Feeds expedition kills into the hub vending machine's wallet.
    ///
    /// The existing MerchantTrade already implements the whole purchase flow (3 items,
    /// price checks, caps, immediate effect on the shooter, UI refresh). It just starts with
    /// a fixed 100 Gold and has no way to earn more. Rather than duplicating that logic, this
    /// bridge tops up its Gold from expedition progress so the economy actually closes the
    /// loop: fight -> earn -> buy -> fight better.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MerchantBridge : MonoBehaviour
    {
        public static MerchantBridge Instance { get; private set; }

        [Tooltip("Leave empty to auto-find the vending machine in the scene.")]
        public MerchantTrade trade;

        int grantedFromKills;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (trade == null) trade = FindObjectOfType<MerchantTrade>();
            if (trade == null)
            {
                // The V12 hub exposes the machine through an external presenter; find it too.
                var presenter = FindObjectOfType<DuckGame.Maps.V12.HubFacilitiesController>();
                if (presenter != null) trade = presenter.GetComponentInChildren<MerchantTrade>(true);
            }
        }

        /// <summary>
        /// MerchantTrade.Gold has a private setter, so we reflect once at startup to obtain a
        /// writable path. Reflection is used deliberately: the field is gameplay-relevant and
        /// already exists, and rewriting the v08 script would risk the verified merchant flow.
        /// </summary>
        System.Reflection.FieldInfo goldField;
        System.Reflection.PropertyInfo goldProp;

        bool EnsureAccess()
        {
            if (goldField != null || goldProp != null) return true;
            if (trade == null) return false;

            goldProp = typeof(MerchantTrade).GetProperty("Gold",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (goldProp != null && goldProp.CanWrite) return true;

            goldField = typeof(MerchantTrade).GetField("<Gold>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return goldField != null || (goldProp != null);
        }

        public int CurrentGold()
        {
            if (trade == null) return 0;
            if (!EnsureAccess()) return 0;
            if (goldField != null) return (int)goldField.GetValue(trade);
            return (int)goldProp.GetValue(trade);
        }

        public void AddGold(int amount)
        {
            if (amount <= 0 || trade == null) return;
            if (!EnsureAccess()) return;

            int current = CurrentGold();
            int next = current + amount;
            if (goldField != null) goldField.SetValue(trade, next);
            else if (goldProp != null && goldProp.CanWrite) goldProp.SetValue(trade, next);
        }

        /// <summary>Called by CombatBridge when an island is fully cleared.</summary>
        public void OnIslandCleared(Encounter island)
        {
            if (island == null) return;
            int bonus = island.risk == RiskTier.Elite ? 35
                      : island.isBoss ? 120
                      : 15;
            AddGold(bonus);
        }
    }
}
