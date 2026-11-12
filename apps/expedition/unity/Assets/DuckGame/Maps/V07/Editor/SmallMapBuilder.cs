using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using DuckGame.V04;
namespace DuckGame.Maps.V07 {
[InitializeOnLoad] public static class SmallMapBuilder {
 const string Root="Assets/DuckGame/Maps/V07", Gen=Root+"/Generated", Request="docs/verification/v07/request.txt";
 static Material grass,path,edge,stone,stoneLight,rock,trunk,leaf,leafLight,cloth,wood,metal,orange,cream;
 static Transform map; static int meshIndex;
 static SmallMapBuilder(){EditorApplication.update+=Tick;}
 static void Tick(){if(EditorApplication.isCompiling||!File.Exists(Request))return;string cmd=File.ReadAllText(Request).Trim();if(cmd.Length==0)return;if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}File.WriteAllText(Request,"");try{if(cmd=="build")Build();else if(cmd=="refine")Refine();}catch(Exception e){Debug.LogException(e);File.WriteAllText("docs/verification/v07/build-error.txt",e.ToString());}}
 [MenuItem("Duck Game/Maps/Build Wizard Grove v0.7")]
 public static void Build(){
  var source=SceneManager.GetActiveScene();if(source.isDirty){Directory.CreateDirectory("docs/verification/v07/baseline");if(!EditorSceneManager.SaveScene(source,"docs/verification/v07/baseline/UnsavedScene.unity",true))throw new Exception("Unsaved scene safety copy failed; stopped.");}
  Directory.CreateDirectory(Gen);AssetDatabase.Refresh();string dest=Gen+"/WizardGrove.unity";if(File.Exists(dest))throw new Exception("Map already exists; preserve it before rebuilding.");
  string src="Assets/DuckGame/V04/Generated/MovementPlayground.unity";if(!AssetDatabase.CopyAsset(src,dest))throw new Exception("Scene copy failed");
  var scene=EditorSceneManager.OpenScene(dest,OpenSceneMode.Single);
  var motor=UnityEngine.Object.FindObjectOfType<GooseMotorV04>();if(motor==null)throw new Exception("Valid player missing");var rig=motor.transform.root.gameObject;
  foreach(var go in scene.GetRootGameObjects())if(go!=rig&&go.name!="Sun"&&go.name!="BloomVolume")UnityEngine.Object.DestroyImmediate(go);
  foreach(var probe in rig.GetComponentsInChildren<MovementVerificationV04>(true))probe.enabled=false;
  motor.InjectInput=false;motor.GetComponentInChildren<OrbShooter>().InjectInput=false;motor.view.InjectInput=false;
  motor.transform.SetPositionAndRotation(new Vector3(0,.04f,-15.5f),Quaternion.identity);motor.view.pitch=8;
  map=new GameObject("Wizard Grove | Compact Training Map").transform;
  grass=Mat("Meadow",new Color(.29f,.38f,.27f));path=Mat("Warm earth",new Color(.59f,.48f,.32f));edge=Mat("Path verge",new Color(.40f,.43f,.29f));stone=Mat("Sandstone",new Color(.61f,.59f,.48f));stoneLight=Mat("Stone cap",new Color(.76f,.72f,.58f));rock=Mat("Boundary slate",new Color(.34f,.40f,.36f));trunk=Mat("Tree bark",new Color(.30f,.25f,.19f));leaf=Mat("Sage canopy",new Color(.30f,.43f,.34f));leafLight=Mat("Canopy light",new Color(.43f,.53f,.38f));cloth=Mat("Merchant sage canvas",new Color(.35f,.49f,.47f));wood=Mat("Oak",new Color(.47f,.34f,.22f));metal=Mat("Silver trim",new Color(.66f,.70f,.66f));orange=Mat("Target terracotta",new Color(.75f,.32f,.15f));cream=Mat("Target ivory",new Color(.92f,.87f,.66f));
  Box("Solid meadow foundation",new Vector3(0,-.6f,0),new Vector3(48,1.2f,48),grass);
  // A single continuous broad loop, with a clear central approach.
  Strip("West loop",new[]{new Vector3(0,0,-16),new Vector3(-12,0,-11),new Vector3(-13,0,7),new Vector3(-8,0,18),new Vector3(0,0,18)},6.2f,path,.025f);
  Strip("East loop",new[]{new Vector3(0,0,-16),new Vector3(12,0,-11),new Vector3(13,0,7),new Vector3(8,0,18),new Vector3(0,0,18)},6.2f,path,.026f);
  Disc("Rounded spawn junction",new Vector3(0,.015f,-15),5,.02f,path,false); Strip("Central promenade",new[]{new Vector3(0,0,-17),new Vector3(0,0,14)},7,path,.027f);
  Disc("Central earth clearing",new Vector3(0,.015f,1),8,.024f,path,false);
  // Low broken stone ring: the south-facing entrance remains unobstructed.
  for(int i=0;i<12;i++){float a=i*30*Mathf.Deg2Rad;if(i>=7&&i<=10)continue;Vector3 p=new Vector3(Mathf.Cos(a)*5.8f,.18f,Mathf.Sin(a)*5.8f+1);var g=Box("Broken ritual kerb",p,new Vector3(2.3f,.36f,.65f),stoneLight);g.transform.rotation=Quaternion.Euler(0,-i*30,0);}
  Disc("Inset ritual seal",new Vector3(0,.044f,1),2.4f,.018f,stone,false);for(int i=0;i<8;i++){float a=i*45*Mathf.Deg2Rad;var g=Box("Silver rune",new Vector3(Mathf.Cos(a)*1.8f,.067f,1+Mathf.Sin(a)*1.8f),new Vector3(.12f,.018f,.46f),metal,false);g.transform.rotation=Quaternion.Euler(0,-i*45,0);}
  Arch(new Vector3(0,0,15));
  // Western jump terrace, away from the perimeter.
  Box("Terrace lower step",new Vector3(-8,.4f,2),new Vector3(4.5f,.8f,5),stone);Box("Terrace upper step",new Vector3(-8,.8f,5.7f),new Vector3(4.5f,1.6f,2.4f),stoneLight);
  for(int i=0;i<3;i++){Vector3 p=new Vector3(7+i%2*1.4f,0,-3+i*5);Rock("Rounded cover cluster",p,new Vector3(2.6f,1.8f+i*.35f,2.1f),stone);Rock("Cover companion",p+new Vector3(1.6f,0,.7f),new Vector3(1.3f,.85f,1.3f),stoneLight);}
  Target(new Vector3(-5,0,12),0);Target(new Vector3(0,0,19),0);Target(new Vector3(5,0,12),0);Target(new Vector3(16.5f,0,3),-70);
  Merchant(new Vector3(-14,0,-16));
  // Continuous physical cliffs, with clustered faceted crags and high crowns.
  Box("North cliff core",new Vector3(0,4.5f,23),new Vector3(48,9,3),rock);Box("South cliff core",new Vector3(0,4.5f,-23),new Vector3(48,9,3),rock);Box("West cliff core",new Vector3(-23,4.5f,0),new Vector3(3,9,44),rock);Box("East cliff core",new Vector3(23,4.5f,0),new Vector3(3,9,44),rock);
  for(int side=0;side<4;side++)for(int i=0;i<6;i++){float t=-20+i*8;Vector3 p=side<2?new Vector3(t,0,side==0?23.7f:-23.7f):new Vector3(side==2?-23.7f:23.7f,0,t);Rock("Cliff crag",p,new Vector3(7,10+(i%3),7),rock);if(i%2==0){Vector3 q=side<2?new Vector3(t,0,side==0?25:-25):new Vector3(side==2?-25:25,0,t);Tree(q,1+i%3*.13f);}}
  // Grouped near-edge shrubs, no collision on foliage.
  foreach(Vector3 p in new[]{new Vector3(-18,0,6),new Vector3(18,0,-9),new Vector3(-18,0,16),new Vector3(17,0,16)}){Rock("Mossy edge stones",p,new Vector3(2.1f,1.1f,2.8f),rock);for(int i=0;i<3;i++)Sphere("Sage shrub",p+new Vector3(i*.7f-1,.55f,1),new Vector3(1.5f,1.1f,1.4f),leaf,false);}
  RenderSettings.ambientLight=new Color(.62f,.67f,.58f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.65f,.72f,.66f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=45;RenderSettings.fogEndDistance=105;
  var sun=UnityEngine.Object.FindObjectsOfType<Light>().FirstOrDefault(x=>x.type==LightType.Directional);if(sun!=null){sun.intensity=1.15f;sun.color=new Color(1,.93f,.78f);sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.shadows=LightShadows.Soft;}
  EditorSceneManager.playModeStartScene=null;if(!EditorSceneManager.SaveScene(scene,dest))throw new Exception("Map save failed");AssetDatabase.SaveAssets();Selection.activeGameObject=map.gameObject;SceneView.lastActiveSceneView?.FrameSelected();
  File.WriteAllText("docs/verification/v07/build.txt","Scene="+dest+"\nObjects="+scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Length)+"\nRenderers="+UnityEngine.Object.FindObjectsOfType<Renderer>().Length+"\nColliders="+UnityEngine.Object.FindObjectsOfType<Collider>().Length);Debug.Log("V07_MAP_BUILD_OK "+dest);
 }
 static void Refine(){
  var scene=SceneManager.GetActiveScene();if(scene.path!=Gen+"/WizardGrove.unity")throw new Exception("Refine only the V07 map");
  map=GameObject.Find("Wizard Grove | Compact Training Map").transform;path=AssetDatabase.LoadAssetAtPath<Material>(Gen+"/Warm earth.mat");
  foreach(string n in new[]{"West loop","East loop","Rounded spawn junction"}){var go=map.Find(n);if(go!=null)UnityEngine.Object.DestroyImmediate(go.gameObject);}
  Strip("West loop",new[]{new Vector3(0,0,-16),new Vector3(-12,0,-11),new Vector3(-13,0,7),new Vector3(-8,0,18),new Vector3(0,0,18)},6.2f,path,.025f);
  Strip("East loop",new[]{new Vector3(0,0,-16),new Vector3(12,0,-11),new Vector3(13,0,7),new Vector3(8,0,18),new Vector3(0,0,18)},6.2f,path,.026f);
  Disc("Rounded spawn junction",new Vector3(0,.015f,-15),5,.02f,path,false);
  foreach(var target in UnityEngine.Object.FindObjectsOfType<TargetDummy>())FixTarget(target.transform);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Refined map save failed");AssetDatabase.SaveAssets();Debug.Log("V07_MAP_REFINE_OK");
 }
 static Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",.04f);AssetDatabase.CreateAsset(m,Gen+"/"+name+".mat");return m;}
 static GameObject Prim(string name,PrimitiveType type,Vector3 p,Vector3 s,Material m,bool collide=true){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(map);g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;if(!collide)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
 static GameObject Box(string n,Vector3 p,Vector3 s,Material m,bool c=true)=>Prim(n,PrimitiveType.Cube,p,s,m,c);
 static GameObject Sphere(string n,Vector3 p,Vector3 s,Material m,bool c=true)=>Prim(n,PrimitiveType.Sphere,p,s,m,c);
 static GameObject Disc(string n,Vector3 p,float r,float h,Material m,bool c=true)=>Prim(n,PrimitiveType.Cylinder,p,new Vector3(r*2,h*.5f,r*2),m,c);
 static void Strip(string n,Vector3[] points,float width,Material mat,float y){var v=new List<Vector3>();var tris=new List<int>();for(int i=0;i<points.Length;i++){Vector3 d=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(0,i-1)]).normalized;Vector3 right=Vector3.Cross(Vector3.up,d)*width*.5f;v.Add(points[i]-right+Vector3.up*y);v.Add(points[i]+right+Vector3.up*y);if(i>0){int j=i*2;tris.AddRange(new[]{j-2,j,j-1,j-1,j,j+1});}}MeshObj(n,v.ToArray(),tris.ToArray(),mat,false);}
 static GameObject MeshObj(string n,Vector3[] v,int[] t,Material mat,bool collide){var mesh=new Mesh{name=n};mesh.vertices=v;mesh.triangles=t;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Gen+"/Mesh_"+(meshIndex++)+".asset"));var go=new GameObject(n);go.transform.SetParent(map);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collide)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;}
 static void Rock(string n,Vector3 p,Vector3 s,Material m){var v=new List<Vector3>();var t=new List<int>();int k=7;for(int ring=0;ring<3;ring++)for(int i=0;i<k;i++){float a=(i*360f/k+ring*12)*Mathf.Deg2Rad;float radius=ring==0?.48f:ring==1?.53f:.29f;v.Add(p+Vector3.Scale(new Vector3(Mathf.Cos(a)*radius,ring==0?0:ring==1?.55f:1,Mathf.Sin(a)*radius),s));}for(int r=0;r<2;r++)for(int i=0;i<k;i++){int a=r*k+i,b=r*k+(i+1)%k,c=a+k,d=b+k;t.AddRange(new[]{a,c,b,b,c,d});}for(int i=1;i<k-1;i++)t.AddRange(new[]{k*2,k*2+i+1,k*2+i});MeshObj(n,v.ToArray(),t.ToArray(),m,true);}
 static void Tree(Vector3 p,float s){Prim("Trunk",PrimitiveType.Cylinder,p+Vector3.up*5*s,new Vector3(.9f,5*s,.9f),trunk);Sphere("Sage crown",p+Vector3.up*11*s,new Vector3(8,5.5f,7)*s,leaf,false);Sphere("Sunward crown",p+new Vector3(-1.7f,12.4f,0)*s,new Vector3(5.6f,4,5.6f)*s,leafLight,false);}
 static void Arch(Vector3 p){for(int side=-1;side<=1;side+=2){for(int i=0;i<3;i++)Box("Ancient arch pier",p+new Vector3(side*4,1+i*1.75f,0),new Vector3(1.65f,1.65f,2),i%2==0?stone:stoneLight);Disc("Pier foot",p+new Vector3(side*4,.2f,0),1.2f,.4f,stoneLight);}
  for(int i=0;i<9;i++){float a=(15+i*18.75f)*Mathf.Deg2Rad;Vector3 q=p+new Vector3(Mathf.Cos(a)*4,4.4f+Mathf.Sin(a)*4,0);var g=Box("Arch voussoir",q,new Vector3(1.55f,1.4f,2),i%3==0?stoneLight:stone);g.transform.rotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);}
  Rock("Fallen arch fragment",p+new Vector3(-6.5f,0,-1),new Vector3(1.7f,.9f,2),stoneLight);
 }
 static void Target(Vector3 p,float yaw){var root=new GameObject("Practice target");root.transform.SetParent(map);root.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));var post=Box("Target post",Vector3.zero,new Vector3(.25f,2.8f,.25f),wood);post.transform.SetParent(root.transform,false);post.transform.localPosition=Vector3.up*1.4f;var plate=Disc("Target terracotta plate",Vector3.zero,1, .18f,orange);plate.transform.SetParent(root.transform,false);plate.transform.localPosition=Vector3.up*2.9f;plate.transform.localRotation=Quaternion.Euler(90,0,0);var center=Disc("Target ivory ring",Vector3.zero,.57f,.06f,cream);center.transform.SetParent(root.transform,false);center.transform.localPosition=new Vector3(0,2.9f,-.12f);center.transform.localRotation=Quaternion.Euler(90,0,0);var dot=Disc("Target center",Vector3.zero,.22f,.04f,orange);dot.transform.SetParent(root.transform,false);dot.transform.localPosition=new Vector3(0,2.9f,-.16f);dot.transform.localRotation=Quaternion.Euler(90,0,0);FixTarget(root.transform);var dummy=root.AddComponent<TargetDummy>();dummy.surface=plate.GetComponent<Renderer>();}
 static void FixTarget(Transform target){foreach(var t in target.GetComponentsInChildren<Transform>()){bool plate=t.name=="Target terracotta plate";if(!plate&&t.name!="Target ivory ring"&&t.name!="Target center")continue;foreach(var c in t.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(c);if(plate)t.gameObject.AddComponent<MeshCollider>().sharedMesh=t.GetComponent<MeshFilter>().sharedMesh;}} static void Merchant(Vector3 p){Box("Merchant corner stone pad",p+Vector3.up*.09f,new Vector3(7,.18f,5),stone);foreach(float x in new[]{-2.8f,2.8f})foreach(float z in new[]{-1.7f,1.7f})Box("Awning post",p+new Vector3(x,2.3f,z),new Vector3(.18f,4.6f,.18f),wood);var roof=Box("Sage canvas awning",p+new Vector3(0,4.5f,0),new Vector3(6.4f,.18f,4.3f),cloth);roof.transform.rotation=Quaternion.Euler(0,0,-6);Box("Unstaffed merchant counter",p+new Vector3(0,.9f,.5f),new Vector3(4,1.6f,1.2f),wood);for(int i=0;i<3;i++){Box("Supply crate",p+new Vector3(-2+i*1.2f,.6f,-.9f),new Vector3(.95f,1,.9f),wood);Disc("Silver jar",p+new Vector3(-1+i,1.95f,.5f),.25f,.5f,metal,false);} }
}
}






