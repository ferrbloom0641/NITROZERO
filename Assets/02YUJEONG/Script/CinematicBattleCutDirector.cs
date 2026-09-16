using System.Collections;
using UnityEngine;

namespace YUJEONG
{
    /// <summary>
    /// 레이싱 배틀 단편 컷 연출 컨트롤러 (Cut B & Cut C)
    /// - 컷 B: [추월 시도 & 블로킹 막아서기]
    ///   * 중심점 기준: 빨간 차가 정중앙 맨 앞에서 주행, 파란 차가 바로 뒤에서 바짝 추격
    ///   * 파란 차가 치고 나오려고 할 때, 빨간 차가 핸들을 휙 꺾어 차체를 살짝 들썩(롤/피치)이며 길을 틀어막음!
    ///   * 거울 모드(Mirror): 오른쪽 블로킹 vs 왼쪽 블로킹 반전 지원
    ///   * 원클릭 On/Off 및 스페이스바로 언제든 2.5초 연출 재시작
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(150)]
    public class CinematicBattleCutDirector : MonoBehaviour
    {
        public enum CutType
        {
            [InspectorName("컷 B: 추월 시도 & 찰나의 블로킹 (Blocking Defense)")]
            CutB_BlockingDefense,
            [InspectorName("컷 C: 사이드 바이 사이드 엎치락뒤치락 (예정)")]
            CutC_SideBySideRivalry
        }

        [Header("[ 🎬 컷 선택 및 활성화 ]")]
        [Tooltip("현재 실행할 연출 컷")]
        public CutType currentCut = CutType.CutB_BlockingDefense;

        [Tooltip("체크(ON): 컷 연출 활성화\n체크 해제(OFF): 모든 차량 위치 기본 복귀 및 비활성화")]
        public bool enableCut = true;

        [Tooltip("체크(ON): 거울 반대 모드! (파란 차가 왼쪽으로 치고 나오고, 빨간 차가 왼쪽으로 핸들 꺾어 막음)\n체크 해제(OFF): 기본 모드 (파란 차가 오른쪽으로 치고 나오고, 빨간 차가 오른쪽으로 막음)")]
        public bool mirrorMode = false;

        [Tooltip("게임 시작 시 자동으로 연출 재생 시작")]
        public bool playOnStart = true;

        [Tooltip("연출 수동 재시작 키 (기본: Space)")]
        public KeyCode restartKey = KeyCode.Space;

        [Header("[ 🚀 부스터 변형 및 이펙트 제어 ]")]
        [Tooltip("체크(ON): 부스터 변형 및 점등 허용\n체크 해제(OFF): 부스터 변형 및 모든 이펙트를 완전히 끄고 순정 차량 모습으로 컷씬 진행 (기본값: OFF)")]
        public bool enableBoosters = false;

        [Header("[ 🚗 차량 참조 (비어있으면 자동 탐색) ]")]
        [Tooltip("주행 중심점 (기본: '중심점')")]
        public Transform centerPivot;

        [Tooltip("빨간 차 (앞서 달리는 수비 차량)")]
        public Transform redCar;

        [Tooltip("파란 차 (뒤에서 추격하는 공격 차량)")]
        public Transform blueCar;

        [Header("[ ⏱️ 컷 B 타이밍 설정 (총 2.5초 내외 권장) ]")]
        [Tooltip("전체 컷 재생 지속 시간 (초 단위, 2.0~3.0초 권장)")]
        [Range(1.5f, 6.0f)]
        public float cutDuration = 2.8f;

        [Tooltip("1단계: 뒤에서 바짝 추격하며 틈을 노리는 정적/긴장감 시간 (초)")]
        [Range(0.2f, 2.0f)]
        public float chaseInitialDelay = 0.5f;

        [Tooltip("2단계: 파란 차가 가속하며 옆으로 쑥 파고드는 시간 (초)")]
        [Range(0.3f, 2.0f)]
        public float overtakeAttemptDuration = 0.9f;

        [Tooltip("3단계: 빨간 차가 반응하여 길을 틀어막는 찰나의 딜레이 (사람의 반응속도 0.15~0.25초)")]
        [Range(0.05f, 0.6f)]
        public float redCarReactionDelay = 0.18f;

        [Header("[ 📏 컷 B 공간 간격 및 차체 들썩임 ]")]
        [Tooltip("빨간 차의 기본 전방 위치 오프셋 (중심점 기준 앞선 거리, Z축)")]
        public float redCarLeadForwardZ = 2.8f;

        [Tooltip("파란 차의 기본 후방 추격 위치 오프셋 (중심점 기준 뒤처진 거리, Z축, -5.4m가 안 겹치고 바짝 추격하는 간격)")]
        public float blueCarChaseDistanceZ = -5.4f;

        [Tooltip("파란 차가 추월 시도 시 옆으로 치고 나가는 좌우 폭 (X축 거리)")]
        [Range(0.8f, 5.0f)]
        public float overtakeLateralDistanceX = 2.2f;

        [Tooltip("파란 차가 추월 시도 시 앞차 옆구리까지 치고 나가는 전진 거리 (Z축 가속)")]
        [Range(0.5f, 6.0f)]
        public float overtakeForwardSurgeZ = 3.6f;

        [Tooltip("빨간 차가 핸들을 꺾어 막아서는 블로킹 좌우 이동 폭 (X축)")]
        [Range(0.8f, 5.0f)]
        public float blockLateralDistanceX = 1.9f;

        [Tooltip("핸들을 휙 꺾을 때 차체가 원심력으로 기우는 롤(Roll) 들썩임 각도 (도 단위)")]
        [Range(1f, 15f)]
        public float steeringRollAngle = 5.5f;

        [Tooltip("핸들을 꺾고 급가속/블로킹할 때 차체 앞뒤 피치(Pitch) 들썩임 각도")]
        [Range(0.5f, 8f)]
        public float suspensionPitchAngle = 2.0f;

        [Header("[ 📊 실시간 재생 진행도 모니터링 ]")]
        [SerializeField] private float playbackTimer = 0f;
        [SerializeField] private bool isPlaying = false;

        // 원본 로컬 위치 저장용
        private Vector3 defaultRedLocalPos = new Vector3(0f, 0f, 1.8f);
        private Vector3 defaultBlueLocalPos = new Vector3(0f, 0f, -2.2f);
        private Quaternion defaultRedLocalRot = Quaternion.identity;
        private Quaternion defaultBlueLocalRot = Quaternion.identity;
        private bool hasSavedDefaultPos = false;

        private void Awake()
        {
            FindReferences();
        }

        private void OnEnable()
        {
            FindReferences();
            if (Application.isPlaying && playOnStart)
            {
                RestartCut();
            }
        }

        private IEnumerator Start()
        {
            if (Application.isPlaying)
            {
                yield return null;
                ApplyBoosterStates();
            }
        }

        private void OnDisable()
        {
            ResetVehiclesToDefault();
        }

        public void FindReferences()
        {
            if (centerPivot == null)
            {
                GameObject centerObj = GameObject.Find("중심점");
                if (centerObj != null) centerPivot = centerObj.transform;
            }

            if (redCar == null)
            {
                GameObject redObj = GameObject.Find("Red_Car_Final_Booster") ?? GameObject.Find("Red_Car_Final") ?? GameObject.Find("Red_Car");
                if (redObj != null) redCar = redObj.transform;
            }

            if (blueCar == null)
            {
                GameObject blueObj = GameObject.Find("Blue_Car_Final_Booster") ?? GameObject.Find("Blue_Car_Final");
                if (blueObj != null) blueCar = blueObj.transform;
            }

            if (!hasSavedDefaultPos)
            {
                if (redCar != null)
                {
                    defaultRedLocalPos = redCar.localPosition;
                    defaultRedLocalRot = redCar.localRotation;
                }
                if (blueCar != null)
                {
                    defaultBlueLocalPos = blueCar.localPosition;
                    defaultBlueLocalRot = blueCar.localRotation;
                }
                hasSavedDefaultPos = true;
            }
        }

        private void Update()
        {
            if (IsRestartKeyPressed())
            {
                RestartCut();
            }

            if (!enableCut) return;

            if (Application.isPlaying)
            {
                if (isPlaying)
                {
                    playbackTimer += Time.deltaTime;
                    if (playbackTimer >= cutDuration)
                    {
                        playbackTimer = cutDuration;
                    }
                }
            }

            float t = (cutDuration > 0.01f) ? Mathf.Clamp01(playbackTimer / cutDuration) : 0f;

            if (currentCut == CutType.CutB_BlockingDefense)
            {
                AnimateCutB(playbackTimer);
            }
        }

        /// <summary>
        /// 컷 B: 파란 차 추월 시도 & 빨간 차 찰나의 블로킹 애니메이션 (초기 깔끔 버전)
        /// </summary>
        private void AnimateCutB(float elapsed)
        {
            if (redCar == null || blueCar == null) return;

            // 좌우 방향 결정 (기본: 오른쪽 +1, 거울 모드: 왼쪽 -1)
            float sideSign = mirrorMode ? -1f : 1f;

            // 미세 엔진 진동/노면 바운스
            float rumble = (Mathf.PerlinNoise(elapsed * 25f, 0f) - 0.5f) * 0.02f;

            // --- [ 파란 차 동작: 추월 찌르기 ] ---
            float blueProgress = 0f;
            if (elapsed > chaseInitialDelay)
            {
                blueProgress = Mathf.Clamp01((elapsed - chaseInitialDelay) / Mathf.Max(0.1f, overtakeAttemptDuration));
            }
            float blueSmoothT = Mathf.SmoothStep(0f, 1f, blueProgress);

            // 파란 차 옆으로 빠지며(+X) 앞으로 치고 나감(+Z)
            float blueTargetX = sideSign * overtakeLateralDistanceX * blueSmoothT;
            float blueTargetZ = blueCarChaseDistanceZ + (overtakeForwardSurgeZ * blueSmoothT);
            Vector3 blueCurrentPos = new Vector3(blueTargetX, defaultBlueLocalPos.y + rumble, blueTargetZ);

            // 파란 차 핸들 조타 각도: 꺾을 때 기울어졌다가 진입 후 정렬
            float blueSteerPhase = Mathf.Sin(blueSmoothT * Mathf.PI);
            float blueYaw = sideSign * blueSteerPhase * 6.0f;
            float blueRoll = -sideSign * blueSteerPhase * (steeringRollAngle * 0.8f);
            Quaternion blueCurrentRot = Quaternion.Euler(rumble * 15f, blueYaw, blueRoll);

            // --- [ 빨간 차 동작: 찰나의 블로킹 & 차체 들썩임 ] ---
            float redReactTime = chaseInitialDelay + redCarReactionDelay;
            float redProgress = 0f;
            if (elapsed > redReactTime)
            {
                float blockDuration = overtakeAttemptDuration * 0.75f;
                redProgress = Mathf.Clamp01((elapsed - redReactTime) / Mathf.Max(0.1f, blockDuration));
            }

            float redSmoothT = Mathf.SmoothStep(0f, 1f, redProgress);
            float redTargetX = sideSign * blockLateralDistanceX * redSmoothT;
            Vector3 redCurrentPos = new Vector3(redTargetX, defaultRedLocalPos.y + rumble, redCarLeadForwardZ);

            // 빨간 차 핸들 조타 및 차체 들썩임 (초기 깔끔 버전: 어색한 흔들림 없이 차체를 가로막음)
            float redSteerPhase = Mathf.Sin(redSmoothT * Mathf.PI);
            float redYaw = sideSign * redSteerPhase * 8.5f;
            float redRoll = -sideSign * redSteerPhase * steeringRollAngle;
            float redPitch = redSteerPhase * suspensionPitchAngle;
            Quaternion redCurrentRot = Quaternion.Euler(redPitch + (rumble * 15f), redYaw, redRoll);

            // 최종 로컬 위치/회전 적용
            blueCar.localPosition = blueCurrentPos;
            blueCar.localRotation = blueCurrentRot;

            redCar.localPosition = redCurrentPos;
            redCar.localRotation = redCurrentRot;
        }

        /// <summary>
        /// 컷 연출 처음부터 즉시 재실행
        /// </summary>
        [ContextMenu("🎬 [재생] 컷 B 처음부터 다시 시작 (Space)")]
        public void RestartCut()
        {
            playbackTimer = 0f;
            isPlaying = true;
            ApplyBoosterStates();
            Debug.Log($"[CinematicBattleCutDirector] 🎬 {currentCut} 시작! (거울 모드: {mirrorMode}, 부스터 활성화: {enableBoosters})");
        }

        /// <summary>
        /// 부스터 변형 및 이펙트 On/Off 상태 적용
        /// </summary>
        public void ApplyBoosterStates()
        {
            var blueCtrl = FindFirstObjectByType<VehicleTransformationController>();
            if (blueCtrl != null)
            {
                if (!enableBoosters)
                {
                    blueCtrl.SnapToNormalState();
                    blueCtrl.SetSubBoosterFxActive(false);
                    blueCtrl.SetMainBoosterFxActive(false);
                }
                else
                {
                    blueCtrl.SnapToFullTransformation();
                }
            }

            var redCtrl = FindFirstObjectByType<BoosterDeploymentController>();
            if (redCtrl != null)
            {
                if (!enableBoosters)
                {
                    redCtrl.SnapToFullRetraction();
                }
                else
                {
                    redCtrl.SnapToFullDeployment();
                }
            }
        }

        /// <summary>
        /// 차량 위치를 원래 기본 상태로 복원
        /// </summary>
        [ContextMenu("🔄 차량 기본 위치로 복귀")]
        public void ResetVehiclesToDefault()
        {
            isPlaying = false;
            playbackTimer = 0f;

            if (redCar != null && hasSavedDefaultPos)
            {
                redCar.localPosition = defaultRedLocalPos;
                redCar.localRotation = defaultRedLocalRot;
            }

            if (blueCar != null && hasSavedDefaultPos)
            {
                blueCar.localPosition = defaultBlueLocalPos;
                blueCar.localRotation = defaultBlueLocalRot;
            }
        }

        /// <summary>
        /// 거울 모드 토글 (오른쪽 블로킹 <-> 왼쪽 블로킹)
        /// </summary>
        [ContextMenu("🪞 [토글] 거울 모드 반전 (오른쪽/왼쪽)")]
        public void ToggleMirrorMode()
        {
            mirrorMode = !mirrorMode;
            RestartCut();
        }

        private bool IsRestartKeyPressed()
        {
            if (restartKey == KeyCode.None) return false;

            try
            {
                if (Input.GetKeyDown(restartKey)) return true;
            }
            catch { }

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_EXISTS
            try
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null)
                {
                    if (restartKey == KeyCode.Space && kb.spaceKey.wasPressedThisFrame) return true;
                }
            }
            catch { }
#endif
            return false;
        }
    }
}
