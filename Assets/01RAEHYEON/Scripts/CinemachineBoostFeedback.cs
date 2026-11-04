using UnityEngine;
using Unity.Cinemachine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Cinemachine/Procedural/Extensions/Cinemachine Boost Feedback")]
public sealed class CinemachineBoostFeedback : CinemachineExtension
{
    [SerializeField] private CockpitBoostFeedback source;
    [SerializeField, Range(0f, 2f)] private float shotIntensity = 1f;

    public float ActiveWeight => enabled && source != null ? source.ActiveWeight * Mathf.Clamp01(shotIntensity) : 0f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || !Application.isPlaying || source == null) return;
        source.SampleCamera(out var position, out var euler);
        state.PositionCorrection += state.RawOrientation * (position * shotIntensity);
        state.OrientationCorrection *= Quaternion.Euler(euler * shotIntensity);
    }
}
