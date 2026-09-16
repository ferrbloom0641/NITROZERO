using UnityEngine;

namespace Damin.VFX.AirDistortion.Reusable
{
    [DefaultExecutionOrder(-100),DisallowMultipleComponent]
    [AddComponentMenu("DAMIN/VFX/Air Preview Flight (Test Only)")]
    public sealed class AirPreviewFlight : MonoBehaviour
    {
        public ReusableBulletAirDistortion AirVFX;
        [Min(0),InspectorName("테스트 이동 속도 (m/s)")] public float MoveSpeed=.72f;
        [Min(.01f),InspectorName("이동 거리 (m)")] public float FlightDistance=4;
        [InspectorName("이동 방향 (VFX 로컬)")] public Vector3 Direction=Vector3.right;
        [InspectorName("프리팹 복사본 시작 위치 (VFX 로컬)")] public Vector3 SpawnOffset;
        [InspectorName("반복 재생")] public bool Loop=true;
        [Min(0),InspectorName("반복 대기 시간")] public float ReplayDelay=.8f;
        [InspectorName("모형을 이동 방향으로 정렬")] public bool OrientAlongMotion=true;
        [InspectorName("정렬 앞뒤 반전")] public bool ReverseModelForward;
        [InspectorName("OFF 시 씬 총알의 원래 위치 복원")] public bool RestoreOnDisable=true;
        public string Status {get;private set;}="";
        public bool ManualSimulation {get;set;}
        Transform source,moving;
        GameObject clone;
        Vector3 originalPosition,start;
        Quaternion originalRotation;
        float travelled,wait;
        bool initialized,finished;
        void OnEnable(){initialized=false;}
        void Update(){if(!ManualSimulation)Simulate(Time.deltaTime);}
        Vector3 WorldDirection=>transform.TransformDirection(Direction.sqrMagnitude>.0001f?Direction.normalized:Vector3.right).normalized;

        public void Simulate(float dt)
        {
            if(!Application.isPlaying||!isActiveAndEnabled)return;
            if(!AirVFX)AirVFX=GetComponent<ReusableBulletAirDistortion>();
            if(!AirVFX){Status="같은 프리팹의 왜곡 컴포넌트가 필요합니다.";return;}
            if(initialized&&AirVFX.Target!=moving){Release();}
            if(!initialized&&!Begin())return;
            if(!moving||!moving.gameObject.activeInHierarchy)return;
            dt=Mathf.Max(0,dt);
            if(finished)
            {
                if(!Loop)return;
                wait+=dt;if(wait<ReplayDelay)return;
                travelled=wait=0;finished=false;moving.position=start;AirVFX.ClearTrail();
            }
            travelled=Mathf.Min(FlightDistance,travelled+MoveSpeed*dt);
            moving.position=start+WorldDirection*travelled;
            if(travelled>=FlightDistance){finished=true;wait=0;}
        }
        bool Begin()
        {
            source=AirVFX.Target;
            if(!source){Status="왜곡의 따라갈 총알을 연결하세요.";return false;}
            if(source==transform||transform.IsChildOf(source)){Status="총알과 VFX는 별도 오브젝트로 배치하세요.";return false;}
            bool asset=!source.gameObject.scene.IsValid();
            if(asset)
            {
                // Do not run somebody else's gameplay scripts just to preview a model.
                if(source.GetComponentsInChildren<MonoBehaviour>(true).Length>0||source.GetComponentsInChildren<Rigidbody>(true).Length>0||source.GetComponentsInChildren<Camera>(true).Length>0)
                {Status="이 총알 프리팹에는 동작 컴포넌트가 있습니다. 씬의 테스트용 총알을 연결하세요. 원본은 변경하지 않았습니다.";return false;}
                clone=Instantiate(source.gameObject);clone.name=source.name+"_AirPreview_RuntimeOnly";
                clone.hideFlags=HideFlags.DontSave;moving=clone.transform;moving.position=transform.TransformPoint(SpawnOffset);
                foreach(var collider in clone.GetComponentsInChildren<Collider>(true))collider.enabled=false;
                clone.SetActive(true);AirVFX.SetTarget(moving);
            }
            else
            {
                // Preview motion must never compete with Timeline/gameplay motion.
                // A scene target with an Animator or another enabled behaviour is considered owned.
                if (source.GetComponentInParent<Animator>() != null || HasExternalMotionOwner(source))
                {
                    Status="Timeline/이동 스크립트가 제어하는 총알입니다. 테스트 이동을 끄고 왜곡만 사용하세요.";
                    return false;
                }
                var body=source.GetComponentInChildren<Rigidbody>();
                if(body&&!body.isKinematic){Status="물리 이동 중인 총알입니다. 테스트용 모형을 사용하거나 테스트 이동을 끄세요.";return false;}
                moving=source;
            }
            originalPosition=moving.position;originalRotation=moving.rotation;start=moving.position;
            if(OrientAlongMotion)
            {
                Vector3 axis=LongestAxis(moving)*(ReverseModelForward?-1:1);
                moving.rotation=Quaternion.FromToRotation(moving.TransformDirection(axis),WorldDirection)*moving.rotation;
            }
            travelled=wait=0;finished=false;initialized=true;AirVFX.ClearTrail();Status=asset?"총알 원본은 유지 · 실행 중 테스트 복사본 이동":"씬 총알 테스트 이동 중 · 게임 이동 코드와 동시 사용 금지";
            return true;
        }
        static bool HasExternalMotionOwner(Transform target)
        {
            foreach(var behaviour in target.GetComponents<MonoBehaviour>())
            {
                if(behaviour&&behaviour.enabled&&!(behaviour is ReusableBulletAirDistortion)&&!(behaviour is AirPreviewFlight)&&!(behaviour is AirPreviewCameraFollow))
                    return true;
            }
            return false;
        }
        static Vector3 LongestAxis(Transform target)
        {
            Bounds bounds=default;bool found=false;
            foreach(var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if(!(renderer is MeshRenderer)&&!(renderer is SkinnedMeshRenderer))continue;
                var b=renderer.localBounds;
                for(int i=0;i<8;i++)
                {
                    var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    p=target.InverseTransformPoint(renderer.transform.TransformPoint(p));
                    if(found)bounds.Encapsulate(p);else{bounds=new Bounds(p,Vector3.zero);found=true;}
                }
            }
            Vector3 s=bounds.size;return !found?Vector3.forward:s.x>=s.y&&s.x>=s.z?Vector3.right:s.y>=s.z?Vector3.up:Vector3.forward;
        }
        [ContextMenu("테스트 다시 시작")]
        public void Restart(){if(!Application.isPlaying)return;Release();if(isActiveAndEnabled)Begin();}
        void Release()
        {
            if(clone)
            {
                if(AirVFX&&AirVFX.Target==moving)AirVFX.SetTarget(source);
                Destroy(clone);
            }
            else if(initialized&&moving&&RestoreOnDisable){moving.SetPositionAndRotation(originalPosition,originalRotation);if(AirVFX&&AirVFX.Target==moving)AirVFX.ClearTrail();}
            clone=null;source=null;moving=null;initialized=false;Status="";
        }
        void OnDisable(){Release();}
    }
}
