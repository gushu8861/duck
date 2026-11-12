using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace DuckGame.V04.EditorTools {
[InitializeOnLoad] static class HotfixDiagnosticsV04 {
 static HotfixDiagnosticsV04(){EditorApplication.update+=Tick;}
 static DateTime last;
 static void Tick(){
  string request="docs/verification/v04-hotfix-request.txt";
  if(!EditorApplication.isPlaying || !File.Exists(request))return;
  DateTime stamp=File.GetLastWriteTimeUtc(request);if(stamp==last)return;last=stamp;
  string command=File.ReadAllText(request).Trim();if(string.IsNullOrEmpty(command))return;File.WriteAllText(request, "");
  if(command=="legacy-camera") {var camera=UnityEngine.Object.FindObjectOfType<ShoulderCamera>();if(camera!=null)camera.gameObject.AddComponent<LegacyCameraProbeV04>();return;}
  var motor=UnityEngine.Object.FindObjectOfType<GooseMotorV04>();if(motor==null)return;
  var probe=motor.GetComponent<HotfixProbeV04>();if(probe==null)probe=motor.gameObject.AddComponent<HotfixProbeV04>();
  try{probe.Begin(command);}catch(Exception e){Debug.LogException(e);UnityEngine.Object.Destroy(probe);}
 }
}
[DefaultExecutionOrder(100)] public sealed class HotfixProbeV04:MonoBehaviour {
 [Serializable] public class Sample {public float rayHatDistance;public Vector3 rayOrigin,rayDirection,hatViewport;public bool injectedMotor,injectedView,stableADS;public float time,dt,yaw,bodyYaw,turn,ads,height,stateTime;public string state;public bool grounded,standBlocked,canStand;public Vector3 root,camera,tip;public Vector2 aim,look;public string[] blockers;}
 [Serializable] public class PoseEvidence {public float time;public string state;public Vector3 min,max,head,neck,tip;}
 [Serializable] public class Report {public List<PoseEvidence> poses=new List<PoseEvidence>();public string mode;public List<Sample> samples=new List<Sample>();public List<string> errors=new List<string>();}
 GooseMotorV04 motor;GoosePoseV04 pose;ShoulderCamera view;OrbShooter shooter;Report report;float start;int lastFrame;string mode,path;bool slide;int jumpPhase;bool started;int capture;bool oldMotorInput,oldViewInput,oldShooterInput;
 public void Begin(string command){Restore();motor=GetComponent<GooseMotorV04>();pose=GetComponent<GoosePoseV04>();view=motor.view;shooter=GetComponentInChildren<OrbShooter>();mode=command;path="docs/verification/v04-hotfix/"+mode;Directory.CreateDirectory(path);report=new Report{mode=mode};start=Time.time;slide=false;jumpPhase=0;capture=0;started=true;Application.logMessageReceived+=Log;
  oldMotorInput=motor.InjectInput;oldViewInput=view.InjectInput;oldShooterInput=shooter.InjectInput;
  bool injected=!mode.StartsWith("manual");motor.InjectInput=view.InjectInput=shooter.InjectInput=injected;
  if(injected){motor.controller.enabled=false;motor.transform.SetPositionAndRotation(mode.Contains("angled")?new Vector3(1.9f,.03f,-1.0f):new Vector3(0,.03f,-12),mode.Contains("angled")?Quaternion.Euler(0,45,0):Quaternion.identity);motor.ResetForTeleport();motor.controller.enabled=true;pose.ResetForTeleport();}
 }
 void Log(string message,string trace,LogType type){if(report!=null&&(type==LogType.Exception||type==LogType.Error))report.errors.Add(message);}
 void Update(){if(!started)return;float t=Time.time-start;
  if(mode.Contains("slide")&&!mode.StartsWith("manual")){motor.TestMove=t<.5f?(mode.Contains("angled")?Vector2.one.normalized:Vector2.up):Vector2.zero;if(t>.3f&&!slide){motor.TestSlide=true;slide=true;}}
  if(mode.Contains("ads")&&!mode.StartsWith("manual")){view.TestADS=t>.5f&&t<7.0f;view.TestLook=t<1.2f?Vector2.zero:new Vector2((t<2.6f?1:t<4?-1:t<5.4f?1:-1)*((mode.Contains("slow")?90:720)*Time.deltaTime/view.mouseSensitivity),0);motor.TestMove=t>4.2f&&t<6?Vector2.right:Vector2.zero;}
  if(mode.Contains("combo"))Combo(t);
 }
  void Combo(float t){
   view.TestADS=t>.5f&&t<2.5f||t>3&&t<5||t>5.5f&&t<7;
   view.TestLook=new Vector2((t<2?1:t<4?-1:t<6?1:-1)*90*Time.deltaTime/view.mouseSensitivity,0);
   motor.TestMove=t>.6f&&t<2?Vector2.right:t>3&&t<4.1f?Vector2.up:Vector2.zero;
   if(t>1.2f&&jumpPhase==0){motor.TestJump=true;jumpPhase++;}
   if(t>1.36f&&jumpPhase==1){motor.TestJump=true;jumpPhase++;}
   if(t>3.5f&&!slide){motor.TestSlide=true;slide=true;}
   shooter.TestFire=t>5.7f&&t<6.8f;
  }
 void LateUpdate(){if(!started||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;float t=Time.time-start;
  var blockers=new List<string>();Bounds box=motor.SlideLocalEnvelope;Vector3 max=box.max;max.y=3.5f;box.SetMinMax(box.min,max);
  foreach(var c in Physics.OverlapBox(motor.transform.TransformPoint(box.center),box.extents,motor.transform.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))blockers.Add("OBB:"+c.name+" layer="+c.gameObject.layer);
  Vector3 half=box.extents,rt=motor.transform.right,fw=motor.transform.forward;Vector3 wh=new Vector3(Mathf.Abs(rt.x)*half.x+Mathf.Abs(fw.x)*half.z,half.y,Mathf.Abs(rt.z)*half.x+Mathf.Abs(fw.z)*half.z);
  foreach(var c in Physics.OverlapBox(motor.transform.TransformPoint(box.center),wh,Quaternion.identity,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))blockers.Add("AABB:"+c.name+" layer="+c.gameObject.layer);
  Ray ray=view.AimRay;
  if(mode.Contains("ads")&&capture<3&&t>(mode.Contains("combo")?(capture==0?1.65f:capture==1?3.9f:5.9f):(capture==0?2.2f:capture==1?3.4f:5.0f))){ScreenCapture.CaptureScreenshot(Path.GetFullPath(path+"/ads-"+capture+".png"));capture++;
    Mesh mesh=new Mesh();Vector3 min=Vector3.one*float.PositiveInfinity,maxPoint=Vector3.one*float.NegativeInfinity;
    foreach(var skin in motor.animator.GetComponentsInChildren<SkinnedMeshRenderer>()){if(skin.name.Contains("_Outline"))continue;skin.BakeMesh(mesh);foreach(Vector3 vertex in mesh.vertices){Vector3 point=skin.transform.TransformPoint(vertex);min=Vector3.Min(min,point);maxPoint=Vector3.Max(maxPoint,point);}}
    Destroy(mesh);report.poses.Add(new PoseEvidence{time=t,state=motor.State.ToString(),min=min,max=maxPoint,head=pose.BonePosition("Head"),neck=pose.BonePosition("Neck1"),tip=shooter.hat.TipWorld});}
  report.samples.Add(new Sample{rayHatDistance=Vector3.Cross(shooter.hat.TipWorld-ray.origin,ray.direction).magnitude,rayOrigin=ray.origin,rayDirection=ray.direction,hatViewport=view.lens.WorldToViewportPoint(shooter.hat.TipWorld),injectedMotor=motor.InjectInput,injectedView=view.InjectInput,stableADS=view.StableADS,time=t,dt=Time.deltaTime,yaw=view.Yaw,bodyYaw=motor.transform.eulerAngles.y,turn=motor.FrameTurn,ads=view.ADS,height=motor.controller.height,stateTime=motor.StateTime,state=motor.State.ToString(),grounded=motor.OnGround,standBlocked=motor.StandBlocked,canStand=motor.CanStand(),root=motor.transform.position,camera=view.transform.position,tip=shooter.hat.TipWorld,aim=view.AimViewport,look=view.InjectInput?view.TestLook:new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y")),blockers=blockers.ToArray()});
  if(report.samples.Count%60==0)File.WriteAllText(path+"/report.json",JsonUtility.ToJson(report,true));
  if(t>(mode.StartsWith("manual")?40:8)){File.WriteAllText(path+"/report.json",JsonUtility.ToJson(report,true));ScreenCapture.CaptureScreenshot(Path.GetFullPath(path+"/final.png"));Restore();Debug.Log("V04_HOTFIX_CAPTURE "+mode+" "+path);}
 }
 void Restore(){if(!started)return;started=false;Application.logMessageReceived-=Log;if(motor!=null){motor.InjectInput=oldMotorInput;motor.TestMove=Vector2.zero;motor.TestJump=motor.TestSlide=motor.TestRun=false;}if(view!=null){view.InjectInput=oldViewInput;view.TestLook=Vector2.zero;view.TestADS=false;}if(shooter!=null){shooter.InjectInput=oldShooterInput;shooter.TestFire=false;}}
 void OnDestroy(){Restore();if(report!=null)File.WriteAllText(path+"/report.json",JsonUtility.ToJson(report,true));}
}
[DefaultExecutionOrder(100)] public sealed class LegacyCameraProbeV04:MonoBehaviour {
 [Serializable] class Row {public float time,ads;public bool stable;public float rayHatDistance;}
 [Serializable] class Data {public List<Row> samples=new List<Row>();}
 Data data=new Data();ShoulderCamera view;float start;bool oldInput;const string PathName="docs/verification/v04-hotfix/legacy-camera.json";
 void Start(){view=GetComponent<ShoulderCamera>();start=Time.time;oldInput=view.InjectInput;view.InjectInput=true;}
 void Update(){view.TestADS=Time.time-start>.3f;view.TestLook=new Vector2(90*Time.deltaTime/view.mouseSensitivity,0);}
 void LateUpdate(){float t=Time.time-start;Ray ray=view.AimRay;data.samples.Add(new Row{time=t,ads=view.ADS,stable=view.StableADS,rayHatDistance=Vector3.Cross(view.hat.TipWorld-ray.origin,ray.direction).magnitude});if(t>3){File.WriteAllText(PathName,JsonUtility.ToJson(data,true));Destroy(this);}}
 void OnDestroy(){if(view!=null){view.InjectInput=oldInput;view.TestADS=false;view.TestLook=Vector2.zero;}}
}
}