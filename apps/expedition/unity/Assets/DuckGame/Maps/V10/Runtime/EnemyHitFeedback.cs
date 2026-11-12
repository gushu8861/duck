using UnityEngine;
namespace DuckGame.Maps.V10 {
public sealed class EnemyHitFeedback:MonoBehaviour {
 public const float Duration=.20f;public int Confirmations{get;private set;}public float Remaining{get;private set;}public int LastFrame{get;private set;}
 OrbShooter shooter;void Awake(){shooter=GetComponent<OrbShooter>();}
 public static float ScaleForHeight(int height)=>height/1080f;
 public void Confirm(){Confirmations++;Remaining=Duration;LastFrame=Time.frameCount;}
 void Update(){Remaining=Mathf.Max(0,Remaining-Time.deltaTime);}
 void OnDisable(){Remaining=0;}
 void OnGUI(){if(Remaining<=0||!shooter.enabled)return;var aim=shooter.view.AimViewport;float scale=ScaleForHeight(Screen.height),x=aim.x*Screen.width,y=(1-aim.y)*Screen.height,age=1-Remaining/Duration;float gap=Mathf.Lerp(16,9,Mathf.SmoothStep(0,1,Mathf.Clamp01(age/.55f)));var old=GUI.color;var matrix=GUI.matrix;GUI.color=new Color(1,.94f,.79f,Mathf.Min(1,Remaining/.08f));for(int i=0;i<4;i++){GUIUtility.RotateAroundPivot(45+i*90,new Vector2(x,y));float fade=Mathf.Min(1,Remaining/.08f);GUI.color=new Color(.045f,.055f,.055f,fade*.85f);GUI.DrawTexture(new Rect(x+(gap-1)*scale,y-2.25f*scale,13*scale,4.5f*scale),Texture2D.whiteTexture);GUI.color=new Color(1,.94f,.79f,fade);GUI.DrawTexture(new Rect(x+gap*scale,y-1.25f*scale,11*scale,2.5f*scale),Texture2D.whiteTexture);GUI.matrix=matrix;}GUI.color=old;}
}
}
