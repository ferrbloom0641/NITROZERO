using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using Damin.SceneOnly;
using Damin.VFX.TireSmoke.Progressive;
public static class DMDirectorAuthor
{
 const string Work="C:/UnityProject_Backups/DMDirector_20260914";
 const string Path="Assets/03DAMIN/DM_avoidMissile.unity";
 static DMCinematicDirector film;static Camera cam;static RenderTexture rt;static double began;
 static List<string> report=new List<string>();static HashSet<int> captured=new HashSet<int>();
 static bool finished;static int testStage=0;static Vector3[] starts;
 static double nextLiveCheck;static string lastWait;
 [InitializeOnLoadMethod] static void Queue(){if(Application.dataPath.Replace('\\','/').Equals("C:/UnityProject/NITROQZERO_GIT/NITROZERO/Assets",StringComparison.OrdinalIgnoreCase))EditorApplication.update+=PollLive;}
 static void PollLive(){
  if(EditorApplication.timeSinceStartup<nextLiveCheck)return;nextLiveCheck=EditorApplication.timeSinceStartup+1;
  if(File.Exists(Work+"/LIVE_DONE.txt")||File.Exists(Work+"/LIVE_FAILED.txt")){EditorApplication.update-=PollLive;return;}
  var scene=SceneManager.GetActiveScene();
  string state="Scene="+scene.path+"; dirty="+scene.isDirty+"; play="+EditorApplication.isPlayingOrWillChangePlaymode+"; compiling="+EditorApplication.isCompiling+"; updating="+EditorApplication.isUpdating+"; prefabStage="+(PrefabStageUtility.GetCurrentPrefabStage()!=null);
  if(state!=lastWait){File.WriteAllText(Work+"/LIVE_WAIT.txt",state);lastWait=state;}
  if(scene.path!=Path||scene.isDirty||PrefabStageUtility.GetCurrentPrefabStage()!=null||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
  Live();
 }
 static Transform Point(Transform parent,string name,Vector3 local){var g=new GameObject(name);Undo.RegisterCreatedObjectUndo(g,"DM window marker");g.transform.SetParent(parent,false);g.transform.localPosition=local;return g.transform;}
 static GameObject Instance(string path,Transform parent,string name){
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!asset)throw new Exception("Missing VFX asset: "+path);
  var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);Undo.RegisterCreatedObjectUndo(g,"DM scene VFX instance");
  PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);g.name=name;g.transform.localPosition=Vector3.zero;g.transform.localRotation=Quaternion.identity;return g;
 }
 static DMCinematicDirector Configure(Scene scene){
  if(scene.GetRootGameObjects().Any(g=>g.name=="DM_CinematicDirector"))throw new Exception("Director already exists");
  var rig=scene.GetRootGameObjects().Single(g=>g.name=="DM_CinematicCameras").GetComponent<DMShotCameraRig>();
  var root=new GameObject("DM_CinematicDirector");Undo.RegisterCreatedObjectUndo(root,"DM whole cinematic");
  var d=root.AddComponent<DMCinematicDirector>();d.cameras=rig;d.opening=rig.opening;d.missileSequence=rig.missileSequence;
  d.redCar=rig.opening.vehicles.Single(t=>t.name=="Red_Car_Final");d.blueCar=rig.opening.vehicles.Single(t=>t.name=="Blue_Car_Final");d.khakiCar=rig.opening.vehicles.Single(t=>t.name=="Green_Car_Final");
  d.preparationSlide=d.khakiCar.GetComponent<DMScene_MissilePreparationSlide>();d.redCruise=d.redCar.GetComponent<TrailerCruiseMotion>();d.redBoosterDeployment=d.redCar.GetComponentInChildren<BoosterDeploymentController>(true);
  d.passengerWindow=Point(d.redCar,"DM_PassengerWindow_Path",new Vector3(.62f,.24f,.20f));
  d.driverWindow=Point(d.redCar,"DM_DriverWindow_Path",new Vector3(-.62f,.24f,.20f));
  d.interiorCameraReady=false;d.raceSpeedKph=rig.opening.speedKph;d.playOnStart=true;rig.autoPlayOpening=false;
  var smokeObj=Instance("Assets/Prefabs/VFX/PF_TireSmoke_Progressive.prefab",root.transform,"DM_RedDriftSmoke");
  var smoke=smokeObj.GetComponent<ProgressiveTireSmokeController>();if(!smoke)throw new Exception("Missing tire smoke binding");
  smoke.vehicleRoot=d.redCar;smoke.rearLeft.wheel=d.redCar.GetComponentsInChildren<Transform>(true).First(t=>t.name=="RL");smoke.rearRight.wheel=d.redCar.GetComponentsInChildren<Transform>(true).First(t=>t.name=="RR");
  smoke.rearLeft.wheelRadius=smoke.rearRight.wheelRadius=.35f;smoke.rearLeft.wheelWidth=smoke.rearRight.wheelWidth=.25f;
  smoke.previewOnPlay=false;smoke.loopPreview=false;smoke.smokePower=0;smoke.buildUpTime=.2f;smoke.fadeOutTime=.6f;d.driftSmoke=smoke;
  d.boostNozzles=d.redCar.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="rocket+nozzle+3d+model").Cast<Renderer>().ToArray();
  if(d.boostNozzles.Length!=2)throw new Exception("Expected two red booster nozzles");
  d.boostEffects=new GameObject[2];
  for(int i=0;i<2;i++){var g=Instance("Assets/Prefabs/VFX/PF_RedBooster_1.prefab",d.redCar,"DM_ReaccelerationBoost_"+i);g.transform.position=d.boostNozzles[i].bounds.center;g.transform.localScale=Vector3.one*.35f;g.SetActive(false);d.boostEffects[i]=g;}
  rig.SelectShot(1);return d;
 }
 static void Live(){
  if(!File.Exists(Work+"/TEST_PASS.txt")||File.Exists(Work+"/LIVE_DONE.txt")||File.Exists(Work+"/LIVE_FAILED.txt"))return;
  if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Live;return;}
  int group=-1;try{
   var scene=SceneManager.GetActiveScene();
   if(scene.path!=Path||scene.isDirty||PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new Exception("Save DM scene and stop Play before apply");
   if(!File.ReadAllBytes(Application.dataPath+"/03DAMIN/DM_avoidMissile.unity").SequenceEqual(File.ReadAllBytes(Work+"/Before.unity")))throw new Exception("Scene changed since backup");
   Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("DM whole cinematic");
   foreach(var g in scene.GetRootGameObjects().Where(g=>new[]{"DM_CinematicCameras","Main Camera","Red_Car_Final"}.Contains(g.name)))Undo.RegisterFullObjectHierarchyUndo(g,"DM whole cinematic");
   var d=Configure(scene);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
   Undo.CollapseUndoOperations(group);Selection.activeGameObject=d.gameObject;
   File.WriteAllText(Work+"/LIVE_DONE.txt","DM scene saved: 13 timed stages, director, scene-owned drift/boost effects and window markers. Shared assets unchanged. Interior camera fallback explicitly enabled.");
  }catch(Exception e){if(group>=0)Undo.RevertAllDownToGroup(group);File.WriteAllText(Work+"/LIVE_FAILED.txt",e.ToString());Debug.LogWarning(e.Message);}
 }
 public static void Test(){
  try{
   if(!Application.dataPath.Replace('\\','/').Contains("/AuthoringProject/Assets"))throw new Exception("Scratch only");
   var scene=EditorSceneManager.OpenScene("Assets/RedAuthoringTools/DMDirectorTest.unity");film=Configure(scene);
   starts=film.opening.vehicles.Select(t=>t.position).ToArray();
   EditorSceneManager.SaveScene(scene,"Assets/RedAuthoringTools/DMDirectorConfigured.unity");
   EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
   EditorApplication.playModeStateChanged+=State;began=EditorApplication.timeSinceStartup;EditorApplication.isPlaying=true;
  }catch(Exception e){Fail(e);}
 }
 static void State(PlayModeStateChange s){
  if(s==PlayModeStateChange.EnteredPlayMode){film=UnityEngine.Object.FindFirstObjectByType<DMCinematicDirector>();cam=film.cameras.outputCamera;rt=new RenderTexture(1280,720,24);rt.Create();cam.targetTexture=rt;EditorApplication.update+=Tick;}
  if(s==PlayModeStateChange.EnteredEditMode&&finished){File.WriteAllLines(Work+"/TEST_PASS.txt",report);EditorApplication.Exit(0);}
 }
 static void Capture(string name){
  RenderPipeline.SubmitRenderRequest(cam,new RenderPipeline.StandardRequest{destination=rt});
  var old=RenderTexture.active;RenderTexture.active=rt;var img=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
  img.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);img.Apply();RenderTexture.active=old;File.WriteAllBytes(Work+"/"+name+".jpg",img.EncodeToJPG(85));UnityEngine.Object.Destroy(img);
 }
 static void Tick(){
  try{
   if(EditorApplication.timeSinceStartup-began>240)throw new Exception("Play timeout");
   if(film.FilmTime<=0)return;if(!string.IsNullOrEmpty(film.Error))throw new Exception(film.Error);
   foreach(var v in film.opening.vehicles)if(!float.IsFinite(v.position.x)||Mathf.Abs(v.position.z)>9)throw new Exception("Vehicle left road: "+v.name);
   if(testStage==0){
    var durations=film.ActiveDurations;float start=0;for(int i=0;i<film.Phase;i++)start+=durations[i];
    if(film.FilmTime>=start+durations[film.Phase]*.5f&&captured.Add(film.Phase)){
     int phase=film.Phase;int[] shots={1,2,3,4,5,6,7,8,9,9,9,8,8};
     if(film.cameras.activeShot!=shots[phase])throw new Exception("Wrong shot at phase "+phase);
     if(Vector3.Distance(cam.transform.position,film.cameras.shots[shots[phase]-1].camera.transform.position)>.05f)throw new Exception("Main camera wrong at phase "+phase);
     if(phase<6&&film.missileSequence.HasFired)throw new Exception("Early missile");
     if(phase>=6&&!film.missileSequence.HasFired)throw new Exception("Missing fired missile");
     if(phase>=8&&phase<=10){
      if(Vector3.Angle(film.redCar.forward,film.opening.roadForward)<80)throw new Exception("Car not sideways");
     }
     Capture("Phase_"+(phase+1).ToString("00"));
     report.Add("Phase "+(phase+1)+" t="+film.FilmTime.ToString("F2")+" shot="+film.cameras.activeShot+" red="+film.redCar.position.ToString("F2")+" fired="+film.missileSequence.HasFired);
    }
    if(film.Completed){
     if(captured.Count!=13)throw new Exception("Missed stages "+captured.Count);
     if(Quaternion.Angle(film.redCar.rotation,Quaternion.LookRotation(film.opening.roadForward))>.1f)throw new Exception("Red did not finish 360 turn");
     if(film.boostEffects.Any(g=>!g.activeSelf))throw new Exception("No boost effect at final");
     report.Add("All 13 stages normal Play PASS; default duration="+film.FilmDuration);
     film.timing.dropBack+=1;film.Replay();film.ManualSimulation=true;
     if(Mathf.Abs(film.FilmDuration-21)>.01f)throw new Exception("Duration accumulation wrong");
     foreach(var p in film.opening.vehicles.Select((v,i)=>Vector3.Distance(v.position,starts[i])))if(p>.01f)throw new Exception("Replay placement mismatch");
     film.Advance(7);if(film.Phase!=4||film.missileSequence.HasFired)throw new Exception("Retimed dropback failed");
     film.Advance(4);if(film.Phase!=5||film.missileSequence.HasFired)throw new Exception("Retimed prep failed");
     film.Advance(.3f);if(film.Phase!=6||!film.missileSequence.HasFired)throw new Exception("Retimed launch failed");
     report.Add("Dropback +1 second shifts all later stages +1 second; replay and retimed launch PASS");
     film.timing.groundPass=float.NaN;film.timing.preparation=-1;
     var safe=film.timing.Values();if(safe.Any(v=>!float.IsFinite(v)||v<.1f))throw new Exception("Invalid duration not sanitized");
     film.timing.groundPass=2;film.timing.preparation=3.6f;
     film.Replay();film.ManualSimulation=true;film.Advance(14+1+.6f);
     var projectile=film.missileSequence.ActiveMissile.transform;
     var expected=(film.passengerWindow.position+film.driverWindow.position)*.5f;
     if(Vector3.Distance(projectile.position,expected)>.12f)throw new Exception("Passage misses window midpoint: "+Vector3.Distance(projectile.position,expected));
     report.Add("Window waypoint midpoint PASS (path only; mesh clearance is a production review)");
     report.Add("Shared missile fireTime="+film.missileSequence.motion.fireTime+"; interior explicitly uses top-view placeholder.");
     finished=true;EditorApplication.update-=Tick;EditorApplication.isPlaying=false;
    }
   }
  }catch(Exception e){Fail(e);}
 }
 static void Fail(Exception e){File.WriteAllText(Work+"/TEST_FAILED.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);}
}
