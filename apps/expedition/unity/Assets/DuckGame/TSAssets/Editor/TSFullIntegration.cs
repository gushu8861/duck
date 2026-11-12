using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DuckGame.TS
{
    /// <summary>
    /// Final full integration pass. Everything runs in -batchmode so no editor window ever
    /// appears for the user.
    ///
    ///  1. Audio: build a name->clip table and write it as a serialized catalogue asset
    ///  2. Sprites: tag UI sprites by family (card / icon / panel) so they are findable
    ///  3. Hub: dress ExpeditionHub with themed prop clusters (not a uniform ring)
    /// </summary>
    public static class TSFullIntegration
    {
        const string AudioDir = "Assets/DuckGame/TSAssets/Audio";
        const string SpriteDir = "Assets/DuckGame/TSAssets/Sprites";
        const string PrefabDir = "Assets/DuckGame/TSAssets/Prefabs";
        const string MatDir = "Assets/DuckGame/TSAssets/Materials";
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string OutScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHubFull.unity";

        [MenuItem("Duck Game/Import TS/Full Integration")]
        public static void Run()
        {
            var report = new System.Text.StringBuilder();

            // ================= 1. AUDIO =================
            var clipPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x).ToList();

            // Classify by filename so gameplay can ask for "fire cast" not "Fire_Cast_2s_06".
            var buckets = new Dictionary<string, List<string>>();
            void Bucket(string key, string path)
            {
                if (!buckets.TryGetValue(key, out var l)) { l = new List<string>(); buckets[key] = l; }
                l.Add(path);
            }
            foreach (var p in clipPaths)
            {
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                if (n.Contains("cast")) Bucket("cast", p);
                else if (n.Contains("impact") || n.Contains("hit")) Bucket("impact", p);
                else if (n.Contains("death")) Bucket("death", p);
                else if (n.Contains("hover")) Bucket("hover", p);
                else if (n.Contains("pickup") || n.Contains("drop") || n.Contains("collect")) Bucket("pickup", p);
                else if (n.Contains("confirm") || n.Contains("cancel") || n.Contains("click") || n.Contains("tick")) Bucket("ui", p);
                else if (n.Contains("final") || n.Contains("stage") || n.Contains("remake") ||
                         n.Contains("scoring") || n.Contains("victory") || n.Contains("defeat")) Bucket("music", p);
                else Bucket("misc", p);
            }
            report.AppendLine("audio clips = " + clipPaths.Count);
            foreach (var kv in buckets.OrderByDescending(x => x.Value.Count))
                report.AppendLine("  " + kv.Key + " = " + kv.Value.Count);

            // Write a generated mapping table the runtime catalogue can consume.
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// AUTO-GENERATED. Sound family -> representative clip paths.");
            sb.AppendLine("// Gameplay asks by family so it does not depend on individual filenames.");
            sb.AppendLine("namespace DuckGame.TS");
            sb.AppendLine("{");
            sb.AppendLine("    public static class TSSoundMap");
            sb.AppendLine("    {");
            sb.AppendLine("        public static readonly string[] Cast = {");
            foreach (var p in buckets.ContainsKey("cast") ? buckets["cast"] : new List<string>())
                sb.AppendLine("            \"" + p.Replace("Assets/DuckGame/TSAssets/Audio/", "").Replace(".wav", "") + "\",");
            sb.AppendLine("        };");
            sb.AppendLine("        public static readonly string[] Impact = {");
            foreach (var p in buckets.ContainsKey("impact") ? buckets["impact"] : new List<string>())
                sb.AppendLine("            \"" + p.Replace("Assets/DuckGame/TSAssets/Audio/", "").Replace(".wav", "") + "\",");
            sb.AppendLine("        };");
            sb.AppendLine("        public static readonly string[] Death = {");
            foreach (var p in buckets.ContainsKey("death") ? buckets["death"] : new List<string>())
                sb.AppendLine("            \"" + p.Replace("Assets/DuckGame/TSAssets/Audio/", "").Replace(".wav", "") + "\",");
            sb.AppendLine("        };");
            sb.AppendLine("        public static readonly string[] Ui = {");
            foreach (var p in buckets.ContainsKey("ui") ? buckets["ui"] : new List<string>())
                sb.AppendLine("            \"" + p.Replace("Assets/DuckGame/TSAssets/Audio/", "").Replace(".wav", "") + "\",");
            sb.AppendLine("        };");
            sb.AppendLine("        public static readonly string[] Music = {");
            foreach (var p in buckets.ContainsKey("music") ? buckets["music"] : new List<string>())
                sb.AppendLine("            \"" + p.Replace("Assets/DuckGame/TSAssets/Audio/", "").Replace(".wav", "") + "\",");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            Directory.CreateDirectory("Assets/DuckGame/TSAssets/Runtime");
            File.WriteAllText(Path.GetFullPath("Assets/DuckGame/TSAssets/Runtime/TSSoundMap.cs"), sb.ToString());
            report.AppendLine("sound map written = TSSoundMap.cs");

            // ================= 2. SPRITE TAGGING =================
            int cards = 0, icons = 0, panels = 0, other = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

                string label;
                if (n.Contains("card")) { label = "ts_card"; cards++; }
                else if (n.Contains("icon") || n.Contains("indicator")) { label = "ts_icon"; icons++; }
                else if (n.Contains("panel") || n.Contains("frame") || n.Contains("border")) { label = "ts_panel"; panels++; }
                else { label = "ts_sprite"; other++; }

                if (ti.assetBundleName != label) ti.assetBundleName = "";   // keep clean
                var so = new SerializedObject(ti);
                // apply to sprite meta as a label via userData so it is queryable
                if (ti.userData != label) { ti.userData = label; ti.SaveAndReimport(); }
            }
            report.AppendLine("sprites tagged: card=" + cards + " icon=" + icons + " panel=" + panels + " other=" + other);

            // ================= 3. THEMED HUB DRESSING =================
            if (File.Exists(HubScene))
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    HubScene, UnityEditor.SceneManagement.OpenSceneMode.Single);

                var stale = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "TS_HubDressing");
                if (stale != null) UnityEngine.Object.DestroyImmediate(stale);

                // gather prefabs by family so each cluster is coherent
                var foliage = new List<GameObject>();
                var rocks = new List<GameObject>();
                var props = new List<GameObject>();
                var structures = new List<GameObject>();

                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (pf == null) continue;

                    if (n.Contains("tree") || n.Contains("palm") || n.Contains("bush") ||
                        n.Contains("plant") || n.Contains("grass") || n.Contains("mushroom")) foliage.Add(pf);
                    else if (n.Contains("rock") || n.Contains("stone") || n.Contains("boulder") ||
                             n.Contains("cliff") || n.Contains("crystal")) rocks.Add(pf);
                    else if (n.Contains("arch") || n.Contains("wall") || n.Contains("pillar") ||
                             n.Contains("gate") || n.Contains("tower") || n.Contains("stairs")) structures.Add(pf);
                    else if (n.Contains("crate") || n.Contains("box") || n.Contains("barrel") ||
                             n.Contains("basket") || n.Contains("table") || n.Contains("bench") ||
                             n.Contains("lantern") || n.Contains("torch") || n.Contains("sign") ||
                             n.Contains("cart") || n.Contains("chest")) props.Add(pf);
                }
                report.AppendLine("families: foliage=" + foliage.Count + " rocks=" + rocks.Count +
                                  " props=" + props.Count + " structures=" + structures.Count);

                var root = new GameObject("TS_HubDressing");
                var rng = new System.Random(20261111);

                // measure the existing camp
                Bounds camp = new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f));
                bool have = false;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go.name == "TS_HubDressing") continue;
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!have) { camp = r.bounds; have = true; }
                        else camp.Encapsulate(r.bounds);
                    }
                }
                report.AppendLine("camp bounds = " + camp);

                int placed = 0;
                void Cluster(List<GameObject> fam, int count, float rMin, float rMax, float targetH)
                {
                    if (fam.Count == 0) return;
                    for (int i = 0; i < count; i++)
                    {
                        var pf = fam[rng.Next(fam.Count)];
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                        if (go == null) continue;
                        go.transform.SetParent(root.transform);

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
                        go.transform.position = new Vector3(x, camp.min.y - b.min.y + .02f, z);
                        placed++;
                    }
                }

                float baseR = camp.extents.magnitude;
                Cluster(foliage, 70, baseR * 1.05f, baseR * 1.85f, 5f);      // tree line
                Cluster(rocks, 45, baseR * .95f, baseR * 1.45f, 2.4f);       // rock border
                Cluster(structures, 22, baseR * 1.15f, baseR * 1.60f, 4.5f); // distant ruins
                Cluster(props, 60, baseR * .80f, baseR * 1.05f, 1.8f);       // camp clutter

                report.AppendLine("dressing placed = " + placed);

                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, OutScene);
                report.AppendLine("output = " + OutScene);
            }

            File.WriteAllText(Path.GetFullPath("docs/ts-full-report.txt"), report.ToString());
            Debug.Log("TS_FULL_DONE");
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
