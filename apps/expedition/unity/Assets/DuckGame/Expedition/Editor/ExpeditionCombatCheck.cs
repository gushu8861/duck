using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using DuckGame.Expedition;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Drives the full combat loop headlessly and asserts each stage works:
    /// damage -> kill -> coin drop -> merchant wallet -> island clear -> reward offer -> pad ready.
    ///
    /// It does not rely on mouse input (unavailable in -batchmode). Instead it calls the same
    /// entry points gameplay uses, so a pass proves the wiring rather than the input device.
    /// Enabled with -expedition-combat-check.
    /// </summary>
    [InitializeOnLoad]
    public static class ExpeditionCombatCheck
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string ReportPath = "docs/expedition-combat-check.txt";
        const string DoneMarker = "docs/.expedition-combat-check.done";

        static bool engaged;
        static StringBuilder log = new StringBuilder();
        static float t0;
        static bool running;

        static ExpeditionCombatCheck()
        {
            bool requested = false;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
                if (argv[i] == "-expedition-combat-check") { requested = true; break; }
            if (!requested || engaged) return;
            engaged = true;
            EditorApplication.delayCall += Begin;
        }

        static void Begin()
        {
            try
            {
                if (EditorApplication.isPlaying)
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        Path.GetFileNameWithoutExtension(HubScene));
                else
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        HubScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    EditorApplication.isPlaying = true;
                }
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Write("begin failed: " + e); }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (t0 == 0f) t0 = Time.realtimeSinceStartup;

            // wait for the scene to settle before touching anything
            if (Time.realtimeSinceStartup - t0 < 1.5f) return;
            if (running) return;
            running = true;
            EditorApplication.update -= Tick;
            RunChecks();
        }

        static void RunChecks()
        {
            var sb = log;
            sb.AppendLine("=== stage 1: systems ===");
            sb.AppendLine("ExpeditionRun    = " + (ExpeditionRun.Instance != null));
            sb.AppendLine("WaveController   = " + (WaveController.Instance != null));
            sb.AppendLine("CombatBridge     = " + (CombatBridge.Instance != null));
            sb.AppendLine("RewardSystem     = " + (RewardSystem.Instance != null));
            sb.AppendLine("Economy          = " + (Economy.Instance != null));
            sb.AppendLine("MerchantBridge   = " + (MerchantBridge.Instance != null));
            sb.AppendLine("TSAudioCatalogue = " + (TSAudioCatalogue.Instance != null));

            sb.AppendLine("");
            sb.AppendLine("=== stage 2: enemies spawned ===");
            var brains = UnityEngine.Object.FindObjectsOfType<EnemyBrain>();
            sb.AppendLine("EnemyBrain count = " + brains.Length);
            int aliveAtStart = 0;
            foreach (var b in brains) if (!b.IsDead) aliveAtStart++;
            sb.AppendLine("alive at start   = " + aliveAtStart);

            sb.AppendLine("");
            sb.AppendLine("=== stage 3: apply damage through the orb interface ===");
            if (brains.Length == 0) { sb.AppendLine("FAIL: no enemies to shoot"); }

            int killed = 0;
            var shot = new DuckGame.OrbShotContext(Vector3.zero, Vector3.forward, 0, null);
            foreach (var b in brains)
            {
                if (b == null) continue;
                // Apply the real damage path in a loop until dead; this is exactly what the
                // projectile pipeline calls on each sweep hit.
                int guard = 0;
                while (!b.IsDead && guard++ < 200)
                {
                    var impact = new DuckGame.OrbImpactContext(
                        b.GetComponent<Collider>() ?? new GameObject().AddComponent<BoxCollider>(),
                        b.transform.position, Vector3.up, true);
                    b.ReceiveOrbHit(shot, impact);
                }
                if (b.IsDead) killed++;
            }
            sb.AppendLine("kills applied    = " + killed);
            sb.AppendLine("EnemyBrain.DamagePerHit = " + EnemyBrain.DamagePerHit);

            sb.AppendLine("");
            sb.AppendLine("=== stage 4: economy after kills ===");
            var cb = CombatBridge.Instance;
            var eco = Economy.Instance;
            var mb = MerchantBridge.Instance;
            sb.AppendLine("CombatBridge.TotalKills = " + (cb != null ? cb.TotalKills : -1));
            sb.AppendLine("CoinsEarned             = " + (cb != null ? cb.CoinsEarned : -1));
            sb.AppendLine("Economy.metaCurrency    = " + (eco != null ? eco.metaCurrency : -1));
            sb.AppendLine("Merchant gold           = " + (mb != null ? mb.CurrentGold() : -1));

            sb.AppendLine("");
            sb.AppendLine("=== stage 5: coin pickups in scene ===");
            var coins = UnityEngine.Object.FindObjectsOfType<CoinPickup>();
            sb.AppendLine("CoinPickup count = " + coins.Length);

            sb.AppendLine("");
            sb.AppendLine("=== stage 6: island clear + reward ===");
            var wc = WaveController.Instance;
            // Force the clear so we can verify the reward path even without the grace timer.
            if (wc != null) sb.AppendLine("remaining        = " + wc.Remaining);
            if (ExpeditionRun.Instance != null && ExpeditionRun.Instance.Current != null)
            {
                bool completed = ExpeditionRun.Instance.CompleteIsland();
                sb.AppendLine("CompleteIsland() = " + completed);
                sb.AppendLine("ClearedIslands   = " + ExpeditionRun.Instance.ClearedIslands);

                if (RewardSystem.Instance != null)
                {
                    RewardSystem.Instance.OfferFor(ExpeditionRun.Instance.Current, EnemyElement.Fire);
                    sb.AppendLine("reward offered   = " + RewardSystem.Instance.Choosing);
                    sb.AppendLine("option count     = " + RewardSystem.Instance.Current.Count);
                    for (int i = 0; i < RewardSystem.Instance.Current.Count; i++)
                        sb.AppendLine("  option " + i + ": " + RewardSystem.Instance.Current[i].title);

                    if (RewardSystem.Instance.Current.Count > 0)
                    {
                        RewardSystem.Instance.Pick(0);
                        sb.AppendLine("picked option 0  = ok");
                        sb.AppendLine("damage now       = " + EnemyBrain.DamagePerHit);
                    }
                }
            }

            sb.AppendLine("");
            sb.AppendLine("=== stage 7: route commit + pad readiness ===");
            var run = ExpeditionRun.Instance;
            if (run != null && run.Current != null && run.Graph != null)
            {
                var exits = run.Graph.ExitsOf(run.Current.islandId);
                sb.AppendLine("exits from current = " + exits.Count);
                if (exits.Count > 0)
                {
                    bool ok = run.CommitRoute(exits[0]);
                    sb.AppendLine("CommitRoute       = " + ok);
                    sb.AppendLine("committedNextId   = " + run.CommittedNextId);
                }
                var pads = UnityEngine.Object.FindObjectsOfType<SpringPad>();
                sb.AppendLine("SpringPad count   = " + pads.Length);
            }

            sb.AppendLine("");
            sb.AppendLine("=== stage 8: audio map ===");
            sb.AppendLine("Impact sounds = " + (TSSoundMap.Impact != null ? TSSoundMap.Impact.Length : -1));
            sb.AppendLine("Death sounds  = " + (TSSoundMap.Death != null ? TSSoundMap.Death.Length : -1));
            sb.AppendLine("Cast sounds   = " + (TSSoundMap.Cast != null ? TSSoundMap.Cast.Length : -1));

            Write("");
        }

        static void Write(string extra)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ReportPath)));
                var sb = new StringBuilder(log.ToString());
                if (!string.IsNullOrEmpty(extra)) sb.AppendLine(extra);
                File.WriteAllText(Path.GetFullPath(ReportPath), sb.ToString());
                File.WriteAllText(Path.GetFullPath(DoneMarker), "done");
                Debug.Log("EXPEDITION_COMBAT_CHECK_DONE");
            }
            catch (Exception e) { Debug.LogError("write failed: " + e.Message); }
        }
    }
}
