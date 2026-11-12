using UnityEngine;
using DuckGame.V04;
namespace DuckGame.Maps.V08 {
[DefaultExecutionOrder(-100)] public sealed class MerchantTrade:MonoBehaviour {
 public Transform interactionAnchor;public OrbShooter shooter;public GooseMotorV04 motor;public ShoulderCamera view;
 [SerializeField] bool externalPresentation;
 [SerializeField] Transform externalAnchor;

 public int Gold {get;private set;}=100;public int[] Bought {get;}=new int[3];public bool IsOpen {get;private set;}public string Message {get;private set;}="欢迎，法师！每次试玩赠送100金币。";
 public bool Restoring=>captured&&!IsOpen;public OrbConfig OriginalConfig {get;private set;}
 MerchantShopUI ui;OrbConfig runtime;bool captured,oldMotor,oldShooter,oldView;float oldTime;CursorLockMode oldLock;bool oldVisible;int closeFrame;
 static readonly int[] prices={25,25,40};
 void Awake(){OriginalConfig=shooter.config;runtime=Instantiate(OriginalConfig);runtime.name=OriginalConfig.name+" (Play only)";shooter.config=runtime;if(!externalPresentation){ui=gameObject.AddComponent<MerchantShopUI>();ui.trade=this;}}
 public bool CanInteract() => !externalPresentation && CanInteractAt(interactionAnchor);

 public bool CanInteractAt(Transform anchor) {
  if(!isActiveAndEnabled || IsOpen || captured || anchor == null ||
     anchor != (externalPresentation ? externalAnchor : interactionAnchor)) return false;
  return CanReach(anchor, externalPresentation ? null : transform);
 }

 public bool CanReach(Transform anchor, Transform facility) {
  if(motor == null || view == null || view.lens == null || anchor == null) return false;
  Vector3 eye = motor.transform.position + Vector3.up * 1.8f;
  Vector3 delta = anchor.position - eye;
  if(delta.magnitude > 3.6f || Vector3.Dot(view.lens.transform.forward, delta.normalized) < .45f) return false;
  foreach(var hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude,
                                       Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
   if(hit.transform.IsChildOf(motor.transform) ||
      (facility != null && hit.transform.IsChildOf(facility))) continue;
   return false;
  }
  return true;
 }
  void Update(){if(IsOpen){if(!externalPresentation&&(Input.GetKeyDown(KeyCode.Escape)||Input.GetKeyDown(KeyCode.F)))Close();return;}if(captured){if(Time.frameCount>closeFrame+1&&!Input.GetMouseButton(0)&&!Input.GetMouseButton(1)&&!Input.GetKey(KeyCode.F)&&!Input.GetKey(KeyCode.Escape))Restore();return;}if(!externalPresentation&&Input.GetKeyDown(KeyCode.F))Open();}
 public bool Open() => !externalPresentation && TryOpenAt(interactionAnchor);

 public bool TryOpenAt(Transform anchor) {
  if(!CanInteractAt(anchor)) return false;
  oldMotor=motor.enabled;oldShooter=shooter.enabled;oldView=view.enabled;oldTime=Time.timeScale;oldLock=Cursor.lockState;oldVisible=Cursor.visible;captured=true;IsOpen=true;motor.enabled=false;shooter.enabled=false;view.enabled=false;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;ui?.Show(true);return true;}
 public void Close(){if(!IsOpen)return;IsOpen=false;ui?.Show(false);closeFrame=Time.frameCount;Time.timeScale=oldTime;}
 public void ReleaseExternalPresentation() {
  if(!externalPresentation) return;
  Close();
  Restore();
 }

 void Restore(){if(!captured)return;captured=false;Time.timeScale=oldTime;if(motor!=null)motor.enabled=oldMotor;if(shooter!=null)shooter.enabled=oldShooter;if(view!=null)view.enabled=oldView;Cursor.lockState=oldLock;Cursor.visible=oldVisible;}
 public bool TryPurchase(int index){if(!IsOpen||index<0||index>2)return false;bool capped=index<2?Bought[index]>=3:runtime.projectileCount>=8;if(capped){Message="这件商品已达到上限。";FeedbackAudio.Play(Cue.Denied,transform.position);ui?.Refresh();return false;}if(Gold<prices[index]){Message="试用金币不足，先看看其他商品吧。";FeedbackAudio.Play(Cue.Denied,transform.position);ui?.Refresh();return false;}Gold-=prices[index];Bought[index]++;if(index==0)shooter.rateMultiplier*=1.2f;else if(index==1)runtime.speed*=1.25f;else runtime.projectileCount=Mathf.Min(8,runtime.projectileCount+1);Message="交易成功！升级已立即生效。";FeedbackAudio.Play(Cue.Purchase,transform.position);ui?.Refresh();return true;}
 public string Status(int i)=>i==0?"已购 "+Bought[i]+" / 3":i==1?"已购 "+Bought[i]+" / 3":"当前 "+runtime.projectileCount+" / 8 颗";
 void OnDisable(){if(ui!=null)ui.Show(false);IsOpen=false;Restore();}
 void OnDestroy(){Restore();if(shooter!=null&&shooter.config==runtime)shooter.config=OriginalConfig;if(runtime!=null)Destroy(runtime);}
}
}

