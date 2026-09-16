using UnityEngine;

namespace YUJEONG
{
    /// <summary>
    /// [02YUJEONG 전용] 앞바퀴 조향 각도 연동 자동 스키드마크 컨트롤러
    /// - 차량 자체 콜라이더(브레이크 캘리퍼, 차체 등)를 자동 무시하여 공중으로 튀는 지그재그 도미노 현상을 원천 차단합니다.
    /// - ±8도 이상 꺾일 때 넉넉하게 발동하며, 두껍고 매끄러운 아스팔트 바닥 밀착 자국을 생성합니다.
    /// - Z-Write 및 RenderQueue 최적화로 뒤따라오는 차량(파란차) 밑으로 자연스럽게 가려집니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1020)]
    public class SimpleSkidmarkController : MonoBehaviour
    {
        [Header("[ 🏁 스키드마크 ON / OFF 스위치 ]")]
        [Tooltip("체크 해제 시 스키드마크 생성이 완전히 꺼집니다.")]
        public bool enableSkidmarks = true;

        [Header("[ 📐 조향 각도 조건 설정 ]")]
        [Tooltip("앞 타이어가 이 각도(기본 8도) 이상 꺾였을 때 스키드마크 시작")]
        [Range(1f, 40f)]
        public float steerAngleThreshold = 8f;

        [Tooltip("한 번 켜진 후 꺼지는 하한 각도 (기본 5도, 부드러운 감속 유지)")]
        [Range(1f, 30f)]
        public float steerOffThreshold = 5f;

        [Tooltip("한 번 시작되면 최소한 유지되는 시간 (초, 넉넉한 지속)")]
        [Range(0.1f, 2f)]
        public float minSustainTime = 0.6f;

        [Tooltip("현재 실시간 감지된 앞바퀴 조향 각도")]
        [SerializeField] private float currentSteerAngle = 0f;
        [Tooltip("현재 스키드마크 분사 중 여부")]
        [SerializeField] private bool isEmitting = false;

        [Header("[ 🛞 바퀴 피벗 참조 (자동 탐색) ]")]
        public Transform frontSteerWheel;
        public Transform frontSteerWheelR;
        public Transform wheelRL;
        public Transform wheelRR;

        [Header("[ 🎨 타이어 자국 외형 설정 ]")]
        [Tooltip("타이어 자국 폭 (미터, 더 굵고 선명하게 0.42m)")]
        [Range(0.1f, 1.0f)]
        public float tireWidth = 0.42f;

        [Tooltip("자국이 도로에 남아있는 시간 (초 단위)")]
        [Range(0.5f, 10f)]
        public float markLifeTime = 3.5f;

        [Tooltip("타이어 자국 검은색 진하기 (불투명도)")]
        [Range(0.1f, 1f)]
        public float markOpacity = 0.88f;

        [Tooltip("도로 표면에서 살짝 띄우는 높이 (m, Z-파이팅 및 메쉬 파고듦 방지)")]
        [Range(0.005f, 0.04f)]
        public float roadOffset = 0.015f;

        // 월드 공간에서 지면을 추적하는 독립 이미터
        private GameObject emitterRoot;
        private Transform emitterRL;
        private Transform emitterRR;
        private TrailRenderer trailRL;
        private TrailRenderer trailRR;
        private Material skidMaterial;

        // 끊김 방지 타이머
        private float emitTimer = 0f;

        // 지면 높이 보정용 이전 유효 Y값
        private float lastValidY_RL = float.NaN;
        private float lastValidY_RR = float.NaN;

        private void Awake()
        {
            AutoFindWheels();
            SetupSkidTrails();
        }

        private void OnEnable()
        {
            AutoFindWheels();
            SetupSkidTrails();
        }

        public void ApplyRecommendedSettings()
        {
            steerAngleThreshold = 8f;
            steerOffThreshold = 5f;
            minSustainTime = 0.6f;
            tireWidth = 0.42f;
            roadOffset = 0.015f;
            markOpacity = 0.88f;
            markLifeTime = 3.5f;
        }

        public void AutoFindWheels()
        {
            if (frontSteerWheel == null) frontSteerWheel = FindDeepChild(transform, "FL_Steering");
            if (frontSteerWheel == null) frontSteerWheel = FindDeepChild(transform, "FL");

            if (frontSteerWheelR == null) frontSteerWheelR = FindDeepChild(transform, "FR_Steering");
            if (frontSteerWheelR == null) frontSteerWheelR = FindDeepChild(transform, "FR");

            if (wheelRL == null) wheelRL = FindDeepChild(transform, "RL_Rolling");
            if (wheelRL == null) wheelRL = FindDeepChild(transform, "RL");

            if (wheelRR == null) wheelRR = FindDeepChild(transform, "RR_Rolling");
            if (wheelRR == null) wheelRR = FindDeepChild(transform, "RR");
        }

