using UnityEngine;

namespace YUJEONG
{
    /// <summary>
    /// [02YUJEONG 전용] CM_Shot02 독립형 카메라 진동(Camera Shake) 컨트롤러
    /// - 기준 좌표(Anchor Pose) 기반 연산으로 프레임 누적에 의한 카메라 상승(위로 올라감) 현상을 원천 차단합니다.
    /// - 위치 흔들림(Position)과 각도 흔들림(Rotation)을 완벽 분리하여 인스펙터에서 자유롭게 튜닝할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class CMShot02CameraShake : MonoBehaviour
    {
        [Header("[ 📳 카메라 진동 마스터 스위치 ]")]
        [Tooltip("체크 해제 시 즉시 진동이 꺼지고 카메라가 원래 기준 위치/각도로 정확히 복귀합니다.")]
        public bool enableShake = true;

        [Header("[ 🎛️ 진동 강도 & 빠르기 설정 ]")]
        [Tooltip("전체 진동 강도 배율 (0: 끔, 1: 보통, 2: 격렬함)")]
        [Range(0f, 3f)]
        public float shakeIntensity = 0.6f;

        [Tooltip("진동 떨림 속도 (주행 속도감, 기본 14 권장)")]
        [Range(1f, 40f)]
        public float shakeFrequency = 14f;

        [Header("[ 📍 위치 흔들림 (Position Noise) ]")]
        [Tooltip("좌우(X), 상하(Y), 앞뒤(Z) 최대 이동 거리 (카메라가 상하로 안 올라가게 하려면 Y를 0으로 두세요)")]
        public Vector3 positionAmount = new Vector3(0.015f, 0f, 0.01f);

        [Header("[ 🔄 각도 흔들림 (Rotation Noise) - 추천! ]")]
        [Tooltip("상하 끄덕임(X), 좌우 도리도리(Y), 화면 롤(Z) 최대 회전 각도 (제자리에서 박진감 있게 흔들림)")]
        public Vector3 rotationAmount = new Vector3(0.45f, 0.4f, 0.75f);

        [Header("[ 💥 순간 임팩트 충격 펄스 ]")]
        [Tooltip("부딪히거나 차선 변경 시 일시적으로 쿵 흔들리는 여진 감쇠 속도")]
        [Range(1f, 10f)]
        public float impactDecaySpeed = 4f;

        [Header("[ ⚓ 고정 기준 좌표 (누적 상승 방지) ]")]
        [Tooltip("카메라가 복귀할 고정 기준 로컬 좌표")]
        public Vector3 baseLocalPosition = new Vector3(-1.5f, 2.15f, 14.46f);
        public Vector3 baseLocalEulerAngles = new Vector3(9.807f, -187.211f, -4.033f);
        [SerializeField] private bool hasBasePose = false;

        // 내부 노이즈 시드 및 상태
        private float noiseSeedX;
        private float noiseSeedY;
        private float noiseSeedZ;
        private float currentImpact = 0f;
        private bool isShakingNow = false;

        private void Awake()
        {
            InitBasePose();
        }

        private void OnEnable()
        {
            noiseSeedX = Random.Range(0f, 100f);
            noiseSeedY = Random.Range(100f, 200f);
            noiseSeedZ = Random.Range(200f, 300f);
            InitBasePose();
        }

        private void OnDisable()
        {
            ResetToBasePose();
        }

        public void InitBasePose()
        {
            if (!hasBasePose)
            {
                baseLocalPosition = transform.localPosition;
                baseLocalEulerAngles = transform.localEulerAngles;
                hasBasePose = true;
            }
        }

        [ContextMenu("📍 현재 위치를 진동 기준점으로 저장")]
        public void SetCurrentAsBasePose()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalEulerAngles = transform.localEulerAngles;
            hasBasePose = true;
            Debug.Log($"[CMShot02CameraShake] 새로운 기준점 저장 완료: Pos={baseLocalPosition}, Rot={baseLocalEulerAngles}");
        }

        public void ResetToBasePose()
        {
            if (hasBasePose)
            {
                transform.localPosition = baseLocalPosition;
                transform.localRotation = Quaternion.Euler(baseLocalEulerAngles);
                isShakingNow = false;
            }
        }

        private void LateUpdate()
        {
            if (!hasBasePose)
            {
                InitBasePose();
            }

            // 진동이 꺼져있거나 강도가 0이면 기준 좌표로 복귀 후 종료
            if (!enableShake || shakeIntensity <= 0.0001f)
            {
                if (isShakingNow)
                {
                    ResetToBasePose();
                }
                currentImpact = 0f;
                return;
            }

            float time = Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup;
            float dt = Application.isPlaying ? Time.deltaTime : 0.0333f;

            // 임팩트 감쇠 계산
            if (currentImpact > 0.001f)
            {
                currentImpact = Mathf.MoveTowards(currentImpact, 0f, dt * impactDecaySpeed);
            }

            // Perlin 노이즈 기반 부드러운 주행 진동 연산
            float t = time * shakeFrequency;
            float totalPower = shakeIntensity + currentImpact;

            float nx = (Mathf.PerlinNoise(noiseSeedX, t) * 2f - 1f);
            float ny = (Mathf.PerlinNoise(noiseSeedY, t) * 2f - 1f);
            float nz = (Mathf.PerlinNoise(noiseSeedZ, t) * 2f - 1f);

            float rx = (Mathf.PerlinNoise(noiseSeedX + 50f, t) * 2f - 1f);
            float ry = (Mathf.PerlinNoise(noiseSeedY + 50f, t) * 2f - 1f);
            float rz = (Mathf.PerlinNoise(noiseSeedZ + 50f, t) * 2f - 1f);

            // 최종 오프셋 계산
            Vector3 posOffset = new Vector3(
                nx * positionAmount.x,
                ny * positionAmount.y,
                nz * positionAmount.z
            ) * totalPower;

            Quaternion rotOffset = Quaternion.Euler(
                rx * rotationAmount.x * totalPower,
                ry * rotationAmount.y * totalPower,
                rz * rotationAmount.z * totalPower
            );

            // [핵심] 이전 프레임에 누적 더하기/빼기를 하지 않고, 항상 '고정 기준 좌표(basePose)'에서만 오프셋을 더함!
            // 이로써 수천 프레임이 지나도 카메라가 위로 떠오르거나 위치가 영구 이동하는 현상이 100% 방지됩니다.
            transform.localPosition = baseLocalPosition + posOffset;
            transform.localRotation = Quaternion.Euler(baseLocalEulerAngles) * rotOffset;
            isShakingNow = true;
        }

        [ContextMenu("💥 순간 충격 흔들림 테스트 (Impact)")]
        public void TriggerImpact()
        {
            TriggerImpact(1.0f);
        }

        public void TriggerImpact(float force)
        {
            currentImpact = Mathf.Max(currentImpact, force);
        }
    }
}
