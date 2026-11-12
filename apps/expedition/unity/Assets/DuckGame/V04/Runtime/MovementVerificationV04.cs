using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckGame.V04
{
    [DefaultExecutionOrder(-100)]
    public sealed class MovementVerificationV04 : MonoBehaviour
    {
        public GooseMotorV04 motor;
        public GoosePoseV04 pose;
        public ShoulderCamera view;
        public OrbShooter shooter;

        [Serializable] public class Frame
        {
            public float time, speed, phase, height, vertical, meshMin, meshMax;
            public string state;
            public bool grounded, hoverUsed, standBlocked, leftSupport, rightSupport;
            public Vector3 position, left, right, meshBoundsMin, meshBoundsMax, neckBase, head, hatTip;
            public float dt, forwardExtent;
        }
        [Serializable] public class Report
        {
            public bool passed;
            public string revision = "v05-double-jump-glide";
            public List<Frame> samples = new List<Frame>();
            public List<string> errors = new List<string>();
            public List<string> captures = new List<string>();
            public float maxSupportDrift, maxFootFrameDelta, maxHoverEpisode;
            public float maxSupportDriftTime, maxFootFrameDeltaTime;
            public float maxSupportDriftDt, maxFootFrameDeltaDt, maxFootSpeed, maxFootSpeedTime;
            public float maxForwardExtent;
            public bool blockedSlideStartExercised;
            public int landings, hoverEpisodes, shots;
            public int outlineRenderers, outlineMaterials;
            public string outlineShader;
            public float outlineWidthPixels;
            public bool hatOutlineSharedMesh, outlineShaderHasErrors;
            public List<string> runtimeErrors = new List<string>();
        }

        bool running;
        Report report;
        SkinnedMeshRenderer[] skins;
        Mesh[] baked;
        Camera side;
        Collider roof, frontWall;
        string output;
        float clock, nextSample, previousPhase, hoverClock, teleportGrace;
        Vector3 lastLeft, lastRight;
        bool lastLS, lastRS, lastGround;
        MoveState lastState;
        readonly HashSet<int> actions = new HashSet<int>();

        void Awake()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("DuckGame.V04.Verify", false))
            {
                UnityEditor.SessionState.SetBool("DuckGame.V04.Verify", false);
                string previous = UnityEditor.SessionState.GetString("DuckGame.V04.PreviousPlayScene", "");
                UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous)
                    ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(previous);
                running = enabled = true;
                motor.InjectInput = view.InjectInput = shooter.InjectInput = true;
            }
