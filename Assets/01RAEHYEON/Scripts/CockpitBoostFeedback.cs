using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Additive boost steering, evaluated before the existing V1/V2 hand rig solve.</summary>
[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
public sealed class CockpitBoostFeedback : MonoBehaviour
{
    [Header("Connections")]
    [SerializeField] private Transform steeringPivot;
    [SerializeField] private Vector3 steeringAxis = Vector3.up;
    [SerializeField] private DriverIKTargetFollower[] lateHandFollowers = new DriverIKTargetFollower[0];

    [Header("Playback")]
    [Tooltip("Play mode: press Space once to start; press again to restart the impact.")]
    [SerializeField] private bool spaceKeyEnabled = true;
    [Tooltip("Total seconds, including release. 0 sustains until Stop Boost is called.")]
    [SerializeField, Min(0f)] private float duration = 6f;
    [SerializeField, Min(0.05f)] private float convergenceTime = 2.2f;
    [SerializeField, Min(0.05f)] private float releaseTime = 0.4f;
    [SerializeField, Range(0f, 3f)] private float intensity = 1f;
    [SerializeField] private float seed = 1f;

    [Header("Steering - kick and delayed countersteer (degrees)")]
    [SerializeField, Min(0f)] private float initialSteeringAngle = 24f;
    [SerializeField, Min(0f)] private float sustainedSteeringAngle = 9f;
    [SerializeField, Min(0f)] private float steeringTremor = 1.5f;

    [Header("Camera - broad impact to tight vibration")]
    [SerializeField, Min(0f)] private float initialPositionAmplitude = 0.09f;
    [SerializeField, Min(0f)] private float sustainedPositionAmplitude = 0.006f;
    [SerializeField, Min(0f)] private float initialRotationAmplitude = 4.5f;
    [SerializeField, Min(0f)] private float sustainedRotationAmplitude = 0.55f;
    [SerializeField, Range(1f, 25f)] private float initialFrequency = 3.5f;
    [SerializeField, Range(1f, 25f)] private float sustainedFrequency = 21f;

    private bool playing, stopping, hasOutput;
    private double startTime, stopTime;
    private float stopWeight;
    private Quaternion lastOffset = Quaternion.identity, lastOutput, lastBase;
    private Transform appliedPivot;

    public bool IsPlaying => isActiveAndEnabled && playing && WeightAt(Elapsed) > 0f;
    private float Elapsed => (float)(Time.timeAsDouble - startTime);
    public float ActiveWeight => isActiveAndEnabled && playing ? WeightAt(Elapsed) * Mathf.Clamp01(intensity) : 0f;

    private void Update()
    {
        // Remove our previous layer before SteeringIdleMotion (LateUpdate 500).
        RemoveSteeringOffset();
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetKeyDown(KeyCode.Space);
#endif
        if (spaceKeyEnabled && pressed) PlayBoost();
        if (playing && WeightAt(Elapsed) <= 0f && Elapsed > 0.05f) playing = false;
    }

    [ContextMenu("Play Boost (Play Mode)")]
    public void PlayBoost()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        startTime = Time.timeAsDouble;
        playing = true;
        stopping = false;
    }

    [ContextMenu("Stop Boost (Play Mode)")]
    public void StopBoost()
    {
        if (!playing || stopping) return;
        stopWeight = WeightAt(Elapsed);
        stopTime = Time.timeAsDouble;
        stopping = true;
    }

    private float WeightAt(float t)
    {
        float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.065f));
        float release = Mathf.Max(0.05f, releaseTime);
        float end = duration > 0f ? Mathf.Clamp01((duration - t) / Mathf.Min(release, duration)) : 1f;
        float weight = attack * Mathf.SmoothStep(0f, 1f, end);
        if (stopping)
            weight = Mathf.Min(weight, stopWeight * (1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((float)(Time.timeAsDouble - stopTime) / release))));
        return weight;
    }

    // Absolute-time sampling keeps all cameras on the same phase when shots change.
    public void Sample(float elapsed, out float steering, out Vector3 cameraPosition, out Vector3 cameraEuler)
    {
        float t = Mathf.Max(0f, elapsed);
        float converge = Mathf.Max(0.05f, convergenceTime);
        float u = Mathf.Clamp01(t / converge);
        float settled = Mathf.SmoothStep(0f, 1f, u);
        float weight = WeightAt(t) * Mathf.Max(0f, intensity);
        float s = seed * 0.73f;

        // Each uneven kick is followed by a smaller opposite correction and recoil.
        float cycle = t * 2.6f;
        int index = Mathf.FloorToInt(cycle);
        float p = cycle - index;
        float direction = (index % 2 == 0 ? 1f : -1f);
        float variation = 0.65f + 0.35f * Mathf.PerlinNoise(index * 1.37f + 0.13f, s + 0.41f);
        float kick = p < 0.28f ? Mathf.Sin(p / 0.28f * Mathf.PI) :
            p < 0.78f ? -0.72f * Mathf.Sin((p - 0.28f) / 0.5f * Mathf.PI) :
            0.23f * Mathf.Sin((p - 0.78f) / 0.22f * Mathf.PI);
        float angle = Mathf.Lerp(initialSteeringAngle, sustainedSteeringAngle, settled);
        steering = weight * (angle * direction * variation * kick + steeringTremor *
            (0.65f * Mathf.Sin(t * 2f * Mathf.PI * 17f) + 0.35f * Mathf.Sin(t * 2f * Mathf.PI * 23f)));

        // Integral of a linear frequency ramp: continuous phase, no jump at convergence.
        float rampTime = Mathf.Min(t, converge);
        float cycles = initialFrequency * rampTime + (sustainedFrequency - initialFrequency) *
            rampTime * rampTime / (2f * converge) + sustainedFrequency * Mathf.Max(0f, t - converge);
        float phase = cycles * 2f * Mathf.PI;
        Vector3 wave = new Vector3(Wave(phase, s), Wave(phase * 1.13f, s + 2.1f), Wave(phase * 0.91f, s + 4.3f));
        Vector3 rot = new Vector3(Wave(phase * 0.97f, s + 1.2f), Wave(phase * 1.07f, s + 3.5f), Wave(phase, s + 5.7f));
        cameraPosition = Vector3.Scale(wave, new Vector3(0.65f, 1f, 0.4f)) *
            (Mathf.Lerp(initialPositionAmplitude, sustainedPositionAmplitude, settled) * weight);
        cameraEuler = Vector3.Scale(rot, new Vector3(0.8f, 0.5f, 1f)) *
            (Mathf.Lerp(initialRotationAmplitude, sustainedRotationAmplitude, settled) * weight);
        float ignition = Mathf.Exp(-t * 12f) * Mathf.Sin(t * 18f) * weight;
        cameraPosition.z -= 0.12f * ignition;
        cameraEuler.x += 3f * ignition;
    }

    private static float Wave(float phase, float offset) =>
        0.72f * Mathf.Sin(phase + offset) + 0.28f * Mathf.Sin(phase * 1.37f + offset * 2.3f);

    public void SampleCamera(out Vector3 position, out Vector3 euler)
    {
        position = euler = Vector3.zero;
        if (Application.isPlaying && isActiveAndEnabled && playing)
            Sample(Elapsed, out _, out position, out euler);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (playing && steeringPivot != null && steeringAxis.sqrMagnitude > 0.000001f)
        {
            Sample(Elapsed, out float angle, out _, out _);
            lastOffset = Quaternion.AngleAxis(angle, steeringAxis.normalized);
            appliedPivot = steeringPivot;
            lastBase = steeringPivot.localRotation;
            lastOutput = lastBase * lastOffset;
            steeringPivot.localRotation = lastOutput;
            hasOutput = true;
        }
        // V2's left hand normally updates early. Refresh it after the wheel, before rig 1500.
        foreach (var follower in lateHandFollowers)
            if (follower != null) follower.FollowNow();
    }

    private void RemoveSteeringOffset()
    {
        if (hasOutput && appliedPivot != null && Quaternion.Angle(appliedPivot.localRotation, lastOutput) < 0.05f)
            appliedPivot.localRotation = lastBase;
        hasOutput = false;
    }

    private void OnDisable()
    {
        RemoveSteeringOffset();
        playing = stopping = false;
    }
}
