using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckGame.TS
{
    /// <summary>Renders the integrated playground so the imported scenery can be verified in situ.</summary>
    public static class TSPlaygroundShots
    {
        const string Scene = "Assets/DuckGame/Generated/v03/PlaygroundWithTS.unity";
        const string OutDir = "docs/ts-playground";

        [MenuItem("Duck Game/Import TS/Capture Integrated Playground")]
        public static void Capture()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutDir));
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (cam == null) { Debug.LogError("no camera"); return; }

            Shot(cam, "01_topdown.png", new Vector3(0f, 90f, 4f), new Vector3(0f, 0f, 4f), 60f);
            Shot(cam, "02_ground_level.png", new Vector3(0f, 3f, -30f), new Vector3(0f, 1.5f, 20f), 60f);
            Shot(cam, "03_ring_side.png", new Vector3(-38f, 12f, -12f), new Vector3(0f, 1f, 10f), 55f);
            Shot(cam, "04_ring_far.png", new Vector3(0f, 16f, 60f), new Vector3(0f, 0f, 10f), 55f);
            Debug.Log("TS_PLAYSAGE_SHOTS_DONE");
        }

        static void Shot(Camera cam, string name, Vector3 from, Vector3 at, float fov)
        {
            var p0 = cam.transform.position; var r0 = cam.transform.rotation;
            var f0 = cam.fieldOfView; var far0 = cam.farClipPlane; var cl0 = cam.clearFlags;
            cam.transform.position = from; cam.transform.LookAt(at);
            cam.fieldOfView = fov; cam.farClipPlane = 800f;
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
                Debug.Log("ts playground shot " + name);
            }
            catch (Exception e) { Debug.LogError("shot fail " + name + ": " + e.Message); }
            finally
            {
                RenderTexture.active = prev; rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                cam.transform.position = p0; cam.transform.rotation = r0;
                cam.fieldOfView = f0; cam.farClipPlane = far0; cam.clearFlags = cl0;
            }
        }
    }
}
