using System;
using UnityEngine;
namespace DuckGame {
[DefaultExecutionOrder(60)] public sealed class OrbShooter:MonoBehaviour {
 public OrbConfig config;public HatBulge hat;public ShoulderCamera view;public Material orbMaterial;
 public float rateMultiplier=1;
 public Material trailMaterial;
 public bool InjectInput,TestFire;
 // Optional: a pooled/alternative projectile owner. Null keeps the original sphere path.
 // Not serialized: routers register through OrbRouterRegistry so no prefab re-save is needed.
 [System.NonSerialized] public IOrbShotRouter shotRouter;
 public OrbMagazine Magazine {get;}=new OrbMagazine();
 public event Action<double,Vector3,Vector3> Fired;
 public event Action<Vector3> Blocked;
 public event Action Readied;
 public double Elapsed {get;private set;}
 public Transform ReadyVisual=>ready!=null?ready.transform:null;
 GameObject ready;bool pendingShot;MaterialPropertyBlock orbColors;
 public float ExtrusionProgress => Mathf.InverseLerp(.60f, .95f, (float)Magazine.LoadProgress);
 public Vector3 RoundAxis => hat.TipDirection;
 public Vector3 FormedRoundPosition => hat.TipWorld + RoundAxis * (config.spawnStrategy == OrbSpawnStrategy.HatTip ? 0 : config.radius * 2.5f) + transform.TransformDirection(config.spawnOffset);
 public double LoadInterval=>Math.Max(.05,config.loadInterval/Mathf.Clamp(rateMultiplier,.1f,10));
 // Legacy API alias: a formed round can fire immediately; loading owns cadence.
 public double ShotInterval=>LoadInterval;
 void Awake(){OrbImpactPool.Warmup();orbColors=new MaterialPropertyBlock();ready=CreateOrb("ReadyOrb",config.radius*1.25f);ready.GetComponent<Renderer>().sharedMaterial=orbMaterial;ready.SetActive(false);}
 public void ResetSimulation(){Magazine.Reset();Elapsed=0;pendingShot=false;if(hat!=null){hat.Active=hat.Waiting=false;hat.Travel=0;}if(ready!=null)ready.SetActive(false);}
 void OnDisable(){ResetSimulation();TestFire=false;}
 void Update(){
  Elapsed+=Time.deltaTime;
  bool wasReady=Magazine.Ready;
  pendingShot=Magazine.Tick(Time.deltaTime,InjectInput?TestFire:Input.GetMouseButton(0),LoadInterval,LoadInterval);
  if(!wasReady&&Magazine.Ready&&!pendingShot)Readied?.Invoke();
  float progress=(float)Magazine.LoadProgress;
  hat.Active=!pendingShot;
  hat.Waiting=Magazine.Ready;
  hat.Travel=Magazine.Ready?1:Mathf.Clamp01(progress/.72f);
 }
 void LateUpdate(){
  SetOrbColor(ready.GetComponent<Renderer>());
  if(pendingShot){Fire();pendingShot=false;}
  float progress=(float)Magazine.LoadProgress;
  bool visible=Magazine.Ready||progress>.60f;
  ready.SetActive(visible);
  if(!visible)return;
  float extrusion=ExtrusionProgress;
  float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.95f,1,progress));
  float travel=Mathf.SmoothStep(0,1,extrusion);
  Vector3 axis=RoundAxis;
  ready.transform.position=Vector3.Lerp(hat.TipWorld-axis*config.radius*.7f,FormedRoundPosition,travel);
  ready.transform.rotation=Quaternion.FromToRotation(Vector3.up,axis);
  // The same normalized loading clock drives elongation, squeeze and rounding.
  Vector3 shape=Vector3.Lerp(new Vector3(.40f,.72f,.40f),new Vector3(.70f,1.42f,.70f),Mathf.Clamp01(extrusion/.55f));
  if(extrusion>.55f)shape=Vector3.Lerp(new Vector3(.70f,1.42f,.70f),new Vector3(1.18f,.75f,1.18f),(extrusion-.55f)/.45f);
  shape=Vector3.Lerp(shape,Vector3.one,settle);
  ready.transform.localScale=shape*config.radius*2.5f;
 }
 void Fire(){Vector3 tip=hat.TipWorld;Vector3 origin=FormedRoundPosition;Ray ray=view.AimRay;Vector3 target=Physics.Raycast(ray,out var aimHit,150,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?aimHit.point:ray.GetPoint(100);Vector3 direction=(target-origin).normalized;Fired?.Invoke(Elapsed,origin,direction);
 Vector3 anchor=transform.position+Vector3.up*1.8f;
 if(MuzzleBlocked(anchor,tip,origin,config.radius,out var block,out var overlap)){
  Vector3 p=overlap!=null?overlap.ClosestPoint(origin):block.point;Collider collider=overlap!=null?overlap:block.collider;Vector3 normal=overlap!=null?(anchor-p).normalized:block.normal;int n=Mathf.Clamp(config.projectileCount,1,8);for(int i=0;i<n;i++)OrbProjectile.ApplyImpact(config,new OrbShotContext(origin,direction,i,gameObject),new OrbImpactContext(collider,p,normal,overlap==null));Blocked?.Invoke(p);return;
 }
 int count=Mathf.Clamp(config.projectileCount,1,8);IOrbShotRouter router=shotRouter!=null?shotRouter:OrbRouterRegistry.Current;if(router!=null&&router.TryRoute(this,origin,direction))return;for(int i=0;i<count;i++){float angle=(i-(count-1)*.5f)*config.spreadDegrees;Vector3 dir=Quaternion.AngleAxis(angle,transform.up)*direction;var shot=new OrbShotContext(origin,dir,i,gameObject);GameObject orb=CreateOrb("MagicOrb",config.radius);orb.transform.position=origin;orb.GetComponent<Renderer>().sharedMaterial=orbMaterial;SetOrbColor(orb.GetComponent<Renderer>());var trail=orb.AddComponent<TrailRenderer>();trail.sharedMaterial=trailMaterial!=null?trailMaterial:orbMaterial;trail.startColor=trail.endColor=config.orbColor;SetOrbColor(trail);trail.time=.16f;trail.startWidth=config.radius*1.2f;trail.endWidth=0;trail.minVertexDistance=.035f;trail.autodestruct=true;orb.AddComponent<OrbProjectile>().Launch(config,shot);}
 }
 void SetOrbColor(Renderer surface){orbColors.SetColor("_Glow",config.orbColor*3);orbColors.SetColor("_BaseColor",config.orbColor);orbColors.SetColor("_Color",config.orbColor);orbColors.SetColor("_EmissionColor",config.orbColor*2);surface.SetPropertyBlock(orbColors);}
 public static bool MuzzleBlocked(Vector3 anchor,Vector3 tip,Vector3 spawn,float radius,out RaycastHit hit,out Collider overlap){hit=default;overlap=null;foreach(var c in Physics.OverlapSphere(anchor,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){overlap=c;return true;}Vector3 first=tip-anchor;if(first.magnitude>.0001f&&OrbProjectile.Sweep(anchor,radius,first.normalized,first.magnitude,out hit))return true;Vector3 last=spawn-tip;if(last.magnitude>.0001f&&OrbProjectile.Sweep(tip,radius,last.normalized,last.magnitude,out hit))return true;foreach(var c in Physics.OverlapSphere(spawn,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){overlap=c;return true;}return false;}
 static GameObject CreateOrb(string name,float radius){GameObject orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name=name;orb.layer=2;orb.GetComponent<Collider>().enabled=false;Destroy(orb.GetComponent<Collider>());orb.transform.localScale=Vector3.one*radius*2;return orb;}
 void OnDestroy(){if(ready!=null)Destroy(ready);}
 void OnGUI(){float alpha=1-view.ADS;if(alpha>.01f){var old=GUI.color;GUI.color=new Color(.8f,1,1,alpha);Vector2 p=new Vector2(view.AimViewport.x*Screen.width,(1-view.AimViewport.y)*Screen.height);GUI.DrawTexture(new Rect(p.x-5,p.y-1,10,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(p.x-1,p.y-5,2,10),Texture2D.whiteTexture);GUI.color=old;}GUI.Label(new Rect(22,18,500,25),"HAT / ORB     WASD move   Mouse aim   Shift run");GUI.Label(new Rect(22,42,600,25),"LMB fire / hold    RMB focus    1-4 gestures    Esc unlock");GUI.Label(new Rect(22,Screen.height-50,500,25),"READY "+(Magazine.Ready?"01":"00")+"   LOAD "+Mathf.RoundToInt((float)Magazine.LoadProgress*100)+"%");}
}
}
