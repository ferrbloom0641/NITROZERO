using UnityEngine;
using Unity.Cinemachine;

/// <summary>Camera-local Perlin layers, like CinemachineEngineVibration, with race/spatial inputs.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RaceCameraForces : CinemachineExtension
{
    public RaceCinematicDirector director;
    public RaceCinematicShot shot;
    [Range(0,2)] public float intensity=1;
    public float seed=1;
    [Header("Engine vibration (Hz / metres / degrees)")]
    [Range(1,25)] public float engineFrequency=17;
    [Range(0,.2f)] public float enginePosition=.008f;
    [Range(0,1)] public float engineRotation=.12f;
    [Header("Road vibration (cycles per metre / metres / degrees)")]
    [Range(.001f,.3f)] public float roadSpatialFrequency=.06f;
    [Range(0,.3f)] public float roadPosition=.025f;
    [Range(0,1)] public float roadRotation=.14f;
    [Header("Wind and passing-car pressure")]
    [Range(.1f,8)] public float windFrequency=2.3f;
    [Range(0,.3f)] public float windPosition=.025f;
    [Range(0,1)] public float windRotation=.22f;
    [Min(1)] public float pressureRange=35;
    [Header("Booster deployment mechanical impact (degrees)")]
    [Range(0,2)] public float deploymentKick=.2f;
    public bool previewInEditMode;

    static float Noise(float row,float t)=>Mathf.PerlinNoise(row,t)*2-1;
    static Vector3 Layer(float seed,float t)=>new Vector3(Noise(seed+.17f,t),Noise(seed+3.41f,t),Noise(seed+7.63f,t));
    static float Pulse(float value,float at,float width)=>Mathf.Exp(-Mathf.Pow((value-at)/width,2));

    public void Sample(out Vector3 position,out Vector3 rotation)
    {
        position=rotation=Vector3.zero;
        if(director==null || director.race==null || !director.race.Ready || shot==null)return;
        var race=director.race;
        int subject=shot.kind==RaceCinematicShot.ShotKind.RedBoosterRear?3:Mathf.Clamp(director.subjectIndex,0,race.racers.Length-1);
        var car=race.racers[subject];
        float speed=Mathf.Clamp01(car.speed/83.333f);
        float t=race.PresentedTime;
        bool fixedCamera=shot.kind==RaceCinematicShot.ShotKind.TracksidePan || shot.kind==RaceCinematicShot.ShotKind.OverheadCheckpoint;
        float pressure=0;
        for(int i=0;i<race.racers.Length;i++)
        {
            if(!fixedCamera && i==subject)continue;
            var other=race.racers[i];
            float distance=Vector3.ProjectOnPlane(other.car.position-transform.position,Vector3.up).magnitude;
            pressure+=Mathf.Exp(-distance*distance/Mathf.Max(1,pressureRange*pressureRange))*Mathf.Clamp01(other.speed/83.333f);
        }
        pressure=Mathf.Min(2,pressure);
        float boost=subject==3 ? Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,race.StoryBoostAmount)) : 0;
        float engine=fixedCamera?0:(.15f+.85f*speed+boost*.7f);
        float road=fixedCamera?0:speed*speed;
        float wind=fixedCamera?pressure:speed*speed*(.35f+pressure);
        float s=seed*37.13f;
        Vector3 e=Layer(s,t*engineFrequency);
        Vector3 r=Layer(s+11.29f,race.PresentedDistance(car)*roadSpatialFrequency);
        Vector3 w=Layer(s+23.71f,t*windFrequency);
        position=Vector3.Scale(e*enginePosition*engine+r*roadPosition*road,new Vector3(.45f,1,.3f))
            +Vector3.Scale(w*windPosition*wind,new Vector3(1,.25f,.5f));
        rotation=Vector3.Scale(Layer(s+35.11f,t*engineFrequency)*engineRotation*engine,new Vector3(1,.35f,.6f))
            +Vector3.Scale(Layer(s+47.29f,race.PresentedDistance(car)*roadSpatialFrequency)*roadRotation*road,new Vector3(1,.2f,.7f))
            +Vector3.Scale(w*windRotation*wind,new Vector3(.25f,1,.6f));
        if(subject==3 && !fixedCamera && race.storyMode)
        {
            float amount=race.StoryBoostAmount;
            float impact=Pulse(amount,.25f,.025f)+Pulse(amount,.515625f,.025f)+Pulse(amount,.96f,.015f);
            float clunk=impact*Mathf.Sin(t*2*Mathf.PI*9+seed)*deploymentKick;
            rotation.x+=clunk;position.y+=clunk*.025f;
        }
        float master=Mathf.Clamp(intensity,0,2)*Mathf.Clamp(director.shakeIntensity,0,2);
        position*=master;rotation*=master;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,ref CameraState state,float deltaTime)
    {
        if(stage!=CinemachineCore.Stage.Finalize || (!Application.isPlaying && !previewInEditMode))return;
        Sample(out var position,out var rotation);
        state.PositionCorrection+=state.RawOrientation*position;
        state.OrientationCorrection*=Quaternion.Euler(rotation);
    }
}
