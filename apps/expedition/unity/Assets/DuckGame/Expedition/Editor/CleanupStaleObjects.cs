using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DuckGame.Expedition.EditorTools
{
    /// <summary>
    /// Removes leftover objects from earlier passes of this system.
    ///
    /// The mirror text on the preview boards came from the OLD TextMesh-based sign: when the
    /// sign was rewritten to use a world-space Canvas, the previously saved "SignText" child
    /// remained in the scene and kept rendering on top of the new board. A bake also leaves
    /// stale light/prop objects behind, so this sweeps everything this system owns.
    /// </summary>
    public static class CleanupStaleObjects
    {
        const string HubScene = "Assets/DuckGame/Maps/V12/Generated/ExpeditionHub.unity";

        [MenuItem("Duck Game/Expedition/Cleanup Stale Objects")]
        public static void Run()
        {
            var sb = new System.Text.StringBuilder();
            var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);

            int removedSignText = 0;
            int removedOther = 0;

            // 1. Legacy TextMesh signs
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == "SignText")
                    {
                        UnityEngine.Object.DestroyImmediate(t.gameObject);
                        removedSignText++;
                    }
                }
            }
            sb.AppendLine("removed legacy SignText = " + removedSignText);

            // 2. Duplicate pad visuals (PadRing / PadColumn are rebuilt every install)
            var padRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Expedition_Pads");
            if (padRoot != null)
            {
                foreach (var pad in padRoot.GetComponentsInChildren<SpringPad>(true))
                {
                    for (int i = pad.transform.childCount - 1; i >= 0; i--)
                    {
                        var child = pad.transform.GetChild(i);
                        if (child.name == "PadRing" || child.name == "PadColumn")
                        {
                            UnityEngine.Object.DestroyImmediate(child.gameObject);
                            removedOther++;
                        }
                    }
                }
            }
            sb.AppendLine("removed duplicate pad visuals = " + removedOther);

            // 3. Duplicate post-process volumes
            var vols = UnityEngine.Object.FindObjectsOfType<UnityEngine.Rendering.Volume>();
            int keptVol = 0;
            foreach (var v in vols)
            {
                if (keptVol == 0) { keptVol++; continue; }
                UnityEngine.Object.DestroyImmediate(v.gameObject);
                removedOther++;
            }
            sb.AppendLine("duplicate volumes removed = " + (vols.Length - keptVol));

            // 4. Duplicate audio listeners (Unity warns and only one works)
            var listeners = UnityEngine.Object.FindObjectsOfType<AudioListener>();
            if (listeners.Length > 1)
            {
                for (int i = 1; i < listeners.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(listeners[i]);
                    removedOther++;
                }
                sb.AppendLine("extra audio listeners removed = " + (listeners.Length - 1));
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HubScene);

            File.WriteAllText(Path.GetFullPath("docs/cleanup-report.txt"), sb.ToString());
            Debug.Log("CLEANUP_DONE\n" + sb);
        }
    }
}
