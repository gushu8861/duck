using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using DuckGame.Maps.V08;

namespace DuckGame.Maps.V12
{
    public static class ExpeditionHubBuilder
    {
        public const string ScenePath = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";
        public const string Output = "docs/verification/v12/";
        const string Generated = "Assets/DuckGame/Maps/V12/Generated/";
        const string Vending = "Assets/DuckGame/ThirdParty/ValentinLaffitteVending/";
        const string Gate = "Assets/DuckGame/ThirdParty/KayKitHalloweenArch/";

        [MenuItem("Duck Game/V12/Build Expedition Hub")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("V12 already exists. Preserve it before rebuilding.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before building.");
            Directory.CreateDirectory(Generated);
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            foreach (string path in new[] { Vending + "VendingMachine.fbx", Gate + "arch.obj" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.importCameras = importer.importLights = importer.addCollider = false;
                importer.isReadable = true;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.SaveAndReimport();
            }
            if (!AssetDatabase.CopyAsset("Assets/DuckGame/Maps/V11/Generated/StyleGrove.unity", ScenePath))
                throw new IOException("Could not copy V11.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureEnvironment();
            var trade = UnityEngine.Object.FindObjectOfType<MerchantTrade>();
            var controller = new GameObject("V12 营地设施").AddComponent<HubFacilitiesController>();
            controller.trade = trade;

            var machine = Model(Vending + "VendingMachine.fbx", "魔法售货机 · Valentin Laffitte CC0", 4.4f);
            var machineBounds = BoundsOf(machine);
            var keypad = machine.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Cube.004");
            Vector3 side = keypad != null ? keypad.bounds.center - machineBounds.center : Vector3.right;
            Vector3 front = machineBounds.size.x < machineBounds.size.z ?
                Vector3.right * Mathf.Sign(side.x) : Vector3.forward * Mathf.Sign(side.z);
            float depth = machineBounds.size.x < machineBounds.size.z ? machineBounds.extents.x : machineBounds.extents.z;
            var collider = machine.AddComponent<BoxCollider>();
            collider.center = machineBounds.center;
            collider.size = machineBounds.size;
            controller.vendingAnchor = Anchor(machine.transform, machineBounds.center + front * (depth + .25f), "售货交互点");
            controller.vendingAnchor.position = new Vector3(controller.vendingAnchor.position.x, 1.8f, controller.vendingAnchor.position.z);
            machine.transform.rotation = Quaternion.FromToRotation(front, new Vector3(.48f, 0, -1).normalized);
            Place(machine, new Vector3(-5.3f, 0, -7.6f));

            var gate = Model(Gate + "arch.obj", "出发门 · KayKit CC0", 5.3f);
            var gateMaterial = Material("古石拱 · 原始图集", new Color(.88f, .96f, .86f),
                AssetDatabase.LoadAssetAtPath<Texture2D>(Gate + "halloweenbits_texture.png"));
            gateMaterial.EnableKeyword("_EMISSION");
            foreach (var filter in gate.GetComponentsInChildren<MeshFilter>())
            {
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                filter.GetComponent<Renderer>().sharedMaterial = gateMaterial;
            }
            controller.gateRenderer = gate.GetComponentInChildren<Renderer>();
            gate.transform.rotation = Quaternion.Euler(0, -18, 0);
            Place(gate, new Vector3(4.4f, 0, -7.0f));
            controller.departureAnchor = Anchor(gate.transform, new Vector3(0, 1.8f, -.9f), "出发交互点");
            var tradeSettings = new SerializedObject(trade);
            tradeSettings.FindProperty("externalPresentation").boolValue = true;
            tradeSettings.FindProperty("externalAnchor").objectReferenceValue = controller.vendingAnchor;
            tradeSettings.ApplyModifiedPropertiesWithoutUndo();

            var mapped = new System.Collections.Generic.Dictionary<Material, Material>();
            string[] canMaterials = { "Material.001", "Material.002", "Material.020", "Material.017", "Material.019" };
            foreach (var renderer in machine.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    if (mapped.TryGetValue(source, out var existing)) return existing;
                    int label = Array.IndexOf(canMaterials, source.name);
                    Color color = new Color(.17f, .23f, .27f);
                    if (source.name == "Material") color = new Color(.39f, .33f, .58f);
                    if (source.name == "Material.013") color = new Color(.74f, .56f, .24f);
                    if (source.name == "Material.005" || source.name == "Material.006") color = new Color(.25f, .56f, .50f);
                    if (source.name == "Material.012") color = new Color(.87f, .79f, .55f);
                    if (source.name == "Material.022" || source.name == "Material.023") color = new Color(.23f, .32f, .31f);
                    Texture2D texture = label >= 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(Vending + "CanLabel" + label + ".png") : null;
                    var material = Material("售货机 " + source.name, label >= 0 ? Color.white : color, texture);
                    if (source.name == "Material.022" || source.name == "Material.023") Glass(material);
                    mapped.Add(source, material);
                    return material;
                }).ToArray();
            }
            Glow(gate.transform, new Vector3(0, 3.1f, 0), 1.35f, 10);
            Glow(machine.transform, machine.transform.InverseTransformPoint(controller.vendingAnchor.position + Vector3.up * 1.15f), .3f, 3);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("V12 scene save failed.");
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "bounds.txt", Report(machine) + "\n" + Report(gate) +
                "\nMachine approach=" + controller.vendingAnchor.position + "\nGate approach=" + controller.departureAnchor.position);
            Debug.Log("V12_BUILD_OK " + ScenePath);
        }

