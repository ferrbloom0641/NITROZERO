using UnityEngine;

[ExecuteAlways]
public class BulletOrbitCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] private float distance = 2.5f;
    [SerializeField] private float height = 0.5f;

    [SerializeField] private float startAngle = 0f;
    [SerializeField] private float endAngle = 180f;

    [Header("Preview / Timeline")]
    [Range(0f, 1f)]
    [SerializeField] private float orbitProgress = 0f;

    [Header("Auto Play")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private float duration = 1.5f;

    [Header("Look At")]
    [SerializeField] private bool lookAtTarget = true;
    [SerializeField] private Vector3 lookAtOffset = Vector3.zero;

    private bool isPlaying = false;

    public float OrbitProgress
    {
        get => orbitProgress;

        set
        {
            orbitProgress = Mathf.Clamp01(value);
            UpdateCameraPosition();
        }
    }

    private void Start()
    {
        UpdateCameraPosition();

        if (Application.isPlaying && playOnStart)
        {
            isPlaying = true;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        // 실제 게임 재생 중일 때만 자동 진행
        if (Application.isPlaying && isPlaying)
        {
            if (duration <= 0f)
            {
                orbitProgress = 1f;
                isPlaying = false;
            }
            else
            {
                orbitProgress += Time.deltaTime / duration;

                if (orbitProgress >= 1f)
                {
                    orbitProgress = 1f;
                    isPlaying = false;
                }
            }
        }

        // Edit Mode에서도 계속 위치 갱신
        UpdateCameraPosition();
    }

    private void OnValidate()
    {
        orbitProgress = Mathf.Clamp01(orbitProgress);
        distance = Mathf.Max(0f, distance);

        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        if (target == null)
            return;

        float angle = Mathf.Lerp(
            startAngle,
            endAngle,
            orbitProgress
        );

        float radians = angle * Mathf.Deg2Rad;

        Vector3 forward = target.forward;
        Vector3 right = target.right;
        Vector3 up = target.up;

        Vector3 orbitOffset =
            (-forward * Mathf.Cos(radians)
            + right * Mathf.Sin(radians))
            * distance;

        orbitOffset += up * height;

        // 이 스크립트가 붙은 오브젝트 이동
        transform.position =
            target.position + orbitOffset;

        if (lookAtTarget)
        {
            Vector3 lookPosition =
                target.position + lookAtOffset;

            Vector3 direction =
                lookPosition - transform.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        direction.normalized,
                        up
                    );
            }
        }
    }

    // =========================
    // Controls
    // =========================

    public void Play()
    {
        isPlaying = true;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void Stop()
    {
        isPlaying = false;
        OrbitProgress = 0f;
    }

    public void Restart()
    {
        orbitProgress = 0f;
        isPlaying = true;
        UpdateCameraPosition();
    }

    public void SetProgress(float value)
    {
        OrbitProgress = value;
    }

    public void SetDistance(float value)
    {
        distance = Mathf.Max(0f, value);
        UpdateCameraPosition();
    }

    public void SetHeight(float value)
    {
        height = value;
        UpdateCameraPosition();
    }
}