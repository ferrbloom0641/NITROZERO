using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Adds a small engine-idle vibration to a Cinemachine camera.
///
/// This runs as a Cinemachine extension rather than a plain MonoBehaviour: Cinemachine
/// writes the camera transform every frame, so shaking the Transform directly would be
/// overwritten. Here the shake is added at the Finalize stage, after everything else.
///
/// Two layers are summed:
///   - Engine:  high frequency, tiny amplitude. The buzz you feel through the seat.
///   - Chassis: low frequency, slightly larger. The body rocking on its suspension.
/// A single frequency alone reads as a mechanical buzz, so the slow layer is what makes
/// it feel like an engine instead of a vibrating prop.
///
/// Rotation is what the eye actually notices; position does very little on its own.
/// Defaults are tuned for this project's cars (root scale ~3.6).
/// </summary>
[AddComponentMenu("Cinemachine/Procedural/Extensions/Cinemachine Engine Vibration")]
[SaveDuringPlay]
[ExecuteAlways]
[DisallowMultipleComponent]
public class CinemachineEngineVibration : CinemachineExtension
{
    [Tooltip("Master multiplier. 0 turns the vibration off, 1 is the tuned default. " +
             "Raise this for a rougher engine, lower it for a smooth idle.")]
    [Range(0f, 3f)]
    public float Intensity = 1f;

    [Header("Engine — high frequency buzz")]
    [Tooltip("Cycles per second. A real idle sits near 20-25Hz. Going much above 25 " +
             "starts to alias against the frame rate and looks like noise.")]
    [Range(1f, 35f)]
    public float EngineFrequency = 22f;

    [Tooltip("Positional shake in world units.")]
    [Range(0f, 0.2f)]
    public float EnginePositionAmplitude = 0.012f;

    [Tooltip("Rotational shake in degrees. This is what you actually see.")]
    [Range(0f, 1f)]
    public float EngineRotationAmplitude = 0.055f;

    [Header("Chassis — slow body movement")]
    [Tooltip("Cycles per second for the slower body rock.")]
    [Range(0.1f, 12f)]
    public float ChassisFrequency = 3.4f;

    [Range(0f, 0.3f)]
    public float ChassisPositionAmplitude = 0.022f;

    [Range(0f, 1f)]
    public float ChassisRotationAmplitude = 0.045f;

    [Header("Axis weighting")]
    [Tooltip("Per-axis scaling of the positional shake, in camera-local space " +
             "(x = side to side, y = up/down, z = forward/back). Engines shake mostly vertically.")]
    public Vector3 PositionWeight = new Vector3(0.5f, 1f, 0.35f);

    [Tooltip("Per-axis scaling of the rotational shake (x = pitch, y = yaw, z = roll).")]
    public Vector3 RotationWeight = new Vector3(1f, 0.45f, 0.7f);

    [Header("Misc")]
    [Tooltip("Give each camera a different seed so several shots do not shake in lockstep.")]
    public float Seed = 0f;

    [Tooltip("Also shake while the editor is not playing. Off by default so it does not " +
             "fight you while framing shots.")]
    public bool PreviewInEditMode = false;

    // Perlin noise is sampled on separate rows so the axes stay uncorrelated.
    // Mathf.PerlinNoise returns 0..1 and is 0.5 along integer rows, hence the offsets.
    static float Noise(float row, float t)
    {
        return Mathf.PerlinNoise(row, t) * 2f - 1f;
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize)
            return;
        if (Intensity <= 0f)
            return;
        if (!Application.isPlaying && !PreviewInEditMode)
            return;

        float t = CinemachineCore.CurrentTime;
        // Let boost converge to its tighter center, then smoothly restore engine idle.
        var boost = GetComponent<CinemachineBoostFeedback>();
        float idleWeight = boost != null ? 1f - boost.ActiveWeight : 1f;
        float s = Seed * 37.13f;

        float te = t * EngineFrequency;
        float tc = t * ChassisFrequency;

        Vector3 enginePos = new Vector3(
            Noise(s + 0.17f, te),
            Noise(s + 3.41f, te),
            Noise(s + 7.63f, te));

        Vector3 chassisPos = new Vector3(
            Noise(s + 11.29f, tc),
            Noise(s + 15.87f, tc),
            Noise(s + 19.03f, tc));

        Vector3 engineRot = new Vector3(
            Noise(s + 23.71f, te),
            Noise(s + 27.19f, te),
            Noise(s + 31.57f, te));

        Vector3 chassisRot = new Vector3(
            Noise(s + 35.11f, tc),
            Noise(s + 39.83f, tc),
            Noise(s + 43.29f, tc));

        Vector3 posOffset = Vector3.Scale(
            enginePos * EnginePositionAmplitude + chassisPos * ChassisPositionAmplitude,
            PositionWeight) * Intensity;

        Vector3 rotOffset = Vector3.Scale(
            engineRot * EngineRotationAmplitude + chassisRot * ChassisRotationAmplitude,
            RotationWeight) * Intensity;

        // Offset along the camera's own axes, not world axes, so the shake reads the
        // same no matter which way the shot is pointing.
        state.PositionCorrection += state.RawOrientation * (posOffset * idleWeight);
        state.OrientationCorrection *= Quaternion.Euler(rotOffset * idleWeight);
    }
}
