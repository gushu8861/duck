using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace DuckGame {
[DefaultExecutionOrder(-100)] public sealed class RuntimeVerification:MonoBehaviour {
 public OrbShooter shooter;public GooseMotor motor;public ShoulderCamera view;
 [Serializable] public class Shot {public double time;public Vector3 origin,direction,tip;public double interval;}
 [Serializable] public class Sample {public double time;public int loaded;public double load,gate;public float ads;public Vector3 camera,tip,playerPosition;public bool cameraOverlap;}
 [Serializable] public class Report {public bool passed;public string unity,scope;public List<Shot> shots=new List<Shot>();public List<Sample> frames=new List<Sample>();public List<string> errors=new List<string>();public List<string> captures=new List<string>();public int flightHits,projectiles,blocked;}
 Report report;string output;bool running;
 void Awake(){
 #if UNITY_EDITOR
 if(UnityEditor.SessionState.GetBool("DuckGame.Verify",false)){UnityEditor.SessionState.SetBool("DuckGame.Verify",false);string previous=UnityEditor.SessionState.GetString("DuckGame.PreviousPlayScene","");UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(previous);enabled=true;running=true;motor.InjectInput=true;view.InjectInput=true;shooter.InjectInput=true;}
 #endif
 }
 IEnumerator Start(){if(!running)yield break;output=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/verification/v03-play"));Directory.CreateDirectory(output);report=new Report{unity=Application.unityVersion,scope="Actual Play; injected input only for this explicit verification run"};shooter.ResetSimulation();OrbProjectile.HitCount=OrbProjectile.SpawnCount=0;shooter.Fired+=RecordShot;shooter.Blocked+=p=>report.blocked++;
 float elapsed=0;int capture=0;bool buff=false,reset=false,teleported=false;double beforeBuff=0;
 float[] times={.13f,.37f,.69f,1.9f,2.18f,4.05f,6.15f,7.65f,8.3f};string[] names={"01_bulge_base","02_bulge_middle","03_bulge_top","04_ready_and_staged","05_shot","05b_movement","06_ads","07_wall_ads","08_wall_turn"};
 while(elapsed<9.2f){yield return new WaitForEndOfFrame();elapsed+=Time.deltaTime;shooter.TestFire=elapsed>=2.1f&&elapsed<4.65f||elapsed>=8.1f&&elapsed<8.7f;view.TestADS=elapsed>=5.6f&&elapsed<6.3f||elapsed>=6.38f&&elapsed<6.44f||elapsed>=6.50f&&elapsed<6.7f||elapsed>=7.25f&&elapsed<7.85f;motor.TestMove=elapsed>=3.1f&&elapsed<3.6f?Vector2.right:(elapsed>=3.6f&&elapsed<4.2f?Vector2.up:Vector2.zero);motor.TestRun=elapsed>=3.6f&&elapsed<4.2f;view.TestLook=elapsed>=7?new Vector2(42*Time.deltaTime,0):Vector2.zero;
 if(!reset&&elapsed>=5){shooter.ResetSimulation();reset=true;}
 if(!buff&&elapsed>=5.25f){beforeBuff=shooter.Magazine.LoadProgress;shooter.rateMultiplier=2;if(Math.Abs(beforeBuff-shooter.Magazine.LoadProgress)>.000001)report.errors.Add("Modifier reset normalized load");buff=true;}
 if(!teleported&&elapsed>=7){motor.controller.enabled=false;motor.transform.position=new Vector3(5,.03f,1);motor.controller.enabled=true;teleported=true;}
 bool overlap=Physics.CheckSphere(view.transform.position,.155f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);if(overlap)report.errors.Add("Camera overlap at "+elapsed);
 report.frames.Add(new Sample{time=elapsed,loaded=shooter.Magazine.Loaded,load=shooter.Magazine.LoadProgress,gate=shooter.Magazine.ShotProgress,ads=view.ADSProgress,camera=view.transform.position,playerPosition=motor.transform.position,tip=shooter.hat.TipWorld,cameraOverlap=overlap});
 if(shooter.Magazine.Loaded<0||shooter.Magazine.Loaded>1)report.errors.Add("Capacity outside 0..1");
 if(capture<times.Length&&elapsed>=times[capture]){string file=names[capture]+".png";ScreenCapture.CaptureScreenshot(Path.Combine(output,file));report.captures.Add(file);capture++;}
 }
 shooter.TestFire=false;view.TestADS=false;view.TestLook=Vector2.zero;report.projectiles=OrbProjectile.SpawnCount;report.flightHits=OrbProjectile.HitCount;
 if(report.shots.Count<4)report.errors.Add("Too few automatic shots");for(int i=1;i<report.shots.Count;i++){double gap=report.shots[i].time-report.shots[i-1].time;if(gap>0&&gap<Math.Min(report.shots[i].interval,report.shots[i-1].interval)-.002)report.errors.Add("Shot gate bypass");}
 if(!report.frames.Exists(f=>f.loaded==1))report.errors.Add("Single round never reached ready");if(!report.frames.Exists(f=>f.ads>.99f))report.errors.Add("ADS never fully reached");report.passed=report.errors.Count==0;File.WriteAllText(Path.Combine(output,"play-report.json"),JsonUtility.ToJson(report,true));Debug.Log("DUCK_V03_PLAY_REPORT "+report.passed+" "+output);yield return new WaitForSeconds(.2f);
 #if UNITY_EDITOR
 UnityEditor.EditorApplication.isPlaying=false;
 #endif
 }
 void RecordShot(double time,Vector3 origin,Vector3 direction){report.shots.Add(new Shot{time=Time.timeSinceLevelLoadAsDouble,origin=origin,direction=direction,tip=shooter.hat.TipWorld,interval=shooter.ShotInterval});}
}
}
