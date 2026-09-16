using UnityEditor;
using UnityEngine;

namespace YUJEONG
{
    [CustomEditor(typeof(CameraWindBlastShock))]
    public class CameraWindBlastShockEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var targetScript = (CameraWindBlastShock)target;

            EditorGUILayout.Space(6);
            // =========================================================================
            // 🎬 1. 360° 수직 텀블링 틸트 & 밤하늘 엔딩 섹션 (Pitch-Flip Cam)
            // =========================================================================
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle tumbleTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            tumbleTitleStyle.normal.textColor = new Color(0.2f, 0.85f, 1f);
            EditorGUILayout.LabelField("🎬 CM_Shot01 360° 수직 텀블링 틸트 & 밤하늘 엔딩", tumbleTitleStyle);
            EditorGUILayout.HelpBox("차가 달려오는 모습(사진 1) ➔ 카메라 아래로 통과(사진 3) ➔ 거꾸로 뒤집힌 채 멀어지는 뒷모습(사진 4, 5) ➔ 밤하늘 응시(엔딩)로 이어지는 시네마틱 연출입니다.\n(빨간색 Rotate Tool 링을 주욱 돌리는 연출)", MessageType.Info);

            EditorGUILayout.Space(4);

            // 1-1. 마스터 ON / OFF 토글 버튼
            EditorGUILayout.BeginHorizontal();
            if (targetScript.enablePitchFlipPass)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("⏹️ 360° 수직 텀블링 끄기 (OFF)", GUILayout.Height(32)))
                {
                    Undo.RecordObject(targetScript, "Toggle Pitch Flip OFF");
                    targetScript.enablePitchFlipPass = false;
                    EditorUtility.SetDirty(targetScript);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.2f, 0.85f, 1f);
                if (GUILayout.Button("▶️ 360° 수직 텀블링 켜기 (ON - 사진 1~5번 구도)", GUILayout.Height(32)))
                {
                    Undo.RecordObject(targetScript, "Toggle Pitch Flip ON");
                    targetScript.ApplyPitchFlipDefaultValues();
                    targetScript.ApplyPitchFlipPose(0f);
                    EditorUtility.SetDirty(targetScript);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // 1-2. 사진 1~5번 기본 각도/좌표 원클릭 적용
            GUI.backgroundColor = new Color(0.7f, 0.92f, 1f);
            if (GUILayout.Button("🔥 사진 1~5번 6단계 각도/좌표 기본값 원클릭 리셋/적용", GUILayout.Height(28)))
            {
                Undo.RecordObject(targetScript, "Apply Pitch Flip Default Values");
                targetScript.ApplyPitchFlipDefaultValues();
                targetScript.ApplyPitchFlipPose(0f);
                EditorUtility.SetDirty(targetScript);
                Debug.Log("[CameraWindBlastShock] 사진 1~5번 6단계 각도 및 고정 위치가 성공적으로 적용되었습니다!");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            // 1-3. 에디터 실시간 프리뷰 슬라이더 (게임 실행 안해도 씬 뷰에서 확인)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            targetScript.previewPitchFlip = EditorGUILayout.ToggleLeft("🛠️ 에디터에서 실시간 텀블링 각도 프리뷰 (Slider)", targetScript.previewPitchFlip, EditorStyles.boldLabel);
            if (targetScript.previewPitchFlip)
            {
                targetScript.previewPitchFlipProgress = EditorGUILayout.Slider("텀블링 진행도 (0~1)", targetScript.previewPitchFlipProgress, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetScript.transform, "Preview Pitch Flip");
                    targetScript.ApplyPitchFlipPose(targetScript.previewPitchFlipProgress);
                }

                EditorGUILayout.HelpBox(
                    "0.00: 사진 1번 (정면 원거리 접근, 2.17°)\n" +
                    "0.20: 사진 2번 (근거리 접근 틸트, 16.48°)\n" +
                    "0.40: 사진 3번 (수직 하향 통과, 72.69°)\n" +
                    "0.65: 사진 4번 (거꾸로 뒤집힌 후면 추종, 153.13°)\n" +
                    "0.85: 사진 5번 (거꾸로 지평선 샷, 177.51°)\n" +
                    "1.00: 6번 엔딩 (신비로운 밤하늘 응시 정지, 265.0°)", MessageType.None);
            }
            else
            {
                if (EditorGUI.EndChangeCheck())
                {
                    targetScript.ApplyPitchFlipPose(0f);
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            // =========================================================================
            // 🎬 2. 원형 회전 훑기 (Orbit Sweep Cam - 이전 구도)
            // =========================================================================
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.65f, 0.15f);
            EditorGUILayout.LabelField("🎬 CM_Shot01 원형 회전 훑기 (Orbit Sweep Cam)", titleStyle);
            EditorGUILayout.HelpBox("노란색 회전 링을 돌리듯 차 앞 대각선(사진 1) ➔ 완벽한 측면(사진 3) ➔ 후면(사진 5)으로 훑는 시네마틱 연출입니다.", MessageType.None);

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            bool isOrbitOn = targetScript.enableOrbitSweepCam && targetScript.enableVerticalAdSweepCam;
            if (isOrbitOn)
            {
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("⏹️ 원형 회전 훑기 끄기 (OFF)", GUILayout.Height(26)))
                {
                    Undo.RecordObject(targetScript, "Toggle Orbit Sweep OFF");
                    targetScript.enableOrbitSweepCam = false;
                    targetScript.enableVerticalAdSweepCam = false;
                    EditorUtility.SetDirty(targetScript);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.35f, 1f, 0.45f);
                if (GUILayout.Button("▶️ 원형 회전 훑기 켜기 (ON)", GUILayout.Height(26)))
                {
                    Undo.RecordObject(targetScript, "Toggle Orbit Sweep ON");
                    targetScript.ApplyOrbitSweepDefaultWaypoints();
                    targetScript.enablePitchFlipPass = false; // 텀블링 끄기
                    EditorUtility.SetDirty(targetScript);
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
            if (GUILayout.Button("🔥 훑기 5단계 궤적 좌표 원클릭 리셋/적용", GUILayout.Height(24)))
            {
                Undo.RecordObject(targetScript, "Apply Orbit Sweep Default Waypoints");
                targetScript.ApplyOrbitSweepDefaultWaypoints();
                targetScript.ApplyOrbitSweepPose(0f);
                EditorUtility.SetDirty(targetScript);
                Debug.Log("[CameraWindBlastShock] 사진 1~5번 5단계 궤적(위치+회전)이 성공적으로 적용되었습니다!");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            targetScript.previewOrbitSweep = EditorGUILayout.ToggleLeft("🛠️ 에디터에서 실시간 훑기 프리뷰 (Slider)", targetScript.previewOrbitSweep, EditorStyles.boldLabel);
            if (targetScript.previewOrbitSweep)
            {
                targetScript.previewOrbitProgress = EditorGUILayout.Slider("훑기 진행도 (0~1)", targetScript.previewOrbitProgress, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetScript.transform, "Preview Orbit Sweep");
                    targetScript.ApplyOrbitSweepPose(targetScript.previewOrbitProgress);
                }
            }
            else
            {
                if (EditorGUI.EndChangeCheck())
                {
                    targetScript.ApplyOrbitSweepPose(0f);
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }

        [MenuItem("Tools/NITROZERO/CM_Shot01 360° 수직 텀블링 켜기 & 좌표 적용")]
        public static void SetupPitchFlipOnCMShot01()
        {
            GameObject camObj = GameObject.Find("CM_Shot01");
            if (camObj == null)
            {
                EditorUtility.DisplayDialog("알림", "씬에서 'CM_Shot01' 오브젝트를 찾을 수 없습니다.", "확인");
                return;
            }

            var script = camObj.GetComponent<CameraWindBlastShock>();
            if (script == null)
            {
                EditorUtility.DisplayDialog("알림", "CM_Shot01에 'CameraWindBlastShock' 컴포넌트가 없습니다.", "확인");
                return;
            }

            Undo.RecordObject(script, "Setup Pitch Flip");
            script.ApplyPitchFlipDefaultValues();
            script.ApplyPitchFlipPose(0f);
            Selection.activeGameObject = camObj;
            EditorUtility.SetDirty(script);
            EditorUtility.DisplayDialog("완료", "CM_Shot01에 [360° 수직 텀블링 틸트 & 밤하늘 엔딩 연출]이 성공적으로 활성화되었습니다!\n사진 1~5번 6단계 각도 및 위치가 적용되었습니다.", "확인");
        }

        [MenuItem("Tools/NITROZERO/CM_Shot01 원형 회전 훑기 켜기 & 좌표 적용")]
        public static void SetupOrbitSweepOnCMShot01()
        {
            GameObject camObj = GameObject.Find("CM_Shot01");
            if (camObj == null)
            {
                EditorUtility.DisplayDialog("알림", "씬에서 'CM_Shot01' 오브젝트를 찾을 수 없습니다.", "확인");
                return;
            }

            var script = camObj.GetComponent<CameraWindBlastShock>();
            if (script == null)
            {
                EditorUtility.DisplayDialog("알림", "CM_Shot01에 'CameraWindBlastShock' 컴포넌트가 없습니다.", "확인");
                return;
            }

            Undo.RecordObject(script, "Setup Orbit Sweep");
            script.ApplyOrbitSweepDefaultWaypoints();
            script.ApplyOrbitSweepPose(0f);
            Selection.activeGameObject = camObj;
            EditorUtility.SetDirty(script);
            EditorUtility.DisplayDialog("완료", "CM_Shot01에 [원형 회전 훑기 연출]이 성공적으로 활성화되었습니다!\n사진 1~5번 궤적이 적용되었습니다.", "확인");
        }
    }
}
