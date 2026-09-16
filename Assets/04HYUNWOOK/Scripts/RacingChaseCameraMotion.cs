using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1500)]
public sealed class RacingChaseCameraMotion : MonoBehaviour
{
    [Header("Speed source")]
    [SerializeField] private MidRaceSpeedController speedController;
    [SerializeField] private CinemachineCamera virtualCamera;
    [Min(1f)] [SerializeField] private float fullEffectSpeedKmh = 300f;

    [Header("Intensity by speed")]
    [Min(0f)] [SerializeField] private float lowSpeedKmh = 100f;
    [Range(0f, 1f)] [SerializeField] private float lowSpeedStrength = 0.12f;
    [Min(0f)] [SerializeField] private float mediumSpeedKmh = 200f;
    [Range(0f, 1f)] [SerializeField] private float mediumSpeedStrength = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float highSpeedStrength = 1f;

    [Header("Vertical FOV by speed")]
    [Tooltip("최고 속도에서 사용할 Vertical FOV입니다. 카메라 위치는 바뀌지 않습니다.")]
    [Range(20f, 100f)] [SerializeField] private float maximumVerticalFov = 72f;

    [Header("High-speed camera motion")]
    [Range(0f, 1f)] [SerializeField] private float intensity = 0.65f;
    [Min(0f)] [SerializeField] private float lateralSway = 0.025f;
    [Min(0f)] [SerializeField] private float verticalBob = 0.012f;
    [Min(0f)] [SerializeField] private float rollDegrees = 0.35f;
    [Min(0f)] [SerializeField] private float pitchDegrees = 0.16f;
    [Min(0.01f)] [SerializeField] private float motionFrequency = 0.85f;
    [Min(0f)] [SerializeField] private float microVibration = 0.0025f;
    [Min(0.01f)] [SerializeField] private float vibrationFrequency = 8.5f;
    [Min(0f)] [SerializeField] private float fadeInSeconds = 0.35f;

    Vector3 baseLocalPosition;
    Quaternion baseLocalRotation;
    float baseVerticalFov;
    float enabledAt;

    void Awake()
    {
        if (speedController == null)
            speedController = GetComponentInParent<MidRaceSpeedController>();
        if (virtualCamera == null)
            virtualCamera = GetComponent<CinemachineCamera>();
        CacheBasePose();
    }

    void OnEnable()
    {
        CacheBasePose();
        enabledAt = Time.time;
    }

    void OnDisable()
    {
        transform.SetLocalPositionAndRotation(baseLocalPosition, baseLocalRotation);
        if (virtualCamera != null)
            virtualCamera.Lens.FieldOfView = baseVerticalFov;
    }

    void LateUpdate()
    {
        float speed = speedController != null ? speedController.CurrentSpeedKmh : fullEffectSpeedKmh;
        float speedStrength = EvaluateSpeedStrength(speed);
        float amount = speedStrength * intensity;
        float t = Time.time;
        float fade = 1f;
        if (fadeInSeconds > 0f)
            fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - enabledAt) / fadeInSeconds));
        amount *= fade;
        if (virtualCamera != null)
            virtualCamera.Lens.FieldOfView = Mathf.Lerp(
                baseVerticalFov, maximumVerticalFov, speedStrength * fade);

        float frequencyScale = Mathf.Lerp(0.55f, 1f,
            Mathf.Clamp01(speed / Mathf.Max(1f, fullEffectSpeedKmh)));
        float slowWave = Mathf.Sin(t * motionFrequency * frequencyScale * Mathf.PI * 2f);
        float secondWave = Mathf.Sin(t * motionFrequency * frequencyScale * 1.37f * Mathf.PI * 2f + 1.2f);
        float fastWave = Mathf.Sin(t * vibrationFrequency * frequencyScale * Mathf.PI * 2f + 0.45f);

        Vector3 positionOffset = new Vector3(
            slowWave * lateralSway * amount,
            (secondWave * verticalBob + fastWave * microVibration) * amount,
            0f);
        Quaternion rotationOffset = Quaternion.Euler(
            secondWave * pitchDegrees * amount,
            0f,
            -slowWave * rollDegrees * amount);

        transform.SetLocalPositionAndRotation(
            baseLocalPosition + positionOffset,
            baseLocalRotation * rotationOffset);
    }

    float EvaluateSpeedStrength(float speedKmh)
    {
        float low = Mathf.Max(0f, lowSpeedKmh);
        float medium = Mathf.Max(low + 0.01f, mediumSpeedKmh);
        float high = Mathf.Max(medium + 0.01f, fullEffectSpeedKmh);
        if (speedKmh <= low)
        {
            float t = low > 0f ? Mathf.Clamp01(speedKmh / low) : 1f;
            return Mathf.SmoothStep(0f, lowSpeedStrength, t);
        }
        if (speedKmh <= medium)
        {
            float t = Mathf.InverseLerp(low, medium, speedKmh);
            return Mathf.SmoothStep(lowSpeedStrength, mediumSpeedStrength, t);
        }

        float highT = Mathf.InverseLerp(medium, high, speedKmh);
        return Mathf.SmoothStep(mediumSpeedStrength, highSpeedStrength, highT);
    }

    void CacheBasePose()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        if (virtualCamera != null)
            baseVerticalFov = virtualCamera.Lens.FieldOfView;
    }
}
