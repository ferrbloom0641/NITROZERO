using System;
using UnityEngine;
using Unity.Cinemachine;
using YUJEONG;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Repeatable exterior takes. Absolute time owns movement, transformation and cameras.</summary>
[DefaultExecutionOrder(1100)]
public sealed class BoostOutsideDirector : MonoBehaviour
{
    public enum Subject { Pair, Red, Blue }
    [Serializable] public sealed class Shot
    {
        public string name;
        public CinemachineCamera camera;
        public Subject subject;
        public Vector3 offset;
        public Vector3 aim;
        public float cue;
        public bool fixedTrackside;
        public bool leaveBehind;
        public bool chaseLookBack;
    }
    public Transform redCar, blueCar;
    public BoosterDeploymentController redBooster;
    public VehicleTransformationController blueBooster;
    public TrailerCruiseMotion redMotion, blueMotion;
    public Camera outputCamera;
    public CinemachineBrain brain;
    public Shot[] shots = Array.Empty<Shot>();
    [Header("Take / F1-F11 select, R replay, Space pause, H controls")]
    [Range(0, 10)] public int selectedShot;
    public bool playOnStart = true;
    public bool showControls;
    [Min(0)] public float time;
    public bool paused;
    [Header("Road frame: metres, local +Z = travel")]
    public Vector3 roadOrigin = new Vector3(2700, -23.65f, 0);
    public Vector3 roadEuler = new Vector3(0, -90, 0);
    public float redHeight, blueHeight;
    public float laneOffset = 6;
    public float initialGap = 42;
    [Header("Sequence (seconds)")]
    public float redDeployAt = 2;
    public float blueDeployAt = 9;
    public float deployDuration = 3.2f;
    public float drawLevelAt = 20;
    public float takeDuration = 28;
    [Header("Travel speed (metres / second)")]
    public float cruiseSpeed = 135f;
    public float boostSpeed = 255f;
    public float accelerationDuration = 2.5f;
    [Header("Exterior camera feel")]
    public bool cameraMotion = true;
    [Tooltip("Subtle engine vibration and wind pressure. 1 = recording default, 0 = locked camera.")]
    [Range(0, 2)] public float cameraMotionIntensity = 1f;
    int lastShot = -1;
    [NonSerialized] public bool batchRecording;
    public float RedAmount => Mathf.Clamp01((time-redDeployAt)/Mathf.Max(.1f,deployDuration));
    public float BlueAmount => Mathf.Clamp01((time-blueDeployAt)/Mathf.Max(.1f,deployDuration));
    public Quaternion RoadRotation => Quaternion.Euler(roadEuler);

