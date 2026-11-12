using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Turns the flat prototype look into a graded, cinematic one.
    ///
    /// The reference game's visual identity comes from four things this adds:
    ///   1. ACES tone mapping so highlights roll off instead of clipping to white
    ///   2. Bloom on emissive elements (orbs, pads, boss crown) for the glow that reads as magic
    ///   3. Vignette to focus the frame and hide the arena edge
    ///   4. A cool shadow / warm highlight grade, which every stylised action game uses to
    ///      separate forms without extra geometry
    ///
    /// It also fixes the lighting: the hub scene had a single hard directional light and flat
    /// ambient, which is what made everything look like untextured clay.
    /// </summary>
    public static class VisualGradeSetup
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";

        [MenuItem("Duck Game/Expedition/Apply Visual Grade")]
        public static void Run()
        {
            var report = new System.Text.StringBuilder();
            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            // ---------- volume profile ----------
            var oldVol = UnityEngine.Object.FindObjectOfType<Volume>();
            if (oldVol != null) UnityEngine.Object.DestroyImmediate(oldVol.gameObject);

            var volGo = new GameObject("PostProcessVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;

            string profileDir = "Assets/DuckGame/Expedition/Profiles";
            System.IO.Directory.CreateDirectory(profileDir);
            string profilePath = profileDir + "/ExpeditionGrade.asset";

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            // clear stale components from earlier runs
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                var c = profile.components[i];
                profile.components.RemoveAt(i);
                if (c != null) UnityEngine.Object.DestroyImmediate(c, true);
            }

            // --- Tonemapping: ACES keeps highlights from flattening ---
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            // --- Bloom: the magic glow ---
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, .93f, .82f));

            // --- Color adjustments: cool shadows, warm highlights ---
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.18f);
            ca.contrast.Override(12f);
            ca.saturation.Override(8f);
            ca.colorFilter.Override(new Color(1f, .985f, .96f));

            // --- Split toning: separates silhouette from background ---
            var st = profile.Add<SplitToning>(true);
            st.shadows.Override(new Color(.42f, .48f, .60f));
            st.highlights.Override(new Color(1f, .93f, .80f));
            st.balance.Override(-18f);

            // --- Vignette: frames the action ---
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.42f);
            vig.color.Override(new Color(.03f, .035f, .06f));

            // --- Film grain: subtle texture that stops flat colour banding ---
            var fg = profile.Add<FilmGrain>(true);
            fg.type.Override(FilmGrainLookup.Thin1);
            fg.intensity.Override(0.10f);
            fg.response.Override(0.72f);

            // --- SSAO: contact shadows so props sit on the ground ---
            // ScreenSpaceAmbientOcclusion is internal to URP, so it is added reflectively.
            // A failure here is non-fatal: the rest of the grade still applies.
            try
            {
                var ssaoType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
                if (ssaoType == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        ssaoType = asm.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                        if (ssaoType != null) break;
                    }
                }
                if (ssaoType != null)
                {
                    var comp = profile.Add(ssaoType, true);
                    SetField(comp, "m_Intensity", 0.85f);
                    SetField(comp, "m_Radius", 0.55f);
                    report.AppendLine("SSAO added");
                }
                else report.AppendLine("SSAO type not found (skipped)");
            }
            catch (Exception e)
            {
                report.AppendLine("SSAO skipped: " + e.Message);
            }

            vol.sharedProfile = profile;
            report.AppendLine("volume profile: ACES + Bloom + ColorAdjust + SplitToning + Vignette + Grain + SSAO");

            // ---------- lighting ----------
            var lights = UnityEngine.Object.FindObjectsOfType<Light>();
            bool hasKey = false;
            foreach (var l in lights)
            {
                if (l.type != LightType.Directional) continue;
                hasKey = true;
                l.color = new Color(1f, .95f, .86f);   // warm key
                l.intensity = 2.05f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = .82f;
                l.shadowBias = .04f;
                l.shadowNormalBias = .35f;
                l.shadowNearPlane = .2f;
                l.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            }
            if (!hasKey)
            {
                var go = new GameObject("KeyLight");
                var l = go.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = new Color(1f, .95f, .86f);
                l.intensity = 2.05f;
                l.shadows = LightShadows.Soft;
                l.shadowStrength = .82f;
                go.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            }
            report.AppendLine("key light warmed to 2.05 with soft shadows");

            // ---------- ambient: three-tone so shadow sides keep colour ----------
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.38f, .44f, .56f);
            RenderSettings.ambientEquatorColor = new Color(.26f, .29f, .35f);
            RenderSettings.ambientGroundColor = new Color(.13f, .14f, .18f);
            RenderSettings.ambientIntensity = 1.05f;

            // ---------- fog: depth separation at distance ----------
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.42f, .52f, .62f);
            RenderSettings.fogDensity = 0.0075f;
            report.AppendLine("trilight ambient + exponential fog");

            // ---------- skybox tint ----------
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Tint"))
                sky.SetColor("_Tint", new Color(.55f, .62f, .74f));
            RenderSettings.sun = lights.Length > 0 ? lights[0] : null;

            // ---------- camera ----------
            var cam = Camera.main;
            if (cam != null)
            {
                cam.allowHDR = true;
                cam.allowMSAA = true;
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.dithering = true;
                report.AppendLine("camera: HDR + SMAA high + post-processing enabled");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HubScene);

            System.IO.File.WriteAllText(
                System.IO.Path.GetFullPath("docs/visual-grade-report.txt"), report.ToString());
            Debug.Log("VISUAL_GRADE_DONE");
        }

        static void SetField(object target, string name, float value)
        {
            if (target == null) return;
            var t = target.GetType();
            var f = t.GetField(name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (f == null) return;
            var v = f.GetValue(target);
            if (v == null) return;

            // URP volume parameters expose Override(T) where T differs per parameter
            // (float, int, enum). Try each numeric overload instead of assuming float.
            var vt = v.GetType();
            var mf = vt.GetMethod("Override", new[] { typeof(float) });
            if (mf != null) { mf.Invoke(v, new object[] { value }); return; }

            var mi = vt.GetMethod("Override", new[] { typeof(int) });
            if (mi != null) { mi.Invoke(v, new object[] { Mathf.RoundToInt(value) }); return; }
        }
    }
}
