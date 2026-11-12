using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DuckGame.Maps.V08;
namespace DuckGame.Maps.V09 {
[InitializeOnLoad]public static class MatchedComparison {
 static MatchedComparison(){EditorApplication.update+=Tick;}
 const string D="docs/verification/v09/";
 static void Tick(){string q=D+"compare-request.txt";if(EditorApplication.isCompiling||!EditorApplication.isPlaying||!File.Exists(q)||File.ReadAllText(q).Trim()!="compare")return;File.WriteAllText(q,"");var t=UnityEngine.Object.FindObjectOfType<MerchantTrade>();Compare(t);}
 static void Photo(Camera cam,string name){var rt=new RenderTexture(1920,1080,24);var active=RenderTexture.active;var target=cam.targetTexture;Texture2D tex=null;try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(D+name+".png",tex.EncodeToPNG());}finally{cam.targetTexture=target;RenderTexture.active=active;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);}}
 static void Compare(MerchantTrade t){var h=t.shooter.hat;var skins=t.motor.GetComponentsInChildren<SkinnedMeshRenderer>();var mats=skins.ToDictionary(x=>x,x=>x.sharedMaterials);var current=h.hat.sharedMesh;var mesh=UnityEngine.Object.Instantiate(current);mesh.subMeshCount=current.subMeshCount-2;var ready=t.shooter.ReadyVisual.GetComponent<Renderer>();var orb=ready.sharedMaterial;bool enabled=h.enabled;try{h.enabled=false;h.hat.sharedMesh=h.outline.sharedMesh=mesh;foreach(var skin in skins){if(skin.name.EndsWith("_Outline")){skin.sharedMaterials=Enumerable.Repeat(AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckGame/V04/Generated/ThinOutlineV04.mat"),mesh.subMeshCount).ToArray();continue;}skin.sharedMaterials=mats[skin].Select(m=>{var path="Assets/DuckGame/Generated/v03/"+Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(m))+".mat";return AssetDatabase.LoadAssetAtPath<Material>(path)??m;}).ToArray();}ready.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckGame/Generated/v03/OrbGlow.mat");ReviewCapture.Portrait(t,"portrait-before-matched");ReviewCapture.Portrait(t,"portrait-side-before");Photo(t.view.lens,"shoulder-before");}finally{foreach(var pair in mats)pair.Key.sharedMaterials=pair.Value;h.hat.sharedMesh=h.outline.sharedMesh=current;h.enabled=enabled;ready.sharedMaterial=orb;UnityEngine.Object.DestroyImmediate(mesh);}ReviewCapture.Portrait(t,"portrait-after-matched");Photo(t.view.lens,"shoulder-after-matched");}
}
}
