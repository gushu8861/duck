using System;
using System.Collections.Generic;
using UnityEngine;
using DuckGame.TS;   // EnemyBrain / EnemyRole / EnemyTuning live in the TS namespace

namespace DuckGame.Expedition
{
    /// <summary>
    /// Tracks the enemies on the current island and reports when the island is cleared.
    ///
    /// Design rule (doc line 48): clearing must account for delayed spawns and summons, so a
    /// momentary "no enemies" does not fire the reward twice. We therefore require the
    /// remaining count to stay at zero for a grace period before declaring the clear, and
    /// the island can only complete once.
    /// </summary>
    public sealed class WaveController : MonoBehaviour
    {
        public static WaveController Instance { get; private set; }

        readonly List<EnemyBrain> alive = new List<EnemyBrain>();
        float zeroTimer;
        bool cleared;

        public int Remaining => alive.Count;
        public event Action OnCleared;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void Register(EnemyBrain brain)
        {
            if (brain == null || alive.Contains(brain)) return;
            alive.Add(brain);

            // NOTE: this must NOT clear `cleared` or reset the zero timer.
            // Enemies register late all the time (IslandSpawner runs after Start, and the hub
            // ships with pre-placed hostiles). Clearing the flag here let OnCleared fire a
            // second time on the same island, which popped a second reward screen.
            if (zeroTimer > 0f) zeroTimer = 0f;   // a live enemy cancels an in-progress clear window
        }

        public void Unregister(EnemyBrain brain)
        {
            alive.Remove(brain);
        }

        void Update()
        {
            // prune destroyed entries
            for (int i = alive.Count - 1; i >= 0; i--)
                if (alive[i] == null || alive[i].IsDead) alive.RemoveAt(i);

            if (cleared) return;

            if (alive.Count == 0)
            {
                zeroTimer += Time.deltaTime;
                // 1.2 s grace so a spawn wave mid-flow does not falsely clear the island.
                if (zeroTimer >= 1.2f)
                {
                    cleared = true;
                    OnCleared?.Invoke();

                    var run = ExpeditionRun.Instance;
                    // CompleteIsland() returns false when this island was already counted, so
                    // it doubles as the "one reward per island" guard. Without this, a second
                    // zero-count window (e.g. after a late register/unregister cycle) opened a
                    // second reward screen.
                    bool firstClear = run != null && run.CompleteIsland();

                    if (firstClear)
                    {
                        if (RewardSystem.Instance != null)
                            RewardSystem.Instance.OfferFor(run.Current, run.Current.element);

                        // Clearing an island pays into the hub machine so the loop closes.
                        if (MerchantBridge.Instance != null)
                            MerchantBridge.Instance.OnIslandCleared(run.Current);
                    }
                }
            }
            else zeroTimer = 0f;
        }

        void LateUpdate()
        {
            // Enemies move by writing transform.position and carry no Rigidbody, while the
            // project runs with m_AutoSyncTransforms = 0. Without this, a moved enemy's hit
            // capsule stays at its OLD position for the whole frame, so the player shoots at
            // where the creature used to be. Syncing once at the end of the frame is also far
            // cheaper than one implicit rebuild per enemy per write.
            if (alive.Count > 0) Physics.SyncTransforms();
        }

        /// <summary>
        /// Drop every tracked enemy. Used when changing islands.
        ///
        /// This also DESTROYS the enemies. It previously only emptied the list, which left
        /// live hostile GameObjects still chasing and damaging the player while being invisible
        /// to the clear rule — the island could be declared clear with enemies standing on it.
        /// </summary>
        public void ClearAll(bool destroyEnemies = true)
        {
            if (destroyEnemies)
            {
                for (int i = alive.Count - 1; i >= 0; i--)
                {
                    var b = alive[i];
                    if (b != null) Destroy(b.gameObject);
                }
            }
            alive.Clear();
            zeroTimer = 0f;
            cleared = false;
        }
    }

    /// <summary>
    /// Spawns the current island's enemies around the player at deterministic positions.
    /// Count and composition derive from the island's seed channel, so a fixed runSeed always
    /// produces the same fight.
    /// </summary>
    public sealed class IslandSpawner : MonoBehaviour
    {
        public Transform player;
        public Transform enemyParent;
        public GameObject[] enemyPrefabs;

        public float spawnRadius = 14f;
        public int baseCount = 5;
        public int perDepth = 2;

        int lastSpawnedForIsland = -1;

        void Update()
        {
            var run = ExpeditionRun.Instance;
            if (run == null || run.Current == null) return;
            if (run.Current.islandId == lastSpawnedForIsland) return;

            // Only spawn once the previous island's fight is done.
            if (!run.Current.completed && WaveController.Instance != null && WaveController.Instance.Remaining > 0)
                return;

            SpawnFor(run.Current);
            lastSpawnedForIsland = run.Current.islandId;
        }

        void SpawnFor(Encounter island)
        {
            if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;
            if (player == null) return;

            if (WaveController.Instance != null) WaveController.Instance.ClearAll();

            var rng = new SeedDerivation.Rng(
                SeedDerivation.Hash((uint)ExpeditionRun.Instance.runSeed, island.islandId,
                    SeedDerivation.Channel.Enemies));

            int count = baseCount + perDepth * Mathf.Max(0, island.depth - 1);
            if (island.risk == RiskTier.Elite) count += 3;
            if (island.isBoss) count += 6;

            for (int i = 0; i < count; i++)
            {
                var pf = enemyPrefabs[rng.Range(0, enemyPrefabs.Length)];
                if (pf == null) continue;

                var go = Instantiate(pf, enemyParent);
                float ang = (float)(i / (float)count * Math.PI * 2.0) + rng.Range(0f, .4f);
                float rad = spawnRadius * rng.Range(.75f, 1.15f);
                var pos = player.position + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);

                var cc = go.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                go.transform.position = pos;

                var brain = go.GetComponent<EnemyBrain>();
                if (brain == null) brain = go.AddComponent<EnemyBrain>();
                brain.target = player;

                var t = brain.tuning;
                t.role = (EnemyRole)rng.Range(0, 3);
                t.moveSpeed = 2.4f + rng.Range(0f, 1.8f);
                t.preferredRange = 6f + rng.Range(0f, 6f);
                t.aggression = .45f + rng.Range(0f, .55f);
                t.maxHealth = (island.isBoss ? 90f : 22f) + rng.Range(0f, 20f);
                t.contactDamage = 6f + rng.Range(0f, 6f);
                brain.tuning = t;

                var rend = go.GetComponentInChildren<Renderer>();
                if (rend != null) brain.bodyRenderer = rend;

                // Make the risk tier readable in the world: elite scales up and gains a ground
                // disc, boss gains a crown. Preview promise and actual fight now match.
                RiskVisuals.Apply(go, island.risk, island.element);

                if (WaveController.Instance != null) WaveController.Instance.Register(brain);
            }
        }
    }
}
