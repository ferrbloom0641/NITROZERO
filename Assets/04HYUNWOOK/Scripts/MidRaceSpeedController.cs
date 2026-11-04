using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MidRaceSpeedController : MonoBehaviour
{
    static readonly List<MidRaceSpeedController> ActiveVehicles =
        new List<MidRaceSpeedController>();

    [Header("Camera 6~8 Mid-race Sequence")]
    [SerializeField] private CutsceneManager cutsceneManager;
    [SerializeField] private CutsceneCameraType firstSequenceCamera = CutsceneCameraType.Camera6;
    [SerializeField] private CutsceneCameraType lastSequenceCamera = CutsceneCameraType.Camera8;
    [Tooltip("CutsceneManager가 없는 독립 레이싱 씬에서도 주행합니다.")]
    [SerializeField] private bool runWithoutCutsceneManager;

    [Header("Speed (km/h)")]
    [Min(0f)] public float initialSpeedKmh = 180f;
    [Min(1f)] public float targetSpeedKmh = 300f;
    [Min(0f)] public float accelerationKmh = 40f;

    [Header("Temporary acceleration burst")]
    [SerializeField] private bool useAccelerationBurst;
    [Min(0f)][SerializeField] private float burstCruiseSpeedKmh = 300f;
    [Min(0f)][SerializeField] private float burstPeakSpeedKmh = 340f;
    [Min(0f)][SerializeField] private float burstStartSeconds = 0.45f;
    [Min(0.01f)][SerializeField] private float burstRampUpSeconds = 0.65f;
    [Min(0f)][SerializeField] private float burstHoldSeconds = 1.55f;
    [Min(0.01f)][SerializeField] private float burstRampDownSeconds = 0.7f;
    [Tooltip("가속 시 정재생되고 감속 시 역재생되는 파란 차량 변신 컨트롤러입니다.")]
    [SerializeField] private YUJEONG.VehicleTransformationController accelerationTransformation;

    [Header("Boost camera FOV")]
    [SerializeField] private bool animateCameraFovDuringBurst;
    [SerializeField] private CinemachineCamera burstCamera;
    [Range(1f, 179f)][SerializeField] private float burstBaseFov = 60f;
    [Range(1f, 179f)][SerializeField] private float burstPeakFov = 80f;
    [Min(0.01f)][SerializeField] private float burstFovRiseSeconds = 1f;
    [Min(0.01f)][SerializeField] private float burstFovFallSeconds = 1f;

    [Header("Sequence timing")]
    [Tooltip("해당 카메라가 시작된 뒤 차량이 출발하기까지 기다리는 시간입니다.")]
    [Min(0f)][SerializeField] private float sequenceStartDelaySeconds;
    [Tooltip("0보다 크면 이 시간 동안 진행률이 0%에서 100%까지 증가합니다.")]
    [Min(0f)][SerializeField] private float sequenceDurationSeconds;
    [Tooltip("100%에 도달하면 차량 이동을 종료합니다.")]
    [SerializeField] private bool stopAtSequenceEnd = true;
    [Tooltip("시퀀스가 끝나면 에디터 Play Mode 또는 실행 중인 빌드를 종료합니다.")]
    [SerializeField] private bool stopApplicationAtSequenceEnd;

    [Header("Movement")]
    public MoveAxis moveAxis = MoveAxis.X_Right;
    public CoordinateSpace space = CoordinateSpace.World;
    public Vector3 customDirection = Vector3.right;

    [Header("Straight lane choreography")]
    [SerializeField] private bool enableSingleLaneMove;
    [Tooltip("도로 진행 방향에 수직인 차선 이동 폭입니다. 부호로 이동 방향을 바꿉니다.")]
    [SerializeField] private float laneMoveAmplitudeZ = 2f;
    [Tooltip("켜면 좌우를 한 번씩 오가고, 끄면 옆 차량에 한 번 접근했다가 복귀합니다.")]
    [SerializeField] private bool weaveBothSides;
    [Tooltip("차선 연출의 시작, 진입, 유지, 복귀 시간을 초 단위로 지정합니다.")]
    [SerializeField] private bool useTimedLaneMove;
    [Min(0f)][SerializeField] private float laneMoveStartSeconds;
    [Min(0.01f)][SerializeField] private float laneMoveInSeconds = 0.7f;
    [Tooltip("차선 접근을 먼저 끝낸 뒤 별도 몸싸움 이동을 시작합니다.")]
    [SerializeField] private bool useLaneApproachPhase;
    [Tooltip("전체 차선 이동량 중 접근 단계에서 사용할 비율입니다.")]
    [Range(0f, 1f)][SerializeField] private float laneApproachRatio = 0.25f;
    [Tooltip("접근 완료 후 첫 몸싸움 밀기 동작에 걸리는 시간입니다.")]
    [Min(0.01f)][SerializeField] private float laneFightInSeconds = 0.65f;
    [Min(0f)][SerializeField] private float laneHoldSeconds = 0.2f;
    [Min(0.01f)][SerializeField] private float laneMoveOutSeconds = 0.7f;
    [Tooltip("차선 진입 후 원래 차선으로 돌아오지 않습니다.")]
    [SerializeField] private bool keepLaneOffsetAtEnd;
    [Tooltip("첫 번째 차선 밀기 후 반대 방향으로 되미는 몸싸움 동작을 사용합니다.")]
    [SerializeField] private bool useLanePushCounter;
    [Tooltip("되밀기 완료 후 첫 이동량 대비 최종 위치입니다. 음수면 시작 위치 반대편까지 이동합니다.")]
    [Range(-1f, 1f)][SerializeField] private float laneCounterFinalRatio;
    [Tooltip("새 충돌체를 만들지 않고 기존 부모 BoxCollider 폭으로 이 차량과의 관통만 방지합니다.")]
    [SerializeField] private MidRaceSpeedController laneSeparationTarget;
    [Min(0f)][SerializeField] private float laneSeparationPadding = 0.2f;
    [Range(0f, 12f)][SerializeField] private float maximumLaneYaw = 6f;
    [Min(0.01f)][SerializeField] private float laneYawResponseTime = 0.22f;

    [Header("Timed longitudinal offset")]
    [Tooltip("씬의 시작 좌표는 유지하고 실행 중 지정 시간에만 진행 방향 좌표를 이동합니다.")]
    [SerializeField] private bool useTimedLongitudinalOffset;
    [Min(0f)][SerializeField] private float longitudinalOffsetStartSeconds;
    [Min(0.01f)][SerializeField] private float longitudinalOffsetDurationSeconds = 0.6f;
    [Tooltip("음수는 진행 방향의 뒤쪽, 양수는 앞쪽입니다.")]
    [SerializeField] private float longitudinalOffsetDistance;
    [Tooltip("현재 시작 좌표에서 대상 차량 뒤의 지정 간격까지 필요한 이동량을 자동 계산합니다.")]
    [SerializeField] private bool calculateLongitudinalOffsetFromTarget;
    [SerializeField] private MidRaceSpeedController longitudinalOffsetTarget;
    [Min(0f)][SerializeField] private float longitudinalTargetGap = 4.5f;
    [Min(0f)][SerializeField] private float maximumAutomaticLongitudinalOffset = 30f;

    [Header("Straight-line pressure")]
    [Tooltip("직선 주행 중 대상 차량을 기준으로 지정한 종방향 위치를 유지합니다.")]
    [SerializeField] private MidRaceSpeedController pressureTarget;
    [Min(0f)][SerializeField] private float pressureFollowingDistance = 5f;
    [Tooltip("대상 차량보다 앞쪽에서 압박할 거리입니다. 뒤에서 따라갈 때는 0으로 둡니다.")]
    [Min(0f)][SerializeField] private float pressureForwardOffset;

    [Header("Vehicle contact")]
    [Tooltip("부모 차량 충돌 박스를 이용해 차량끼리 서로 통과하지 못하게 합니다.")]
    [SerializeField] private bool preventVehicleOverlap = true;
    [Tooltip("접촉 중에도 뒤로 위치를 보정하지 않고 측면 추월을 계속합니다.")]
    [InspectorName("접촉 중 전방 추월 허용")]
    [SerializeField] private bool allowForwardOvertake;
    [Tooltip("추월 동선을 유지하면서 한 번 밀어낼 대상 차량입니다.")]
    [SerializeField] private MidRaceSpeedController overtakeImpactTarget;
    [SerializeField] private BoxCollider vehicleCollisionBox;
    [Min(0f)][SerializeField] private float vehicleContactPadding = 0.18f;
    [Range(0f, 8f)][SerializeField] private float contactYawKick = 2.5f;
    [Min(0.01f)][SerializeField] private float contactYawRecoveryTime = 0.2f;
    [Min(0f)][SerializeField] private float contactForwardPushDistance = 0.35f;
    [Tooltip("전방 충격 거리를 한 프레임에 적용하지 않고 이 시간 동안 나누어 적용합니다.")]
    [Min(0.05f)][SerializeField] private float contactForwardPushDuration = 0.24f;
    [Min(0f)][SerializeField] private float contactSidePushDistance = 0.22f;
    [Min(0.01f)][SerializeField] private float contactSideRecoverySpeed = 1.4f;

    [Header("Curved road path (optional)")]
    [Tooltip("Road Point 00~44 중심선을 따라 차량 위치와 회전을 갱신합니다.")]
    [SerializeField] private bool followRoadPointPath;
    [Tooltip("차량을 이 도로의 MeshCollider 위에 유지합니다.")]
    [SerializeField] private Collider roadSurfaceCollider;
    [SerializeField] private string roadSurfaceObjectName = "Racing Asphalt Road Preview";
    [SerializeField] private string roadGuideObjectName = "Racing Road Centerline Guide";
    [Tooltip("현재 HW_Racing 차량 배치는 Road Point의 역순으로 커브에 진입합니다.")]
    [SerializeField] private bool reverseRoadPointOrder = true;
    [Tooltip("앞쪽 경로를 미리 읽어 커브 회전을 부드럽게 만드는 거리입니다.")]
    [Min(0.1f)][SerializeField] private float pathLookAheadDistance = 30f;
    [Tooltip("현재 위치에서 지정한 Road Point까지 정확히 시퀀스 시간 안에 도착하도록 실제 속도를 계산합니다.")]
    [SerializeField] private bool calculateSpeedToRoadPoint;
    [Min(0)][SerializeField] private int finishRoadPointIndex = 14;

    [Header("Visual steering")]
    [SerializeField] private TrailerCruiseMotion cruiseMotion;
    [SerializeField] private bool applyVisualSteering = true;
    [Range(0f, 2f)][SerializeField] private float visualSteeringStrength = 1.25f;
    [Range(0f, 45f)][SerializeField] private float maxVisualWheelAngle = 22f;

    [Header("Cornering body dynamics")]
    [Tooltip("목표 코너 방향까지 차체가 따라가는 시간입니다.")]
    [Min(0.01f)][SerializeField] private float steeringResponseTime = 0.28f;
    [Tooltip("차체가 한 초 동안 회전할 수 있는 최대 각도입니다.")]
    [Min(1f)][SerializeField] private float maximumYawRate = 45f;
    [Tooltip("코너 바깥쪽으로 기울어지는 차체 최대 각도입니다.")]
    [Range(0f, 8f)][SerializeField] private float maximumBodyRoll = 2.2f;
    [Min(0.01f)][SerializeField] private float bodyRollResponseTime = 0.32f;

    [Header("Drift")]
    [SerializeField] private bool enableDrift;
    [Tooltip("코너 진행 방향을 기준으로 차체 Y축이 벌어지는 최대 드리프트 각도입니다.")]
    [Range(0f, 45f)][SerializeField] private float maximumDriftAngle = 35f;
    [Min(0.01f)][SerializeField] private float driftResponseTime = 0.16f;
    [Range(0f, 1f)][SerializeField] private float counterSteerStrength = 0.55f;

    [Header("HUD speed variation")]
    [Tooltip("HUD에 표시되는 속도를 실제 속도 대신 지정한 범위 안에서 계속 왕복시킵니다.")]
    [SerializeField] private bool useHudSpeedVariation;
    [Tooltip("왕복의 최고점(km/h)입니다. 예: 300")]
    [SerializeField] private float hudSpeedPeakKmh = 299f;
    [Tooltip("'깊게' 내려갈 때 도달하는 최저점(km/h)입니다. 예: 298")]
    [SerializeField] private float hudSpeedDeepLowKmh = 299f;
    [Tooltip("'얕게' 내려갈 때 도달하는 최저점(km/h)입니다. 예: 299")]
    [SerializeField] private float hudSpeedShallowLowKmh = 0.2f;
    [Tooltip("한 번 왕복할 때마다 깊은 쪽(Deep Low)까지 내려갈 확률입니다. 0.5면 298과 299가 절반씩 무작위로 나옵니다.")]
    [Range(0f, 1f)][SerializeField] private float hudSpeedDeepLowChance = 0.15f;
    [Tooltip("최고점에서 최저점을 거쳐 다시 최고점으로 돌아오는 왕복 한 번에 걸리는 시간(초)입니다. 1 = 1초 기준이며, 값이 클수록 더 느리게 왔다갔다 합니다.")]
    [Min(0.01f)][SerializeField] private float hudSpeedCycleSeconds = 0.1f;
    [Tooltip("한 번의 왕복이 끝난 뒤, 다음 왕복을 시작하기 전까지 최고점에 가만히 머무르는 쿨타임(초)입니다. 1 = 1초 기준입니다.")]
    [Min(0f)][SerializeField] private float hudSpeedCooldownSeconds = 1.5f;

    float hudSpeedCycleTimer;
    float hudSpeedCurrentLowKmh;
    bool hudSpeedLowChosen;
    bool hudSpeedInCooldown;

    public float CurrentSpeedKmh { get; private set; }
    public float DisplaySpeedKmh => useHudSpeedVariation
        ? (hudSpeedInCooldown
            ? hudSpeedPeakKmh
            : Mathf.Max(0f, hudSpeedPeakKmh - (hudSpeedPeakKmh - hudSpeedCurrentLowKmh) * 0.5f *
                (1f - Mathf.Cos(hudSpeedCycleTimer / Mathf.Max(0.01f, hudSpeedCycleSeconds) * Mathf.PI * 2f))))
        : CurrentSpeedKmh;
    public int CurrentGear { get; private set; } = 1;
    public float SequenceProgress => sequenceDurationSeconds > 0f
        ? Mathf.Clamp01(sequenceElapsed / sequenceDurationSeconds)
        : 0f;
    public bool ProvidesSequenceProgress => sequenceDurationSeconds > 0f && IsSequenceActive;
    public bool ProvidesRoadRankingPosition => followRoadPointPath && pathReady;
    public float RoadRankingPosition => reverseRoadPointOrder
        ? pathLength - pathDistance
        : pathDistance;
    public bool IsSequenceActive => cutsceneManager != null
        ? cutsceneManager.activeCamera >= firstSequenceCamera &&
          cutsceneManager.activeCamera <= lastSequenceCamera
        : runWithoutCutsceneManager;

    bool sequenceStarted;
    float sequenceActivationElapsed;
    bool stopRequested;
    float sequenceElapsed;
    readonly List<Transform> roadPoints = new List<Transform>();
    float[] cumulativeDistances;
    float pathDistance;
    float pathLength;
    float lateralOffset;
    float verticalOffset;
    bool pathReady;
    float roadSurfaceClearance;
    float yawVelocity;
    float bodyRoll;
    float bodyRollVelocity;
    float driftAngle;
    float driftVelocity;
    float calculatedPathSpeedKmh;
    float finishPathDistance;
    bool pathPoseFinalized;
    float straightBaseWorldZ;
    Quaternion straightBaseWorldRotation;
    float straightYawOffset;
    float straightYawVelocity;
    float appliedLongitudinalOffset;
    float resolvedLongitudinalOffsetDistance;
    float laneSeparationSide;
    bool accelerationTransformationInitialized;
    float currentPathLateralOffset;
    float contactYawOffset;
    float contactYawVelocity;
    float contactLateralDisplacement;
    float pendingForwardImpact;
    bool overtakeImpactTriggered;

    void OnEnable()
    {
        if (!ActiveVehicles.Contains(this)) ActiveVehicles.Add(this);
        overtakeImpactTriggered = false;
        sequenceStarted = false;
        sequenceActivationElapsed = 0f;
    }

    void OnDisable()
    {
        ActiveVehicles.Remove(this);
    }

    void Awake()
    {
        if (vehicleCollisionBox == null)
            vehicleCollisionBox = GetComponent<BoxCollider>();
        if (cutsceneManager == null)
            cutsceneManager = FindFirstObjectByType<CutsceneManager>();
        if (followRoadPointPath)
            InitializeRoadPath();
        if (cruiseMotion == null)
            cruiseMotion = GetComponent<TrailerCruiseMotion>();
        ResetSequenceSpeed();
    }

    void Update()
    {
        if (!IsSequenceActive) return;
        if (!sequenceStarted)
        {
            sequenceActivationElapsed += Time.deltaTime;
            if (sequenceActivationElapsed < sequenceStartDelaySeconds) return;
            ResetSequenceSpeed();
            sequenceElapsed = 0f;
            straightBaseWorldZ = transform.position.z;
            straightBaseWorldRotation = transform.rotation;
            straightYawOffset = 0f;
            appliedLongitudinalOffset = 0f;
            ResolveTimedLongitudinalOffset();
            ResolveLaneSeparationSide();
            ApplyAccelerationTransformation(0f);
            sequenceStarted = true;
            hudSpeedCycleTimer = 0f;
            hudSpeedLowChosen = false;
            hudSpeedInCooldown = false;
        }

        if (stopAtSequenceEnd && sequenceDurationSeconds > 0f &&
            sequenceElapsed >= sequenceDurationSeconds)
        {
            FinalizePathPose();
            FinalizeStraightLanePose();
            ApplyAccelerationTransformation(0f);
            ApplyBoostCameraFov(sequenceDurationSeconds);
            RequestApplicationStop();
            return;
        }

        float activeDeltaTime = sequenceDurationSeconds > 0f
            ? Mathf.Min(Time.deltaTime, Mathf.Max(0f, sequenceDurationSeconds - sequenceElapsed))
            : Time.deltaTime;

        UpdateHudSpeedVariation(activeDeltaTime);

        float nextElapsed = sequenceElapsed + activeDeltaTime;
        float transformationProgress = 0f;
        if (useAccelerationBurst)
            CurrentSpeedKmh = EvaluateAccelerationBurst(nextElapsed, out transformationProgress);
        else
        {
            float activeTargetSpeed = calculateSpeedToRoadPoint && pathReady
                ? calculatedPathSpeedKmh
                : Mathf.Max(1f, targetSpeedKmh);
            CurrentSpeedKmh = Mathf.MoveTowards(CurrentSpeedKmh,
                activeTargetSpeed, Mathf.Max(0f, accelerationKmh) * activeDeltaTime);
        }
        ApplyAccelerationTransformation(transformationProgress);
        ApplyBoostCameraFov(nextElapsed);
        SyncCruiseMotionSpeed();
        UpdateGear();

        float distanceThisFrame = CurrentSpeedKmh / 3.6f * activeDeltaTime;
        float longitudinalOffsetDelta =
            EvaluateTimedLongitudinalOffsetDelta(sequenceElapsed + activeDeltaTime);
        if (followRoadPointPath && pathReady)
            FollowRoadPath(distanceThisFrame + longitudinalOffsetDelta,
                sequenceElapsed + activeDeltaTime);
        else
        {
            Vector3 direction = GetDirection();
            distanceThisFrame = LimitStraightPressureDistance(direction, distanceThisFrame);
            transform.Translate(direction * (distanceThisFrame + longitudinalOffsetDelta),
                space == CoordinateSpace.World ? Space.World : Space.Self);
            ApplyStraightLaneMotion(sequenceElapsed + activeDeltaTime);
        }
        sequenceElapsed += activeDeltaTime;
        if (sequenceDurationSeconds > 0f && sequenceElapsed >= sequenceDurationSeconds)
        {
            FinalizePathPose();
            FinalizeStraightLanePose();
            ApplyAccelerationTransformation(0f);
            ApplyBoostCameraFov(sequenceDurationSeconds);
            RequestApplicationStop();
        }
    }

    void ResetSequenceSpeed()
    {
        CurrentSpeedKmh = calculateSpeedToRoadPoint && pathReady
            ? calculatedPathSpeedKmh
            : Mathf.Clamp(initialSpeedKmh, 0f, Mathf.Max(1f, targetSpeedKmh));
        ApplyBoostCameraFov(0f);
        UpdateGear();
        if (cruiseMotion != null)
        {
            cruiseMotion.speedKph = CurrentSpeedKmh;
            cruiseMotion.wheelSpeedKph = Mathf.Min(600f, CurrentSpeedKmh);
        }
    }

    // 왕복(최고점 -> 최저점 -> 최고점) 단계와 쿨타임(최고점에 정지) 단계를
    // hudSpeedCycleSeconds / hudSpeedCooldownSeconds 시간만큼 번갈아 진행합니다.
    // 왕복 단계가 새로 시작될 때마다 이번엔 깊은 최저점(Deep Low)으로 갈지
    // 얕은 최저점(Shallow Low)으로 갈지 hudSpeedDeepLowChance 확률로 다시 뽑습니다.
    void UpdateHudSpeedVariation(float deltaTime)
    {
        if (!useHudSpeedVariation) return;

        // 아직 최저점이 선택되지 않았다면 새로 뽑고 타이머 초기화
        if (!hudSpeedLowChosen)
        {
            PickNextHudSpeedLow();
            hudSpeedLowChosen = true;
            hudSpeedInCooldown = false;
            hudSpeedCycleTimer = 0f;
        }

        hudSpeedCycleTimer += deltaTime;

        // 현재 상태(왕복 진행 중 vs 쿨타임 중)에 따른 한 단계(Phase) 시간
        float currentPhaseDuration = hudSpeedInCooldown
            ? Mathf.Max(0.0001f, hudSpeedCooldownSeconds)
            : Mathf.Max(0.01f, hudSpeedCycleSeconds);

        // 현재 단계 시간이 완료된 경우 다음 단계로 전환
        while (hudSpeedCycleTimer >= currentPhaseDuration)
        {
            hudSpeedCycleTimer -= currentPhaseDuration;

            if (!hudSpeedInCooldown)
            {
                // [왕복 완료] -> 쿨타임 진입
                hudSpeedInCooldown = true;
            }
            else
            {
                // 다음 왕복을 즉시 준비하고 남은 프레임 시간을 유지합니다.
                hudSpeedInCooldown = false;
                PickNextHudSpeedLow();
                hudSpeedLowChosen = true;
            }

            currentPhaseDuration = hudSpeedInCooldown
                ? Mathf.Max(0.0001f, hudSpeedCooldownSeconds)
                : Mathf.Max(0.01f, hudSpeedCycleSeconds);
        }
    }

    void PickNextHudSpeedLow()
    {
        hudSpeedCurrentLowKmh = Random.value < hudSpeedDeepLowChance
            ? hudSpeedDeepLowKmh
            : hudSpeedShallowLowKmh;
    }

    void SyncCruiseMotionSpeed()
    {
        if (cruiseMotion == null) return;
        cruiseMotion.speedKph = CurrentSpeedKmh;
        cruiseMotion.wheelSpeedKph = Mathf.Min(600f, CurrentSpeedKmh);
    }

    float EvaluateAccelerationBurst(float elapsed, out float transformationProgress)
    {
        float rampUpEnd = burstStartSeconds + Mathf.Max(0.01f, burstRampUpSeconds);
        float holdEnd = rampUpEnd + Mathf.Max(0f, burstHoldSeconds);
        float rampDownEnd = holdEnd + Mathf.Max(0.01f, burstRampDownSeconds);

        if (elapsed <= burstStartSeconds)
            transformationProgress = 0f;
        else if (elapsed < rampUpEnd)
            transformationProgress = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(burstStartSeconds, rampUpEnd, elapsed));
        else if (elapsed <= holdEnd)
            transformationProgress = 1f;
        else if (elapsed < rampDownEnd)
            transformationProgress = Mathf.SmoothStep(1f, 0f,
                Mathf.InverseLerp(holdEnd, rampDownEnd, elapsed));
        else
            transformationProgress = 0f;

        return Mathf.Lerp(burstCruiseSpeedKmh, burstPeakSpeedKmh, transformationProgress);
    }

    void ApplyAccelerationTransformation(float progress)
    {
        if (!useAccelerationBurst || accelerationTransformation == null) return;
        if (!accelerationTransformationInitialized)
        {
            accelerationTransformation.AutoBindParts();
            accelerationTransformation.AutoBindBoosterFx();
            accelerationTransformationInitialized = true;
        }
        accelerationTransformation.ApplyTransformationProgress(Mathf.Clamp01(progress));
    }

    void ApplyBoostCameraFov(float elapsed)
    {
        if (!animateCameraFovDuringBurst || burstCamera == null) return;

        float riseEnd = burstStartSeconds + Mathf.Max(0.01f, burstFovRiseSeconds);
        float fallEnd = riseEnd + Mathf.Max(0.01f, burstFovFallSeconds);
        float progress;
        if (elapsed <= burstStartSeconds)
            progress = 0f;
        else if (elapsed < riseEnd)
            progress = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(burstStartSeconds, riseEnd, elapsed));
        else if (elapsed < fallEnd)
            progress = Mathf.SmoothStep(1f, 0f,
                Mathf.InverseLerp(riseEnd, fallEnd, elapsed));
        else
            progress = 0f;

        LensSettings lens = burstCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(burstBaseFov, burstPeakFov, progress);
        burstCamera.Lens = lens;
    }

    void UpdateGear()
    {
        CurrentGear = CurrentSpeedKmh <= .01f
            ? 1
            : Mathf.Clamp(Mathf.CeilToInt(CurrentSpeedKmh / 50f), 1, 6);
    }

    Vector3 GetDirection()
    {
        switch (moveAxis)
        {
            case MoveAxis.Z_Forward: return Vector3.forward;
            case MoveAxis.X_Right: return Vector3.right;
            case MoveAxis.Y_Up: return Vector3.up;
            case MoveAxis.Custom:
                return customDirection.sqrMagnitude > 0f
                ? customDirection.normalized : Vector3.right;
            default: return Vector3.right;
        }
    }

    void InitializeRoadPath()
    {
        ResolveRoadSurface();
        GameObject guide = GameObject.Find(roadGuideObjectName);
        if (guide == null) return;

        roadPoints.Clear();
        foreach (Transform child in guide.GetComponentsInChildren<Transform>(true))
            if (child != guide.transform && child.name.StartsWith("Road Point "))
                roadPoints.Add(child);
        roadPoints.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (roadPoints.Count < 2) return;

        cumulativeDistances = new float[roadPoints.Count];
        pathLength = 0f;
        for (int i = 1; i < roadPoints.Count; i++)
        {
            pathLength += Vector3.Distance(roadPoints[i - 1].position, roadPoints[i].position);
            cumulativeDistances[i] = pathLength;
        }

        pathDistance = FindNearestPathDistance(transform.position, out Vector3 nearest, out Vector3 tangent);
        int destinationIndex = Mathf.Clamp(finishRoadPointIndex, 0, roadPoints.Count - 1);
        finishPathDistance = cumulativeDistances[destinationIndex];
        if (calculateSpeedToRoadPoint && sequenceDurationSeconds > 0f)
        {
            float distanceToFinish = Mathf.Abs(pathDistance - finishPathDistance);
            calculatedPathSpeedKmh = distanceToFinish / sequenceDurationSeconds * 3.6f;
        }
        Vector3 travelTangent = reverseRoadPointOrder ? -tangent : tangent;
        Vector3 lateral = Vector3.Cross(Vector3.up, travelTangent).normalized;
        lateralOffset = Vector3.Dot(transform.position - nearest, lateral);
        currentPathLateralOffset = lateralOffset;
        verticalOffset = transform.position.y - nearest.y;
        pathReady = true;
    }

    void ResolveRoadSurface()
    {
        if (roadSurfaceCollider == null)
        {
            GameObject roadSurface = GameObject.Find(roadSurfaceObjectName);
            if (roadSurface != null)
                roadSurfaceCollider = roadSurface.GetComponent<Collider>();
        }

        if (TryGetRoadSurfaceHeight(transform.position, out float roadHeight))
            roadSurfaceClearance = transform.position.y - roadHeight;
    }

    bool TryGetRoadSurfaceHeight(Vector3 position, out float height)
    {
        height = 0f;
        if (roadSurfaceCollider == null) return false;

        Bounds bounds = roadSurfaceCollider.bounds;
        float rayHeight = Mathf.Max(position.y, bounds.max.y) + 20f;
        Ray ray = new Ray(new Vector3(position.x, rayHeight, position.z), Vector3.down);
        if (!roadSurfaceCollider.Raycast(ray, out RaycastHit hit, 100f)) return false;
        height = hit.point.y;
        return true;
    }

    float FindNearestPathDistance(Vector3 position, out Vector3 nearest, out Vector3 tangent)
    {
        float bestSqr = float.PositiveInfinity;
        float bestDistance = 0f;
        nearest = roadPoints[0].position;
        tangent = (roadPoints[1].position - roadPoints[0].position).normalized;
        for (int i = 0; i < roadPoints.Count - 1; i++)
        {
            Vector3 a = roadPoints[i].position;
            Vector3 segment = roadPoints[i + 1].position - a;
            float length = segment.magnitude;
            if (length <= 0.0001f) continue;
            float t = Mathf.Clamp01(Vector3.Dot(position - a, segment) / (length * length));
            Vector3 candidate = a + segment * t;
            float sqr = (position - candidate).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            nearest = candidate;
            tangent = segment / length;
            bestDistance = cumulativeDistances[i] + length * t;
        }
        return bestDistance;
    }

    void FollowRoadPath(float deltaDistance, float elapsed)
    {
        if (pendingForwardImpact > 0f)
        {
            float impactSpeed = Mathf.Max(contactForwardPushDistance,
                pendingForwardImpact) / Mathf.Max(0.05f, contactForwardPushDuration);
            float impactStep = Mathf.Min(pendingForwardImpact,
                impactSpeed * Time.deltaTime);
            deltaDistance += impactStep;
            pendingForwardImpact -= impactStep;
        }
        float previousPathDistance = pathDistance;
        pathDistance = Mathf.Clamp(pathDistance + (reverseRoadPointOrder ? -deltaDistance : deltaDistance), 0f, pathLength);
        SamplePath(pathDistance, out Vector3 center, out Vector3 segmentTangent);
        float directionSign = reverseRoadPointOrder ? -1f : 1f;
        float lookAheadDistance = Mathf.Clamp(
            pathDistance + directionSign * Mathf.Max(0.1f, pathLookAheadDistance), 0f, pathLength);
        SamplePath(lookAheadDistance, out Vector3 lookAheadPoint, out _);
        Vector3 travelTangent = lookAheadPoint - center;
        if (travelTangent.sqrMagnitude <= 0.0001f)
            travelTangent = reverseRoadPointOrder ? -segmentTangent : segmentTangent;
        else
            travelTangent.Normalize();
        Vector3 lateral = Vector3.Cross(Vector3.up, travelTangent).normalized;
        float laneOffset = enableSingleLaneMove && sequenceDurationSeconds > 0f
            ? laneMoveAmplitudeZ * EvaluateLaneProfile(Mathf.Clamp01(elapsed / sequenceDurationSeconds))
            : 0f;
        float baseLateralOffset = lateralOffset + laneOffset;
        float plannedLateralOffset = baseLateralOffset + contactLateralDisplacement;
        plannedLateralOffset = ConstrainLaneSeparation(
            plannedLateralOffset, travelTangent, lateral);
        bool contacted = ResolveVehicleContact(previousPathDistance, ref pathDistance,
            ref plannedLateralOffset, travelTangent, lateral);
        if (contacted)
        {
            SamplePath(pathDistance, out center, out segmentTangent);
            lookAheadDistance = Mathf.Clamp(
                pathDistance + directionSign * Mathf.Max(0.1f, pathLookAheadDistance),
                0f, pathLength);
            SamplePath(lookAheadDistance, out lookAheadPoint, out _);
            travelTangent = lookAheadPoint - center;
            if (travelTangent.sqrMagnitude <= 0.0001f)
                travelTangent = reverseRoadPointOrder ? -segmentTangent : segmentTangent;
            else
                travelTangent.Normalize();
            lateral = Vector3.Cross(Vector3.up, travelTangent).normalized;
        }
        contactLateralDisplacement = plannedLateralOffset - baseLateralOffset;
        if (!contacted)
            contactLateralDisplacement = Mathf.MoveTowards(contactLateralDisplacement, 0f,
                Mathf.Max(0.01f, contactSideRecoverySpeed) * Time.deltaTime);
        currentPathLateralOffset = plannedLateralOffset;
        float contactYawTarget = contacted
            ? Mathf.Sign(plannedLateralOffset - lateralOffset) * contactYawKick
            : 0f;
        contactYawOffset = Mathf.SmoothDampAngle(contactYawOffset, contactYawTarget,
            ref contactYawVelocity, Mathf.Max(0.01f, contactYawRecoveryTime),
            Mathf.Infinity, Time.deltaTime);

        float visualWheelAngle = ApplyCurveSteering(lookAheadDistance, travelTangent, directionSign);
        float driftStrength = enableDrift
            ? Mathf.Clamp01(Mathf.Abs(visualWheelAngle) / 4f)
            : 0f;
        float targetDriftAngle = Mathf.Sign(visualWheelAngle) * maximumDriftAngle * driftStrength;
        driftAngle = Mathf.SmoothDampAngle(driftAngle, targetDriftAngle, ref driftVelocity,
            Mathf.Max(0.01f, driftResponseTime), Mathf.Infinity, Time.deltaTime);
        Vector3 targetPosition = center + lateral * plannedLateralOffset + Vector3.up * verticalOffset;
        if (TryGetRoadSurfaceHeight(targetPosition, out float roadHeight))
            targetPosition.y = roadHeight + roadSurfaceClearance;
        transform.position = targetPosition;
        if (travelTangent.sqrMagnitude > 0.0001f)
        {
            float laneYaw = UpdateLaneYawOffset(elapsed);
            float targetYaw = Quaternion.LookRotation(travelTangent, Vector3.up).eulerAngles.y +
                driftAngle + laneYaw + contactYawOffset;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVelocity,
                Mathf.Max(0.01f, steeringResponseTime), Mathf.Max(1f, maximumYawRate), Time.deltaTime);
            float steeringRatio = maxVisualWheelAngle > 0.001f
                ? Mathf.Clamp(visualWheelAngle / maxVisualWheelAngle, -1f, 1f)
                : 0f;
            float targetRoll = -steeringRatio * maximumBodyRoll;
            bodyRoll = Mathf.SmoothDamp(bodyRoll, targetRoll, ref bodyRollVelocity,
                Mathf.Max(0.01f, bodyRollResponseTime), Mathf.Infinity, Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw, bodyRoll);
        }
    }

    float ApplyCurveSteering(float lookAheadDistance, Vector3 travelTangent, float directionSign)
    {
        float steeringSampleDistance = Mathf.Clamp(
            lookAheadDistance + directionSign * Mathf.Max(8f, pathLookAheadDistance), 0f, pathLength);
        SamplePath(lookAheadDistance, out Vector3 nearPoint, out _);
        SamplePath(steeringSampleDistance, out Vector3 fartherPoint, out Vector3 fartherSegmentTangent);
        Vector3 fartherTangent = fartherPoint - nearPoint;
        if (fartherTangent.sqrMagnitude <= 0.0001f)
            fartherTangent = reverseRoadPointOrder ? -fartherSegmentTangent : fartherSegmentTangent;
        else
            fartherTangent.Normalize();

        float wheelAngle = Mathf.Clamp(
            Vector3.SignedAngle(travelTangent, fartherTangent, Vector3.up) * visualSteeringStrength,
            -maxVisualWheelAngle, maxVisualWheelAngle);
        if (!applyVisualSteering || cruiseMotion == null) return wheelAngle;
        float driftBlend = enableDrift ? Mathf.Clamp01(Mathf.Abs(wheelAngle) / 4f) : 0f;
        float displayedWheelAngle = Mathf.Lerp(
            wheelAngle, -wheelAngle * counterSteerStrength, driftBlend);
        cruiseMotion.manualSteeringWeight = 1f;
        cruiseMotion.steeringWheelAngle = cruiseMotion.wheelAngleAt360 > 0.0001f
            ? displayedWheelAngle / cruiseMotion.wheelAngleAt360 * 360f
            : 0f;
        return wheelAngle;
    }

    void ApplyStraightLaneMotion(float elapsed)
    {
        if (!enableSingleLaneMove || sequenceDurationSeconds <= 0f) return;
        float progress = Mathf.Clamp01(elapsed / sequenceDurationSeconds);
        float profile = EvaluateLaneProfile(progress);
        Vector3 position = transform.position;
        position.z = straightBaseWorldZ + laneMoveAmplitudeZ * profile;
        transform.position = position;

        UpdateLaneYawOffset(elapsed);
        transform.rotation = straightBaseWorldRotation * Quaternion.Euler(0f, straightYawOffset, 0f);
        if (applyVisualSteering && cruiseMotion != null)
        {
            cruiseMotion.manualSteeringWeight = 1f;
            cruiseMotion.steeringWheelAngle = cruiseMotion.wheelAngleAt360 > 0.0001f
                ? straightYawOffset / Mathf.Max(0.01f, maximumLaneYaw) *
                  cruiseMotion.wheelAngleAt360 * 8f
                : 0f;
        }
    }

    float EvaluateTimedLongitudinalOffsetDelta(float elapsed)
    {
        if (!useTimedLongitudinalOffset) return 0f;

        float duration = Mathf.Max(0.01f, longitudinalOffsetDurationSeconds);
        float progress = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(longitudinalOffsetStartSeconds,
                longitudinalOffsetStartSeconds + duration, elapsed));
        float targetOffset = resolvedLongitudinalOffsetDistance * progress;
        float deltaOffset = targetOffset - appliedLongitudinalOffset;
        appliedLongitudinalOffset = targetOffset;
        return Mathf.Abs(deltaOffset) <= 0.0001f ? 0f : deltaOffset;
    }

    void ResolveTimedLongitudinalOffset()
    {
        resolvedLongitudinalOffsetDistance = longitudinalOffsetDistance;
        if (!calculateLongitudinalOffsetFromTarget ||
            longitudinalOffsetTarget == null)
            return;

        Vector3 direction = GetDirection();
        Vector3 worldDirection = space == CoordinateSpace.World
            ? direction.normalized
            : transform.TransformDirection(direction).normalized;
        float currentGap = Vector3.Dot(
            longitudinalOffsetTarget.transform.position - transform.position,
            worldDirection);
        resolvedLongitudinalOffsetDistance = Mathf.Clamp(
            currentGap - longitudinalTargetGap,
            0f,
            Mathf.Max(0f, maximumAutomaticLongitudinalOffset));
    }

    void ResolveLaneSeparationSide()
    {
        laneSeparationSide = 0f;
        if (laneSeparationTarget == null) return;

        float side = currentPathLateralOffset -
            laneSeparationTarget.currentPathLateralOffset;
        laneSeparationSide = Mathf.Abs(side) > 0.001f
            ? Mathf.Sign(side)
            : 1f;
    }

    float ConstrainLaneSeparation(
        float plannedLateral, Vector3 forward, Vector3 lateral)
    {
        if (laneSeparationTarget == null ||
            vehicleCollisionBox == null ||
            laneSeparationTarget.vehicleCollisionBox == null ||
            !laneSeparationTarget.pathReady)
            return plannedLateral;

        float requiredLongitudinal =
            ProjectedHalfExtent(vehicleCollisionBox, forward) +
            ProjectedHalfExtent(
                laneSeparationTarget.vehicleCollisionBox, forward) +
            laneSeparationPadding;
        float forwardGap = Mathf.Abs(
            PathForwardDistance(pathDistance) -
            laneSeparationTarget.PathForwardDistance(
                laneSeparationTarget.pathDistance));
        if (forwardGap >= requiredLongitudinal)
            return plannedLateral;

        float requiredLateral =
            ProjectedHalfExtent(vehicleCollisionBox, lateral) +
            ProjectedHalfExtent(
                laneSeparationTarget.vehicleCollisionBox, lateral) +
            laneSeparationPadding;
        float targetLateral = laneSeparationTarget.currentPathLateralOffset;
        return laneSeparationSide >= 0f
            ? Mathf.Max(plannedLateral, targetLateral + requiredLateral)
            : Mathf.Min(plannedLateral, targetLateral - requiredLateral);
    }

    float UpdateLaneYawOffset(float elapsed)
    {
        if (!enableSingleLaneMove || sequenceDurationSeconds <= 0f) return 0f;
        float progress = Mathf.Clamp01(elapsed / sequenceDurationSeconds);
        if (progress >= 0.999f)
        {
            straightYawOffset = 0f;
            straightYawVelocity = 0f;
            return 0f;
        }
        float sampleStep = 0.01f;
        float nextProgress = Mathf.Clamp01(progress + sampleStep);
        float nextProfile = EvaluateLaneProfile(nextProgress);
        float sampleSeconds = Mathf.Max(0.0001f, sequenceDurationSeconds * (nextProgress - progress));
        float lateralVelocity = sampleSeconds > 0.0001f
            ? laneMoveAmplitudeZ * (nextProfile - EvaluateLaneProfile(progress)) / sampleSeconds
            : 0f;
        float forwardSpeed = Mathf.Max(0.01f, CurrentSpeedKmh / 3.6f);
        float targetYawOffset = Mathf.Clamp(
            Mathf.Atan2(lateralVelocity, forwardSpeed) * Mathf.Rad2Deg,
            -maximumLaneYaw, maximumLaneYaw);
        straightYawOffset = Mathf.SmoothDampAngle(straightYawOffset, targetYawOffset,
            ref straightYawVelocity, Mathf.Max(0.01f, laneYawResponseTime),
            Mathf.Infinity, Time.deltaTime);
        return straightYawOffset;
    }

    float EvaluateLaneProfile(float progress)
    {
        if (useTimedLaneMove)
        {
            float elapsed = progress * sequenceDurationSeconds;
            if (elapsed <= laneMoveStartSeconds) return 0f;

            float moveInEnd = laneMoveStartSeconds + Mathf.Max(0.01f, laneMoveInSeconds);
            if (elapsed < moveInEnd)
                return Mathf.SmoothStep(0f,
                    useLaneApproachPhase ? laneApproachRatio : 1f,
                    Mathf.InverseLerp(laneMoveStartSeconds, moveInEnd, elapsed));

            float fightEnd = moveInEnd;
            if (useLaneApproachPhase)
            {
                fightEnd += Mathf.Max(0.01f, laneFightInSeconds);
                if (elapsed < fightEnd)
                    return Mathf.SmoothStep(laneApproachRatio, 1f,
                        Mathf.InverseLerp(moveInEnd, fightEnd, elapsed));
            }

            float holdEnd = fightEnd + Mathf.Max(0f, laneHoldSeconds);
            if (elapsed <= holdEnd) return 1f;

            float moveOutEnd = holdEnd + Mathf.Max(0.01f, laneMoveOutSeconds);
            if (useLanePushCounter)
            {
                if (elapsed < moveOutEnd)
                    return Mathf.SmoothStep(1f, laneCounterFinalRatio,
                        Mathf.InverseLerp(holdEnd, moveOutEnd, elapsed));
                return laneCounterFinalRatio;
            }
            if (keepLaneOffsetAtEnd) return 1f;
            if (elapsed < moveOutEnd)
                return Mathf.SmoothStep(1f, 0f,
                    Mathf.InverseLerp(holdEnd, moveOutEnd, elapsed));
            return 0f;
        }

        if (!weaveBothSides)
        {
            float wave = Mathf.Sin(Mathf.PI * progress);
            return wave * wave;
        }

        return 1.333333f * Mathf.Sin(Mathf.PI * 2f * progress) *
            Mathf.Sin(Mathf.PI * progress);
    }

    void FinalizeStraightLanePose()
    {
        if (!enableSingleLaneMove || (followRoadPointPath && pathReady)) return;

        Vector3 position = transform.position;
        position.z = straightBaseWorldZ +
            laneMoveAmplitudeZ * EvaluateLaneProfile(1f);
        transform.position = position;
        transform.rotation = straightBaseWorldRotation;
        straightYawOffset = 0f;
        straightYawVelocity = 0f;
        if (cruiseMotion != null)
            cruiseMotion.steeringWheelAngle = 0f;
    }

    float LimitStraightPressureDistance(Vector3 direction, float requestedDistance)
    {
        if (pressureTarget == null || requestedDistance <= 0f)
            return requestedDistance;

        Vector3 worldDirection = space == CoordinateSpace.World
            ? direction.normalized
            : transform.TransformDirection(direction).normalized;
        float forwardGap = Vector3.Dot(
            pressureTarget.transform.position - transform.position,
            worldDirection);
        return Mathf.Min(requestedDistance,
            Mathf.Max(0f, forwardGap + pressureForwardOffset -
                pressureFollowingDistance));
    }

    void FinalizePathPose()
    {
        if (pathPoseFinalized || !pathReady || !calculateSpeedToRoadPoint) return;
        pathPoseFinalized = true;
        pathDistance = finishPathDistance;
        SamplePath(pathDistance, out Vector3 center, out Vector3 tangent);
        Vector3 travelTangent = reverseRoadPointOrder ? -tangent : tangent;
        Vector3 lateral = Vector3.Cross(Vector3.up, travelTangent).normalized;
        transform.position = center + lateral * lateralOffset + Vector3.up * verticalOffset;
        transform.rotation = Quaternion.LookRotation(travelTangent, Vector3.up);
        driftAngle = 0f;
        bodyRoll = 0f;
        if (cruiseMotion != null)
        {
            cruiseMotion.manualSteeringWeight = 1f;
            cruiseMotion.steeringWheelAngle = 0f;
        }
    }

    bool ResolveVehicleContact(float previousDistance, ref float nextDistance,
        ref float plannedLateral, Vector3 forward, Vector3 lateral)
    {
        if (!preventVehicleOverlap || !pathReady || vehicleCollisionBox == null)
            return false;

        bool contacted = false;
        float previousForward = PathForwardDistance(previousDistance);
        float nextForward = PathForwardDistance(nextDistance);
        float ownHalfLength = ProjectedHalfExtent(vehicleCollisionBox, forward);
        float ownHalfWidth = ProjectedHalfExtent(vehicleCollisionBox, lateral);

        for (int i = ActiveVehicles.Count - 1; i >= 0; i--)
        {
            MidRaceSpeedController other = ActiveVehicles[i];
            if (other == null)
            {
                ActiveVehicles.RemoveAt(i);
                continue;
            }
            if (other == this || !other.isActiveAndEnabled || !other.pathReady ||
                !other.preventVehicleOverlap || other.vehicleCollisionBox == null ||
                other.roadGuideObjectName != roadGuideObjectName)
                continue;

            float requiredLongitudinal = ownHalfLength +
                ProjectedHalfExtent(other.vehicleCollisionBox, forward) +
                vehicleContactPadding;
            float requiredLateral = ownHalfWidth +
                ProjectedHalfExtent(other.vehicleCollisionBox, lateral) +
                vehicleContactPadding;
            float otherForward = other.PathForwardDistance(other.pathDistance);
            float previousSide = currentPathLateralOffset - other.currentPathLateralOffset;
            float plannedSide = plannedLateral - other.currentPathLateralOffset;
            bool crossesLongitudinally =
                (previousForward - otherForward) * (nextForward - otherForward) <= 0f;
            bool closeLongitudinally = crossesLongitudinally ||
                Mathf.Abs(nextForward - otherForward) < requiredLongitudinal;
            bool crossesSide = previousSide * plannedSide <= 0f;
            bool overlapsSide = Mathf.Abs(plannedSide) < requiredLateral;

            if (!closeLongitudinally || (!crossesSide && !overlapsSide)) continue;
            bool scriptedOvertakeContact = allowForwardOvertake &&
                (overtakeImpactTarget == null || other == overtakeImpactTarget);
            float sideSign = Mathf.Abs(previousSide) > 0.001f
                ? Mathf.Sign(previousSide)
                : Mathf.Abs(plannedSide) > 0.001f
                    ? Mathf.Sign(plannedSide)
                    : (GetInstanceID() < other.GetInstanceID() ? -1f : 1f);
            float lateralPenetration = Mathf.Max(0f,
                requiredLateral - Mathf.Abs(plannedSide));
            if (scriptedOvertakeContact)
            {
                if (!overtakeImpactTriggered)
                {
                    float sidePush = -sideSign * Mathf.Min(contactSidePushDistance,
                        Mathf.Max(.08f, lateralPenetration * .65f));
                    other.ReceiveVehicleImpact(0f, sidePush,
                        -sideSign * contactYawKick);
                    overtakeImpactTriggered = true;
                }
                continue;
            }

            contacted = true;
            bool firstContact = Mathf.Abs(previousForward - otherForward) >
                requiredLongitudinal + .02f ||
                Mathf.Abs(previousSide) > requiredLateral + .02f;

            bool approachingFromBehind = previousForward < otherForward - .05f &&
                nextForward > otherForward - requiredLongitudinal;
            float longitudinalPenetration = Mathf.Max(0f,
                requiredLongitudinal - (otherForward - nextForward));
            float impactForwardPush = firstContact && approachingFromBehind
                ? Mathf.Min(contactForwardPushDistance,
                    Mathf.Max(.06f, longitudinalPenetration * .35f))
                : 0f;
            if (approachingFromBehind)
            {
                nextForward = otherForward - requiredLongitudinal + impactForwardPush;
                nextDistance = PathDistanceFromForward(nextForward);
                CurrentSpeedKmh = Mathf.Min(CurrentSpeedKmh, other.CurrentSpeedKmh);
            }

            plannedLateral = other.currentPathLateralOffset + sideSign * requiredLateral;

            if (firstContact)
            {
                bool glancingContact = Mathf.Abs(previousSide) > .05f ||
                    Mathf.Abs(plannedSide) > .05f;
                float sidePush = glancingContact
                    ? -sideSign * Mathf.Min(contactSidePushDistance,
                        Mathf.Max(.04f, lateralPenetration * .5f))
                    : 0f;
                float yawPush = glancingContact ? -sideSign * contactYawKick : 0f;
                other.ReceiveVehicleImpact(impactForwardPush, sidePush, yawPush);
            }
        }

        return contacted;
    }

    void ReceiveVehicleImpact(float forwardPush, float sidePush, float yawPush)
    {
        pendingForwardImpact = Mathf.Max(pendingForwardImpact, Mathf.Max(0f, forwardPush));
        contactLateralDisplacement = Mathf.Clamp(
            contactLateralDisplacement + sidePush, -1f, 1f);
        if (Mathf.Abs(yawPush) > .001f)
        {
            contactYawOffset = Mathf.Clamp(contactYawOffset + yawPush,
                -contactYawKick, contactYawKick);
            contactYawVelocity = 0f;
        }
    }

    float PathForwardDistance(float distance)
    {
        return reverseRoadPointOrder ? pathLength - distance : distance;
    }

    float PathDistanceFromForward(float forwardDistance)
    {
        float distance = reverseRoadPointOrder
            ? pathLength - forwardDistance
            : forwardDistance;
        return Mathf.Clamp(distance, 0f, pathLength);
    }

    static float ProjectedHalfExtent(BoxCollider box, Vector3 axis)
    {
        Vector3 direction = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.forward;
        Vector3 scale = box.transform.lossyScale;
        Vector3 half = Vector3.Scale(box.size * .5f,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return Mathf.Abs(Vector3.Dot(box.transform.right, direction)) * half.x +
            Mathf.Abs(Vector3.Dot(box.transform.up, direction)) * half.y +
            Mathf.Abs(Vector3.Dot(box.transform.forward, direction)) * half.z;
    }

    void SamplePath(float distance, out Vector3 position, out Vector3 tangent)
    {
        int segment = 0;
        while (segment < cumulativeDistances.Length - 2 && cumulativeDistances[segment + 1] < distance)
            segment++;
        Vector3 a = roadPoints[segment].position;
        Vector3 b = roadPoints[segment + 1].position;
        float length = Mathf.Max(0.0001f, cumulativeDistances[segment + 1] - cumulativeDistances[segment]);
        float t = Mathf.Clamp01((distance - cumulativeDistances[segment]) / length);
        position = Vector3.Lerp(a, b, t);
        tangent = (b - a).normalized;
    }

    void RequestApplicationStop()
    {
        if (!stopApplicationAtSequenceEnd || stopRequested) return;
        stopRequested = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += StopEditorPlayMode;
#else
        Application.Quit();
#endif
    }

#if UNITY_EDITOR
    static void StopEditorPlayMode()
    {
        if (UnityEditor.EditorApplication.isPlaying)
            UnityEditor.EditorApplication.isPlaying = false;
    }
#endif
}
