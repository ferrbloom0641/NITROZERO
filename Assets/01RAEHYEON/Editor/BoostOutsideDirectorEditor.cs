using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BoostOutsideDirector))]
public sealed class BoostOutsideDirectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var d=(BoostOutsideDirector)target;
        if(GUILayout.Button("구도별 일괄 녹화…"))BoostOutsideBatchRecorder.Open();
        if(d.batchRecording){EditorGUILayout.HelpBox("구도별 녹화 중입니다. 녹화 창에서 진행 상황을 확인하거나 중단할 수 있습니다.",MessageType.Info);return;}
        EditorGUILayout.HelpBox("F1–F11: camera | R: full replay | Space: pause | H: controls. All takes use the same 28-second run. Controls are hidden by default for recording.",MessageType.Info);
        DrawDefaultInspector();
        if(GUILayout.Button("Apply current time / camera")){d.Sample(d.time);SceneView.RepaintAll();}
        if(GUILayout.Button("Cue selected shot (2 seconds before action)")){d.CueSelected();SceneView.RepaintAll();}
        if(Application.isPlaying&&GUILayout.Button("Replay entire take"))d.Replay();
    }
}
