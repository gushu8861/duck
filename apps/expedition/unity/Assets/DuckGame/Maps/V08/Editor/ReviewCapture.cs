using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace DuckGame.Maps.V08 {
[InitializeOnLoad] public static class ReviewCapture {
 static ReviewCapture(){EditorApplication.update+=Tick;}
 const string D="docs/verification/v09/";
 static void Tick(){string q=D+"capture-request.txt";if(EditorApplication.isCompiling||!File.Exists(q))return;string cmd=File.ReadAllText(q).Trim();if(cmd.Length==0)return;File.WriteAllText(q,"");if(!EditorApplication.isPlaying)return;var t=UnityEngine.Object.FindObjectOfType<MerchantTrade>();if(cmd.StartsWith("state")){File.WriteAllText(D+cmd+".json",JsonUtility.ToJson(new State{gold=t.Gold,bought=t.Bought[0],shots=OrbProjectile.SpawnCount,hits=OrbProjectile.HitCount,open=t.IsOpen,restoring=t.Restoring,motor=t.motor.enabled,shooter=t.shooter.enabled,view=t.view.enabled,time=Time.timeScale,cursor=Cursor.lockState.ToString(),visible=Cursor.visible,rate=t.shooter.rateMultiplier,progress=(float)t.shooter.Magazine.LoadProgress},true));return;}if(cmd.StartsWith("portrait"))Portrait(t,cmd);}
 public static void Portrait(MerchantTrade t,string label){var go=new GameObject("Review portrait camera");var cam=go.AddComponent<Camera>();var p=t.motor.transform;cam.transform.position=p.TransformPoint(label.Contains("side")?new Vector3(6.4f,2.7f,.3f):new Vector3(3.5f,2.7f,5.7f));cam.transform.LookAt(p.position+Vector3.up*1.9f);cam.fieldOfView=32;var rt=new RenderTexture(1400,1400,24);var old=RenderTexture.active;Texture2D tex=null;try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex=new Texture2D(1400,1400,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1400),0,0);tex.Apply();File.WriteAllBytes(D+label+".png",tex.EncodeToPNG());}finally{RenderTexture.active=old;cam.targetTexture=null;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}}
 [Serializable]class State{public int gold,bought,shots,hits;public bool open,restoring,motor,shooter,view,visible;public float time,rate,progress;public string cursor;}
}
}