#endif
        }

        IEnumerator Start()
        {
            if (!running) yield break;
            report = new Report();
            Application.logMessageReceived += CaptureRuntimeError;
            InspectOutline();
            roof = GameObject.Find("V04LowRoof").GetComponent<Collider>();
            frontWall = GameObject.Find("V04TunnelEndWall").GetComponent<Collider>();
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/verification/v04-play"));
            Directory.CreateDirectory(output);
            var list = new List<SkinnedMeshRenderer>();
            foreach (var skin in motor.animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (!skin.name.Contains("_Outline")) list.Add(skin);
            skins = list.ToArray();
            baked = new Mesh[skins.Length];
            for (int i = 0; i < baked.Length; i++) baked[i] = new Mesh();
            side = new GameObject("V04VerificationSideCamera").AddComponent<Camera>();
            side.enabled = false;
            side.orthographic = true;
            side.orthographicSize = 2.25f;
            side.nearClipPlane = .03f;
            shooter.Fired += (t, p, d) => report.shots++;
            float[] times = { 1f, 2.25f, 3.2f, 4.2f, 5.08f, 5.35f, 5.8f, 6.7f, 7.15f, 8.1f, 9.98f, 11.78f, 13f, 17.8f, 19.1f, 20.7f };
            string[] labels = { "walk", "turn", "stop", "wall", "takeoff", "apex", "landing", "hover", "hover_expired", "second_hover", "ceiling_jump", "slide", "low_roof", "back_out", "stand", "ads_shoot" };
            int capture = 0;
            while (clock < 22.8f)
            {
                yield return new WaitForEndOfFrame();
                float dt = Time.deltaTime;
                clock += dt;
                teleportGrace = Mathf.Max(0, teleportGrace - dt);
                Drive();
                CheckEveryFrame(dt);
                if (clock >= nextSample)
                {
                    nextSample = clock + 1f / 30;
                    Record();
                }
                if (capture < times.Length && clock >= times[capture])
                {
                    string name = (capture + 1).ToString("00") + "_" + labels[capture];
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
                    report.captures.Add(name + ".png");
                    if (capture == 0 || capture == 5 || capture == 7 || capture == 11 || capture == 12)
                        CaptureSide(name + "_side.png");
                    capture++;
                }
            }
            motor.TestMove = Vector2.zero;
            motor.TestJumpHeld = false;
            motor.InjectInput = view.InjectInput = shooter.InjectInput = false;
            shooter.TestFire = view.TestADS = false;
            if (report.maxSupportDrift > .015f) AddError("Support foot drift exceeded 1.5cm per frame");
            if (!report.blockedSlideStartExercised) AddError("Blocked slide start was not exercised");
            if (report.hoverEpisodes < 2) report.errors.Add("Hover did not refresh after landing");
            if (report.landings < 3) report.errors.Add("Landing events missing");
            if (report.maxHoverEpisode > motor.hoverDuration + .08f) report.errors.Add("Repeated space extended hover");
            if (!report.samples.Exists(f => f.standBlocked)) report.errors.Add("Low roof recovery not exercised");
            if (!report.samples.Exists(f => f.time > 18.4f && f.height > 3.4f)) report.errors.Add("Did not stand after exiting roof");
            if (report.shots < 2) report.errors.Add("Shooting regression not exercised");
            if (report.runtimeErrors.Count > 0) AddError("Runtime errors were logged");
            report.passed = report.errors.Count == 0;
            Application.logMessageReceived -= CaptureRuntimeError;
            File.WriteAllText(Path.Combine(output, "movement-report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("DUCK_V04_PLAY_REPORT " + report.passed + " " + output);
            yield return new WaitForSeconds(.2f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void Drive()
        {
            motor.TestMove = Vector2.zero;
            motor.TestRun = false;
            motor.TestJumpHeld = clock > 6.36f && clock < 7.45f || clock > 8.96f && clock < 9.45f;
            if (clock > .5f && clock < 2) motor.TestMove = Vector2.up;
            if (clock >= 2 && clock < 2.8f) motor.TestMove = Vector2.right;
            Once(1, 3.4f, () => Teleport(new Vector3(5, .03f, .8f)));
            if (clock > 3.6f && clock < 4.45f) { motor.TestMove = Vector2.up; motor.TestRun = true; }
            Once(2, 4.6f, () => Teleport(new Vector3(0, .03f, -6)));
            Jump(3, 5f);
            Jump(4, 6.2f); Jump(5, 6.36f);
            Jump(6, 6.50f); Jump(7, 6.65f); Jump(8, 6.8f); Jump(9, 6.95f);
            if (clock > 6.3f && clock < 7.2f) motor.TestMove = Vector2.right;
            Jump(10, 8.8f); Jump(11, 8.96f);
            Once(12, 9.5f, () => Teleport(new Vector3(10, .03f, 0)));
            Jump(13, 9.8f);
            Once(14, 11f, () => Teleport(new Vector3(-10, .03f, -3.8f)));
            if (clock > 11.05f && clock < 14.5f) motor.TestMove = Vector2.up;
            Once(15, 11.2f, () => motor.TestSlide = true);
            if (clock >= 14.5f && clock < 19.0f) motor.TestMove = Vector2.down;
            Once(16, 19.3f, () => Teleport(new Vector3(0, .03f, -6)));
            shooter.TestFire = clock > 20 && clock < 21.8f;
            view.TestADS = clock > 20.2f && clock < 20.9f;
            view.TestLook = Vector2.zero;
            Once(17, 21.9f, () => Teleport(new Vector3(-10, .03f, 5.4f), 180));
            if (clock > 21.95f && clock < 22.1f) motor.TestMove = Vector2.down;
            Once(18, 22.03f, () => motor.TestSlide = true);
            if (clock > 22.15f && clock < 22.6f)
            {
                report.blockedSlideStartExercised = true;
                if (motor.State == MoveState.Slide) AddError("Slide started with occupied forward envelope");
            }
        }

        void Jump(int id, float time) => Once(id, time, () => motor.TestJump = true);
        void Once(int id, float time, Action action)
        {
            if (clock >= time && actions.Add(id)) action();
        }
        void Teleport(Vector3 position, float yaw = 0)
        {
            motor.controller.enabled = false;
            motor.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            motor.ResetForTeleport();
            motor.controller.enabled = true;
            pose.ResetForTeleport();
            teleportGrace = .25f;
        }

        void CheckEveryFrame(float dt)
        {
            Vector3 left = pose.LeftFoot.position, right = pose.RightFoot.position;
            if (teleportGrace <= 0 && clock > .2f)
            {
                float supportDrift = Mathf.Max(pose.LeftSupport && lastLS ? Vector3.ProjectOnPlane(left - lastLeft, Vector3.up).magnitude : 0,
                    pose.RightSupport && lastRS ? Vector3.ProjectOnPlane(right - lastRight, Vector3.up).magnitude : 0);
                float footDelta = Mathf.Max((left - lastLeft).magnitude, (right - lastRight).magnitude);
                if (supportDrift > report.maxSupportDrift) { report.maxSupportDrift = supportDrift; report.maxSupportDriftTime = clock; report.maxSupportDriftDt = dt; }
                float footSpeed = footDelta / Mathf.Max(.0001f, dt);
                if (footSpeed > report.maxFootSpeed) { report.maxFootSpeed = footSpeed; report.maxFootSpeedTime = clock; }
                if (footSpeed > Mathf.Max(motor.runSpeed, motor.ActualVelocity.magnitude) * 4 + 4)
                    AddError("Foot frame velocity exceeded locomotion continuity envelope");
                if (footDelta > report.maxFootFrameDelta) { report.maxFootFrameDelta = footDelta; report.maxFootFrameDeltaTime = clock; report.maxFootFrameDeltaDt = dt; }
            }
            if (motor.OnGround && !lastGround && teleportGrace <= 0) report.landings++;
            if (motor.State == MoveState.Hover)
            {
                if (lastState != MoveState.Hover) { report.hoverEpisodes++; hoverClock = 0; }
                hoverClock += dt;
                report.maxHoverEpisode = Mathf.Max(report.maxHoverEpisode, hoverClock);
            }
            if (clock > 4.1f && clock < 4.4f && motor.ActualVelocity.magnitude < .05f && Mathf.Abs(pose.Phase - previousPhase) > .001f)
                AddError("Wall stopped body but gait continued");
            if (Physics.CheckSphere(view.transform.position, .15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) AddError("Camera overlapping geometry");
            previousPhase = pose.Phase;
            lastLeft = left; lastRight = right;
            lastLS = pose.LeftSupport; lastRS = pose.RightSupport;
            lastGround = motor.OnGround; lastState = motor.State;
        }

        void Record()
        {
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            Vector3 boundsMin = Vector3.one * float.PositiveInfinity, boundsMax = Vector3.one * float.NegativeInfinity;
            float forwardExtent = float.NegativeInfinity;
            Bounds roofBounds = roof.bounds, wallBounds = frontWall.bounds;
            roofBounds.Expand(-.01f); wallBounds.Expand(-.01f);
            for (int i = 0; i < skins.Length; i++)
            {
                skins[i].BakeMesh(baked[i]);
                foreach (Vector3 local in baked[i].vertices)
                {
                    Vector3 p = skins[i].transform.TransformPoint(local);
                    if (float.IsNaN(p.y) || float.IsInfinity(p.y)) AddError("Nonfinite pose");
                    low = Mathf.Min(low, p.y); high = Mathf.Max(high, p.y);
                    boundsMin = Vector3.Min(boundsMin, p); boundsMax = Vector3.Max(boundsMax, p);
                    Vector3 playerLocal = motor.transform.InverseTransformPoint(p);
                    forwardExtent = Mathf.Max(forwardExtent, playerLocal.z);
                    if (roofBounds.Contains(p)) AddError("Visible mesh intersected low roof AABB");
                    if (wallBounds.Contains(p)) AddError("Visible mesh intersected tunnel front wall AABB");
                    if (pose.Compression > .999f &&
                        (playerLocal.z > motor.SlideLocalEnvelope.max.z || playerLocal.z < motor.SlideLocalEnvelope.min.z ||
                         Mathf.Abs(playerLocal.x) > motor.SlideLocalEnvelope.extents.x || playerLocal.y > motor.SlideLocalEnvelope.max.y))
                        AddError("Slide mesh exceeded reserved collision envelope");
                }
            }
            if (pose.Compression > .95f) report.maxForwardExtent = Mathf.Max(report.maxForwardExtent, forwardExtent);
            if (low < -.025f) AddError("Visible mesh below floor");
            if (clock > 11.7f && clock < 14.4f && pose.Compression > .95f && high > 1.56f) AddError("Slide visible mesh above roof clearance");
            if (clock > 9.7f && clock < 10.8f && high > 4.02f) AddError("Hat penetrated jump ceiling");
            report.samples.Add(new Frame { time = clock, speed = motor.ActualVelocity.magnitude, phase = pose.Phase,
                height = motor.controller.height, vertical = motor.VerticalSpeed, state = motor.State.ToString(),
                grounded = motor.OnGround, hoverUsed = motor.HoverUsed, standBlocked = motor.StandBlocked,
                leftSupport = pose.LeftSupport, rightSupport = pose.RightSupport, position = motor.transform.position,
                left = pose.LeftFoot.position, right = pose.RightFoot.position, meshMin = low, meshMax = high,
                dt = Time.deltaTime, meshBoundsMin = boundsMin, meshBoundsMax = boundsMax, forwardExtent = forwardExtent,
                neckBase = pose.BonePosition("Neck1"), head = pose.BonePosition("Head"), hatTip = pose.BonePosition("HatTip") });
        }
        void CaptureRuntimeError(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                if (!report.runtimeErrors.Contains(message)) report.runtimeErrors.Add(message);
        }

        void InspectOutline()
        {
            var materials = new HashSet<Material>();
            foreach (var skin in motor.animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool outline = false;
                foreach (var material in skin.sharedMaterials)
                {
                    if (material == null || material.shader == null || material.shader.name != "Duck Game/V04 Pixel Outline URP") continue;
                    outline = true;
                    materials.Add(material);
                    report.outlineShader = material.shader.name;
                    report.outlineWidthPixels = material.GetFloat("_WidthPixels");
#if UNITY_EDITOR
                    report.outlineShaderHasErrors |= UnityEditor.ShaderUtil.ShaderHasError(material.shader);
#endif
                }
                if (outline) report.outlineRenderers++;
            }
            report.outlineMaterials = materials.Count;
            report.hatOutlineSharedMesh = shooter.hat.hat != null && shooter.hat.outline != null &&
                shooter.hat.hat.sharedMesh == shooter.hat.outline.sharedMesh;
            if (report.outlineRenderers == 0 || report.outlineMaterials != 1 || report.outlineShaderHasErrors ||
                !report.hatOutlineSharedMesh || report.outlineWidthPixels < 1 || report.outlineWidthPixels > 1.3f)
                AddError("Thin outline configuration invalid");
        }

        void AddError(string value) { if (!report.errors.Contains(value)) report.errors.Add(value); }

        void CaptureSide(string file)
        {
            bool low = motor.State == MoveState.Slide;
            Vector3 root = motor.transform.position;
            side.orthographicSize = low ? 1.25f : 2.25f;
            side.transform.position = new Vector3(root.x + 6, low ? 1.0f : 2.6f, root.z - 3);
            side.transform.LookAt(new Vector3(root.x, low ? .7f : 1.7f, root.z));
            var texture = new RenderTexture(960, 720, 24);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = texture };
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(side, request);
                RenderTexture.active = texture;
                pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, file), pixels.EncodeToPNG());
                report.captures.Add(file);
            }
            finally
            {
                RenderTexture.active = previous;
                texture.Release();
                Destroy(texture);
                if (pixels != null) Destroy(pixels);
            }
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= CaptureRuntimeError;
            if (running)
            {
                motor.InjectInput = view.InjectInput = shooter.InjectInput = false;
                motor.TestMove = Vector2.zero; motor.TestJump = motor.TestJumpHeld = motor.TestSlide = motor.TestRun = false;
                view.TestLook = Vector2.zero; view.TestADS = shooter.TestFire = false;
            }
            if (baked != null) foreach (Mesh mesh in baked) if (mesh != null) Destroy(mesh);
            if (side != null) Destroy(side.gameObject);
        }
    }
}
