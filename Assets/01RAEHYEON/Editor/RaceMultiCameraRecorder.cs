using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.Recorder.Encoder;
using UnityEngine;
using Unity.Cinemachine;

public sealed class RaceMultiCameraRecorder : EditorWindow
{
    public int width=1920,height=1080,fps=30;
    public float seconds=30;
    RecorderController controller;
    RecorderControllerSettings settings;
    RaceMultiCameraCapture capture;
    RaceCinematicDirector director;
    bool raceUI,cameraUI;
    float originalAspect;
    CinemachineVirtualCameraBase.StandbyUpdateMode[] standby;
    string folder,status="Play 후 시작하면 레이스를 처음부터 3배속으로 녹화합니다.";
    public bool IsRecording => controller!=null && controller.IsRecording();
    public string OutputFolder => folder;
    [MenuItem("Tools/Trailer/Record All Race Cameras")]
    public static void Open(){GetWindow<RaceMultiCameraRecorder>("Race Multi Record");}
    void OnEnable(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayChanged;}
    void OnDisable(){EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayChanged;StopCapture();}
    void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)StopCapture();}
    void Tick(){if(controller!=null && !controller.IsRecording()){StopCapture();status="녹화 완료: "+folder;}Repaint();}
    void OnGUI()
    {
        EditorGUILayout.LabelField("카메라별 동시 녹화 / 3배속",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status,MessageType.Info);
        using(new EditorGUI.DisabledScope(controller!=null))
        {
            width=EditorGUILayout.IntField("Width",width);height=EditorGUILayout.IntField("Height",height);
            fps=EditorGUILayout.IntPopup("FPS",fps,new[]{"30","60"},new[]{30,60});
            seconds=EditorGUILayout.FloatField("영상 길이 (초)",seconds);
            using(new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                if(GUILayout.Button("Restart + Record All Cameras"))
                    try{BeginCapture();}catch(Exception e){status=e.Message;Debug.LogException(e);StopCapture();}
        }
        using(new EditorGUI.DisabledScope(controller==null))if(GUILayout.Button("Stop Recording")){StopCapture();status="녹화 종료: "+folder;}
        if(!string.IsNullOrEmpty(folder) && GUILayout.Button("Open Output Folder"))EditorUtility.RevealInFinder(Path.GetFullPath(folder));
        EditorGUILayout.HelpBox("등록된 각 컷을 별도 MP4로 저장합니다. 녹화 중에는 카메라 전환·재시작·속도 변경을 하지 마세요. 고정 컷은 차량이 도착하기 전까지 빈 도로를 담습니다.",MessageType.None);
    }
    public void BeginCapture()
    {
        if(!Application.isPlaying || controller!=null)throw new InvalidOperationException("Play 중이며 다른 녹화가 진행 중이지 않아야 합니다.");
        if(Resources.FindObjectsOfTypeAll<RecorderWindow>().Any(w=>w.IsRecording()))throw new InvalidOperationException("기존 Recorder 녹화를 먼저 정지해 주세요.");
        director=FindFirstObjectByType<RaceCinematicDirector>();
        if(director==null || !director.enabled || director.race==null || !director.race.Ready || director.shots==null || director.shots.Length==0)throw new InvalidOperationException("자동 주행 씬의 카메라 컨트롤러가 준비되지 않았습니다.");
        if(director.shots.Any(s=>s==null || s.cameraRig==null))throw new InvalidOperationException("등록된 카메라에 빈 항목이 있습니다.");
        if(width<64 || height<64 || width%2!=0 || height%2!=0 || seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds))throw new InvalidOperationException("해상도는 64 이상의 짝수, 길이는 양수로 입력하세요.");
        raceUI=director.race.showControls;cameraUI=director.showControls;originalAspect=director.outputCamera.aspect;
        director.race.showControls=director.showControls=false;director.recordingLocked=true;
        director.race.playbackSpeed=3;director.race.RestartRace();director.race.PresentCurrentState();director.ReplayMove();
        director.outputCamera.aspect=width/(float)height;director.Evaluate(0);
        folder="Recordings/Race_All_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");Directory.CreateDirectory(folder);
        capture=new GameObject("Race simultaneous capture outputs").AddComponent<RaceMultiCameraCapture>();capture.director=director;
        capture.outputs=new Camera[director.shots.Length];standby=new CinemachineVirtualCameraBase.StandbyUpdateMode[director.shots.Length];
        settings=CreateInstance<RecorderControllerSettings>();settings.FrameRate=fps;settings.FrameRatePlayback=FrameRatePlayback.Constant;
        settings.CapFrameRate=true;settings.ExitPlayMode=false;settings.SetRecordModeToFrameInterval(0,Mathf.Max(1,Mathf.RoundToInt(seconds*fps))-1);
        for(int i=0;i<director.shots.Length;i++)
        {
            var shot=director.shots[i];standby[i]=shot.cameraRig.StandbyUpdate;shot.cameraRig.StandbyUpdate=CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            var go=new GameObject("Capture "+shot.name);go.transform.SetParent(capture.transform);var camera=go.AddComponent<Camera>();capture.outputs[i]=camera;
            camera.CopyFrom(director.outputCamera);
            foreach(var source in director.outputCamera.GetComponents<Component>())
                if(source!=null && source.GetType().Name=="UniversalAdditionalCameraData")
                {
                    var copy=go.AddComponent(source.GetType());EditorUtility.CopySerialized(source,copy);
                    // A capture is an independent base camera, without the source camera's overlay stack.
                    var so=new SerializedObject(copy);var stack=so.FindProperty("m_Cameras");if(stack!=null && stack.isArray){stack.ClearArray();so.ApplyModifiedPropertiesWithoutUndo();}
                }
            camera.targetTexture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Race capture "+(i+1)};camera.targetTexture.Create();camera.aspect=width/(float)height;camera.rect=new Rect(0,0,1,1);camera.enabled=true;
            var movie=CreateInstance<MovieRecorderSettings>();movie.name="Race shot "+(i+1);movie.Enabled=true;movie.CaptureAudio=false;movie.CaptureAlpha=false;
            movie.ImageInputSettings=new RenderTextureInputSettings{RenderTexture=camera.targetTexture};
            movie.EncoderSettings=new CoreEncoderSettings{Codec=CoreEncoderSettings.OutputCodec.MP4,EncodingQuality=CoreEncoderSettings.VideoEncodingQuality.High};
            movie.OutputFile=folder+"/"+(i+1).ToString("00")+"_"+string.Concat(shot.name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));
            settings.AddRecorderSettings(movie);
        }
        capture.Synchronize();controller=new RecorderController(settings);controller.PrepareRecording();
        if(!controller.StartRecording())throw new InvalidOperationException("Recorder 시작 실패. Console을 확인하세요.");
        status=director.shots.Length+"개 카메라 녹화 중 · 3배속 · "+fps+"fps";
    }
    public void StopCapture()
    {
        if(controller!=null){controller.StopRecording();controller=null;}
        if(capture!=null){foreach(var camera in capture.outputs)if(camera!=null){var rt=camera.targetTexture;camera.targetTexture=null;camera.enabled=false;if(rt!=null){rt.Release();DestroyImmediate(rt);}}DestroyImmediate(capture.gameObject);capture=null;}
        if(director!=null && standby!=null){for(int i=0;i<standby.Length;i++)if(director.shots[i]!=null)director.shots[i].cameraRig.StandbyUpdate=standby[i];}
        if(director!=null){director.race.showControls=raceUI;director.showControls=cameraUI;director.recordingLocked=false;director.outputCamera.aspect=originalAspect;}
        standby=null;director=null;
        if(settings!=null){foreach(var movie in settings.RecorderSettings.ToArray())if(movie!=null)DestroyImmediate(movie);DestroyImmediate(settings);settings=null;}
    }
}
