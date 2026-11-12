using UnityEngine;
namespace DuckGame.Maps.V08 {
public sealed class SnailIdle:MonoBehaviour {
 public Transform body,leftEye,rightEye;Vector3 size;Quaternion l,r;
 void Awake(){size=body.localScale;l=leftEye.localRotation;r=rightEye.localRotation;}
 void Update(){float t=Time.time;body.localScale=Vector3.Scale(size,new Vector3(1,1+Mathf.Sin(t*1.7f)*.014f,1));leftEye.localRotation=l*Quaternion.Euler(Mathf.Sin(t*1.2f)*3,0,Mathf.Sin(t)*3);rightEye.localRotation=r*Quaternion.Euler(Mathf.Sin(t*1.2f+.5f)*3,0,Mathf.Sin(t+.8f)*3);}
}
}
