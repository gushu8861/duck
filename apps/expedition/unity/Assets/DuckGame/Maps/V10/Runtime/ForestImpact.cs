using UnityEngine;
namespace DuckGame.Maps.V10 {
public sealed class ForestImpact:OrbEffect {
 public Material ring,streak;
 public override void OnImpact(OrbShotContext shot,OrbImpactContext impact){ForestImpactPool.Emit(this,impact.Point,impact.Normal,impact.Collider.GetComponentInParent<ForestEnemy>()!=null);}
}
public sealed class ForestImpactPool:MonoBehaviour {
 const int Capacity=12;static ForestImpactPool current;Slot[] slots;int cursor;MaterialPropertyBlock block;
 sealed class Slot{public GameObject root;public Transform ring;public LineRenderer[] lines;public Renderer face;public float age;public bool enemy,active;}
 public static void Warmup(ForestImpact effect){if(current==null){current=new GameObject("V10 short impact pool").AddComponent<ForestImpactPool>();current.Initialize(effect);}}
 public static void Emit(ForestImpact effect,Vector3 point,Vector3 normal,bool enemy){Warmup(effect);current.Play(point,normal,enemy);}
 void Initialize(ForestImpact e){slots=new Slot[Capacity];block=new MaterialPropertyBlock();for(int i=0;i<Capacity;i++){var s=new Slot{root=new GameObject("Short impact "+i),lines=new LineRenderer[4]};s.root.transform.SetParent(transform,false);var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="Thin contact ring";q.layer=2;q.GetComponent<Collider>().enabled=false;Destroy(q.GetComponent<Collider>());q.transform.SetParent(s.root.transform,false);s.ring=q.transform;s.face=q.GetComponent<Renderer>();s.face.sharedMaterial=e.ring;for(int j=0;j<4;j++){var go=new GameObject("Short spark");go.transform.SetParent(s.root.transform,false);var l=go.AddComponent<LineRenderer>();l.useWorldSpace=false;l.positionCount=2;l.sharedMaterial=e.streak;l.startWidth=.018f;l.endWidth=.002f;l.numCapVertices=2;s.lines[j]=l;}s.root.SetActive(false);slots[i]=s;}}
 void Play(Vector3 p,Vector3 n,bool enemy){var s=slots[cursor++%Capacity];s.root.transform.SetPositionAndRotation(p+n*.025f,Quaternion.LookRotation(n));s.enemy=enemy;s.age=0;s.active=true;s.root.SetActive(true);Pose(s);}
 void Update(){if(slots==null)return;foreach(var s in slots)if(s.active){s.age+=Time.deltaTime;if(s.age>(s.enemy?.20f:.13f)){s.active=false;s.root.SetActive(false);}else Pose(s);}}
 void Pose(Slot s){float t=s.age/(s.enemy?.20f:.13f),r=Mathf.Lerp(.10f,s.enemy?.34f:.21f,t);s.ring.localScale=Vector3.one*r*2;Color c=s.enemy?new Color(1,.83f,.48f,(1-t)*.65f):new Color(.35f,.87f,1,(1-t)*.4f);block.SetColor("_Tint",c);s.face.SetPropertyBlock(block);for(int j=0;j<4;j++){float a=j*Mathf.PI*.5f+.4f;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);s.lines[j].SetPosition(0,d*r*.45f);s.lines[j].SetPosition(1,d*(r*.65f+.06f)+Vector3.forward*.035f);s.lines[j].startColor=c;s.lines[j].endColor=new Color(c.r,c.g,c.b,0);s.lines[j].SetPropertyBlock(block);}}
 void OnDestroy(){if(current==this)current=null;}
}
}
