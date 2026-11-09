using UnityEngine;
using Unity.Cinemachine;

/// <summary>Adds road vibration to the final camera state without changing the authored pose or FOV.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Racing/주행 카메라 미세 떨림")]
public sealed class RacingRoadCameraShake : CinemachineExtension
{
    [SerializeField] private RacingHudController hudController;
    [SerializeField] private MidRaceSpeedController speedController;

    [Header("화면 떨림")]
    [InspectorName("전체 떨림 강도")]
    [Range(0f, 10f)] [SerializeField] private float intensity = 1f;
    [Header("1인칭 강도 변화")]
    [InspectorName("약한 떨림 기준 속도")]
    [Min(0f)] [SerializeField] private float lowSpeedKmh = 100f;
    [InspectorName("부스트 사용 시 강도 배율")]
    [Range(1f, 3f)] [SerializeField] private float boostMultiplier = 1.3f;
    [InspectorName("떨림 강도 전환 시간")]
    [Min(.01f)] [SerializeField] private float responseSeconds = .15f;
    [InspectorName("1인칭 떨림 증가 단계 수")]
    [Range(2, 12)] [SerializeField] private int firstPersonIntensitySteps = 6;
    [InspectorName("1인칭 시작 강도 (최대 강도 대비)")]
    [Range(0f, 1f)] [SerializeField] private float firstPersonStartingStrength = .5f;

    [Header("미세 진동 설정")]
    [InspectorName("위치 떨림 크기 (미터)")]
    [SerializeField] private Vector3 positionAmplitude = new Vector3(.004f, .008f, .001f);
    [InspectorName("회전 떨림 크기 (도)")]
    [SerializeField] private Vector3 rotationAmplitude = new Vector3(.12f, .07f, .06f);
    [InspectorName("진동 속도")]
    [Range(1f, 20f)] [SerializeField] private float frequency = 9f;

    float noiseTime, strength;
    int updatedFrame = -1;

    protected override void OnEnable()
    {
        base.OnEnable();
        ResetMotion();
    }

    void ResetMotion()
    {
        noiseTime = strength = 0f;
        updatedFrame = -1;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Noise || !Application.isPlaying) return;
        if (deltaTime < 0f || (hudController != null && !hudController.IsHudVisible))
        {
            ResetMotion();
            return;
        }

        bool firstPerson = hudController != null && hudController.IsFirstPersonHud;
        if (updatedFrame != Time.frameCount)
        {
            bool firstFrame = updatedFrame < 0;
            updatedFrame = Time.frameCount;
            float dt = Mathf.Max(0f, deltaTime) * (hudController != null ? hudController.CinematicTimeScale : 1f);
            float speed = hudController != null ? hudController.CurrentSpeedKmh
                : speedController != null ? speedController.CurrentSpeedKmh : 0f;
            float frequencyRatio = 1f;
            if (firstPerson)
            {
                // Equally timed, equal-sized steps avoid stacking the speed easing
                // and boost multiplier into a steep jump in the middle of the shot.
                float progress = hudController.CinematicSequenceProgress;
                int steps = Mathf.Max(2, firstPersonIntensitySteps);
                int step = Mathf.Min(steps - 1, Mathf.FloorToInt(progress * steps));
                float peak = hudController.IsBoostConsuming ? boostMultiplier : 1f;
                float target = Mathf.Lerp(peak * firstPersonStartingStrength, peak, step / (float)(steps - 1));
                target *= Mathf.Clamp01(speed / Mathf.Max(1f, lowSpeedKmh));
                if (firstFrame) strength = target;
                strength = Mathf.Lerp(strength, target, 1f - Mathf.Exp(-dt / Mathf.Max(.01f, responseSeconds)));
                frequencyRatio = progress;
            }
            else
            {
                // Third person uses the Inspector intensity immediately and throughout the shot.
                strength = 1f;
            }
            noiseTime += dt * frequency * Mathf.Lerp(.7f, 1f, frequencyRatio);
        }

        float amount = intensity * strength;
        float Noise(float seed) => (Mathf.PerlinNoise(noiseTime, seed) - .5f) * 2f;
        Vector3 position = Vector3.Scale(positionAmplitude, new Vector3(Noise(11.3f), Noise(29.7f), Noise(43.1f))) * amount;
        Vector3 rotation = Vector3.Scale(rotationAmplitude, new Vector3(Noise(67.2f), Noise(83.5f), Noise(97.1f))) * amount;
        state.PositionCorrection += state.GetCorrectedOrientation() * position;
        state.OrientationCorrection *= Quaternion.Euler(rotation);
    }
}
