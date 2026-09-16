using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

namespace YUJEONG
{
    /// <summary>
    /// 시네마틱 카메라 풍압 전복 및 지면 진동 연출 컨트롤러 (Fly-by Wind Blast Shock Cam)
    /// 
    /// [정밀 연출 타임라인]
    /// 1. 조용함 (0초 ~ 1.0초): 카메라는 평온한 상태 유지 (뒤에 대기 중인 두 차량)
    /// 2. 진동 고조 (1.0초 ~ 3.0초, 약 2초간): 엔진 기동 및 지면 진동이 점점 거세짐
    /// 3. 스플라인 발진 (3.0초 시점): 두 차량이 스플라인을 타고 굉음과 함께 카메라를 추월
    /// 4. 위태로운 추월 샷 (약 0.4초간): 카메라 앞을 쏜살같이 스쳐 지나가는 자동차들의 뒷모습을 격렬한 진동과 함께 포착
    /// 5. 풍압 전복 (0.4초 직후): 엄청난 후폭풍에 카메라가 뒤로 덜컹! 벌러덩 넘어가 밤하늘을 바라봄
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class CameraWindBlastShock : MonoBehaviour
    {
        public enum CinematicPhase
        {
            [InspectorName("0. 정적 (Quiet)")]
            Quiet,
            [InspectorName("1. 진동 고조 (Rumble Building)")]
            RumbleBuilding,
            [InspectorName("2. 차량 스쳐지나감 (Pass-by Rush)")]
            PassByRush,
            [InspectorName("3. 풍압 전복 중 (Knocking Down)")]
            KnockedDown,
            [InspectorName("4. 하늘 바라보기 (Sky Gazing, 3초)")]
            SkyGazing,
            [InspectorName("5. 시퀀스 완료 (Completed)")]
            Completed
        }

        [Header("[ 🚗 스플라인 및 차량 제어 ]")]
        [Tooltip("스플라인 주행 컴포넌트 (미지정 시 '중심점' 오브젝트에서 자동 탐색)")]
        public SplineAnimate splineAnimate;

        [Tooltip("추적 기준 오브젝트 (중심점 또는 차량 루트)")]
        public Transform targetVehicle;

        [Tooltip("체크 시 스크립트가 차량의 SplineAnimate 속도/시간 설정을 덮어쓰지 않고, 사용자가 인스펙터에서 설정한 값을 그대로 유지 (기본 true)")]
        public bool keepUserSplineSpeed = true;

        [Tooltip("게임 실행 시 시네마틱 연출 시퀀스 자동 시작")]
        public bool playOnStart = true;

        [Tooltip("시퀀스 수동 재시작 키 (기본: Space)")]
        public KeyCode restartKey = KeyCode.Space;

        [Header("[ 🚀 차량 부스터 변형 연출 설정 ]")]
        [Tooltip("카메라 뒤에서 대기할 때부터 두 차의 변형과 모든 부스터 이펙트를 100% 켠 상태로 시작")]
        public bool startCarsWithFullBoosters = true;

        [Tooltip("스플라인 출발 시 차량 부스터 변형도 자동 동시 트리거 (풀 부스터 대기가 아닐 때 사용)")]
        public bool triggerBoosterOnLaunch = false;

        [Tooltip("스플라인 주행 시작 후 부스터 변형이 시작되기까지 대기할 시간 (초 단위, 요청: 1초 정도 대기 후 변형)")]
        [Range(0f, 10.0f)]
        public float boosterTransformDelay = 1.0f;

        [Header("[ 💥 부스터 점화 & 카메라 분리 발진 연출 (부와앙-!) ]")]
        [Tooltip("체크(ON): 부스터 변형 후 카메라를 도로에 남겨두고 차들만 폭발적으로 달려감 (카메라 유기 연출)\n체크 해제(OFF): 카메라를 두고 가지 않고, 카메라가 차에 탑승/추종한 상태로 함께 계속 주행")]
        public bool leaveCameraBehindOnBoost = true;

        [Tooltip("변형 시작 후 최종 부스터가 켜지기까지 걸리는 변형 진행 시간 (초 단위, 기본 3.2초)")]
        [Range(1.0f, 8.0f)]
        public float transformationDuration = 3.2f;

        [Header("[ 🎬 부스터 변형 훑기 카메라 연출 (Booster Sweep Cam) ]")]
        [Tooltip("체크(ON): 지금 카메라 구도에서 달리는 두 차량을 함께 추종하며, 부스터가 변형되는 모습을 천천히 훑어보는 시네마틱 연출 활성화\n체크 해제(OFF): 기존 일반 카메라 구도로 복귀 (인스펙터에서 언제든 껐다 켤 수 있습니다)")]
        public bool enableBoosterSweepCam = true;

        [Tooltip("체크(ON): 완전 거울 반대 모드! 스플라인(2) 중심선 기준으로 정확히 반대편(파란 차 측면)에서 똑같은 앵글로 파란 차 부스터부터 빨간 차 부스터 순서로 훑기\n체크 해제(OFF): 기본 모드 (빨간 차 측면에서 빨간 차 부스터부터 훑기)\n인스펙터 체크박스나 컴포넌트 우클릭 메뉴로 실시간 껐다 켤 수 있습니다.")]
        public bool mirrorBoosterSweepCam = false;

        [Tooltip("부스터 훑기 카메라 롤(Dutch Angle) 기울기 (도 단위, 기본 2.886도, 거울 모드 시 부호 반전되어 완벽한 대칭 연출)")]
        [Range(-15f, 15f)]
        public float sweepCamRoll = 2.886f;

        [Tooltip("부스터 변형 모습을 천천히 훑는 데 걸리는 시간 (초 단위, 권장 6.0~10.0초)")]
        [Range(2.0f, 20.0f)]
        public float sweepDuration = 8.0f;

        [Header("[ 🎬 360° 수직 텀블링 틸트 & 밤하늘 엔딩 연출 (Pitch-Flip Camera Sequence) ]")]
        [Tooltip("체크(ON): 사진 1~5번 구도대로 차가 달려오는 것을 정면에서 포착 ➔ 카메라 아래로 통과할 때 수직 틸트 다운 ➔ 거꾸로 뒤집힌 채 멀어지는 뒷모습 포착 ➔ 계속 돌아 밤하늘을 올려다보며 여운 엔딩 연출\n체크 해제(OFF): 기본 모드 / 기존 카메라 설정 유지")]
        public bool enablePitchFlipPass = true;

        [Header("[ 📐 텀블링 틸트 6단계 각도 설정 (X축 회전) ]")]
        [Tooltip("1단계 (사진 1): 정면 원거리 접근 기본 각도 (기본 2.174도)")]
        public float pitchAngleApproach = 2.174f;

        [Tooltip("다가오는 차량 포착 상하 구도 오프셋 (0 = 정중앙 락온, 음수 = 화면 하단부, 기본 0도)")]
        [Range(-5f, 5f)]
        public float approachFramingOffset = 0.0f;

        [Tooltip("2단계 (사진 2): 근거리 접근 하향 틸트 각도 (기본 16.484도)")]
        public float pitchAngleNearPass = 16.484f;

        [Tooltip("3단계 (사진 3): 카메라 바로 밑 통과 수직 각도 (기본 90.0도)")]
        public float pitchAnglePassUnder = 90.0f;

        [Tooltip("4단계 (사진 4): 거꾸로 뒤집혀 멀어지는 차량 후면 추종 각도 (기본 153.128도)")]
        public float pitchAngleInvertedRear = 153.128f;

        [Tooltip("5단계 (사진 5): 거꾸로 수평 지평선 각도 (기본 177.507도)")]
        public float pitchAngleInvertedHorizon = 177.507f;

        [Tooltip("6단계 (엔딩): 밤하늘 응시 최종 각도 (기본 265도)")]
        public float pitchAngleSkyEnding = 265.0f;

        [Header("[ 📍 카메라 위치 및 수평 고정 설정 ]")]
        [Tooltip("체크 시 아래 고정 위치(사진 1~5번 좌표)로 카메라 위치 강제 고정")]
        public bool lockPitchFlipPosition = true;

        [Tooltip("텀블링 연출 카메라 고정 로컬/월드 위치 (사진 기준: -1314.5, 1.97743, -0.5998)")]
        public Vector3 pitchFlipFixedPos = new Vector3(-1314.5f, 1.97743f, -0.5998f);

        [Tooltip("고정 Yaw 각도 (Y축, 사진 기준: -90도)")]
        public float pitchFlipFixedYaw = -90.0f;

        [Tooltip("고정 Roll 각도 (Z축, 사진 기준: 0도)")]
        public float pitchFlipFixedRoll = 0.0f;

        [Header("[ ⏱️ 완급조절 및 지속 시간 설정 ]")]
        [Tooltip("통과 후 거꾸로 뒤집힌 채 멀어지는 두 차량의 후면을 감상하며 여운을 남기는 시간 (초 단위, 기본 3.0초)")]
        [Range(1.0f, 8.0f)]
        public float invertedRearLingerDuration = 3.0f;

        [Tooltip("차량 후면 감상 후 밤하늘까지 점점 느려지며 회전하는 전환 시간 (초 단위, 권장 5.0~6.0초, 기본 5.5초)")]
        [Range(1.5f, 12.0f)]
        public float skyTiltUpDuration = 5.5f;

        [Tooltip("카메라 각도 추종 반응성 (8도 협각 렌즈에서 차를 놓치지 않고 민첩하게 화면 중심에 락온, 권장 20~35)")]
        [Range(5f, 50f)]
        public float pitchTrackingResponsiveness = 25f;

        [Header("[ 🛠️ 텀블링 실시간 에디터 프리뷰 ]")]
        [Tooltip("체크 시 게임을 실행하지 않아도 인스펙터 슬라이더로 0%~100% 텀블링 각도를 씬 뷰에서 실시간 확인")]
        public bool previewPitchFlip = false;
        [Range(0f, 1f)]
        public float previewPitchFlipProgress = 0f;

        [Header("[ 🎬 원형 회전 훑기 연출 (Orbit Sweep Cam) - 사진 1~5번 구도 ]")]
        [Tooltip("체크(ON): 사진처럼 노란색 회전 링을 돌리듯 차 앞 대각선 ➔ 측면 ➔ 후면으로 매끄럽게 훑는 시네마틱 카메라 연출 활성화\n체크 해제(OFF): 기존 일반 카메라")]
        public bool enableVerticalAdSweepCam = true;
        public bool enableOrbitSweepCam = true;

        [Tooltip("1단계 (사진 1): 전측면 대각 구도 로컬 위치")]
        public Vector3 verticalSweepStartPos = new Vector3(28.0201f, 2.75505f, 104.834f);
        public Vector3 verticalSweepStartRot = new Vector3(0.495f, 185.033f, -5.21f);

        [Tooltip("2단계 (사진 2): 전측면 3/4 구도 로컬 위치")]
        public Vector3 verticalSweepMidPos1 = new Vector3(43.435f, 4.55768f, 89.8453f);
        public Vector3 verticalSweepMidRot1 = new Vector3(2.028f, 212.368f, -4.83f);

        [Tooltip("3단계 (사진 3): 완벽한 측면 프로필 구도 로컬 위치")]
        public Vector3 verticalSweepMidPos2 = new Vector3(60.1264f, 6.59522f, 55.9025f);
        public Vector3 verticalSweepMidRot2 = new Vector3(4.100f, 241.117f, -3.29f);

        [Tooltip("4단계 (사진 4): 후측면 부스터/디퓨저 구도 로컬 위치")]
        public Vector3 verticalSweepMidPos3 = new Vector3(51.3738f, 6.82395f, 1.78538f);
        public Vector3 verticalSweepMidRot3 = new Vector3(5.223f, 283.428f, 0.31f);

        [Tooltip("5단계 (사진 5): 차가 지나가며 뒤편을 비추는 후면 구도 로컬 위치")]
        public Vector3 verticalSweepEndPos = new Vector3(19.4323f, 4.19730f, -28.158f);
        public Vector3 verticalSweepEndRot = new Vector3(4.188f, 316.957f, 3.17f);

        [Tooltip("카메라 고정 회전 각도 (회전 보간 미사용 시 기본값)")]
        public Vector3 verticalSweepFixedEuler = new Vector3(4.100f, 241.117f, -3.29f);

        [Header("[ 🛠️ 에디터 미리보기 (프리뷰 슬라이더) ]")]
        [Tooltip("체크 시 게임을 실행하지 않아도 인스펙터 슬라이더로 0%~100% 궤적을 씬 뷰에서 실시간 확인")]
        public bool previewOrbitSweep = false;
        [Range(0f, 1f)]
        public float previewOrbitProgress = 0f;

        [Tooltip("스플라인 출발 후 훑기 카메라 연출이 시작되기까지의 대기 시간 (초)")]
        [Range(0f, 5.0f)]
        public float sweepStartDelay = 0.0f;

        [Tooltip("빨간 차 부스터 훑기 조준 오프셋 (Image 1 & 2 구도 미세 조정)")]
        public Vector3 redCarAimOffset = new Vector3(0.5f, 0.4f, -0.5f);

        [Tooltip("파란 차 부스터 훑기 조준 오프셋 (Image 3 & 4 구도 미세 조정)")]
        public Vector3 blueCarAimOffset = new Vector3(-0.5f, 0.4f, -0.5f);

        [Tooltip("훑기 시작 시 추가 팬 각도 (Image 1의 빨간 차 앞바퀴/측면 여백 각도)")]
        [Range(-15f, 15f)]
        public float sweepStartPanAngle = -2.5f;

        [Tooltip("훑기 종료 시 추가 팬 각도 (Image 4의 파란 차 우측 날개 여백 각도)")]
        [Range(-15f, 15f)]
        public float sweepEndPanAngle = 2.5f;

        [Header("[ 🏁 두 차량 경쟁 주행 연출 (앞서거니 뒤서거니) ]")]
        [Tooltip("체크(ON): 빨간차와 파란차가 나란히 달리지 않고, 서로 살짝씩 앞섰다 뒤로 밀렸다 하며 치열하게 경쟁하는 연출 활성화\n체크 해제(OFF): 고정된 기본 위치로 나란히 주행")]
        public bool enableRacingRivalry = true;

        [Tooltip("경쟁 시 앞뒤로 치고 나가는 최대 거리 (미터 단위, 권장 0.4 ~ 0.8m)")]
        [Range(0.1f, 2.0f)]
        public float rivalryDistance = 0.6f;

        [Tooltip("앞뒤로 엎치락뒤치락 바뀌는 속도 / 주기 (초당 반복 속도, 권장 1.0 ~ 2.0)")]
        [Range(0.2f, 4.0f)]
        public float rivalrySpeed = 1.4f;

        [Tooltip("경쟁 시 차체가 미세하게 좌우로 흔들리는 미세 주행 유격 (미터 단위)")]
        [Range(0f, 0.2f)]
        public float rivalryLateralJitter = 0.04f;

        [Tooltip("부스터 점화 전 일반 주행 순항 속도 (SplineAnimate Speed)")]
        [Range(20f, 400f)]
        public float cruiseSpeed = 100f;

        [Tooltip("부스터 점화 후 폭발적인 최종 로켓 질주 속도 (너무 빠르지 않은 적정 속도, 기본 200 권장)")]
        [Range(50f, 600f)]
        public float boostRocketSpeed = 200f;

        [Tooltip("부스터 점화 시 최고 속도까지 치고 나가는 급가속 시간 (초 단위, 부드럽고 묵직한 가속을 위해 0.8초 권장)")]
        [Range(0.1f, 3.0f)]
        public float boostAccelerationDuration = 0.8f;

        [Header("[ ⏱️ 타임라인 시간 설정 ]")]
        [Tooltip("1단계: 카메라가 정면을 응시하는 초기 대기 시간 (초, 권장 0.5초)")]
        [Range(0f, 3.0f)]
        public float quietDuration = 0.5f;

        [Tooltip("2단계: 뒤에서 엔진 굉음 및 지면 진동 고조 시간 (초, 권장 0.5초)")]
        [Range(0f, 3.0f)]
        public float rumbleBuildUpDuration = 0.5f;

        [Header("[ 📍 차량 통과 실시간 물리 인식 설정 (타이머 방식 완전 제거) ]")]
        [Tooltip("카메라 바로 옆으로 인정되는 최대 거리 (미터 단위, 차량이 이 반경 이내로 들어와야 통과 감지 활성화, 기본 8m)")]
        [Range(2f, 25f)]
        public float passingProximityRadius = 8.0f;

        [Tooltip("카메라를 지나친 후 전복이 일어나는 거리 오프셋 (미터 단위, 0 = 카메라 평면 통과 즉시, 0.1~0.5 = 차체가 지나간 직후 리얼한 전복)")]
        [Range(0f, 3.0f)]
        public float passKnockdownDelayOffset = 0.2f;

        [Tooltip("차량이 카메라 옆을 스쳐 지나간 직후 진동이 급격하게 가라앉는 시간 (초, 요청: 1.0초 내외로 급격히 감쇠)")]
        [Range(0.2f, 5.0f)]
        public float passByCalmDownDuration = 1.0f;

        [Tooltip("스플라인 주행 완주 시간 (초, 8초 권장)")]
        public float splineDriveDuration = 8.0f;

        [HideInInspector] public float passByViewDuration = 4.0f;
        [HideInInspector] public bool autoDetectPassByPosition = true;

        [Header("[ 🌌 하늘 바라보기 지속 시간 (여운 연출) ]")]
        [Tooltip("카메라가 뒤로 넘어진 후 밤하늘을 가만히 응시하며 머무는 시간 (초, 요청: 3.0초)")]
        [Range(0.5f, 10.0f)]
        public float skyHoldDuration = 3.0f;

        [Header("[ 🛣️ 거리 기반 아스팔트 지면 진동 (차량 접근 연출) ]")]
        [Tooltip("체크 시 차량과의 실제 거리에 비례하여 지면 진동 발생 (멀리 있으면 완전 조용함 ➔ 가까워질수록 아스팔트 진동 점점 심해짐)")]
        public bool useDistanceBasedRumble = true;

        [Tooltip("아스팔트 지면 진동이 시작되는 차량과의 감지 거리 (미터 단위, 이 거리보다 멀면 완전 정적 유지, 기본 150m)")]
        [Range(30f, 400f)]
        public float rumbleStartDistance = 150f;

        [Tooltip("접근 시 진동이 거세지는 곡선 지수 (1=선형, 2=서서히 시작되어 가까워질수록 급격히 웅장해짐, 권장 2.2)")]
        [Range(1f, 4f)]
        public float rumbleCurvePower = 2.2f;

        [Tooltip("지면 진동의 수직 바운스 배율 (아스팔트 상하 쿵쿵거림 강조, 1.0 = 기본)")]
        [Range(0.5f, 3.0f)]
        public float verticalRumbleMultiplier = 1.4f;

        [Header("[ 🌪️ 진동 및 충격 세기 설정 (0으로 설정 시 완전 무진동) ]")]
        [Tooltip("지면/차체 진동 최대 위치 흔들림 세기 (미터 단위, 0 설정 시 완전 고정)")]
        [Range(0f, 0.5f)]
        public float maxRumbleShake = 0.04f;

        [Tooltip("지면/차체 진동 최대 각도 흔들림 세기 (도 단위, 0 설정 시 완전 고정)")]
        [Range(0f, 20.0f)]
        public float maxRumbleAngleShake = 1.5f;

        [Tooltip("차량이 고속 주행 시 발생하는 난기류/풍압 배율 (0 설정 시 완전 고정)")]
        [Range(0f, 10.0f)]
        public float passByTurbulenceMultiplier = 1.8f;

        [Tooltip("진동 주파수 (Hz, 높을수록 빠르고 앙칼진 고속 엔진 진동, 25~45Hz 권장)")]
        [Range(0f, 120f)]
        public float shakeFrequency = 36f;

        [Tooltip("체크 시 주행 내내 진동이 줄어들지 않고 강렬하게 100% 유지 (차량 탑승 온보드 액션캠 필수)")]
        public bool continuousOnboardShake = false;

        [Header("[ 💥 풍압 전복 옵션 (카메라 넘어짐 제어) ]")]
        [Tooltip("체크 시 차가 지나갈 때 카메라가 뒤로 벌러덩 넘어가 하늘을 응시함 (창문 탑승 캠 사용 시 반드시 체크 해제)")]
        public bool enableKnockdown = false;

        [Tooltip("뒤로 넘어질 때 렌즈가 하늘을 향하는 X축 회전 각도 (음수 각도가 하늘 방향, 기본 약 -82도)")]
        [Range(-95f, -60f)]
        public float knockdownPitch = -82f;

        [Tooltip("넘어질 때 옆으로 기우는 롤(Roll) 각도 (Z축)")]
        [Range(-30f, 30f)]
        public float knockdownRoll = 14f;

        [Tooltip("풍압에 뒤로 벌러덩 넘어가는 데 걸리는 시간 (초, 작을수록 순간적인 충격감)")]
        [Range(0.15f, 0.6f)]
        public float knockdownDuration = 0.30f;

        [Tooltip("바닥에 부딪힐 때 덜컹거리는 충격 반동 (Bounce) 세기")]
        [Range(0f, 0.5f)]
        public float groundImpactBounce = 0.22f;

        [Tooltip("카메라 지지대가 꺾이며 지면으로 주저앉는 높이 하강 오프셋 (미터)")]
        [Range(-1.5f, 0f)]
        public float groundDropHeight = -0.35f;

        [Header("[ 🎥 차량 추적 패닝 회전 (Pan-Tracking Shot) ]")]
        [Tooltip("체크 시 카메라가 제자리에서 지나가는 두 차량을 따라 고개를 부드럽게 회전하며 추적 촬영")]
        public bool enableVehicleTracking = true;

        [Tooltip("기본 회전 추적 추종 부드러움/속도 (0 = 즉시 100% 락온, 12~18 = 카메라맨 팬 질감, 기본 15 권장)")]
        [Range(0f, 40f)]
        public float trackingSmoothness = 15f;

        [Tooltip("차량이 카메라 바로 아래/근처를 초고속으로 통과할 때 회전 추적 속도 부스트 배율 (차량이 아래로 지나갈 때 시선을 놓치지 않고 바닥으로 꺾어주는 가속)")]
        [Range(1f, 5f)]
        public float closePassSpeedBoost = 2.5f;

        [Tooltip("차량 추적 시 조준할 높이 및 중심 오프셋 (지면이 아닌 차체 중심 응시, 기본 Y: 0.4m)")]
        public Vector3 trackingAimOffset = new Vector3(0f, 0.4f, 0f);

        [Tooltip("상하 각도(Pitch)도 함께 추적할지 여부 (체크 시 카메라 바로 아래로 지나갈 때 아래를 똑바로 내려다봅니다)")]
        public bool allowPitchTracking = true;

        [Tooltip("아래를 내려다보는 최대 하향 각도 (도 단위, 카메라 바로 아래 통과 시 차를 놓치지 않으려면 85~89도 권장)")]
        [Range(30f, 89.5f)]
        public float maxDownwardPitch = 88f;

        [Tooltip("위(하늘)를 올려다보는 최대 상향 각도 (음수 각도, 기본 -25도)")]
        [Range(-60f, 0f)]
        public float maxUpwardPitch = -25f;

        [Tooltip("차량 진행 방향 앞쪽을 살짝 선행 조준하여 구도 여백(Lead Room)을 주는 거리")]
        [Range(0f, 15f)]
        public float leadAheadDistance = 0f;

        [Header("[ 📊 현재 진행 상태 모니터링 ]")]
        [SerializeField] private CinematicPhase currentPhase = CinematicPhase.Quiet;
        [SerializeField] private float sequenceTimer = 0f;
        [SerializeField] private float skyGazeTimer = 0f;
        [SerializeField] private bool isSequenceRunning = false;
        [SerializeField] private float currentDistanceToCar = 0f;
        [Range(0f, 1f)]
        [SerializeField] private float currentRumbleIntensity = 0f;

        // 원본 트랜스폼 보존
        private Vector3 initialLocalPos;
        private Vector3 initialWorldPos;
        private Quaternion initialLocalRot;
        private Quaternion initialWorldRot;
        private Quaternion currentTrackingRot;
        private bool isInitialized = false;

        // 카메라 분리 및 부스터 발진 상태
        private bool isCameraDetached = false;
        private Vector3 detachedWorldPos;
        private Quaternion detachedWorldRot;
        private Coroutine boostAccelerationRoutine;
        private bool isBoostBlastActive = false;
        private float boostBlastTimer = 0f;

        // 전복 애니메이션 변수
        private bool isKnockedDown = false;
        private float knockdownProgress = 0f;
        private float bounceDecay = 0f;
        private bool splineLaunched = false;
        private float actualPassTime = -1f;
        private Quaternion knockdownStartRot;

        // 차량 실제 위치 및 통과 감지용 정밀 상태 변수
        // 차량 실제 위치 및 통과 감지용 정밀 물리 상태 변수
        private Vector3 carLaunchStartPos = Vector3.zero;
        private Vector3 lastCarCenterPos = Vector3.zero;
        private Vector3 carMovingHeading = Vector3.zero;
        private float initialCarDistanceToCam = 9999f;
        private float minCarDistanceToCam = 9999f;
        private bool hasApproachedFromFront = false;
        private bool hasCarTraveledEnough = false;

        // 360 수직 텀블링 틸트 상태 변수
        private bool isPitchFlipReceding = false;
        private float pitchFlipRecedeTimer = 0f;
        private float currentPitchFlipAngle = 2.174f;
        private float currentCameraAheadDistance = 9999f;
        private float skyTransitionStartPitch = -1f;

        // 부스터 변형 훑기 카메라 및 경쟁 주행 상태 변수
        private Transform redCarTransform;
        private Transform blueCarTransform;
        private Vector3 baseRedCarLocalPos;
        private Vector3 baseBlueCarLocalPos;
        private bool hasStoredCarBasePos = false;
        private Vector3 sweepCamLocalOffset;
        private Quaternion sweepCamLocalRot;
        private bool sweepCamOffsetInitialized = false;
        private Vector3 baseRedSideLocalOffset;
        private bool hasBaseRedSideOffset = false;
        private float sweepTimer = 0f;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                StoreInitialPose();
                AutoFindComponents();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                if (!isInitialized) StoreInitialPose();
                AutoFindComponents();
            }
        }

        private void OnDisable()
        {
            // 에디터 모드에서는 사용자가 지정한 트랜스폼을 절대 임의로 복원/수정하지 않음
            if (Application.isPlaying)
            {
                ResetRacingRivalryMotion();
                if (isKnockedDown)
                {
                    RestoreInitialPose();
                }
            }
        }

        private IEnumerator Start()
        {
            if (Application.isPlaying)
            {
                AutoFindComponents();

                // 스플라인이 시작 시 자동으로 달리지 않도록 일시 대기
                if (splineAnimate != null)
                {
                    splineAnimate.NormalizedTime = 0f;
                    splineAnimate.Pause();
                }

                if (startCarsWithFullBoosters)
                {
                    ApplyVehiclesFullBoosted();
                }

                // 1프레임 대기 (타 스크립트 Start() 이후 확실하게 적용)
                yield return null;

                if (startCarsWithFullBoosters)
                {
                    ApplyVehiclesFullBoosted();
                }

                // 차량 추적 패닝 활성화 시 시작 위치로 카메라 초기 각도 즉시 조준 (Image 1 구도)
                if (enableVehicleTracking)
                {
                    Vector3 initTargetPos = GetTrackingTargetWorldPosition();
                    if (initTargetPos != Vector3.zero)
                    {
                        Vector3 toInit = initTargetPos - transform.position;
                        if (toInit.sqrMagnitude > 0.01f)
                        {
                            Quaternion initLook = Quaternion.LookRotation(toInit.normalized, Vector3.up);
                            if (!allowPitchTracking)
                            {
                                Vector3 euler = initLook.eulerAngles;
                                euler.x = initialWorldRot.eulerAngles.x;
                                euler.z = 0f;
                                initLook = Quaternion.Euler(euler);
                            }
                            currentTrackingRot = initLook;
                            transform.rotation = initLook;
                        }
                    }
                }

                if (playOnStart)
                {
                    StartCinematicSequence();
                }
            }
        }

        public void StoreInitialPose()
        {
            if (isKnockedDown) return;
            if (!isCameraDetached)
            {
                initialLocalPos = transform.localPosition;
                initialWorldPos = transform.position;
                initialLocalRot = transform.localRotation;
                initialWorldRot = transform.rotation;
                currentTrackingRot = transform.rotation;

                if (targetVehicle != null)
                {
                    sweepCamLocalOffset = Quaternion.Inverse(targetVehicle.rotation) * (transform.position - targetVehicle.position);
                    sweepCamLocalRot = Quaternion.Inverse(targetVehicle.rotation) * transform.rotation;
                    sweepCamOffsetInitialized = true;
                    if (!hasBaseRedSideOffset)
                    {
                        baseRedSideLocalOffset = sweepCamLocalOffset;
                        hasBaseRedSideOffset = true;
                    }
                }
            }
            isInitialized = true;
        }

        public void RestoreInitialPose()
        {
            if (!isInitialized) return;
            ResetBoosterLaunchState();
            ResetRacingRivalryMotion();
            sweepTimer = 0f;

            if (isKnockedDown || isCameraDetached || isPitchFlipReceding)
            {
                transform.localPosition = initialLocalPos;
                transform.position = initialWorldPos;
                transform.localRotation = initialLocalRot;
                currentTrackingRot = initialWorldRot;
                knockdownStartRot = initialWorldRot;
            }
            isKnockedDown = false;
            isCameraDetached = false;
            isPitchFlipReceding = false;
            pitchFlipRecedeTimer = 0f;
            currentCameraAheadDistance = 9999f;
            skyTransitionStartPitch = -1f;
            knockdownProgress = 0f;
            currentPhase = CinematicPhase.Quiet;
            sequenceTimer = 0f;
            skyGazeTimer = 0f;
            isSequenceRunning = false;
            splineLaunched = false;
            actualPassTime = -1f;
            carLaunchStartPos = Vector3.zero;
            lastCarCenterPos = Vector3.zero;
            carMovingHeading = Vector3.zero;
            initialCarDistanceToCam = 9999f;
            minCarDistanceToCam = 9999f;
            hasApproachedFromFront = false;
            hasCarTraveledEnough = false;
            currentDistanceToCar = 0f;
            currentRumbleIntensity = 0f;

            if (splineAnimate != null && Application.isPlaying)
            {
                splineAnimate.NormalizedTime = 0f;
                splineAnimate.Pause();
            }
        }

        public void AutoFindComponents()
        {
            if (targetVehicle == null)
            {
                GameObject centerObj = GameObject.Find("중심점");
                if (centerObj != null) targetVehicle = centerObj.transform;
                else
                {
                    GameObject blueCar = GameObject.Find("Blue_Car_Final_Booster") ?? GameObject.Find("Blue_Car_Final");
                    if (blueCar != null)
                    {
                        targetVehicle = (blueCar.transform.parent != null && blueCar.transform.parent != blueCar.transform.root)
                            ? blueCar.transform.parent
                            : blueCar.transform;
                    }
                }
            }

            if (splineAnimate == null && targetVehicle != null)
            {
                splineAnimate = targetVehicle.GetComponent<SplineAnimate>();
                if (splineAnimate == null)
                    splineAnimate = targetVehicle.GetComponentInChildren<SplineAnimate>();
            }

            FindCarTransforms();

            if (targetVehicle != null && !sweepCamOffsetInitialized)
            {
                sweepCamLocalOffset = Quaternion.Inverse(targetVehicle.rotation) * (transform.position - targetVehicle.position);
                sweepCamLocalRot = Quaternion.Inverse(targetVehicle.rotation) * transform.rotation;
                sweepCamOffsetInitialized = true;
                if (!hasBaseRedSideOffset)
                {
                    baseRedSideLocalOffset = sweepCamLocalOffset;
                    hasBaseRedSideOffset = true;
                }
            }
        }

        public void FindCarTransforms()
        {
            if (targetVehicle != null)
            {
                for (int i = 0; i < targetVehicle.childCount; i++)
                {
                    Transform child = targetVehicle.GetChild(i);
                    string nameLower = child.name.ToLower();
                    if (nameLower.Contains("red"))
                    {
                        redCarTransform = child;
                    }
                    else if (nameLower.Contains("blue"))
                    {
                        blueCarTransform = child;
                    }
                }
            }

            if (redCarTransform == null)
            {
                GameObject redObj = GameObject.Find("Red_Car_Final_Booster") ?? GameObject.Find("Red_Car_Final") ?? GameObject.Find("Red_Car");
                if (redObj != null) redCarTransform = redObj.transform;
            }

            if (blueCarTransform == null)
            {
                GameObject blueObj = GameObject.Find("Blue_Car_Final_Booster") ?? GameObject.Find("Blue_Car_Final");
                if (blueObj != null) blueCarTransform = blueObj.transform;
            }

            if (!hasStoredCarBasePos)
            {
                if (redCarTransform != null) baseRedCarLocalPos = redCarTransform.localPosition;
                if (blueCarTransform != null) baseBlueCarLocalPos = blueCarTransform.localPosition;
                if (redCarTransform != null || blueCarTransform != null) hasStoredCarBasePos = true;
            }
        }

        /// <summary>
        /// 빨간 차 부스터 조준 지점 (월드 좌표)
        /// </summary>
        public Vector3 GetRedBoosterFocusPoint()
        {
            if (targetVehicle == null) return transform.position;
            Vector3 carPos = (redCarTransform != null) ? redCarTransform.position : (targetVehicle.position + targetVehicle.right * 3.2f);
            return carPos + (targetVehicle.rotation * redCarAimOffset);
        }

        /// <summary>
        /// 파란 차 부스터 조준 지점 (월드 좌표)
        /// </summary>
        public Vector3 GetBlueBoosterFocusPoint()
        {
            if (targetVehicle == null) return transform.position;
            Vector3 carPos = (blueCarTransform != null) ? blueCarTransform.position : (targetVehicle.position - targetVehicle.right * 3.6f);
            return carPos + (targetVehicle.rotation * blueCarAimOffset);
        }

        /// <summary>
        /// 추적 대상(두 차량의 중심점)의 실시간 월드 좌표 계산
        /// </summary>
        public Vector3 GetTrackingTargetWorldPosition()
        {
            // 1. targetVehicle이 지정되어 있는 경우
            if (targetVehicle != null)
            {
                // targetVehicle 아래에 활성화된 자식(두 차량 등)이 여러 개 있으면 그들의 중심점 계산
                if (targetVehicle.childCount >= 2)
                {
                    Vector3 sumPos = Vector3.zero;
                    int count = 0;
                    for (int i = 0; i < targetVehicle.childCount; i++)
                    {
                        Transform child = targetVehicle.GetChild(i);
                        if (child != null && child.gameObject.activeInHierarchy)
                        {
                            sumPos += child.position;
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        return (sumPos / count) + trackingAimOffset;
                    }
                }
                return targetVehicle.position + trackingAimOffset;
            }

            // 2. targetVehicle이 비어있는 경우 씬에서 Blue_Car_Final, Red_Car 자동 탐색하여 중심점 산출
            GameObject blueCar = GameObject.Find("Blue_Car_Final");
            GameObject redCar = GameObject.Find("Red_Car") ?? GameObject.Find("Red_Car_Final");

            if (blueCar != null && redCar != null)
            {
                return ((blueCar.transform.position + redCar.transform.position) * 0.5f) + trackingAimOffset;
            }
            if (blueCar != null) return blueCar.transform.position + trackingAimOffset;
            if (redCar != null) return redCar.transform.position + trackingAimOffset;

            return Vector3.zero;
        }

        /// <summary>
        /// 두 차량의 실제 월드 중심점 (오프셋 미포함 순수 물리 좌표)
        /// </summary>
        public Vector3 GetActualCarCenterPosition()
        {
            if (targetVehicle != null)
            {
                if (targetVehicle.childCount >= 2)
                {
                    Vector3 sumPos = Vector3.zero;
                    int count = 0;
                    for (int i = 0; i < targetVehicle.childCount; i++)
                    {
                        Transform child = targetVehicle.GetChild(i);
                        if (child != null && child.gameObject.activeInHierarchy)
                        {
                            sumPos += child.position;
                            count++;
                        }
                    }
                    if (count > 0) return sumPos / count;
                }
                return targetVehicle.position;
            }

            GameObject blueCar = GameObject.Find("Blue_Car_Final");
            GameObject redCar = GameObject.Find("Red_Car") ?? GameObject.Find("Red_Car_Final");

            if (blueCar != null && redCar != null)
            {
                return (blueCar.transform.position + redCar.transform.position) * 0.5f;
            }
            if (blueCar != null) return blueCar.transform.position;
            if (redCar != null) return redCar.transform.position;

            return Vector3.zero;
        }

        /// <summary>
        /// 두 차량의 실제 월드 진행(전방) 방향 벡터
        /// </summary>
        public Vector3 GetActualCarForwardDirection()
        {
            GameObject blueCar = GameObject.Find("Blue_Car_Final");
            if (blueCar != null) return blueCar.transform.forward;

            if (targetVehicle != null)
            {
                if (targetVehicle.childCount > 0)
                {
                    Transform child = targetVehicle.GetChild(0);
                    if (child != null) return child.forward;
                }
                return targetVehicle.forward;
            }

            GameObject redCar = GameObject.Find("Red_Car") ?? GameObject.Find("Red_Car_Final");
            if (redCar != null) return redCar.transform.forward;

            return transform.forward;
        }

        /// <summary>
        /// 두 차량이 나란히 달리지 않고 살짝살짝 앞섰다 뒤로 밀렸다 하는 치열한 레이싱 경쟁 연출
        /// </summary>
        private void UpdateRacingRivalryMotion()
        {
            if (!enableRacingRivalry || !hasStoredCarBasePos) return;

            float time = Time.time * rivalrySpeed;
            // 복합 사인파 + 펄린 노이즈를 섞어 실제 드라이버가 액셀을 밟으며 엎치락뒤치락하는 레이싱 질감 구현
            float wave1 = Mathf.Sin(time * 1.0f);
            float wave2 = Mathf.Sin(time * 2.1f + 1.2f) * 0.35f;
            float noise = (Mathf.PerlinNoise(time * 0.8f, 25.4f) - 0.5f) * 0.3f;
            float surge = (wave1 + wave2 + noise); // 약 -1.3 ~ +1.3 범위
            float forwardOffset = surge * rivalryDistance;

            // 좌우 미세 유격 (핸들 조타 느낌)
            float lateralJitter = (Mathf.PerlinNoise(time * 1.5f, 0f) - 0.5f) * 2f * rivalryLateralJitter;

            if (redCarTransform != null)
            {
                // 빨간 차가 앞서 나가면 (+Z)
                redCarTransform.localPosition = baseRedCarLocalPos + new Vector3(lateralJitter, 0f, forwardOffset);
            }

            if (blueCarTransform != null)
            {
                // 파란 차는 뒤로 밀렸다가 (-Z), 반대로 파란 차가 치고 나가면 빨간 차가 뒤로 빠짐
                blueCarTransform.localPosition = baseBlueCarLocalPos + new Vector3(-lateralJitter, 0f, -forwardOffset);
            }
        }

        public void ResetRacingRivalryMotion()
        {
            if (hasStoredCarBasePos)
            {
                if (redCarTransform != null) redCarTransform.localPosition = baseRedCarLocalPos;
                if (blueCarTransform != null) blueCarTransform.localPosition = baseBlueCarLocalPos;
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            UpdateRacingRivalryMotion();

            // 재시작 키 체크 (New Input System 및 Legacy Input 호환)
            if (IsRestartKeyPressed())
            {
                StartCinematicSequence();
            }

            if (!isSequenceRunning) return;

            sequenceTimer += Time.deltaTime;
            float time = Time.time;

            float launchTime = quietDuration + rumbleBuildUpDuration;

            // 1단계: 조용함 (카메라 정면 응시)
            if (sequenceTimer < quietDuration)
            {
                currentPhase = CinematicPhase.Quiet;
            }
            // 2단계: 진동 고조 (엔진 굉음 빌드업)
            else if (sequenceTimer < launchTime)
            {
                currentPhase = CinematicPhase.RumbleBuilding;
            }
            // 3단계: 스플라인 작동 & 차량 질주 / 풍압 전복
            else
            {
                if (!splineLaunched)
                {
                    LaunchCarsOnSpline();
                }

                // 차량 실시간 위치 및 주행 변위 업데이트
                Vector3 currentCarPos = GetActualCarCenterPosition();
                float currentDistToCam = (currentCarPos != Vector3.zero) ? Vector3.Distance(currentCarPos, transform.position) : 9999f;
                float traveledDist = (carLaunchStartPos != Vector3.zero && currentCarPos != Vector3.zero) ? Vector3.Distance(currentCarPos, carLaunchStartPos) : 0f;

                // 차량의 실제 주행 진행 방향 계산 (이동 델타 우선, 미이동 시 차량 forward)
                Vector3 moveDelta = (lastCarCenterPos != Vector3.zero && currentCarPos != Vector3.zero) ? (currentCarPos - lastCarCenterPos) : Vector3.zero;
                if (moveDelta.sqrMagnitude > 0.0001f)
                {
                    carMovingHeading = moveDelta.normalized;
                }
                lastCarCenterPos = currentCarPos;

                Vector3 carForwardDir = (carMovingHeading != Vector3.zero) ? carMovingHeading : GetActualCarForwardDirection();

                // 차량이 스폰 위치에서 최소 2.0미터 이상 실제로 주행했는지 확인 (시작하자마자 오작동하는 현상 방지)
                if (!hasCarTraveledEnough)
                {
                    bool splineStarted = (splineAnimate != null && splineAnimate.NormalizedTime > 0.02f);
                    if (traveledDist >= 2.0f || splineStarted)
                    {
                        hasCarTraveledEnough = true;
                    }
                }

                // 차량에서 카메라를 바라보는 벡터
                Vector3 carToCam = transform.position - currentCarPos;
                // 차량의 진행 방향 기준으로 카메라가 앞쪽에 있는지(+값), 뒤쪽에 있는지(-값) 계산
                // (차량이 카메라를 향해 올 때는 양수, 카메라 바로 옆을 지날 때 0, 카메라를 지나치면 음수)
                float cameraAheadDistance = Vector3.Dot(carToCam, carForwardDir);
                currentCameraAheadDistance = cameraAheadDistance;

                // 차량이 처음 출발하여 카메라 앞쪽에서 달려오고 있었음을 확인
                if (cameraAheadDistance > 1.0f)
                {
                    hasApproachedFromFront = true;
                }

                // 주행 시작 이후 카메라와의 최단 거리(최근접점) 갱신
                if (hasCarTraveledEnough && currentDistToCam < minCarDistanceToCam)
                {
                    minCarDistanceToCam = currentDistToCam;
                }

                if (enablePitchFlipPass)
                {
                    if (!isPitchFlipReceding)
                    {
                        currentPhase = CinematicPhase.PassByRush;

                        if (currentCameraAheadDistance <= 0.05f && hasCarTraveledEnough && hasApproachedFromFront)
                        {
                            isPitchFlipReceding = true;
                            pitchFlipRecedeTimer = 0f;
                            actualPassTime = sequenceTimer;
                            Debug.Log($"[CameraWindBlastShock] 💥 차량 통과 감지! 거꾸로 뒤집힌 후면 추종 연출 시작! (시간: {sequenceTimer:F2}초)");
                        }
                    }
                    else
                    {
                        float totalTumbleTime = invertedRearLingerDuration + skyTiltUpDuration;
                        if (pitchFlipRecedeTimer < totalTumbleTime)
                        {
                            currentPhase = (pitchFlipRecedeTimer < invertedRearLingerDuration) ? CinematicPhase.PassByRush : CinematicPhase.KnockedDown;
                        }
                        else
                        {
                            skyGazeTimer += Time.deltaTime;
                            if (skyGazeTimer < skyHoldDuration)
                            {
                                currentPhase = CinematicPhase.SkyGazing;
                            }
                            else
                            {
                                currentPhase = CinematicPhase.Completed;
                            }
                        }
                    }
                }
                else if (enableKnockdown)
                {
                    if (!isKnockedDown)
                    {
                        currentPhase = CinematicPhase.PassByRush;

                        bool shouldKnockdown = false;

                        // 차량이 카메라 "바로 옆" 근접 반경(passingProximityRadius, 기본 8m) 이내로 실제로 진입했는지 확인
                        bool isRightNextToCamera = currentDistToCam <= passingProximityRadius;

                        // 차량이 씬에 존재하고, 실제로 주행을 시작했고, 전방에서 다가왔으며, 카메라 바로 옆에 위치한 경우에만 통과 판정!
                        // ※ 타이밍/타이머 방식 완전 제거: 사용자가 속도를 바꾸거나 대기해도 오직 물리적으로 바로 옆을 지나칠 때만 작동
                        // ※ 부스터 훑기 카메라(enableBoosterSweepCam) 활성화 중에는 전복되지 않고 차와 함께 달리며 훑습니다.
                        if (!enableBoosterSweepCam && currentCarPos != Vector3.zero && hasCarTraveledEnough && hasApproachedFromFront && isRightNextToCamera)
                        {
                            // 판정 A: 카메라 평면 통과
                            // 카메라가 차량의 진행방향 앞쪽(+값)에서 뒤쪽(-값)으로 넘어간 순간 (차량 앞범퍼/차체가 카메라를 스쳐 지나간 찰나)
                            bool planeCrossed = (cameraAheadDistance <= -passKnockdownDelayOffset);

                            // 판정 B: 최근접점 변곡 통과
                            // 카메라 바로 옆에서 최단 거리를 찍고 차체가 멀어지기 시작한 순간
                            bool distanceTurned = (currentDistToCam >= minCarDistanceToCam + Mathf.Max(0.25f, passKnockdownDelayOffset));

                            if (planeCrossed || distanceTurned)
                            {
                                shouldKnockdown = true;
                                Debug.Log($"[CameraWindBlastShock] 💥 차량 카메라 바로 옆 통과 인식 성공! (평면통과={planeCrossed}, 거리변곡={distanceTurned}, ahead={cameraAheadDistance:F2}m, 현재거리={currentDistToCam:F2}m, 최근접={minCarDistanceToCam:F2}m)");
                            }
                        }

                        if (shouldKnockdown)
                        {
                            TriggerKnockdownInternal();
                        }
                    }
                    else
                    {
                        // 4단계: 풍압 전복 진행 중 -> 5단계: 하늘 바라보기 (skyHoldDuration) -> 완료
                        if (knockdownProgress < 1f)
                        {
                            currentPhase = CinematicPhase.KnockedDown;
                        }
                        else
                        {
                            skyGazeTimer += Time.deltaTime;
                            if (skyGazeTimer < skyHoldDuration)
                            {
                                currentPhase = CinematicPhase.SkyGazing;
                            }
                            else
                            {
                                currentPhase = CinematicPhase.Completed;
                            }
                        }
                    }
                }
                else
                {
                    currentPhase = CinematicPhase.PassByRush;
                }
            }
        }

        private void TriggerKnockdownInternal()
        {
            if (isKnockedDown) return;
            isKnockedDown = true;
            knockdownProgress = 0f;
            bounceDecay = 1f;
            skyGazeTimer = 0f;
            actualPassTime = sequenceTimer;
            knockdownStartRot = transform.rotation;
            currentPhase = CinematicPhase.KnockedDown;
            Debug.Log($"[CameraWindBlastShock] 💥 차량 통과 감지! 카메라 벌러덩 전복 실행! (시간: {sequenceTimer:F2}초)");
        }

        private void LaunchCarsOnSpline()
        {
            splineLaunched = true;

            Vector3 carPos = GetActualCarCenterPosition();
            carLaunchStartPos = carPos;
            lastCarCenterPos = carPos;
            if (carPos != Vector3.zero)
            {
                initialCarDistanceToCam = Vector3.Distance(carPos, transform.position);
                minCarDistanceToCam = initialCarDistanceToCam;
            }
            else
            {
                initialCarDistanceToCam = 9999f;
                minCarDistanceToCam = 9999f;
            }
            hasCarTraveledEnough = false;
            actualPassTime = -1f;

            if (splineAnimate != null)
            {
                if (!keepUserSplineSpeed)
                {
                    // Speed 모드인 경우 사용자가 지정한 크루즈 스피드 적용
                    if (splineAnimate.AnimationMethod == SplineAnimate.Method.Speed)
                    {
                        splineAnimate.MaxSpeed = cruiseSpeed;
                    }
                    else if (splineDriveDuration > 0f)
                    {
                        splineAnimate.Duration = splineDriveDuration;
                    }
                }
                splineAnimate.Restart(true);
            }

            if (startCarsWithFullBoosters)
            {
                ApplyVehiclesFullBoosted();
            }
            else if (triggerBoosterOnLaunch)
            {
                ApplyVehiclesNormalState();

                if (boosterTransformDelay <= 0f)
                {
                    TriggerAllVehiclesBoosterTransformation();
                    StartCoroutine(DelayedBoosterCompletionRoutine(transformationDuration));
                }
                else
                {
                    StartCoroutine(DelayedBoosterTriggerRoutine(boosterTransformDelay));
                }
            }
        }

        private IEnumerator DelayedBoosterTriggerRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerAllVehiclesBoosterTransformation();

            float waitDuration = enableBoosterSweepCam ? Mathf.Max(transformationDuration, sweepDuration + sweepStartDelay) : transformationDuration;
            yield return new WaitForSeconds(Mathf.Max(0.5f, waitDuration));

            if (leaveCameraBehindOnBoost)
            {
                DetachCameraAndRocketLaunch();
            }
            else
            {
                ActivateFullBoostersWithoutDetaching();
            }
        }

        private IEnumerator DelayedBoosterCompletionRoutine(float duration)
        {
            float waitDuration = enableBoosterSweepCam ? Mathf.Max(duration, sweepDuration + sweepStartDelay) : duration;
            yield return new WaitForSeconds(Mathf.Max(0.5f, waitDuration));

            if (leaveCameraBehindOnBoost)
            {
                DetachCameraAndRocketLaunch();
            }
            else
            {
                ActivateFullBoostersWithoutDetaching();
            }
        }

        /// <summary>
        /// 부스터 변형 완료 직후: 카메라를 노면에 남겨두고 두 차량이 폭발적인 부스터 가속(부와앙-)으로 질주하는 연출
        /// </summary>
        [ContextMenu("💥 카메라 두고 부스터 폭발 발진 (부와앙-!)")]
        public void DetachCameraAndRocketLaunch()
        {
            if (isCameraDetached) return;

            // 1. 카메라 월드 좌표 스냅샷 기록 & 분리 플래그 활성화 (계층 구조 변경 없이 LateUpdate에서 월드 좌표 고정)
            detachedWorldPos = transform.position;
            detachedWorldRot = transform.rotation;
            isCameraDetached = true;

            // 2. 두 차량 최종 부스터 100% 동시 풀 점등
            var blueCtrl = FindFirstObjectByType<VehicleTransformationController>();
            var redCtrl = FindFirstObjectByType<BoosterDeploymentController>();

            if (blueCtrl != null)
            {
                blueCtrl.SetSubBoosterFxActive(true);
                blueCtrl.SetMainBoosterFxActive(true);
            }

            if (redCtrl != null)
            {
                redCtrl.SnapToFullDeployment();
            }

            // 3. 폭발적인 부스터 속도 급가속 (부와앙-!)
            if (boostAccelerationRoutine != null) StopCoroutine(boostAccelerationRoutine);
            boostAccelerationRoutine = StartCoroutine(AnimateBoostRocketSpeed(boostRocketSpeed, boostAccelerationDuration));

            // 4. 카메라 부스터 후폭풍 진동 트리거
            boostBlastTimer = 0f;
            isBoostBlastActive = true;

            Debug.Log("[CameraWindBlastShock] 💥 카메라를 두고 두 차량 부스터 동시 점화 & 폭발적 초고속 질주 발진 (부와앙-!)");
        }

        /// <summary>
        /// 카메라를 노면에 남겨두지 않고, 카메라가 차량에 탑승/추종한 상태로 부스터를 풀 점등하고 함께 계속 질주 (leaveCameraBehindOnBoost = false)
        /// </summary>
        [ContextMenu("🏎️ 카메라 동승 부스터 풀 발진 (분리 안 함)")]
        public void ActivateFullBoostersWithoutDetaching()
        {
            isCameraDetached = false;

            // 1. 두 차량 최종 부스터 100% 동시 풀 점등
            var blueCtrl = FindFirstObjectByType<VehicleTransformationController>();
            var redCtrl = FindFirstObjectByType<BoosterDeploymentController>();

            if (blueCtrl != null)
            {
                blueCtrl.SetSubBoosterFxActive(true);
                blueCtrl.SetMainBoosterFxActive(true);
            }

            if (redCtrl != null)
            {
                redCtrl.SnapToFullDeployment();
            }

            // 2. 카메라가 함께 탑승한 상태로 부스터 속도 가속
            if (boostAccelerationRoutine != null) StopCoroutine(boostAccelerationRoutine);
            boostAccelerationRoutine = StartCoroutine(AnimateBoostRocketSpeed(boostRocketSpeed, boostAccelerationDuration));

            // 3. 탑승 온보드 부스터 후폭풍 진동 트리거
            boostBlastTimer = 0f;
            isBoostBlastActive = true;

            Debug.Log("[CameraWindBlastShock] 🏎️ 카메라 분리 없이(함께 동승) 두 차량 부스터 풀 점화 & 질주!");
        }

        /// <summary>
        /// '카메라 두고 달려가기' 연출 켜기 (ON)
        /// </summary>
        [ContextMenu("💥 [토글] 카메라 두고 달려가기 켜기 (ON)")]
        public void ToggleLeaveCameraBehindOn()
        {
            leaveCameraBehindOnBoost = true;
            Debug.Log("[CameraWindBlastShock] 💥 '카메라 두고 달려가기' 연출 활성화 (ON) - 부스터 변형 후 카메라를 도로에 남겨두고 차만 달려갑니다.");
        }

        /// <summary>
        /// '카메라 두고 달려가기' 연출 끄기 (OFF - 카메라가 차와 함께 계속 달림)
        /// </summary>
        [ContextMenu("🏎️ [토글] 카메라 두고 달려가기 끄기 (OFF - 함께 주행)")]
        public void ToggleLeaveCameraBehindOff()
        {
            leaveCameraBehindOnBoost = false;
            isCameraDetached = false;
            Debug.Log("[CameraWindBlastShock] 🏎️ '카메라 두고 달려가기' 연출 비활성화 (OFF) - 카메라가 분리되지 않고 차량과 함께 계속 주행합니다.");
        }

        /// <summary>
        /// '광고 CF 스타일 세로 훑기 (위 -> 아래)' 켜기 (ON)
        /// </summary>
        [ContextMenu("🏎️ [토글] CF 세로 훑기 켜기 (ON - 사진 1->4번 구도)")]
        public void ToggleVerticalAdSweepCamOn()
        {
            enableBoosterSweepCam = true;
            enableVerticalAdSweepCam = true;
            sweepTimer = 0f;
            Debug.Log("[CameraWindBlastShock] 🏎️ 광고 CF 스타일 세로 훑기 카메라 활성화 (ON) - 차와 함께 달리며 위에서 아래로 훑습니다.");
        }

        /// <summary>
        /// '광고 CF 스타일 세로 훑기 (위 -> 아래)' 끄기 (OFF)
        /// </summary>
        [ContextMenu("🎥 [토글] CF 세로 훑기 끄기 (OFF)")]
        public void ToggleVerticalAdSweepCamOff()
        {
            enableVerticalAdSweepCam = false;
            Debug.Log("[CameraWindBlastShock] 🎥 광고 CF 스타일 세로 훑기 비활성화 (OFF)");
        }

        /// <summary>
        /// '부스터 변형 훑기 카메라' 연출 켜기 (ON)
        /// </summary>
        [ContextMenu("🎬 [토글] 부스터 변형 훑기 카메라 켜기 (ON)")]
        public void ToggleBoosterSweepCamOn()
        {
            enableBoosterSweepCam = true;
            isCameraDetached = false;
            sweepTimer = 0f;
            if (targetVehicle != null)
            {
                sweepCamLocalOffset = Quaternion.Inverse(targetVehicle.rotation) * (transform.position - targetVehicle.position);
                sweepCamLocalRot = Quaternion.Inverse(targetVehicle.rotation) * transform.rotation;
                sweepCamOffsetInitialized = true;
            }
            Debug.Log("[CameraWindBlastShock] 🎬 부스터 변형 훑기 카메라 연출 활성화 (ON) - 현재 카메라 구도로 차량을 추종하며 부스터 변형을 천천히 훑습니다.");
        }

        /// <summary>
        /// '부스터 변형 훑기 카메라' 연출 끄기 (OFF - 기본 노면/추적 카메라로 복귀)
        /// </summary>
        [ContextMenu("🎥 [토글] 부스터 변형 훑기 카메라 끄기 (OFF - 기본 카메라)")]
        public void ToggleBoosterSweepCamOff()
        {
            enableBoosterSweepCam = false;
            if (transform.parent == null)
            {
                transform.position = initialWorldPos;
                transform.rotation = initialWorldRot;
            }
            else
            {
                transform.localPosition = initialLocalPos;
                transform.localRotation = initialLocalRot;
            }
            Debug.Log("[CameraWindBlastShock] 🎥 부스터 변형 훑기 카메라 연출 비활성화 (OFF) - 기본 카메라 구도로 복귀합니다.");
        }

        /// <summary>
        /// '부스터 변형 훑기 카메라 - 거울 반대 모드' 켜기 (ON: 파란 차 측면에서 파란 차부터 훑기)
        /// </summary>
        [ContextMenu("🪞 [토글] 거울 반대 모드 켜기 (ON - 파란차부터 훑기)")]
        public void ToggleMirrorBoosterSweepCamOn()
        {
            mirrorBoosterSweepCam = true;
            if (!Application.isPlaying)
            {
                ApplySweepCamStaticPose(true);
            }
            Debug.Log("[CameraWindBlastShock] 🪞 부스터 훑기 거울 반대 모드 활성화 (ON) - 스플라인(2) 기준 반대편(파란 차 측면)에서 파란 차 부스터부터 훑습니다.");
        }

        /// <summary>
        /// '부스터 변형 훑기 카메라 - 거울 반대 모드' 끄기 (OFF: 기본 빨간 차 측면에서 빨간 차부터 훑기)
        /// </summary>
        [ContextMenu("🎬 [토글] 거울 반대 모드 끄기 (OFF - 빨간차부터 훑기)")]
        public void ToggleMirrorBoosterSweepCamOff()
        {
            mirrorBoosterSweepCam = false;
            if (!Application.isPlaying)
            {
                ApplySweepCamStaticPose(false);
            }
            Debug.Log("[CameraWindBlastShock] 🎬 부스터 훑기 거울 반대 모드 비활성화 (OFF) - 기본 빨간 차 측면에서 빨간 차 부스터부터 훑습니다.");
        }

        /// <summary>
        /// 에디터 모드에서 거울 반대 모드 / 기본 모드 카메라 위치 및 각도 즉시 정적 적용
        /// </summary>
        public void ApplySweepCamStaticPose(bool mirror)
        {
            AutoFindComponents();
            if (targetVehicle == null) return;

            if (!sweepCamOffsetInitialized)
            {
                sweepCamLocalOffset = Quaternion.Inverse(targetVehicle.rotation) * (transform.position - targetVehicle.position);
                sweepCamLocalRot = Quaternion.Inverse(targetVehicle.rotation) * transform.rotation;
                sweepCamOffsetInitialized = true;
                if (!hasBaseRedSideOffset)
                {
                    baseRedSideLocalOffset = sweepCamLocalOffset;
                    hasBaseRedSideOffset = true;
                }
            }

            Vector3 effectiveOffset = hasBaseRedSideOffset ? baseRedSideLocalOffset : sweepCamLocalOffset;
            if (mirror)
            {
                effectiveOffset.x = -effectiveOffset.x;
            }

            transform.position = targetVehicle.position + (targetVehicle.rotation * effectiveOffset);

            Vector3 startFocus = mirror ? GetBlueBoosterFocusPoint() : GetRedBoosterFocusPoint();
            float startPan = mirror ? -sweepStartPanAngle : sweepStartPanAngle;
            float roll = mirror ? -sweepCamRoll : sweepCamRoll;

            Vector3 toAim = startFocus - transform.position;
            if (toAim.sqrMagnitude > 0.001f)
            {
                Quaternion baseAimRot = Quaternion.LookRotation(toAim.normalized, targetVehicle.up);
                transform.rotation = baseAimRot * Quaternion.Euler(0f, startPan, roll);
            }
        }

        /// <summary>
        /// '경쟁 주행(앞서거니 뒤서거니)' 연출 켜기 (ON)
        /// </summary>
        [ContextMenu("🏁 [토글] 두 차량 경쟁 주행 켜기 (ON)")]
        public void ToggleRacingRivalryOn()
        {
            enableRacingRivalry = true;
            FindCarTransforms();
            Debug.Log("[CameraWindBlastShock] 🏁 두 차량 경쟁 주행(앞서거니 뒤서거니) 연출 활성화 (ON)");
        }

        /// <summary>
        /// '경쟁 주행' 연출 끄기 (OFF - 기본 나란히 주행)
        /// </summary>
        [ContextMenu("🚗 [토글] 두 차량 경쟁 주행 끄기 (OFF - 나란히 주행)")]
        public void ToggleRacingRivalryOff()
        {
            enableRacingRivalry = false;
            ResetRacingRivalryMotion();
            Debug.Log("[CameraWindBlastShock] 🚗 두 차량 경쟁 주행 비활성화 (OFF) - 기본 나란히 위치로 복귀");
        }

        private IEnumerator AnimateBoostRocketSpeed(float targetSpeed, float duration)
        {
            if (splineAnimate == null) yield break;

            float startSpeed = splineAnimate.MaxSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curve = Mathf.Sin(t * Mathf.PI * 0.5f);
                splineAnimate.MaxSpeed = Mathf.Lerp(startSpeed, targetSpeed, curve);
                yield return null;
            }

            splineAnimate.MaxSpeed = targetSpeed;
        }

        public void ResetBoosterLaunchState()
        {
            if (boostAccelerationRoutine != null)
            {
                StopCoroutine(boostAccelerationRoutine);
                boostAccelerationRoutine = null;
            }

            isBoostBlastActive = false;
            boostBlastTimer = 0f;

            if (isCameraDetached)
            {
                isCameraDetached = false;
                transform.localPosition = initialLocalPos;
                transform.localRotation = initialLocalRot;
            }

            if (splineAnimate != null)
            {
                splineAnimate.MaxSpeed = cruiseSpeed;
            }

            ApplyVehiclesNormalState();
        }

        /// <summary>
        /// 블루카 및 레드카의 부스터 변형 시퀀스를 동시에 시작
        /// </summary>
        [ContextMenu("🚀 두 차량 부스터 변형 시퀀스 동시 시작")]
        public void TriggerAllVehiclesBoosterTransformation()
        {
            // 블루카 풀 변형 시퀀스 트리거
            var blueCtrl = FindFirstObjectByType<VehicleTransformationController>();
            if (blueCtrl != null)
            {
                blueCtrl.DeployFullTransformation();
            }

            // 레드카 부스터 트리거
            var redCtrl = FindFirstObjectByType<BoosterDeploymentController>();
            if (redCtrl != null)
            {
                redCtrl.DeployBoosters();
            }

            Debug.Log("[CameraWindBlastShock] 🚀 두 차량 주행 중 1초 대기 후 부스터 변형 시퀀스 시작!");
        }

        /// <summary>
        /// 블루카 및 레드카를 원래 기본 미변형 상태(0%)로 즉시 복귀
        /// </summary>
        [ContextMenu("🔄 두 차량 기본 미변형 모습 복귀 (0%)")]
        public void ApplyVehiclesNormalState()
        {
            var blueCtrl = FindFirstObjectByType<VehicleTransformationController>();
            if (blueCtrl != null)
            {
                blueCtrl.SnapToNormalState();
            }

            var redCtrl = FindFirstObjectByType<BoosterDeploymentController>();
            if (redCtrl != null)
            {
                redCtrl.SnapToFullRetraction();
            }
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (!isInitialized) StoreInitialPose();

            float time = Time.time;
            Vector3 shakePos = Vector3.zero;
            Vector3 shakeRotEuler = Vector3.zero;

            float launchTime = quietDuration + rumbleBuildUpDuration;

            bool hasNoShake = maxRumbleShake <= 0.00001f && maxRumbleAngleShake <= 0.00001f;

            // 거리 기반 아스팔트 지면 진동 배율 실시간 산출 (멀리 있으면 0 -> 가까워질수록 1.0 점진 고조)
            float distanceRumbleFactor = 0f;
            if (useDistanceBasedRumble)
            {
                Vector3 currentCarPos = GetActualCarCenterPosition();
                if (currentCarPos != Vector3.zero)
                {
                    float dist = Vector3.Distance(currentCarPos, transform.position);
                    currentDistanceToCar = dist;

                    if (splineLaunched && !isKnockedDown)
                    {
                        Vector3 carToCam = transform.position - currentCarPos;
                        Vector3 carForwardDir = (carMovingHeading != Vector3.zero) ? carMovingHeading : GetActualCarForwardDirection();
                        float cameraAheadDistance = Vector3.Dot(carToCam, carForwardDir);

                        // 차량이 아직 카메라 앞쪽에서 달려오는 중
                        if (cameraAheadDistance > -passKnockdownDelayOffset)
                        {
                            if (dist < rumbleStartDistance)
                            {
                                float t = Mathf.Clamp01(1f - (dist / rumbleStartDistance));
                                distanceRumbleFactor = Mathf.Pow(t, rumbleCurvePower);
                            }
                            else
                            {
                                distanceRumbleFactor = 0f; // 멀리 있을 때는 완전 조용함 (지면 무진동)
                            }
                        }
                        else
                        {
                            // 차량이 카메라를 지나쳐 멀어지는 중 (또는 통과 직후)
                            if (actualPassTime > 0f)
                            {
                                float elapsedAfterPass = sequenceTimer - actualPassTime;
                                float decay = Mathf.Clamp01(1f - (elapsedAfterPass / Mathf.Max(0.1f, passByCalmDownDuration)));
                                distanceRumbleFactor = Mathf.Pow(decay, 2.5f);
                            }
                            else if (dist < rumbleStartDistance)
                            {
                                float t = Mathf.Clamp01(1f - (dist / rumbleStartDistance));
                                distanceRumbleFactor = Mathf.Pow(t, rumbleCurvePower);
                            }
                        }
                    }
                }

                if (isBoostBlastActive)
                {
                    boostBlastTimer += Time.deltaTime;
                    float blastT = Mathf.Clamp01(boostBlastTimer / Mathf.Max(0.1f, passByCalmDownDuration));
                    float decay = Mathf.Pow(1f - blastT, 3.0f);
                    distanceRumbleFactor = Mathf.Max(distanceRumbleFactor, decay * 1.5f);
                    if (blastT >= 1f) isBoostBlastActive = false;
                }

                currentRumbleIntensity = distanceRumbleFactor;
            }

            if (!isKnockedDown)
            {
                if (hasNoShake || currentPhase == CinematicPhase.Quiet)
                {
                    // 완전 정적 (사용자가 인스펙터에서 흔들림 0으로 조절했거나 Quiet 구간)
                    shakePos = Vector3.zero;
                    shakeRotEuler = Vector3.zero;
                }
                else if (useDistanceBasedRumble)
                {
                    if (distanceRumbleFactor <= 0.0001f)
                    {
                        shakePos = Vector3.zero;
                        shakeRotEuler = Vector3.zero;
                    }
                    else
                    {
                        float noiseTime = time * shakeFrequency;
                        float nX = (Mathf.PerlinNoise(noiseTime, 15.3f) - 0.5f) * 2f;
                        float nY = (Mathf.PerlinNoise(32.1f, noiseTime) - 0.5f) * 2f;
                        float nRot = (Mathf.PerlinNoise(noiseTime * 0.85f, noiseTime * 0.85f) - 0.5f) * 2f;

                        // 아스팔트 지면 수직 진동(상하 바운스)과 좌우 진동 합성
                        Vector3 localShake = new Vector3(
                            nX * 0.5f,
                            nY * verticalRumbleMultiplier,
                            0f
                        ) * (maxRumbleShake * distanceRumbleFactor);

                        shakePos = initialWorldRot * localShake;
                        shakeRotEuler = new Vector3(
                            nY * 0.7f,
                            nX * 0.5f,
                            nRot * 0.8f
                        ) * (maxRumbleAngleShake * distanceRumbleFactor);
                    }
                }
                else if (currentPhase == CinematicPhase.RumbleBuilding)
                {
                    // 진동이 점점 거세짐 (1초~3초 구간)
                    float t = Mathf.Clamp01((sequenceTimer - quietDuration) / Mathf.Max(0.01f, rumbleBuildUpDuration));
                    float curve = Mathf.Pow(t, 2.2f); // 서서히 거세지다가 출발 직전에 폭발적으로 진동

                    float noiseTime = time * shakeFrequency;
                    float nX = (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 2f;
                    float nY = (Mathf.PerlinNoise(0f, noiseTime) - 0.5f) * 2f;
                    float nRot = (Mathf.PerlinNoise(noiseTime * 0.7f, noiseTime * 0.7f) - 0.5f) * 2f;

                    shakePos = new Vector3(nX, nY, 0f) * (maxRumbleShake * curve);
                    shakeRotEuler = new Vector3(nY * 0.6f, nX * 0.6f, nRot) * (maxRumbleAngleShake * curve);
                }
                else if (currentPhase == CinematicPhase.PassByRush)
                {
                    if (hasNoShake)
                    {
                        shakePos = Vector3.zero;
                        shakeRotEuler = Vector3.zero;
                    }
                    else
                    {
                        float currentTurbulence = passByTurbulenceMultiplier;

                        if (!continuousOnboardShake)
                        {
                            // 카메라 옆으로 차들이 실제로 통과한 이후에만 급격하게 진동 감쇠
                            if (actualPassTime <= 0f)
                            {
                                currentTurbulence = passByTurbulenceMultiplier;
                            }
                            else
                            {
                                float elapsedAfterPass = sequenceTimer - actualPassTime;
                                float t = Mathf.Clamp01(elapsedAfterPass / Mathf.Max(0.1f, passByCalmDownDuration));
                                float decay = Mathf.Pow(1f - t, 3.0f);
                                currentTurbulence = passByTurbulenceMultiplier * decay;
                            }
                        }

                        if (isBoostBlastActive)
                        {
                            boostBlastTimer += Time.deltaTime;
                            float blastT = Mathf.Clamp01(boostBlastTimer / Mathf.Max(0.1f, passByCalmDownDuration));
                            float decay = Mathf.Pow(1f - blastT, 3.0f);
                            currentTurbulence = Mathf.Max(currentTurbulence, passByTurbulenceMultiplier * 2.5f * decay);
                            if (blastT >= 1f) isBoostBlastActive = false;
                        }

                        float noiseTime = time * (shakeFrequency * 1.1f);
                        float nX = (Mathf.PerlinNoise(noiseTime, 15.3f) - 0.5f) * 2f;
                        float nY = (Mathf.PerlinNoise(32.1f, noiseTime) - 0.5f) * 2f;
                        float nRot = (Mathf.PerlinNoise(noiseTime, noiseTime) - 0.5f) * 2f;

                        shakePos = new Vector3(nX, nY, 0f) * (maxRumbleShake * currentTurbulence);
                        shakeRotEuler = new Vector3(nY, nX, nRot) * (maxRumbleAngleShake * currentTurbulence);
                    }
                }

                Quaternion baseRot = isCameraDetached ? detachedWorldRot : initialWorldRot;

                if (enableVehicleTracking)
                {
                    Vector3 targetWorldPos = GetTrackingTargetWorldPosition();
                    if (targetWorldPos != Vector3.zero)
                    {
                        if (leadAheadDistance > 0.01f && targetVehicle != null)
                        {
                            targetWorldPos += targetVehicle.forward * leadAheadDistance;
                        }

                        Vector3 toTarget = targetWorldPos - transform.position;
                        float distToTarget = toTarget.magnitude;

                        if (distToTarget > 0.01f)
                        {
                            Vector3 dir = toTarget / distToTarget;

                            // 수직 아래(카메라 바로 밑)로 통과할 때 Gimbal Lock / 180도 회전 튀는 현상 방지용 Up 벡터 구성
                            Vector3 upRef = Vector3.up;
                            if (Vector3.Dot(dir, -Vector3.up) > 0.95f)
                            {
                                Vector3 forwardRef = targetVehicle != null ? targetVehicle.forward : transform.forward;
                                forwardRef.y = 0f;
                                if (forwardRef.sqrMagnitude > 0.001f)
                                {
                                    forwardRef.Normalize();
                                    Vector3 right = Vector3.Cross(Vector3.up, forwardRef).normalized;
                                    upRef = Vector3.Cross(right, dir).normalized;
                                }
                            }

                            Quaternion targetLookRot;

                            if (!allowPitchTracking)
                            {
                                Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
                                if (flatDir.sqrMagnitude > 0.001f)
                                {
                                    targetLookRot = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
                                    Vector3 e = targetLookRot.eulerAngles;
                                    e.x = initialWorldRot.eulerAngles.x;
                                    targetLookRot = Quaternion.Euler(e);
                                }
                                else
                                {
                                    targetLookRot = currentTrackingRot;
                                }
                            }
                            else
                            {
                                // 상하 Pitch 각도 계산: 수평 거리 대비 높낮이 각도 (아래가 양수)
                                float horizDist = Mathf.Sqrt(toTarget.x * toTarget.x + toTarget.z * toTarget.z);
                                float verticalAngle = -Mathf.Atan2(toTarget.y, horizDist) * Mathf.Rad2Deg;

                                // maxUpwardPitch(-25도) ~ maxDownwardPitch(88도)로 안전하게 클램프
                                float clampedPitch = Mathf.Clamp(verticalAngle, maxUpwardPitch, maxDownwardPitch);

                                if (Mathf.Abs(clampedPitch - verticalAngle) > 0.1f && horizDist > 0.001f)
                                {
                                    float rad = clampedPitch * Mathf.Deg2Rad;
                                    Vector3 flatDirNorm = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
                                    Vector3 clampedDir = flatDirNorm * Mathf.Cos(rad) - Vector3.up * Mathf.Sin(rad);
                                    targetLookRot = Quaternion.LookRotation(clampedDir.normalized, Vector3.up);
                                }
                                else
                                {
                                    targetLookRot = Quaternion.LookRotation(dir, upRef);
                                }
                            }

                            // 카메라 바로 아래/근처를 지날 때 추종 속도 동적 부스트 (차량이 밑으로 지나갈 때 놓쳐서 위로 뜨는 현상 방지)
                            float effectiveSmoothness = trackingSmoothness;
                            if (closePassSpeedBoost > 1f && distToTarget < 25f)
                            {
                                float closeFactor = Mathf.Clamp01(1f - (distToTarget / 25f));
                                effectiveSmoothness *= Mathf.Lerp(1f, closePassSpeedBoost, closeFactor);
                            }

                            if (effectiveSmoothness > 0f)
                            {
                                currentTrackingRot = Quaternion.Slerp(currentTrackingRot, targetLookRot, Time.deltaTime * effectiveSmoothness);
                            }
                            else
                            {
                                currentTrackingRot = targetLookRot;
                            }

                            baseRot = currentTrackingRot;
                        }
                    }
                }

                if (enablePitchFlipPass)
                {
                    float calculatedPitch = pitchAngleApproach;
                    float skyShakeFade = 0.15f;

                    Vector3 currentCarPos = GetActualCarCenterPosition();
                    Vector3 camPos = lockPitchFlipPosition ? pitchFlipFixedPos : ((transform.parent != null) ? transform.parent.TransformPoint(pitchFlipFixedPos) : pitchFlipFixedPos);

                    if (currentCarPos != Vector3.zero && splineLaunched)
                    {
                        // 카메라 고정 위치 기준 거리
                        // 차량이 접근 중: carPos.x < camPos.x => d_ahead > 0
                        // 차량이 통과 후: carPos.x > camPos.x => d_ahead < 0
                        float d_ahead = camPos.x - currentCarPos.x;
                        currentCameraAheadDistance = d_ahead;

                        // 높이 차이 (카메라 높이 1.98m 대비 차체 중심 높이)
                        float heightDiff = Mathf.Max(0.2f, camPos.y - (currentCarPos.y + 0.35f));

                        if (d_ahead > 0.05f && !isPitchFlipReceding)
                        {
                            // =========================================================================
                            // [1단계] 다가오는 차량 접근 실시간 락온 추종 (0도 ~ 90도)
                            // =========================================================================
                            // 8도 망원 렌즈에서도 차량이 화면 밖으로 이탈하지 않도록 삼각함수로 각도 직결
                            float physicalLookPitch = Mathf.Atan2(heightDiff, Mathf.Max(0.01f, d_ahead)) * Mathf.Rad2Deg;

                            // 화면 중심에 안정적으로 락온
                            float targetPitch = Mathf.Max(pitchAngleApproach, physicalLookPitch + approachFramingOffset);

                            // 차량의 속도에 맞춰 밀착 추종 (원거리에선 차분하게, 카메라 바로 밑 0m 근접 시 90도로 휙 가속)
                            currentPitchFlipAngle = Mathf.Lerp(currentPitchFlipAngle, targetPitch, Time.deltaTime * pitchTrackingResponsiveness);
                            calculatedPitch = currentPitchFlipAngle;
                        }
                        else
                        {
                            // 통과 순간 즉시 2단계 진입
                            if (!isPitchFlipReceding)
                            {
                                isPitchFlipReceding = true;
                                pitchFlipRecedeTimer = 0f;
                                actualPassTime = sequenceTimer;
                            }

                            // =========================================================================
                            // [2단계 & 3단계] 통과 후 거꾸로 후면 감상 ➔ 밤하늘 감속 안착 (90도 ~ 265도)
                            // =========================================================================
                            pitchFlipRecedeTimer += Time.deltaTime;

                            float d_receding = Mathf.Max(0.01f, currentCarPos.x - camPos.x);

                            if (pitchFlipRecedeTimer < invertedRearLingerDuration)
                            {
                                // [2단계: 후면 감상] 거꾸로 뒤집힌 채(150~178도) 멀어지는 차량 후면 배기구를 화면에 담으며 여운 남기기
                                // 기하학적 후면 조준 각도: 180도 - Atan2(heightDiff, d_receding)
                                float invertedTargetPitch = 180.0f - (Mathf.Atan2(heightDiff, d_receding) * Mathf.Rad2Deg);
                                currentPitchFlipAngle = Mathf.Lerp(currentPitchFlipAngle, invertedTargetPitch, Time.deltaTime * pitchTrackingResponsiveness);
                                calculatedPitch = currentPitchFlipAngle;
                                skyTransitionStartPitch = currentPitchFlipAngle;
                            }
                            else
                            {
                                // [3단계: 밤하늘 안착] 후면 감상 완료 후, 부드럽게 시작하여(초기 속도 0) 점점 밤하늘(265도)로 올라가며 마지막에 완전히 멈추기 (SmootherStep)
                                if (skyTransitionStartPitch < 0f)
                                {
                                    skyTransitionStartPitch = currentPitchFlipAngle;
                                }

                                float skyT = Mathf.Clamp01((pitchFlipRecedeTimer - invertedRearLingerDuration) / Mathf.Max(0.1f, skyTiltUpDuration));

                                // Ken Perlin's SmootherStep (6t^5 - 15t^4 + 10t^3)
                                // t=0에서 미분계수(속도)가 0이므로 휙 채가듯 급발진하지 않고, 극도로 부드럽고 차분하게 고개를 들기 시작함.
                                // t=1에서도 미분계수(속도)가 0이므로 265도 밤하늘에서 우아하고 완벽하게 감속 정지(Ease-Out)함.
                                float smoothSkyT = skyT * skyT * skyT * (skyT * (skyT * 6f - 15f) + 10f);

                                calculatedPitch = Mathf.Lerp(skyTransitionStartPitch, pitchAngleSkyEnding, smoothSkyT);
                                currentPitchFlipAngle = calculatedPitch;

                                skyShakeFade = Mathf.Lerp(0.15f, 0f, skyT);
                            }
                        }
                    }
                    else
                    {
                        calculatedPitch = pitchAngleApproach;
                        currentPitchFlipAngle = calculatedPitch;
                    }

                    // 카메라 위치 고정
                    if (lockPitchFlipPosition)
                    {
                        if (transform.parent != null)
                        {
                            transform.localPosition = pitchFlipFixedPos;
                        }
                        else
                        {
                            transform.position = pitchFlipFixedPos;
                        }
                    }

                    // 회전: 오직 빨간색 회전 링(Pitch X축)만 주욱 회전!
                    Quaternion basePitchRot = Quaternion.Euler(calculatedPitch, pitchFlipFixedYaw, pitchFlipFixedRoll);
                    transform.localRotation = basePitchRot * Quaternion.Euler(shakeRotEuler * skyShakeFade);
                }
                else if ((enableBoosterSweepCam || enableOrbitSweepCam) && targetVehicle != null && !isCameraDetached)
                {
                    sweepTimer += Time.deltaTime;

                    float sweepT = Mathf.Clamp01((sweepTimer - sweepStartDelay) / Mathf.Max(0.1f, sweepDuration));
                    float smoothSweepT = Mathf.SmoothStep(0f, 1f, sweepT);

                    if (enableVerticalAdSweepCam || enableOrbitSweepCam)
                    {
                        // 🏎️ 사진 1~5번 5단계 궤적을 3차 곡선 및 쿼터니언 Slerp로 회전 링 돌리듯 물 흐르듯 훑기
                        ApplyOrbitSweepPose(sweepT);
                    }
                    else if (sweepCamOffsetInitialized)
                    {
                        // 1. 차량과 함께 달리는 위치 추종 (기본 vs 거울 모드에 따라 스플라인(2) 중심선 기준 X축 반전 반영)
                        Vector3 effectiveOffset = hasBaseRedSideOffset ? baseRedSideLocalOffset : sweepCamLocalOffset;
                        if (mirrorBoosterSweepCam)
                        {
                            effectiveOffset.x = -effectiveOffset.x;
                        }

                        Vector3 followPos = targetVehicle.position + (targetVehicle.rotation * effectiveOffset);
                        transform.position = followPos + shakePos;

                        // 2. 부스터 변형 모습을 천천히 훑는 회전 연출 (기존 좌우 Sweep)
                        Vector3 redBoosterFocus = GetRedBoosterFocusPoint();
                        Vector3 blueBoosterFocus = GetBlueBoosterFocusPoint();

                        Vector3 startFocus = mirrorBoosterSweepCam ? blueBoosterFocus : redBoosterFocus;
                        Vector3 endFocus = mirrorBoosterSweepCam ? redBoosterFocus : blueBoosterFocus;

                        float startPan = mirrorBoosterSweepCam ? -sweepStartPanAngle : sweepStartPanAngle;
                        float endPan = mirrorBoosterSweepCam ? -sweepEndPanAngle : sweepEndPanAngle;
                        float roll = mirrorBoosterSweepCam ? -sweepCamRoll : sweepCamRoll;

                        Vector3 currentAimPoint = Vector3.Lerp(startFocus, endFocus, smoothSweepT);
                        float currentPanAngle = Mathf.Lerp(startPan, endPan, smoothSweepT);

                        Vector3 toAim = currentAimPoint - transform.position;
                        if (toAim.sqrMagnitude > 0.001f)
                        {
                            Quaternion baseAimRot = Quaternion.LookRotation(toAim.normalized, targetVehicle.up);
                            Quaternion sweepLookRot = baseAimRot * Quaternion.Euler(0f, currentPanAngle, roll);
                            transform.rotation = sweepLookRot * Quaternion.Euler(shakeRotEuler);
                        }
                        else
                        {
                            transform.rotation = targetVehicle.rotation * sweepCamLocalRot * Quaternion.Euler(shakeRotEuler);
                        }
                    }
                }
                else if (isCameraDetached)
                {
                    transform.position = detachedWorldPos + shakePos;
                    transform.rotation = baseRot * Quaternion.Euler(shakeRotEuler);
                }
                else
                {
                    if (shakePos.sqrMagnitude > 0.000001f)
                    {
                        if (transform.parent != null)
                        {
                            transform.localPosition = initialLocalPos + transform.parent.InverseTransformVector(shakePos);
                        }
                        else
                        {
                            transform.position = initialWorldPos + shakePos;
                        }
                    }
                    else
                    {
                        // 흔들림이 없을 때는 사용자가 씬/인스펙터에서 자유롭게 조정한 위치를 새 시작 기준으로 실시간 동기화
                        initialLocalPos = transform.localPosition;
                        initialWorldPos = transform.position;
                    }

                    if (enableVehicleTracking)
                    {
                        transform.rotation = baseRot * Quaternion.Euler(shakeRotEuler);
                    }
                    else if (shakeRotEuler.sqrMagnitude > 0.000001f)
                    {
                        transform.localRotation = initialLocalRot * Quaternion.Euler(shakeRotEuler);
                    }
                    else
                    {
                        initialLocalRot = transform.localRotation;
                        initialWorldRot = transform.rotation;
                    }
                }
            }
            else
            {
                // 뒤로 덜컹! 벌러덩 넘어가는 전복 물리 애니메이션
                if (knockdownProgress < 1f)
                {
                    knockdownProgress += Time.deltaTime / Mathf.Max(0.01f, knockdownDuration);
                    if (knockdownProgress > 1f) knockdownProgress = 1f;
                }

                float smoothT = Mathf.SmoothStep(0f, 1f, knockdownProgress);

                // 바닥 충돌 바운스 반동 (Damped Sine Bounce)
                bounceDecay = Mathf.MoveTowards(bounceDecay, 0f, Time.deltaTime * 2.8f);
                float bounceAngle = Mathf.Sin(knockdownProgress * Mathf.PI * 4f) * (groundImpactBounce * 16f) * bounceDecay;
                float bounceY = Mathf.Abs(Mathf.Sin(knockdownProgress * Mathf.PI * 3f)) * (groundImpactBounce * 0.35f) * bounceDecay;

                // 회전: 하늘 보기(knockdownPitch) + 약간의 롤 기울기(knockdownRoll)
                float currentPitch = Mathf.Lerp(0f, knockdownPitch, smoothT) + bounceAngle;
                float currentRoll = Mathf.Lerp(0f, knockdownRoll, smoothT);
                Quaternion tumbleRot = Quaternion.Euler(currentPitch, 0f, currentRoll);

                // 위치: 바닥으로 주저앉음
                float currentDrop = Mathf.Lerp(0f, groundDropHeight, smoothT) + bounceY;
                Vector3 tumblePos = new Vector3(0f, currentDrop, 0f);

                // 바닥에 넘어진 후의 미세 여진
                float afterNoise = (Mathf.PerlinNoise(time * 20f, 0f) - 0.5f) * 0.012f * bounceDecay;

                if (isCameraDetached)
                {
                    transform.position = detachedWorldPos + tumblePos + new Vector3(0f, afterNoise, 0f);
                }
                else
                {
                    Vector3 baseTumblePos = (transform.parent != null) ? initialLocalPos : initialWorldPos;
                    if (transform.parent != null)
                    {
                        transform.localPosition = baseTumblePos + tumblePos + new Vector3(0f, afterNoise, 0f);
                    }
                    else
                    {
                        transform.position = baseTumblePos + tumblePos + new Vector3(0f, afterNoise, 0f);
                    }
                }
                transform.rotation = knockdownStartRot * tumbleRot;
            }
        }

        /// <summary>
        /// 5개 제어점을 끊김 없이 물 흐르듯 통과하는 3차 캣멀-롬(Catmull-Rom) 연속 스플라인 보간
        /// (스팟에서 멈칫하거나 꺾이지 않고 처음부터 끝까지 완전 부드러운 단일 곡선 주행)
        /// </summary>
        private Vector3 EvaluateCatmullRomSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, float t)
        {
            t = Mathf.Clamp01(t);
            float scaledT = t * 4f;
            int segment = Mathf.Min(Mathf.FloorToInt(scaledT), 3);
            float u = scaledT - segment;

            Vector3 cp0, cp1, cp2, cp3;
            if (segment == 0)
            {
                cp0 = p0 + (p0 - p1); // 가상 선행 제어점
                cp1 = p0;
                cp2 = p1;
                cp3 = p2;
            }
            else if (segment == 1)
            {
                cp0 = p0;
                cp1 = p1;
                cp2 = p2;
                cp3 = p3;
            }
            else if (segment == 2)
            {
                cp0 = p1;
                cp1 = p2;
                cp2 = p3;
                cp3 = p4;
            }
            else
            {
                cp0 = p2;
                cp1 = p3;
                cp2 = p4;
                cp3 = p4 + (p4 - p3); // 가상 후행 제어점
            }

            // Catmull-Rom Spline Formula
            return 0.5f * (
                (2f * cp1) +
                (-cp0 + cp2) * u +
                (2f * cp0 - 5f * cp1 + 4f * cp2 - cp3) * (u * u) +
                (-cp0 + 3f * cp1 - 3f * cp2 + cp3) * (u * u * u)
            );
        }

        /// <summary>
        /// 0~1 진행도에 맞춰 CM_Shot01의 로컬 위치와 회전을 사진 1~5번 궤적으로 멈칫거림 없이 부드럽게 주욱 훑기
        /// (중간 5개 지점에서 감속하거나 멈칫거리지 않고 처음부터 끝까지 완전한 단일 연속 곡선으로 부드럽게 통과)
        /// </summary>
        public void ApplyOrbitSweepPose(float t)
        {
            // 전체 훑기의 시작(0)과 끝(1)에서만 부드러운 가감속 적용 (중간 경유점에서는 속도 0 감속 완전 차단)
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            // 1. 위치: 5개 제어점을 멈칫거림 없이 통과하는 3차 캣멀-롬 연속 곡선
            Vector3 currentLocalPos = EvaluateCatmullRomSpline(
                verticalSweepStartPos,
                verticalSweepMidPos1,
                verticalSweepMidPos2,
                verticalSweepMidPos3,
                verticalSweepEndPos,
                smoothT
            );

            // 2. 회전: 오일러 각도 역시 3차 캣멀-롬 연속 곡선으로 보간하여 노란색 회전 링을 일정하게 돌리듯 매끄럽게 훑음
            Vector3 currentEuler = EvaluateCatmullRomSpline(
                verticalSweepStartRot,
                verticalSweepMidRot1,
                verticalSweepMidRot2,
                verticalSweepMidRot3,
                verticalSweepEndRot,
                smoothT
            );

            transform.localPosition = currentLocalPos;
            transform.localRotation = Quaternion.Euler(currentEuler);
        }

        public void ApplyOrbitSweepDefaultWaypoints()
        {
            verticalSweepStartPos = new Vector3(28.0201f, 2.75505f, 104.834f);
            verticalSweepStartRot = new Vector3(0.495f, 185.033f, -5.21f);

            verticalSweepMidPos1 = new Vector3(43.435f, 4.55768f, 89.8453f);
            verticalSweepMidRot1 = new Vector3(2.028f, 212.368f, -4.83f);

            verticalSweepMidPos2 = new Vector3(60.1264f, 6.59522f, 55.9025f);
            verticalSweepMidRot2 = new Vector3(4.100f, 241.117f, -3.29f);

            verticalSweepMidPos3 = new Vector3(51.3738f, 6.82395f, 1.78538f);
            verticalSweepMidRot3 = new Vector3(5.223f, 283.428f, 0.31f);

            verticalSweepEndPos = new Vector3(19.4323f, 4.19730f, -28.158f);
            verticalSweepEndRot = new Vector3(4.188f, 316.957f, 3.17f);

            enableVerticalAdSweepCam = true;
            enableOrbitSweepCam = true;
            enableBoosterSweepCam = true;
        }

        /// <summary>
        /// 차량 통과 직후 멈칫거림 없이 거꾸로 뒤집힌 채(153도 -> 177도) 멀어지는 후면을 감상하고,
        /// 밤하늘(265도)에 도달하면서 점진적으로 감속(Ease-out)하여 웅장하게 멈추는 PCHIP 연속 곡선
        /// </summary>
        public float EvaluatePostPassPitch(float t)
        {
            t = Mathf.Clamp01(t);

            float[] x = new float[] { 0.00f, 0.28f, 0.55f, 1.00f };
            float[] y = new float[] {
                Mathf.Max(75f, pitchAnglePassUnder),
                pitchAngleInvertedRear,
                pitchAngleInvertedHorizon,
                pitchAngleSkyEnding
            };

            return EvaluatePchipSpline(x, y, t, true);
        }

        /// <summary>
        /// 6개 제어 각도를 중간 멈칫거림 없이 100% 매끄럽게 통과하고 밤하늘에서 부드럽게 감속하는 단일 연속 PCHIP 곡선
        /// (0.00: 2.17° -> 0.18: 16.48° -> 0.38: 72.69° -> 0.60: 153.13° -> 0.80: 177.51° -> 1.00: 265.0°)
        /// </summary>
        public float EvaluatePitchFlipAngle(float t)
        {
            t = Mathf.Clamp01(t);

            float[] x = new float[] { 0.00f, 0.18f, 0.38f, 0.60f, 0.80f, 1.00f };
            float[] y = new float[] {
                pitchAngleApproach,
                pitchAngleNearPass,
                pitchAnglePassUnder,
                pitchAngleInvertedRear,
                pitchAngleInvertedHorizon,
                pitchAngleSkyEnding
            };

            return EvaluatePchipSpline(x, y, t, true);
        }

        /// <summary>
        /// 구간마다 멈칫거리지 않고, 중간 점들의 연속적인 속도를 보존하며 끝점에서만 자연스럽게 감속(Ease-out)하는 단일 3차 PCHIP 연속 스플라인
        /// </summary>
        private float EvaluatePchipSpline(float[] x, float[] y, float t, bool zeroEndTangent)
        {
            int n = x.Length;
            if (n == 0) return 0f;
            if (n == 1) return y[0];
            if (t <= x[0]) return y[0];
            if (t >= x[n - 1]) return y[n - 1];

            int i = 0;
            while (i < n - 2 && t > x[i + 1])
            {
                i++;
            }

            float h0 = x[i + 1] - x[i];
            float delta0 = (y[i + 1] - y[i]) / h0;

            float d_i;
            if (i == 0)
            {
                d_i = delta0;
            }
            else
            {
                float hPrev = x[i] - x[i - 1];
                float deltaPrev = (y[i] - y[i - 1]) / hPrev;
                d_i = (deltaPrev * delta0 > 0f) ? (2f * deltaPrev * delta0) / (deltaPrev + delta0) : 0f;
            }

            float d_next;
            if (i + 1 == n - 1)
            {
                d_next = zeroEndTangent ? 0f : delta0;
            }
            else
            {
                float hNext = x[i + 2] - x[i + 1];
                float deltaNext = (y[i + 2] - y[i + 1]) / hNext;
                d_next = (delta0 * deltaNext > 0f) ? (2f * delta0 * deltaNext) / (delta0 + deltaNext) : 0f;
            }

            float u = Mathf.Clamp01((t - x[i]) / h0);
            float u2 = u * u;
            float u3 = u2 * u;

            float h00 = 2f * u3 - 3f * u2 + 1f;
            float h10 = u3 - 2f * u2 + u;
            float h01 = -2f * u3 + 3f * u2;
            float h11 = u3 - u2;

            return h00 * y[i] + h10 * h0 * d_i + h01 * y[i + 1] + h11 * h0 * d_next;
        }

        public void ApplyPitchFlipPose(float progress)
        {
            float pitch = EvaluatePitchFlipAngle(progress);
            currentPitchFlipAngle = pitch;

            if (lockPitchFlipPosition)
            {
                if (transform.parent != null)
                {
                    transform.localPosition = pitchFlipFixedPos;
                }
                else
                {
                    transform.position = pitchFlipFixedPos;
                }
            }

            transform.localRotation = Quaternion.Euler(pitch, pitchFlipFixedYaw, pitchFlipFixedRoll);
        }

        [ContextMenu("🎬 360° 수직 텀블링 연출 켜기 및 사진 1~5번 기본값 적용")]
        public void ApplyPitchFlipDefaultValues()
        {
            pitchAngleApproach = 2.174f;
            approachFramingOffset = 0.0f;
            pitchAngleNearPass = 16.484f;
            pitchAnglePassUnder = 90.0f;
            pitchAngleInvertedRear = 153.128f;
            pitchAngleInvertedHorizon = 177.507f;
            pitchAngleSkyEnding = 265.0f;

            pitchFlipFixedPos = new Vector3(-1314.5f, 1.97743f, -0.5998f);
            pitchFlipFixedYaw = -90.0f;
            pitchFlipFixedRoll = 0.0f;

            invertedRearLingerDuration = 3.0f;
            skyTiltUpDuration = 5.5f;
            pitchTrackingResponsiveness = 25f;

            enablePitchFlipPass = true;
            enableKnockdown = false;
            enableOrbitSweepCam = false;
            enableVerticalAdSweepCam = false;
            enableBoosterSweepCam = false;
        }

        private void OnValidate()
        {
            if (previewPitchFlip && !Application.isPlaying)
            {
                ApplyPitchFlipPose(previewPitchFlipProgress);
            }
            else if (previewOrbitSweep && !Application.isPlaying)
            {
                ApplyOrbitSweepPose(previewOrbitProgress);
            }
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
                    if (restartKey == KeyCode.Return && kb.enterKey.wasPressedThisFrame) return true;
                    if (restartKey == KeyCode.R && kb.rKey.wasPressedThisFrame) return true;
                    if (restartKey == KeyCode.T && kb.tKey.wasPressedThisFrame) return true;
                    if (restartKey == KeyCode.Alpha1 && kb.digit1Key.wasPressedThisFrame) return true;
                    if (restartKey == KeyCode.Alpha2 && kb.digit2Key.wasPressedThisFrame) return true;
                    if (restartKey == KeyCode.Escape && kb.escapeKey.wasPressedThisFrame) return true;
                }
            }
            catch { }
