using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DuckGame.Expedition;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Wires the expedition prototype into the player's real hub scene (in place).
    ///
    /// What gets added to Maps/V12/Generated/ExpeditionHub.unity:
    ///   ExpeditionSystems  – ExpeditionRun, RewardSystem, WaveController, HUD, spawner
    ///   Expedition_Pads    – one spring pad per reachable exit of the current island
    ///   Expedition_Enemies – enemy prefabs used by the spawner
    ///
    /// Pads are rebuilt from the route graph, so the number of pads always equals the number
    /// of exits (1 at linear depths, 2 at the branch depth) — matching the design rule that
    /// no depth shows more than two choices.
    /// </summary>
    public static class ExpeditionSetup
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string PrefabDir = "Assets/DuckGame/TSAssets/Prefabs";

        [MenuItem("Duck Game/Expedition/Install Into Hub Scene")]
        public static void Run()
        {
            var report = new System.Text.StringBuilder();
            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            // ---------- clean previous install ----------
            foreach (var n in new[] { "ExpeditionSystems", "Expedition_Pads", "Expedition_Enemies", "ExpeditionLanding" })
            {
                var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == n);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }

            // ---------- find player ----------
            Transform player = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "PlayerRig" || go.name == "GooseWizard_Rig") { player = go.transform; break; }
            }
            if (player == null)
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    var m = go.GetComponentInChildren<DuckGame.V04.GooseMotorV04>(true);
                    if (m != null) { player = m.transform; break; }
                }
            }
            report.AppendLine("player = " + (player != null ? player.name : "NOT FOUND"));

            // ---------- measure camp ----------
            Bounds camp = new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f));
            bool have = false;
            foreach (var go in scene.GetRootGameObjects())
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!have) { camp = r.bounds; have = true; }
                    else camp.Encapsulate(r.bounds);
                }
            }
            report.AppendLine("camp = " + camp);

            // ---------- enemy prefabs ----------
            var hostilePrefabs = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (n.Contains("bat") || n.Contains("slime") || n.Contains("skeleton") ||
                    n.Contains("dragon") || n.Contains("demon") || n.Contains("frog") ||
                    n.Contains("lizard") || n.Contains("golem") || n.Contains("spider") ||
                    n.Contains("rat") || n.Contains("wolf"))
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (pf != null) hostilePrefabs.Add(pf);
                }
            }
            report.AppendLine("hostile prefabs = " + hostilePrefabs.Count);

            // ---------- systems object ----------
            var sys = new GameObject("ExpeditionSystems");
            var run = sys.AddComponent<ExpeditionRun>();
            run.runSeed = 20261111;
            run.combatRounds = 5;

            var rewards = sys.AddComponent<RewardSystem>();
            var waves = sys.AddComponent<WaveController>();
            var hud = sys.AddComponent<ExpeditionHUD>();
            var result = sys.AddComponent<ExpeditionResult>();
            var coiner = sys.AddComponent<CoinSpawner>();
            var merchant = sys.AddComponent<MerchantBridge>();

            var spawner = sys.AddComponent<IslandSpawner>();
            spawner.player = player;
            spawner.enemyPrefabs = hostilePrefabs.ToArray();
            spawner.spawnRadius = 14f;
            spawner.baseCount = 5;
            spawner.perDepth = 2;

            var combat = sys.AddComponent<CombatBridge>();
            combat.player = player;
            combat.baseDamage = 14f;

            var feedback = sys.AddComponent<CombatFeedback>();
            feedback.poolSize = 28;

            if (player != null)
            {
                var hp = player.GetComponent<PlayerHealth>();
                if (hp == null) hp = player.gameObject.AddComponent<PlayerHealth>();
                hp.maxHealth = 100f;
                hp.regenPerSecond = 1.2f;
                hp.invulnerableAfterHit = .8f;
                report.AppendLine("PlayerHealth attached to " + player.name);
            }

            var enemyParent = new GameObject("Expedition_Enemies");
            spawner.enemyParent = enemyParent.transform;
            report.AppendLine("systems installed: Run/Reward/Wave/HUD/Result/CoinSpawner/MerchantBridge/Spawner/CombatBridge/PlayerHealth");

            // ---------- build pads from the route graph ----------
            var padsRoot = new GameObject("Expedition_Pads");
            var padMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            padMat.color = new Color(.30f, .78f, .55f);

            // We build the graph here just to know how many exits to create. The runtime
            // rebuilds the same graph from runSeed, so pad count matches at play time.
            var preview = RouteGraph.Build(20261111, 5);
            var entries = preview.Entries();
            int maxExits = 1;
            foreach (var kv in preview.Islands)
                maxExits = Mathf.Max(maxExits, preview.ExitsOf(kv.Key).Count);
            report.AppendLine("route: islands=" + preview.Islands.Count +
                              " entries=" + entries.Count +
                              " maxExits=" + maxExits +
                              " bossDepth=" + preview.BossDepth);

            int padsMade = 0;
            var padComponents = new List<SpringPad>();
            var signComponents = new List<IslandSign>();
            for (int i = 0; i < maxExits; i++)
            {
                var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = "SpringPad_" + i;
                pad.transform.SetParent(padsRoot.transform);

                // Place the pads in the clear zone in FRONT of the player, away from the
                // vending machine and the arch, so they are visible and walkable.
                float ang = (i == 0) ? 150f : 210f;
                float rad = Mathf.Min(16f, camp.extents.magnitude * 0.20f);
                var pos = new Vector3(
                    camp.center.x + Mathf.Sin(ang * Mathf.Deg2Rad) * rad,
                    camp.min.y + 0.12f,
                    camp.center.z + Mathf.Cos(ang * Mathf.Deg2Rad) * rad);
                pad.transform.position = pos;
                pad.transform.localScale = new Vector3(3.2f, .12f, 3.2f);

                var col = pad.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;

                var rend = pad.GetComponent<Renderer>();
                if (rend != null) rend.sharedMaterial = padMat;

                var sp = pad.AddComponent<SpringPad>();
                sp.playerRig = player;
                sp.padRenderer = rend;
                sp.triggerRadius = 2.2f;

                // Procedural ring + light column so the pad reads as a destination and shows
                // when it is actually armed.
                var pv = pad.AddComponent<PadVisuals>();
                pv.pad = sp;

                // Preview board: reads the same Encounter the pad acts on, so the promise
                // shown here can never disagree with what the player actually flies into.
                var signGo = new GameObject("IslandSign_" + i);
                signGo.transform.SetParent(padsRoot.transform);
                signGo.transform.position = pos + Vector3.up * 1.2f;
                var sign = signGo.AddComponent<IslandSign>();
                sign.player = player;
                sign.visibleRadius = 26f;
                signComponents.Add(sign);

                // Landing target: a raised disc the player drops onto, well clear of the camp.
                var landing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                landing.name = "ExpeditionLanding_" + i;
                landing.transform.SetParent(padsRoot.transform);
                var lpos = pos + new Vector3(
                    Mathf.Sin(ang * Mathf.Deg2Rad) * rad * .55f, 0f,
                    Mathf.Cos(ang * Mathf.Deg2Rad) * rad * .55f);
                landing.transform.position = new Vector3(lpos.x, camp.min.y + 0.1f, lpos.z);
                landing.transform.localScale = new Vector3(6f, .2f, 6f);
                var lrend = landing.GetComponent<Renderer>();
                if (lrend != null) lrend.sharedMaterial = padMat;
                sp.landingPoint = landing.transform;

                padComponents.Add(sp);
                padsMade++;
            }
            report.AppendLine("spring pads created = " + padsMade);

            // Runtime binder: assigns pad[i] -> current island's exit[i] every time the run
            // advances, so pad count always equals real exit count (1 or 2).
            var binder = padsRoot.AddComponent<PadBinder>();
            binder.pads = padComponents.ToArray();
            binder.signs = signComponents.ToArray();
            report.AppendLine("PadBinder installed with " + padComponents.Count + " pads, " + signComponents.Count + " signs");

            // ---------- audio ----------
            var audioGo = new GameObject("Expedition_Audio");
            audioGo.transform.SetParent(sys.transform);
            audioGo.AddComponent<AudioSource>().playOnAwake = false;
            audioGo.AddComponent<TSAudioCatalogue>();

            // ---------- save in place ----------
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HubScene);

            report.AppendLine("");
            report.AppendLine("SAVED IN PLACE: " + HubScene);
            report.AppendLine("");
            report.AppendLine("=== 运行时的路线图（固定 seed 可复现）===");
            report.AppendLine(preview.Dump());

            File.WriteAllText(Path.GetFullPath("docs/expedition-install-report.txt"), report.ToString());
            Debug.Log("EXPEDITION_INSTALL_DONE");
        }
    }
}
