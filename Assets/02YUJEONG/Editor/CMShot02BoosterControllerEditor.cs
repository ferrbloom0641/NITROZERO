using UnityEditor;
using UnityEngine;

namespace YUJEONG
{
    [CustomEditor(typeof(CMShot02BoosterController))]
    public class CMShot02BoosterControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (CMShot02BoosterController)target;

            serializedObject.Update();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.3f, 0.8f, 1f);
            EditorGUILayout.LabelField("🎬 CM_Shot02 부스터 & 이펙트 제어 센터", titleStyle);
            EditorGUILayout.HelpBox("부스터를 사용하지 않는 씬일 때 아래 두 체크박스를 꺼두시면 차량 부스터 변형 및 불꽃 이펙트가 완전히 비활성화됩니다.", MessageType.Info);

            EditorGUILayout.Space(4);

            // 1. 부스터 파츠 토글
            SerializedProperty propBooster = serializedObject.FindProperty("enableBooster");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🚀 부스터 파츠 (노즐/날개 전개)", EditorStyles.boldLabel, GUILayout.Width(190));
            bool newBooster = EditorGUILayout.Toggle(propBooster.boolValue);
            if (newBooster != propBooster.boolValue)
            {
                propBooster.boolValue = newBooster;
            }
            EditorGUILayout.EndHorizontal();

            // 2. 부스터 이펙트 토글
            SerializedProperty propEffects = serializedObject.FindProperty("enableBoosterEffects");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔥 부스터 이펙트 (화염/파티클/네온)", EditorStyles.boldLabel, GUILayout.Width(190));
            bool newEffects = EditorGUILayout.Toggle(propEffects.boolValue);
            if (newEffects != propEffects.boolValue)
            {
                propEffects.boolValue = newEffects;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);

            // 빠른 프리셋 버튼
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("🚫 부스터 & 이펙트 전체 끄기 (권장)", GUILayout.Height(28)))
            {
                propBooster.boolValue = false;
                propEffects.boolValue = false;
            }

            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("⚡ 부스터 & 이펙트 전체 켜기", GUILayout.Height(28)))
            {
                propBooster.boolValue = true;
                propEffects.boolValue = true;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // 기본 속성들 그리기
            DrawDefaultInspector();

            if (serializedObject.ApplyModifiedProperties())
            {
                controller.ApplySettings();
                EditorUtility.SetDirty(controller);
            }
        }

        [MenuItem("Tools/NITROZERO/CM_Shot02에 부스터 제어 스크립트 붙이기")]
        public static void AttachToCMShot02()
        {
            GameObject shot02 = GameObject.Find("CM_Shot02");
            if (shot02 == null)
            {
                EditorUtility.DisplayDialog("알림", "씬에서 'CM_Shot02' 오브젝트를 찾을 수 없습니다.", "확인");
                return;
            }

            var comp = shot02.GetComponent<CMShot02BoosterController>();
            if (comp == null)
            {
                comp = Undo.AddComponent<CMShot02BoosterController>(shot02);
            }

            comp.EnableBooster = false;
            comp.EnableBoosterEffects = false;
            comp.AutoFindReferences();
            comp.ApplySettings();

            Selection.activeGameObject = shot02;
            EditorUtility.SetDirty(shot02);
            EditorUtility.DisplayDialog("완료", "CM_Shot02에 부스터 제어 스크립트가 추가되었으며, 부스터 및 이펙트가 꺼짐(OFF) 상태로 세팅되었습니다!", "확인");
        }
    }
}
