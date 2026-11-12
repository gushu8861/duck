using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace DuckGame.TS
{
    /// <summary>
    /// Modifies the player's ACTUAL playable scene (Maps/V12/ExpeditionHub) in place —
    /// not a copy, not a side project. Adds:
    ///   - themed scenery using the imported Trials Survivors props
    ///   - live enemies driven by EnemyBrain
    ///   - a ProjectilePool wired to the goose's shooting
    ///   - an Economy instance feeding the vending machine
    ///   - an audio catalogue so imported sounds actually play
    /// Everything is inserted into the open scene and saved back to the same path.
    /// </summary>
    public static class TSHubModify
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string PrefabDir = "Assets/DuckGame/TSAssets/Prefabs";
        const string MatDir = "Assets/DuckGame/TSAssets/Materials";

        [MenuItem("Duck Game/Import TS/Patch My Hub Scene")]
        public static void Run()
        {
            var report = new System.Text.StringBuilder();
            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            // -------- clean previous patch so re-runs stay idempotent --------
            foreach (var name in new[] { "TS_Scenery", "TS_Enemies", "TS_Systems" })
            {
                var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }

            // -------- locate the existing player rig --------
            Transform player = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "PlayerRig" || go.name == "GooseWizard_Rig")
                {
                    player = go.transform;
                    break;
                }
            }
            if (player == null)
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    var m = go.GetComponentInChildren<DuckGame.V04.GooseMotorV04>(true);
                    if (m != null) { player = m.transform; break; }
                }
            }
            report.AppendLine("player rig = " + (player != null ? player.name : "NOT FOUND"));

            // -------- measure the existing camp --------
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
            report.AppendLine("camp bounds = " + camp);

            // -------- gather prefabs by family --------
            var foliage = new List<GameObject>();
            var rocks = new List<GameObject>();
            var props = new List<GameObject>();
            var structures = new List<GameObject>();
            var hostiles = new List<GameObject>();

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (pf == null) continue;

                if (n.Contains("tree") || n.Contains("palm") || n.Contains("bush") ||
                    n.Contains("plant") || n.Contains("mushroom")) foliage.Add(pf);
                else if (n.Contains("rock") || n.Contains("stone") || n.Contains("boulder")) rocks.Add(pf);
                else if (n.Contains("arch") || n.Contains("wall") || n.Contains("pillar") ||
                         n.Contains("gate") || n.Contains("tower")) structures.Add(pf);
                else if (n.Contains("bat") || n.Contains("slime") || n.Contains("skeleton") ||
                         n.Contains("dragon") || n.Contains("demon") || n.Contains("frog") ||
                         n.Contains("lizard") || n.Contains("golem") || n.Contains("spider") ||
                         n.Contains("rat") || n.Contains("wolf")) hostiles.Add(pf);
                else if (n.Contains("crate") || n.Contains("box") || n.Contains("barrel") ||
                         n.Contains("lantern") || n.Contains("torch") || n.Contains("sign") ||
                         n.Contains("chest") || n.Contains("pot")) props.Add(pf);
            }
            report.AppendLine(string.Format(
                "families: foliage={0} rocks={1} props={2} structures={3} hostiles={4}",
                foliage.Count, rocks.Count, props.Count, structures.Count, hostiles.Count));

            var rng = new System.Random(20261111);

            // ================= 1. SCENERY =================
            var sceneryRoot = new GameObject("TS_Scenery");
            int sceneryPlaced = 0;

            void Scatter(List<GameObject> fam, int count, float rMin, float rMax, float targetH, bool alignGround)
            {
                if (fam.Count == 0) return;
                for (int i = 0; i < count; i++)
                {
                    var pf = fam[rng.Next(fam.Count)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                    if (go == null) continue;
                    go.transform.SetParent(sceneryRoot.transform);

                    float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float rad = rMin + (float)rng.NextDouble() * (rMax - rMin);
                    float x = camp.center.x + Mathf.Cos(ang) * rad;
                    float z = camp.center.z + Mathf.Sin(ang) * rad;

                    Bounds b = CombinedBounds(go);
                    float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    float sc = (largest > .001f) ? Mathf.Clamp(targetH / largest, .08f, 12f) : 1f;
                    go.transform.localScale = Vector3.one * sc;
                    go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

                    b = CombinedBounds(go);
                    go.transform.position = new Vector3(x, alignGround ? camp.min.y - b.min.y + .02f : b.center.y, z);
                    sceneryPlaced++;
                }
            }

            float baseR = camp.extents.magnitude;
            Scatter(foliage, 65, baseR * 1.02f, baseR * 1.75f, 5.5f, true);
            Scatter(rocks, 40, baseR * .92f, baseR * 1.35f, 2.2f, true);
            Scatter(structures, 18, baseR * 1.20f, baseR * 1.60f, 5f, true);
            Scatter(props, 50, baseR * .78f, baseR * 1.02f, 1.6f, true);
            report.AppendLine("scenery placed = " + sceneryPlaced);

            // ================= 2. ENEMIES =================
            var enemyRoot = new GameObject("TS_Enemies");
            int enemiesMade = 0;
            if (player != null && hostiles.Count > 0)
            {
                int total = 9;
                for (int i = 0; i < total; i++)
                {
                    var pf = hostiles[rng.Next(hostiles.Count)];
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                    if (go == null) continue;
                    go.transform.SetParent(enemyRoot.transform);

                    // spawn on a mid ring so they are visible but not on top of the player
                    float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float rad = baseR * (0.45f + (float)rng.NextDouble() * 0.35f);
                    var pos = new Vector3(
                        camp.center.x + Mathf.Cos(ang) * rad,
                        camp.min.y + 0.05f,
                        camp.center.z + Mathf.Sin(ang) * rad);

                    Bounds b = CombinedBounds(go);
                    float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    float sc = (largest > .001f) ? Mathf.Clamp(1.6f / largest, .1f, 8f) : 1f;
                    go.transform.localScale = Vector3.one * sc;

                    b = CombinedBounds(go);
                    go.transform.position = new Vector3(pos.x, camp.min.y - b.min.y + .05f, pos.z);

                    // behaviour: rotate roles so all three archetypes appear
                    var brain = go.AddComponent<EnemyBrain>();
                    brain.target = player;
                    var tuning = brain.tuning;
                    tuning.role = (EnemyRole)(i % 3);
                    tuning.moveSpeed = 2.6f + (float)rng.NextDouble() * 1.6f;
                    tuning.preferredRange = 6f + (float)rng.NextDouble() * 6f;
                    tuning.aggression = .5f + (float)rng.NextDouble() * .5f;
                    tuning.maxHealth = 24f + (float)rng.NextDouble() * 22f;
                    tuning.contactDamage = 6f + (float)rng.NextDouble() * 6f;
                    brain.tuning = tuning;

                    // tint the body so roles are readable at a glance
                    var rend = go.GetComponentInChildren<Renderer>();
                    if (rend != null)
                    {
                        brain.bodyRenderer = rend;
                        brain.idleTint = tuning.role == EnemyRole.Chaser ? new Color(.78f, .28f, .22f)
                                       : tuning.role == EnemyRole.Keeper ? new Color(.35f, .45f, .72f)
                                       : new Color(.45f, .62f, .35f);
                    }
                    enemiesMade++;
                }
            }
            report.AppendLine("enemies spawned = " + enemiesMade + " (Chaser/Keeper/Fleer rotating)");

            // ================= 3. SYSTEMS =================
            var sysRoot = new GameObject("TS_Systems");

            // economy
            var eco = sysRoot.AddComponent<Economy>();
            eco.runBudget = 100;

            // projectile pool
            var pool = sysRoot.AddComponent<ProjectilePool>();
            pool.capacity = 256;
            var specFire = new ProjectileSpec
            {
                speed = 34f,
                damage = 12f,
                lifetime = 3f,
                radius = .18f,
                gravity = 0f,
                scale = Vector3.one * .28f,
                tint = new Color(.25f, .78f, 1f),
            };
            pool.RegisterSpec(specFire);
            report.AppendLine("systems: Economy(runBudget=100) + ProjectilePool(cap 256) + 1 spec");

            // audio catalogue
            var audioGo = new GameObject("TS_AudioCatalogue");
            audioGo.transform.SetParent(sysRoot.transform);
            audioGo.AddComponent<TSAudioCatalogue>();
            var src = audioGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            report.AppendLine("audio catalogue attached");

            // ================= SAVE BACK TO THE SAME SCENE =================
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HubScene);

            report.AppendLine("");
            report.AppendLine("SAVED IN PLACE: " + HubScene);
            File.WriteAllText(Path.GetFullPath("docs/ts-hub-patch-report.txt"), report.ToString());
            Debug.Log("TS_HUB_PATCH_DONE");
        }

        static Bounds CombinedBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
