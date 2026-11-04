using UnityEngine;
using Unity.Cinemachine;

// Runs after the race director. Capture outputs have no Brain and cannot compete for shots.
[DefaultExecutionOrder(1200)]
public sealed class RaceMultiCameraCapture : MonoBehaviour
{
    public RaceCinematicDirector director;
    public Camera[] outputs;
    public void Synchronize()
    {
        if (director == null || outputs == null) return;
        for (int i=0;i<outputs.Length;i++)
        {
            var shot=director.shots[i];var camera=outputs[i];
            if(shot==null || shot.cameraRig==null || camera==null)continue;
            shot.cameraRig.UpdateCameraState(Vector3.up,Time.deltaTime);
            var state=shot.cameraRig.State;
            camera.transform.SetPositionAndRotation(state.GetFinalPosition(),state.GetFinalOrientation());
            camera.fieldOfView=state.Lens.FieldOfView;
            camera.nearClipPlane=state.Lens.NearClipPlane;
            camera.farClipPlane=state.Lens.FarClipPlane;
            camera.orthographic=state.Lens.Orthographic;
            camera.orthographicSize=state.Lens.OrthographicSize;
        }
    }
    void LateUpdate(){Synchronize();}
}
