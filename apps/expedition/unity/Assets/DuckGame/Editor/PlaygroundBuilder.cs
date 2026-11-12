using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace DuckGame.EditorTools {
public static class PlaygroundBuilder {
 public const string Root="Assets/DuckGame";public const string ModelPath=Root+"/Art/GooseWizard_v02.fbx";
 public static string LatestScene=>!Directory.Exists(Root+"/Generated")?null:Directory.GetFiles(Root+"/Generated","Playground.unity",SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()?.Replace('\\','/');
 [MenuItem("Duck Game/Build v0.3 Playground")]
 public static void Build(){
  for(int i=0;i<SceneManager.sceneCount;i++){var old=SceneManager.GetSceneAt(i);if(old.isDirty||string.IsNullOrEmpty(old.path)){EditorUtility.DisplayDialog("Scene preserved","Save your current scene before building. Nothing was discarded.","OK");return;}}
  if(!AssetDatabase.IsValidFolder(Root+"/Generated"))AssetDatabase.CreateFolder(Root,"Generated");string folder=AssetDatabase.GenerateUniqueAssetPath(Root+"/Generated/v03");AssetDatabase.CreateFolder(Root+"/Generated",Path.GetFileName(folder));
  var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);importer.animationType=ModelImporterAnimationType.Generic;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.importAnimation=true;importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
  var clips=importer.defaultClipAnimations;foreach(var c in clips)c.loopTime=new[]{"Idle","Walk","Run","WingFlap"}.Any(x=>c.name.EndsWith(x));importer.clipAnimations=clips;importer.SaveAndReimport();
  Shader toon=Shader.Find("Duck Game/Toon URP"),ink=Shader.Find("Duck Game/Outline URP");if(toon==null||ink==null)throw new Exception("Duck Game shaders not compiled.");
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
  var config=ScriptableObject.CreateInstance<OrbConfig>();AssetDatabase.CreateAsset(config,folder+"/OrbConfig.asset");
  Material outline=SaveMaterial(new Material(ink),folder+"/Outline.mat");outline.SetFloat("_Width",.003f);
  Material glow=SaveMaterial(new Material(Shader.Find("Duck Game/Orb Glow URP")),folder+"/OrbGlow.mat");glow.SetVector("_Glow",new Vector4(.25f,2,4,1));
  GameObject rig=new GameObject("PlayerRig");GameObject player=new GameObject("GoosePlayer");player.transform.SetParent(rig.transform);player.transform.position=new Vector3(0,.03f,-6);player.layer=2;
  GameObject visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath),player.transform);visual.name="GooseWizard_v02";
  foreach(var t in visual.GetComponentsInChildren<Transform>())t.gameObject.layer=2;
  var cache=new System.Collections.Generic.Dictionary<string,Material>();var skins=visual.GetComponentsInChildren<SkinnedMeshRenderer>();SkinnedMeshRenderer hat=null,hatOutline=null;
  foreach(var skin in skins){var materials=skin.sharedMaterials;for(int i=0;i<materials.Length;i++){string n=materials[i]!=null?materials[i].name:"Cream";if(!cache.TryGetValue(n,out var mat)){mat=SaveMaterial(new Material(toon),folder+"/"+n+".mat");mat.SetVector("_Tint",Palette(n));cache[n]=mat;}materials[i]=mat;}skin.sharedMaterials=materials;skin.updateWhenOffscreen=true;skin.shadowCastingMode=ShadowCastingMode.Off;
   var edge=new GameObject(skin.name+"_Outline");edge.layer=2;edge.transform.SetParent(skin.transform,false);var sr=edge.AddComponent<SkinnedMeshRenderer>();sr.sharedMesh=skin.sharedMesh;sr.bones=skin.bones;sr.rootBone=skin.rootBone;sr.sharedMaterials=Enumerable.Repeat(outline,skin.sharedMesh.subMeshCount).ToArray();sr.localBounds=skin.localBounds;sr.updateWhenOffscreen=true;sr.shadowCastingMode=ShadowCastingMode.Off;
   if(skin.name=="Sculpted_Hat"){hat=skin;hatOutline=sr;}
  }
  var cc=player.AddComponent<CharacterController>();cc.height=2.60f;cc.radius=.46f;cc.center=new Vector3(0,1.30f,0);cc.stepOffset=.24f;cc.skinWidth=.035f;cc.minMoveDistance=0;
  var anim=visual.GetComponent<Animator>();if(anim==null)anim=visual.AddComponent<Animator>();anim.applyRootMotion=false;anim.runtimeAnimatorController=BuildController(folder);
  var bulge=player.AddComponent<HatBulge>();bulge.hat=hat;bulge.outline=hatOutline;ConfigureHat(bulge,player.transform);
  var cameraGO=new GameObject("ShoulderCamera");cameraGO.transform.SetParent(rig.transform);cameraGO.tag="MainCamera";var cam=cameraGO.AddComponent<Camera>();cam.nearClipPlane=.035f;cam.farClipPlane=180;cam.backgroundColor=new Color(.055f,.08f,.12f);cameraGO.AddComponent<AudioListener>();var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
  var view=cameraGO.AddComponent<ShoulderCamera>();view.player=player.transform;view.hat=bulge;view.lens=cam;view.pitch=4;
  var motor=player.AddComponent<GooseMotor>();motor.controller=cc;motor.animator=anim;motor.view=view;
  var shooter=player.AddComponent<OrbShooter>();shooter.config=config;shooter.hat=bulge;shooter.view=view;shooter.orbMaterial=glow;
  var verify=rig.AddComponent<RuntimeVerification>();verify.shooter=shooter;verify.motor=motor;verify.view=view;verify.enabled=false;
  var idle=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().First(c=>c.name.EndsWith("Idle"));idle.SampleAnimation(visual,0);
  cam.transform.position=player.transform.position+new Vector3(.8f,2.75f,-5.2f);cam.transform.rotation=Quaternion.Euler(4,0,0);
  PrefabUtility.SaveAsPrefabAsset(rig,folder+"/PlayerRig.prefab");
  MakeRange(folder);
  var volumeGO=new GameObject("BloomVolume");var volume=volumeGO.AddComponent<Volume>();volume.isGlobal=true;var profile=ScriptableObject.CreateInstance<VolumeProfile>();var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.7f);bloom.threshold.Override(1.1f);AssetDatabase.CreateAsset(profile,folder+"/BloomProfile.asset");AssetDatabase.AddObjectToAsset(bloom,profile);volume.sharedProfile=profile;
  RenderSettings.ambientLight=new Color(.35f,.42f,.5f);var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(45,-30,0);sun.shadows=LightShadows.Soft;
  foreach(string guid in AssetDatabase.FindAssets("",new[]{folder})){var owned=AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));if(owned!=null)AssetDatabase.SaveAssetIfDirty(owned);}string path=folder+"/Playground.unity";EditorSceneManager.SaveScene(scene,path);EditorSceneManager.OpenScene(path,OpenSceneMode.Single);Selection.activeGameObject=GameObject.Find("GoosePlayer");Debug.Log("DUCK_V03_BUILD_OK "+path);
 }
 static Material SaveMaterial(Material m,string path){AssetDatabase.CreateAsset(m,path);return m;}
 static Vector4 Palette(string n){if(n.Contains("Orange"))return new Vector4(1,.31f,.035f,1);if(n.Contains("Indigo"))return new Vector4(.48f,.49f,.47f,1);if(n.Contains("Gold"))return new Vector4(.51f,.53f,.52f,1);if(n.Contains("Ink"))return new Vector4(.018f,.016f,.032f,1);if(n.Contains("White"))return new Vector4(.97f,.97f,.91f,1);if(n.Contains("Blue"))return new Vector4(.13f,.52f,.73f,1);if(n.Contains("Rim"))return new Vector4(.9f,.35f,.08f,1);if(n.Contains("Feather"))return new Vector4(.52f,.54f,.52f,1);return new Vector4(.91f,.86f,.70f,1);}
 static AnimatorController BuildController(string folder){var ctl=AnimatorController.CreateAnimatorControllerAtPath(folder+"/Goose.controller");ctl.AddParameter("Speed",AnimatorControllerParameterType.Float);var clips=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();var state=ctl.layers[0].stateMachine.AddState("Locomotion");var blend=new BlendTree{name="Locomotion",blendParameter="Speed",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(blend,ctl);blend.AddChild(clips.First(c=>c.name.EndsWith("Idle")),0);blend.AddChild(clips.First(c=>c.name.EndsWith("Walk")),2.2f);blend.AddChild(clips.First(c=>c.name.EndsWith("Run")),4.4f);state.motion=blend;ctl.layers[0].stateMachine.defaultState=state;
 foreach(string n in new[]{"Honk","Peck","WingFlap","Startle"}){ctl.AddParameter(n,AnimatorControllerParameterType.Trigger);var action=ctl.layers[0].stateMachine.AddState(n);action.motion=clips.First(c=>c.name.EndsWith(n));var enter=ctl.layers[0].stateMachine.AddAnyStateTransition(action);enter.AddCondition(AnimatorConditionMode.If,0,n);enter.duration=.12f;enter.canTransitionToSelf=false;var exit=action.AddTransition(state);exit.hasExitTime=true;exit.exitTime=.98f;exit.duration=.15f;}return ctl;}
 static void ConfigureHat(HatBulge b,Transform player){int hi=Array.FindIndex(b.hat.bones,x=>x.name=="Head"),ti=Array.FindIndex(b.hat.bones,x=>x.name=="HatTip");if(hi<0||ti<0)throw new Exception("Hat bones missing");b.head=b.hat.bones[hi];b.tipBone=b.hat.bones[ti];b.meshToHead=b.hat.sharedMesh.bindposes[hi];b.meshToTip=b.hat.sharedMesh.bindposes[ti];b.upInHead=b.head.InverseTransformDirection(player.up).normalized;Vector3[] vertices=b.hat.sharedMesh.vertices.Select(v=>b.meshToHead.MultiplyPoint3x4(v)).ToArray();float top=vertices.Max(v=>Vector3.Dot(v,b.upInHead));float bottom=top-.89f;b.centerLine=new Vector3[17];Vector3 last=Vector3.zero;
 for(int i=0;i<b.centerLine.Length;i++){float h=Mathf.Lerp(bottom,top,i/(float)(b.centerLine.Length-1));var near=vertices.Where(v=>Mathf.Abs(Vector3.Dot(v,b.upInHead)-h)<.055f&&Vector3.ProjectOnPlane(v,b.upInHead).magnitude<.40f).ToArray();Vector3 center=near.Length>0?near.Aggregate(Vector3.zero,(a,v)=>a+v)/near.Length:last;center+=b.upInHead*(h-Vector3.Dot(center,b.upInHead));b.centerLine[i]=center;last=center;}var bounds=b.hat.localBounds;bounds.Expand(.35f);b.hat.localBounds=bounds;b.outline.localBounds=bounds;
 }
 static void MakeRange(string folder){var ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));ground.color=new Color(.12f,.16f,.19f);SaveMaterial(ground,folder+"/Floor.mat");var wall=new Material(ground);wall.color=new Color(.20f,.28f,.32f);SaveMaterial(wall,folder+"/Wall.mat");var orange=new Material(ground);orange.color=new Color(.72f,.26f,.08f);SaveMaterial(orange,folder+"/Target.mat");var white=new Material(ground);white.color=new Color(.8f,.88f,.86f);SaveMaterial(white,folder+"/TargetCenter.mat");Box("Floor",new Vector3(0,-.25f,4),new Vector3(40,.5f,60),ground);Box("Backstop",new Vector3(0,3,26),new Vector3(36,6,.6f),wall);Box("CoverLeft",new Vector3(-5,.65f,1),new Vector3(3,1.3f,1),wall);Box("TallCover",new Vector3(5,2,2),new Vector3(2,4,1),wall);
 for(int row=0;row<2;row++)for(int i=-1;i<=1;i++){var target=new GameObject("Target_"+row+"_"+i);target.transform.position=new Vector3(i*5+.8f,2,8+row*9);var plate=GameObject.CreatePrimitive(PrimitiveType.Cylinder);plate.name="TargetPlate";plate.transform.SetParent(target.transform,false);plate.transform.localRotation=Quaternion.Euler(90,0,0);plate.transform.localScale=new Vector3(.8f,.10f,.8f);plate.GetComponent<Renderer>().sharedMaterial=orange;var dummy=target.AddComponent<TargetDummy>();dummy.surface=plate.GetComponent<Renderer>();var center=GameObject.CreatePrimitive(PrimitiveType.Cylinder);center.name="Bullseye";center.transform.SetParent(target.transform,false);center.transform.localPosition=new Vector3(0,0,-.13f);center.transform.localRotation=Quaternion.Euler(90,0,0);center.transform.localScale=new Vector3(.35f,.035f,.35f);center.GetComponent<Renderer>().sharedMaterial=white;Box("TargetPost",target.transform.position+Vector3.down*1.1f,new Vector3(.08f,1.8f,.08f),wall);}
 }
 static void Box(string name,Vector3 p,Vector3 scale,Material material){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.position=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;}
}
}