        private void SetupSkidTrails()
        {
            CleanupOldEmitters();

            if (skidMaterial == null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (s != null)
                {
                    skidMaterial = new Material(s);
                    skidMaterial.name = "M_TireSkidmark_Smooth";
                    Color col = new Color(0.04f, 0.04f, 0.04f, markOpacity);
                    if (skidMaterial.HasProperty("_BaseColor")) skidMaterial.SetColor("_BaseColor", col);
                    if (skidMaterial.HasProperty("_Color")) skidMaterial.SetColor("_Color", col);

                    // 양면 렌더링 지원 (카메라 각도 상관없이 항상 렌더링)
                    if (skidMaterial.HasProperty("_Cull")) skidMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

                    // 반투명 블렌딩
                    if (skidMaterial.HasProperty("_Surface")) skidMaterial.SetFloat("_Surface", 1);
                    if (skidMaterial.HasProperty("_Blend")) skidMaterial.SetFloat("_Blend", 0);
                    if (skidMaterial.HasProperty("_SrcBlend")) skidMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (skidMaterial.HasProperty("_DstBlend")) skidMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

                    // 파란차 등 후속 차량 아래로 자연스럽게 가려지도록 ZWrite 활성화
                    skidMaterial.renderQueue = 2450;
                    if (skidMaterial.HasProperty("_ZWrite")) skidMaterial.SetInt("_ZWrite", 1);
                    if (skidMaterial.HasProperty("_ZTest")) skidMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                }
            }

            if (emitterRoot == null)
            {
                emitterRoot = new GameObject($"{gameObject.name}_SkidEmitters");
                emitterRoot.hideFlags = HideFlags.DontSaveInEditor;
            }

            if (emitterRL == null)
            {
                trailRL = CreateFlatTrail("Emitter_RL", out emitterRL);
            }

            if (emitterRR == null)
            {
                trailRR = CreateFlatTrail("Emitter_RR", out emitterRR);
            }
        }

        private void CleanupOldEmitters()
        {
            Transform old1 = transform.Find("SkidEmitter_Ground_RL");
            Transform old2 = transform.Find("SkidEmitter_Ground_RR");
            if (old1 != null) DestroyImmediate(old1.gameObject);
            if (old2 != null) DestroyImmediate(old2.gameObject);

            if (wheelRL != null)
            {
                Transform old = wheelRL.Find("Skid_Emitter_RL");
                if (old != null) DestroyImmediate(old.gameObject);
            }
            if (wheelRR != null)
            {
                Transform old = wheelRR.Find("Skid_Emitter_RR");
                if (old != null) DestroyImmediate(old.gameObject);
            }
        }

        private TrailRenderer CreateFlatTrail(string name, out Transform trans)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(emitterRoot.transform, false);
            trans = go.transform;

            TrailRenderer tr = go.AddComponent<TrailRenderer>();
            tr.time = markLifeTime;
            tr.startWidth = tireWidth;
            tr.endWidth = tireWidth;
            tr.autodestruct = false;
            tr.emitting = false;

            // 정밀한 버텍스 간격 (부드러운 곡선)
            tr.minVertexDistance = 0.05f;

            tr.alignment = LineAlignment.TransformZ;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.generateLightingData = false;

