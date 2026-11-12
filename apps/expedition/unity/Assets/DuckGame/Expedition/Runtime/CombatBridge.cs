using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DuckGame.TS;

namespace DuckGame.Expedition
{
    /// <summary>
    /// Turns hits and deaths from the existing orb pipeline into expedition progress:
    /// damage scaling from upgrades, kill rewards, coin drops and audio.
    ///
    /// EnemyBrain implements IOrbHitReceiver and calls NotifyHit() here, so neither
    /// OrbShooter nor OrbProjectile had to be modified to gain damage and kills.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class CombatBridge : MonoBehaviour
    {
        public static CombatBridge Instance { get; private set; }

        [Header("Wiring")]
        public Transform player;

        [Header("Combat tuning")]
        public float baseDamage = 14f;

        [Header("Rewards per kill")]
        public int coinsPerKill = 4;
        public int coinsPerElite = 14;
        public int coinsPerBoss = 60;

        public int TotalKills { get; private set; }
        public int CoinsEarned { get; private set; }

        static readonly Dictionary<string, int> cursor = new Dictionary<string, int>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (player == null)
            {
                var shooter = FindObjectOfType<OrbShooter>();
                if (shooter != null) player = shooter.transform;
            }
            RefreshDamageFromUpgrades();
        }

        /// <summary>Reward offers change damage; called whenever an upgrade is taken.</summary>
        /// <summary>Reward offers change damage; called whenever an upgrade is taken.</summary>
        public void RefreshDamageFromUpgrades()
        {
            var rw = RewardSystem.Instance;
            if (rw == null) { EnemyBrain.DamagePerHit = baseDamage; return; }
            // Extra projectiles effectively raise dps; represent it as a per-hit bonus so a
            // single orb still feels like it lands harder as the build grows.
            EnemyBrain.DamagePerHit = baseDamage * (1f + rw.ExtraProjectiles() * .35f);
        }

        /// <summary>Called by EnemyBrain.ReceiveOrbHit for every successful orb contact.</summary>
        public void NotifyHit(EnemyBrain brain, Vector3 point)
        {
            if (brain == null) return;

            // The pool already reports environment vs enemy contacts; use the enemy impact
            // cue here so a hit on a body never sounds like a hit on a wall.
            PlayCue(TSSoundMap.HitEnemy, "impact", .5f);
            var fb = CombatFeedback.Instance;
            bool killing = brain.IsDead;
            float dealt = EnemyBrain.DamagePerHit;

            if (fb != null)
            {
                fb.DamageNumber(point, dealt, killing);
                fb.Shake(killing ? .10f : .035f);
            }

            // White pulse on the body so the hit lands physically, not just numerically.
            var rend = brain.GetComponentInChildren<Renderer>();
            if (rend != null) CombatFeedback.HitFlash(rend, killing ? .14f : .08f);

            if (killing) HandleKill(brain, point);
        }

        void HandleKill(EnemyBrain brain, Vector3 point)
        {
            TotalKills++;

            var run = ExpeditionRun.Instance;
            var enc = run != null ? run.Current : null;

            int coins = coinsPerKill;
            if (enc != null)
            {
                if (enc.risk == RiskTier.Elite) coins = coinsPerElite;
                else if (enc.risk == RiskTier.Boss) coins = coinsPerBoss;
            }
            CoinsEarned += coins;

            // Coins feed the shared economy: meta for unlocks, run budget for the machine.
            if (Economy.Instance != null) Economy.Instance.GrantMeta(coins);

            // Physical coin drops so the reward is visible, not just a number.
            if (CoinSpawner.Instance != null)
                CoinSpawner.Instance.Drop(brain.transform.position, coins, player);

            // Kill sound scales with the enemy: boss/elite use the big pop, trash uses small.
            string killCue = TSSoundMap.KillNormal;
            if (enc != null && enc.isBoss) killCue = TSSoundMap.KillBig;
            else if (enc != null && enc.risk == RiskTier.Elite) killCue = TSSoundMap.KillBig;
            else if (brain.tuning.role == EnemyRole.Fleer) killCue = TSSoundMap.KillSmall;
            PlayCue(killCue, "death", .7f);
            PlayCue(TSSoundMap.CoinDrop, "pickup", .35f);

            if (WaveController.Instance != null) WaveController.Instance.Unregister(brain);
            StartCoroutine(Despawn(brain.gameObject, point));
        }

        IEnumerator Despawn(GameObject go, Vector3 at)
        {
            // Short hit-stop sells the kill without interrupting aiming.
            yield return CombatFeedback.HitStop(.055f);

            float t = 0f;
            Vector3 start = go != null ? go.transform.localScale : Vector3.one;
            while (t < .24f && go != null)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / .24f);
                // overshoot then collapse: reads as a pop, not a shrink
                float s = k * (1f + (1f - k) * .35f);
                go.transform.localScale = start * Mathf.Max(.01f, s);
                go.transform.position = at + Vector3.up * (1f - k) * .5f;
                go.transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        static void PlayAt(string family, float volume)
        {
            var cat = TSAudioCatalogue.Instance;
            if (cat == null) return;

            string[] arr = family switch
            {
                "impact" => TSSoundMap.Impact,
                "death" => TSSoundMap.Death,
                "cast" => TSSoundMap.Cast,
                "ui" => TSSoundMap.Ui,
                "music" => TSSoundMap.Music,
                _ => TSSoundMap.Impact,
            };
            if (arr == null || arr.Length == 0) return;

            if (!cursor.TryGetValue(family, out int i)) i = 0;
            cursor[family] = (i + 1) % arr.Length;
            cat.Play(arr[i], volume);
        }

        /// <summary>
        /// Semantic one-shot. Prefers a named clip that was verified to mean this thing,
        /// then falls back to the family rotation. This is what stops kills from all sounding
        /// like the same generic impact.
        /// </summary>
        public static void PlayCue(string clipName, string fallbackFamily, float volume)
        {
            var cat = TSAudioCatalogue.Instance;
            if (cat == null) return;
            if (!string.IsNullOrEmpty(clipName) && cat.Has(clipName))
            {
                cat.Play(clipName, volume);
                return;
            }
            if (!string.IsNullOrEmpty(fallbackFamily)) PlayAt(fallbackFamily, volume);
        }
    }
}
