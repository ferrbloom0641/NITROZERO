using UnityEngine;
using UnityEngine.VFX;
using Unity.Cinemachine;
using Damin.VFX.TireSmoke.Progressive;
using Damin.CinematicCopy;

/// <summary>Rear-axle drift take. Timeline time drives the car and a world-space missile line.</summary>
[ExecuteAlways, DefaultExecutionOrder(2500)]
public sealed class ExteriorMissileDrift : MonoBehaviour
{
    public enum View { Automatic, Overhead, RearWheel, WideDrift }
    [Header("Playback / 카메라 선택")]
    public View cameraView;
    public float duration = 14;
    public float rearCut = 4.6f, wideCut = 7.7f;
    [Header("Scene connections")]
    public Transform car, missile, exhaust, passengerEntry, driverExit;
    public VisualEffect missileSmoke;
    public ProgressiveTireSmokeController tireSmoke;
    public DMRaceWheelVisual wheels;
    public ExteriorDriftAction action;
    public CinemachineCamera overheadCamera, rearWheelCamera, wideCamera;
    [Header("Drift / 후륜 축 기준")]
    public Vector3 rearAxleLocal;
    public Vector3 startRearAxleWorld;
    public float roadHeading = 90;
    public AnimationCurve clockwiseYaw = new AnimationCurve(
        new Keyframe(0,0), new Keyframe(.65f,0), new Keyframe(2.6f,90),
        new Keyframe(4.4f,90), new Keyframe(8.3f,270), new Keyframe(11.6f,360), new Keyframe(14,360));
    public AnimationCurve travel = new AnimationCurve(
        new Keyframe(0,0,14,14), new Keyframe(.8f,10,10,10), new Keyframe(2.6f,22,3,3),
        new Keyframe(4.4f,27.4f,3,3), new Keyframe(7,43,8,8), new Keyframe(12,100,14,14));
    public AnimationCurve lateralSlide = new AnimationCurve(
        new Keyframe(0,0), new Keyframe(.65f,0), new Keyframe(2.6f,3),
        new Keyframe(4.4f,3), new Keyframe(7,7), new Keyframe(12,4));
    [Header("Missile / 보조석 → 운전석, 크기는 미사일 Transform Scale")]
    public float windowEntryTime = 2.8f, windowExitTime = 4.2f;
    [Min(1)] public float outsideMissileSpeed = 2.2f;
    [Min(0)] public float missileLeadInDistance;
    public ParticleSystem missileFlame;
    public Light missileFlameLight;
    [Min(0)] public float missileCoreIntensity = .65f;
    public float missileSmokeSize = .1f, missileSmokeExpansion = .25f;
    public float missileTrailLength = 7, missileSmokeLifetime = 2.6f;
    [Range(.05f,1)] public float missileSmokeTimeScale = .4f;
    [Header("Tire smoke / 양쪽 후륜")]
    public AnimationCurve smokePower = new AnimationCurve(new Keyframe(0,0),new Keyframe(.6f,0),
        new Keyframe(1.6f,.76f),new Keyframe(3,.85f),new Keyframe(6,.92f),new Keyframe(8.6f,.7f),new Keyframe(11,0),new Keyframe(12,0));
    [Range(.25f,3)] public float tireLifetime = 1.1f, tireExpansion = 1.05f;
    [Header("Camera composition / 차체 기준 오프셋")]
    [Tooltip("차량 이동은 즉시 따라가고 회전만 이 시간만큼 늦게 따라갑니다.")]
    [Range(0,1.5f)] public float cameraTurnDelay = .65f;
    [Range(0,1)] public float overheadTurnFollow = .22f;
    public Vector3 overheadOffset = new Vector3(4,24,-12);
    public Vector3 overheadAim = new Vector3(0,1.3f,-7);
    public Vector3 rearOffset = new Vector3(-8,.1f,-4);
    public Vector3 rearAimOffset = new Vector3(-3.7f, -.25f, 1.5f);
    [Range(0,1)] public float rearTurnFollow = .12f;
    public float rearSwingArm = 3.5f;
    public Vector3 wideOffset = new Vector3(-17,6,-16);
    public Vector3 wideAimLocal = new Vector3(0,1,-9);
    [System.NonSerialized] public float previewTime;
    float smokeClock = -1;
    Quaternion StartRotation => Quaternion.Euler(0, roadHeading, 0);

