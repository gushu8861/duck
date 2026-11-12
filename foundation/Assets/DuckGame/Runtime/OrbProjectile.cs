using UnityEngine;
namespace DuckGame {
public sealed class OrbProjectile:MonoBehaviour {
 Vector3 direction;OrbConfig config;OrbShotContext context;float age;bool alive;
 public static int HitCount;public static int SpawnCount;
 public void Launch(OrbConfig settings,OrbShotContext shot){config=settings;context=shot;direction=shot.Direction;alive=true;SpawnCount++;Destroy(gameObject,settings.lifetime+.2f);}
 void Update(){if(!alive)return;float distance=config.speed*Time.deltaTime;if(Sweep(transform.position,config.radius,direction,distance,out var hit)){Impact(hit);return;}transform.position+=direction*distance;age+=Time.deltaTime;if(age>config.lifetime){alive=false;Destroy(gameObject);}}
 public static bool Sweep(Vector3 origin,float radius,Vector3 direction,float distance,out RaycastHit hit){return Physics.SphereCast(origin,radius,direction,out hit,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);}
 public void Impact(RaycastHit hit){if(!alive)return;alive=false;transform.position=hit.point+hit.normal*.015f;ApplyImpact(config,context,new OrbImpactContext(hit.collider,hit.point,hit.normal,true));Destroy(gameObject);}
 public static void ApplyImpact(OrbConfig config,OrbShotContext shot,OrbImpactContext impact){
  if(config==null||impact.Collider==null)return;
  HitCount++;
  var receiver=impact.Collider.GetComponentInParent<IOrbHitReceiver>();
  receiver?.ReceiveOrbHit(shot,impact);
  DuckGame.Maps.V08.FeedbackAudio.Impact(receiver!=null?DuckGame.Maps.V08.Cue.Target:DuckGame.Maps.V08.Cue.Environment,impact.Collider,impact.Point);
  if(receiver!=null&&shot.Owner!=null)shot.Owner.GetComponent<DuckGame.Maps.V08.CombatFeedback>()?.TargetHit();
  config.impactEffect?.OnImpact(shot,impact);
  if(!config.replaceDefaultImpact)OrbImpactPool.Emit(impact.Point,impact.Normal,receiver!=null?config.enemyImpactColor:config.orbColor,receiver!=null,config.impactScale);
 }
 public static void MakeImpact(Vector3 point,Color color){OrbImpactPool.Emit(point,Vector3.up,color,false);}
}
}
