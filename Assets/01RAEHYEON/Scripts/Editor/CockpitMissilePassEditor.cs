using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

[CustomEditor(typeof(CockpitMissilePass))]
public sealed class CockpitMissilePassEditor : Editor
{
    double lastTime;
    bool previewPlaying;
    void OnEnable() => EditorApplication.update += Tick;
    void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (!Application.isPlaying && target) ((CockpitMissilePass)target).ResetShot();
    }
    public override void OnInspectorGUI()
    {
        var shot = (CockpitMissilePass)target;
        EditorGUILayout.HelpBox("CAM_06 실내 미사일 패스 · 8.5초\nTimeline 또는 아래 미리보기로 확인하세요. 시안 마커 Entry / Exit를 옮기면 통과 경로가 바뀝니다.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        float time = EditorGUILayout.Slider("Preview (seconds)", shot.previewTime, 0, shot.duration);
        if (EditorGUI.EndChangeCheck()) { shot.Evaluate(time); SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate(); }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(previewPlaying ? "Pause preview" : "Play preview"))
            {
                previewPlaying = !previewPlaying;
                if (shot.previewTime >= shot.duration) shot.ResetShot();
                lastTime = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("Reset")) { previewPlaying = false; shot.previewTime = 0; shot.ResetShot(); SceneView.RepaintAll(); }
            if (GUILayout.Button("Select Timeline")) Selection.activeGameObject = shot.GetComponent<PlayableDirector>().gameObject;
        }
        DrawDefaultInspector();
    }
    void Tick()
    {
        if (!previewPlaying || !target || Application.isPlaying) return;
        var shot = (CockpitMissilePass)target;
        double now = EditorApplication.timeSinceStartup;
        shot.Evaluate(shot.previewTime + (float)(now - lastTime));
        lastTime = now;
        if (shot.previewTime >= shot.duration) previewPlaying = false;
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        Repaint();
    }
}
