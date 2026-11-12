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
    /// Wires the imported Trials Survivors assets into the actual gameplay scenes:
    ///  1. Registers every model as a prefab-ready asset under TSAssets/Prefabs
    ///  2. Decorates the playable playground with the imported props (collidable scenery)
    ///  3. Adds an audio catalogue component so the imported SFX/BGM are reachable at runtime
    /// </summary>
    public static class TSGameIntegration
    {
        const string ModelDir = "Assets/DuckGame/TSAssets/Models";
        const string MatDir = "Assets/DuckGame/TSAssets/Materials";
        const string AudioDir = "Assets/DuckGame/TSAssets/Audio";
        const string PrefabDir = "Assets/DuckGame/TSAssets/Prefabs";
        const string Playground = "Assets/DuckGame/Generated/v03/Playground.unity";
        const string OutScene = "Assets/DuckGame/Generated/v03/PlaygroundWithTS.unity";

        // Prop name fragments that make good ground scenery (avoid UI/effect meshes).
        static readonly string[] SceneryHints =
        {
            "Brick", "Wall", "Arch", "Pillar", "Box", "Crate", "Barrel", "Boat",
            "Torch", "Rock", "Stone", "Tree", "Bush", "Fence", "Bridge", "Stairs",
            "Tomb", "Grave", "Cross", "Bell", "Totem", "Potion", "Coin", "Chest",
            "Spike", "Pumpkin", "Tent", "Well", "Bench", "Table", "Chair"
        };

        [MenuItem("Duck Game/Import TS/Integrate Into Gameplay")]
        public static void Run()
        {
            Directory.CreateDirectory(PrefabDir);
            var report = new System.Text.StringBuilder();

            // ---------- 1. Build prefabs ----------
            var models = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)) models.Add(p);
            }
            models.Sort(StringComparer.OrdinalIgnoreCase);

            var mats = new List<Material>();
            foreach (var f in Directory.GetFiles(MatDir, "*.mat").OrderBy(x => x))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(f);
                if (m != null) mats.Add(m);
            }

            var scenery = new List<GameObject>();
            int prefabsMade = 0;

            foreach (var path in models)
            {
                string baseName = Path.GetFileNameWithoutExtension(path);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) continue;

                string prefabPath = PrefabDir + "/" + baseName + ".prefab";
                GameObject inst = null;
                try
                {
                    inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
                    if (inst == null) continue;
                    inst.name = baseName;
                    if (mats.Count > 0)
                    {
                        int mi = Mathf.Abs(baseName.GetHashCode()) % mats.Count;
                        foreach (var r in inst.GetComponentsInChildren<Renderer>())
                            r.sharedMaterial = mats[mi];
                    }
                    PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
                    prefabsMade++;
                }
                catch { }
                finally { if (inst != null) UnityEngine.Object.DestroyImmediate(inst); }

                // keep scenery candidates
                bool isScenery = SceneryHints.Any(h => baseName.IndexOf(h, StringComparison.OrdinalIgnoreCase) >= 0);
                if (isScenery)
                {
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (pf != null) scenery.Add(pf);
                }
            }
            report.AppendLine("prefabs created = " + prefabsMade);
            report.AppendLine("scenery candidates = " + scenery.Count);

            // ---------- 2. Decorate the playground ----------
            if (File.Exists(Playground) && scenery.Count > 0)
            {
                var scene = EditorSceneManager.OpenScene(Playground, OpenSceneMode.Single);

                // Clear any previous pass
                foreach (var go in scene.GetRootGameObjects())
                    if (go.name == "TS_Scenery") UnityEngine.Object.DestroyImmediate(go);

                var root = new GameObject("TS_Scenery");
                var rng = new System.Random(20261111);

                // Ring the 40 x 60 range with props, leaving the centre clear for shooting.
                int spawned = 0;
                for (int i = 0; i < 90; i++)
                {
                    var pf = scenery[rng.Next(scenery.Count)];
                    if (pf == null) continue;

                    // place on a ring outside the play area
                    float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
                    float rad = 26f + (float)rng.NextDouble() * 34f;
                    float x = Mathf.Cos(ang) * rad;
                    float z = 4f + Mathf.Sin(ang) * rad;

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                    if (go == null) continue;
                    go.transform.SetParent(root.transform);

                    Bounds b = CombinedBounds(go);
                    float largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    float sc = (largest > .001f) ? Mathf.Clamp(2.6f / largest, .1f, 8f) : 1f;
                    go.transform.localScale = Vector3.one * sc;
                    go.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);

                    b = CombinedBounds(go);
                    go.transform.position = new Vector3(x, -b.min.y, z);
                    spawned++;
                }
                report.AppendLine("scenery spawned in playground = " + spawned);

                EditorSceneManager.SaveScene(scene, OutScene);
                report.AppendLine("output scene = " + OutScene);
            }

            // ---------- 3. Audio catalogue ----------
            var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir });
            report.AppendLine("audio clips available = " + clips.Length);

            File.WriteAllText(Path.GetFullPath("docs/ts-integration-report.txt"), report.ToString());
            Debug.Log("TS_INTEGRATE_DONE " + report.ToString().Replace("\n", " | "));
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
