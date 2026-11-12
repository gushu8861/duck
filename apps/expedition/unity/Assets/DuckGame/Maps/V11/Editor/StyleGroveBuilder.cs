using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using DuckGame.Maps.V08;
using DuckGame.Maps.V10;

namespace DuckGame.Maps.V11
{
    public static class StyleGroveBuilder
    {
        public const string Baseline = "Assets/DuckGame/Maps/V10/Generated/EnemyGrove.unity";
        public const string Folder = "Assets/DuckGame/Maps/V11/Generated/";
        public const string ScenePath = Folder + "StyleGrove.unity";

        [MenuItem("Duck Game/V11/Build Style Grove")]
        public static void Build()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Style Grove already exists.");

            var active = SceneManager.GetActiveScene();
            if (active.isDirty)
                throw new InvalidOperationException("The active scene has unsaved edits. Save or keep them before building Style Grove.");

            var surface = RequiredShader("Duck Game/V11/Layered Toon");
            var ink = RequiredShader("Duck Game/V11/Ink Outline");
            var effect = RequiredShader("Hidden/Duck Game/V11/Camera Style");
            if (!AssetDatabase.CopyAsset(Baseline, ScenePath))
                throw new IOException("Could not copy Enemy Grove.");

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var trade = UnityEngine.Object.FindObjectOfType<MerchantTrade>();
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in trade.motor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var source = materials[slot];
                    bool isOutline = source.shader.name == "Duck Game/V10/Fade Pixel Outline";
                    if (!isOutline && source.shader.name != "Duck Game/V10/Fade Toon")
                        continue;

                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = new Material(source) { shader = isOutline ? ink : surface };
                        if (isOutline)
                        {
                            var width = source.GetFloat("_WidthPixels");
                            copy.SetFloat("_WidthPixels", width < .8f ? 1.08f : 1.7f);
                            copy.SetColor("_Ink", new Color(.012f, .013f, .017f, 1));
                        }
                        AssetDatabase.CreateAsset(copy, Folder + source.name + ".mat");
                        copies.Add(source, copy);
                    }
                    materials[slot] = copy;
                }
                renderer.sharedMaterials = materials;
            }

            AddEnemyInk(ink);
            var cameraStyle = trade.view.lens.gameObject.AddComponent<CameraStyle>();
            var style = new SerializedObject(cameraStyle);
            style.FindProperty("upwardFade").objectReferenceValue = trade.motor.GetComponent<UpwardPlayerVisibility>();
            style.ApplyModifiedPropertiesWithoutUndo();
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Settings" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                InstallFeature(renderer, effect);
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"V11 Style Grove saved with {copies.Count} distinct player material assets.");
        }

        private static void AddEnemyInk(Shader shader)
        {
            var ink = new Material(shader) { name = "Enemy silhouette ink" };
            ink.SetFloat("_WidthPixels", 1.22f);
            ink.SetColor("_Ink", new Color(.015f, .019f, .021f, 1));
            AssetDatabase.CreateAsset(ink, Folder + "Enemy silhouette ink.mat");

            foreach (var enemy in UnityEngine.Object.FindObjectsOfType<ForestEnemy>())
            {
                foreach (var shape in enemy.body.GetComponentsInChildren<MeshFilter>())
                {
                    if (shape.name != "Pear mushroom body" && shape.name != "Rounded wood trunk" &&
                        shape.name != "Moss shoulder crown" && shape.name != "Soft broad mushroom cap")
                        continue;

                    var edge = new GameObject(shape.name + " ink", typeof(MeshFilter), typeof(MeshRenderer));
                    edge.layer = shape.gameObject.layer;
                    edge.transform.SetParent(shape.transform, false);
                    edge.GetComponent<MeshFilter>().sharedMesh = shape.sharedMesh;
                    var renderer = edge.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = ink;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }
        private static Shader RequiredShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException($"Shader unavailable: {name}");
            return shader;
        }

        private static void InstallFeature(UniversalRendererData renderer, Shader effect)
        {
            foreach (var feature in renderer.rendererFeatures)
                if (feature is CameraStyleFeature)
                    return;

            var instance = ScriptableObject.CreateInstance<CameraStyleFeature>();
            instance.name = "V11 Camera Style";
            var featureObject = new SerializedObject(instance);
            featureObject.FindProperty("shader").objectReferenceValue = effect;
            featureObject.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(instance, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(instance, out _, out long fileId);

            var data = new SerializedObject(renderer);
            var features = data.FindProperty("m_RendererFeatures");
            var map = data.FindProperty("m_RendererFeatureMap");
            var index = features.arraySize;
            features.arraySize = index + 1;
            features.GetArrayElementAtIndex(index).objectReferenceValue = instance;
            map.arraySize = index + 1;
            map.GetArrayElementAtIndex(index).longValue = fileId;
            data.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
        }
    }
}
