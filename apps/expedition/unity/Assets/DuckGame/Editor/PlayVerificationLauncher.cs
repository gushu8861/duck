using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace DuckGame.EditorTools {
public static class PlayVerificationLauncher {
 [MenuItem("Duck Game/Play Verification v0.3")]
 public static void Launch(){if(EditorApplication.isPlaying)return;var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty){EditorUtility.DisplayDialog("Scene preserved","Save the current scene before verification. No changes discarded.","OK");return;}string path=PlaygroundBuilder.LatestScene;if(string.IsNullOrEmpty(path)){EditorUtility.DisplayDialog("Build first","Run Duck Game / Build v0.3 Playground first.","OK");return;}SessionState.SetString("DuckGame.PreviousPlayScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetBool("DuckGame.Verify",true);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(path);EditorApplication.isPlaying=true;}
}
}
