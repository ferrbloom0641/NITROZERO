using UnityEditor;
using UnityEngine;

namespace YUJEONG
{
    [CustomEditor(typeof(YujeongWheelSteerKeeper))]
    public class YujeongWheelSteerKeeperEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("💡 TrailerCruiseMotion의 자동 바퀴 굴러가기(Spin)를 유지하면서, 타임라인에서 찍은 앞바퀴 조향 각도를 게임 실행 중에도 완벽하게 지켜줍니다.", MessageType.Info);
            EditorGUILayout.Space(5);

            DrawDefaultInspector();
        }

        [MenuItem("Tools/NITROZERO/파란차 & 빨간차에 [타임라인 바퀴 조향 보존기] 장착")]
        public static void AttachToBothCars()
        {
            AttachToCar("Blue_Car_Final_Booster");
            AttachToCar("Red_Car_Final_Booster");

            EditorUtility.DisplayDialog("완료", "파란차와 빨간차에 [타임라인 바퀴 조향 보존기]가 성공적으로 장착되었습니다!\n이제 TrailerCruiseMotion을 켜둔 상태에서도 타임라인 바퀴 각도가 그대로 나옵니다.", "확인");
        }

        private static void AttachToCar(string carName)
        {
            GameObject car = GameObject.Find(carName);
            if (car == null) return;

            Undo.RecordObject(car, "Attach YujeongWheelSteerKeeper");
            var keeper = car.GetComponent<YujeongWheelSteerKeeper>();
            if (keeper == null) keeper = car.AddComponent<YujeongWheelSteerKeeper>();

            keeper.AutoFindSteerPivots();
            EditorUtility.SetDirty(car);
        }
    }
}
