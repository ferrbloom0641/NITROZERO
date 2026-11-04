using UnityEditor;
using UnityEngine;
[CustomEditor(typeof(GreenCarExplosionEnd))]
public sealed class GreenCarExplosionEndEditor:Editor {
 public override void OnInspectorGUI(){var s=(GreenCarExplosionEnd)target;
 EditorGUILayout.HelpBox("촬영 완료한 총알 비행 뒤에 붙이는 4.8초 정면 컷입니다. 시작 0.37초 후 포드가 폭발하고 뒤쪽이 솟구칩니다. Pitch는 차체 들림, Slide Distance는 관성 이동, Explosion Scale은 폭발 크기입니다.",MessageType.Info);
 float offset=s.cutOnly?s.cutStartTime:0;EditorGUI.BeginChangeCheck();float t=EditorGUILayout.Slider("컷 미리보기 (초)",s.previewTime-offset,0,s.duration-offset);
 if(EditorGUI.EndChangeCheck()){s.director.time=t;s.director.Evaluate();s.Evaluate(t+offset);SceneView.RepaintAll();}
 if(GUILayout.Button("시작으로 되돌리기"))s.ResetShot();DrawDefaultInspector();
 }
}
