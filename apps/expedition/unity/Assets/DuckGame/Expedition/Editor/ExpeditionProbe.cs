using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DuckGame.Expedition;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Headless runtime verification. Opens the hub scene, enters Play, runs the expedition
    /// loop for a while and reports what actually happened: graph built, enemies spawned,
    /// orbs fired, damage applied, kills, coins, rewards offered, pads usable.
    ///
    /// Runs under -batchmode so nothing appears on the user's screen. It never calls
    /// EditorApplication.Exit; the caller kills the process after the report is written.
    /// </summary>
    [InitializeOnLoad]
    public static class ExpeditionProbe
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string ReportPath = "docs/expedition-probe.txt";
        const string DoneMarker = "docs/.expedition-probe.done";

        static bool started;

        static ExpeditionProbe()
        {
            // Only engage when explicitly requested, never on a normal editor open.
            bool requested = false;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
                if (argv[i] == "-expedition-probe") { requested = true; break; }
            if (!requested) return;
            if (started) return;
            started = true;
            EditorApplication.delayCall += Begin;
        }

        static void Begin()
        {
            try
            {
                if (!File.Exists(HubScene))
                {
                    Write("hub scene missing");
                    return;
                }

                // In -batchmode the editor auto-enters play mode, so OpenScene is unavailable
                // here. Load through SceneManager instead, which works in both modes.
                if (EditorApplication.isPlaying)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        System.IO.Path.GetFileNameWithoutExtension(HubScene));
                }
                else
                {
                    EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
                    EditorApplication.isPlaying = true;
                }

                EditorApplication.update += Tick;
            }
            catch (Exception e)
            {
                Write("begin failed: " + e);
            }
        }

        static float t0;
        static int frames;
        static StringBuilder log = new StringBuilder();
        static int lastRemaining = -1;

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (t0 == 0f) t0 = Time.realtimeSinceStartup;
            frames++;

            if (frames == 2)
            {
                log.AppendLine("=== systems present ===");
                log.AppendLine("ExpeditionRun   = " + (ExpeditionRun.Instance != null));
                log.AppendLine("RewardSystem    = " + (RewardSystem.Instance != null));
                log.AppendLine("WaveController  = " + (WaveController.Instance != null));
                log.AppendLine("CombatBridge    = " + (CombatBridge.Instance != null));
                log.AppendLine("CoinSpawner     = " + (CoinSpawner.Instance != null));
                log.AppendLine("MerchantBridge  = " + (MerchantBridge.Instance != null));
                log.AppendLine("Economy         = " + (Economy.Instance != null));
                log.AppendLine("TSAudioCatalogue= " + (TSAudioCatalogue.Instance != null));

                var run = ExpeditionRun.Instance;
                if (run != null)
                {
                    log.AppendLine("");
                    log.AppendLine("=== route graph ===");
                    log.AppendLine(run.DebugDump());
                }
            }

            var wc = WaveController.Instance;
            if (wc != null && wc.Remaining != lastRemaining)
            {
                lastRemaining = wc.Remaining;
                log.AppendLine("[t=" + (Time.realtimeSinceStartup - t0).ToString("F2") + "] enemies remaining = " + wc.Remaining);
            }

            // Run for 12 seconds of play, then report.
            if (Time.realtimeSinceStartup - t0 > 12f)
            {
                Finish();
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;

            var run = ExpeditionRun.Instance;
            var cb = CombatBridge.Instance;
            var eco = Economy.Instance;
            var mb = MerchantBridge.Instance;

            log.AppendLine("");
            log.AppendLine("=== final state ===");
            log.AppendLine("elapsed            = " + (Time.realtimeSinceStartup - t0).ToString("F2") + " s");
            log.AppendLine("orb spawns (legacy)= " + OrbProjectile.SpawnCount);
            log.AppendLine("orb hits   (legacy)= " + OrbProjectile.HitCount);

            // The pooled path replaces the legacy projectile owner, so SpawnCount alone reads
            // as "0 shots fired" even when the gun works. Report the active owner too.
            var pool = UnityEngine.Object.FindObjectOfType<DuckGame.TS.ProjectilePool>();
            var bridge = UnityEngine.Object.FindObjectOfType<DuckGame.TS.PooledOrbBridge>();
            log.AppendLine("pool spawned       = " + (pool != null ? pool.Spawned : -1));
            log.AppendLine("pool consumed      = " + (pool != null ? pool.Consumed : -1));
            log.AppendLine("bridge routed      = " + (bridge != null ? bridge.RoutedShots : -1));
            log.AppendLine("bridge enemy hits  = " + (bridge != null ? bridge.EnemyHits : -1));
            log.AppendLine("kills              = " + (cb != null ? cb.TotalKills : -1));
            log.AppendLine("coins earned       = " + (cb != null ? cb.CoinsEarned : -1));
            log.AppendLine("meta currency      = " + (eco != null ? eco.metaCurrency : -1));
            log.AppendLine("merchant gold      = " + (mb != null ? mb.CurrentGold() : -1));
            if (run != null)
            {
                log.AppendLine("cleared islands    = " + run.ClearedIslands);
                log.AppendLine("current island     = " + (run.Current != null ? run.Current.Preview : "none"));
                log.AppendLine("boss cleared       = " + run.BossCleared);
            }

            Write("");

            EditorApplication.isPlaying = false;
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
                Debug.Log("EXPEDITION_PROBE_DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("probe write failed: " + e.Message);
            }
        }
    }
}
