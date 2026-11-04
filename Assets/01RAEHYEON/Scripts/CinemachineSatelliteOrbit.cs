using UnityEngine;

/// <summary>
/// 재생 시작 시 카메라가 바라보는 정면 방향으로 천천히 직선 이동합니다.
/// Cinemachine Camera의 부모 Rig에 붙여 사용합니다.
/// </summary>
public class CinemachineSatelliteOrbit : MonoBehaviour
{
    [Header("Forward Movement")]
    [Tooltip("정면으로 이동할 전체 거리")]
    [Min(0f)]
    [SerializeField] private float travelDistance = 30f;

    [Tooltip("전체 거리를 이동하는 시간. 값이 클수록 느립니다.")]
    [Min(0.01f)]
    [SerializeField] private float travelDuration = 40f;

    [Tooltip("처음과 끝에서 부드럽게 가속·감속")]
    [SerializeField] private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("One Direction Rotation")]
    [Tooltip("카메라 앞축 기준 초당 회전 각도. 음수는 반대 방향, 0은 회전 없음")]
    [SerializeField] private float rollSpeed = 0.25f;

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;

    private Vector3 startPosition;
    private Vector3 forwardDirection;
    private Quaternion startRotation;
    private float elapsed;
    private bool playing;
    private bool captured;

    private void Awake()
    {
        CaptureStart();
        playing = playOnStart;
    }

    private void LateUpdate()
    {
        if (!playing)
            return;

        if (!captured)
            CaptureStart();

        float duration = Mathf.Max(0.01f, travelDuration);
        elapsed = Mathf.Min(elapsed + Time.deltaTime, duration);
        float normalizedTime = elapsed / duration;
        float distanceT = movementCurve.Evaluate(normalizedTime);

        // 시작할 때 바라보던 정면 방향으로만 이동합니다.
        transform.position =
            startPosition + forwardDirection * (travelDistance * distanceT);

        // 왕복 흔들림 없이 앞축을 중심으로 일정한 속도로 한 방향 회전합니다.
        float roll = elapsed * rollSpeed;
        transform.rotation =
            startRotation * Quaternion.AngleAxis(roll, Vector3.forward);

        if (normalizedTime >= 1f)
            playing = false;
    }

    private void CaptureStart()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        forwardDirection = transform.forward.normalized;
        elapsed = 0f;
        captured = true;
    }

    public void Play()
    {
        if (!captured)
            CaptureStart();
        playing = true;
    }

    public void Pause() => playing = false;

    public void Restart()
    {
        CaptureStart();
        playing = true;
    }
}
