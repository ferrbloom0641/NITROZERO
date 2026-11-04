using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public sealed class RaceCinematicShot : MonoBehaviour
{
    public enum ShotKind { PackFront, PairRear, SideDolly, TracksidePan, RedBoosterRear, OverheadCheckpoint }
    public ShotKind kind;
    public CinemachineCamera cameraRig;
    [Tooltip("Camera offset in metres: lateral, height above road, forward.")]
    public Vector3 offset;
    [Min(.1f)] public float aimHeight = 2.5f;
    [Min(.05f)] public float rotationResponse = .18f;
    [Min(1)] public float dollyDuration = 8;
    public float dollyStart = -18;
    public float dollyEnd = 18;
    [Tooltip("Metres along the road. The pan camera stays at this station.")]
    public float panRoadDistance = 2850;
    [Tooltip("Fixed checkpoint framing: metres ahead of the camera station to look at.")]
    public float checkpointLookAhead = 10;
}
