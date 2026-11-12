using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
namespace DuckGame.Maps.V09 {
[InitializeOnLoad]public static class PolishSetupRepair {
 static PolishSetupRepair(){EditorApplication.update+=Tick;}
 static void Tick(){string q="docs/verification/v09/repair-request.txt";if(EditorApplication.isPlaying||EditorApplication.isCompiling||!File.Exists(q)||File.ReadAllText(q).Trim()!="repair")return;File.WriteAllText(q,"");var m=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckGame/Maps/V09/Generated/TailoredHat.asset");var vs=m.vertices;int start=vs.Length-582;for(int i=194;i<582;i++){int strip=i/194,local=i%194,row=local/97;float a=(local%97)*Mathf.PI*2/96,z=(strip==1?2.548f:2.597f)+row*.005f,rx=Mathf.Lerp(.337f,.313f,(z-2.55f)/.06f)+.009f;vs[start+i]=new Vector3(Mathf.Cos(a)*rx,-.22f+Mathf.Sin(a)*rx*.915f,z);}m.vertices=vs;for(int s=m.subMeshCount-2;s<m.subMeshCount;s++){var ts=m.GetTriangles(s);for(int i=0;i<ts.Length;i+=3){var a=vs[ts[i]];var b=vs[ts[i+1]];var c=vs[ts[i+2]];var radial=(a+b+c)/3;radial.y+=.22f;radial.z=0;if(Vector3.Dot(Vector3.Cross(b-a,c-a),radial)<0){int temp=ts[i+1];ts[i+1]=ts[i+2];ts[i+2]=temp;}}m.SetTriangles(ts,s);}m.RecalculateBounds();EditorUtility.SetDirty(m);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());File.WriteAllText("docs/verification/v09/repair.txt","Applied outward triangles; seams offset 3mm from band. Inspect screenshots before acceptance.");}
}
}