    void Awake()
    {
        if (redBooster != null) { redBooster.SetCinematicOwner(transform); redBooster.enabled = false; }
        if (blueBooster != null) { blueBooster.StopAllCoroutines(); blueBooster.enabled = false; }
    }
    void Start() { paused = !playOnStart; Sample(0); }
    void LateUpdate()
    {
        if(!batchRecording)
        {
#if ENABLE_INPUT_SYSTEM
        var k=Keyboard.current;
        if(k!=null)
        {
            for(int i=0;i<11;i++) if(k[(Key)((int)Key.F1+i)].wasPressedThisFrame) SelectShot(i);
            if(k.rKey.wasPressedThisFrame) Replay();
            if(k.spaceKey.wasPressedThisFrame) paused=!paused;
            if(k.hKey.wasPressedThisFrame) showControls=!showControls;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        for(int i=0;i<11;i++) if(Input.GetKeyDown((KeyCode)((int)KeyCode.F1+i))) SelectShot(i);
        if(Input.GetKeyDown(KeyCode.R)) Replay();
        if(Input.GetKeyDown(KeyCode.Space)) paused=!paused;
        if(Input.GetKeyDown(KeyCode.H)) showControls=!showControls;
#endif
        }
        Sample(time + (paused ? 0 : Time.deltaTime));
        if(time>=takeDuration) paused=true;
        if(brain!=null) brain.ManualUpdate();
    }
    public float BlueDistance(float t)
    {
        float u=Mathf.Clamp(t-(blueDeployAt+deployDuration),0,Mathf.Max(.1f,accelerationDuration));
        float tail=Mathf.Max(0,t-(blueDeployAt+deployDuration)-accelerationDuration);
        return cruiseSpeed*t+(boostSpeed-cruiseSpeed)*(u*u/(2*Mathf.Max(.1f,accelerationDuration))+tail);
    }
    public float Gap(float t)
    {
        float start=redDeployAt+deployDuration;
        float u=Mathf.Clamp01((t-start)/Mathf.Max(.1f,drawLevelAt-start));
        return initialGap*(1-u*u*(3-2*u));
    }
    public float RedDistance(float t) => BlueDistance(t)-Gap(t);
    public Vector3 CarPosition(bool red,float t)
    {
        return roadOrigin+RoadRotation*new Vector3(red?-laneOffset:laneOffset,red?redHeight:blueHeight,red?RedDistance(t):BlueDistance(t));
    }
    public void Sample(float seconds)
    {
        time=Mathf.Clamp(seconds,0,takeDuration);
        if(redCar==null||blueCar==null) return;
        redCar.SetPositionAndRotation(CarPosition(true,time),RoadRotation);
        blueCar.SetPositionAndRotation(CarPosition(false,time),RoadRotation);
        if(redBooster!=null) redBooster.ApplyCinematicAmount(RedAmount);
        if(blueBooster!=null) blueBooster.ApplyTransformationProgress(BlueAmount);
        PoseWheels(redMotion,RedDistance(time)+initialGap,true);
        PoseWheels(blueMotion,BlueDistance(time),false);
        UpdateCameras();
    }
    void PoseWheels(TrailerCruiseMotion motion,float distance,bool red)
    {
        if(motion==null)return;
        float dt=.01f;
        float speed=red?(RedDistance(time+dt)-RedDistance(time))/dt:(BlueDistance(time+dt)-BlueDistance(time))/dt;
        motion.speedKph=speed*3.6f;
        motion.ApplyTimelinePose(this,time,distance,0,1);
    }
    public void UpdateCameras()
    {
        if(shots==null||shots.Length==0)return;
        selectedShot=Mathf.Clamp(selectedShot,0,shots.Length-1);
        Quaternion q=RoadRotation;
        bool changed=lastShot!=selectedShot;
        for(int i=0;i<shots.Length;i++)
        {
            var s=shots[i];if(s.camera==null)continue;
            Vector3 anchor=s.subject==Subject.Red?redCar.position:s.subject==Subject.Blue?blueCar.position:(redCar.position+blueCar.position)*.5f;
            Vector3 position=anchor+q*s.offset, focus=anchor+q*s.aim;
            if(s.chaseLookBack)focus=Vector3.Lerp(blueCar.position+q*new Vector3(0,0,-8),redCar.position+q*new Vector3(0,0,5),.6f);
            if(s.fixedTrackside)
            {
                position=roadOrigin+q*new Vector3(s.offset.x,s.offset.y,BlueDistance(s.cue)+s.offset.z);
                focus=roadOrigin+q*new Vector3(s.aim.x,s.aim.y,BlueDistance(s.cue)+s.aim.z);
            }
            if(s.leaveBehind)
            {
                float launch=redDeployAt+deployDuration;
                float lag=Mathf.Clamp(time-launch,0,2)*7;
                position-=q*Vector3.forward*lag;
                focus-=q*Vector3.forward*lag*.5f;
            }
            foreach(var terrain in Terrain.activeTerrains)
            {
                Vector3 local=position-terrain.transform.position;
                Vector3 size=terrain.terrainData.size;
                if(local.x>=0&&local.z>=0&&local.x<=size.x&&local.z<=size.z)
                    position.y=Mathf.Max(position.y,terrain.SampleHeight(position)+terrain.transform.position.y+.5f);
            }
            Quaternion rotation=Quaternion.LookRotation(focus-position,q*Vector3.up);
            ApplyCameraMotion(s,i,ref position,ref rotation);
            s.camera.transform.SetPositionAndRotation(position,rotation);
            s.camera.Priority=i==selectedShot?100:0;
            if(changed)s.camera.PreviousStateIsValid=false;
        }
        if(changed&&shots[selectedShot].camera!=null)shots[selectedShot].camera.Prioritize();
        lastShot=selectedShot;
    }
    void ApplyCameraMotion(Shot shot,int index,ref Vector3 position,ref Quaternion rotation)
    {
        if(!cameraMotion || cameraMotionIntensity<=0)return;
        // Absolute take time keeps pause, scrubbing and separate recordings identical.
        float seed=13.7f+index*19.31f;
        float acceleration=Mathf.Clamp01((time-blueDeployAt-deployDuration)/Mathf.Max(.1f,accelerationDuration));
        float speed=Mathf.Lerp(cruiseSpeed,boostSpeed,acceleration);
        float wind=Mathf.Clamp(speed/127.5f,0,1.5f);
        float weight=shot.subject==Subject.Pair?.8f:.65f;
        if(shot.leaveBehind || shot.chaseLookBack)weight=1f;
        float engine=1f;
        if(shot.fixedTrackside)
        {
            // Roadside mounts respond to the passing wake, not the car's engine.
            float distance=Mathf.Min(Mathf.Abs(BlueDistance(time)-BlueDistance(shot.cue)),
                Mathf.Abs(RedDistance(time)-BlueDistance(shot.cue)));
            weight=.12f+.7f*Mathf.Exp(-distance*distance/(2*65f*65f));
            engine=.08f;
        }
        Vector3 vibration=new Vector3(Mathf.Sin(time*2*Mathf.PI*7.3f+seed),
            Mathf.Sin(time*2*Mathf.PI*9.1f+seed*.7f),Mathf.Sin(time*2*Mathf.PI*6.7f+seed*1.3f));
        Vector3 gust=new Vector3(Mathf.PerlinNoise(seed,time*.85f)*2-1,
            Mathf.PerlinNoise(seed+7,time*1.1f)*2-1,Mathf.PerlinNoise(seed+17,time*.65f)*2-1);
        float kick=0;
        if(!shot.fixedTrackside)
        {
            if(shot.subject!=Subject.Blue)kick+=IgnitionKick(time-redDeployAt-deployDuration*.96f);
            if(shot.subject!=Subject.Red)kick+=IgnitionKick(time-blueDeployAt-deployDuration*.85f);
        }
        float strength=weight*Mathf.Clamp(cameraMotionIntensity,0,2);
        Vector3 localOffset=(Vector3.Scale(vibration,new Vector3(.006f,.009f,.003f))*engine
            +Vector3.Scale(gust,new Vector3(.028f,.022f,.012f))*wind+new Vector3(0,.008f,-.018f)*kick)*strength;
        Vector3 angles=(Vector3.Scale(vibration,new Vector3(.045f,.025f,.035f))*engine
            +Vector3.Scale(gust,new Vector3(.15f,.12f,.18f))*wind+new Vector3(.12f,0,.035f)*kick)*strength;
        position+=rotation*Vector3.ClampMagnitude(localOffset,.06f);
        rotation*=Quaternion.Euler(Vector3.ClampMagnitude(angles,.35f));
    }
    static float IgnitionKick(float elapsed)
    {
        return elapsed<0 || elapsed>1.2f ? 0 : Mathf.Sin(elapsed*2*Mathf.PI*4)*Mathf.Exp(-elapsed*6);
    }
    public void SelectShot(int index){ selectedShot=Mathf.Clamp(index,0,shots.Length-1);lastShot=-1;UpdateCameras(); }
    public void Replay(){ResetParticles();paused=false;lastShot=-1;Sample(0);}
    public void CueSelected(){ResetParticles();paused=true;lastShot=-1;Sample(Mathf.Max(0,shots[selectedShot].cue-2));}
    void ResetParticles()
    {
        foreach(var ps in GetComponentsInChildren<ParticleSystem>(true)){ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=17;}
        foreach(var fx in GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true)){fx.resetSeedOnPlay=false;fx.startSeed=17;fx.Reinit();}
    }
    void OnGUI()
    {
        if(!showControls)return;
        GUILayout.BeginArea(new Rect(15,15,330,440),GUI.skin.box);
        GUILayout.Label("BOOST OUTSIDE  |  F1-F11 cameras");
        for(int i=0;i<shots.Length;i++)if(GUILayout.Toggle(selectedShot==i,$"{i+1:00}  {shots[i].name}",GUI.skin.button)&&selectedShot!=i)SelectShot(i);
        GUILayout.Label($"{time:F2} / {takeDuration:F0}s    gap {Gap(time):F1}m");
        if(GUILayout.Button("R  Replay from start"))Replay();
        if(GUILayout.Button("Cue selected shot (paused, 2s handle)"))CueSelected();
        if(GUILayout.Button(paused?"Space  Play":"Space  Pause"))paused=!paused;
        if(GUILayout.Button("H  Hide controls for recording"))showControls=false;
        GUILayout.EndArea();
    }
}
