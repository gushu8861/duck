using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DuckGame.V04;

namespace DuckGame.EditorTools
{
    [InitializeOnLoad] static class OrbVerificationV06
    {
        static OrbVerificationV06() { EditorApplication.update += Tick; }
        static void Tick()
        {
            const string path = "docs/verification/v06-request.txt";
            if (!File.Exists(path)) return;
            string mode = File.ReadAllText(path).Trim();
            if (mode.Length == 0) return;
            if (mode == "validate")
            {
                File.WriteAllText(path, ""); PrototypeValidator.Validate(); return;
            }
            if (!EditorApplication.isPlaying) return;
            var shooter = UnityEngine.Object.FindObjectOfType<OrbShooter>();
            if (shooter == null || shooter.GetComponent<OrbProbeV06>() != null) return;
            File.WriteAllText(path, ""); shooter.gameObject.AddComponent<OrbProbeV06>().Begin(mode);
        }
    }

    [DefaultExecutionOrder(-120)] public sealed class OrbProbeV06 : MonoBehaviour
    {
        [Serializable] public class Frame
        {
            public string scenario, state;
            public float time, dt, load, interval, ads, travel, originError, rayHatDistance;
            public int loaded, shots, active, peak;
            public bool visible, injected, grounded;
            public Vector3 tip, round, scale, axis;
        }
        [Serializable] public class Shot { public string scenario; public float time, interval; public Vector3 origin, formed; }
        [Serializable] public class Report
        {
            public List<Frame> frames = new List<Frame>();
            public List<Shot> shots = new List<Shot>();
            public List<string> errors = new List<string>();
            public int enemyDelta, environmentDelta, targetDelta, peak, reused, activeAfter, realEnemy, realEnvironment, realTarget;
        }
        readonly string[] scenarios = { "hold-fast", "hold-normal", "hold-slow", "faster-mid", "slower-mid", "tap", "disable-reset", "ads-slide", "impacts" };
        readonly HashSet<int> once = new HashSet<int>();
        readonly HashSet<int> existingProjectiles = new HashSet<int>();
        OrbShooter shooter; GooseMotorV04 motor; GoosePoseV04 pose; ShoulderCamera view;
        OrbConfig original, runtime; bool oldShooter, oldMotor, oldView, running;
        string mode, scenario, output; int index = -1, capture; float start; Report report;
        int enemyBefore, environmentBefore, targetBefore; TargetDummy dummy; float oldRate; bool oldEnabled;
        public void Begin(string requested)
        {
            mode = requested; shooter = GetComponent<OrbShooter>(); motor = FindObjectOfType<GooseMotorV04>(); pose = motor.GetComponent<GoosePoseV04>(); view = shooter.view;
            oldRate = shooter.rateMultiplier; oldEnabled = shooter.enabled;
            foreach (var projectile in FindObjectsOfType<OrbProjectile>()) existingProjectiles.Add(projectile.GetInstanceID());
            original = shooter.config; runtime = Instantiate(original); shooter.config = runtime;
            oldShooter = shooter.InjectInput; oldMotor = motor.InjectInput; oldView = view.InjectInput;
            shooter.InjectInput = motor.InjectInput = view.InjectInput = true;
            output = Path.GetFullPath("docs/verification/v06/" + mode); Directory.CreateDirectory(output);
            report = new Report(); Application.logMessageReceived += Log; shooter.Fired += Fired;
            running = true; Next();
        }
        void Next()
        {
            index++; once.Clear(); capture = 0; start = Time.time + .4f;
            scenario = mode == "preview" ? "preview" : mode == "impact-preview" ? "impacts" : scenarios[index];
            runtime.loadInterval = scenario == "hold-fast" ? .4f : scenario == "hold-slow" || scenario == "preview" ? 1.6f : .8f;
            shooter.rateMultiplier = 1; shooter.ResetSimulation(); shooter.TestFire = false;
            motor.TestMove = Vector2.zero; motor.TestRun = motor.TestJump = motor.TestJumpHeld = motor.TestSlide = false;
            motor.controller.enabled = false; motor.transform.SetPositionAndRotation(new Vector3(0, .03f, -10), Quaternion.identity);
            motor.ResetForTeleport(); motor.controller.enabled = true; pose.ResetForTeleport();
            view.TestLook = Vector2.zero; view.TestADS = false;
        }
        void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message); }
        void Fired(double elapsed, Vector3 origin, Vector3 direction)
        {
            report.shots.Add(new Shot { scenario = scenario, time = Time.time - start, interval = (float)shooter.LoadInterval, origin = origin, formed = shooter.FormedRoundPosition });
        }
        void Once(int id, float t, float at, Action action) { if (t >= at && once.Add(id)) action(); }
        void Update()
        {
            if (!running) return;
            float t = Time.time - start;
            if (t < 0) { shooter.ResetSimulation(); return; }
            shooter.TestFire = false; motor.TestMove = Vector2.zero; view.TestLook = Vector2.zero; view.TestADS = false;
            if (scenario == "preview") shooter.TestFire = t > 2.0f && t < 2.05f;
            else if (scenario.StartsWith("hold")) shooter.TestFire = true;
            else if (scenario == "faster-mid" || scenario == "slower-mid")
            {
                shooter.TestFire = true;
                if (t >= .32f) shooter.rateMultiplier = scenario == "faster-mid" ? 2 : .5f;
            }
            else if (scenario == "tap") shooter.TestFire = t > .15f && t < .17f || t > 1.2f && t < 1.22f;
            else if (scenario == "disable-reset")
            {
                shooter.TestFire = true;
                Once(0, t, .35f, () => shooter.enabled = false);
                Once(1, t, .65f, () => shooter.enabled = true);
                Once(2, t, 1.8f, () => shooter.ResetSimulation());
            }
            else if (scenario == "ads-slide")
            {
                shooter.TestFire = true; view.TestADS = t > .2f;
                view.TestLook = new Vector2(60 * Time.deltaTime / view.mouseSensitivity, 0);
                motor.TestMove = t > .3f && t < 1.5f ? Vector2.up : Vector2.zero;
                Once(0, t, .6f, () => motor.TestJump = true); Once(1, t, .8f, () => motor.TestJump = true);
                motor.TestJumpHeld = t > .8f && t < 1.6f;
                Once(2, t, 2.2f, () => motor.TestMove = Vector2.up);
                if (t > 2.1f && t < 2.5f) motor.TestMove = Vector2.up;
                Once(3, t, 2.3f, () => motor.TestSlide = true);
            }
            else if (scenario == "impacts")
            {
                Once(10, t, .1f, () =>
                {
                    dummy = FindObjectOfType<TargetDummy>(); targetBefore = dummy.Hits;
                    enemyBefore = OrbImpactPool.Current.EnemyHits; environmentBefore = OrbImpactPool.Current.EnvironmentHits;
                    LaunchReal(dummy.GetComponentInChildren<Collider>());
                    LaunchReal(GameObject.Find("TallCover").GetComponent<Collider>());
                });
                Once(11, t, .7f, () =>
                {
                    report.realEnemy = OrbImpactPool.Current.EnemyHits - enemyBefore;
                    report.realEnvironment = OrbImpactPool.Current.EnvironmentHits - environmentBefore;
                    report.realTarget = dummy.Hits - targetBefore;
                });
                Once(0, t, 1.1f, () =>
                {
                    dummy = FindObjectOfType<TargetDummy>(); targetBefore = dummy.Hits;
                    if (OrbImpactPool.Current != null) { enemyBefore = OrbImpactPool.Current.EnemyHits; environmentBefore = OrbImpactPool.Current.EnvironmentHits; }
                    Collider enemy = dummy.GetComponentInChildren<Collider>(); Collider wall = GameObject.Find("TallCover").GetComponent<Collider>();
                    for (int i = 0; i < (mode == "impact-preview" ? 2 : 40); i++)
                    {
                        bool target = i % 2 == 0;
                        Collider collider = target ? enemy : wall;
                        Vector3 point = new Vector3((target ? -1 : 1) + (i % 5) * .04f, 2.8f, -7);
                        OrbProjectile.ApplyImpact(runtime, new OrbShotContext(point, Vector3.forward, i, gameObject), new OrbImpactContext(collider, point, Vector3.back, true));
                    }
                    var pool = OrbImpactPool.Current;
                    report.enemyDelta = pool.EnemyHits - enemyBefore; report.environmentDelta = pool.EnvironmentHits - environmentBefore;
                    report.targetDelta = dummy.Hits - targetBefore; report.peak = pool.PeakActive; report.reused = pool.RecycledWhileActive;
                });
                Once(1, t, 1.14f, () => Capture("enemy-environment"));
            }
        }
        IEnumerator Start()
        {
            while (running)
            {
                yield return new WaitForEndOfFrame();
                float t = Time.time - start;
                if (t < 0) continue;
                Transform round = shooter.ReadyVisual; var pool = OrbImpactPool.Current; Ray ray = view.AimRay;
                report.frames.Add(new Frame { scenario = scenario, time = t, dt = Time.deltaTime, load = (float)shooter.Magazine.LoadProgress, interval = (float)shooter.LoadInterval,
                    loaded = shooter.Magazine.Loaded, shots = shooter.Magazine.Shots, visible = round != null && round.gameObject.activeSelf,
                    tip = shooter.hat.TipWorld, round = round.position, scale = round.localScale, axis = shooter.RoundAxis, travel = shooter.hat.Travel,
                    ads = view.ADS, state = motor.State.ToString(), grounded = motor.OnGround, injected = shooter.InjectInput,
                    rayHatDistance = Vector3.Cross(shooter.hat.TipWorld - ray.origin, ray.direction).magnitude,
                    originError = Vector3.Distance(round.position, shooter.FormedRoundPosition), active = pool != null ? pool.ActiveCount : 0, peak = pool != null ? pool.PeakActive : 0 });
                if (scenario == "preview")
                {
                    float[] times = { .16f, .72f, 1.02f, 1.30f, 1.49f, 1.58f, 1.7f, 2.02f, 2.8f, 3.3f };
                    if (capture < times.Length && t >= times[capture]) { Capture("load-" + capture); capture++; }
                }
                float duration = scenario == "preview" ? 4 : scenario == "hold-slow" ? 5.2f : 3.6f;
                if (t < duration) continue;
                if (scenario == "impacts") report.activeAfter = OrbImpactPool.Current.ActiveCount;
                File.WriteAllText(output + "/report.json", JsonUtility.ToJson(report, true));
                if (mode == "preview" || mode == "impact-preview" || index == scenarios.Length - 1) { Restore(); Debug.Log("DUCK_V06_REPORT " + output); Destroy(this); }
                else Next();
            }
        }
        void LaunchReal(Collider target)
        {
            Vector3 origin = target.bounds.center + Vector3.back * (target.bounds.extents.z + 2);
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere); orb.name = "V06ActualCollisionProjectile"; orb.layer = 2;
            var collider = orb.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            orb.transform.position = origin; orb.transform.localScale = Vector3.one * runtime.radius * 2;
            orb.GetComponent<Renderer>().sharedMaterial = shooter.orbMaterial;
            orb.AddComponent<OrbProjectile>().Launch(runtime, new OrbShotContext(origin, Vector3.forward, 0, gameObject));
        }
        void Capture(string name)
        {
            Camera camera = new GameObject("V06PreviewCamera").AddComponent<Camera>(); camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = scenario == "impacts" ? 1.8f : .95f;
            Vector3 target = scenario == "impacts" ? new Vector3(0, 2.8f, -7) : shooter.hat.PointWorld(.55f);
            camera.transform.position = target + new Vector3(2, .25f, -3); camera.transform.LookAt(target);
            var texture = new RenderTexture(960, 720, 24); var previous = RenderTexture.active; Texture2D pixels = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture; pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); pixels.Apply(); File.WriteAllBytes(output + "/" + name + ".png", pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; texture.Release(); Destroy(texture); if (pixels != null) Destroy(pixels); Destroy(camera.gameObject); }
        }
        void Restore()
        {
            if (!running) return; running = false; Application.logMessageReceived -= Log; shooter.Fired -= Fired;
            shooter.enabled = true; shooter.TestFire = false; shooter.config = original; shooter.rateMultiplier = oldRate; shooter.ResetSimulation(); shooter.enabled = oldEnabled;
            shooter.InjectInput = oldShooter; motor.InjectInput = oldMotor; view.InjectInput = oldView;
            motor.TestMove = Vector2.zero; motor.TestJump = motor.TestJumpHeld = motor.TestSlide = motor.TestRun = false;
            view.TestADS = false; view.TestLook = Vector2.zero;
            foreach (var projectile in FindObjectsOfType<OrbProjectile>()) if (!existingProjectiles.Contains(projectile.GetInstanceID())) Destroy(projectile.gameObject);
            Destroy(runtime);
        }
        void OnDestroy() { Restore(); }
    }
}
