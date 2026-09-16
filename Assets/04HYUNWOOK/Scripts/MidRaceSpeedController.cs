using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MidRaceSpeedController : MonoBehaviour
{
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

    [Header("Sequence timing")]
    [Tooltip("0보다 크면 이 시간 동안 진행률이 0%에서 100%까지 증가합니다.")]
    [Min(0f)] [SerializeField] private float sequenceDurationSeconds;
    [Tooltip("100%에 도달하면 차량 이동을 종료합니다.")]
    [SerializeField] private bool stopAtSequenceEnd = true;
    [Tooltip("시퀀스가 끝나면 에디터 Play Mode 또는 실행 중인 빌드를 종료합니다.")]
    [SerializeField] private bool stopApplicationAtSequenceEnd;

    [Header("Movement")]
    public MoveAxis moveAxis = MoveAxis.X_Right;
    public CoordinateSpace space = CoordinateSpace.World;
    public Vector3 customDirection = Vector3.right;

    [Header("Curved road path (optional)")]
    [Tooltip("Road Point 00~44 중심선을 따라 차량 위치와 회전을 갱신합니다.")]
    [SerializeField] private bool followRoadPointPath;
    [SerializeField] private string roadGuideObjectName = "Racing Road Centerline Guide";
    [Tooltip("현재 HW_Racing 차량 배치는 Road Point의 역순으로 커브에 진입합니다.")]
    [SerializeField] private bool reverseRoadPointOrder = true;
    [Tooltip("앞쪽 경로를 미리 읽어 커브 회전을 부드럽게 만드는 거리입니다.")]
    [Min(0.1f)] [SerializeField] private float pathLookAheadDistance = 30f;

    [Header("Visual steering")]
    [SerializeField] private TrailerCruiseMotion cruiseMotion;
    [SerializeField] private bool applyVisualSteering = true;
    [Range(0f, 2f)] [SerializeField] private float visualSteeringStrength = 1.25f;
    [Range(0f, 45f)] [SerializeField] private float maxVisualWheelAngle = 22f;

    [Header("Cornering body dynamics")]
    [Tooltip("목표 코너 방향까지 차체가 따라가는 시간입니다.")]
    [Min(0.01f)] [SerializeField] private float steeringResponseTime = 0.28f;
    [Tooltip("차체가 한 초 동안 회전할 수 있는 최대 각도입니다.")]
    [Min(1f)] [SerializeField] private float maximumYawRate = 45f;
    [Tooltip("코너 바깥쪽으로 기울어지는 차체 최대 각도입니다.")]
    [Range(0f, 8f)] [SerializeField] private float maximumBodyRoll = 2.2f;
    [Min(0.01f)] [SerializeField] private float bodyRollResponseTime = 0.32f;

    public float CurrentSpeedKmh { get; private set; }
    public int CurrentGear { get; private set; } = 1;
    public float SequenceProgress => sequenceDurationSeconds > 0f
        ? Mathf.Clamp01(sequenceElapsed / sequenceDurationSeconds)
        : 0f;
    public bool ProvidesSequenceProgress => sequenceDurationSeconds > 0f && IsSequenceActive;
    public bool IsSequenceActive => cutsceneManager != null
        ? cutsceneManager.activeCamera >= firstSequenceCamera &&
          cutsceneManager.activeCamera <= lastSequenceCamera
        : runWithoutCutsceneManager;

    bool sequenceStarted;
    bool stopRequested;
    float sequenceElapsed;
    readonly List<Transform> roadPoints = new List<Transform>();
    float[] cumulativeDistances;
    float pathDistance;
    float pathLength;
    float lateralOffset;
    float verticalOffset;
    bool pathReady;
    float yawVelocity;
    float bodyRoll;
    float bodyRollVelocity;

    void Awake()
    {
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
            ResetSequenceSpeed();
            sequenceElapsed = 0f;
            sequenceStarted = true;
        }

        if (stopAtSequenceEnd && sequenceDurationSeconds > 0f &&
            sequenceElapsed >= sequenceDurationSeconds)
        {
            RequestApplicationStop();
            return;
        }

        float activeDeltaTime = sequenceDurationSeconds > 0f
            ? Mathf.Min(Time.deltaTime, Mathf.Max(0f, sequenceDurationSeconds - sequenceElapsed))
            : Time.deltaTime;

        CurrentSpeedKmh = Mathf.MoveTowards(CurrentSpeedKmh,
            Mathf.Max(1f, targetSpeedKmh), Mathf.Max(0f, accelerationKmh) * activeDeltaTime);
        UpdateGear();

        float distanceThisFrame = CurrentSpeedKmh / 3.6f * activeDeltaTime;
        if (followRoadPointPath && pathReady)
            FollowRoadPath(distanceThisFrame);
        else
        {
            Vector3 direction = GetDirection();
            transform.Translate(direction * distanceThisFrame,
                space == CoordinateSpace.World ? Space.World : Space.Self);
        }
        sequenceElapsed += activeDeltaTime;
        if (sequenceDurationSeconds > 0f && sequenceElapsed >= sequenceDurationSeconds)
            RequestApplicationStop();
    }

    void ResetSequenceSpeed()
    {
        CurrentSpeedKmh = Mathf.Clamp(initialSpeedKmh, 0f, Mathf.Max(1f, targetSpeedKmh));
        UpdateGear();
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
            case MoveAxis.Custom: return customDirection.sqrMagnitude > 0f
                ? customDirection.normalized : Vector3.right;
            default: return Vector3.right;
        }
    }

    void InitializeRoadPath()
    {
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
        Vector3 travelTangent = reverseRoadPointOrder ? -tangent : tangent;
        Vector3 lateral = Vector3.Cross(Vector3.up, travelTangent).normalized;
        lateralOffset = Vector3.Dot(transform.position - nearest, lateral);
        verticalOffset = transform.position.y - nearest.y;
        pathReady = true;
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

    void FollowRoadPath(float deltaDistance)
    {
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
        float visualWheelAngle = ApplyCurveSteering(lookAheadDistance, travelTangent, directionSign);
        Vector3 lateral = Vector3.Cross(Vector3.up, travelTangent).normalized;
        transform.position = center + lateral * lateralOffset + Vector3.up * verticalOffset;
        if (travelTangent.sqrMagnitude > 0.0001f)
        {
            float targetYaw = Quaternion.LookRotation(travelTangent, Vector3.up).eulerAngles.y;
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
        cruiseMotion.manualSteeringWeight = 1f;
        cruiseMotion.steeringWheelAngle = cruiseMotion.wheelAngleAt360 > 0.0001f
            ? wheelAngle / cruiseMotion.wheelAngleAt360 * 360f
            : 0f;
        return wheelAngle;
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
