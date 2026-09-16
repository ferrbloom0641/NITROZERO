using UnityEditor;
using UnityEngine;

namespace YUJEONG
{
    [InitializeOnLoad]
    public static class RestoreCarOriginalPose
    {
        [MenuItem("Tools/NITROZERO/🚨 [원클릭] 바퀴 원래대로 100% 복구")]
        public static void RevertWheels()
        {
            RevertCar("Blue_Car_Final_Booster", isBlueCar: true);
            RevertCar("Red_Car_Final_Booster", isBlueCar: false);

            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("복구 완료", "파란차와 빨간차의 모든 바퀴가 원래 처음 상태로 완벽하게 복구되었습니다!", "확인");
        }

        private static void RevertCar(string carName, bool isBlueCar)
        {
            GameObject car = GameObject.Find(carName);
            if (car == null) return;

            Undo.RecordObject(car, "Restore Original Wheels");

            // 1. 프리팹 오버라이드 전체 원상복구 시도
            if (PrefabUtility.IsPartOfPrefabInstance(car))
            {
                PrefabUtility.RevertPrefabInstance(car, InteractionMode.AutomatedAction);
            }

            // 2. 수동 트랜스폼 정밀 원복 (프리팹 연결 여부 무관)
            Transform fl = FindDeep(car.transform, "FL");
            Transform fr = FindDeep(car.transform, "FR");
            Transform rl = FindDeep(car.transform, "RL");
            Transform rr = FindDeep(car.transform, "RR");

            if (isBlueCar)
            {
                if (fl != null) fl.localRotation = new Quaternion(0f, -0.7071068f, 0f, 0.7071068f);
                if (fr != null) fr.localRotation = new Quaternion(0f, 0.7071068f, 0f, 0.7071068f);
                if (rl != null) rl.localRotation = new Quaternion(0f, -0.7071068f, 0f, 0.7071068f);
                if (rr != null) rr.localRotation = new Quaternion(0f, 0.7071068f, 0f, 0.7071068f);
            }
            else
            {
                if (fl != null) fl.localRotation = new Quaternion(0f, 1f, 0f, 0f);
                if (fr != null) fr.localRotation = new Quaternion(0f, 0f, 0f, 1f);
                if (rl != null) rl.localRotation = new Quaternion(0f, 1f, 0f, 0f);
                if (rr != null) rr.localRotation = new Quaternion(0f, 0f, 0f, 1f);
            }

            // 관절들 0도 리셋
            string[] joints = { "FL_Steering", "FR_Steering", "RL_Steering", "RR_Steering", "FL_Rolling", "FR_Rolling", "RL_Rolling", "RR_Rolling" };
            foreach (var j in joints)
            {
                Transform jt = FindDeep(car.transform, j);
                if (jt != null) jt.localRotation = Quaternion.identity;
            }

            EditorUtility.SetDirty(car);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform direct = parent.Find(name);
            if (direct != null) return direct;
            foreach (Transform c in parent.GetComponentsInChildren<Transform>(true))
            {
                if (c.name == name) return c;
            }
            return null;
        }
    }
}
