using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DuckGame.V04.EditorTools
{
    [InitializeOnLoad]
    static class MovementDiagnosticsV05
    {
        const string Request = "docs/verification/v05-request.txt";
        static MovementDiagnosticsV05() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || !File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim();
            if (command.Length == 0) return;
            File.WriteAllText(Request, "");
            var motor = UnityEngine.Object.FindObjectOfType<GooseMotorV04>();
            if (motor == null || motor.GetComponent<MovementProbeV05>() != null) return;
            motor.gameObject.AddComponent<MovementProbeV05>().Begin(command);
        }
    }

    [DefaultExecutionOrder(-110)]
    public sealed class MovementProbeV05 : MonoBehaviour
    {
        [Serializable] public class Row
        {
            public string scenario, state;
            public float time, dt, vertical, speed, stateTime, ads, phase, height;
            public bool grounded, doubleUsed, hoverUsed, jumpHeld, standBlocked, injectedMotor, injectedView;
            public Vector3 root, velocity, hatViewport, leftFoot, rightFoot;
            public float rayHatDistance;
        }
        [Serializable] public class Report
        {
            public string mode;
            public float walk, run, acceleration, braking;
            public int shots;
            public List<Row> samples = new List<Row>();
            public List<string> errors = new List<string>();
        }
        readonly string[] cases = { "single-hold", "late-double", "glide-expire", "glide-release", "ceiling", "ledge", "stop", "reverse", "angled-slide", "low-roof", "ads-shoot", "landing-reset", "walk-off" };
        GooseMotorV04 motor;
        GoosePoseV04 pose;
        ShoulderCamera view;
        OrbShooter shooter;
        GameObject platform;
        Report report;
        string folder, scenario;
        int index = -1;
        float start, caseStart;
        bool oldMotor, oldView, oldShooter, active;
        readonly HashSet<int> actions = new HashSet<int>();
        FieldInfo heldField;
        PropertyInfo doubleProperty;
        public void Begin(string command)
        {
            motor = GetComponent<GooseMotorV04>(); pose = GetComponent<GoosePoseV04>(); view = motor.view;
            shooter = GetComponentInChildren<OrbShooter>();
            heldField = typeof(GooseMotorV04).GetField("TestJumpHeld");
            doubleProperty = typeof(GooseMotorV04).GetProperty("DoubleJumpUsed");
            oldMotor = motor.InjectInput; oldView = view.InjectInput; oldShooter = shooter.InjectInput;
            motor.InjectInput = view.InjectInput = shooter.InjectInput = !command.StartsWith("manual");
            report = new Report { mode = command, walk = motor.walkSpeed, run = motor.runSpeed, acceleration = motor.acceleration, braking = motor.braking };
            folder = Path.GetFullPath("docs/verification/v05/" + command); Directory.CreateDirectory(folder);
            Application.logMessageReceived += Log; shooter.Fired += Shot;
            active = true; start = Time.time; Next();
        }
        void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message); }
        void Shot(double t, Vector3 p, Vector3 d) { report.shots++; }
        void Next()
        {
            if (platform != null) Destroy(platform);
            index++; actions.Clear(); caseStart = Time.time + .35f;
            if (report.mode.StartsWith("manual")) { scenario = "manual"; return; }
            scenario = report.mode.Contains("inertia") ? (index == 0 ? "stop" : "reverse") : cases[index];
            motor.controller.enabled = false;
            Vector3 position = new Vector3(-8, .03f, -12);
            float yaw = 0;
            if (scenario == "angled-slide") { position = new Vector3(1.9f, .03f, -1); yaw = 45; }
            if (scenario == "low-roof") position = new Vector3(-10, .03f, -3.8f);
            if (scenario == "ceiling") position = new Vector3(10, .03f, 0);
            if (scenario == "ledge") position = new Vector3(-8, 1.5f, -12);
            if (scenario == "walk-off")
            {
                platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = "V05TemporaryLedge";
                platform.transform.position = new Vector3(-8, 1, -12);
                platform.transform.localScale = new Vector3(3, 2, 3);
                Physics.SyncTransforms();
                position = new Vector3(-8, 2.03f, -12);
            }
            motor.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            motor.ResetForTeleport(); motor.controller.enabled = true; pose.ResetForTeleport();
            motor.TestJump = motor.TestSlide = motor.TestRun = false; motor.TestMove = Vector2.zero;
            view.TestADS = false; view.TestLook = Vector2.zero;
            foreach (string field in new[] { "desiredYaw", "angleYVelocity" })
                typeof(ShoulderCamera).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(view, 0f);
            typeof(ShoulderCamera).GetProperty("Yaw").SetValue(view, 0f);
            view.transform.rotation = Quaternion.Euler(12, 0, 0);
        }
        void Once(int id, float t, float at, Action action) { if (t >= at && actions.Add(id)) action(); }
        void Jump(int id, float t, float at) { Once(id, t, at, () => motor.TestJump = true); }
        void Update()
        {
            if (!active || report.mode.StartsWith("manual")) return;
            float t = Time.time - caseStart;
            if (t < 0) return;
            motor.TestMove = Vector2.zero; motor.TestRun = false; view.TestLook = Vector2.zero; view.TestADS = false; shooter.TestFire = false;
            bool held = false;
            if (scenario == "stop" || scenario == "reverse")
            {
                motor.TestRun = true;
                motor.TestMove = t < 1.6f ? Vector2.right : scenario == "reverse" ? Vector2.left : Vector2.zero;
            }
            else if (scenario == "angled-slide")
            {
                motor.TestMove = t < .5f ? Vector2.one.normalized : Vector2.zero;
                Once(0, t, .16f, () => motor.TestSlide = true);
            }
            else if (scenario == "low-roof")
            {
                motor.TestMove = t < 2.5f ? Vector2.up : Vector2.down;
                Once(0, t, .15f, () => motor.TestSlide = true);
            }
            else if (scenario == "walk-off")
            {
                motor.TestMove = t < .75f ? Vector2.right : Vector2.zero;
                if (t > .5f && !motor.OnGround && actions.Add(0)) motor.TestJump = true;
                if (actions.Contains(0)) Jump(1, t, .85f);
            }
            else if (scenario == "ads-shoot")
            {
                view.TestADS = t > .3f;
                view.TestLook = new Vector2((t < 2 ? 1 : -1) * 90 * Time.deltaTime / view.mouseSensitivity, 0);
                shooter.TestFire = t > 2.0f;
                motor.TestMove = t > 1 && t < 2 ? Vector2.right : Vector2.zero;
                Jump(0, t, 1.2f); Jump(1, t, 1.4f); held = t > 1.4f && t < 2.5f;
            }
            else
            {
                Jump(0, t, scenario == "ledge" ? .15f : .3f);
                held = scenario == "single-hold" && t >= .3f;
                if (scenario != "single-hold" && scenario != "ledge")
                {
                    float second = scenario == "late-double" ? .82f : .5f;
                    Jump(1, t, second);
                    if (scenario == "glide-expire" || scenario == "glide-release" || scenario == "landing-reset") held = t >= second;
                    if (scenario == "glide-release" && t > 1.0f && t < 1.2f) held = false;
                    Jump(2, t, 1.3f); Jump(3, t, 1.5f);
                    if (scenario == "landing-reset") { Jump(4, t, 2.8f); Jump(5, t, 3.0f); held = t > 3.0f || t >= .5f && t < 1.9f; }
                }
                if (scenario == "ledge") Jump(1, t, .3f);
            }
            heldField?.SetValue(motor, held);
        }
        IEnumerator Start()
        {
            while (active)
            {
                yield return new WaitForEndOfFrame();
                Record();
            }
        }
        void Record()
        {
            if (!active) return;
            float t = Time.time - caseStart;
            if (t < 0) return;
            Ray ray = view.AimRay;
            report.samples.Add(new Row { scenario = scenario, time = t, dt = Time.deltaTime, state = motor.State.ToString(), stateTime = motor.StateTime,
                vertical = motor.VerticalSpeed, speed = motor.ActualVelocity.magnitude, velocity = motor.ActualVelocity, root = transform.position,
                leftFoot = pose.LeftFoot.position, rightFoot = pose.RightFoot.position, grounded = motor.OnGround, doubleUsed = doubleProperty != null && (bool)doubleProperty.GetValue(motor), hoverUsed = motor.HoverUsed,
                jumpHeld = heldField != null && (bool)heldField.GetValue(motor), standBlocked = motor.StandBlocked, phase = pose.Phase, height = motor.controller.height,
                ads = view.ADS, hatViewport = view.lens.WorldToViewportPoint(shooter.hat.TipWorld), rayHatDistance = Vector3.Cross(shooter.hat.TipWorld - ray.origin, ray.direction).magnitude,
                injectedMotor = motor.InjectInput, injectedView = view.InjectInput });
            Once(20, t, scenario == "glide-expire" ? .58f : .9f, () => ScreenCapture.CaptureScreenshot(folder + "/" + scenario + "-early.png"));
            Once(21, t, scenario == "glide-expire" ? 1.0f : 3.3f, () => ScreenCapture.CaptureScreenshot(folder + "/" + scenario + "-late.png"));
            if (scenario == "glide-expire" || scenario == "stop" || scenario == "reverse")
            {
                Once(22, t, scenario == "glide-expire" ? .64f : 1.75f, () => CaptureSide(scenario + "-side-early.png"));
                Once(23, t, scenario == "glide-expire" ? 1.02f : 2.15f, () => CaptureSide(scenario + "-side-late.png"));
            }
            float duration = report.mode.StartsWith("manual") ? 12 : scenario == "low-roof" ? 7 : scenario == "landing-reset" ? 5 : 4;
            if (t < duration) return;
            File.WriteAllText(folder + "/report.json", JsonUtility.ToJson(report, true));
            int count = report.mode.Contains("inertia") ? 2 : cases.Length;
            if (report.mode.StartsWith("manual") || index + 1 >= count) { Restore(); Debug.Log("DUCK_V05_REPORT " + folder); Destroy(this); }
            else Next();
        }
        void CaptureSide(string file)
        {
            Camera side = new GameObject("V05TemporarySideCamera").AddComponent<Camera>();
            side.enabled = false; side.orthographic = true; side.orthographicSize = 2.3f; side.nearClipPlane = .03f;
            side.transform.position = transform.position + new Vector3(6, 2.6f, -3);
            side.transform.LookAt(transform.position + Vector3.up * 1.7f);
            var texture = new RenderTexture(960, 720, 24);
            var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(side, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture;
                pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); pixels.Apply();
                File.WriteAllBytes(folder + "/" + file, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; texture.Release(); Destroy(texture);
                if (pixels != null) Destroy(pixels); Destroy(side.gameObject);
            }
        }
        void Restore()
        {
            if (!active) return; active = false;
            Application.logMessageReceived -= Log; shooter.Fired -= Shot;
            motor.TestMove = Vector2.zero; motor.TestJump = motor.TestSlide = motor.TestRun = false; heldField?.SetValue(motor, false);
            view.TestLook = Vector2.zero; view.TestADS = false; shooter.TestFire = false;
            motor.InjectInput = oldMotor; view.InjectInput = oldView; shooter.InjectInput = oldShooter;
            if (platform != null) Destroy(platform);
        }
        void OnDestroy() { Restore(); }
    }
}

