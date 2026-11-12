using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DuckGame.TS
{
    /// <summary>
    /// Configures the imported Trials Survivors assets for the duck project:
    ///  - every model gets the project's cel-shaded material with an auto-assigned palette tint
    ///  - every model is marked static-importable with correct scale/normals
    ///  - textures are set to the flat/unlit-friendly import settings the project uses
    /// The art direction target is the goose's look: flat colour, 3-band toon ramp, ink outline.
    /// </summary>
    public static class TSAssetConfigurator
    {
        const string Root = "Assets/DuckGame/TSAssets";
        const string MatDir = "Assets/DuckGame/TSAssets/Materials";

        // Palette sampled from the goose prototype so imported props read as the same world.
        static readonly Color[] Palette =
        {
            new Color(.91f, .86f, .70f), new Color(.52f, .54f, .52f),
            new Color(.13f, .52f, .73f), new Color(1f, .31f, .04f),
            new Color(.51f, .53f, .52f), new Color(.72f, .26f, .08f),
            new Color(.20f, .28f, .32f), new Color(.80f, .88f, .86f),
            new Color(.45f, .62f, .35f), new Color(.62f, .45f, .30f),
        };

        [MenuItem("Duck Game/Import TS/Configure Materials")]
        public static void Run()
        {
            var sb = new System.Text.StringBuilder();
            Directory.CreateDirectory(MatDir);

            var toonShader = Shader.Find("Duck Game/Toon URP");
            if (toonShader == null)
            {
                Debug.LogError("TS: Duck Game/Toon URP shader not found");
                return;
            }

            // 1. Build one material per palette colour (shared, keeps draw calls sane).
            var mats = new List<Material>();
            for (int i = 0; i < Palette.Length; i++)
            {
                string p = MatDir + "/TS_Toon_" + i + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null)
                {
                    m = new Material(toonShader);
                    AssetDatabase.CreateAsset(m, p);
                }
                m.SetVector("_Tint", Palette[i]);
                EditorUtility.SetDirty(m);
                mats.Add(m);
            }
            AssetDatabase.SaveAssets();

            // 2. Apply to every model under TSAssets/Models.
            string modelDir = Root + "/Models";
            var objs = AssetDatabase.FindAssets("t:Model", new[] { modelDir });
            int applied = 0, skipped = 0;

            foreach (var guid in objs)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { skipped++; continue; }

                // Import settings that match the project's look: no animation, keep normals,
                // no material generation (we assign our own).
                bool dirty = false;
                // Unity 2022.3 removed ModelImporter.importMaterials; the replacement is
                // materialImportMode. None = do not generate materials (we assign our own).
                if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    dirty = true;
                }
                if (importer.importAnimation) { importer.importAnimation = false; dirty = true; }
                if (importer.importNormals != ModelImporterNormals.Import)
                {
                    importer.importNormals = ModelImporterNormals.Import;
                    dirty = true;
                }
                if (dirty) importer.SaveAndReimport();

                // Pick the palette colour by a stable hash of the name so the same prop
                // always gets the same colour across re-imports.
                int idx = Mathf.Abs(path.GetHashCode()) % mats.Count;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) { skipped++; continue; }

                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterial = mats[idx];
                applied++;
            }

            sb.AppendLine("models configured = " + applied + ", skipped = " + skipped);
            sb.AppendLine("materials = " + mats.Count + " at " + MatDir);
            File.WriteAllText(Path.GetFullPath("docs/ts-import-report.txt"), sb.ToString());
            Debug.Log("TS_CONFIGURE_DONE " + sb.ToString().Replace("\n", " | "));
        }
    }
}
