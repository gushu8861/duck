using UnityEngine;
namespace DuckGame {
public sealed class TargetDummy:MonoBehaviour,IOrbHitReceiver {
 public int Hits {get;private set;}public Renderer surface;public bool directional;float flash;Color initial;Material owned;Vector3 scale,position,kickDirection;Quaternion rotation;float hitAge=1;
 void Start(){if(surface!=null){owned=surface.material;initial=owned.color;scale=surface.transform.localScale;position=surface.transform.localPosition;rotation=surface.transform.localRotation;}}
 void OnDestroy(){if(owned!=null)Destroy(owned);}
 public void ReceiveOrbHit(OrbShotContext shot,OrbImpactContext impact){kickDirection=surface!=null&&surface.transform.parent!=null?surface.transform.parent.InverseTransformDirection(shot.Direction):shot.Direction;hitAge=0;Hit();}
 public void Hit(){Hits++;flash=.22f;}
 void Update(){if(surface==null||owned==null)return;flash=Mathf.MoveTowards(flash,0,Time.deltaTime);owned.color=flash>0?new Color(1,.90f,.55f):initial;if(directional){hitAge+=Time.deltaTime;float kick=hitAge<.25f?Mathf.Sin(hitAge*28)*Mathf.Exp(-hitAge*21):0;surface.transform.localPosition=position+kickDirection*kick*.055f;surface.transform.localRotation=rotation*Quaternion.Euler(kick*kickDirection.z*4,0,-kick*kickDirection.x*4);surface.transform.localScale=Vector3.Scale(scale,new Vector3(1+kick*.018f,1-kick*.025f,1));return;}float legacy=Mathf.Sin((1-flash/.22f)*Mathf.PI*2)*flash/.22f;surface.transform.localScale=Vector3.Scale(scale,new Vector3(1+legacy*.07f,1-legacy*.06f,1));surface.transform.localRotation=rotation*Quaternion.Euler(legacy*7,0,0);}
}
}
