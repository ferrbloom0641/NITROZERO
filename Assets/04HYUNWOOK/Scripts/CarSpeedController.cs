using UnityEngine;

// 이동 방향 축 선택
public enum MoveAxis
{
    Z_Forward,  // Z축 (기본 전방)
    X_Right,    // X축 (우측)
    Y_Up,       // Y축 (위)
    Custom      // 수동 Vector3 지정
}

// 기준 좌표계 선택
public enum CoordinateSpace
{
    Local, // 차량 자체 회전값 기준
    World  // 월드 절대 좌표 기준
}

public class CarSpeedController : MonoBehaviour
{
    [Header("0. 사용 범위")]
    [Tooltip("활성화하면 지정한 카메라 번호부터 기존 돔 탈출용 이동을 정지합니다.")]
    [SerializeField] private bool thirdPersonTrailerOnly = false;
    [SerializeField] private CutsceneManager cutsceneManager;
    [SerializeField] private CutsceneCameraType firstPersonCamera = CutsceneCameraType.Camera6;

    [Header("1. 속도 및 가속도 설정")]
    [Tooltip("중간 주행 장면이 시작될 때 이미 유지하고 있는 속도 (km/h)")]
    [Min(0f)] public float initialSpeedKmh = 0f;

    [Tooltip("목표 최고 속도 (km/h)")]
    public float targetSpeedKmh = 300f;

    [Tooltip("가속도 (초당 증가할 km/h). 0으로 설정 시 즉시 최고 속도로 출발합니다.")]
    public float accelerationKmh = 200f;

    [Header("2. 출발 타이밍 설정")]
    [Tooltip("게임 시작 후 몇 초 뒤에 출발할지 지정합니다.")]
    public float startDelay = 0f;

    [Header("3. 부스터 / 역전 연출 (선택)")]
    [Tooltip("주행 중 부스터(추가 가속) 사용 여부")]
    public bool useBoost = false;

    [Tooltip("출발 후 몇 초 뒤에 부스터가 터질지 설정")]
    public float boostDelaySeconds = 1.5f;

    [Tooltip("부스터 발동 시 최고 속도 배율 (예: 1.3 = 390km/h)")]
    public float boostMultiplier = 1.3f;

    [Header("4. 고속 주행 좌우 흔들림 연출 (선택)")]
    [Tooltip("고속 주행 중 미세한 좌우 흔들림(Wobble) 적용")]
    public bool enableWobble = false;

    [Tooltip("좌우 흔들림 폭")]
    public float wobbleAmount = 0.05f;

    [Tooltip("좌우 흔들림 속도")]
    public float wobbleSpeed = 15f;

    [Header("5. 이동 축 및 좌표계 설정")]
    public MoveAxis moveAxis = MoveAxis.Z_Forward;
    public CoordinateSpace space = CoordinateSpace.Local;

    [Header("커스텀 방향 (moveAxis가 Custom일 때 사용)")]
    public Vector3 customDirection = new Vector3(0, 0, 1);

    // 내부 상태 변수
    private float currentSpeedKmh = 0f;
    private float elapsedTime = 0f;
    public float CurrentSpeedKmh => currentSpeedKmh;
    public int CurrentGear { get; private set; } = 1;

    private void Awake()
    {
        if (thirdPersonTrailerOnly && cutsceneManager == null)
            cutsceneManager = FindFirstObjectByType<CutsceneManager>();
        currentSpeedKmh = Mathf.Clamp(initialSpeedKmh, 0f, Mathf.Max(0f, targetSpeedKmh));
        UpdateAutomaticGear();
    }

    void Update()
    {
        if (thirdPersonTrailerOnly && cutsceneManager != null &&
            cutsceneManager.activeCamera >= firstPersonCamera)
        {
            currentSpeedKmh = 0f;
            return;
        }

        elapsedTime += Time.deltaTime;

        // 1. 출발 딜레이 체크
        if (elapsedTime < startDelay) return;

        float activeDriveTime = elapsedTime - startDelay;

        // 2. 부스터 속도 적용 여부
        float finalTargetSpeed = targetSpeedKmh;
        if (useBoost && activeDriveTime >= boostDelaySeconds)
        {
            finalTargetSpeed *= boostMultiplier;
        }

        // 3. 가속도 처리
        if (accelerationKmh > 0)
        {
            currentSpeedKmh = Mathf.MoveTowards(currentSpeedKmh, finalTargetSpeed, accelerationKmh * Time.deltaTime);
        }
        else
        {
            currentSpeedKmh = finalTargetSpeed;
        }
        UpdateAutomaticGear();

        // km/h -> m/s 변환
        float currentSpeedMps = currentSpeedKmh / 3.6f;

        // 4. 방향 벡터 계산
        Vector3 moveDir = GetSelectedDirection();

        // 5. 좌우 흔들림 오프셋 추가
        if (enableWobble && currentSpeedKmh > 50f)
        {
            float wobbleOffset = Mathf.Sin(Time.time * wobbleSpeed) * wobbleAmount;
            moveDir += Vector3.right * wobbleOffset;
        }

        // 6. 이동 적용
        if (space == CoordinateSpace.Local)
        {
            transform.Translate(moveDir * currentSpeedMps * Time.deltaTime, Space.Self);
        }
        else
        {
            transform.Translate(moveDir * currentSpeedMps * Time.deltaTime, Space.World);
        }
    }

    private void UpdateAutomaticGear()
    {
        CurrentGear = currentSpeedKmh <= 0.01f
            ? 1
            : Mathf.Clamp(Mathf.CeilToInt(currentSpeedKmh / 50f), 1, 6);
    }

    private Vector3 GetSelectedDirection()
    {
        switch (moveAxis)
        {
            case MoveAxis.Z_Forward: return Vector3.forward;
            case MoveAxis.X_Right: return Vector3.right;
            case MoveAxis.Y_Up: return Vector3.up;
            case MoveAxis.Custom: return customDirection.normalized;
            default: return Vector3.forward;
        }
    }
}
