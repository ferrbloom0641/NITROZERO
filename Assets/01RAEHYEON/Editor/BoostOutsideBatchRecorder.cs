using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public sealed class BoostOutsideBatchRecorder : EditorWindow
{
    const string Pending="BoostOutside.RecordPending";
    internal static BoostOutsideRecordingRunner Runner;
    [SerializeField] int resolution, frameRate;
    internal static string Status="녹화 준비", Folder;
    static BoostOutsideBatchRecorder()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending,false))
            {
                SessionState.SetBool(Pending,false);
                Begin(true);
            }
            if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Pending,false);
        };
        AssemblyReloadEvents.beforeAssemblyReload+=()=>{if(Runner!=null)Runner.Finish("스크립트 갱신으로 녹화를 중단했습니다.");};
    }
    [MenuItem("Tools/Boost Outside/구도별 일괄 녹화")]
    public static void Open()=>GetWindow<BoostOutsideBatchRecorder>("부스터 일괄 녹화").Show();
    void OnInspectorUpdate()=>Repaint();
    void OnGUI()
    {
        EditorGUILayout.HelpBox("모든 구도를 처음부터 끝까지 각각 MP4로 저장합니다. 같은 시점의 차량 동작과 카메라 흔들림이 반복됩니다. 녹화 중 Game 뷰를 열어 두세요.",MessageType.Info);
        bool busy=Runner!=null || SessionState.GetBool(Pending,false);
        using(new EditorGUI.DisabledScope(busy))
        {
            resolution=EditorGUILayout.Popup("해상도",resolution,new[]{"1920 × 1080","3840 × 2160"});
            frameRate=EditorGUILayout.Popup("프레임",frameRate,new[]{"30 fps","60 fps"});
            EditorGUILayout.LabelField("형식","MP4 / H.264 High · 오디오 포함");
            if(GUILayout.Button("모든 구도 녹화 시작",GUILayout.Height(34)))
            {
                try
                {
                    Validate();
                    SessionState.SetInt("BoostOutside.Width",resolution==0?1920:3840);
                    SessionState.SetInt("BoostOutside.Fps",frameRate==0?30:60);
                    if(EditorApplication.isPlaying)Begin(false);
                    else {SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;}
                }
                catch(Exception e){Status=e.Message;Debug.LogException(e);}
            }
        }
        EditorGUILayout.LabelField(Status,EditorStyles.wordWrappedLabel);
        if(busy && GUILayout.Button("녹화 중단 (완료 파일 유지)"))
        {
            SessionState.SetBool(Pending,false);
            if(Runner!=null)Runner.Finish("사용자가 녹화를 중단했습니다.");
        }
        if(GUILayout.Button("저장 폴더 열기"))
        {
            string path=Folder??Path.GetFullPath("Recordings/BoostOutside");
            Directory.CreateDirectory(path);EditorUtility.RevealInFinder(path);
        }
    }
    static BoostOutsideDirector Validate()
    {
        if(SceneManager.GetActiveScene().name!="RH_boostOutside")throw new InvalidOperationException("RH_boostOutside 씬을 먼저 열어 주세요.");
        var all=UnityEngine.Object.FindObjectsByType<BoostOutsideDirector>(FindObjectsSortMode.None);
        if(all.Length!=1)throw new InvalidOperationException("활성 Boost Outside Director가 하나여야 합니다.");
        var d=all[0];
        if(!d.enabled || !d.outputCamera || !d.outputCamera.enabled || !d.brain || !d.brain.enabled || !d.redCar || !d.blueCar || d.takeDuration<=0 || d.shots.Length==0 || d.shots.Any(s=>s==null || !s.camera || !s.camera.isActiveAndEnabled))
            throw new InvalidOperationException("차량·출력 카메라·구도 연결과 활성 상태를 확인해 주세요.");
        if(Resources.FindObjectsOfTypeAll<RecorderWindow>().Any(w=>w.IsRecording()))throw new InvalidOperationException("다른 Recorder 녹화를 먼저 정지해 주세요.");
        return d;
    }
    static void Begin(bool exitPlay)
    {
        try
        {
            var d=Validate();
            PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.GameView);
            var gameType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            EditorWindow.GetWindow(gameType).Show();
            Folder=Path.GetFullPath("Recordings/BoostOutside/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(Folder);
            Runner=new BoostOutsideRecordingRunner();
            Runner.Initialize(d,Folder,SessionState.GetInt("BoostOutside.Width",1920),SessionState.GetInt("BoostOutside.Fps",30),exitPlay);
        }
        catch(Exception e){Status="녹화 시작 실패: "+e.Message;Debug.LogException(e);if(Runner!=null)Runner.Finish(Status);else if(exitPlay)EditorApplication.isPlaying=false;}
    }
}
