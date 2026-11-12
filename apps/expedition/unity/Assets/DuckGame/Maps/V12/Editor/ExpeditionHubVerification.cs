using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DuckGame.Maps.V08;

namespace DuckGame.Maps.V12
{
    [InitializeOnLoad]
    public static class ExpeditionHubVerification
    {
        const string Pending = "Duck.V12.Verify";
        static ExpeditionHubVerification()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                {
                    SessionState.SetBool(Pending, false);
                    var runner = new GameObject("V12 verification").AddComponent<HubVerificationProbe>();
                    UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);
                    runner.Run();
                }
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Pending + ".done", false))
                {
                    SessionState.SetBool(Pending + ".done", false);
                    EditorApplication.Exit(SessionState.GetInt(Pending + ".exit", 1));
                }
            };
        }

        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene(ExpeditionHubBuilder.ScenePath);
            EditorApplication.isPlaying = true;
        }

        public static void Finish(bool passed)
        {
            SessionState.SetInt(Pending + ".exit", passed ? 0 : 1);
            SessionState.SetBool(Pending + ".done", true);
            EditorApplication.isPlaying = false;
        }
    }

    public sealed class HubVerificationProbe : MonoBehaviour
    {
        [Serializable] public class Result
        {
            public string scene = ExpeditionHubBuilder.ScenePath;
            public string unity;
            public string time;
            public List<string> passed = new List<string>();
            public List<string> errors = new List<string>();
            public string flight = "本轮为装配准备，试飞检查不适用。";
        }
        readonly Result result = new Result();
        const string Output = ExpeditionHubBuilder.Output;
        HubFacilitiesController hub;
        MerchantTrade trade;

        public void Run() { StartCoroutine(Guard()); }

        IEnumerator Guard()
        {
            result.unity = Application.unityVersion;
            result.time = DateTime.Now.ToString("O");
            Application.logMessageReceived += Log;
            var checks = Checks();
            while (true)
            {
                object current;
                try { if (!checks.MoveNext()) break; current = checks.Current; }
                catch (Exception exception) { result.errors.Add(exception.ToString()); break; }
                yield return current;
            }
            Application.logMessageReceived -= Log;
            File.WriteAllText(Output + "report.json", JsonUtility.ToJson(result, true));
            Debug.Log("V12_VERIFY_" + (result.errors.Count == 0 ? "OK" : "FAILED") +
                " groups=" + result.passed.Count + " errors=" + result.errors.Count);
            ExpeditionHubVerification.Finish(result.errors.Count == 0);
        }

        void Log(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                result.errors.Add(message + "\n" + trace);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        void Passed(string message) { result.passed.Add(message); }

        IEnumerator Checks()
        {
            for (int i = 0; i < 12; i++) yield return null;
            Debug.Log("ENVIRONMENT " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " mode=" + RenderSettings.ambientMode + " SH0=" + RenderSettings.ambientProbe[0, 0] + "," + RenderSettings.ambientProbe[1, 0] + "," + RenderSettings.ambientProbe[2, 0]);
            hub = FindObjectOfType<HubFacilitiesController>();
            trade = hub.trade;
            trade.motor.InjectInput = trade.shooter.InjectInput = trade.view.InjectInput = true;
            Require(FindObjectsOfType<MerchantTrade>().Length == 1 && FindObjectsOfType<MerchantShopUI>().Length == 0,
                "V12 must have one budget and only the external UI.");
            var roots = new[] { hub.vendingAnchor.parent.gameObject, hub.departureAnchor.parent.gameObject };
            foreach (var root in roots)
            {
                var bounds = ExpeditionHubBuilder.BoundsOf(root);
                Require(Mathf.Abs(bounds.min.y) < .01f, root.name + " is not grounded.");
                Require(root.GetComponentsInChildren<MeshFilter>().All(f =>
                    AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/DuckGame/ThirdParty/")), "Facility mesh provenance.");
                Require(root.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).All(r =>
                    r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported &&
                    m.shader.name.StartsWith("Universal Render Pipeline/"))), "Facility URP material validation.");
            }
            var door = hub.departureAnchor.parent;
            Vector3 approach = door.position - door.forward * 2 + Vector3.up * .04f;
            Require(!Physics.CapsuleCast(approach + Vector3.up * trade.motor.controller.radius,
                approach + Vector3.up * (trade.motor.controller.height - trade.motor.controller.radius),
                trade.motor.controller.radius, door.forward, 4, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore), "Downloaded gate opening must pass the player capsule.");
            Passed("1 下载网格引用、贴地、URP材质、单一钱包与界面");
            Photograph(trade.view.lens, "hub-1080.png", 1920, 1080);
            Portrait(roots[0], "vending-close.png");
            Portrait(roots[1], "departure-close.png");

            Aim(hub.departureAnchor, 2.4f);
            var initialLock = Cursor.lockState;
            bool initialVisible = Cursor.visible;
            Require(hub.OpenDeparture() && !hub.OpenShop(), "Departure panel must be exclusive.");
            int gold = trade.Gold;
            Require(hub.TogglePreparation() && hub.Prepared && trade.Gold == gold, "Prepare must retain budget.");
            hub.TogglePreparation();
            Require(!hub.Prepared, "Preparation cancellation.");
            hub.TogglePreparation();
            Photograph(trade.view.lens, "loadout-1080.png", 1920, 1080);
            hub.ClosePanel();
            for (int i = 0; i < 4; i++) yield return null;
            Require(!hub.Releasing && trade.motor.enabled && trade.shooter.enabled && trade.view.enabled &&
                Time.timeScale == 1 && Cursor.lockState == initialLock && Cursor.visible == initialVisible, "Departure input restoration.");
            Passed("2 真实装配确认、取消、菜单互斥与预算保持");

            Aim(hub.vendingAnchor, 2.4f);
            Require(hub.CanOpenShop(), "Front approach must reach vending machine.");
            var camera = trade.view.transform;
            Quaternion forward = camera.rotation;
            camera.rotation = forward * Quaternion.Euler(0, 180, 0);
            Require(!hub.CanOpenShop(), "Back-facing interaction.");
            camera.rotation = forward;
            Vector3 position = trade.motor.transform.position;
            Teleport(position - camera.forward * 8);
            Require(!hub.CanOpenShop(), "Far interaction.");
            Teleport(position);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Temporary verification occluder";
            wall.transform.position = (hub.vendingAnchor.position + position + Vector3.up * 1.8f) * .5f;
            wall.transform.localScale = new Vector3(2, 4, .4f);
            Physics.SyncTransforms();
            Require(!hub.CanOpenShop(), "Occluded interaction.");
            Destroy(wall);
            yield return null;
            Aim(hub.vendingAnchor, 2.4f);
            Require(hub.OpenShop() && !hub.OpenDeparture() && !hub.OpenShop(), "Shop opening must be exclusive.");
            Passed("3 近距、朝向、遮挡与重复打开限制");

            float rate = trade.shooter.rateMultiplier, speed = trade.shooter.config.speed;
            int count = trade.shooter.config.projectileCount;
            Require(hub.Purchase(0) && trade.Gold == 75 && Mathf.Approximately(trade.shooter.rateMultiplier, rate * 1.2f),
                "Rate purchase.");
            Require(!hub.Prepared, "Changed loadout must invalidate preparation.");
            Require(hub.Purchase(1) && trade.Gold == 50 && Mathf.Approximately(trade.shooter.config.speed, speed * 1.25f),
                "Speed purchase.");
            Require(hub.Purchase(2) && trade.Gold == 10 && trade.shooter.config.projectileCount == count + 1,
                "Projectile purchase.");
            Require(!hub.Purchase(2) && trade.Gold == 10 && !hub.Purchase(-1) && !hub.Purchase(3),
                "Insufficient budget and invalid product.");
            Photograph(trade.view.lens, "shop-1080.png", 1920, 1080);
            Photograph(trade.view.lens, "shop-720.png", 1280, 720);
            hub.ClosePanel();
            Require(!trade.TryPurchase(0) && !hub.OpenShop(), "Close-frame input guard.");
            for (int i = 0; i < 4; i++) yield return null;
            Require(trade.motor.enabled && trade.shooter.enabled && trade.view.enabled && Time.timeScale == 1,
                "Shop control restoration.");
            Require(hub.OpenShop() && trade.Gold == 10, "Reopening must retain budget.");
            hub.ClosePanel();
            for (int i = 0; i < 4; i++) yield return null;
            Passed("4 三商品真实效果、100金币预算、失败购买、准备失效与重开保持");
            Passed("5 关闭延迟、移动、射击、相机与时间恢复");

            // Firing must resume after the shop closes. Count the ACTUAL projectile owner:
            // once PooledOrbBridge is installed the legacy OrbProjectile path is bypassed, so
            // OrbProjectile.SpawnCount stays at zero and asserting on it alone fails on a
            // correct build. Accept either owner, but require that one of them advanced.
            int shots = OrbProjectile.SpawnCount;
            var orbPool = UnityEngine.Object.FindObjectOfType<DuckGame.TS.ProjectilePool>();
            var orbBridge = UnityEngine.Object.FindObjectOfType<DuckGame.TS.PooledOrbBridge>();
            int pooledShots = orbPool != null ? orbPool.Spawned : 0;
            int routedShots = orbBridge != null ? orbBridge.RoutedShots : 0;

            trade.shooter.TestFire = true;
            yield return new WaitForSecondsRealtime(1.3f);
            trade.shooter.TestFire = false;

            bool legacyFired = OrbProjectile.SpawnCount > shots;
            bool pooledFired = orbPool != null && orbPool.Spawned > pooledShots;
            bool routedFired = orbBridge != null && orbBridge.RoutedShots > routedShots;
            Require(legacyFired || pooledFired || routedFired,
                "Shooting must resume after closing UI. (legacy=" + (OrbProjectile.SpawnCount - shots) +
                " pooled=" + (orbPool != null ? orbPool.Spawned - pooledShots : -1) +
                " routed=" + (orbBridge != null ? orbBridge.RoutedShots - routedShots : -1) + ")");
            Passed("6 关闭后实际法球发射恢复（池化路径 " + (pooledFired ? "生效" : "未启用") + "）");

            Aim(hub.departureAnchor, 2.4f);
            Require(hub.OpenDeparture(), "Open before disable.");
            hub.enabled = false;
            Require(trade.motor.enabled && trade.shooter.enabled && trade.view.enabled && Time.timeScale == 1,
                "Controller disable must restore departure controls.");
            hub.enabled = true;
            Require(!hub.GetComponentInChildren<Canvas>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(), "Re-enable must hide modal cards.");
            Aim(hub.vendingAnchor, 2.4f);
            Require(hub.OpenShop(), "Open shop after re-enable.");
            Destroy(hub);
            yield return null;
            Require(!trade.IsOpen && !trade.Restoring && trade.motor.enabled && trade.shooter.enabled &&
                trade.view.enabled && Time.timeScale == 1, "Controller destroy must restore shop controls.");
            Passed("7 控制器禁用、重启与销毁的恢复");

            EditorSceneManager.LoadSceneInPlayMode(ExpeditionHubBuilder.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 8; i++) yield return null;
            Debug.Log("ENVIRONMENT " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + " mode=" + RenderSettings.ambientMode + " SH0=" + RenderSettings.ambientProbe[0, 0] + "," + RenderSettings.ambientProbe[1, 0] + "," + RenderSettings.ambientProbe[2, 0]);
            hub = FindObjectOfType<HubFacilitiesController>();
            trade = hub.trade;
            trade.motor.InjectInput = trade.shooter.InjectInput = trade.view.InjectInput = true;
            Aim(hub.vendingAnchor, 2.4f);
            Require(hub.OpenShop(), "Fresh play cap session.");
            for (int i = 0; i < 3; i++) Require(hub.Purchase(0), "First three rate upgrades.");
            Require(trade.Gold == 25 && trade.Bought[0] == 3 && !hub.Purchase(0) && trade.Gold == 25, "Rate cap.");
            trade.shooter.config.projectileCount = 8;
            Require(!hub.Purchase(2) && trade.Gold == 25 && trade.shooter.config.projectileCount == 8 && trade.Message.Contains("上限"), "Eight-projectile cap.");
            hub.ClosePanel();
            Passed("8 独立新次游玩的三次商品上限与受控8颗上限");

            EditorSceneManager.LoadSceneInPlayMode("Assets/DuckGame/Maps/V11/Generated/StyleGrove.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 8; i++) yield return null;
            Debug.Log("ENVIRONMENT V11 mode=" + RenderSettings.ambientMode + " SH0=" + RenderSettings.ambientProbe[0, 0] + "," + RenderSettings.ambientProbe[1, 0] + "," + RenderSettings.ambientProbe[2, 0]);
            trade = FindObjectOfType<MerchantTrade>();
            trade.motor.InjectInput = trade.shooter.InjectInput = trade.view.InjectInput = true;
            Aim(trade.interactionAnchor, 2.4f);
            Require(FindObjectsOfType<MerchantShopUI>().Length == 1 && trade.CanInteract() && trade.Open(),
                "Legacy shop default path.");
            Require(trade.TryPurchase(0) && trade.Gold == 75, "Legacy purchase.");
            trade.Close();
            for (int i = 0; i < 4; i++) yield return null;
            Require(trade.motor.enabled && trade.shooter.enabled && trade.view.enabled && Time.timeScale == 1,
                "Legacy input restoration.");
            Passed("9 V11默认商店入口、原UI、购买与恢复回归");
            Passed("10 实际Play设施、购物与装配截图，1920×1080和1280×720");
        }

        void Teleport(Vector3 position)
        {
            trade.motor.controller.enabled = false;
            trade.motor.transform.position = position;
            trade.motor.ResetForTeleport();
            trade.motor.controller.enabled = true;
            Physics.SyncTransforms();
        }

        void Aim(Transform anchor, float distance)
        {
            Vector3 outside = anchor.position - anchor.parent.position;
            outside.y = 0;
            outside.Normalize();
            if (outside.sqrMagnitude < .1f) outside = -Vector3.forward;
            Vector3 position = anchor.position + outside * distance;
            position.y = .04f;
            Teleport(position);
            Vector3 direction = anchor.position - (position + Vector3.up * 1.8f);
            Quaternion rotation = Quaternion.LookRotation(direction);
            trade.motor.transform.rotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            trade.view.transform.rotation = rotation;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ShoulderCamera).GetField("desiredYaw", flags).SetValue(trade.view, rotation.eulerAngles.y);
            typeof(ShoulderCamera).GetField("desiredPitch", flags).SetValue(trade.view, Mathf.DeltaAngle(0, rotation.eulerAngles.x));
            typeof(ShoulderCamera).GetField("<Yaw>k__BackingField", flags).SetValue(trade.view, rotation.eulerAngles.y);
        }

        void Portrait(GameObject facility, string file)
        {
            var camera = trade.view.lens;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            var bounds = ExpeditionHubBuilder.BoundsOf(facility);
            Vector3 forward = facility == hub.vendingAnchor.parent.gameObject ?
                (hub.vendingAnchor.position - bounds.center) : -facility.transform.forward;
            forward.y = 0;
            camera.transform.position = bounds.center + forward.normalized * 9 + Vector3.up * 1.2f;
            camera.transform.LookAt(bounds.center);
            Photograph(camera, file, 1400, 1000, false);
            camera.transform.SetPositionAndRotation(position, rotation);
        }

        static void Photograph(Camera camera, string file, int width, int height, bool includeUi = true)
        {
            var canvases = FindObjectsOfType<Canvas>();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var oldCameras = canvases.Select(c => c.worldCamera).ToArray();
            var enabled = canvases.Select(c => c.enabled).ToArray();
            var rt = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            var target = camera.targetTexture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].enabled &= includeUi;
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = camera;
                    canvases[i].planeDistance = .3f;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + file, image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < canvases.Length; i++)
                {
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = oldCameras[i];
                    canvases[i].enabled = enabled[i];
                }
                camera.targetTexture = target;
                RenderTexture.active = previous;
                Destroy(image);
                rt.Release();
                Destroy(rt);
            }
        }
    }
}




