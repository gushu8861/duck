using UnityEngine;
namespace DuckGame.Maps.V08 {
public sealed class CombatFeedback:MonoBehaviour {
 public bool polished;public bool externalMarker;
 OrbShooter shooter;float confirmation;public int TargetConfirmations {get;private set;}
 void Awake(){shooter=GetComponent<OrbShooter>();}
 void OnEnable(){shooter.Fired+=Shot;shooter.Readied+=Ready;}
 void OnDisable(){shooter.Fired-=Shot;shooter.Readied-=Ready;confirmation=0;}
 void Shot(double t,Vector3 p,Vector3 d){FeedbackAudio.Play(Cue.Shot,p);}
 void Ready(){FeedbackAudio.Play(Cue.Ready,transform.position);}
 public void TargetHit(){confirmation=.16f;TargetConfirmations++;}
 void Update(){confirmation=Mathf.Max(0,confirmation-Time.deltaTime);}
 void OnGUI(){if(externalMarker||confirmation<=0||!shooter.enabled)return;Vector2 p=shooter.view.AimViewport;float x=p.x*Screen.width,y=(1-p.y)*Screen.height;Color old=GUI.color;GUI.color=new Color(1,.92f,.73f,Mathf.Min(1,confirmation/.05f));if(polished){Matrix4x4 matrix=GUI.matrix;float gap=Mathf.Lerp(6,11,confirmation/.16f);for(int i=0;i<4;i++){float a=45+i*90;GUIUtility.RotateAroundPivot(a,new Vector2(x,y));GUI.DrawTexture(new Rect(x+gap,y-1,7,2),Texture2D.whiteTexture);GUI.matrix=matrix;}}else for(int i=0;i<4;i++){float sx=(i%2==0?-1:1),sy=(i<2?-1:1);GUI.DrawTexture(new Rect(x+sx*11-2,y+sy*11-2,4,4),Texture2D.whiteTexture);}GUI.color=old;}
}
}
