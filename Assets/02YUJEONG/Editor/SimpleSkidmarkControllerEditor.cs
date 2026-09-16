using UnityEditor;
using UnityEngine;

namespace YUJEONG
{
    [CustomEditor(typeof(SimpleSkidmarkController))]
    public class SimpleSkidmarkControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (SimpleSkidmarkController)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.9f, 0.4f, 0.2f);
            EditorGUILayout.LabelField("🏁 자동 타이어 스키드마크 컨트롤러", titleStyle);
            EditorGUILayout.HelpBox($"앞바퀴가 지정 각도(현재: ±{controller.steerAngleThreshold}°) 이상 꺾일 때만 자동으로 뒷바퀴에 검은 타이어 자국이 깔립니다.", MessageType.Info);

            EditorGUILayout.Space(4);

            // ON / OFF 토글 버튼
            EditorGUILayout.BeginHorizontal();
            if (controller.enableSkidmarks)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("⏹️ 스키드마크 끄기 (OFF)", GUILayout.Height(30)))
                {
                    Undo.RecordObject(controller, "Toggle Skidmarks OFF");
                    controller.enableSkidmarks = false;
                    EditorUtility.SetDirty(controller);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.4f, 1f, 0.4f);
                if (GUILayout.Button("▶️ 스키드마크 켜기 (ON)", GUILayout.Height(30)))
                {
                    Undo.RecordObject(controller, "Toggle Skidmarks ON");
                    controller.enableSkidmarks = true;
                    EditorUtility.SetDirty(controller);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("🔥 권장 설정 원클릭 적용 (±8도, 굵기 0.42m, 바닥 밀착)", GUILayout.Height(28)))
            {
                Undo.RecordObject(controller, "Apply Recommended Skidmark Settings");
                controller.ApplyRecommendedSettings();
                EditorUtility.SetDirty(controller);
                Debug.Log("[SimpleSkidmarkController] 권장 설정(조향 ±8도, 굵기 0.42m, 바닥 밀착)이 성공적으로 적용되었습니다!");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }

        [MenuItem("Tools/NITROZERO/파란차에 [자동 스키드마크] 장착")]
        public static void AttachToBlueCar()
        {
            AttachToCar("Blue_Car_Final_Booster");
        }

        [MenuItem("Tools/NITROZERO/빨간차에 [자동 스키드마크] 장착")]
        public static void AttachToRedCar()
        {
            AttachToCar("Red_Car_Final_Booster");
        }

        private static void AttachToCar(string carName)
        {
            GameObject car = GameObject.Find(carName);
            if (car == null)
            {
                EditorUtility.DisplayDialog("알림", $"씬에서 '{carName}' 차량을 찾을 수 없습니다.", "확인");
                return;
            }

            Undo.RecordObject(car, "Attach SimpleSkidmarkController");
            var comp = car.GetComponent<SimpleSkidmarkController>();
            if (comp == null) comp = Undo.AddComponent<SimpleSkidmarkController>(car);

            comp.ApplyRecommendedSettings();
            comp.AutoFindWheels();
            Selection.activeGameObject = car;
            EditorUtility.SetDirty(car);
            EditorUtility.DisplayDialog("완료", $"'{carName}'에 [자동 스키드마크] 컨트롤러가 성공적으로 장착/업데이트되었습니다!\n앞바퀴가 ±8도 이상 꺾일 때 굵고 선명한 자국이 바닥에 밀착됩니다.", "확인");
        }
    }
}
