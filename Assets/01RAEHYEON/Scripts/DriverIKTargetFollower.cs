using UnityEngine;

/// <summary>
/// Keeps an IK target aligned to a cockpit control while preserving the pose
/// that was manually adjusted at the moment the source is assigned.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class DriverIKTargetFollower : MonoBehaviour
{
    [SerializeField] private Transform source;
    [SerializeField] private bool followPosition = true;
    [SerializeField] private bool followRotation = true;
    [SerializeField] private bool useStoredOffset;
    [SerializeField, HideInInspector] private bool hasStoredOffset;
    [SerializeField, HideInInspector] private Transform storedSource;
    [SerializeField, HideInInspector] private Vector3 storedPositionOffset;
    [SerializeField, HideInInspector] private Quaternion storedRotationOffset = Quaternion.identity;

    private Transform capturedSource;
    private Vector3 positionOffset;
    private Quaternion rotationOffset;

    private void OnEnable()
    {
        if (useStoredOffset)
        {
            capturedSource = storedSource;
        }
        else
        {
            CaptureOffset();
        }
    }

    private void Update()
    {
        FollowNow();
    }

    // A late procedural wheel motion may refresh hands before the manual rig solve.
    public void FollowNow()
    {
        // Manual target calibration is an editor-owned value.  Never write to
        // this Transform unless the scene is actively playing.
        if (!Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        if (source == null)
        {
            capturedSource = null;
            return;
        }

        if (capturedSource != source)
        {
            if (useStoredOffset)
            {
                // A locked calibration belongs to its original source.
                return;
            }

            CaptureOffset();
        }

        if (useStoredOffset && !hasStoredOffset)
        {
            // Do not jump to an uncalibrated source pose.
            return;
        }

        var activePositionOffset = useStoredOffset ? storedPositionOffset : positionOffset;
        var activeRotationOffset = useStoredOffset ? storedRotationOffset : rotationOffset;

        var position = followPosition
            ? source.TransformPoint(activePositionOffset)
            : transform.position;
        var rotation = followRotation
            ? source.rotation * activeRotationOffset
            : transform.rotation;

        transform.SetPositionAndRotation(position, rotation);
    }

    [ContextMenu("Capture Current Offset")]
    public void CaptureOffset()
    {
        capturedSource = source;
        if (source == null)
        {
            return;
        }

        positionOffset = source.InverseTransformPoint(transform.position);
        rotationOffset = Quaternion.Inverse(source.rotation) * transform.rotation;
    }

    [ContextMenu("Lock Current Offset For Future Plays")]
    private void LockCurrentOffset()
    {
        if (Application.isPlaying || source == null)
        {
            return;
        }

        storedPositionOffset = source.InverseTransformPoint(transform.position);
        storedRotationOffset = Quaternion.Inverse(source.rotation) * transform.rotation;
        hasStoredOffset = true;
        useStoredOffset = true;
        storedSource = source;
        capturedSource = storedSource;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
