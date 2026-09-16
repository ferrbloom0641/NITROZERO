using UnityEditor;
using UnityEngine;

namespace YUJEONG
{
    [InitializeOnLoad]
    public static class WheelEmergencyFixer
    {
        static WheelEmergencyFixer()
        {
            // 유니티 에디터가 켜져 있는 매 순간 실행 (Update 첫 프레임에 무조건 즉각 자동 실행)
            EditorApplication.update += RunOnceOnUpdate;
        }

        private static void RunOnceOnUpdate()
        {
            EditorApplication.update -= RunOnceOnUpdate;
            ForceResetBothCars();
        }

        [MenuItem("Tools/NITROZERO/🚨 긴급: 두 차량 바퀴 원본 프리팹 100% 강제 리셋")]
        public static void ForceResetBothCars()
        {
            ResetCarFromPrefab("Blue_Car_Final_Booster", "Assets/Prefabs/Final_Car_Booster/Blue_Car_Final_Booster.prefab");
            ResetCarFromPrefab("Red_Car_Final_Booster", "Assets/Prefabs/Final_Car_Booster/Red_Car_Final_Booster.prefab");

            SceneView.RepaintAll();
        }

        private static void ResetCarFromPrefab(string carName, string prefabPath)
        {
            GameObject sceneCar = GameObject.Find(carName);
            if (sceneCar == null)
            {
                Debug.LogWarning($"[WheelEmergencyFixer] 씬에서 '{carName}'를 찾을 수 없습니다.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[WheelEmergencyFixer] 프리팹 경로를 찾을 수 없습니다: {prefabPath}");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(sceneCar, "Emergency Wheel Fix");

            Transform[] prefabTransforms = prefab.GetComponentsInChildren<Transform>(true);
            Transform[] sceneTransforms = sceneCar.GetComponentsInChildren<Transform>(true);

            int restoredCount = 0;
            foreach (Transform sT in sceneTransforms)
            {
                string sName = sT.name;
                string sUpper = sName.ToUpper();
                if (sUpper.Contains("FL") || sUpper.Contains("FR") || sUpper.Contains("RL") || sUpper.Contains("RR") || sUpper.Contains("WHEEL") || sUpper.Contains("STEERING") || sUpper.Contains("ROLLING"))
                {
                    // 프리팹에서 동일한 이름과 부모 이름을 가진 트랜스폼 탐색
                    Transform matchedPT = null;
                    foreach (Transform pT in prefabTransforms)
                    {
                        if (pT.name == sName)
                        {
                            if (sT.parent == null && pT.parent == null) { matchedPT = pT; break; }
                            if (sT.parent != null && pT.parent != null && sT.parent.name == pT.parent.name) { matchedPT = pT; break; }
                            if (matchedPT == null) matchedPT = pT; // fallback
                        }
                    }

                    if (matchedPT != null)
                    {
                        sT.localPosition = matchedPT.localPosition;
                        sT.localRotation = matchedPT.localRotation;
                        sT.localScale = matchedPT.localScale;
                        EditorUtility.SetDirty(sT);
                        restoredCount++;
                    }
                }
            }

            EditorUtility.SetDirty(sceneCar);
            Debug.Log($"<color=cyan><b>[바퀴 원상복구 완료]</b></color> '{carName}' 바퀴 관련 부품 {restoredCount}개를 원본 프리팹 각도로 100% 되돌렸습니다!");
        }
    }
}
