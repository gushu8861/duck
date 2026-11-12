using System;
using System.Collections;
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
    /// Verifies the enemy ranged attack actually lands on the player.
    ///
    /// Why a dedicated check: the player's body sits on layer 2 ("Ignore Raycast"), which
    /// Physics.DefaultRaycastLayers excludes. An enemy projectile swept with the default mask
    /// can never touch the player, and nothing in the game log says so — the enemy just looks
    /// like it is missing. This drives a Keeper/Fleer in range and asserts the player's health
    /// actually drops from a projectile (not from contact damage).
    /// Enabled with -enemy-ranged-check.
    /// </summary>
    [InitializeOnLoad]
    public static class EnemyRangedCheck
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string ReportPath = "docs/enemy-ranged-check.txt";
        const string DoneMarker = "docs/.enemy-ranged-check.done";

        static bool engaged;
        static readonly StringBuilder log = new StringBuilder();
        static bool running;

        static EnemyRangedCheck()
        {
            bool requested = false;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
                if (argv[i] == "-enemy-ranged-check") { requested = true; break; }
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

            EditorApplication.update += SpawnRunnerWhenPlaying;
        }

        static void SpawnRunnerWhenPlaying()
        {
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (UnityEngine.Object.FindObjectOfType<Runner>() != null)
            {
                EditorApplication.update -= SpawnRunnerWhenPlaying;
                return;
            }
            EditorApplication.update -= SpawnRunnerWhenPlaying;
            var host = new GameObject("EnemyRangedRunner");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            void Start() { if (!running) StartCoroutine(Drive()); }

            IEnumerator Drive()
            {
                running = true;
                yield return new WaitForSeconds(1.5f);

                var pool = FindObjectOfType<ProjectilePool>();
                var bridge = FindObjectOfType<PooledOrbBridge>();
                var hp = PlayerHealth.Instance;
                var wc = WaveController.Instance;

                log.AppendLine("=== stage 1: setup ===");
                log.AppendLine("pool            = " + (pool != null));
                log.AppendLine("bridge          = " + (bridge != null));
                log.AppendLine("PlayerHealth    = " + (hp != null));

                // There can be more than one ProjectilePool in the scene (TS_Systems carries
                // one, and the bridge creates PooledOrbs when none is found). Reading counters
                // from a different instance than the one the enemies shoot into made an earlier
                // run report "spawned delta 0" while the shot had in fact been fired.
                var pools = FindObjectsOfType<ProjectilePool>();
                log.AppendLine("pool instances  = " + pools.Length);
                for (int i = 0; i < pools.Length; i++)
                    log.AppendLine("  pool[" + i + "] " + pools[i].name +
                                   " id=" + pools[i].GetInstanceID() +
                                   " specs=" + pools[i].SpecCount +
                                   " spawned=" + pools[i].Spawned);

                if (bridge != null && bridge.pool != null && bridge.pool != pool)
                {
                    pool = bridge.pool;
                    log.AppendLine("using bridge.pool = " + pool.name + " id=" + pool.GetInstanceID());
                }
                if (pool == null || hp == null)
                {
                    Write("aborted: missing pool or PlayerHealth");
                    yield break;
                }

                // Stage the situation with a DEDICATED enemy rather than re-purposing a scene
                // one: IslandSpawner re-populates the island on its first Update and (since
                // ClearAll now really destroys) it can remove the enemy we just configured.
                // A fresh enemy created after that pass is stable for the whole measurement.
                var brains = FindObjectsOfType<EnemyBrain>();
                log.AppendLine("enemies in scene = " + brains.Length);

                var prefab = brains.Length > 0 ? brains[0].gameObject : null;
                if (prefab == null) { Write("aborted: no enemy prefab source in scene"); yield break; }

                var testGo = Instantiate(prefab);
                testGo.name = "RangedTestEnemy";

                // The imported creature prefabs carry their own behaviour scripts and a
                // CharacterController. Left enabled they snap the instance back to wherever
                // their own logic wants it, which is why an earlier run measured a 0 m gap and
                // the brain's Update() bailed out on dist < 0.0001f before it could shoot.
                // Keep ONLY EnemyBrain (and its visual renderers) active.
                foreach (var mb in testGo.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && !(mb is EnemyBrain)) mb.enabled = false;
                foreach (var cc in testGo.GetComponentsInChildren<CharacterController>(true))
                    cc.enabled = false;
                foreach (var rb in testGo.GetComponentsInChildren<Rigidbody>(true))
                    rb.isKinematic = true;

                var shooter = testGo.GetComponent<EnemyBrain>();
                if (shooter == null) shooter = testGo.AddComponent<EnemyBrain>();

                // PlayerHealth sits on GoosePlayer, whose transform is NOT the rig the game
                // moves — the rig is what CombatBridge targets. Measuring from the health
                // component put the test enemy at (0,0,8) while the player stood elsewhere, so
                // the reported "distance 0.04" was an artefact of reading the wrong transform.
                var combat = CombatBridge.Instance;
                var playerT = (combat != null && combat.player != null)
                    ? combat.player
                    : (FindObjectOfType<OrbShooter>() != null
                        ? FindObjectOfType<OrbShooter>().transform
                        : hp.transform);

                log.AppendLine("player transform  = " + playerT.name + " at " + playerT.position);
                log.AppendLine("health transform  = " + hp.transform.name + " at " + hp.transform.position);

                shooter.enabled = true;
                shooter.target = playerT;
                var t = shooter.tuning;
                t.role = EnemyRole.Keeper;
                t.moveSpeed = 0f;          // stand still: contact damage must not be the cause
                t.maxHealth = 99999f;      // survive the whole measurement
                shooter.tuning = t;
                shooter.shotInterval = .4f;
                shooter.shotRange = 60f;
                shooter.shotDamage = 9f;
                shooter.speedForTest = 22f;

                // Park every other enemy far away so only this one can possibly hurt the player.
                foreach (var b in brains)
                    if (b != null) b.transform.position += Vector3.up * 300f;

                hp.ResetHealth();
                yield return new WaitForSeconds(.4f);

                Vector3 want = playerT.position + new Vector3(0f, 0f, 9f);
                testGo.transform.position = want;
                testGo.transform.rotation = Quaternion.LookRotation(-Vector3.forward, Vector3.up);
                yield return null;
                yield return null;

                log.AppendLine("wanted position   = " + want);
                log.AppendLine("actual position   = " + testGo.transform.position);
                log.AppendLine("lossyScale        = " + testGo.transform.lossyScale);

                // Which scripts could be moving it? List everything still enabled.
                var movers = new StringBuilder();
                foreach (var mb in testGo.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.enabled) movers.Append(mb.GetType().Name + " ");
                log.AppendLine("enabled scripts   = " + movers);

                // Find out where the PLAYER really is: PlayerRig is the parent rig, but the
                // thing the game actually walks around may be a child (GoosePlayer).
                var goose = GameObject.Find("GoosePlayer");
                log.AppendLine("GoosePlayer       = " + (goose != null ? goose.transform.position.ToString() : "none"));
                var rig = GameObject.Find("PlayerRig");
                if (rig != null)
                {
                    log.AppendLine("PlayerRig children= " + rig.transform.childCount);
                    for (int i = 0; i < rig.transform.childCount && i < 8; i++)
                    {
                        var c = rig.transform.GetChild(i);
                        log.AppendLine("  child[" + i + "] " + c.name + " at " + c.position);
                    }
                }
                log.AppendLine("distance at start = " +
                                Vector3.Distance(testGo.transform.position, playerT.position).ToString("F2"));

                log.AppendLine("test enemy role   = " + shooter.tuning.role);
                log.AppendLine("test enemy speed  = " + shooter.tuning.moveSpeed);
                log.AppendLine("test enemy alive  = " + !shooter.IsDead);

                float hpStart = hp.Health;
                int shotHits0 = bridge != null ? bridge.EnemyShotHits : -1;
                int spawned0 = pool.Spawned;
                int specsBefore = pool.SpecCount;

                log.AppendLine("");
                log.AppendLine("=== stage 2: ranged volley (2.5 s) ===");
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 2.5f) yield return null;

                log.AppendLine("shots fired by test enemy = " + shooter.ShotsFired);
                log.AppendLine("specs before/after        = " + specsBefore + " / " + pool.SpecCount);
                log.AppendLine("spec is enemy spec        = " +
                                (shooter.SpecForTest >= 0 ? pool.IsEnemySpec(shooter.SpecForTest).ToString() : "n/a"));

                int shotHits1 = bridge != null ? bridge.EnemyShotHits : -1;
                int spawned1 = pool.Spawned;
                float hpEnd = hp.Health;

                log.AppendLine("pool spawned          = " + spawned1 + " (delta " + (spawned1 - spawned0) + ")");
                log.AppendLine("enemy shots landed    = " + shotHits1 + " (delta " + (shotHits1 - shotHits0) + ")");
                log.AppendLine("player hp             = " + hpStart.ToString("F1") + " -> " + hpEnd.ToString("F1"));
                log.AppendLine("enemy distance now    = " +
                                Vector3.Distance(shooter.transform.position, playerT.position).ToString("F2"));

                bool shotFired = shooter.ShotsFired > 0;
                bool shotLanded = shotHits1 > shotHits0;
                bool playerHurt = hpEnd < hpStart - .01f;

                log.AppendLine("");
                log.AppendLine("=== verdict ===");
                log.AppendLine("enemy fired a projectile   = " + shotFired);
                log.AppendLine("projectile reached player  = " + shotLanded);
                log.AppendLine("player health dropped      = " + playerHurt);
                bool pass = shotFired && shotLanded && playerHurt;
                log.AppendLine("PASS                       = " + pass);

                Write("");
                yield return null;
                EditorApplication.isPlaying = false;
                yield return new WaitForSeconds(.5f);
                EditorApplication.Exit(pass ? 0 : 1);
            }
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
                Debug.Log("ENEMY_RANGED_CHECK_DONE");
            }
            catch (Exception e) { Debug.LogError("enemy ranged check write failed: " + e.Message); }
        }
    }
}