        public static void FinalizeVisuals()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ConfigureEnvironment();
            var hub = UnityEngine.Object.FindObjectOfType<HubFacilitiesController>();
            hub.departureAnchor.parent.position = new Vector3(4.4f, 0, -7.0f);
            foreach (string name in new[] { "Material.022", "Material.023" })
                Glass(AssetDatabase.LoadAssetAtPath<Material>(Generated + "售货机 " + name + ".mat"));
            AssetDatabase.LoadAssetAtPath<Material>(Generated + "售货机 Material.mat")
                .SetColor("_BaseColor", new Color(.39f, .33f, .58f));
            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
                if (renderer.sharedMaterials.All(m => m != null && m.renderQueue == 3000))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "bounds.txt", Report(hub.vendingAnchor.parent.gameObject) + "\n" + Report(hub.departureAnchor.parent.gameObject) + "\nMachine approach=" + hub.vendingAnchor.position + "\nGate approach=" + hub.departureAnchor.position);
            Debug.Log("V12_VISUALS_OK");
        }

        static void ConfigureEnvironment()
        {
            // Constant scene-local ambient avoids the asynchronous sky-probe warmup in a new scene.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .47f, .52f);
        }
        static void Glass(Material material)
        {
            material.SetColor("_BaseColor", new Color(.52f, .82f, .74f, .055f));
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Smoothness", .12f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
        }
        static GameObject Model(string path, string name, float height)
        {
            var root = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            model.transform.SetParent(root.transform, false);
            model.transform.localScale *= height / BoundsOf(root).size.y;
            // The imported geometry remains unchanged. Only its instance scale is adapted.
            return root;
        }

        static Transform Anchor(Transform parent, Vector3 position, string name)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = position;
            return point;
        }

        static void Place(GameObject root, Vector3 position)
        {
            var foundation = GameObject.Find("Solid meadow foundation").GetComponent<Renderer>();
            float surface = foundation.bounds.max.y;
            root.transform.position = position;
            var bounds = BoundsOf(root);
            root.transform.position += Vector3.up * (surface - bounds.min.y);
        }

        public static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static string Report(GameObject root)
        {
            var bounds = BoundsOf(root);
            return root.name + " | world size=" + bounds.size + " | bottom=" + bounds.min.y.ToString("F4") +
                " | transform=" + root.transform.position + " | scale=" + root.transform.localScale;
        }

        static Material Material(string name, Color color, Texture texture = null)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .14f);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(material, Generated + name + ".mat");
            return material;
        }

        static void Glow(Transform parent, Vector3 position, float radius, float rate)
        {
            var go = new GameObject("微光 · 附加特效");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop();
            var main = particles.main;
            main.startLifetime = 1.8f;
            main.startSpeed = .15f;
            main.startSize = .045f;
            main.startColor = new Color(.42f, .90f, .76f, .60f);
            main.maxParticles = 40;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = particles.emission;
            emission.rateOverTime = rate;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetFloat("_Surface", 1);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0);
            material.renderQueue = 3000;
            string matPath = Generated + "微光材质.mat";
            if (File.Exists(matPath))
            {
                UnityEngine.Object.DestroyImmediate(material);
                material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            }
            else AssetDatabase.CreateAsset(material, matPath);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            particles.Play();
        }
    }
}





