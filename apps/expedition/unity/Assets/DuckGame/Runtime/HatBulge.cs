using UnityEngine;
using System.Collections.Generic;
namespace DuckGame {
public sealed class HatBulge:MonoBehaviour {
 public SkinnedMeshRenderer hat,outline;
 public Transform head,tipBone;
 public Matrix4x4 meshToHead,meshToTip;
 public Vector3 upInHead=Vector3.up;
 public Vector3[] centerLine;
 public float amplitude=.085f,width=.13f;
 Mesh instance;Vector3[] basis,vertices;float[] along;Vector3[] radial;int[] normalGroup;Vector3[] normalSums;readonly List<Vector3> normals=new List<Vector3>();
 public float Travel {get;set;}
 public bool Active {get;set;}
 public bool Waiting {get;set;}
 public Vector3 TipWorld=>tipBone.TransformPoint(meshToTip.MultiplyPoint3x4(meshToHead.inverse.MultiplyPoint3x4(centerLine[centerLine.Length-1])));
 public Vector3 TipDirection => (TipWorld-PointWorld(.88f)).sqrMagnitude>.000001f ? (TipWorld-PointWorld(.88f)).normalized : tipBone.up;
 public Vector3 PointWorld(float t){Vector3 p=Center(t);float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.78f,t));Vector3 rest=meshToHead.inverse.MultiplyPoint3x4(p);return Vector3.Lerp(head.TransformPoint(p),tipBone.TransformPoint(meshToTip.MultiplyPoint3x4(rest)),blend);}
 void Awake(){instance=Instantiate(hat.sharedMesh);instance.name="RuntimeHatLocalBulge";basis=instance.vertices;vertices=new Vector3[basis.Length];along=new float[basis.Length];radial=new Vector3[basis.Length];float start=Vector3.Dot(centerLine[0],upInHead),span=Vector3.Dot(centerLine[centerLine.Length-1]-centerLine[0],upInHead);
 for(int i=0;i<basis.Length;i++){Vector3 h=meshToHead.MultiplyPoint3x4(basis[i]);along[i]=(Vector3.Dot(h,upInHead)-start)/span;Vector3 d=h-Center(along[i]);Vector3 planar=Vector3.ProjectOnPlane(d,upInHead);radial[i]=planar.normalized*Mathf.SmoothStep(0,1,planar.magnitude/.025f);}BuildNormalGroups();hat.sharedMesh=instance;if(outline!=null)outline.sharedMesh=instance;
 }
 void BuildNormalGroups(){var groups=new Dictionary<Vector3Int,List<int>>();var representatives=new List<Vector3>();Vector3[] original=instance.normals;normalGroup=new int[basis.Length];
 for(int i=0;i<basis.Length;i++){Vector3 v=basis[i]*100000;var key=new Vector3Int(Mathf.RoundToInt(v.x),Mathf.RoundToInt(v.y),Mathf.RoundToInt(v.z));if(!groups.TryGetValue(key,out var possible)){possible=new List<int>();groups[key]=possible;}int group=-1;foreach(int g in possible)if(Vector3.Dot(original[i],representatives[g])>.5f){group=g;break;}if(group<0){group=representatives.Count;representatives.Add(original[i]);possible.Add(group);}normalGroup[i]=group;}normalSums=new Vector3[representatives.Count];}
 void SmoothWeldedNormals(){instance.GetNormals(normals);System.Array.Clear(normalSums,0,normalSums.Length);for(int i=0;i<normals.Count;i++)normalSums[normalGroup[i]]+=normals[i];for(int i=0;i<normals.Count;i++)normals[i]=normalSums[normalGroup[i]].normalized;instance.SetNormals(normals);}
 Vector3 Center(float t){t=Mathf.Clamp01(t)*(centerLine.Length-1);int a=Mathf.Min((int)t,centerLine.Length-2);return Vector3.Lerp(centerLine[a],centerLine[a+1],t-a);}
 void LateUpdate(){float center=Waiting ? .89f : Mathf.Lerp(.10f,.91f,Travel);float strength=Active?(Waiting ? .045f : amplitude):0;Matrix4x4 inverse=meshToHead.inverse;
 for(int i=0;i<basis.Length;i++){float d=(along[i]-center)/width;float tipFade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.93f,1f,along[i]));float weight=Mathf.Exp(-d*d*2)*Mathf.SmoothStep(0,1,along[i]*8)*tipFade;vertices[i]=basis[i]+inverse.MultiplyVector(radial[i]*strength*weight);}instance.vertices=vertices;instance.RecalculateNormals();SmoothWeldedNormals();instance.RecalculateBounds();
 }
 void OnDestroy(){if(instance!=null)Destroy(instance);}
}
}
