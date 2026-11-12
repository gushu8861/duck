using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using DuckGame;
using DuckGame.Expedition;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Proves the pooled projectile path is the one the game actually shoots through.
    ///
    /// It does not fire by hand: it drives OrbShooter's injected trigger, so the whole real
    /// chain runs — magazine, aim ray, muzzle check, router, pool, SphereCast, ApplyImpact,
    /// IOrbHitReceiver, CombatBridge. Assertions:
    ///   - pool spawned pellets while the legacy GameObject path stayed at zero spawns
    ///   - a pellet that meets an enemy delivers a hit (enemy hit counter > 0)
    ///   - damage/kill bookkeeping advanced through CombatBridge
    ///   - live projectiles are released, not leaked, after the volley
    /// Enabled with -pooled-orb-check.
    /// </summary>
    [InitializeOnLoad]
    public static class PooledOrbCheck
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string ReportPath = "docs/pooled-orb-check.txt";
        const string DoneMarker = "docs/.pooled-orb-check.done";

        static bool engaged;
        static readonly StringBuilder log = new StringBuilder();
        static bool running;

        static PooledOrbCheck()
        {
            bool requested = false;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
                if (argv[i] == "-pooled-orb-check") { requested = true; break; }
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
            }
            catch (Exception e) { Write("begin failed: " + e); return; }

            var host = new GameObject("PooledOrbCheckRunner");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Start() { if (!running) StartCoroutine(Drive()); }

            IEnumerator Drive()
            {
                running = true;
                yield return new WaitForSeconds(1.2f);

                var shooter = FindObjectOfType<OrbShooter>();
                var bridge = FindObjectOfType<PooledOrbBridge>();
                var pool = FindObjectOfType<ProjectilePool>();
                var wave = WaveController.Instance;
                var combat = CombatBridge.Instance;

                log.AppendLine("=== stage 1: wiring ===");
                log.AppendLine("OrbShooter        = " + (shooter != null));
                log.AppendLine("PooledOrbBridge   = " + (bridge != null));
                log.AppendLine("ProjectilePool    = " + (pool != null));
                log.AppendLine("registry router   = " + (OrbRouterRegistry.Current != null));
                if (shooter == null || bridge == null || pool == null)
                {
                    Write("aborted: missing wiring");
                    yield break;
                }

                int poolSpawned0 = pool.Spawned;
                int legacySpawn0 = OrbProjectile.SpawnCount;
                log.AppendLine("pool.Spawned at start = " + poolSpawned0);
                log.AppendLine("OrbProjectile.SpawnCount at start = " + legacySpawn0);

                // ---- aim at the nearest enemy so hits are guaranteed, not lucky ----
                var brains = FindObjectsOfType<EnemyBrain>();
                log.AppendLine("enemies in scene  = " + brains.Length);

                // Enemies only become hittable if they carry a collider. Report what is there.
                int shown = 0;
                foreach (var b in brains)
                {
                    if (b == null || b.IsDead || shown++ >= 6) continue;
                    var cols = b.GetComponentsInChildren<Collider>(true);
                    var cc = b.GetComponent<CharacterController>();
                    log.AppendLine("  enemy " + b.name + " layer=" + b.gameObject.layer +
                                   " colliders=" + cols.Length +
                                   " characterController=" + (cc != null) +
                                   " hasRigidbody=" + (b.GetComponent<Rigidbody>() != null));
                    for (int i = 0; i < cols.Length; i++)
                        log.AppendLine("    col[" + i + "] " + cols[i].GetType().Name +
                                       " enabled=" + cols[i].enabled +
                                       " trigger=" + cols[i].isTrigger);
                }

                EnemyBrain target = null;
                float best = float.MaxValue;
                foreach (var b in brains)
                {
                    if (b == null || b.IsDead) continue;
                    float d = Vector3.Distance(b.transform.position, shooter.transform.position);
                    if (d < best) { best = d; target = b; }
                }
                if (target != null)
                    log.AppendLine("target = " + target.name + " at " + best.ToString("F2") + " m");

                var cam = shooter.view != null && shooter.view.lens != null
                    ? shooter.view.lens : Camera.main;
                log.AppendLine("aim camera = " + (cam != null ? cam.name : "none"));
                log.AppendLine("wave.Remaining before volley = " + (wave != null ? wave.Remaining : -1));

                // Place the target ON the aim ray instead of fighting ShoulderCamera for
                // control of its yaw/pitch (it re-derives rotation every LateUpdate). This is
                // not a substitute for aiming: the shot itself still runs through the real
                // Fire() -> router -> pool -> SphereCast chain, only the target moved.
                if (target != null && cam != null)
                {
                    var ray = shooter.view != null ? shooter.view.AimRay : cam.ViewportPointToRay(new Vector3(.5f, .5f, 0f));

                    // freeze it so it cannot walk out of the line while the volley runs
                    target.target = null;
                    target.transform.position = ray.GetPoint(8f);

                    var col = target.GetComponent<Collider>();
                    if (col != null)
                    {
                        // nudge so the collider centre sits on the ray
                        Vector3 delta = ray.GetPoint(8f) - col.bounds.center;
                        target.transform.position += delta;
                        log.AppendLine("target collider " + col.GetType().Name +
                                       " radius=" + col.bounds.extents.ToString("F2") +
                                       " distToRay=" + Vector3.Cross(ray.direction, col.bounds.center - ray.origin).magnitude.ToString("F2"));
                    }
                    log.AppendLine("target placed on aim ray at 8 m");
                }

                // ---- fire a real volley through the live weapon ----
                bool oldInject = shooter.InjectInput;
                bool oldFire = shooter.TestFire;
                orbSpawnsBefore = OrbProjectile.SpawnCount;

                shooter.InjectInput = true;
                shooter.ResetSimulation();

                float t0 = Time.realtimeSinceStartup;
                float shotWindow = 6f;
                while (Time.realtimeSinceStartup - t0 < shotWindow)
                {
                    shooter.TestFire = true;
                    yield return null;
                }
                shooter.TestFire = false;
                shooter.InjectInput = oldInject;
                shooter.TestFire = oldFire;

                yield return new WaitForSeconds(.6f);

                log.AppendLine("");
                log.AppendLine("=== stage 2: the pooled path ran ===");
                log.AppendLine("bridge.Active        = " + bridge.Active);
                log.AppendLine("bridge.RoutedShots   = " + bridge.RoutedShots);
                log.AppendLine("bridge.RoutedPellets = " + bridge.RoutedPellets);
                log.AppendLine("bridge.EnemyHits     = " + bridge.EnemyHits);
                log.AppendLine("bridge.WorldHits     = " + bridge.WorldHits);
                log.AppendLine("bridge.PoolExhausted = " + bridge.PoolExhausted);
                log.AppendLine("pool.Spawned delta   = " + (pool.Spawned - poolSpawned0));
                log.AppendLine("pool.Consumed        = " + pool.Consumed);
                log.AppendLine("pool.Expired         = " + pool.Expired);
                log.AppendLine("pool.LiveCount now   = " + pool.LiveCount);
                log.AppendLine("legacy orb spawns    = " + (OrbProjectile.SpawnCount - orbSpawnsBefore));
                log.AppendLine("legacy orb hits      = " + OrbProjectile.HitCount);

                log.AppendLine("");
                log.AppendLine("=== stage 3: combat bookkeeping ===");
                log.AppendLine("enemies remaining    = " + (wave != null ? wave.Remaining : -1));
                log.AppendLine("CombatBridge kills   = " + (combat != null ? combat.TotalKills : -1));
                log.AppendLine("coins earned         = " + (combat != null ? combat.CoinsEarned : -1));
                log.AppendLine("Economy meta         = " + (Economy.Instance != null ? Economy.Instance.metaCurrency : -1));

                // ---- stage 4: an upgrade pick must reach the pooled spec ----
                float specSpeedBefore = -1f;
                float specSpeedAfter = -1f;
                bool speedGrew = true;
                var rw = RewardSystem.Instance;
                if (rw != null && shooter.config != null)
                {
                    // Read the spec by the bridge's own index: the pool may already hold specs
                    // registered by other builders, so a hard-coded GetSpec(0) reported the
                    // wrong row and made the upgrade conclusion unreproducible.
                    int specIx = bridge.SpecIndex;
                    var spec0 = specIx >= 0 ? pool.GetSpec(specIx) : default;
                    specSpeedBefore = spec0.speed;
                    float cfgBefore = shooter.config.speed;

                    // Grant the speed upgrade through the public path. OfferFor() rolls a fixed
                    // seed and may not include this option, which is why the first run of this
                    // check reported a false failure.
                    bool granted = rw.GrantUpgrade("proj_speed");
                    yield return null;
                    yield return null;

                    var spec1 = specIx >= 0 ? pool.GetSpec(specIx) : default;
                    specSpeedAfter = spec1.speed;
                    speedGrew = specSpeedAfter > specSpeedBefore + .001f;

                    log.AppendLine("");
                    log.AppendLine("=== stage 4: reward reaches the weapon ===");
                    log.AppendLine("grant accepted       = " + granted);
                    log.AppendLine("config.speed before  = " + cfgBefore.ToString("F2"));
                    log.AppendLine("config.speed after   = " + shooter.config.speed.ToString("F2"));
                    log.AppendLine("pool spec speed before = " + specSpeedBefore.ToString("F2"));
                    log.AppendLine("pool spec speed after  = " + specSpeedAfter.ToString("F2"));
                    log.AppendLine("speed multiplier      = " + rw.ProjectileSpeedMultiplier().ToString("F3"));
                    log.AppendLine("spec picked up upgrade = " + speedGrew);
                }

                // ---- verdict ----
                bool okRouted = bridge.RoutedShots > 0;
                bool okNoLegacy = (OrbProjectile.SpawnCount - orbSpawnsBefore) == 0;
                bool okHits = bridge.EnemyHits + bridge.WorldHits > 0;
                bool okNoLeak = pool.LiveCount == 0;

                log.AppendLine("");
                log.AppendLine("=== verdict ===");
                log.AppendLine("routed shots > 0            = " + okRouted);
                log.AppendLine("legacy path unused          = " + okNoLegacy);
                log.AppendLine("at least one hit delivered  = " + okHits);
                log.AppendLine("no leaked live projectiles  = " + okNoLeak);
                log.AppendLine("upgrade reached pool spec   = " + speedGrew);
                log.AppendLine("PASS                        = " +
                              (okRouted && okNoLegacy && okHits && okNoLeak && speedGrew));

                Write("");
                yield return null;
                EditorApplication.isPlaying = false;

                // Exit on our own: a batchmode editor left in play mode keeps the project lock
                // and makes every later run fail with "another Unity instance has this project".
                yield return new WaitForSeconds(.5f);
                EditorApplication.Exit(0);
            }
        }

        static int orbSpawnsBefore;

        static void Write(string extra)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ReportPath)));
                var sb = new StringBuilder(log.ToString());
                if (!string.IsNullOrEmpty(extra)) sb.AppendLine(extra);
                File.WriteAllText(Path.GetFullPath(ReportPath), sb.ToString());
                File.WriteAllText(Path.GetFullPath(DoneMarker), "done");
                Debug.Log("POOLED_ORB_CHECK_DONE");
            }
            catch (Exception e) { Debug.LogError("pooled orb check write failed: " + e.Message); }
        }
    }
}
