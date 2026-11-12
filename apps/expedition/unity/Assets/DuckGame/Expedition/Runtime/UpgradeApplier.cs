using System;
using UnityEngine;
using DuckGame;          // OrbShooter
using DuckGame.TS;       // PooledOrbBridge

namespace DuckGame.Expedition
{
    /// <summary>
    /// Makes reward picks actually change the weapon, WITHOUT fighting the vending machine.
    ///
    /// History of this file (both bugs are worth keeping visible):
    ///   1. RewardSystem only accumulated stacks. The HUD printed "弹速 x1.44 射速 x1.20"
    ///      while OrbShooter kept firing with the original config — damage was nudged through
    ///      CombatBridge, but speed/rate/pellet count never reached the gun.
    ///   2. The first fix wrote cfg.speed = baseSpeed * multiplier. MerchantTrade multiplies
    ///      that SAME live config copy (speed *= 1.25, projectileCount += 1,
    ///      rateMultiplier *= 1.2), so every reward pick silently erased whatever the player
    ///      bought, and the two systems overwrote each other forever.
    ///
    /// The fix is DELTA application: remember what this component last wrote for each field,
    /// and only push the difference. Merchant bonuses therefore survive, and repeated calls
    /// with an unchanged multiplier are no-ops instead of resets.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class UpgradeApplier : MonoBehaviour
    {
        [Tooltip("Leave empty to auto-find.")]
        public OrbShooter shooter;

        // What this component last actually wrote, so the next write can be a delta.
        float writtenSpeedMult = 1f;
        float writtenRateMult = 1f;
        int writtenExtraPellets = 0;

        bool applied;

        void Start()
        {
            if (shooter == null) shooter = FindObjectOfType<OrbShooter>();
            Apply();
        }

        void Update()
        {
            var rw = RewardSystem.Instance;
            if (rw == null || shooter == null) return;

            if (!Mathf.Approximately(rw.ProjectileSpeedMultiplier(), writtenSpeedMult) ||
                !Mathf.Approximately(rw.FireRateMultiplier(), writtenRateMult) ||
                rw.ExtraProjectiles() != writtenExtraPellets)
            {
                Apply();
            }
        }

        public void Apply()
        {
            var rw = RewardSystem.Instance;
            if (rw == null || shooter == null) return;

            float speedMult = rw.ProjectileSpeedMultiplier();
            float rateMult = rw.FireRateMultiplier();
            int extra = rw.ExtraProjectiles();

            var cfg = shooter.config;

            // --- projectile speed: delta against the multiplier we previously contributed ---
            if (cfg != null && writtenSpeedMult > 0f)
            {
                float ratio = speedMult / writtenSpeedMult;
                if (!Mathf.Approximately(ratio, 1f))
                    cfg.speed = Mathf.Max(1f, cfg.speed * ratio);
            }

            // --- pellet count: delta of the extra pellets we added ---
            if (cfg != null)
            {
                int delta = extra - writtenExtraPellets;
                if (delta != 0)
                    cfg.projectileCount = Mathf.Clamp(cfg.projectileCount + delta, 1, 8);
            }

            // --- fire rate: same delta logic on the shooter's own multiplier ---
            if (writtenRateMult > 0f)
            {
                shooter.rateMultiplier *= rateMult / writtenRateMult;
                shooter.rateMultiplier = Mathf.Clamp(shooter.rateMultiplier, .1f, 10f);
            }

            writtenSpeedMult = speedMult;
            writtenRateMult = rateMult;
            writtenExtraPellets = extra;
            applied = true;

            var bridge = FindObjectOfType<PooledOrbBridge>();
            if (bridge != null) bridge.RefreshSpec();

            if (CombatBridge.Instance != null) CombatBridge.Instance.RefreshDamageFromUpgrades();
        }

        /// <summary>True once a multiplier has been pushed to the live weapon at least once.</summary>
        public bool Applied => applied;

        /// <summary>Diagnostics for verification: what this component believes it contributed.</summary>
        public (float speed, float rate, int extra) Written => (writtenSpeedMult, writtenRateMult, writtenExtraPellets);
    }
}
