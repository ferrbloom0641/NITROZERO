using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(ExteriorMissileDrift))]
public sealed class ExteriorMissileDriftEditor:Editor
{
 public override void OnInspectorGUI(){var shot=(ExteriorMissileDrift)target;
 EditorGUILayout.HelpBox("8초 360도 드리프트: 90도 미사일 회피 → 270도까지 추가 회전 → 360도 복귀와 부스터 전개. Play 또는 Timeline으로 재생합니다. Camera View로 구도를 고정합니다. Camera Turn Delay로 차체보다 늦게 회전하는 정도를 조절합니다. 부스터 전개 시점은 Exterior Drift Action에서 조절합니다. 타이어 연기는 Play 모드에서 확인합니다.",MessageType.Info);
 EditorGUI.BeginChangeCheck();float t=EditorGUILayout.Slider("장면 미리보기 (초)",shot.previewTime,0,shot.duration);
 if(EditorGUI.EndChangeCheck()){shot.Evaluate(t);SceneView.RepaintAll();}
 if(GUILayout.Button("시작 위치로 되돌리기"))shot.ResetShot();
 EditorGUI.BeginChangeCheck();DrawDefaultInspector();if(EditorGUI.EndChangeCheck()){shot.Evaluate(shot.previewTime);SceneView.RepaintAll();}
 }
}
