using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using DuckGame.V04;
namespace DuckGame.Maps.V07 {
[InitializeOnLoad] static class MapVerification {
 static MapVerification(){EditorApplication.update+=Tick;}
 static void Tick(){string p="docs/verification/v07/verify-request.txt";if(!EditorApplication.isPlaying||!File.Exists(p)||File.ReadAllText(p).Trim()=="")return;File.WriteAllText(p,"");var m=UnityEngine.Object.FindObjectOfType<GooseMotorV04>();if(m!=null&&m.GetComponent<MapProbe>()==null)m.gameObject.AddComponent<MapProbe>();}
}
[DefaultExecutionOrder(-100)] public class MapProbe:MonoBehaviour {
 [Serializable] public class Result {public List<string> checks=new List<string>(),errors=new List<string>();public int frames,shots,targetHits,environmentHits;public float minY=999,maxFrame,maxRouteFrame;public bool passed;}
 Result r=new Result();GooseMotorV04 m;GoosePoseV04 pose;ShoulderCamera v;OrbShooter s;bool route;Vector3 destination;bool run;float began;int jumpFrames,slideFrames;bool record;
 void OnLog(string msg,string trace,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!r.errors.Contains(msg))r.errors.Add(msg);}
 void Update(){if(m==null)return;r.frames++;r.maxFrame=Mathf.Max(r.maxFrame,Time.deltaTime);r.minY=Mathf.Min(r.minY,m.transform.position.y);if(record){if(m.State==MoveState.Jump)jumpFrames++;if(m.State==MoveState.Slide)slideFrames++;}m.TestMove=Vector2.zero;m.TestRun=run;if(route){r.maxRouteFrame=Mathf.Max(r.maxRouteFrame,Time.deltaTime);Vector3 d=destination-m.transform.position;d.y=0;var local=Quaternion.Inverse(Quaternion.Euler(0,v.Yaw,0))*d.normalized;m.TestMove=new Vector2(local.x,local.z);if(d.magnitude<.45f)route=false;}}
 void Check(bool ok,string what){if(ok)r.checks.Add(what);else r.errors.Add(what);}
 void Tele(Vector3 p,float yaw=0){m.controller.enabled=false;m.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));m.ResetForTeleport();pose.ResetForTeleport();m.controller.enabled=true;v.TestLook=Vector2.zero;typeof(ShoulderCamera).GetField("desiredYaw",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(v,yaw);typeof(ShoulderCamera).GetProperty("Yaw").SetValue(v,yaw);m.TestMove=Vector2.zero;Physics.SyncTransforms();}
 IEnumerator Travel(Vector3 p,string label,float max=10){destination=p;route=true;float t=Time.time;while(route&&Time.time-t<max)yield return null;Check(!route,label);route=false;yield return new WaitForSeconds(.18f);}
 bool aborted; IEnumerator Start(){var stack=new Stack<IEnumerator>();stack.Push(Run());while(stack.Count>0&&!aborted){var work=stack.Peek();object value;if(!Advance(work,out value)){stack.Pop();continue;}if(value is IEnumerator child)stack.Push(child);else yield return value;}Restore();} bool Advance(IEnumerator work,out object value){value=null;try{if(!work.MoveNext())return false;value=work.Current;return true;}catch(Exception e){aborted=true;Restore();r.errors.Add(e.ToString());r.passed=false;File.WriteAllText("docs/verification/v07/play-report.json",JsonUtility.ToJson(r,true));Debug.LogException(e);return false;}} IEnumerator Run(){
  Directory.CreateDirectory("docs/verification/v07");m=GetComponent<GooseMotorV04>();pose=GetComponent<GoosePoseV04>();v=m.view;s=GetComponentInChildren<OrbShooter>();Application.logMessageReceived+=OnLog;s.Fired+=Shot;m.InjectInput=v.InjectInput=s.InjectInput=true;v.TestLook=Vector2.zero;began=Time.time;
  Tele(new Vector3(0,.04f,-15.5f));yield return new WaitForSeconds(1.5f);Check(m.OnGround&&Mathf.Abs(transform.position.y)<.15f,"Spawn grounded without penetration");Check(s.ReadyVisual!=null&&s.ReadyVisual.gameObject.activeInHierarchy,"Ready orb visible at loaded hat tip");yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("docs/verification/v07/shoulder-spawn.png");
  var topGO=new GameObject("V07 temporary overview");var top=topGO.AddComponent<Camera>();top.transform.position=new Vector3(12,75,-10);top.transform.LookAt(new Vector3(0,1,0));top.orthographic=true;top.orthographicSize=37;top.nearClipPlane=.1f;top.farClipPlane=180;top.backgroundColor=new Color(.62f,.70f,.65f);top.clearFlags=CameraClearFlags.SolidColor;var previousRT=RenderTexture.active;bool previousFog=RenderSettings.fog;RenderTexture rt=null;Texture2D tex=null;try{rt=new RenderTexture(1400,1400,24);top.targetTexture=rt;bool oldFog=RenderSettings.fog;RenderSettings.fog=false;top.Render();RenderSettings.fog=oldFog;RenderTexture.active=rt;tex=new Texture2D(1400,1400,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1400,1400),0,0);tex.Apply();File.WriteAllBytes("docs/verification/v07/overview.png",tex.EncodeToPNG());}finally{RenderTexture.active=previousRT;RenderSettings.fog=previousFog;top.targetTexture=null;if(tex!=null)Destroy(tex);if(rt!=null){rt.Release();Destroy(rt);}Destroy(topGO);}
  run=true;foreach(var p in new[]{new Vector3(-12,0,-11),new Vector3(-13,0,7),new Vector3(-8,0,18),new Vector3(0,0,18)})yield return Travel(p,"West loop "+p);
  foreach(var p in new[]{new Vector3(8,0,18),new Vector3(13,0,7),new Vector3(12,0,-11),new Vector3(0,0,-15.5f)})yield return Travel(p,"East loop "+p);
  Check(Time.time-began<30,"Both loops traversed under 30 seconds");
  Tele(new Vector3(-8,.04f,-2));run=false;record=true;destination=new Vector3(-8,0,2);route=true;yield return new WaitForSeconds(.22f);m.TestJump=true;yield return new WaitForSeconds(.25f);m.TestJump=true;yield return new WaitForSeconds(.7f);route=false;yield return new WaitForSeconds(1);Check(transform.position.y>.65f&&m.OnGround,"Jump and double-jump land on terrace");Check(jumpFrames>0,"Real jump state observed");
  Tele(new Vector3(0,.04f,-10));destination=new Vector3(0,0,0);route=true;run=true;yield return new WaitForSeconds(.3f);m.TestSlide=true;yield return new WaitForSeconds(1.2f);route=false;yield return new WaitForSeconds(.3f);Check(slideFrames>0&&m.State!=MoveState.Slide&&Mathf.Abs(m.controller.height-3.5f)<.01f,"Slide restores standing capsule");
  foreach(var dir in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right}){Tele(dir*18+Vector3.up*.04f);destination=dir*30;route=true;yield return new WaitForSeconds(.3f);m.TestJump=true;yield return new WaitForSeconds(.25f);m.TestJump=true;yield return new WaitForSeconds(1.6f);route=false;Check(Mathf.Abs(transform.position.x)<21.8f&&Mathf.Abs(transform.position.z)<21.8f,"Boundary contains double jump "+dir);}
  Tele(new Vector3(5,.04f,4));run=false;v.TestADS=true;typeof(ShoulderCamera).GetField("desiredPitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(v,3.5f);yield return new WaitForSeconds(1.2f);int before=UnityEngine.Object.FindObjectsOfType<TargetDummy>().Sum(t=>t.Hits);s.TestFire=true;yield return new WaitForSeconds(2.2f);s.TestFire=false;int after=UnityEngine.Object.FindObjectsOfType<TargetDummy>().Sum(t=>t.Hits);r.targetHits=after-before;Check(r.targetHits>0,"ADS projectile hit map target");Check(v.ADS>.999f,"Full ADS reached");yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("docs/verification/v07/ads-target.png");
  Tele(new Vector3(19,.04f,-1),90);v.TestADS=true;typeof(ShoulderCamera).GetField("desiredPitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(v,0f);yield return new WaitForSeconds(1);s.TestFire=true;yield return new WaitForSeconds(1.8f);s.TestFire=false;r.environmentHits=OrbImpactPool.Current.EnvironmentHits;Check(r.environmentHits>0,"Real projectiles impacted boundary environment");Check(r.shots>=3,"Map shooting with target and boundary geometry");Check(!Physics.CheckSphere(v.transform.position,.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"ADS camera outside wall colliders");
  v.TestADS=false;Tele(new Vector3(0,.04f,-15.5f));typeof(ShoulderCamera).GetField("desiredPitch",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(v,8f);yield return new WaitForSeconds(1);Check(r.minY>-.15f,"No floor fall-through");Restore();Check(!m.InjectInput&&!v.InjectInput&&!s.InjectInput,"All input injection disabled on handoff");r.passed=r.errors.Count==0;File.WriteAllText("docs/verification/v07/play-report.json",JsonUtility.ToJson(r,true));Debug.Log("V07_MAP_VERIFY "+r.passed);Destroy(this);
 }
 void Shot(double t,Vector3 p,Vector3 d){r.shots++;}
 void Restore(){route=false;Application.logMessageReceived-=OnLog;if(m!=null){m.InjectInput=false;m.TestMove=Vector2.zero;m.TestRun=m.TestJump=m.TestJumpHeld=m.TestSlide=false;}if(v!=null){v.InjectInput=false;v.TestADS=false;v.TestLook=Vector2.zero;}if(s!=null){s.InjectInput=false;s.TestFire=false;s.Fired-=Shot;}} void OnDisable(){aborted=true;StopAllCoroutines();Restore();} void OnDestroy(){aborted=true;Restore();}
}
}






