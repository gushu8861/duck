using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DuckGame.V04.EditorTools
{
    public static class MovementUpgradeV04
    {
        const string Root = "Assets/DuckGame/V04";
        public const string VerifyFlag = "DuckGame.V04.Verify";
        public static string LatestScene
        {
            get
            {
                string marker = "docs/design/v04-latest-scene.txt";
                return File.Exists(marker) ? File.ReadAllText(marker).Trim() : null;
            }
        }

        [MenuItem("Duck Game/Upgrade v0.4 Movement")]
        public static void Upgrade()
        {
            if (EditorApplication.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                {
                    EditorUtility.DisplayDialog("Scene preserved", "Save the current scene first. No scene was discarded.", "OK");
                    return;
                }
            }
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene original = SceneManager.GetActiveScene();
            string source = original.path;
            ValidateSource(original);
            Shader outlineShader = Shader.Find("Duck Game/V04 Pixel Outline URP");
            if (outlineShader == null || ShaderUtil.ShaderHasError(outlineShader))
                throw new Exception("V04 outline shader is unavailable or has errors; no scene changed.");
            string folder = AssetDatabase.GenerateUniqueAssetPath(Root + "/Generated");
            AssetDatabase.CreateFolder(Root, Path.GetFileName(folder));
            string path = folder + "/MovementPlayground.unity";
            if (!AssetDatabase.CopyAsset(source, path)) throw new Exception("Could not copy current saved scene");
            try
            {
                Scene copy = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                GooseMotor legacy = Components<GooseMotor>(copy).Single();
                if (legacy == null) throw new Exception("v03 motor missing");
                GameObject player = legacy.gameObject;
                legacy.enabled = false;
                OrbShooter oldShooter = player.GetComponent<OrbShooter>();
                GameObject weapon = new GameObject("V04WeaponAnchor");
                weapon.layer = 2;
                weapon.transform.SetParent(player.transform, false);
                OrbShooter shooter = weapon.AddComponent<OrbShooter>();
                shooter.config = oldShooter.config;
                shooter.hat = oldShooter.hat;
                shooter.view = oldShooter.view;
                shooter.orbMaterial = oldShooter.orbMaterial;
                shooter.rateMultiplier = oldShooter.rateMultiplier;
                UnityEngine.Object.DestroyImmediate(oldShooter);
                foreach (RuntimeVerification oldProbe in Components<RuntimeVerification>(copy))
                    UnityEngine.Object.DestroyImmediate(oldProbe);

                GameObject proxy = new GameObject("V04CameraAnchor");
                proxy.layer = 2;
                proxy.transform.SetParent(player.transform, false);
                legacy.view.player = proxy.transform;
                var motor = player.AddComponent<GooseMotorV04>();
                motor.controller = legacy.controller;
                motor.animator = legacy.animator;
                motor.view = legacy.view;
                motor.walkSpeed = legacy.walkSpeed;
                motor.runSpeed = legacy.runSpeed;
                if (motor.controller.height < 3.5f)
                {
                    motor.controller.height = 3.5f;
                    motor.controller.center = Vector3.up * 1.75f;
                }
                var pose = player.AddComponent<GoosePoseV04>();
                pose.motor = motor;
                pose.animator = motor.animator;
                pose.cameraProxy = proxy.transform;
                pose.weaponProxy = weapon.transform;
                var probe = player.transform.root.gameObject.AddComponent<MovementVerificationV04>();
                probe.motor = motor;
                probe.pose = pose;
                probe.view = motor.view;
                probe.shooter = shooter;
                probe.enabled = false;

                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(.25f, .36f, .39f);
                AssetDatabase.CreateAsset(material, folder + "/MovementTestGeometry.mat");
                Box("V04LowRoof", new Vector3(-10, 1.70f, 2), new Vector3(3, .30f, 5), material);
                Box("V04TunnelEndWall", new Vector3(-10, 1, 4.7f), new Vector3(3, 2, .3f), material);
                Box("V04JumpCeiling", new Vector3(10, 4.1f, 0), new Vector3(5, .20f, 5), material);
                ApplyOutline(player, outlineShader, folder);
                PrefabUtility.SaveAsPrefabAsset(player.transform.root.gameObject, folder + "/PlayerRigV04.prefab");
                if (!EditorSceneManager.SaveScene(copy, path)) throw new Exception("Could not save V04 scene.");
                Directory.CreateDirectory("docs/design");
                File.WriteAllText("docs/design/v04-latest-scene.txt", path);
                Debug.Log("DUCK_V04_UPGRADE_OK " + path + " SOURCE " + source);
            }
            catch (Exception exception)
            {
                // Keep partial output for diagnosis, but never advertise it as the latest scene.
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                Debug.LogError("V04 upgrade failed; original scene restored. Partial output: " + path);
                Debug.LogException(exception);
                throw;
            }
        }

        [MenuItem("Duck Game/Play Movement Verification v0.4")]
        public static void Verify()
        {
            if (EditorApplication.isPlaying) return;
            if (SceneManager.GetActiveScene().isDirty)
            {
                EditorUtility.DisplayDialog("Scene preserved", "Save the current scene before verification.", "OK");
                return;
            }
            string path = LatestScene;
            if (string.IsNullOrEmpty(path)) throw new Exception("Upgrade v0.4 first");
            SessionState.SetString("DuckGame.V04.PreviousPlayScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            SessionState.SetBool(VerifyFlag, true);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            EditorApplication.isPlaying = true;
        }


        static T[] Components<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        static void ValidateSource(Scene scene)
        {
            if (Components<GooseMotorV04>(scene).Length != 0 || Components<GoosePoseV04>(scene).Length != 0 ||
                Components<MovementVerificationV04>(scene).Length != 0)
                throw new Exception("This scene already has V04 components. Open the saved source scene to upgrade again.");
            GooseMotor[] motors = Components<GooseMotor>(scene);
            if (motors.Length != 1) throw new Exception("Expected exactly one legacy GooseMotor in the active scene.");
            GooseMotor motor = motors[0];
            OrbShooter shooter = motor.GetComponent<OrbShooter>();
            if (!motor.enabled || motor.controller == null || motor.animator == null || motor.view == null ||
                shooter == null || !shooter.enabled || shooter.config == null || shooter.hat == null ||
                shooter.view != motor.view || shooter.orbMaterial == null)
                throw new Exception("Player movement/shooting references are incomplete; source scene preserved.");
            string[] required = { "Body", "Neck1", "Neck2", "Neck3", "Head", "HatTip", "Scarf.L", "Scarf.R", "Tail", "Wing.L", "Wing.R", "Leg.L", "Leg.R", "Foot.L", "Foot.R" };
            var names = motor.animator.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            if (required.Any(name => !names.Contains(name))) throw new Exception("Required procedural pose bones are missing.");
            HatBulge hat = shooter.hat;
            if (hat.hat == null || hat.outline == null || hat.hat.sharedMesh == null ||
                hat.head == null || hat.tipBone == null || hat.centerLine == null || hat.centerLine.Length < 2)
                throw new Exception("Hat deformation/outline links are incomplete.");
        }

        static void ApplyOutline(GameObject player, Shader shader, string folder)
        {
            var material = new Material(shader) { name = "V04ThinInk" };
            material.SetFloat("_WidthPixels", 1.15f);
            material.SetColor("_Ink", new Color(.026f, .022f, .043f, 1));
            AssetDatabase.CreateAsset(material, folder + "/ThinOutlineV04.mat");
            int count = 0;
            foreach (var renderer in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Reuse the existing skinned shells: HatBulge shares its runtime mesh with this renderer.
                if (!renderer.sharedMaterials.Any(m => m != null && m.shader != null &&
                    (m.shader.name == "Duck Game/Outline URP" || m.shader == shader))) continue;
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMesh.subMeshCount).ToArray();
                count++;
            }
            if (count == 0) throw new Exception("No existing skinned outline shells found.");
        }

        static void Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
