using UnityEditor;
using UnityEngine;

namespace YUJEONG
{
    [CustomEditor(typeof(CMShot02CameraShake))]
    public class CMShot02CameraShakeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var shake = (CMShot02CameraShake)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.7f, 0.2f);
            EditorGUILayout.LabelField("🎥 CM_Shot02 전용 독립 카메라 진동기", titleStyle);
            EditorGUILayout.HelpBox("다른 스크립트나 시스템과 일절 독립되어 동작합니다.\n기준 좌표(Base Pose) 고정 방식으로 수천 프레임이 지나도 위로 뜨지 않고 제자리를 유지합니다.", MessageType.Info);

            EditorGUILayout.Space(4);

            // 원클릭 토글 버튼
            EditorGUILayout.BeginHorizontal();
            if (shake.enableShake)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("⏹️ 카메라 진동 끄기 (OFF)", GUILayout.Height(30)))
                {
                    Undo.RecordObject(shake, "Toggle Shake OFF");
                    shake.enableShake = false;
                    EditorUtility.SetDirty(shake);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
                if (GUILayout.Button("▶️ 카메라 진동 켜기 (ON)", GUILayout.Height(30)))
                {
                    Undo.RecordObject(shake, "Toggle Shake ON");
                    shake.enableShake = true;
                    EditorUtility.SetDirty(shake);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("💥 순간 충격 테스트 (쿵!)", GUILayout.Height(26)))
            {
                shake.TriggerImpact(1.2f);
            }
            if (GUILayout.Button("🔄 원래 기준 위치로 복귀", GUILayout.Height(26)))
            {
                Undo.RecordObject(shake.transform, "Reset To Base Pose");
                shake.ResetToBasePose();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            if (GUILayout.Button("📍 현재 카메라 구도를 기준점(Base)으로 저장", GUILayout.Height(24)))
            {
                Undo.RecordObject(shake, "Set Current As Base Pose");
                shake.SetCurrentAsBasePose();
                EditorUtility.SetDirty(shake);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }

        [MenuItem("Tools/NITROZERO/CM_Shot02에 [독립 카메라 진동기] 장착")]
        public static void AttachToCMShot02()
        {
            GameObject shot02 = GameObject.Find("CM_Shot02");
            if (shot02 == null)
            {
                EditorUtility.DisplayDialog("알림", "씬에서 'CM_Shot02' 오브젝트를 찾을 수 없습니다.", "확인");
                return;
            }

            Undo.RecordObject(shot02, "Attach CMShot02CameraShake");
            var comp = shot02.GetComponent<CMShot02CameraShake>();
            if (comp == null) comp = Undo.AddComponent<CMShot02CameraShake>(shot02);

            comp.InitBasePose();
            Selection.activeGameObject = shot02;
            EditorUtility.SetDirty(shot02);
            EditorUtility.DisplayDialog("완료", "CM_Shot02에 [독립 카메라 진동기]가 성공적으로 장착/갱신되었습니다!\n기준 위치 고정 방식으로 카메라가 위로 뜨지 않고 제자리를 완벽하게 유지합니다.", "확인");
        }
    }
}
