using UnityEngine;
namespace DuckGame.Maps.V10 {
public sealed class ForestEnemy:MonoBehaviour,IOrbHitReceiver {
 public Transform body;public string kind;public int Hits{get;private set;}public int LastHitFrame{get;private set;}public Vector3 LastHitPoint{get;private set;}
 Vector3 rest,scale,direction;Quaternion rotation;float age=10;Renderer[] surfaces;MaterialPropertyBlock block;Color[] colors;
 void Awake(){if(body==null){Debug.LogError("Enemy body reference missing",this);enabled=false;return;}rest=body.localPosition;scale=body.localScale;rotation=body.localRotation;surfaces=body.GetComponentsInChildren<Renderer>();block=new MaterialPropertyBlock();colors=new Color[surfaces.Length];for(int i=0;i<colors.Length;i++)colors[i]=surfaces[i].sharedMaterial.GetVector("_Tint");}
 public void ReceiveOrbHit(OrbShotContext shot,OrbImpactContext impact){Hits++;LastHitFrame=Time.frameCount;LastHitPoint=impact.Point;age=0;direction=transform.InverseTransformDirection(shot.Direction);if(shot.Owner!=null)shot.Owner.GetComponent<EnemyHitFeedback>()?.Confirm();}
 void Update(){age+=Time.deltaTime;float k=age<.32f?Mathf.Sin(age*30)*Mathf.Exp(-age*17):0;body.localPosition=rest+Vector3.up*(Mathf.Sin(Time.time*1.6f+transform.position.x)*.014f)+direction*k*.09f;body.localRotation=rotation*Quaternion.Euler(direction.z*k*6,0,-direction.x*k*6);body.localScale=Vector3.Scale(scale,new Vector3(1+k*.035f,1-k*.05f,1+k*.02f));for(int i=0;i<surfaces.Length;i++){block.SetVector("_Tint",Color.Lerp(colors[i],new Color(1,.93f,.70f),age<.07f?.55f:0));surfaces[i].SetPropertyBlock(block);}}
 void OnDisable(){if(body!=null){body.localPosition=rest;body.localRotation=rotation;body.localScale=scale;}}
}
}
