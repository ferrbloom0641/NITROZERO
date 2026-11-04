using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>Preserves the calibrated V1 grip and evaluates it after wheel motion.</summary>
[DefaultExecutionOrder(1500)]
[DisallowMultipleComponent]
public sealed class V1SteeringHandLock : MonoBehaviour
{
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private TwoBoneIKConstraint leftArm;
    [SerializeField] private TwoBoneIKConstraint rightArm;
    [SerializeField] private Transform leftSteeringSource;
    [SerializeField] private Transform rightSteeringSource;

    private Vector3 leftOffset, rightOffset;
    private Quaternion leftRotationOffset, rightRotationOffset;
    private Quaternion savedRightLocalRotation;
    private float savedRightRotationWeight;
    private bool savedBuilderEnabled;
    private bool ownsRig;

    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        if (rigBuilder == null || leftArm == null || rightArm == null ||
            leftSteeringSource == null || rightSteeringSource == null ||
            leftArm.data.target == null || rightArm.data.target == null || rightArm.data.tip == null)
        {
            Debug.LogError("V1 steering hand lock: missing rig, hand or steering reference.", this);
            return;
        }

        var left = leftArm.data.target;
        var right = rightArm.data.target;
        savedRightRotationWeight = rightArm.data.targetRotationWeight;
        savedRightLocalRotation = right.localRotation;
        // V1 previously ignored this target's rotation. Start from the existing
        // wrist orientation instead of snapping to the unused target rotation.
        if (savedRightRotationWeight <= 0f && !rightArm.data.maintainTargetRotationOffset)
            right.rotation = rightArm.data.tip.rotation;

        leftOffset = leftSteeringSource.InverseTransformPoint(left.position);
        rightOffset = rightSteeringSource.InverseTransformPoint(right.position);
        leftRotationOffset = Quaternion.Inverse(leftSteeringSource.rotation) * left.rotation;
        rightRotationOffset = Quaternion.Inverse(rightSteeringSource.rotation) * right.rotation;
        rightArm.data.targetRotationWeight = 1f;

        // Use the Animation Rigging package's manual-evaluation pattern.
        savedBuilderEnabled = rigBuilder.enabled;
        rigBuilder.enabled = false;
        ownsRig = true;
        if (!rigBuilder.Build())
        {
            Debug.LogError("V1 steering hand lock: RigBuilder could not build its graph.", this);
            ReleaseRig();
            return;
        }
        rigBuilder.graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !ownsRig || !rigBuilder.graph.IsValid()) return;
        leftArm.data.target.SetPositionAndRotation(
            leftSteeringSource.TransformPoint(leftOffset), leftSteeringSource.rotation * leftRotationOffset);
        rightArm.data.target.SetPositionAndRotation(
            rightSteeringSource.TransformPoint(rightOffset), rightSteeringSource.rotation * rightRotationOffset);
        rigBuilder.Evaluate(Time.deltaTime);
    }

    private void OnDisable() => ReleaseRig();

    private void ReleaseRig()
    {
        if (!ownsRig) return;
        ownsRig = false;
        if (rightArm != null)
        {
            rightArm.data.targetRotationWeight = savedRightRotationWeight;
            if (rightArm.data.target != null) rightArm.data.target.localRotation = savedRightLocalRotation;
        }
        if (rigBuilder != null)
        {
            rigBuilder.Clear();
            rigBuilder.enabled = savedBuilderEnabled;
        }
    }
}
