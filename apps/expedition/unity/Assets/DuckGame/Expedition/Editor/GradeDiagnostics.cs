using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Diagnoses why the post-processing grade is not visible: checks the URP asset flags,
    /// the camera's additional data, the Volume, and the sign orientation.
    /// </summary>
    public static class GradeDiagnostics
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";

        [MenuItem("Duck Game/Expedition/Diagnose Grade")]
        public static void Run()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            // ---- URP asset ----
            var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            sb.AppendLine("=== URP ASSET ===");
            if (rp == null) sb.AppendLine("  NOT a URP pipeline!");
            else
            {
                sb.AppendLine("  name = " + rp.name);
                sb.AppendLine("  supportsHDR = " + rp.supportsHDR);
                sb.AppendLine("  msaaSampleCount = " + rp.msaaSampleCount);
                sb.AppendLine("  renderScale = " + rp.renderScale);

                // The per-renderer post-processing flag is what silently disables everything.
                var type = rp.GetType();
                var prop = type.GetProperty("scriptableRendererData",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                var datas = prop?.GetValue(rp) as ScriptableRendererData[];
                if (datas != null)
                {
                    sb.AppendLine("  renderer data count = " + datas.Length);
                    foreach (var d in datas)
                        sb.AppendLine("    " + d.name);
                }
                else sb.AppendLine("  could not read renderer data");
            }

            // ---- camera ----
            sb.AppendLine("");
            sb.AppendLine("=== CAMERA ===");
            var cams = UnityEngine.Object.FindObjectsOfType<Camera>();
            foreach (var c in cams)
            {
                var d = c.GetComponent<UniversalAdditionalCameraData>();
                sb.AppendLine("  " + c.name + " enabled=" + c.enabled +
                              " hdr=" + c.allowHDR +
                              " data=" + (d != null) +
                              (d != null ? " postFX=" + d.renderPostProcessing : ""));
            }

            // ---- volume ----
            sb.AppendLine("");
            sb.AppendLine("=== VOLUME ===");
            var vols = UnityEngine.Object.FindObjectsOfType<Volume>();
            sb.AppendLine("  count = " + vols.Length);
            foreach (var v in vols)
            {
                sb.AppendLine("  " + v.name + " global=" + v.isGlobal +
                              " profile=" + (v.sharedProfile != null ? v.sharedProfile.name : "NULL") +
                              " layer=" + LayerMask.LayerToName(v.gameObject.layer));
                if (v.sharedProfile != null)
                    foreach (var comp in v.sharedProfile.components)
                        if (comp != null)
                            sb.AppendLine("    - " + comp.GetType().Name + " active=" + comp.active);
            }

            // ---- volume mask on camera ----
            sb.AppendLine("");
            sb.AppendLine("=== VOLUME MASK ===");
            foreach (var c in cams)
            {
                var d = c.GetComponent<UniversalAdditionalCameraData>();
                if (d == null) continue;
                int mask = d.volumeLayerMask;
                sb.AppendLine("  " + c.name + " volumeLayerMask = " + mask +
                              " (includes Default=" + ((mask & 1) != 0) + ")");
                sb.AppendLine("  " + c.name + " volumeTrigger = " +
                              (d.volumeTrigger != null ? d.volumeTrigger.name : "null"));
            }

            // ---- environment ----
            sb.AppendLine("");
            sb.AppendLine("=== ENVIRONMENT ===");
            sb.AppendLine("  ambientMode = " + RenderSettings.ambientMode);
            sb.AppendLine("  fog = " + RenderSettings.fog + " density = " + RenderSettings.fogDensity);
            sb.AppendLine("  skybox = " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL"));

            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            sb.AppendLine("  lights = " + lights.Length);
            foreach (var l in lights)
                sb.AppendLine("    " + l.name + " " + l.type + " intensity=" + l.intensity +
                              " shadows=" + l.shadows);

            // ---- signs ----
            sb.AppendLine("");
            sb.AppendLine("=== SIGNS ===");
            var signs = UnityEngine.Object.FindObjectsOfType<IslandSign>();
            sb.AppendLine("  count = " + signs.Length);
            foreach (var s in signs)
            {
                var canvases = s.GetComponentsInChildren<Canvas>(true);
                foreach (var cv in canvases)
                    sb.AppendLine("  " + s.name + " -> canvas " + cv.name +
                                  " mode=" + cv.renderMode +
                                  " scale=" + cv.transform.localScale +
                                  " rot=" + cv.transform.localRotation.eulerAngles);
            }

            File.WriteAllText(Path.GetFullPath("docs/grade-diagnostics.txt"), sb.ToString());
            Debug.Log("GRADE_DIAG_DONE");
        }
    }
}
