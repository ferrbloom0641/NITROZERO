using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

[ExecuteAlways,DefaultExecutionOrder(3100)]
public sealed class GreenCarExplosionEnd : MonoBehaviour {
 [Header("Sequence / 포드 명중과 전복")]
 public PlayableDirector director;
 public float impactTime=10.066667f, duration=14.5f;
 public bool cutOnly;
 public float cutStartTime=9.7f;
 public Transform car, bullet, impactMarker, nosePivot;
 public BulletSplineController bulletFlight;
 public TrailerCruiseMotion cruise;
 public GameObject bulletDistortion;
 public Renderer[] bulletRenderers, damagedParts;
 public CinemachineCamera gunCamera, bulletCamera, crashCamera;
 public Vector3 carStart, pivotLocal, impactLocal;
 public Quaternion carRotation;
 [Header("Inertia / 앞부분을 축으로 뒤가 솟구침")]
 public float approachSpeed=8, slideDistance=7, liftHeight=1.8f;
 public float stopTime=.6f;
 public AnimationCurve pitch=new AnimationCurve(new Keyframe(0,0),new Keyframe(.1f,3,90,90),new Keyframe(.38f,40,110,110),new Keyframe(.9f,82,55,55),new Keyframe(1.5f,102,16,16),new Keyframe(3,115,6,6),new Keyframe(4.44f,122,3,3));
 [Header("Explosion / 연출 크기")]
 public Transform explosionRoot;
 public ParticleSystem[] layers;
 public Light flashLight;
 public AudioSource explosionAudio;
 public float explosionScale=1;
 [System.Serializable] public class ChainBlast {
 public float delay; public Vector3 localPoint; public Transform root; public ParticleSystem[] particles; public Renderer[] missileParts; public Light light; public AudioSource audio;
 }
 [Header("Chain reaction / 포드 내 미사일 연쇄 폭발")]
 public ChainBlast[] chainBlasts=new ChainBlast[0];
 [Header("Debris / 분리된 덮개와 포드")]
 public Transform[] debris;
 public Vector3[] debrisStart, debrisVelocity;
 public Quaternion[] debrisRotation;
 [Header("Camera / 낮은 앞 대각선")]
 public Vector3 cameraOffset=new Vector3(-12,2.1f,14);
 public float cameraShake=.18f;
 public float cameraRoadSpeed=10.5f;
 [Header("Impact close-up / 명중점에서 시작하는 후퇴")]
 public float impactCameraDistance=3.4f, impactCameraHeight=.5f;
 public AnimationCurve cameraPullback=new AnimationCurve(new Keyframe(0,0,3,3),new Keyframe(.366667f,3,15,15),new Keyframe(.7f,14,35,35),new Keyframe(1,22,20,20),new Keyframe(1.8f,32.5f,10.5f,10.5f),new Keyframe(4.8f,64,10.5f,10.5f));
 [System.NonSerialized] public float previewTime;
 float fxTime=-1, lastTime=-1;
 public Vector3 ImpactWorld=>carStart+carRotation*Vector3.Scale(impactLocal,car.lossyScale);
 public Vector3 PivotWorld=>carStart+carRotation*Vector3.Scale(pivotLocal,car.lossyScale);
 public float SlideAt(float age){if(age<0)return -approachSpeed*Mathf.Min(2.05f,-age);float u=Mathf.Clamp01(age/Mathf.Max(.01f,stopTime));return slideDistance*(2*u-u*u);}
 public Quaternion RotationAtAge(float age)=>carRotation*Quaternion.Euler(Mathf.Max(0,pitch.Evaluate(age)),age>0?Mathf.Min(age*4,10):0,age>0?Mathf.Sin(Mathf.Min(age,2)*1.3f)*3:0);
 public Vector3 PointAtAge(float age,Vector3 local){float lift=age<0?0:liftHeight*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,2.2f,age));return PivotWorld+carRotation*Vector3.forward*SlideAt(age)+Vector3.up*lift+RotationAtAge(age)*Vector3.Scale(local-pivotLocal,car.lossyScale);}
 void LateUpdate(){if(director&&director.enabled)Evaluate((float)director.time+(cutOnly?cutStartTime:0));}
 public void Evaluate(float time){
 if(!car||!bulletFlight)return;previewTime=Mathf.Clamp(time,0,duration);float age=previewTime-impactTime;
 Vector3 forward=carRotation*Vector3.forward;
 if(cruise)cruise.ApplyTimelinePose(this,previewTime,impactTime*approachSpeed+SlideAt(age),0);
 float slide=SlideAt(age);
 float lift=age<0?0:liftHeight*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,2.2f,age));
 car.rotation=RotationAtAge(age);
 Vector3 pivot=PivotWorld+forward*slide+Vector3.up*lift;
 car.position=pivot-car.rotation*Vector3.Scale(pivotLocal,car.lossyScale);
 if(nosePivot)nosePivot.position=pivot;
 if(impactMarker)impactMarker.position=ImpactWorld;
 float u=Mathf.Clamp01((previewTime-8.066667f)/2);
 // Keep the existing spline; blend only its final leg into the moving pod's impact point.
 float progress=Mathf.Pow(u,1.7f);bulletFlight.Progress=progress;
 Vector3 end;bulletFlight.Progress=1;end=bullet.position;bulletFlight.Progress=progress;
 bullet.position+=(ImpactWorld-end)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,progress));
 if(bulletDistortion)bulletDistortion.SetActive(age<0&&previewTime>=8.066667f);
 if(bulletCamera&&previewTime>=8.066667f&&age<0){Vector3 direction=(ImpactWorld-bullet.position).normalized;if(direction.sqrMagnitude<.1f)direction=Vector3.right;Vector3 side=Vector3.Cross(Vector3.up,direction);Vector3 pos=bullet.position-direction*.7f-side*2.5f+Vector3.up*.55f;bulletCamera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(bullet.position+direction*.25f-pos,Vector3.up));bulletCamera.Lens.FieldOfView=42;}
 foreach(var r in bulletRenderers)if(r)r.enabled=age<0&&previewTime>=8.066667f;
 foreach(var r in damagedParts)if(r)r.enabled=age<.025f;
 for(int i=0;i<debris.Length;i++){
 var d=debris[i];d.gameObject.SetActive(age>=.025f);if(age<0)continue;
 float a=Mathf.Max(0,age-.025f);Vector3 p=debrisStart[i]+debrisVelocity[i]*a+Vector3.down*4.9f*a*a;
 p.y=Mathf.Max(PivotWorld.y+.15f,p.y);d.SetPositionAndRotation(p,Quaternion.Euler(a*(100+i*43),a*(70+i*29),a*37)*debrisRotation[i]);
 }
 explosionRoot.position=ImpactWorld;explosionRoot.localScale=Vector3.one*explosionScale;
 float previousFx=fxTime;
 if(age<0){if(fxTime>=0)foreach(var ps in layers)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);fxTime=-1;}
 else{
 bool reset=fxTime<0||age<fxTime;
 foreach(var ps in layers){if(reset)ps.Simulate(age,true,true,true);else if(age>fxTime)ps.Simulate(age-fxTime,true,false,true);ps.Pause(true);}fxTime=age;
 }
 if(flashLight){flashLight.intensity=age>=0?Mathf.Exp(-age*13)*180:0;flashLight.transform.position=ImpactWorld+Vector3.up*1.5f;}
 if(Application.isPlaying&&explosionAudio&&lastTime<impactTime&&time>=impactTime&&time-lastTime<.25f)explosionAudio.Play();
 foreach(var blast in chainBlasts){
 float elapsed=age-blast.delay,old=previousFx-blast.delay;
 blast.root.position=PointAtAge(blast.delay,blast.localPoint);blast.root.localScale=Vector3.one*explosionScale;
 foreach(var r in blast.missileParts)if(r)r.enabled=elapsed<0;
 foreach(var ps in blast.particles){if(elapsed<0){ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);continue;}bool reset=old<0||age<previousFx;ps.Simulate(reset?elapsed:Mathf.Max(0,age-previousFx),true,reset,true);ps.Pause(true);}
 if(blast.light){blast.light.transform.position=blast.root.position+Vector3.up;blast.light.intensity=elapsed>=0?130*Mathf.Exp(-elapsed*12):0;}
 if(Application.isPlaying&&blast.audio&&lastTime<impactTime+blast.delay&&time>=impactTime+blast.delay&&time-lastTime<.25f)blast.audio.Play();
 }
 lastTime=time;
 if(crashCamera){
 float rising=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.05f,1.1f,age));
 Vector3 target=Vector3.Lerp(car.TransformPoint(impactLocal),car.position+Vector3.up*1.2f,rising);
 // Start beside the incoming bullet's destination, then reveal the whole car by dollying back.
 float cutTime=Mathf.Max(0,previewTime-cutStartTime);
 Vector3 pos=PointAtAge(cutStartTime-impactTime,impactLocal)+Vector3.up*impactCameraHeight+forward*(impactCameraDistance+cameraPullback.Evaluate(cutTime));
 float shake=age>=0?cameraShake*Mathf.Exp(-age*3):0;
 pos+=new Vector3(Mathf.Sin(age*79),Mathf.Sin(age*103),0)*shake;
 crashCamera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(target-pos,Vector3.up));
 crashCamera.Lens.FieldOfView=Mathf.Lerp(32,45,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,1.1f,cutTime)));
 }
 bool crash=previewTime>=9.7f; if(gunCamera)gunCamera.Priority=previewTime<8.4f?500:0;if(bulletCamera)bulletCamera.Priority=!crash&&previewTime>=8.4f?500:0;if(crashCamera)crashCamera.Priority=crash?600:0;
 }
 public void ResetShot(){if(director){director.time=0;director.Evaluate();}Evaluate(cutOnly?cutStartTime:0);}
 void OnDisable(){if(layers!=null)foreach(var p in layers)if(p)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);foreach(var b in chainBlasts){foreach(var p in b.particles)if(p)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(b.light)b.light.intensity=0;}if(flashLight)flashLight.intensity=0;}
}
