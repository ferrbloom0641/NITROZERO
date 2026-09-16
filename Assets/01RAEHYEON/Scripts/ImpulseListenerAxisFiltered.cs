using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Drop-in replacement for CinemachineImpulseListener that applies the impulse as
/// positional shake on chosen axes and discards the rotation.
///
/// The stock listener does this, with no way to opt out of the rotation:
///     state.PositionCorrection    += impulsePos;
///     state.OrientationCorrection *= impulseRot;
/// The rotation term is what makes a ground camera look like it is tilting rather
/// than being jolted, so here it is dropped entirely (or scaled down via RotationWeight).
///
/// Everything the Impulse system is good at still applies, because the signal is read
/// straight from CinemachineImpulseManager:
///   - distance falloff from each source's Dissipation settings
///   - several vehicles summing automatically
///
/// Vertical is the base motion. Lateral shake is folded in only as the signal gets
/// strong, so a distant vehicle gives a clean up-down tremble and a close pass starts
/// to throw the camera sideways as well.
///
/// Remove CinemachineImpulseListener from the camera when using this, or the two will
/// both apply and double up.
/// </summary>
[AddComponentMenu("NITROZERO/Impulse Listener (Axis Filtered)")]
[SaveDuringPlay]
[ExecuteAlways]
[DisallowMultipleComponent]
public class ImpulseListenerAxisFiltered : CinemachineExtension
{
    [Tooltip("Overall strength of the received impulse.")]
    [Range(0f, 5f)]
    public float Gain = 1f;

    [Tooltip("Impulse channels to listen on, as a bit mask. Must overlap the sources' " +
             "Impulse Channel. 1 is the default channel.")]
    public int ChannelMask = 1;

    [Tooltip("Ignore the Z axis when measuring distance to the source.")]
    public bool Use2DDistance;

    [Header("Vertical — the base shake")]
    [Tooltip("How much of the impulse becomes up/down movement.")]
    [Range(0f, 3f)]
    public float VerticalWeight = 1f;

    [Header("Lateral — added only as it gets strong")]
    [Tooltip("Maximum side-to-side movement, reached once the signal hits LateralFullAt.")]
    [Range(0f, 3f)]
    public float LateralWeight = 0.6f;

    [Tooltip("Signal strength at which lateral shake starts appearing at all. " +
             "Below this the camera moves purely up and down.")]
    [Min(0f)]
    public float LateralStartAt = 0.02f;

    [Tooltip("Signal strength at which lateral shake reaches LateralWeight.")]
    [Min(0.001f)]
    public float LateralFullAt = 0.12f;

    [Tooltip("Forward/back movement, relative to the lateral amount. Usually small.")]
    [Range(0f, 1f)]
    public float DepthWeight = 0.25f;

    [Header("Rotation")]
    [Tooltip("How much of the impulse's rotation to keep. 0 removes the tilting entirely, " +
             "which is the point of this component.")]
    [Range(0f, 1f)]
    public float RotationWeight = 0f;

    [Tooltip("Apply the shake along the camera's own axes instead of world axes. " +
             "Leave off so 'vertical' means world up, whatever way the camera points.")]
    public bool UseCameraSpace;

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || deltaTime < 0f)
            return;
        if (Gain <= 0f)
            return;

        if (!CinemachineImpulseManager.Instance.GetImpulseAt(
                state.GetFinalPosition(), Use2DDistance, ChannelMask,
                out var impulsePos, out var impulseRot))
            return;

        impulsePos *= Gain;

        // Strength of what arrived, used to decide how much lateral to mix in.
        float strength = impulsePos.magnitude;
        float t = Mathf.InverseLerp(LateralStartAt, LateralFullAt, strength);
        float lateral = LateralWeight * Mathf.Clamp01(t);

        var shaped = new Vector3(
            impulsePos.x * lateral,
            impulsePos.y * VerticalWeight,
            impulsePos.z * lateral * DepthWeight);

        if (UseCameraSpace)
            shaped = state.RawOrientation * shaped;

        state.PositionCorrection += shaped;

        if (RotationWeight > 0f)
        {
            var rot = Quaternion.SlerpUnclamped(Quaternion.identity, impulseRot, RotationWeight * Gain);
            state.OrientationCorrection = state.OrientationCorrection * rot;
        }
    }
}