#endif
            return false;
        }

        private void Reset()
        {
            ApplyOnboardWindowCamPreset();
        }

        /// <summary>
        /// 카메라 전복 & 밤하늘 3초 응시 시네마틱 프리셋 적용 (요청 모드)
        /// </summary>
        [ContextMenu("🎬 카메라 전복 & 밤하늘 3초 응시 프리셋 적용")]
        public void ApplyKnockdownSky3sPreset()
        {
            enableKnockdown = true;
            startCarsWithFullBoosters = true;
            quietDuration = 0.5f;
            rumbleBuildUpDuration = 0.5f;
            passByViewDuration = 0.4f;
            autoDetectPassByPosition = true;
            skyHoldDuration = 3.0f;
            splineDriveDuration = 8.0f;
            maxRumbleShake = 0.025f;
            maxRumbleAngleShake = 0.6f;
            passByTurbulenceMultiplier = 1.4f;
            shakeFrequency = 20f;
            knockdownDuration = 0.32f;
            knockdownPitch = -82f;
            knockdownRoll = 14f;
            groundImpactBounce = 0.22f;
            groundDropHeight = -0.35f;
        }

        /// <summary>
        /// 차량 창문 온보드 액션캠 프리셋 (속도감 & 노면/엔진 고주파 떨림)
        /// </summary>
        [ContextMenu("🏎️ 차량 창문 온보드 액션캠 프리셋 적용")]
        public void ApplyOnboardWindowCamPreset()
        {
            enableKnockdown = false;
            startCarsWithFullBoosters = true;
            quietDuration = 0.0f;
            rumbleBuildUpDuration = 0.2f;
            passByViewDuration = 999f;
            autoDetectPassByPosition = false;
            splineDriveDuration = 8.0f;
            maxRumbleShake = 0.012f;
            maxRumbleAngleShake = 0.45f;
            passByTurbulenceMultiplier = 1.2f;
            shakeFrequency = 28f;
            continuousOnboardShake = true;
        }

        /// <summary>
        /// booston_yujeong_1 씬 전용: 스플라인 속도 주행 ➔ 1초 대기 ➔ 부스터 변형 ➔ 카메라 두고 두 차량 부와앙- 발진 연출 프리셋 (요청 모드)
        /// </summary>
        [ContextMenu("🎬 booston_yujeong_1 1초 변형 후 카메라 두고 부스터 발진 프리셋 적용")]
        public void ApplyBoosterTransformationShotPreset()
        {
            enableKnockdown = false;
            startCarsWithFullBoosters = false;
            triggerBoosterOnLaunch = true;
            boosterTransformDelay = 1.0f;
            leaveCameraBehindOnBoost = true;
            transformationDuration = 3.2f;
            cruiseSpeed = 150f;
            boostRocketSpeed = 500f;
            boostAccelerationDuration = 0.35f;
            continuousOnboardShake = true;
            quietDuration = 0f;
            rumbleBuildUpDuration = 0.1f;
            maxRumbleShake = 0.008f;
            maxRumbleAngleShake = 0.8f;
            passByTurbulenceMultiplier = 1.8f;
            passByCalmDownDuration = 1.0f;
            ResetBoosterLaunchState();
        }

        /// <summary>
        /// 차량 접근 거리 비례 아스팔트 지면 진동 연출 프리셋 (요청 연출)
        /// 멀리 있을 때 완전 고요 ➔ 가까워질수록 아스팔트 진동 점진 고조 ➔ 스쳐갈 때 최고조 ➔ 통과 전복
        /// </summary>
        [ContextMenu("🛣️ 차량 거리 비례 아스팔트 지면 진동 프리셋 적용 (요청 모드)")]
        public void ApplyDistanceBasedAsphaltRumblePreset()
        {
            useDistanceBasedRumble = true;
            rumbleStartDistance = 150f;
            rumbleCurvePower = 2.2f;
            verticalRumbleMultiplier = 1.4f;
            maxRumbleShake = 0.04f;
            maxRumbleAngleShake = 1.5f;
            passByTurbulenceMultiplier = 1.8f;
            shakeFrequency = 36f;
            continuousOnboardShake = false;
            Debug.Log("[CameraWindBlastShock] 🛣️ 차량 거리 비례 아스팔트 지면 진동 프리셋 적용 완료! (멀리 있을 땐 조용함 -> 가까워질수록 아스팔트 진동 점점 심해짐)");
        }

        /// <summary>
        /// 차량 통과 시 카메라가 제자리에서 부드럽게 고개를 돌리며 추적하는 로드사이드 팬 트래킹 프리셋 (요청 모드)
        /// </summary>
        [ContextMenu("🎥 차량 통과 추적 패닝 프리셋 (Pan-Tracking Cam)")]
        public void ApplyPanTrackingPreset()
        {
            enableVehicleTracking = true;
            trackingSmoothness = 15f;
            closePassSpeedBoost = 2.5f;
            trackingAimOffset = new Vector3(0f, 0.4f, 0f);
            allowPitchTracking = true;
            maxDownwardPitch = 88f;
            maxUpwardPitch = -25f;
            enableKnockdown = false;
            maxRumbleShake = 0f;
            maxRumbleAngleShake = 0f;
            AlignCameraToVehicleStart();
            Debug.Log("[CameraWindBlastShock] 🎥 차량 통과 추적 패닝 프리셋 적용 완료! (지나가는 두 차량을 따라 카메라가 부드럽게 회전 추적합니다)");
        }

        /// <summary>
        /// 에디터 편집 중 현재 카메라 각도를 차량 시작 위치로 즉시 조준 (Image 1 구도 미리보기)
        /// </summary>
        [ContextMenu("🎯 차량 시작 위치로 카메라 각도 즉시 조준 (LookAt Preview)")]
        public void AlignCameraToVehicleStart()
        {
            AutoFindComponents();
            Vector3 targetPos = GetTrackingTargetWorldPosition();
            if (targetPos != Vector3.zero)
            {
                Vector3 toTarget = targetPos - transform.position;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                    if (!allowPitchTracking)
                    {
                        Vector3 euler = lookRot.eulerAngles;
                        euler.x = transform.eulerAngles.x;
                        euler.z = 0f;
                        lookRot = Quaternion.Euler(euler);
                    }
                    transform.rotation = lookRot;
                    currentTrackingRot = lookRot;
                    initialWorldRot = lookRot;
                    initialLocalRot = transform.localRotation;
                    Debug.Log("[CameraWindBlastShock] 🎯 카메라 각도를 차량 시작 위치로 조준 완료했습니다.");
                }
            }
        }

        /// <summary>
        /// 도로 정적 ➔ 진동 고조 ➔ 질주 추월 ➔ 멀어지며 고요해짐 연출 프리셋 (요청 모드)
        /// </summary>
        [ContextMenu("🎬 도로 정적 ➔ 진동 고조 ➔ 멀어지며 조용해짐 프리셋 적용")]
        public void ApplyRoadPassByFadePreset()
        {
            enableKnockdown = false;
            continuousOnboardShake = false;
            startCarsWithFullBoosters = true;
            quietDuration = 1.2f;
            rumbleBuildUpDuration = 1.8f;
            passByViewDuration = 0.4f;
            passByCalmDownDuration = 1.0f;
            autoDetectPassByPosition = false;
            splineDriveDuration = 8.0f;
            maxRumbleShake = 0.035f;
            maxRumbleAngleShake = 1.6f;
            passByTurbulenceMultiplier = 1.8f;
            shakeFrequency = 24f;
        }

        /// <summary>
        /// 8초 도로 질주 연출 프리셋 (카메라 넘어짐 없음, 풀 부스터 질주)
        /// </summary>
        [ContextMenu("🎬 8초 도로 질주 프리셋 적용 (넘어짐 제거)")]
        public void Apply8SecondDrivePreset()
        {
            enableKnockdown = false;
            startCarsWithFullBoosters = true;
            quietDuration = 0.5f;
            rumbleBuildUpDuration = 0.5f;
            passByViewDuration = 7.0f;
            autoDetectPassByPosition = false;
            splineDriveDuration = 8.0f;
            maxRumbleShake = 0.025f;
            maxRumbleAngleShake = 0.6f;
            passByTurbulenceMultiplier = 1.4f;
            shakeFrequency = 20f;
        }

        /// <summary>
        /// 6초 완성 시네마틱 연출 프리셋 일괄 적용
        /// </summary>
        [ContextMenu("🎬 6초 시네마틱 프리셋 적용")]
        public void Apply6SecondPreset()
        {
            enableKnockdown = true;
            startCarsWithFullBoosters = true;
            quietDuration = 1.0f;
            rumbleBuildUpDuration = 1.5f;
            passByViewDuration = 0.4f;
            skyHoldDuration = 3.0f;
            splineDriveDuration = 5.0f;
            maxRumbleShake = 0.025f;
            maxRumbleAngleShake = 0.6f;
            passByTurbulenceMultiplier = 1.4f;
            shakeFrequency = 20f;
            knockdownDuration = 0.35f;
            knockdownPitch = -82f;
            knockdownRoll = 14f;
            groundImpactBounce = 0.22f;
            groundDropHeight = -0.35f;
        }

        /// <summary>
        /// 블루카 및 레드카의 부스터 변형 및 모든 이펙트를 대기 상태부터 100% 완료 상태로 즉시 설정
        /// </summary>
        [ContextMenu("🚀 두 차량 부스터 풀 전개 & 점등 즉시 적용")]
        public void ApplyVehiclesFullBoosted()
        {
            // 블루카 풀 변형 & 부스터 VFX 점등
            var blueCtrl = FindFirstObjectByType<VehicleTransformationController>();
            if (blueCtrl != null)
            {
                blueCtrl.SnapToFullTransformation();
            }

            // 레드카 풀 변형 & 부스터 VFX 점등
            var redCtrl = FindFirstObjectByType<BoosterDeploymentController>();
            if (redCtrl != null)
            {
                redCtrl.SnapToFullDeployment();
            }
        }

        /// <summary>
        /// 시네마틱 시퀀스를 처음부터 재실행
        /// </summary>
        [ContextMenu("🎬 시네마틱 시퀀스 처음부터 실행")]
        public void StartCinematicSequence()
        {
            if (isKnockedDown || isCameraDetached)
            {
                RestoreInitialPose();
            }
            else
            {
                StoreInitialPose();
            }
            AutoFindComponents();

            if (splineAnimate != null && Application.isPlaying)
            {
                splineAnimate.NormalizedTime = 0f;
                splineAnimate.Pause();
            }

            if (startCarsWithFullBoosters)
            {
                ApplyVehiclesFullBoosted();
            }
            else
            {
                ApplyVehiclesNormalState();
            }

            sequenceTimer = 0f;
            isSequenceRunning = true;
            splineLaunched = false;
            isKnockedDown = false;
            isPitchFlipReceding = false;
            pitchFlipRecedeTimer = 0f;
            currentCameraAheadDistance = 9999f;
        }

        /// <summary>
        /// 에디터 또는 런타임에서 즉시 풍압 전복 효과만 테스트
        /// </summary>
        [ContextMenu("💥 테스트: 풍압 전복 실행")]
        public void TriggerKnockdownTest()
        {
            StoreInitialPose();
            currentPhase = CinematicPhase.KnockedDown;
            isKnockedDown = true;
            knockdownProgress = 0f;
            bounceDecay = 1f;
        }

        /// <summary>
        /// 카메라를 원래 정면 상태로 리셋
        /// </summary>
        [ContextMenu("🔄 카메라 원래대로 리셋")]
        public void ResetCameraTest()
        {
            RestoreInitialPose();
        }
    }
}
