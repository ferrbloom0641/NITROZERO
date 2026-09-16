using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[ExecuteAlways]
public class BulletSplineController : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Movement")]
    [Range(0f, 1f)]
    [SerializeField] private float progress = 0f;

    [SerializeField] private float speed = 0.3f;
    [SerializeField] private bool playOnStart = false;

    [Header("Bullet Rotation")]
    [SerializeField] private Transform bulletModel;
    [SerializeField] private float spinSpeed = 1500f;

    [Header("Options")]
    [SerializeField] private bool faceSplineDirection = true;
    [SerializeField] private bool stopAtEnd = true;

    private bool isPlaying;

    public float Progress
    {
        get => progress;
        set
        {
            progress = Mathf.Clamp01(value);
            UpdateBulletPosition();
        }
    }

    public float Speed
    {
        get => speed;
        set => speed = value;
    }

    private void Start()
    {
        UpdateBulletPosition();

        if (Application.isPlaying && playOnStart)
            isPlaying = true;
    }

    private void Update()
    {
        // 에디터에서도 Progress 변경 즉시 반영
        if (!Application.isPlaying)
        {
            UpdateBulletPosition();
            return;
        }

        if (isPlaying)
        {
            progress += speed * Time.deltaTime;

            if (progress >= 1f)
            {
                progress = 1f;

                if (stopAtEnd)
                    isPlaying = false;
            }

            UpdateBulletPosition();
        }

        if (isPlaying && bulletModel != null)
        {
            bulletModel.Rotate(
                Vector3.right, spinSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void OnValidate()
    {
        progress = Mathf.Clamp01(progress);

        if (!Application.isPlaying)
            UpdateBulletPosition();
    }

    private void UpdateBulletPosition()
    {
        if (splineContainer == null)
            return;

        // SplineContainer 기준으로 위치 / 방향 / Up 계산
        bool success = splineContainer.Evaluate(
            progress,
            out float3 position,
            out float3 tangent,
            out float3 upVector
        );

        if (!success)
            return;

        // Evaluate 결과를 Vector3로 변환
        Vector3 splinePosition =
            new Vector3(position.x, position.y, position.z);

        Vector3 splineTangent =
            new Vector3(tangent.x, tangent.y, tangent.z);

        Vector3 splineUp =
            new Vector3(upVector.x, upVector.y, upVector.z);

        // 총알 ROOT를 Spline 위치로 강제 이동
        transform.position = splinePosition;

        // 총알 진행방향
        if (faceSplineDirection &&
            splineTangent.sqrMagnitude > 0.0001f)
        {
            if (splineUp.sqrMagnitude < 0.0001f)
                splineUp = Vector3.up;

            transform.rotation = Quaternion.LookRotation(
                splineTangent.normalized,
                splineUp.normalized
            );
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
        Progress = 0f;
    }

    public void Restart()
    {
        progress = 0f;
        UpdateBulletPosition();
        isPlaying = true;
    }

    public void SetProgress(float value)
    {
        Progress = value;
    }

    public void SetSpeed(float value)
    {
        speed = value;
    }
}