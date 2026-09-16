using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace Damin.VFX.TireSmoke.Progressive
{
    [DisallowMultipleComponent]
    public sealed class ProgressiveTireSmokeController : MonoBehaviour
    {
        [Serializable] public sealed class WheelBinding
        {
            public Transform wheel;
            public Transform centerPoint;
            public Transform groundContactPoint;
            [Tooltip("Non-spinning steering/axle frame, not the rotating tire mesh.")]
            public Transform directionReference;
            [Min(.01f)] public float wheelRadius = .35f;
            [Min(.01f)] public float wheelWidth = .22f;
            [Tooltip("Axle direction in the non-spinning direction reference / vehicle frame.")]
            public Vector3 wheelAxis = Vector3.right;
            [Range(-1,1)] public int rotationDirection = 1;
            [Min(0)] public float wheelAngularSpeed = 5.7f;
            [Range(0,1)] public float slipAmount = 1;
            [Range(0,2)] public float powerMultiplier = 1;
            public bool grounded = true;
        }
        [Serializable] public sealed class LayerResponse
        {
            public string layerPrefix;
            public AnimationCurve response;
            [Range(0,2)] public float maxStrength = 1;
            public LayerResponse(string prefix,float start,float middle,float maximum)
            {layerPrefix=prefix;maxStrength=maximum;response=new AnimationCurve(new Keyframe(0,0),new Keyframe(start,0),new Keyframe(.65f,middle),new Keyframe(1,1));}
        }
        [Header("Connections / 스크립트는 연기 부모에 붙입니다")]
        public Transform vehicleRoot;
        public WheelBinding rearLeft = new WheelBinding();
        public WheelBinding rearRight = new WheelBinding();
        public Vector3 vehicleForwardAxis = Vector3.forward;
        public bool hideSourceWhenBound = true;
        [Header("Power / 단계적 증가")]
        [Range(0,1)] public float smokePower = .25f;
        public bool useSlipAmount;
        [Min(.01f)] public float buildUpTime = 3f;
        [Min(.01f)] public float fadeOutTime = 2.5f;
        [Range(.05f,.95f)] public float surgeThreshold = .6f;
        [Range(0,2)] public float surgeBoost = .65f;
        public LayerResponse[] layers = {
            new LayerResponse("01_", .015f, .8f, .8f),
            new LayerResponse("02_", .08f, .40f, 1f),
            new LayerResponse("03_", .30f, .24f, 1f),
            new LayerResponse("04_", .08f, .5f, .6f),
            new LayerResponse("05_", .18f, .55f, .9f)
        };
        [Header("Flow / 흐름과 축적")]
        [Min(.01f)] public float referenceWheelRadius = .65f;
        [Range(0,1)] public float wrapAmount = .8f;
        [Min(0)] public float previewAngularSpeed = 5.7f;
        [Range(.25f,3)] public float smokeLifetime = 1.2f;
        [Range(.25f,3)] public float smokeExpansion = 1.4f;
        [Min(0)] public float vehicleSpeed;
        public AnimationCurve speedToWake = AnimationCurve.Linear(0,.85f,60,2f);
        [Min(.1f)] public float groundWidth = 1f;
        [Min(.1f)] public float groundHeight = 1f;
        [Min(.1f)] public float trailLength = 1f;
        [Header("Ground follow / 노면 밀착 (선택)")]
        [Tooltip("각 접지 앵커에서 아래로 Raycast하여 실제 노면에 연기를 붙입니다.")]
        public bool followGround;
        public LayerMask groundLayers = ~0;
        [Min(.01f)] public float groundRayStart = .2f;
        [Min(.01f)] public float groundRayDistance = .6f;
        [Min(0)] public float groundOffset = .01f;
        [Header("Preview / Play 모드에서 미리보기")]
        public bool previewOnPlay = true;
        public bool loopPreview;
        [Tooltip("Seconds after Play / Start Sequence before smoke begins. Trailer clock, independent of slip.")]
        [Min(0)] public float sequenceStartDelay = 2f;
        [Min(.1f)] public float previewBuildTime = 5f;
        [Min(0)] public float previewHoldTime = 3f;
        [Min(.1f)] public float previewFadeTime = 3f;
        [Min(0)] public float previewRestTime = 2f;
        public AnimationCurve previewRamp = AnimationCurve.EaseInOut(0,0,1,1);
        public float CurrentPower { get; private set; }
        public bool PreviewRunning { get; private set; }
        public float SequenceTime => previewClock;
        public int SpawnedWheelCount => followers.Count;

        sealed class EffectState
        {
            public VisualEffect fx; public Renderer renderer; public bool rendererEnabled;
            public float rate, size, wake, width; public Vector3 scale;
        }
        sealed class Follower
        {
            public WheelBinding binding; public Transform wheel; public Renderer tire;
            public GameObject root; public EffectState[] effects; public float power;
        }
        readonly List<Follower> followers = new List<Follower>();
        EffectState[] source = Array.Empty<EffectState>();
        Transform builtVehicle;
        float previewClock, localPower;
        object timelineOwner;
        float timelinePower;
        float timelineLifetime = 1.2f, timelineExpansion = 1.4f;
        float timelineGroundWidth = 1, timelineGroundHeight = 1, timelineTrailLength = 1;

        public bool IsTimelineControlled => timelineOwner != null;

        static EffectState[] Capture(GameObject root)
        {
            var fxs=root.GetComponentsInChildren<VisualEffect>(true);var result=new EffectState[fxs.Length];
            for(int i=0;i<fxs.Length;i++) {
                var f=fxs[i];var r=f.GetComponent<Renderer>();
                result[i]=new EffectState{fx=f,renderer=r,rendererEnabled=r&&r.enabled,rate=Get(f,"EmissionRate",0),size=Get(f,"SizeMultiplier",1),wake=Get(f,"WakeSpeed",1.6f),width=Get(f,"WheelWidth",.6f),scale=f.transform.localScale};
            }return result;
        }
        static float Get(VisualEffect f,string key,float fallback)=>f.HasFloat(key)?f.GetFloat(key):fallback;
        static void Set(VisualEffect f,string key,float value){if(f.HasFloat(key))f.SetFloat(key,value);}
        void OnEnable()
        {
            if(!Application.isPlaying)return; source=Capture(gameObject);localPower=0;CurrentPower=0;
            foreach(var e in source)Set(e.fx,"EmissionRate",0);
            RebuildBindings();if(previewOnPlay)StartPreview();
        }
        void OnDisable(){ClearFollowers();RestoreSource();PreviewRunning=false;timelineOwner=null;timelinePower=0;CurrentPower=0;}
        void OnDestroy(){ClearFollowers();}
        void RestoreSource(){foreach(var e in source){if(e.renderer)e.renderer.enabled=e.rendererEnabled;if(e.fx)Set(e.fx,"EmissionRate",e.rate);}}
        void ClearFollowers(){foreach(var f in followers)if(f.root){f.root.SetActive(false);Destroy(f.root);}followers.Clear();}
        public void SetSmokePower(float value){smokePower=Mathf.Clamp01(value);}
        public void SetWheelSlip(bool left,float value){(left?rearLeft:rearRight).slipAmount=Mathf.Clamp01(value);}
        public void SetVehicleSpeed(float value){vehicleSpeed=Mathf.Max(0,value);}
        public void SetTimelineState(object owner,float power,float lifetime,float expansion,float width,float height,float length)
        {
            if(owner==null)return;timelineOwner=owner;PreviewRunning=false;
            timelinePower=Mathf.Clamp01(power);timelineLifetime=Mathf.Clamp(lifetime,.25f,3);
            timelineExpansion=Mathf.Clamp(expansion,.25f,3);timelineGroundWidth=Mathf.Max(.1f,width);
            timelineGroundHeight=Mathf.Max(.1f,height);timelineTrailLength=Mathf.Max(.1f,length);
        }
        public void ReleaseTimeline(object owner)
        {
            if(timelineOwner!=owner)return;timelineOwner=null;timelinePower=0;smokePower=0;localPower=0;
            foreach(var f in followers)f.power=0;
        }
        [ContextMenu("Start Progressive Preview (Play Mode)")]
        public void StartPreview(){if(!Application.isPlaying)return;previewClock=0;PreviewRunning=true;smokePower=0;}
        [ContextMenu("Stop Emission / Fade Out")]
        public void StopPreview(){PreviewRunning=false;smokePower=0;}
        [ContextMenu("Rebuild Rear Wheel Bindings (Play Mode)")]
        public void RebuildBindings()
        {
            if(!Application.isPlaying)return;ClearFollowers();builtVehicle=vehicleRoot;
            if(!vehicleRoot||source.Length==0)return;
            var used=new HashSet<Transform>();int index=0;
            foreach(var binding in new[]{rearLeft,rearRight}){
                index++;if(binding==null||!binding.wheel||!used.Add(binding.wheel))continue;
                var obj=CloneInternalSmoke();obj.name=index==1?"ProgressiveSmoke_RearLeft (Runtime)":"ProgressiveSmoke_RearRight (Runtime)";
                SceneManager.MoveGameObjectToScene(obj,gameObject.scene);foreach(var t in obj.GetComponentsInChildren<Transform>(true))t.gameObject.layer=gameObject.layer;
                var f=new Follower{binding=binding,wheel=binding.wheel,root=obj,tire=binding.wheel.GetComponent<Renderer>()??binding.wheel.GetComponentInChildren<Renderer>(),effects=CaptureClone(obj)};
                foreach(var e in f.effects){Set(e.fx,"EmissionRate",0);e.fx.startSeed+=(uint)(index*101);}
                followers.Add(f);Pose(f);obj.SetActive(true);foreach(var e in f.effects)e.fx.Reinit();
            }
        }

        // Clone only the five internal VFX layer objects, never the controller root.
        // The inactive container prevents OnPlay until rates/pose have been configured.
        GameObject CloneInternalSmoke()
        {
            var root=new GameObject("InternalSmokeCopy");root.SetActive(false);
            foreach(var e in source){
                if(!e.fx)continue;
                var node=Instantiate(e.fx.gameObject,root.transform,false);node.name=e.fx.name;
                node.transform.localPosition=transform.InverseTransformPoint(e.fx.transform.position);
                node.transform.localRotation=Quaternion.Inverse(transform.rotation)*e.fx.transform.rotation;
                node.transform.localScale=e.scale;
            }
            return root;
        }
        EffectState[] CaptureClone(GameObject root)
        {
            var copies=Capture(root);
            for(int i=0;i<copies.Length;i++){
                // Source renderer/rate may already be hidden/zero during a runtime rebuild.
                // Use the values cached before the controller first changed them.
                var e=copies[i];var original=Array.Find(source,s=>s.fx && s.fx.name==e.fx.name);
                if(original==null)continue;
                e.rate=original.rate;e.size=original.size;e.wake=original.wake;
                e.width=original.width;e.scale=original.scale;e.rendererEnabled=original.rendererEnabled;
                if(e.renderer)e.renderer.enabled=original.rendererEnabled;
            }
            return copies;
        }

        bool BindingsChanged()
        {
            if(builtVehicle!=vehicleRoot)return true;if(!vehicleRoot||source.Length==0)return false;
            int n=0;var used=new HashSet<Transform>();foreach(var b in new[]{rearLeft,rearRight}){
                if(b==null||!b.wheel||!used.Add(b.wheel))continue;
                if(n>=followers.Count||followers[n].binding!=b||followers[n].wheel!=b.wheel||!followers[n].root)return true;n++;
            }return n!=followers.Count;
        }
        void LateUpdate()
        {
            if(BindingsChanged())RebuildBindings();
            bool sequenceWasRunning=PreviewRunning;
            if(PreviewRunning){
                previewClock+=Time.deltaTime;float delay=Mathf.Max(0,sequenceStartDelay),a=delay+Mathf.Max(.1f,previewBuildTime),b=a+Mathf.Max(0,previewHoldTime),c=b+Mathf.Max(.1f,previewFadeTime);
                if(previewClock<delay)smokePower=0;
                else if(previewClock<a)smokePower=Mathf.Clamp01(previewRamp==null?(previewClock-delay)/(a-delay):previewRamp.Evaluate((previewClock-delay)/(a-delay)));
                else if(previewClock<b)smokePower=1;
                else if(previewClock<c)smokePower=1-Mathf.SmoothStep(0,1,(previewClock-b)/(c-b));
                else {smokePower=0;if(loopPreview&&previewClock>=c+previewRestTime)previewClock=0;else if(!loopPreview)PreviewRunning=false;}
            }
            float target=timelineOwner!=null?timelinePower:Mathf.Clamp01(smokePower);bool bound=followers.Count>0;
            bool direct=sequenceWasRunning||timelineOwner!=null;
            localPower=direct?(bound?0:target):Smooth(localPower,bound?0:target);CurrentPower=localPower;
            foreach(var e in source){if(e.renderer)e.renderer.enabled=!(bound&&hideSourceWhenBound)&&e.rendererEnabled;Apply(e,localPower,previewAngularSpeed,1);}
            foreach(var f in followers){
                Pose(f);float requested=f.binding.grounded&&f.wheel.gameObject.activeInHierarchy?target*Mathf.Clamp01(useSlipAmount?f.binding.slipAmount:1)*Mathf.Max(0,f.binding.powerMultiplier):0;
                f.power=direct?Mathf.Clamp01(requested):Smooth(f.power,Mathf.Clamp01(requested));CurrentPower=Mathf.Max(CurrentPower,f.power);
                foreach(var e in f.effects)Apply(e,f.power,f.binding.wheelAngularSpeed,f.binding.rotationDirection<0?-1:1);
            }
        }
        float Smooth(float current,float target)=>Mathf.MoveTowards(current,target,Time.deltaTime/Mathf.Max(.01f,target>current?buildUpTime:fadeOutTime));
        public float EvaluateLayerStrength(int index,float power)
        {
            if(layers==null||index<0||index>=layers.Length||layers[index]==null)return 0;
            var layer=layers[index];power=Mathf.Clamp01(power);if(power<=0)return 0;
            float surge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(Mathf.Clamp(surgeThreshold,.05f,.95f),1,power));
            return Mathf.Max(0,layer.response==null?power:layer.response.Evaluate(power))*Mathf.Max(0,layer.maxStrength)*(1+Mathf.Max(0,surgeBoost)*surge);
        }
        void Apply(EffectState e,float power,float angularSpeed,int direction)
        {
            if(!e.fx)return;int index=-1;if(layers!=null)for(int i=0;i<layers.Length;i++)if(layers[i]!=null&&!string.IsNullOrEmpty(layers[i].layerPrefix)&&e.fx.name.StartsWith(layers[i].layerPrefix,StringComparison.Ordinal)){index=i;break;}
            Set(e.fx,"EmissionRate",e.rate*EvaluateLayerStrength(index,power));
            // Do not fade existing particles by multiplying their opacity with power.
            float life=timelineOwner!=null?timelineLifetime:smokeLifetime, expansion=timelineOwner!=null?timelineExpansion:smokeExpansion;
            Set(e.fx,"LifetimeMultiplier",Mathf.Clamp(life,.25f,3));Set(e.fx,"Expansion",Mathf.Clamp(expansion,.25f,3));
            Set(e.fx,"WrapAmount",Mathf.Clamp01(wrapAmount));Set(e.fx,"WrapAngularSpeed",Mathf.Max(.1f,angularSpeed));Set(e.fx,"RotationDirection",direction);
            float wake=speedToWake==null?1:Mathf.Clamp(speedToWake.Evaluate(Mathf.Max(0,vehicleSpeed)),.05f,6);
            Set(e.fx,"WakeSpeed",e.wake*wake);
            if(e.fx.name.StartsWith("05_")){float w=timelineOwner!=null?timelineGroundWidth:groundWidth,h=timelineOwner!=null?timelineGroundHeight:groundHeight,l=timelineOwner!=null?timelineTrailLength:trailLength;Set(e.fx,"FlowSpeed",1.7f*wake);Set(e.fx,"CloudWidth",Mathf.Max(.1f,w));Set(e.fx,"CloudHeight",Mathf.Max(.1f,h));Set(e.fx,"CloudLength",Mathf.Max(.1f,l));}
        }
        void Pose(Follower f)
        {
            var b=f.binding;var frame=b.directionReference?b.directionReference:vehicleRoot;Vector3 up=Vector3.up;
            var forward=Vector3.ProjectOnPlane(vehicleRoot.TransformDirection(vehicleForwardAxis),up);if(forward.sqrMagnitude<.0001f)forward=Vector3.forward;
            var axle=Vector3.ProjectOnPlane(frame.TransformDirection(b.wheelAxis),up);if(axle.sqrMagnitude<.0001f)axle=Vector3.Cross(up,forward);
            var facing=Vector3.Cross(axle.normalized,up);if(Vector3.Dot(facing,forward)<0)facing=-facing;
            var rotation=Quaternion.LookRotation(facing,up);float radius=Mathf.Max(.01f,b.wheelRadius);float ratio=radius/Mathf.Max(.01f,referenceWheelRadius);
            Vector3 center=b.centerPoint?b.centerPoint.position:f.tire?f.tire.bounds.center:b.wheel.position;
            Vector3 contact=b.groundContactPoint?b.groundContactPoint.position:center-up*radius;
            if(followGround&&Physics.Raycast(contact+up*Mathf.Max(.01f,groundRayStart),-up,out var hit,Mathf.Max(.01f,groundRayStart+groundRayDistance),groundLayers,QueryTriggerInteraction.Ignore))contact=hit.point+hit.normal*groundOffset;
            f.root.transform.SetPositionAndRotation(contact,rotation);
            f.root.transform.localScale=Vector3.one*ratio;
            foreach(var e in f.effects){if(!e.fx)continue;bool free=e.fx.name.StartsWith("03_")||e.fx.name.StartsWith("05_");e.fx.transform.localScale=e.scale*(free?1/ratio:1);
                Set(e.fx,"WheelRadius",free?radius:Mathf.Max(.01f,referenceWheelRadius));Set(e.fx,"WheelWidth",Mathf.Max(.01f,b.wheelWidth)/(free?1:ratio));}
        }
    }
}
