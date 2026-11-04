using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

/// <summary>Connects the existing V2 gear and button Timeline curves to the internal hand target.</summary>
[DefaultExecutionOrder(1500)]
[DisallowMultipleComponent]
public sealed class V2ButtonHandDriver : MonoBehaviour
{
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private Transform steeringSource;
    [SerializeField] private Transform shifterSource;
    [SerializeField, Range(0f, 1f)] private float shifterBlend;
    [SerializeField] private Transform buttonSource;
    [SerializeField, Range(0f, 1f)] private float buttonBlend;

    private Vector3 steeringPositionOffset;
    private Quaternion steeringRotationOffset;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Animator driverAnimator;
    private AnimatorCullingMode originalCulling;
    private bool originalBuilderEnabled;
    private bool ownsRig;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (rigBuilder == null || steeringSource == null || buttonSource == null)
        {
            Debug.LogError("V2 button hand: assign the driver RigBuilder, steering source and button target.", this);
            return;
        }
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        steeringPositionOffset = steeringSource.InverseTransformPoint(transform.position);
        steeringRotationOffset = Quaternion.Inverse(steeringSource.rotation) * transform.rotation;
        driverAnimator = rigBuilder.GetComponent<Animator>();
        if (driverAnimator != null)
        {
            originalCulling = driverAnimator.cullingMode;
            driverAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        originalBuilderEnabled = rigBuilder.enabled;
        rigBuilder.enabled = false;
        ownsRig = true;
        if (!rigBuilder.Build())
        {
            Debug.LogError("V2 button hand: failed to build the driver rig.", this);
            ReleaseRig();
            return;
        }
        rigBuilder.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !ownsRig || !rigBuilder.graph.IsValid()) return;
        // Read this frame's Timeline weights and moving contact targets.
        // Zero weights retain the user's calibrated steering position and wrist.
        Vector3 position = steeringSource.TransformPoint(steeringPositionOffset);
        Quaternion rotation = steeringSource.rotation * steeringRotationOffset;
        if (shifterSource != null)
        {
            float gearAmount = Mathf.Clamp01(shifterBlend);
            position = Vector3.Lerp(position, shifterSource.position, gearAmount);
            rotation = Quaternion.Slerp(rotation, shifterSource.rotation, gearAmount);
        }
        // Preserve the existing button behavior. If both actions are deliberately
        // blended, the button layer is applied last; schedule separate Directors
        // sequentially for independent gear and button shots.
        float buttonAmount = Mathf.Clamp01(buttonBlend);
        transform.SetPositionAndRotation(
            Vector3.Lerp(position, buttonSource.position, buttonAmount),
            Quaternion.Slerp(rotation, buttonSource.rotation, buttonAmount));
        rigBuilder.Evaluate(Time.deltaTime);
    }

    private void OnDisable() => ReleaseRig();

    private void ReleaseRig()
    {
        if (!ownsRig) return;
        ownsRig = false;
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        if (driverAnimator != null) driverAnimator.cullingMode = originalCulling;
        if (rigBuilder != null)
        {
            rigBuilder.Clear();
            rigBuilder.enabled = originalBuilderEnabled;
        }
    }
}
