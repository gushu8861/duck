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
    /// Builds a browsable showcase for every imported Trials Survivors asset, laid out as a
    /// Minecraft-debug-style grid like the project's existing showroom, so the user can walk
    /// through and judge which pieces fit the game.
    /// </summary>
    public static class TSShowcaseBuilder
    {
        const string ModelDir = "Assets/DuckGame/TSAssets/Models";
        const string MatDir = "Assets/DuckGame/TSAssets/Materials";
        const string OutScene = "Assets/DuckGame/Generated/TSShowcase/TSShowcase.unity";

        const float Cell = 6f;
        const float ModelHeight = 2.4f;
        const int PerRow = 30;

        [MenuItem("Duck Game/Import TS/Build Showcase Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutScene));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- environment: same palette as the goose prototype ---
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2.2f;
            sun.color = new Color(1f, .97f, .92f);
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, -38f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.30f, .36f, .46f);
            RenderSettings.ambientEquatorColor = new Color(.20f, .24f, .30f);
            RenderSettings.ambientGroundColor = new Color(.08f, .09f, .12f);
            RenderSettings.fog = false;

            // --- materials ---
            var mats = new List<Material>();
            foreach (var f in Directory.GetFiles(MatDir, "*.mat").OrderBy(x => x))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(f);
                if (m != null) mats.Add(m);
            }

            // --- collect models, dedupe by name ---
            var models = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                // Only .obj from the import; ignore any embedded FBX
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                    models.Add(p);
            }
            models.Sort(StringComparer.OrdinalIgnoreCase);

            var report = new System.Text.StringBuilder();
            report.AppendLine("models found = " + models.Count);

            // --- ground ---
            int rows = Mathf.CeilToInt((float)models.Count / PerRow);
            float width = PerRow * Cell;
            float depth = rows * Cell;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ShowcaseGround";
            ground.transform.position = new Vector3(width * .5f, -.5f, depth * .5f);
            ground.transform.localScale = new Vector3(width + Cell * 2f, 1f, depth + Cell * 2f);
            var gmat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gmat.color = new Color(.10f, .12f, .16f);
            ground.GetComponent<Renderer>().sharedMaterial = gmat;

            // --- place ---
            int placed = 0, failed = 0;
            for (int i = 0; i < models.Count; i++)
            {
                int col = i % PerRow;
                int row = i / PerRow;
                var center = new Vector3(col * Cell + Cell * .5f, 0f, row * Cell + Cell * .5f);

                GameObject inst = null;
                try
                {
                    inst = AssetDatabase.LoadAssetAtPath<GameObject>(models[i]);
                    if (inst == null) { failed++; continue; }
                    inst = (GameObject)PrefabUtility.InstantiatePrefab(inst);
                    if (inst == null) { failed++; continue; }
                }
                catch { failed++; continue; }

                inst.name = Path.GetFileNameWithoutExtension(models[i]);

                // normalise scale — but NOT by the largest axis alone. Flat pieces
                // (planes, decals, ground cards) have near-zero height on one axis, so
                // 2.4/largest would still leave them as huge flat slabs on the floor.
                Bounds b;
                try { b = CombinedBounds(inst); }
                catch { UnityEngine.Object.DestroyImmediate(inst); failed++; continue; }

                float sx = b.size.x, sy = b.size.y, sz = b.size.z;
                float largest = Mathf.Max(sx, Mathf.Max(sy, sz));
                float scale;
                if (largest < .001f)
                {
                    scale = 1f;
                }
                else if (sy < largest * .06f)
                {
                    // Nearly flat: stand it up so it reads as an object, and size by its
                    // longest horizontal axis so it does not dominate the cell.
                    inst.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                    scale = Mathf.Clamp(ModelHeight / largest, .05f, 20f);
                }
                else
                {
                    scale = Mathf.Clamp(ModelHeight / largest, .05f, 20f);
                }
                inst.transform.localScale = Vector3.one * scale;
                b = CombinedBounds(inst);
                inst.transform.position = center + Vector3.up * (-b.min.y + .02f);

                // material
                int mi = Mathf.Abs(inst.name.GetHashCode()) % Mathf.Max(1, mats.Count);
                if (mats.Count > 0)
                    foreach (var r in inst.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = mats[mi];

                // floor tile
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Tile";
                tile.transform.position = center + Vector3.up * -.04f;
                tile.transform.localScale = new Vector3(Cell * .92f, .06f, Cell * .92f);
                tile.GetComponent<Renderer>().sharedMaterial = gmat;
                GameObject.DestroyImmediate(tile.GetComponent<Collider>());

                placed++;
            }

            // --- camera ---
            var camGo = new GameObject("ShowcaseCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.farClipPlane = 900f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(width * .5f, depth * .6f, -Cell * 3f);
            camGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            EditorSceneManager.SaveScene(scene, OutScene);

            report.AppendLine("placed = " + placed + ", failed = " + failed);
            report.AppendLine("scene = " + OutScene);
            report.AppendLine("grid = " + PerRow + " x " + rows + ", cell = " + Cell);
            File.WriteAllText(Path.GetFullPath("docs/ts-showcase-report.txt"), report.ToString());
            Debug.Log("TS_SHOWCASE_DONE " + report.ToString().Replace("\n", " | "));
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
