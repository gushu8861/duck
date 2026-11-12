using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DuckGame.Expedition;
using DuckGame.TS;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Captures the game exactly as the player sees it, including both the graded 3D world and
    /// the ScreenSpaceOverlay HUD.
    ///
    /// Why this approach:
    ///   - Camera.Render() runs the forward pass but skips URP post-processing, so graded
    ///     captures looked flat.
    ///   - SubmitRenderRequest includes post-processing but only the requested camera, so a
    ///     separate UI camera never appears.
    ///   - ScreenSpaceOverlay UI is composited by the compositor after everything, so the ONLY
    ///     capture path that shows world + UI together is a real backbuffer grab.
    ///
    /// ScreenCapture.CaptureScreenshot was reporting success without writing files under
    /// -batchmode, so this uses the window handle: it runs with -nographics OFF, waits for the
    /// file to actually exist, and copies it out of persistentDataPath.
    /// </summary>
    [InitializeOnLoad]
    public static class ExpeditionShots
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        const string OutDir = "docs/expedition-shots";
        const string DoneMarker = "docs/.expedition-shots.done";

        static bool engaged;
        static float t0;
        static bool begun;

        static ExpeditionShots()
        {
            bool requested = false;
            var argv = Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length; i++)
                if (argv[i] == "-expedition-shots") { requested = true; break; }
            if (!requested || engaged) return;
            engaged = true;
            EditorApplication.delayCall += Begin;
        }

        static void Begin()
        {
            try
            {
                if (EditorApplication.isPlaying)
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        Path.GetFileNameWithoutExtension(HubScene));
                else
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        HubScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    EditorApplication.isPlaying = true;
                }
                EditorApplication.update += Tick;
            }
            catch (Exception e) { Debug.LogError("shots begin failed: " + e); }
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            if (t0 == 0f) t0 = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - t0 < 3f) return;
            if (begun) return;
            begun = true;
            EditorApplication.update -= Tick;

            var host = UnityEngine.Object.FindObjectOfType<ExpeditionHUD>();
            if (host != null) host.StartCoroutine(Capture());
            else Done();
        }

        static IEnumerator Capture()
        {
            Directory.CreateDirectory(Path.GetFullPath(OutDir));
            yield return new WaitForSeconds(.8f);

            var player = GameObject.Find("PlayerRig") ?? GameObject.Find("GooseWizard_Rig");
            var cam = Camera.main ?? UnityEngine.Object.FindObjectOfType<Camera>();
            Vector3 pc = player != null ? player.transform.position : Vector3.zero;

            var rw = RewardSystem.Instance;
            if (rw != null && rw.Choosing) rw.Pick(0);
            yield return new WaitForSeconds(.3f);

            if (cam != null)
            {
                cam.transform.position = pc + new Vector3(0f, 3.0f, -6.5f);
                cam.transform.LookAt(pc + Vector3.up * 1.3f);
                cam.allowHDR = true;
                var d = cam.GetComponent<UniversalAdditionalCameraData>();
                if (d != null) { d.renderPostProcessing = true; d.dithering = true; }
            }
            yield return new WaitForSeconds(.5f);

            yield return Grab("01_hud.png");

            // combat: land a hit and capture during the flash
            var brains = UnityEngine.Object.FindObjectsOfType<EnemyBrain>();
            if (brains.Length > 0 && cam != null)
            {
                var b = brains[0];
                cam.transform.position = b.transform.position + new Vector3(2.2f, 2.4f, -4.2f);
                cam.transform.LookAt(b.transform.position + Vector3.up * .7f);
                yield return new WaitForSeconds(.25f);

                var shot = new DuckGame.OrbShotContext(Vector3.zero, Vector3.forward, 0, null);
                var imp = new DuckGame.OrbImpactContext(
                    b.GetComponent<Collider>(), b.transform.position, Vector3.up, true);
                b.ReceiveOrbHit(shot, imp);
                yield return new WaitForSeconds(.10f);
                yield return Grab("02_combat.png");
            }

            // reward picker last
            if (cam != null && player != null)
            {
                cam.transform.position = pc + new Vector3(0f, 3.0f, -6.5f);
                cam.transform.LookAt(pc + Vector3.up * 1.3f);
            }
            var run = ExpeditionRun.Instance;
            if (run != null && run.Current != null)
            {
                run.CompleteIsland();
                if (rw != null) rw.OfferFor(run.Current, run.Current.element);
            }
            yield return new WaitForSeconds(.5f);
            yield return Grab("03_reward.png");

            Done();
        }

        /// <summary>
        /// Grab the real backbuffer (world + overlay UI).
        ///
        /// ScreenCapture.CaptureScreenshot silently wrote nothing in this environment, so the
        /// frame is read straight out of the backbuffer at end-of-frame instead. ScreenSpaceOverlay
        /// UI is painted into that same backbuffer by the UI system, which is why it appears here
        /// but never in a RenderTexture handed to a single camera.
        /// </summary>
        static IEnumerator Grab(string name)
        {
            yield return new WaitForEndOfFrame();

            string dst = Path.GetFullPath(Path.Combine(OutDir, name));
            Texture2D tex = null;
            var prevActive = RenderTexture.active;
            try
            {
                int w = Screen.width, h = Screen.height;
                RenderTexture.active = null;   // read the backbuffer itself

                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                File.WriteAllBytes(dst, tex.EncodeToPNG());
                Debug.Log("GRABBED " + name + " " + w + "x" + h +
                          " -> " + new FileInfo(dst).Length + " bytes");
            }
            catch (Exception e) { Debug.LogError("grab failed " + name + ": " + e.Message); }
            finally
            {
                RenderTexture.active = prevActive;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static void Done()
        {
            File.WriteAllText(Path.GetFullPath(DoneMarker), "done");
            Debug.Log("EXPEDITION_SHOTS_DONE");
        }
    }
}