            if (skidMaterial != null) tr.material = skidMaterial;

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.04f, 0.04f, 0.04f), 0f), new GradientColorKey(new Color(0.04f, 0.04f, 0.04f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(markOpacity, 0f), new GradientAlphaKey(markOpacity * 0.85f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            tr.colorGradient = grad;

            return tr;
        }

        private void LateUpdate()
        {
            if (frontSteerWheel == null || wheelRL == null || wheelRR == null)
            {
                AutoFindWheels();
                if (frontSteerWheel == null) return;
            }

            float dt = Application.isPlaying ? Time.deltaTime : 0.0333f;
            if (dt <= 0.0001f) dt = 0.0333f;

            // 1. 앞바퀴 조향 각도 측정 (FL 및 FR 양쪽 중 최대 각도 감지)
            float angleL = frontSteerWheel != null ? Mathf.Abs(Mathf.DeltaAngle(0f, frontSteerWheel.localEulerAngles.y)) : 0f;
            float angleR = frontSteerWheelR != null ? Mathf.Abs(Mathf.DeltaAngle(0f, frontSteerWheelR.localEulerAngles.y)) : 0f;
            currentSteerAngle = Mathf.Max(angleL, angleR);

            // 2. 도미노 깜빡임 방지 (스마트 히스테리시스 & 넉넉한 지속시간)
            if (enableSkidmarks)
            {
                if (currentSteerAngle >= steerAngleThreshold)
                {
                    // 8도 이상 꺾이면 무조건 발동 + 타이머 충전
                    isEmitting = true;
                    emitTimer = minSustainTime;
                }
                else if (isEmitting)
                {
                    // 8도 미만으로 복귀해도, 타이머가 남아있거나 5도 이상이면 끊기지 않고 부드럽게 유지
                    emitTimer -= dt;
                    if (emitTimer <= 0f && currentSteerAngle < steerOffThreshold)
                    {
                        isEmitting = false;
                    }
                }
            }
            else
            {
                isEmitting = false;
                emitTimer = 0f;
            }

            // 3. 도로 노면 정밀 추적 (차체/캘리퍼 관통 및 공중 튐 원천 방지)
            AlignEmitterToRoad(wheelRL, emitterRL, trailRL, ref lastValidY_RL);
            AlignEmitterToRoad(wheelRR, emitterRR, trailRR, ref lastValidY_RR);
        }

        private void AlignEmitterToRoad(Transform wheel, Transform emitter, TrailRenderer trail, ref float lastValidY)
        {
            if (wheel == null || emitter == null) return;

            // 바퀴 피벗 상단 40cm 지점에서 레이캐스트
            Vector3 origin = wheel.position + Vector3.up * 0.4f;
            Vector3 groundNormal = Vector3.up;
            bool hitGround = false;
            Vector3 targetPos = wheel.position;

            // 차량 자체의 콜라이더(캘리퍼, 차체, 날개 등)를 필터링하여 오직 바닥 도로에만 밀착
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 5.0f, ~0, QueryTriggerInteraction.Ignore);
            float closestDist = float.MaxValue;
            RaycastHit bestHit = default;

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                // 1. 내 차량의 모든 하위 콜라이더 무시
                if (h.transform.IsChildOf(transform)) continue;

                // 2. 다른 차량 및 캘리퍼 부품 콜라이더 무시
                if (h.collider.CompareTag("Player") || h.collider.name.Contains("Car") || h.collider.name.Contains("Caliper"))
                    continue;

                // 3. 수직 벽면(normal.y < 0.4) 무시하고 완만한 도로 표면만 채택
                if (h.normal.y < 0.4f) continue;

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    bestHit = h;
                    hitGround = true;
                }
            }

            if (hitGround)
            {
                targetPos = bestHit.point + bestHit.normal * roadOffset;
                groundNormal = bestHit.normal;
                lastValidY = targetPos.y;
            }
            else
            {
                // 레이캐스트가 순간적으로 누락될 경우 직전 유효 Y 유지 (급격한 높이 튐 방지)
                if (!float.IsNaN(lastValidY))
                {
                    targetPos = new Vector3(wheel.position.x, lastValidY, wheel.position.z);
                }
                else
                {
                    targetPos.y = wheel.position.y - 0.32f + roadOffset;
                }
            }

            emitter.position = targetPos;

            // 차량 전방 벡터를 도로 노면에 투영하여 완벽하게 지면에 평평하게 밀착
            Vector3 forwardDir = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            if (forwardDir.sqrMagnitude < 0.001f) forwardDir = transform.forward;
            emitter.rotation = Quaternion.LookRotation(-groundNormal, forwardDir);

            if (trail != null)
            {
                trail.emitting = isEmitting;
                if (Mathf.Abs(trail.startWidth - tireWidth) > 0.001f)
                {
                    trail.startWidth = tireWidth;
                    trail.endWidth = tireWidth;
                }
            }
        }

        private void OnDestroy()
        {
            if (emitterRoot != null)
            {
                DestroyImmediate(emitterRoot);
            }
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null) return null;
            Transform direct = parent.Find(name);
            if (direct != null) return direct;
            foreach (Transform c in parent.GetComponentsInChildren<Transform>(true))
            {
                if (c.name == name) return c;
            }
            return null;
        }
    }
}
