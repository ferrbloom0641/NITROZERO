using UnityEngine;

namespace YUJEONG
{
    /// <summary>
    /// [02YUJEONG 전용] 타임라인 바퀴 조향 완벽 보존기
    /// 타임라인 애니메이션이 적용된 직후(LateUpdate Order 990)에 타임라인 바퀴 각도를 캡처하고,
    /// TrailerCruiseMotion(Order 1000)이 덮어쓴 직후(LateUpdate Order 1010)에 캡처한 각도를 완벽하게 재적용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1010)] // TrailerCruiseMotion(1000) 이후에 최종 복원 실행
    public class YujeongWheelSteerKeeper : MonoBehaviour
    {
        [Header("[ 🚗 조향 대상 피벗 ]")]
        public Transform steerFL;
        public Transform steerFR;

        [Header("[ ⚙️ 옵션 ]")]
        public bool keepTimelineSteering = true;

        // 캡처한 타임라인 로컬 회전값
        public Quaternion capturedRotFL = Quaternion.identity;
        public Quaternion capturedRotFR = Quaternion.identity;

        private SteerPreCapture preCaptureHelper;

        private void Awake()
        {
            AutoFindSteerPivots();
            SetupPreCaptureHelper();
        }

        private void OnEnable()
        {
            AutoFindSteerPivots();
            SetupPreCaptureHelper();
        }

        public void AutoFindSteerPivots()
        {
            if (steerFL == null) steerFL = FindDeepChild(transform, "FL_Steering");
            if (steerFR == null) steerFR = FindDeepChild(transform, "FR_Steering");

            if (steerFL == null) steerFL = FindDeepChild(transform, "FL");
            if (steerFR == null) steerFR = FindDeepChild(transform, "FR");
        }

        private void SetupPreCaptureHelper()
        {
            if (preCaptureHelper == null)
            {
                preCaptureHelper = GetComponent<SteerPreCapture>();
                if (preCaptureHelper == null)
                {
                    preCaptureHelper = gameObject.AddComponent<SteerPreCapture>();
                    preCaptureHelper.hideFlags = HideFlags.DontSaveInEditor | HideFlags.HideInInspector;
                }
            }
            preCaptureHelper.targetKeeper = this;
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform direct = parent.Find(name);
            if (direct != null) return direct;
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        // 2단계: TrailerCruiseMotion(1000)이 덮어쓴 직후(1010), 타임라인 각도로 완벽 복원
        private void LateUpdate()
        {
            if (!keepTimelineSteering) return;

            if (steerFL != null) steerFL.localRotation = capturedRotFL;
            if (steerFR != null) steerFR.localRotation = capturedRotFR;
        }
    }

    /// <summary>
    /// 타임라인 애니메이션 평가 직후, TrailerCruiseMotion(1000) 실행 직전(990)에 순수 타임라인 각도를 캡처하는 헬퍼
    /// </summary>
    [DefaultExecutionOrder(990)]
    public class SteerPreCapture : MonoBehaviour
    {
        public YujeongWheelSteerKeeper targetKeeper;

        private void LateUpdate()
        {
            if (targetKeeper == null || !targetKeeper.keepTimelineSteering) return;

            if (targetKeeper.steerFL != null)
                targetKeeper.capturedRotFL = targetKeeper.steerFL.localRotation;

            if (targetKeeper.steerFR != null)
                targetKeeper.capturedRotFR = targetKeeper.steerFR.localRotation;
        }
    }
}
