using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DuckGame;
using DuckGame.Expedition;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Installs the pooled-orb bridge into the hub scene (in place, idempotent).
    ///
    /// What it does:
    ///   1. finds the real OrbShooter inside the player rig;
    ///   2. reuses (or creates) the scene's ProjectilePool;
    ///   3. attaches PooledOrbBridge, points it at both, hands it the weapon's orb material;
    ///   4. saves the scene and writes a short report.
    ///
    /// It deliberately does NOT touch the player prefab: the bridge registers itself in
    /// OrbRouterRegistry at runtime, so OrbShooter picks it up without any serialized link.
    /// </summary>
    public static class PooledOrbInstall
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string ReportPath = "docs/pooled-orb-report.txt";

        [MenuItem("Duck Game/Expedition/Install Pooled Orbs")]
        public static void Run()
        {
            var report = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            var shooter = Object.FindObjectOfType<OrbShooter>();
            report.AppendLine("shooter = " + (shooter != null ? shooter.name : "NOT FOUND"));
            if (shooter == null)
            {
                Write(report, "aborted: no OrbShooter in scene");
                return;
            }

            report.AppendLine("config  = " + (shooter.config != null ? shooter.config.name : "none"));
            report.AppendLine("orbMaterial = " + (shooter.orbMaterial != null ? shooter.orbMaterial.name : "none"));

            // reuse the pool the hub builder already created, otherwise add one next to the bridge
            var pool = Object.FindObjectOfType<ProjectilePool>();
            if (pool == null)
            {
                var poolGo = new GameObject("PooledProjectiles");
                pool = poolGo.AddComponent<ProjectilePool>();
                report.AppendLine("pool    = created PooledProjectiles");
            }
            else
            {
                report.AppendLine("pool    = reused " + pool.name);
            }

            pool.capacity = 256;
            pool.trailTime = .16f;
            pool.visualScale = 1f;
            if (shooter.orbMaterial != null) pool.ApplyMaterial(shooter.orbMaterial);
            EditorUtility.SetDirty(pool);

            // one bridge only, no matter how often this runs
            var bridge = Object.FindObjectOfType<PooledOrbBridge>();
            if (bridge == null)
            {
                var host = new GameObject("PooledOrbBridge");
                bridge = host.AddComponent<PooledOrbBridge>();
                report.AppendLine("bridge  = created PooledOrbBridge");
            }
            else
            {
                report.AppendLine("bridge  = reused " + bridge.name);
            }

            bridge.shooter = shooter;
            bridge.pool = pool;
            bridge.useWeaponMaterial = true;
            bridge.visualSize = .30f;
            bridge.trailTime = .16f;
            EditorUtility.SetDirty(bridge);

            report.AppendLine("bridge.pool = " + (bridge.pool != null ? bridge.pool.name : "null"));
            report.AppendLine("bridge.shooter = " + (bridge.shooter != null ? bridge.shooter.name : "null"));

            // Reward picks must reach the live weapon, otherwise the HUD advertises
            // multipliers that the gun never receives.
            var sys = Object.FindObjectOfType<ExpeditionRun>();
            if (sys != null)
            {
                if (sys.GetComponent<UpgradeApplier>() == null)
                {
                    var applier = sys.gameObject.AddComponent<UpgradeApplier>();
                    applier.shooter = shooter;
                    report.AppendLine("upgrade applier = attached to " + sys.name);
                }
                else
                {
                    report.AppendLine("upgrade applier = already present on " + sys.name);
                }
            }
            else
            {
                report.AppendLine("upgrade applier = SKIPPED (no ExpeditionRun)");
            }

            // Music director: picks hub / battle / reward / post-combat / victory themes from
            // the imported soundtrack based on run state.
            if (sys != null)
            {
                if (sys.GetComponent<MusicDirector>() == null)
                {
                    sys.gameObject.AddComponent<MusicDirector>();
                    report.AppendLine("music director = attached to " + sys.name);
                }
                else
                {
                    report.AppendLine("music director = already present on " + sys.name);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HubScene);
            report.AppendLine("");
            report.AppendLine("SAVED IN PLACE: " + HubScene);

            Write(report, "");
            Debug.Log("POOLED_ORB_INSTALL_DONE");
        }

        static void Write(StringBuilder report, string extra)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ReportPath)));
            if (!string.IsNullOrEmpty(extra)) report.AppendLine(extra);
            File.WriteAllText(Path.GetFullPath(ReportPath), report.ToString());
        }
    }
}