    public Quaternion RotationAt(float t) => StartRotation * Quaternion.Euler(0, clockwiseYaw.Evaluate(t),0);
    Vector3 RoadAt(float t) => startRearAxleWorld + StartRotation * new Vector3(lateralSlide.Evaluate(t),0,travel.Evaluate(t));
    public Vector3 RearAt(float t)
    {
        float start = windowExitTime + .12f;
        float amount = Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,start+.4f,t)) * (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.5f,5.8f,t)));
        return RoadAt(t)+(RotationAt(start)*Vector3.forward-RotationAt(t)*Vector3.forward)*rearSwingArm*amount;
    }
    public Vector3 CarPointAt(float t, Vector3 local) => RearAt(t) + RotationAt(t) * Vector3.Scale(local-rearAxleLocal,car.lossyScale);
    public Vector3 MissileAt(float t)
    {
        // Endpoints are sampled at their own crossing times: one fixed world line,
        // not a missile parented to the turning car. Each moving window meets this line in time.
        Vector3 a=CarPointAt(windowEntryTime,passengerEntry.localPosition);
        Vector3 b=CarPointAt(windowExitTime,driverExit.localPosition);
        float u=(t-windowEntryTime)/Mathf.Max(.1f,windowExitTime-windowEntryTime);
        if(u<0)u*=outsideMissileSpeed;
        if(u>1)u=1+(u-1)*outsideMissileSpeed;
        float lead=Mathf.Clamp01((windowEntryTime-t)/Mathf.Max(.01f,windowEntryTime));
        return Vector3.LerpUnclamped(a,b,u)-(b-a).normalized*missileLeadInDistance*lead*lead;
    }
    void PoseMissile(float t)
    {
        missile.position=MissileAt(t);
        missile.rotation=Quaternion.LookRotation(MissileAt(windowExitTime)-MissileAt(windowEntryTime),Vector3.up);
    }
    public void Evaluate(float t, bool simulateTrail = true)
    {
        if(!car||!missile||!passengerEntry||!driverExit)return;
        previewTime=Mathf.Clamp(t,0,duration);
        car.rotation=RotationAt(previewTime);
        car.position=CarPointAt(previewTime,Vector3.zero);
        if(wheels)wheels.Sample(travel.Evaluate(previewTime)*1.75f,-Mathf.Sin(clockwiseYaw.Evaluate(previewTime)*Mathf.Deg2Rad)*29);
        if(action)action.Evaluate(previewTime);
        PoseMissile(previewTime);
        if(tireSmoke){
            tireSmoke.SetTimelineState(this,smokePower.Evaluate(previewTime),tireLifetime,tireExpansion,1.1f,.75f,1.25f);
            tireSmoke.SetVehicleSpeed(Mathf.Abs(travel.Evaluate(previewTime+.02f)-travel.Evaluate(previewTime))/.02f);
        }
        if(simulateTrail&&missileSmoke&&exhaust)SimulateMissileTrail(previewTime);
        PoseMissile(previewTime);
        UpdateCameras();
        if(missileFlame&&exhaust){missileFlame.transform.SetPositionAndRotation(exhaust.position,Quaternion.LookRotation(-missile.forward,missile.up));missileFlame.Simulate(previewTime+.15f,true,true,true);missileFlame.Pause(true);}
        if(missileFlameLight)missileFlameLight.intensity=.7f+.12f*Mathf.Sin(previewTime*47);
    }
    public void UpdateCameras()
    {
        if(!car)return;
        // Absolute-time angular delay also works when scrubbing backwards.
        float delayedYaw=clockwiseYaw.Evaluate(Mathf.Max(0,previewTime-cameraTurnDelay));
        float anchorYaw=clockwiseYaw.Evaluate(rearCut-cameraTurnDelay);
        Quaternion trailingRotation=StartRotation*Quaternion.Euler(0,anchorYaw+(delayedYaw-anchorYaw)*rearTurnFollow,0);
        Quaternion overheadRotation=StartRotation*Quaternion.Euler(0,delayedYaw*overheadTurnFollow,0);
        Aim(overheadCamera,RearAt(previewTime)+overheadRotation*overheadOffset,car.TransformPoint(overheadAim));
        Aim(rearWheelCamera,RoadAt(previewTime)+trailingRotation*rearOffset,RoadAt(previewTime)+trailingRotation*rearAimOffset);
        // Wide camera retains its world-facing side of the road as the car slides across frame.
        Vector3 wideBase=startRearAxleWorld+StartRotation*new Vector3(0,0,travel.Evaluate(previewTime)*.95f);
        Quaternion wideReaction=Quaternion.Euler(0,(delayedYaw-clockwiseYaw.Evaluate(previewTime))*.35f,0);
        Aim(wideCamera,wideBase+wideReaction*StartRotation*wideOffset,car.TransformPoint(wideAimLocal));
        int view=cameraView==View.Automatic?(previewTime<rearCut?1:previewTime<wideCut?2:3):(int)cameraView;
        if(overheadCamera)overheadCamera.Priority=view==1?200:20;
        if(rearWheelCamera)rearWheelCamera.Priority=view==2?200:20;
        if(wideCamera)wideCamera.Priority=view==3?200:20;
    }
    static void Aim(CinemachineCamera cam,Vector3 p,Vector3 target){if(cam)cam.transform.SetPositionAndRotation(p,Quaternion.LookRotation(target-p,Vector3.up));}
    void F(string key,float value){if(missileSmoke.HasFloat(key))missileSmoke.SetFloat(key,value);}
    void V(string key,Vector3 value){if(missileSmoke.HasVector3(key))missileSmoke.SetVector3(key,value);}
    void SimulateMissileTrail(float t)
    {
        missileSmoke.pause=true;
        if(smokeClock<0||t<smokeClock-.0001f){missileSmoke.resetSeedOnPlay=false;missileSmoke.startSeed=7382;missileSmoke.Reinit();smokeClock=0;}
        F("SpawnRate",700);F("SmokeDensity",1.1f);F("ParticleSize",missileSmokeSize);F("Expansion",missileSmokeExpansion);
        F("Turbulence",.025f);F("Speed",.12f);F("TrailLength",missileTrailLength);F("MaxSmokeLifetime",missileSmokeLifetime);F("TailFadeStart",.55f);F("CoreIntensity",missileCoreIntensity);
        if(missileSmoke.HasVector4("SmokeColor"))missileSmoke.SetVector4("SmokeColor",new Vector4(.82f,.85f,.89f,1));
        while(smokeClock+.00001f<t){
            float dt=Mathf.Min(1f/60,t-smokeClock);PoseMissile(smokeClock);Vector3 previous=exhaust.position;
            smokeClock+=dt;PoseMissile(smokeClock);V("PreviousEmitterPosition",previous);V("EmitterPosition",exhaust.position);V("EmitterDirection",exhaust.forward);
            F("MissileSpeed",Vector3.Distance(previous,exhaust.position)/Mathf.Max(.00001f,dt*missileSmokeTimeScale));F("Emission",smokeClock<6?1:0);
            missileSmoke.Simulate(dt*missileSmokeTimeScale,1);
        }
    }
    public void ResetShot(){smokeClock=-1;if(tireSmoke)tireSmoke.ReleaseTimeline(this);if(missileSmoke){F("Emission",0);missileSmoke.Reinit();missileSmoke.pause=true;}Evaluate(0,false);}
    void OnDisable(){if(tireSmoke)tireSmoke.ReleaseTimeline(this);if(wheels)wheels.Release();}
    void OnDrawGizmosSelected(){if(!car||!passengerEntry||!driverExit)return;Gizmos.color=Color.cyan;Gizmos.DrawLine(MissileAt(windowEntryTime),MissileAt(windowExitTime));Gizmos.DrawWireSphere(RearAt(previewTime),.45f);}
}

