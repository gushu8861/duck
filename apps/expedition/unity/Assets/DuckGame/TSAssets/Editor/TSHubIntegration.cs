using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DuckGame.TS
{
    /// <summary>
    /// Decorates the REAL main scene (Maps/V12/ExpeditionHub) with the imported Trials
    /// Survivors assets, replacing the earlier mistake of dressing the obsolete v03 range.
    ///
    /// The hub already contains first-party gameplay props (magic vending machine, stone
    /// departure arch, targets, cover). New props are placed in a ring outside the camp so
    /// they read as surrounding scenery and never block the existing interactions.
    /// </summary>
    public static class TSHubIntegration
    {
        const string PrefabDir = "Assets/DuckGame/TSAssets/Prefabs";
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string OutScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHubWithTS.unity";

        // Prop families that suit a camp / island setting.
        static readonly string[] SceneryHints =
        {
            "Tree", "Palm", "Bush", "Plant", "Grass", "Rock", "Stone", "Boulder",
            "Crate", "Box", "Barrel", "Basket", "Pot", "Urn", "Vase",
            "Fence", "Gate", "Arch", "Pillar", "Wall", "Stairs", "Bridge",
            "Tent", "Banner", "Flag", "Lantern", "Torch", "Lamp",
            "Bench", "Table", "Chair", "Well", "Tomb", "Grave", "Cross",
            "Pumpkin", "Mushroom", "Crystal", "Sign", "Cart", "Boat"
        };

        [MenuItem("Duck Game/Import TS/Integrate Into V12 Hub")]
        public static void Run()
        {
            var report = new System.Text.StringBuilder();

            if (!File.Exists(HubScene))
            {
                Debug.LogError("TS: hub scene missing: " + HubScene);
                return;
            }

            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            // Clear previous pass so re-runs stay idempotent.
            var stale = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "TS_HubScenery");
            if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

            // Gather prefabs that suit the setting.
            var candidates = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(p);
                if (SceneryHints.Any(h => name.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (pf != null) candidates.Add(pf);
                }
            }
            report.AppendLine("scenery prefabs available = " + candidates.Count);

            // Measure the existing camp so the ring sits outside it.
            Bounds camp = new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f));
            bool have = false;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "TS_HubScenery") continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!have) { camp = r.bounds; have = true; }
                    else camp.Encapsulate(r.bounds);
                }
            }
            report.AppendLine("existing camp bounds = " + camp);

            var root = new GameObject("TS_HubScenery");
            var rng = new System.Random(20261111);

            int spawned = 0;
            if (candidates.Count > 0)
            {
                // Two rings: a near ring of small props and a far ring of tall scenery.
                for (int ring = 0; ring < 2; ring++)
                {
                    int count = ring == 0 ? 55 : 45;
                    float rMin = (ring == 0) ? camp.extents.magnitude * 0.85f : camp.extents.magnitude * 1.35f;
                    float rMax = (ring == 0) ? camp.extents.magnitude * 1.10f : camp.extents.magnitude * 1.90f;

                    for (int i = 0; i < count; i++)
                    {
                        var pf = candidates[rng.Next(candidates.Count)];
                        if (pf == null) continue;

                        float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                        float rad = rMin + (float)rng.NextDouble() * (rMax - rMin);
                        float x = camp.center.x + Mathf.Cos(ang) * rad;
                        float z = camp.center.z + Mathf.Sin(ang) * rad;

                        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                        if (go == null) continue;
                        go.transform.SetParent(root.transform);

                        Bounds b = CombinedBounds(go);
                        float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                        float target = ring == 0 ? 2.2f : 4.2f;
                        float sc = (largest > .001f) ? Mathf.Clamp(target / largest, .1f, 10f) : 1f;
                        go.transform.localScale = Vector3.one * sc;
                        go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

                        b = CombinedBounds(go);
                        go.transform.position = new Vector3(x, camp.min.y - b.min.y + 0.02f, z);
                        spawned++;
                    }
                }
            }
            report.AppendLine("props spawned = " + spawned);

            EditorSceneManager.SaveScene(scene, OutScene);
            report.AppendLine("output = " + OutScene);

            File.WriteAllText(Path.GetFullPath("docs/ts-hub-report.txt"), report.ToString());
            Debug.Log("TS_HUB_DONE " + report.ToString().Replace("\n", " | "));
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
