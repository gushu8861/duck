using UnityEngine;
namespace DuckGame {
[DefaultExecutionOrder(30)] public sealed class ShoulderCamera:MonoBehaviour {
 public Transform player;public HatBulge hat;public Camera lens;
 public float minPitch=-18;
 public float distance=5.2f,shoulder=.8f,mouseSensitivity=2,pitch=12;
 public AnimationCurve adsCurve=AnimationCurve.EaseInOut(0,0,1,1);
 public bool InjectInput,TestADS;public Vector2 TestLook;
 // V04 supplies an unanimated aiming anchor; legacy scenes retain hat-tip aiming.
 public bool StableADS {get;set;}
 public Vector3 StableADSAnchor {get;set;}
 public float Yaw {get;private set;}
 public float ADS {get;private set;}
 public float ADSProgress {get;private set;}
 public Vector2 AimViewport {get;private set;}=new Vector2(.5f,.5f);
 SphereCollider probe;
 void Awake(){probe=gameObject.AddComponent<SphereCollider>();probe.radius=.18f;probe.enabled=false;gameObject.layer=2;}
 Vector3 Resolve(Vector3 p){for(int i=0;i<6;i++){bool changed=false;foreach(var c in Physics.OverlapSphere(p,.18f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){if(Physics.ComputePenetration(probe,p,Quaternion.identity,c,c.transform.position,c.transform.rotation,out var dir,out float depth)){p+=dir*(depth+.012f);changed=true;}}if(!changed)break;}return p;}
 Vector3 damping;float desiredYaw,desiredPitch,angleYVelocity,anglePVelocity;
 void Start(){Yaw=desiredYaw=player.eulerAngles.y;desiredPitch=pitch;transform.position=player.position+Vector3.up*2.5f-player.forward*distance;if(!InjectInput)Cursor.lockState=CursorLockMode.Locked;}
 void LateUpdate(){if(!InjectInput){if(Input.GetKeyDown(KeyCode.Escape))Cursor.lockState=CursorLockMode.None;if(Input.GetMouseButtonDown(0))Cursor.lockState=CursorLockMode.Locked;}
 Vector2 look=InjectInput?TestLook:(Cursor.lockState==CursorLockMode.Locked?new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y")):Vector2.zero);desiredYaw+=look.x*mouseSensitivity;desiredPitch=Mathf.Clamp(desiredPitch-look.y*mouseSensitivity,minPitch,55);Yaw=Mathf.SmoothDampAngle(Yaw,desiredYaw,ref angleYVelocity,.035f);pitch=Mathf.SmoothDampAngle(pitch,desiredPitch,ref anglePVelocity,.035f);
 bool ads=InjectInput?TestADS:Input.GetMouseButton(1);ADSProgress=Mathf.MoveTowards(ADSProgress,ads?1:0,Time.deltaTime/.5f);ADS=adsCurve.Evaluate(ADSProgress);Quaternion rotation=Quaternion.Euler(pitch,Yaw,0);Vector3 direction=rotation*Vector3.forward;Vector3 pivot=player.position+Vector3.up*2.35f;Vector3 normal=pivot+rotation*new Vector3(shoulder,.4f,-distance);Vector3 adsAnchor=StableADS?StableADSAnchor:hat.TipWorld;Vector3 near=adsAnchor-direction*1.60f;Vector3 wanted=Vector3.Lerp(normal,near,ADS);Vector3 castOrigin=Vector3.Lerp(pivot,adsAnchor,ADS);castOrigin=Resolve(castOrigin);Vector3 delta=wanted-castOrigin;
 if(Physics.SphereCast(castOrigin,.16f,delta.normalized,out var hit,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))wanted=castOrigin+delta.normalized*Mathf.Max(.03f,hit.distance-.05f);
 Vector3 final=Vector3.SmoothDamp(transform.position,wanted,ref damping,Mathf.Lerp(.075f,.035f,ADS));if(StableADS)final=Vector3.Lerp(final,wanted,ADS);Vector3 path=final-castOrigin;if(path.sqrMagnitude>.00001f&&Physics.SphereCast(castOrigin,.18f,path.normalized,out var finalHit,path.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){final=castOrigin+path.normalized*Mathf.Max(0,finalHit.distance-.025f);damping=Vector3.ProjectOnPlane(damping,finalHit.normal);}transform.position=Resolve(final);transform.rotation=rotation;lens.fieldOfView=Mathf.Lerp(58,48,ADS);Vector3 tip=lens.WorldToViewportPoint(hat.TipWorld);AimViewport=Vector2.Lerp(new Vector2(.5f,.5f),new Vector2(tip.x,tip.y),ADS);
 }
 public Ray AimRay=>lens.ViewportPointToRay(new Vector3(AimViewport.x,AimViewport.y,0));
 void OnDisable(){Cursor.lockState=CursorLockMode.None;}
}
}
