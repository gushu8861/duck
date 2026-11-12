using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckGame.TS
{
    /// <summary>Renders overview and close-up shots of the TS showcase so the import can be visually verified.</summary>
    public static class TSShowcaseShots
    {
        const string Scene = "Assets/DuckGame/Generated/TSShowcase/TSShowcase.unity";
        const string OutDir = "docs/ts-showcase";

        [MenuItem("Duck Game/Import TS/Capture Showcase Shots")]
        public static void Capture()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutDir));
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var cam = UnityEngine.Object.FindObjectOfType<Camera>();
            if (cam == null) { Debug.LogError("no camera"); return; }

            // Grid is 30 x 18 cells at 6 m => 180 x 108. Camera framing tuned to that.
            Shot(cam, "01_overview.png", new Vector3(90f, 70f, -30f), new Vector3(90f, 0f, 54f), 60f);
            Shot(cam, "02_closeprobe.png", new Vector3(6f, 4f, -6f), new Vector3(9f, 1.5f, 6f), 55f);
            Shot(cam, "03_mid_grid.png", new Vector3(45f, 12f, 20f), new Vector3(45f, 1f, 42f), 55f);
            Shot(cam, "04_far_grid.png", new Vector3(90f, 18f, 60f), new Vector3(90f, 0f, 90f), 55f);

            Debug.Log("TS_SHOTS_DONE");
        }

        static void Shot(Camera cam, string name, Vector3 from, Vector3 at, float fov)
        {
            var p0 = cam.transform.position;
            var r0 = cam.transform.rotation;
            var f0 = cam.fieldOfView;
            var c0 = cam.clearFlags;

            cam.transform.position = from;
            cam.transform.LookAt(at);
            cam.fieldOfView = fov;
            cam.farClipPlane = 1200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.06f, .07f, .10f);

            var rt = new RenderTexture(1600, 900, 24);
            var req = new UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            var prev = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(cam, req);
                RenderTexture.active = rt;
                tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(OutDir, name)), tex.EncodeToPNG());
                Debug.Log("ts shot " + name);
            }
            catch (Exception e) { Debug.LogError("shot fail " + name + ": " + e.Message); }
            finally
            {
                RenderTexture.active = prev;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                cam.transform.position = p0;
                cam.transform.rotation = r0;
                cam.fieldOfView = f0;
                cam.clearFlags = c0;
            }
        }
    }
}
