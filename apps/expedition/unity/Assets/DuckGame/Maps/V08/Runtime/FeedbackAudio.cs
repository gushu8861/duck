using UnityEngine;
namespace DuckGame.Maps.V08 {
public enum Cue { Shot, Ready, Environment, Target, Purchase, Denied }
public sealed class FeedbackAudio : MonoBehaviour {
 public AudioClip[] clips;public AudioClip wood;public bool polished; public static FeedbackAudio Instance {get;private set;}
 readonly AudioSource[] voices=new AudioSource[8]; readonly float[] until=new float[8]; readonly float[] last=new float[6];
 public int Played {get;private set;} public int Dropped {get;private set;} public int Peak {get;private set;}
 void Awake(){Instance=this;if(clips!=null)foreach(var clip in clips)if(clip!=null)clip.LoadAudioData();for(int i=0;i<8;i++){voices[i]=polished?new GameObject("Feedback voice "+i).AddComponent<AudioSource>():gameObject.AddComponent<AudioSource>();if(polished)voices[i].transform.SetParent(transform,false);voices[i].playOnAwake=false;voices[i].spatialBlend=0;voices[i].ignoreListenerPause=true;}for(int i=0;i<6;i++)last[i]=-100;}
 public static void Play(Cue cue,Vector3 point){if(Instance!=null)Instance.PlayCue(cue);}
 public static void Impact(Cue cue,Collider surface,Vector3 point){if(Instance==null)return;bool timber=Instance.polished&&cue==Cue.Environment&&surface!=null&&(surface.name.IndexOf("wood",System.StringComparison.OrdinalIgnoreCase)>=0||surface.name.IndexOf("post",System.StringComparison.OrdinalIgnoreCase)>=0||surface.name.IndexOf("trunk",System.StringComparison.OrdinalIgnoreCase)>=0);Instance.PlayCue(cue,timber?Instance.wood:null,point);}
 public void PlayCue(Cue cue,AudioClip variation=null,Vector3? point=null){int c=(int)cue;if(clips==null||c>=clips.Length||clips[c]==null)return;float now=Time.unscaledTime;bool ui=c>=4;if(now-last[c]<(ui?.06f:.045f)){Dropped++;return;}int slot=-1;int start=ui?6:0,end=ui?8:6;for(int i=start;i<end;i++)if(until[i]<=now){slot=i;break;}if(slot<0){Dropped++;return;}var s=voices[slot];s.spatialBlend=polished&&c==2?.65f:0;s.minDistance=3;s.maxDistance=35;s.rolloffMode=AudioRolloffMode.Logarithmic;if(polished&&c==2)s.transform.position=point??transform.position;s.clip=variation!=null?variation:clips[c];s.pitch=ui?1:Random.Range(.96f,1.04f);s.volume=c==0?.35f:c==1?.18f:ui?.45f:polished&&c==3?.44f:.32f;s.Play();until[slot]=now+s.clip.length/s.pitch;last[c]=now;Played++;int active=0;for(int i=0;i<8;i++)if(until[i]>now)active++;Peak=Mathf.Max(Peak,active);}
 void OnDestroy(){if(Instance==this)Instance=null;}
}
}
