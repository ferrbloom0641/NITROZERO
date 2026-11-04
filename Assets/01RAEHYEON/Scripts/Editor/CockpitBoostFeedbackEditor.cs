using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CockpitBoostFeedback))]
public sealed class CockpitBoostFeedbackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Play → Game 화면 클릭 → Space: 부스터 흔들림 시작 / 다시 누르면 재시작.\nDuration 0: 계속 유지. Stop Boost: 부드럽게 종료.", MessageType.Info);
        DrawDefaultInspector();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            var boost = (CockpitBoostFeedback)target;
            if (GUILayout.Button("Play Boost")) boost.PlayBoost();
            if (GUILayout.Button("Stop Boost")) boost.StopBoost();
        }
    }
}
