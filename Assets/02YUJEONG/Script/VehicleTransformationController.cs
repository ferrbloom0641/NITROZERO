using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YUJEONG
{
    /// <summary>
    /// 차량 파츠 변형 시퀀스 제어 컨트롤러
    /// 숫자 키패드 및 숫자 상단키(1, 2, 3...)로 단계별 기믹을 테스트하고 제어할 수 있습니다.
    /// </summary>
    public class VehicleTransformationController : MonoBehaviour
    {
        [System.Serializable]
        public struct TransformPose
        {
            public Vector3 position;
            public Vector3 rotationEuler;

            public TransformPose(Vector3 pos, Vector3 rot)
            {
                position = pos;
                rotationEuler = rot;
            }
        }

        [Header("[ 제어 키 설정 ]")]
        [Tooltip("1단계: 보조 벤트 판 1, 2 작동 키")]
        public KeyCode triggerKey1 = KeyCode.Alpha1;
        public KeyCode triggerKeypad1 = KeyCode.Keypad1;

        [Tooltip("2단계: 3D 스캐너 전개 키")]
        public KeyCode triggerKey2 = KeyCode.Alpha2;
        public KeyCode triggerKeypad2 = KeyCode.Keypad2;

        [Tooltip("3단계: 연결부 1, 2 접기 키")]
        public KeyCode triggerKey3 = KeyCode.Alpha3;
        public KeyCode triggerKeypad3 = KeyCode.Keypad3;

        [Tooltip("4단계: 대형 단발 제트 부스터 노즐 돌출 키")]
        public KeyCode triggerKey4 = KeyCode.Alpha4;
        public KeyCode triggerKeypad4 = KeyCode.Keypad4;

        [Tooltip("5단계: 부품 메인상판덮개 개폐 키")]
        public KeyCode triggerKey5 = KeyCode.Alpha5;
        public KeyCode triggerKeypad5 = KeyCode.Keypad5;

        [Header("[ 전체 풀 변형 시퀀스 설정 (1->2->3->4+5) ]")]
        [Tooltip("전체 순차 변형 단축키 (스페이스바 또는 숫자 0)")]
        public KeyCode triggerKeyFullSequence = KeyCode.Space;
        public KeyCode triggerKeyFullSequenceAlt = KeyCode.Alpha0;

        [Tooltip("4번 노즐 돌출 시작 후 5번 상판 수납이 시작되기까지의 딜레이 (한 박자 늦게, 초 단위)")]
        [Range(0.02f, 0.3f)]
        public float stage5DelayAfterStage4 = 0.08f;

        [Header("[ UI 표시 설정 ]")]
        [Tooltip("화면에 변형 제어 UI 패널을 표시할지 여부 (체크 해제 시 화면에서 숨김, 단축키는 정상 작동)")]
        public bool showOnGUI = false;

        [Header("[ 🛠️ 에디터 미리보기 (게임 실행 안해도 확인) ]")]
        [Tooltip("체크 시 게임을 실행하지 않아도 최종 변신 상태를 씬 뷰에서 바로 확인할 수 있습니다.")]
        [SerializeField] private bool previewFinalTransformation = false;

        [Tooltip("변신 진행도를 0%(기본 원래 상태) ~ 100%(최종 변신 상태)로 조절합니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float previewProgress = 0f;

        // 우측 파츠 기본 포즈 상수 (안전 백업용)
        public static readonly Vector3 DefaultVentRightPos = new Vector3(0.41788054f, 0.16597676f, -1.1267076f);
        public static readonly Quaternion DefaultVentRightRot = Quaternion.Euler(-93.437f, 90f, 0f);

        public static readonly Vector3 DefaultCouplerRightPos = new Vector3(0.441f, -0.048f, -1.481f);
        public static readonly Quaternion DefaultCouplerRightRot = Quaternion.Euler(-90f, 0f, 0f);

        [Header("[ 대상 차량 루트 (기본: SportCar_4change_2) ]")]
        public Transform targetCarRoot;

        [Header("[ 1단계 대상: 보조 벤트 판 ]")]
        public Transform ventLeft;  // 부품_보조벤트판1
        public Transform ventRight; // 부품_보조벤트판2

        [Header("[ 2단계 대상: 3D 스캐너 ]")]
        public Transform scanner3D; // 3d스캐너

        [Header("[ 3단계 대상: 카울 연결부 1, 2 ]")]
        public Transform couplerLeft;  // 부품_연결부1
        public Transform couplerRight; // 부품_연결부2

        [Header("[ 3단계 대상: 기계식 상부 아머드 카울 ]")]
        public Transform armoredCowl;  // 부품_기계식상부아머드카울

        [Header("[ 4단계 대상: 대형 단발 제트 부스터 노즐 & 터빈 ]")]
        public Transform boosterNozzle; // 대형_단발_제트_부스터_노즐
        public Transform boosterTurbine; // 제트부스터_터빈
        public GameObject boosterNeon;   // 네온

        public enum TurbineAxis
        {
            [InspectorName("Y축 (권장: 원형 제자리 시계방향)")]
            Up_Y,
            [InspectorName("Y축 역방향 (권장: 원형 제자리 반시계방향)")]
            Down_NegY,
            [InspectorName("X축")]
            Right_X,
            [InspectorName("X축 역방향")]
            Left_NegX,
            [InspectorName("Z축")]
            Forward_Z,
            [InspectorName("Z축 역방향")]
            Back_NegZ
        }

        [Header("[ 제트부스터 터빈 회전 설정 ]")]
        [Tooltip("터빈 회전 축 (FBX 피벗 특성상 원형 제자리 회전은 Y축 또는 Y축 역방향입니다)")]
        public TurbineAxis turbineAxis = TurbineAxis.Up_Y;

        [Tooltip("터빈 최고 회전 속도 (도/초, 1800도 = 초당 5바퀴)")]
        [Range(360f, 7200f)]
        public float maxTurbineSpeed = 1800f;

        [Tooltip("엔진 기동 가속 시간 (천천히->빠르게 가속되는 시간, 초)")]
        [Range(0.5f, 5.0f)]
        public float spoolUpDuration = 1.8f;

        [Tooltip("엔진 정지 감속 시간 (빠르게->천천히 멈추는 시간, 초)")]
        [Range(0.5f, 5.0f)]
        public float spoolDownDuration = 1.2f;

        [Tooltip("가속 곡선 (엔진 시동 걸리듯 서서히 가속)")]
        public AnimationCurve spoolUpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("감속 곡선 (서서히 정지)")]
        public AnimationCurve spoolDownCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("[ 제트부스터 네온 스플라인 순차 점등 연출 (빨강->파랑->노랑->주황) ]")]
        [Tooltip("1단계 점등: 빨강 스플라인")]
        public GameObject splineRed;
        [Tooltip("2단계 점등: 파랑 스플라인")]
        public GameObject splineBlue;
        [Tooltip("3단계 점등: 노랑 스플라인")]
        public GameObject splineYellow;
        [Tooltip("4단계 점등: 주황 스플라인")]
        public GameObject splineOrange;

        [Tooltip("스플라인 간 점등 딜레이 (초, 빨강 후 파랑, 파랑 후 노랑...)")]
        [Range(0.05f, 1.0f)]
        public float splineInterval = 0.28f;

        [Tooltip("각 스플라인이 서서히 밝아지며 본래 색으로 진해지는 시간 (초)")]
        [Range(0.1f, 2.0f)]
        public float splineGlowDuration = 0.5f;

        [Tooltip("부스터 수납 시 빛이 꺼지는 페이드 아웃 시간 (초)")]
        [Range(0.1f, 1.5f)]
        public float splineFadeOutDuration = 0.4f;

        [Header("[ 5단계 대상: 부품 메인상판덮개 ]")]
        public Transform mainCover;     // 부품_메인상판덮개

        [Header("[ 🚀 부스터 VFX 이펙트 연동 (pf_redbooster) ]")]
        [Tooltip("부스터 이펙트 최상위 그룹 오브젝트 (Booster_effect)")]
        public GameObject boosterEffectRoot;

        [Tooltip("메인 대형 제트 부스터 이펙트 (대형 단발 제트 부스터 노즐에 마지막 주황색 네온이 들어올 때 점등)")]
        public GameObject boosterFxMain; // pf_redbooster_1

        [Tooltip("보조 부스터 이펙트 4종 (부스터 변형 시작과 동시에 4개 모두 점등)")]
        public GameObject[] boosterFxSub = new GameObject[4]; // pf_redbooster_2, (1), (2), (3)

        [Header("[ 🚀 시작 시 풀 변형 & 부스터 점등 설정 ]")]
        [Tooltip("게임 시작 시 처음부터 변형 및 모든 부스터 이펙트가 100% 완료된 상태로 시작")]
        public bool startWithFullTransformation = true;

        [Header("[ 바퀴 회전 컨트롤러 연동 옵션 ]")]
        [Tooltip("바퀴 회전 제어 스크립트")]
        public VehicleWheelSpinController wheelSpinController;

        [Tooltip("체크 시 부스터(Space/4번) 작동 시 바퀴도 자동으로 초고속 연동 (기본값: false, T/B 키로 각각 독립 조작)")]
        public bool syncWheelBoostWithBooster = false;

        [Header("[ 애니메이션 세부 설정 ]")]
        [Tooltip("각 단계별 기본 이동 소요 시간 (초)")]
        [Range(0.05f, 1.0f)]
        public float stepDuration = 0.2f;

        [Tooltip("메인상판덮개가 차 안으로 슬라이드 수납되는 시간 (초, 값이 클수록 천천히 부드럽게 들어감)")]
        [Range(0.1f, 1.5f)]
        public float coverRetractDuration = 0.45f;

        [Tooltip("단계 사이 기계적 딜레이 (철컥 느낌)")]
        [Range(0.0f, 0.5f)]
        public float stepPause = 0.05f;

        [Tooltip("기계적 움직임을 위한 가감속 커브")]
        public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("[ 보조벤트판1 (좌측) 4단계 포즈 ]")]
        [Tooltip("1단계: 원래 위치")]
        public TransformPose leftStep1 = new TransformPose(new Vector3(-0.44f, 0.168f, -1.126f), new Vector3(-85.53f, 90f, -180f));
        [Tooltip("2단계: 수직 상승")]
        public TransformPose leftStep2 = new TransformPose(new Vector3(-0.443f, 0.204f, -1.126f), new Vector3(-85.53f, 90f, -180f));
        [Tooltip("3단계: 안쪽 눕기 (틸트)")]
        public TransformPose leftStep3 = new TransformPose(new Vector3(-0.481f, 0.223f, -1.126f), new Vector3(-44.74f, 90f, -180f));
        [Tooltip("4단계: 메인상판덮개 안으로 완전 수납")]
        public TransformPose leftStep4 = new TransformPose(new Vector3(-0.441f, 0.262f, -1.126f), new Vector3(-44.74f, 90f, -180f));

        [Header("[ 3D 스캐너 3단계 포즈 ]")]
        [Tooltip("1단계: 원래 위치 (지붕 뒤쪽 대기)")]
        public TransformPose scannerStep1 = new TransformPose(new Vector3(0.003f, 0.243f, -0.883f), new Vector3(-108.4f, 0f, 0f));
        [Tooltip("2단계: 대각선 슬라이드 상승")]
        public TransformPose scannerStep2 = new TransformPose(new Vector3(0.003f, 0.324f, -0.641f), new Vector3(-108.4f, 0f, 0f));
        [Tooltip("3단계: 지붕 위 수평 전개 완료")]
        public TransformPose scannerStep3 = new TransformPose(new Vector3(0.003f, 0.341f, -0.419f), new Vector3(-90.38f, 0f, 0f));

        [Header("[ 연결부1 (좌측) 3단계 포즈 ]")]
        [Tooltip("1단계: 원래 위치 (펼쳐진 상태)")]
        public TransformPose couplerLeftStep1 = new TransformPose(new Vector3(-0.441f, -0.048f, -1.481f), new Vector3(-90f, 0f, 0f));
        [Tooltip("2단계: 80도 회전 접힘")]
        public TransformPose couplerLeftStep2 = new TransformPose(new Vector3(-0.392f, -0.048f, -1.534f), new Vector3(-90f, 0f, -80.13f));
        [Tooltip("3단계: 147도 완전 접힘")]
        public TransformPose couplerLeftStep3 = new TransformPose(new Vector3(-0.227f, -0.048f, -1.589f), new Vector3(-90f, 0f, -147.2f));

        [Header("[ 기계식 상부 아머드 카울 3단계 포즈 ]")]
        [Tooltip("1단계: 원래 위치")]
        public TransformPose cowlStep1 = new TransformPose(new Vector3(0.023f, -0.013f, -1.324f), new Vector3(-89.49f, 180f, 0f));
        [Tooltip("2단계: 수직 대폭 상승")]
        public TransformPose cowlStep2 = new TransformPose(new Vector3(0.023f, 0.164f, -1.327f), new Vector3(-89.49f, 180f, 0f));
        [Tooltip("3단계: 전방 살짝 틸트 및 완전 상승")]
        public TransformPose cowlStep3 = new TransformPose(new Vector3(0.023f, 0.184f, -1.253f), new Vector3(-80.23f, 180f, 0f));

        [Header("[ 대형 단발 제트 부스터 노즐 2단계 포즈 ]")]
        [Tooltip("1단계: 원래 대기 위치")]
        public TransformPose boosterStep1 = new TransformPose(new Vector3(0.016f, -0.306f, -1.088f), new Vector3(-89.65f, 0f, 90f));
        [Tooltip("2단계: 후방 돌출 발동 위치")]
        public TransformPose boosterStep2 = new TransformPose(new Vector3(0.017f, -0.305f, -1.251f), new Vector3(-89.65f, 0f, 90f));

        [Header("[ 부품 메인상판덮개 3단계 포즈 ]")]
        [Tooltip("1단계: 원래 닫힌 위치")]
        public TransformPose coverStep1 = new TransformPose(new Vector3(0.003f, 0.1718f, -1.244f), new Vector3(-90f, 180f, 0f));
        [Tooltip("2단계: 사용자 지정 오픈 대기 위치")]
        public TransformPose coverStep2 = new TransformPose(new Vector3(0.003f, 0.442f, -1.131f), new Vector3(-142.1f, 180f, 0f));
        [Tooltip("3단계: 차체 내부 완전 슬라이드 수납")]
        public TransformPose coverStep3 = new TransformPose(new Vector3(0.003f, -0.098f, -0.565f), new Vector3(-142.1f, 180f, 0f));

        // 3D 스캐너 상태 관리
        private bool isScannerDeployed = false;
        private Coroutine scannerCoroutine = null;

        // 연결부 상태 관리
        private bool isCouplerFolded = false;
        private Coroutine couplerCoroutine = null;

        // 아머드 카울 상태 관리
        private bool isCowlLifted = false;
        private Coroutine cowlCoroutine = null;

        // 제트 부스터 노즐 상태 관리
        private bool isBoosterDeployed = false;
        private Coroutine boosterCoroutine = null;

        // 제트 부스터 터빈 상태 관리
        private float currentTurbineSpeed = 0f;
        private Coroutine turbineCoroutine = null;
        private Quaternion initialTurbineRotation = Quaternion.identity;
        private bool hasStoredTurbineRot = false;
        private Transform turbineSpinPivot = null;

        // 스플라인 점등 상태 관리
        private Coroutine splineSequenceCoroutine = null;

        private class SplineGlowData
        {
            public GameObject obj;
            public MeshRenderer renderer;
            public Material material;
            public Color targetEmission;
            public Color targetBaseColor;
        }

        private SplineGlowData redGlowData;
        private SplineGlowData blueGlowData;
        private SplineGlowData yellowGlowData;
        private SplineGlowData orangeGlowData;

        // 메인 상판 덮개 상태 관리
        private bool isCoverOpened = false;
        private Coroutine coverCoroutine = null;

        // 전체 시퀀스 상태 관리
        private bool isFullSequenceRunning = false;
        private bool isFullyTransformed = false;
        private Coroutine fullSequenceCoroutine = null;

        // 내부 상태 관리
        private bool isVentHidden = false; // 현재 수납되었는지 여부 (토글용)
        private Coroutine ventCoroutine = null;

        public bool PreviewFinalTransformation
        {
            get => previewFinalTransformation;
            set
            {
                previewFinalTransformation = value;
                previewProgress = value ? 1f : 0f;
                lastPreviewFinalState = previewFinalTransformation;
                lastPreviewProgress = previewProgress;
                ApplyTransformationProgress(previewProgress);
            }
        }

        public float PreviewProgress
        {
            get => previewProgress;
            set
            {
                previewProgress = Mathf.Clamp01(value);
                previewFinalTransformation = previewProgress >= 0.999f;
                lastPreviewFinalState = previewFinalTransformation;
                lastPreviewProgress = previewProgress;
                ApplyTransformationProgress(previewProgress);
            }
        }

        [ContextMenu("🚀 최종 변신 모습 보기 (100%)")]
        public void ApplyFinalTransformationPreview()
        {
            PreviewFinalTransformation = true;
        }

        [ContextMenu("🔄 기본 원래 모습 복귀 (0%)")]
        public void ResetTransformationPreview()
        {
            PreviewFinalTransformation = false;
            SetSubBoosterFxActive(false);
            SetMainBoosterFxActive(false);
        }

        private bool lastPreviewFinalState = false;
        private float lastPreviewProgress = 0f;

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (boosterEffectRoot == null || boosterFxMain == null || boosterFxSub == null || boosterFxSub[0] == null)
                {
                    AutoBindBoosterFx();
                }

                if (previewFinalTransformation != lastPreviewFinalState)
                {
                    lastPreviewFinalState = previewFinalTransformation;
                    previewProgress = previewFinalTransformation ? 1f : 0f;
                    lastPreviewProgress = previewProgress;
                }
                else if (!Mathf.Approximately(previewProgress, lastPreviewProgress))
                {
                    lastPreviewProgress = previewProgress;
                    previewFinalTransformation = previewProgress >= 0.999f;
                    lastPreviewFinalState = previewFinalTransformation;
                }

                ApplyTransformationProgress(previewProgress);
            }
        }

        /// <summary>
        /// 에디터 또는 인스펙터 슬라이더/토글에서 변신 진행도(0% ~ 100%)를 실시간으로 파츠에 반영
        /// </summary>
        public void ApplyTransformationProgress(float t)
        {
            t = Mathf.Clamp01(t);

            if (ventLeft == null || scanner3D == null || couplerLeft == null || armoredCowl == null || boosterNozzle == null || mainCover == null)
            {
                AutoBindParts();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                List<UnityEngine.Object> targetsToRecord = new List<UnityEngine.Object>();
                if (ventLeft != null) targetsToRecord.Add(ventLeft);
                if (ventRight != null) targetsToRecord.Add(ventRight);
                if (scanner3D != null) targetsToRecord.Add(scanner3D);
                if (couplerLeft != null) targetsToRecord.Add(couplerLeft);
                if (couplerRight != null) targetsToRecord.Add(couplerRight);
                if (armoredCowl != null) targetsToRecord.Add(armoredCowl);
                if (boosterNozzle != null) targetsToRecord.Add(boosterNozzle);
                if (mainCover != null) targetsToRecord.Add(mainCover);
                if (targetsToRecord.Count > 0)
                {
                    UnityEditor.Undo.RecordObjects(targetsToRecord.ToArray(), "Vehicle Transformation Preview");
                }
            }
#endif

            // 1. 3D 스캐너
            if (scanner3D != null)
            {
                Vector3 defPos = scannerStep1.position;
                Quaternion defRot = Quaternion.Euler(scannerStep1.rotationEuler);
                Vector3 transPos = scannerStep3.position;
                Quaternion transRot = Quaternion.Euler(scannerStep3.rotationEuler);

                scanner3D.localPosition = Vector3.Lerp(defPos, transPos, t);
                scanner3D.localRotation = Quaternion.Slerp(defRot, transRot, t);
            }

            // 2. 기계식 상부 아머드 카울
            if (armoredCowl != null)
            {
                Vector3 defPos = cowlStep1.position;
                Quaternion defRot = Quaternion.Euler(cowlStep1.rotationEuler);
                Vector3 transPos = cowlStep3.position;
                Quaternion transRot = Quaternion.Euler(cowlStep3.rotationEuler);

                armoredCowl.localPosition = Vector3.Lerp(defPos, transPos, t);
                armoredCowl.localRotation = Quaternion.Slerp(defRot, transRot, t);
            }

            // 3. 카울 연결부 1, 2
            Vector3 couplerLDefPos = couplerLeftStep1.position;
            Quaternion couplerLDefRot = Quaternion.Euler(couplerLeftStep1.rotationEuler);

            Vector3 cowlFinalDeltaPos = cowlStep3.position - cowlStep1.position;
            Quaternion cowlFinalDeltaRot = Quaternion.Euler(cowlStep3.rotationEuler) * Quaternion.Inverse(Quaternion.Euler(cowlStep1.rotationEuler));
            Vector3 couplerLTransPos = couplerLeftStep3.position + cowlFinalDeltaPos;
            Quaternion couplerLTransRot = cowlFinalDeltaRot * Quaternion.Euler(couplerLeftStep3.rotationEuler);

            if (couplerLeft != null)
            {
                couplerLeft.localPosition = Vector3.Lerp(couplerLDefPos, couplerLTransPos, t);
                couplerLeft.localRotation = Quaternion.Slerp(couplerLDefRot, couplerLTransRot, t);
            }
            if (couplerRight != null)
            {
                if (couplerLeft != null)
                {
                    couplerRight.localPosition = new Vector3(-couplerLeft.localPosition.x, couplerLeft.localPosition.y, couplerLeft.localPosition.z);
                    Vector3 leftEuler = couplerLeft.localEulerAngles;
                    couplerRight.localEulerAngles = new Vector3(leftEuler.x, -leftEuler.y, -leftEuler.z);
                }
                else
                {
                    Vector3 couplerRDefPos = (initialCouplerRightPos != Vector3.zero) ? initialCouplerRightPos : DefaultCouplerRightPos;
                    Quaternion couplerRDefRot = (initialCouplerRightRot != Quaternion.identity) ? initialCouplerRightRot : DefaultCouplerRightRot;
                    Vector3 deltaPosL = couplerLTransPos - couplerLDefPos;
                    Vector3 couplerRTransPos = couplerRDefPos + new Vector3(-deltaPosL.x, deltaPosL.y, deltaPosL.z);
                    Vector3 transEulerL = couplerLTransRot.eulerAngles;
                    Quaternion couplerRTransRot = Quaternion.Euler(transEulerL.x, -transEulerL.y, -transEulerL.z);

                    couplerRight.localPosition = Vector3.Lerp(couplerRDefPos, couplerRTransPos, t);
                    couplerRight.localRotation = Quaternion.Slerp(couplerRDefRot, couplerRTransRot, t);
                }
            }

            // 4. 대형 단발 제트 부스터 노즐
            if (boosterNozzle != null)
            {
                Vector3 defPos = boosterStep1.position;
                Quaternion defRot = Quaternion.Euler(boosterStep1.rotationEuler);
                Vector3 transPos = boosterStep2.position;
                Quaternion transRot = Quaternion.Euler(boosterStep2.rotationEuler);

                boosterNozzle.localPosition = Vector3.Lerp(defPos, transPos, t);
                boosterNozzle.localRotation = Quaternion.Slerp(defRot, transRot, t);
            }

            // 5. 부품 메인상판덮개
            if (mainCover != null)
            {
                Vector3 defPos = coverStep1.position;
                Quaternion defRot = Quaternion.Euler(coverStep1.rotationEuler);
                Vector3 transPos = coverStep3.position;
                Quaternion transRot = Quaternion.Euler(coverStep3.rotationEuler);

                mainCover.localPosition = Vector3.Lerp(defPos, transPos, t);
                mainCover.localRotation = Quaternion.Slerp(defRot, transRot, t);
            }

            // 6. 보조 벤트 판 1, 2
            Vector3 ventLDefPos = leftStep1.position;
            Quaternion ventLDefRot = Quaternion.Euler(leftStep1.rotationEuler);
            Vector3 ventRDefPos = (initialRightPos != Vector3.zero) ? initialRightPos : DefaultVentRightPos;
            Quaternion ventRDefRot = (initialRightRot != Quaternion.identity) ? initialRightRot : DefaultVentRightRot;

            Vector3 coverPivot = coverStep1.position;
            Quaternion finalDeltaRotCover = Quaternion.Euler(coverStep3.rotationEuler) * Quaternion.Inverse(Quaternion.Euler(coverStep1.rotationEuler));

            Vector3 ventLTransPos = coverStep3.position + finalDeltaRotCover * (leftStep4.position - coverPivot);
            Quaternion ventLTransRot = finalDeltaRotCover * Quaternion.Euler(leftStep4.rotationEuler);

            Vector3 deltaPosLV4 = leftStep4.position - ventLDefPos;
            Vector3 ventRStep4Pos = ventRDefPos + new Vector3(-deltaPosLV4.x, deltaPosLV4.y, deltaPosLV4.z);
            Quaternion deltaRotLV4 = Quaternion.Euler(leftStep4.rotationEuler) * Quaternion.Inverse(ventLDefRot);
            deltaRotLV4.ToAngleAxis(out float angleV, out Vector3 axisV);
            if (angleV > 180f) angleV -= 360f;
            Quaternion ventRStep4Rot = Quaternion.AngleAxis(-angleV, new Vector3(-axisV.x, axisV.y, axisV.z)) * ventRDefRot;

            Vector3 ventRTransPos = coverStep3.position + finalDeltaRotCover * (ventRStep4Pos - coverPivot);
            Quaternion ventRTransRot = finalDeltaRotCover * ventRStep4Rot;

            if (ventLeft != null)
            {
                ventLeft.localPosition = Vector3.Lerp(ventLDefPos, ventLTransPos, t);
                ventLeft.localRotation = Quaternion.Slerp(ventLDefRot, ventLTransRot, t);
            }
            if (ventRight != null)
            {
                ventRight.localPosition = Vector3.Lerp(ventRDefPos, ventRTransPos, t);
                ventRight.localRotation = Quaternion.Slerp(ventRDefRot, ventRTransRot, t);
            }

            // 7. 부스터 네온 및 스플라인
            bool neonActive = t > 0.5f;
            if (splineRed != null && splineRed.activeSelf != neonActive) splineRed.SetActive(neonActive);
            if (splineBlue != null && splineBlue.activeSelf != neonActive) splineBlue.SetActive(neonActive);
            if (splineYellow != null && splineYellow.activeSelf != neonActive) splineYellow.SetActive(neonActive);
            if (splineOrange != null && splineOrange.activeSelf != neonActive) splineOrange.SetActive(neonActive);
            if (boosterNeon != null && boosterNeon.activeSelf != neonActive) boosterNeon.SetActive(neonActive);

            // 8. 부스터 VFX 이펙트 연동 (보조 4개: 변형 시작 시점, 메인 1개: 주황 네온 켜지는 최종 시점)
            bool subFxActive = t > 0.35f;
            bool mainFxActive = t >= 0.85f;
            SetSubBoosterFxActive(subFxActive);
            SetMainBoosterFxActive(mainFxActive);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
#endif
        }

        private void Reset()
        {
            AutoBindParts();
        }

        private void Awake()
        {
            if (ventLeft == null || ventRight == null)
            {
                AutoBindParts();
            }

            // 사용자 지정 정확한 2단계 오픈 대기 위치 및 3단계 수납 위치 적용
            coverStep2.position = new Vector3(0.003f, 0.442f, -1.131f);
            coverStep2.rotationEuler = new Vector3(-142.1f, 180f, 0f);
            coverStep3.position = new Vector3(0.003f, -0.098f, -0.565f);
            coverStep3.rotationEuler = new Vector3(-142.1f, 180f, 0f);

            // 터빈 중심 피벗 자동 보정
            SetupTurbinePivot();

            // 바퀴 회전 컨트롤러 자동 탐색 연동
            AutoFindWheelController();

            // 부스터 VFX 자동 연결 및 초기 소등
            AutoBindBoosterFx();
            SetSubBoosterFxActive(false);
            SetMainBoosterFxActive(false);
        }

        [ContextMenu("차량 부품 자동 연결 (SportCar / BlueCar 지원)")]
        public void AutoBindParts()
        {
            // 1. targetCarRoot 찾기
            if (targetCarRoot == null)
            {
                if (transform.Find("Blue_Car_Final_Model") != null)
                {
                    targetCarRoot = transform.Find("Blue_Car_Final_Model");
                }
                else if (gameObject.name == "SportCar_4change_2" || gameObject.name == "Blue_Car_Final_Model")
                {
                    targetCarRoot = transform;
                }
                else if (transform.Find("부품_보조벤트판1") != null || transform.Find("3d스캐너") != null)
                {
                    targetCarRoot = transform;
                }
                else if (transform.parent != null && (transform.parent.Find("부품_보조벤트판1") != null || transform.parent.Find("3d스캐너") != null))
                {
                    targetCarRoot = transform.parent;
                }
                else
                {
                    GameObject carObj = GameObject.Find("Blue_Car_Final");
                    if (carObj != null)
                    {
                        Transform model = carObj.transform.Find("Blue_Car_Final_Model");
                        targetCarRoot = model != null ? model : carObj.transform;
                    }
                    if (targetCarRoot == null)
                    {
                        carObj = GameObject.Find("SportCar_4change_2");
                        if (carObj != null) targetCarRoot = carObj.transform;
                    }
                }

                if (targetCarRoot == null)
                {
                    targetCarRoot = transform;
                }
            }

            // 2. targetCarRoot 하위에서 부품 찾기 (재귀 탐색 지원)
            if (targetCarRoot != null)
            {
                Transform foundLeft = FindPartRecursive(targetCarRoot, "부품_보조벤트판1");
                if (foundLeft != null) ventLeft = foundLeft;

                Transform foundRight = FindPartRecursive(targetCarRoot, "부품_보조벤트판2");
                if (foundRight != null) ventRight = foundRight;

                Transform foundScanner = FindPartRecursive(targetCarRoot, "3d스캐너");
                if (foundScanner != null) scanner3D = foundScanner;

                Transform foundCouplerL = FindPartRecursive(targetCarRoot, "부품_연결부1");
                if (foundCouplerL != null) couplerLeft = foundCouplerL;

                Transform foundCouplerR = FindPartRecursive(targetCarRoot, "부품_연결부2");
                if (foundCouplerR != null) couplerRight = foundCouplerR;

                Transform foundCowl = FindPartRecursive(targetCarRoot, "부품_기계식상부아머드카울");
                if (foundCowl != null) armoredCowl = foundCowl;

                // 대형_단발_제트_부스터_노즐 찾기
                Transform foundBooster = FindPartRecursive(targetCarRoot, "대형_단발_제트_부스터_노즐");
                if (foundBooster == null) foundBooster = FindPartRecursive(targetCarRoot, "대형_단발_제트_부스터");
                if (foundBooster != null) boosterNozzle = foundBooster;

                // 네온 찾기
                Transform foundNeon = FindPartRecursive(targetCarRoot, "네온");
                if (foundNeon != null) boosterNeon = foundNeon.gameObject;

                // 제트부스터_터빈 찾기
                Transform foundTurbine = FindPartRecursive(targetCarRoot, "제트부스터_터빈_회전축");
                if (foundTurbine == null) foundTurbine = FindPartRecursive(targetCarRoot, "제트부스터_터빈");
                if (foundTurbine == null) foundTurbine = FindPartRecursive(targetCarRoot, "부스터터빈");
                if (foundTurbine != null) boosterTurbine = foundTurbine;

                // 빨강, 파랑, 노랑, 주황 스플라인 자동 연결
                Transform searchRoot = (boosterNozzle != null) ? boosterNozzle : targetCarRoot;
                if (searchRoot != null)
                {
                    Transform r = FindPartRecursive(searchRoot, "빨강"); if (r != null) splineRed = r.gameObject;
                    Transform b = FindPartRecursive(searchRoot, "파랑"); if (b != null) splineBlue = b.gameObject;
                    Transform y = FindPartRecursive(searchRoot, "노랑"); if (y != null) splineYellow = y.gameObject;
                    Transform o = FindPartRecursive(searchRoot, "주황"); if (o != null) splineOrange = o.gameObject;
                }

                // 부품_메인상판덮개 찾기
                Transform foundCover = FindPartRecursive(targetCarRoot, "부품_메인상판덮개");
                if (foundCover != null) mainCover = foundCover;

                // 부스터 VFX 자동 연결
                AutoBindBoosterFx();

                Debug.Log($"[VehicleTransformationController] '{targetCarRoot.name}' 하위에서 부품들을 자동으로 연결했습니다.");
            }
            else
            {
                // 전역 탐색 백업
                GameObject leftObj = GameObject.Find("부품_보조벤트판1");
                if (leftObj != null) ventLeft = leftObj.transform;

                GameObject rightObj = GameObject.Find("부품_보조벤트판2");
                if (rightObj != null) ventRight = rightObj.transform;

                GameObject scannerObj = GameObject.Find("3d스캐너");
                if (scannerObj != null) scanner3D = scannerObj.transform;

                GameObject couplerLObj = GameObject.Find("부품_연결부1");
                if (couplerLObj != null) couplerLeft = couplerLObj.transform;

                GameObject couplerRObj = GameObject.Find("부품_연결부2");
                if (couplerRObj != null) couplerRight = couplerRObj.transform;

                GameObject cowlObj = GameObject.Find("부품_기계식상부아머드카울");
                if (cowlObj != null) armoredCowl = cowlObj.transform;

                GameObject boosterObj = GameObject.Find("대형_단발_제트_부스터_노즐");
                if (boosterObj != null) boosterNozzle = boosterObj.transform;

                GameObject neonObj = GameObject.Find("네온");
                if (neonObj != null) boosterNeon = neonObj;

                GameObject turbineObj = GameObject.Find("제트부스터_터빈");
                if (turbineObj == null) turbineObj = GameObject.Find("부스터터빈");
                if (turbineObj != null) boosterTurbine = turbineObj.transform;

                if (splineRed == null) splineRed = GameObject.Find("빨강");
                if (splineBlue == null) splineBlue = GameObject.Find("파랑");
                if (splineYellow == null) splineYellow = GameObject.Find("노랑");
                if (splineOrange == null) splineOrange = GameObject.Find("주황");

                GameObject coverObj = GameObject.Find("부품_메인상판덮개");
                if (coverObj != null) mainCover = coverObj.transform;
            }

            // 바퀴 회전 컨트롤러 자동 탐색 및 연결
            AutoFindWheelController();
        }

        private Transform FindPartRecursive(Transform root, string partName)
        {
            if (root == null) return null;
            Transform direct = root.Find(partName);
            if (direct != null) return direct;

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == partName) return child;
            }
            return null;
        }

        [Header("[ 거울(Mirror) 대칭 모드 ]")]
        [Tooltip("체크 시 좌측 벤트판의 움직임을 실시간 거울 대칭하여 우측에 자동 적용합니다 (뒤집힘 0% 보장)")]
        public bool mirrorRightFromLeft = true;

        [Tooltip("체크 시 좌측 연결부1의 움직임을 실시간 거울 대칭하여 우측 연결부2에 자동 적용합니다")]
        public bool mirrorRightCoupler = true;

        private Vector3 initialLeftPos;
        private Quaternion initialLeftRot;
        private Vector3 initialRightPos;
        private Quaternion initialRightRot;

        private Vector3 initialCouplerLeftPos;
        private Quaternion initialCouplerLeftRot;
        private Vector3 initialCouplerRightPos;
        private Quaternion initialCouplerRightRot;

        private Vector3 initialCowlPos;
        private Quaternion initialCowlRot;

        // 카울이 올라갈 때 기준이 될 연결부 포즈
        private Vector3 couplerLeftBeforeCowlPos;
        private Quaternion couplerLeftBeforeCowlRot;

        // 메인 상판 덮개가 열릴 때 기준이 될 보조 벤트 포즈
        private Vector3 ventLeftBeforeCoverPos;
        private Quaternion ventLeftBeforeCoverRot;
        private Vector3 ventRightBeforeCoverPos;
        private Quaternion ventRightBeforeCoverRot;

        private void Start()
        {
            // 부스터 VFX 자동 연결 및 탐색
            AutoBindBoosterFx();

            // 평소에는 꺼두는 설정(사용자가 꺼놓은 상태)을 존중하여 시작 시 부스터 이펙트 전체 소등
            SetSubBoosterFxActive(false);
            SetMainBoosterFxActive(false);

            // 게임 시작 시 미리보기 상태를 깨끗이 원복하여 기본 주행 모드로 초기화
            if (previewProgress > 0.001f || previewFinalTransformation)
            {
                ResetTransformationPreview();
            }

            if (ventLeft == null || ventRight == null || scanner3D == null || couplerLeft == null || couplerRight == null || armoredCowl == null || boosterNozzle == null || mainCover == null)
            {
                AutoBindParts();
            }

            SetupTurbinePivot();

            if (ventLeft != null && ventRight != null)
            {
                initialLeftPos = leftStep1.position;
                initialLeftRot = Quaternion.Euler(leftStep1.rotationEuler);

                initialRightPos = (ventRight.localPosition != Vector3.zero) ? ventRight.localPosition : DefaultVentRightPos;
                initialRightRot = (ventRight.localRotation != Quaternion.identity) ? ventRight.localRotation : DefaultVentRightRot;

                ventLeftBeforeCoverPos = ventLeft.localPosition;
                ventLeftBeforeCoverRot = ventLeft.localRotation;
                ventRightBeforeCoverPos = ventRight.localPosition;
                ventRightBeforeCoverRot = ventRight.localRotation;
            }

            if (couplerLeft != null && couplerRight != null)
            {
                initialCouplerLeftPos = couplerLeftStep1.position;
                initialCouplerLeftRot = Quaternion.Euler(couplerLeftStep1.rotationEuler);

                initialCouplerRightPos = new Vector3(-initialCouplerLeftPos.x, initialCouplerLeftPos.y, initialCouplerLeftPos.z);
                initialCouplerRightRot = DefaultCouplerRightRot;
            }

            if (armoredCowl != null)
            {
                initialCowlPos = cowlStep1.position;
                initialCowlRot = Quaternion.Euler(cowlStep1.rotationEuler);
            }

            Debug.Log($"[VehicleTransformationController] ✅ 준비 완료! Vent: {(ventLeft != null)}, Scanner: {(scanner3D != null)}, Coupler: {(couplerLeft != null)}, Cowl: {(armoredCowl != null)}, Booster: {(boosterNozzle != null)}, Cover: {(mainCover != null)}");

            if (startWithFullTransformation)
            {
                SnapToFullTransformation();
            }
        }

        /// <summary>
        /// 전체 변형 및 모든 부스터 이펙트를 100% 완료 상태로 즉시 적용
        /// </summary>
        [ContextMenu("🚀 전체 변형 및 부스터 이펙트 즉시 완료 (Snap)")]
        public void SnapToFullTransformation()
        {
            if (fullSequenceCoroutine != null)
            {
                StopCoroutine(fullSequenceCoroutine);
                fullSequenceCoroutine = null;
            }

            isFullyTransformed = true;
            isVentHidden = true;
            isScannerDeployed = true;
            isCouplerFolded = true;
            isCowlLifted = true;
            isBoosterDeployed = true;
            isCoverOpened = true;
            isFullSequenceRunning = false;

            ApplyTransformationProgress(1.0f);

            currentTurbineSpeed = maxTurbineSpeed;

            if (splineRed != null) splineRed.SetActive(true);
            if (splineBlue != null) splineBlue.SetActive(true);
            if (splineYellow != null) splineYellow.SetActive(true);
            if (splineOrange != null) splineOrange.SetActive(true);
            if (boosterNeon != null) boosterNeon.SetActive(true);

            SetSubBoosterFxActive(true);
            SetMainBoosterFxActive(true);

            Debug.Log("[VehicleTransformationController] 🚀 블루카 풀 변형 & 모든 부스터 이펙트 즉시 점등 완료!");
        }

        /// <summary>
        /// 블루카를 즉시 원래 기본 미변형 상태(0%)로 안전하게 복귀 (스플라인 주행 시작 전 초기화용)
        /// </summary>
        [ContextMenu("🔄 기본 미변형 상태 즉시 복귀 (0%)")]
        public void SnapToNormalState()
        {
            if (fullSequenceCoroutine != null)
            {
                StopCoroutine(fullSequenceCoroutine);
                fullSequenceCoroutine = null;
            }

            isFullyTransformed = false;
            isVentHidden = false;
            isScannerDeployed = false;
            isCouplerFolded = false;
            isCowlLifted = false;
            isBoosterDeployed = false;
            isCoverOpened = false;
            isFullSequenceRunning = false;

            ApplyTransformationProgress(0.0f);

            currentTurbineSpeed = 0f;

            if (splineRed != null) splineRed.SetActive(false);
            if (splineBlue != null) splineBlue.SetActive(false);
            if (splineYellow != null) splineYellow.SetActive(false);
            if (splineOrange != null) splineOrange.SetActive(false);
            if (boosterNeon != null) boosterNeon.SetActive(false);

            SetSubBoosterFxActive(false);
            SetMainBoosterFxActive(false);

            Debug.Log("[VehicleTransformationController] 🔄 블루카 기본 원래 상태(0%) 복귀 완료!");
        }

        /// <summary>
        /// 블루카 전체 순차 변형 시퀀스를 확실하게 시작 (토글 상태와 무관하게 100% 전개 방향으로 시작)
        /// </summary>
        [ContextMenu("🚀 전체 풀 변형 시퀀스 전개 시작")]
        public void DeployFullTransformation()
        {
            if (fullSequenceCoroutine != null)
            {
                StopCoroutine(fullSequenceCoroutine);
                fullSequenceCoroutine = null;
            }

            isFullyTransformed = true;
            isFullSequenceRunning = false;
            fullSequenceCoroutine = StartCoroutine(AnimateFullSequence(true));
        }

        private void Update()
        {
            // 0. 전체 변형 시퀀스 키 입력 체크 (스페이스바 / 숫자 0번 / 키패드 0번)
            bool keyFullPressed = false;
            try
            {
                if (Input.GetKeyDown(triggerKeyFullSequence) || Input.GetKeyDown(triggerKeyFullSequenceAlt) || Input.GetKeyDown(KeyCode.Space))
                    keyFullPressed = true;
            }
            catch { }

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_EXISTS
            if (!keyFullPressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.spaceKey.wasPressedThisFrame || kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame)
                    keyFullPressed = true;
            }
#endif
            if (keyFullPressed)
            {
                Debug.Log("[VehicleTransformationController] 🔥 전체 풀 변형 시퀀스 실행 (스페이스바 / 0번)!");
                ToggleFullTransformation();
            }

            // 1. 1번 키 입력 체크 (보조 벤트판)
            bool key1Pressed = false;
            try
            {
                if (Input.GetKeyDown(triggerKey1) || Input.GetKeyDown(triggerKeypad1))
                    key1Pressed = true;
            }
            catch { }

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_EXISTS
            if (!key1Pressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
                    key1Pressed = true;
            }
#endif
            if (key1Pressed)
            {
                Debug.Log("[VehicleTransformationController] 🎯 1번 키 입력 감지! 보조 벤트 변형 시작");
                ToggleVentRetraction();
            }

            // 2. 2번 키 입력 체크 (3D 스캐너 + 카울 연결부 1, 2 동시 실행)
            bool key2Pressed = false;
            try
            {
                if (Input.GetKeyDown(triggerKey2) || Input.GetKeyDown(triggerKeypad2))
                    key2Pressed = true;
            }
            catch { }

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_EXISTS
            if (!key2Pressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
                    key2Pressed = true;
            }
#endif
            if (key2Pressed)
            {
                Debug.Log("[VehicleTransformationController] 🎯 2번 키 입력 감지! 3D 스캐너 + 연결부 동시 변형 시작");
                ToggleStage2();
            }

            // 3. 3번 키 입력 체크 (기계식 상부 아머드 카울 + 접힌 연결부 동반 상승)
            bool key3Pressed = false;
            try
            {
                if (Input.GetKeyDown(triggerKey3) || Input.GetKeyDown(triggerKeypad3))
                    key3Pressed = true;
            }
            catch { }

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_EXISTS
            if (!key3Pressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
                    key3Pressed = true;
            }
#endif
            if (key3Pressed)
            {
                Debug.Log("[VehicleTransformationController] 🎯 3번 키 입력 감지! 기계식 상부 아머드 카울 상승 시작");
                ToggleCowlLift();
            }

            // 4. 4번 키 입력 체크 (대형 단발 제트 부스터 노즐 돌출)
            bool key4Pressed = false;
            try
            {
                if (Input.GetKeyDown(triggerKey4) || Input.GetKeyDown(triggerKeypad4))
                    key4Pressed = true;
            }
            catch { }

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_EXISTS
            if (!key4Pressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
                    key4Pressed = true;
            }
#endif
            if (key4Pressed)
            {
                Debug.Log("[VehicleTransformationController] 🎯 4번 키 입력 감지! 제트 부스터 노즐 돌출 시작");
                ToggleBoosterDeploy();
            }

            // 5. 5번 키 입력 체크 (부품 메인상판덮개 개폐)
            bool key5Pressed = false;
            try
            {
                if (Input.GetKeyDown(triggerKey5) || Input.GetKeyDown(triggerKeypad5))
                    key5Pressed = true;
            }
            catch { }

#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM_EXISTS
            if (!key5Pressed && UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame)
                    key5Pressed = true;
            }
#endif
            if (key5Pressed)
            {
                Debug.Log("[VehicleTransformationController] 🎯 5번 키 입력 감지! 메인 상판 덮개 개폐 시작");
                ToggleMainCover();
            }

            // 터빈 회전 업데이트
            UpdateTurbineRotation();
        }

        private void OnGUI()
        {
            if (!showOnGUI) return;

            // 화면 좌상단에 테스트용 UI 패널 표시
            GUI.Box(new Rect(10, 10, 260, 280), "🚗 변형 제어 패널");

            // 전체 풀 변형 시퀀스 버튼 (1->2->3->4+5)
            GUI.color = isFullyTransformed ? new Color(1f, 0.75f, 0.75f) : new Color(0.75f, 1f, 0.75f);
            if (GUI.Button(new Rect(20, 35, 240, 30), isFullyTransformed ? "🔥 전체 원복 (Space / 0)" : "🔥 풀 변형 시퀀스 (Space / 0)"))
            {
                ToggleFullTransformation();
            }
            GUI.color = Color.white;

            // 1번 버튼: 보조 벤트
            if (GUI.Button(new Rect(20, 70, 240, 26), isVentHidden ? "1번: 보조벤트 원복 (4->1)" : "1번: 보조벤트 수납 (1->4)"))
            {
                ToggleVentRetraction();
            }

            // 2번 버튼: 3D 스캐너 + 연결부 2개 동시 전개
            if (GUI.Button(new Rect(20, 100, 240, 26), (isScannerDeployed || isCouplerFolded) ? "2번: 스캐너+연결부 복구" : "2번: 스캐너+연결부 동시 전개"))
            {
                ToggleStage2();
            }

            // 3번 버튼: 기계식 상부 아머드 카울 + 연결부 상승
            if (GUI.Button(new Rect(20, 130, 240, 26), isCowlLifted ? "3번: 아머드 카울 하강 (3->1)" : "3번: 카울+연결부 상승 (1->3)"))
            {
                ToggleCowlLift();
            }

            // 4번 버튼: 대형 단발 제트 부스터 노즐 돌출
            if (GUI.Button(new Rect(20, 160, 240, 26), isBoosterDeployed ? "4번: 부스터 노즐 수납 (2->1)" : "4번: 부스터 노즐 돌출 (1->2)"))
            {
                ToggleBoosterDeploy();
            }

            // 5번 버튼: 부품 메인상판덮개 개폐 (내부 수납)
            if (GUI.Button(new Rect(20, 190, 240, 26), isCoverOpened ? "5번: 메인상판 원복 (3->1)" : "5번: 메인상판 내부수납 (1->3)"))
            {
                ToggleMainCover();
            }

            // 터빈 회전 상태 표시 및 방향/리셋 버튼
            if (boosterTurbine != null)
            {
                GUI.Label(new Rect(20, 220, 240, 20), $"🌀 터빈: {(int)currentTurbineSpeed} deg/s ({turbineAxis})");
                if (GUI.Button(new Rect(20, 244, 115, 24), "방향/축 전환"))
                {
                    CycleTurbineAxis();
                }
                if (GUI.Button(new Rect(145, 244, 115, 24), "각도 리셋"))
                {
                    ResetTurbineRotation();
                }
            }
        }

        /// <summary>
        /// 2단계: 3D 스캐너 지붕 상승 + 카울 연결부 2개 접기 동시 실행
        /// </summary>
        [ContextMenu("2번 3D스캐너 + 연결부2개 동시 실행 / 토글")]
        public void ToggleStage2()
        {
            bool targetState = !(isScannerDeployed && isCouplerFolded);

            if (scannerCoroutine != null) StopCoroutine(scannerCoroutine);
            if (couplerCoroutine != null) StopCoroutine(couplerCoroutine);

            isScannerDeployed = targetState;
            isCouplerFolded = targetState;

            scannerCoroutine = StartCoroutine(AnimateScannerSequence(targetState));
            couplerCoroutine = StartCoroutine(AnimateCouplerSequence(targetState));

            Debug.Log($"[VehicleTransformationController] 🚀 2번 실행: 3D스캐너와 연결부 2개가 동시에 {(targetState ? "전개/접힘" : "복구")}됩니다!");
        }

        /// <summary>
        /// 3D 스캐너 지붕 전개 / 복구 토글
        /// </summary>
        [ContextMenu("2번 3D 스캐너 전개 / 복구 토글")]
        public void ToggleScannerDeployment()
        {
            if (scannerCoroutine != null)
            {
                StopCoroutine(scannerCoroutine);
            }

            isScannerDeployed = !isScannerDeployed;
            scannerCoroutine = StartCoroutine(AnimateScannerSequence(isScannerDeployed));
        }

        /// <summary>
        /// 3D 스캐너 3단계 전개 시퀀스 코루틴
        /// </summary>
        private IEnumerator AnimateScannerSequence(bool deploy)
        {
            if (deploy)
            {
                // 1 -> 2단계: 대각선 슬라이드 상승
                yield return AnimateScannerBetweenPoses(scannerStep1, scannerStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                // 2 -> 3단계: 지붕 위 수평 전개 완료
                yield return AnimateScannerBetweenPoses(scannerStep2, scannerStep3, stepDuration);
            }
            else
            {
                // 역순 복구 (3 -> 2 -> 1)
                yield return AnimateScannerBetweenPoses(scannerStep3, scannerStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                yield return AnimateScannerBetweenPoses(scannerStep2, scannerStep1, stepDuration);
            }

            scannerCoroutine = null;
        }

        /// <summary>
        /// 3D 스캐너의 두 포즈 사이 부드러운 보간
        /// </summary>
        private IEnumerator AnimateScannerBetweenPoses(TransformPose from, TransformPose to, float duration)
        {
            if (scanner3D == null) yield break;

            float elapsed = 0f;
            Quaternion rotFrom = Quaternion.Euler(from.rotationEuler);
            Quaternion rotTo = Quaternion.Euler(to.rotationEuler);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = motionCurve != null ? motionCurve.Evaluate(t) : t;

                scanner3D.localPosition = Vector3.Lerp(from.position, to.position, curveT);
                scanner3D.localRotation = Quaternion.Slerp(rotFrom, rotTo, curveT);

                yield return null;
            }

            scanner3D.localPosition = to.position;
            scanner3D.localRotation = rotTo;
        }

        /// <summary>
        /// 보조 벤트 판 수납 / 복구 토글
        /// </summary>
        [ContextMenu("1번 변형 실행 / 토글")]
        public void ToggleVentRetraction()
        {
            if (isCoverOpened)
            {
                Debug.LogWarning("[VehicleTransformationController] ⚠️ 메인 상판 덮개가 열려 있는 상태에서는 보조 벤트를 단독으로 조작할 수 없습니다. 덮개를 먼저 닫아주세요.");
                return;
            }

            if (ventCoroutine != null)
            {
                StopCoroutine(ventCoroutine);
            }

            isVentHidden = !isVentHidden;
            ventCoroutine = StartCoroutine(AnimateVentSequence(isVentHidden));
        }

        /// <summary>
        /// 4단계 변형 시퀀스 애니메이션 코루틴
        /// </summary>
        private IEnumerator AnimateVentSequence(bool hide)
        {
            if (hide)
            {
                // 1 -> 2단계: 수직 상승
                yield return AnimateBetweenPoses(leftStep1, leftStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                // 2 -> 3단계: 안쪽으로 눕기 (틸트)
                yield return AnimateBetweenPoses(leftStep2, leftStep3, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                // 3 -> 4단계: 상판 덮개 안으로 완전 수납
                yield return AnimateBetweenPoses(leftStep3, leftStep4, stepDuration);
            }
            else
            {
                // 역순 복구 (4 -> 3 -> 2 -> 1)
                yield return AnimateBetweenPoses(leftStep4, leftStep3, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                yield return AnimateBetweenPoses(leftStep3, leftStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                yield return AnimateBetweenPoses(leftStep2, leftStep1, stepDuration);
            }

            ventCoroutine = null;
        }

        /// <summary>
        /// 좌측 포즈를 보간 이동하면서, 우측은 수학적 거울 대칭으로 완벽 연동
        /// </summary>
        private IEnumerator AnimateBetweenPoses(TransformPose leftFrom, TransformPose leftTo, float duration)
        {
            float elapsed = 0f;

            Quaternion leftRotFrom = Quaternion.Euler(leftFrom.rotationEuler);
            Quaternion leftRotTo = Quaternion.Euler(leftTo.rotationEuler);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = motionCurve != null ? motionCurve.Evaluate(t) : t;

                // 1. 좌측 벤트판 보간
                if (ventLeft != null)
                {
                    ventLeft.localPosition = Vector3.Lerp(leftFrom.position, leftTo.position, curveT);
                    ventLeft.localRotation = Quaternion.Slerp(leftRotFrom, leftRotTo, curveT);
                }

                // 2. 우측 벤트판은 좌측의 움직임을 거울(Mirror) 반사
                if (ventRight != null && mirrorRightFromLeft)
                {
                    ApplyMirrorTransformToRight();
                }

                yield return null;
            }

            // 최종 위치 정확히 고정
            if (ventLeft != null)
            {
                ventLeft.localPosition = leftTo.position;
                ventLeft.localRotation = leftRotTo;
            }

            if (ventRight != null && mirrorRightFromLeft)
            {
                ApplyMirrorTransformToRight();
            }
        }

        /// <summary>
        /// 좌측 벤트판의 현재 상대 변화량을 기반으로 우측 벤트판에 데칼코마니 거울 대칭 적용
        /// </summary>
        private void ApplyMirrorTransformToRight()
        {
            if (ventLeft == null || ventRight == null) return;

            // 1. 위치 미러링: X축 이동량만 반대 부호, Y/Z는 동일
            Vector3 deltaPosLeft = ventLeft.localPosition - initialLeftPos;
            Vector3 deltaPosRight = new Vector3(-deltaPosLeft.x, deltaPosLeft.y, deltaPosLeft.z);
            ventRight.localPosition = initialRightPos + deltaPosRight;

            // 2. 회전 미러링: 시작 각도에서 벗어난 회전량(Delta)을 거울 반사
            Quaternion deltaRotLeft = ventLeft.localRotation * Quaternion.Inverse(initialLeftRot);
            deltaRotLeft.ToAngleAxis(out float angle, out Vector3 axis);

            if (angle > 180f) angle -= 360f;

            // 거울 대칭 반사 공식 (X축 반전 시 회전축과 각도 반사)
            Vector3 mirrorAxis = new Vector3(-axis.x, axis.y, axis.z);
            float mirrorAngle = -angle;

            Quaternion deltaRotRight = Quaternion.AngleAxis(mirrorAngle, mirrorAxis);
            ventRight.localRotation = deltaRotRight * initialRightRot;
        }

        /// <summary>
        /// 카울 연결부 1, 2 접기 / 복구 토글
        /// </summary>
        [ContextMenu("3번 카울 연결부 접기 / 복구 토글")]
        public void ToggleCouplerFolding()
        {
            if (couplerCoroutine != null)
            {
                StopCoroutine(couplerCoroutine);
            }

            isCouplerFolded = !isCouplerFolded;
            couplerCoroutine = StartCoroutine(AnimateCouplerSequence(isCouplerFolded));
        }

        /// <summary>
        /// 카울 연결부 3단계 접힘 시퀀스 코루틴
        /// </summary>
        private IEnumerator AnimateCouplerSequence(bool fold)
        {
            if (fold)
            {
                // 1 -> 2단계: 80도 회전 접힘
                yield return AnimateCouplerBetweenPoses(couplerLeftStep1, couplerLeftStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                // 2 -> 3단계: 147도 완전 접힘
                yield return AnimateCouplerBetweenPoses(couplerLeftStep2, couplerLeftStep3, stepDuration);
            }
            else
            {
                // 역순 복구 (3 -> 2 -> 1)
                yield return AnimateCouplerBetweenPoses(couplerLeftStep3, couplerLeftStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                yield return AnimateCouplerBetweenPoses(couplerLeftStep2, couplerLeftStep1, stepDuration);
            }

            couplerCoroutine = null;
        }

        /// <summary>
        /// 연결부1 좌측 포즈를 보간 이동하면서, 연결부2 우측은 수학적 거울 대칭으로 완벽 연동
        /// </summary>
        private IEnumerator AnimateCouplerBetweenPoses(TransformPose leftFrom, TransformPose leftTo, float duration)
        {
            float elapsed = 0f;

            Quaternion leftRotFrom = Quaternion.Euler(leftFrom.rotationEuler);
            Quaternion leftRotTo = Quaternion.Euler(leftTo.rotationEuler);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = motionCurve != null ? motionCurve.Evaluate(t) : t;

                // 1. 좌측 연결부1 보간
                if (couplerLeft != null)
                {
                    couplerLeft.localPosition = Vector3.Lerp(leftFrom.position, leftTo.position, curveT);
                    couplerLeft.localRotation = Quaternion.Slerp(leftRotFrom, leftRotTo, curveT);
                }

                // 2. 우측 연결부2 실시간 거울 대칭
                if (couplerRight != null && mirrorRightCoupler)
                {
                    ApplyMirrorTransformToRightCoupler();
                }

                yield return null;
            }

            if (couplerLeft != null)
            {
                couplerLeft.localPosition = leftTo.position;
                couplerLeft.localRotation = leftRotTo;
            }

            if (couplerRight != null && mirrorRightCoupler)
            {
                ApplyMirrorTransformToRightCoupler();
            }
        }

        /// <summary>
        /// 좌측 연결부1의 상대 변화량을 기반으로 우측 연결부2에 완벽한 거울 대칭 적용
        /// </summary>
        private void ApplyMirrorTransformToRightCoupler()
        {
            if (couplerLeft == null || couplerRight == null) return;

            // X축 대칭 위치 및 Euler 회전 대칭 적용 (음수 스케일 X에 최적화)
            couplerRight.localPosition = new Vector3(-couplerLeft.localPosition.x, couplerLeft.localPosition.y, couplerLeft.localPosition.z);
            Vector3 leftEuler = couplerLeft.localEulerAngles;
            couplerRight.localEulerAngles = new Vector3(leftEuler.x, -leftEuler.y, -leftEuler.z);
        }

        /// <summary>
        /// 3단계: 기계식 상부 아머드 카울 상승 / 하강 (메인 상판 덮개도 카울 위에 얹혀져 있으므로 함께 상승/하강)
        /// </summary>
        [ContextMenu("3번 아머드 카울 상승 / 하강 토글")]
        public void ToggleCowlLift()
        {
            if (cowlCoroutine != null)
            {
                StopCoroutine(cowlCoroutine);
            }

            isCowlLifted = !isCowlLifted;

            // 카울이 상승을 시작할 때, 현재 접혀있는 연결부 및 상판 덮개 안의 보조 벤트 기준점 스냅샷
            if (isCowlLifted)
            {
                if (couplerLeft != null)
                {
                    couplerLeftBeforeCowlPos = couplerLeft.localPosition;
                    couplerLeftBeforeCowlRot = couplerLeft.localRotation;
                }
                if (ventLeft != null)
                {
                    ventLeftBeforeCoverPos = ventLeft.localPosition;
                    ventLeftBeforeCoverRot = ventLeft.localRotation;
                }
                if (ventRight != null)
                {
                    ventRightBeforeCoverPos = ventRight.localPosition;
                    ventRightBeforeCoverRot = ventRight.localRotation;
                }
            }

            cowlCoroutine = StartCoroutine(AnimateCowlWithCoverSequence(isCowlLifted));
        }

        /// <summary>
        /// 카울 상승 시 메인 상판 덮개(1->2)도 카울 위에 얹힌 채 함께 동반 상승/하강
        /// </summary>
        private IEnumerator AnimateCowlWithCoverSequence(bool lift)
        {
            float totalCowlDuration = stepDuration * 2f + (stepPause > 0 ? stepPause : 0f);

            Coroutine cowlCor = StartCoroutine(AnimateCowlSequence(lift));
            Coroutine coverCor = null;

            if (lift)
            {
                // 상판 덮개가 아직 차체에 닫혀 있다면 카울과 완벽히 동기화되어 1 -> 2로 함께 상승
                coverCor = StartCoroutine(AnimateCoverBetweenPoses(coverStep1, coverStep2, totalCowlDuration));
            }
            else
            {
                // 상판 덮개가 2단계(카울 상단)에 있다면 카울과 함께 2 -> 1로 원래 위치 하강
                coverCor = StartCoroutine(AnimateCoverBetweenPoses(coverStep2, coverStep1, totalCowlDuration));
            }

            yield return cowlCor;
            if (coverCor != null) yield return coverCor;
            cowlCoroutine = null;
        }

        /// <summary>
        /// 아머드 카울 3단계 상승 시퀀스 코루틴
        /// </summary>
        private IEnumerator AnimateCowlSequence(bool lift)
        {
            if (lift)
            {
                // 1 -> 2단계: 수직 대폭 상승
                yield return AnimateCowlBetweenPoses(cowlStep1, cowlStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                // 2 -> 3단계: 전방 틸트 및 완전 상승
                yield return AnimateCowlBetweenPoses(cowlStep2, cowlStep3, stepDuration);
            }
            else
            {
                // 역순 하강 (3 -> 2 -> 1)
                yield return AnimateCowlBetweenPoses(cowlStep3, cowlStep2, stepDuration);
                if (stepPause > 0) yield return new WaitForSeconds(stepPause);

                yield return AnimateCowlBetweenPoses(cowlStep2, cowlStep1, stepDuration);
            }

            cowlCoroutine = null;
        }

        /// <summary>
        /// 카울을 보간 이동/회전시키면서, 연결부 2개도 카울의 움직임만큼 함께 동반 이동
        /// </summary>
        private IEnumerator AnimateCowlBetweenPoses(TransformPose from, TransformPose to, float duration)
        {
            if (armoredCowl == null) yield break;

            float elapsed = 0f;
            Quaternion rotFrom = Quaternion.Euler(from.rotationEuler);
            Quaternion rotTo = Quaternion.Euler(to.rotationEuler);

            Quaternion initialCowlRotInv = Quaternion.Inverse(Quaternion.Euler(cowlStep1.rotationEuler));

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = motionCurve != null ? motionCurve.Evaluate(t) : t;

                // 1. 카울 본체 보간
                armoredCowl.localPosition = Vector3.Lerp(from.position, to.position, curveT);
                armoredCowl.localRotation = Quaternion.Slerp(rotFrom, rotTo, curveT);

                // 2. 카울의 시작점 대비 이동량 & 회전량 계산
                Vector3 deltaPosCowl = armoredCowl.localPosition - cowlStep1.position;
                Quaternion deltaRotCowl = armoredCowl.localRotation * initialCowlRotInv;

                // 3. 접힌 연결부 1 (좌측)을 카울을 따라 동반 이동/회전
                if (couplerLeft != null)
                {
                    couplerLeft.localPosition = couplerLeftBeforeCowlPos + deltaPosCowl;
                    couplerLeft.localRotation = deltaRotCowl * couplerLeftBeforeCowlRot;
                }

                // 4. 우측 연결부 2는 좌측 연결부의 움직임을 실시간 거울 대칭
                if (couplerRight != null && mirrorRightCoupler)
                {
                    ApplyMirrorTransformToRightCoupler();
                }

                yield return null;
            }

            // 최종 포즈 고정
            armoredCowl.localPosition = to.position;
            armoredCowl.localRotation = rotTo;

            Vector3 finalDeltaPosCowl = armoredCowl.localPosition - cowlStep1.position;
            Quaternion finalDeltaRotCowl = armoredCowl.localRotation * initialCowlRotInv;

            if (couplerLeft != null)
            {
                couplerLeft.localPosition = couplerLeftBeforeCowlPos + finalDeltaPosCowl;
                couplerLeft.localRotation = finalDeltaRotCowl * couplerLeftBeforeCowlRot;
            }

            if (couplerRight != null && mirrorRightCoupler)
            {
                ApplyMirrorTransformToRightCoupler();
            }
        }

        /// <summary>
        /// 4단계: 대형 단발 제트 부스터 노즐 돌출 / 수납 토글
        /// </summary>
        [ContextMenu("4번 제트 부스터 노즐 돌출 / 수납 토글")]
        public void ToggleBoosterDeploy()
        {
            if (boosterCoroutine != null)
            {
                StopCoroutine(boosterCoroutine);
            }

            isBoosterDeployed = !isBoosterDeployed;
            boosterCoroutine = StartCoroutine(AnimateBoosterSequence(isBoosterDeployed));
        }

        /// <summary>
        /// 부스터 노즐 돌출 시퀀스 코루틴 (후방 돌출 완료 시 터빈 엔진 가속 시작)
        /// </summary>
        private IEnumerator AnimateBoosterSequence(bool deploy)
        {
            if (deploy)
            {
                // ★ 1. 부스터 변형 시작과 동시에 보조 부스터 4종(pf_redbooster_2 4개) 즉시 점등!
                SetSubBoosterFxActive(true);

                // 1 -> 2단계: 후방으로 노즐 슬라이드 돌출
                yield return AnimateBoosterBetweenPoses(boosterStep1, boosterStep2, stepDuration);
                
                // ★ 대형 단발 제트 부스터가 변신 자리에 온 그 순간! 터빈 엔진 천천히->빠르게 가속 시작!
                StartTurbineSpoolUp();
            }
            else
            {
                // 수납 시: 메인 및 보조 부스터 전체 소등
                SetMainBoosterFxActive(false);
                SetSubBoosterFxActive(false);

                // 수납 시작 시: 터빈 서서히 감속 정지
                StartTurbineSpoolDown();

                // 2 -> 1단계: 원래 대기 위치로 수납
                yield return AnimateBoosterBetweenPoses(boosterStep2, boosterStep1, stepDuration);
            }

            boosterCoroutine = null;
        }

        /// <summary>
        /// 제트 부스터가 변신 자리에 도착했을 때 터빈 엔진 가속 기동 (천천히 -> 빠르게)
        /// </summary>
        [ContextMenu("🚀 터빈 가속 시작 (Spool Up)")]
        public void StartTurbineSpoolUp()
        {
            if (turbineSpinPivot == null) SetupTurbinePivot();

            if (turbineCoroutine != null) StopCoroutine(turbineCoroutine);
            turbineCoroutine = StartCoroutine(AnimateTurbineSpeed(maxTurbineSpeed, spoolUpDuration, spoolUpCurve));

            // ★ 터빈이 천천히 돌기 시작하는 바로 그 순간! 빨강->파랑->노랑->주황 스플라인 순차 점등 시작!
            StartSplineSequence();

            // ★ 부스터 작동 시 바퀴 자동 연동 옵션이 켜져 있을 때만 바퀴 연동 (기본은 독립 조작)
            if (syncWheelBoostWithBooster)
            {
                if (wheelSpinController == null) AutoFindWheelController();
                if (wheelSpinController != null)
                {
                    wheelSpinController.SetBoostMode(true);
                }
            }
        }

        /// <summary>
        /// 터빈 메쉬의 오프셋 피벗(Z축 편향)을 보정하여 원판 정중앙을 기준으로 완벽한 제자리 회전축을 구성
        /// </summary>
        public void SetupTurbinePivot()
        {
            if (boosterTurbine == null) return;

            // 이미 제트부스터_터빈_회전축으로 지정되어 있는 경우
            if (boosterTurbine.name == "제트부스터_터빈_회전축")
            {
                turbineSpinPivot = boosterTurbine;
                initialTurbineRotation = turbineSpinPivot.localRotation;
                hasStoredTurbineRot = true;
                return;
            }

            if (boosterTurbine.parent != null && boosterTurbine.parent.name == "제트부스터_터빈_회전축")
            {
                turbineSpinPivot = boosterTurbine.parent;
                initialTurbineRotation = turbineSpinPivot.localRotation;
                hasStoredTurbineRot = true;
                return;
            }

            MeshFilter mf = boosterTurbine.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 localCenter = mf.sharedMesh.bounds.center;
                if (localCenter.magnitude > 0.01f)
                {
                    Vector3 worldCenter = mf.transform.TransformPoint(localCenter);

                    GameObject pivotObj = new GameObject("제트부스터_터빈_회전축");
                    pivotObj.transform.SetParent(boosterTurbine.parent, false);
                    pivotObj.transform.position = worldCenter;
                    pivotObj.transform.rotation = boosterTurbine.rotation;
                    pivotObj.transform.localScale = Vector3.one;

                    boosterTurbine.SetParent(pivotObj.transform, true);
                    turbineSpinPivot = pivotObj.transform;

                    initialTurbineRotation = turbineSpinPivot.localRotation;
                    hasStoredTurbineRot = true;

                    Debug.Log($"[VehicleTransformationController] 🎯 터빈 원판 정중앙(오프셋: {localCenter})에 완벽한 회전 중심축({pivotObj.name})을 구성하여 제자리 고정 회전을 보장합니다!");
                    return;
                }
            }

            turbineSpinPivot = boosterTurbine;
            initialTurbineRotation = turbineSpinPivot.localRotation;
            hasStoredTurbineRot = true;
        }

        /// <summary>
        /// 터빈 각도 초기화 (뒤틀린 상태 원복)
        /// </summary>
        [ContextMenu("🔄 터빈 초기 각도로 원복")]
        public void ResetTurbineRotation()
        {
            Transform target = turbineSpinPivot != null ? turbineSpinPivot : boosterTurbine;
            if (target != null && hasStoredTurbineRot)
            {
                currentTurbineSpeed = 0f;
                if (turbineCoroutine != null) StopCoroutine(turbineCoroutine);
                target.localRotation = initialTurbineRotation;
                Debug.Log("[VehicleTransformationController] 터빈 각도가 초기 위치로 원복되었습니다.");
            }
        }

        /// <summary>
        /// 터빈 회전 축 및 방향 순환 전환 (Y축 시계 <-> Y축 반시계 등)
        /// </summary>
        [ContextMenu("🔄 터빈 회전 축/방향 전환")]
        public void CycleTurbineAxis()
        {
            switch (turbineAxis)
            {
                case TurbineAxis.Up_Y: turbineAxis = TurbineAxis.Down_NegY; break;
                case TurbineAxis.Down_NegY: turbineAxis = TurbineAxis.Up_Y; break;
                case TurbineAxis.Right_X: turbineAxis = TurbineAxis.Left_NegX; break;
                case TurbineAxis.Left_NegX: turbineAxis = TurbineAxis.Right_X; break;
                case TurbineAxis.Forward_Z: turbineAxis = TurbineAxis.Back_NegZ; break;
                case TurbineAxis.Back_NegZ: turbineAxis = TurbineAxis.Forward_Z; break;
            }
            Debug.Log($"[VehicleTransformationController] 🔄 터빈 회전 축이 변경되었습니다: {turbineAxis}");
        }

        /// <summary>
        /// 제트 부스터 수납 시 터빈 엔진 감속 정지 (빠르게 -> 천천히 -> 0)
        /// </summary>
        [ContextMenu("🛑 터빈 감속 정지 (Spool Down)")]
        public void StartTurbineSpoolDown()
        {
            if (turbineCoroutine != null) StopCoroutine(turbineCoroutine);
            turbineCoroutine = StartCoroutine(AnimateTurbineSpeed(0f, spoolDownDuration, spoolDownCurve));

            // ★ 부스터 수납 및 터빈 감속 시 스플라인 서서히 소등
            StopSplineSequence();

            // ★ 부스터 자동 연동 옵션이 켜져 있을 때만 바퀴 복귀
            if (syncWheelBoostWithBooster)
            {
                if (wheelSpinController == null) AutoFindWheelController();
                if (wheelSpinController != null)
                {
                    wheelSpinController.SetBoostMode(false);
                }
            }
        }

        /// <summary>
        /// 차량의 바퀴 회전 컨트롤러 자동 탐색 및 연결
        /// </summary>
        public void AutoFindWheelController()
        {
            if (wheelSpinController != null) return;

            wheelSpinController = GetComponent<VehicleWheelSpinController>();
            if (wheelSpinController == null && targetCarRoot != null)
            {
                wheelSpinController = targetCarRoot.GetComponent<VehicleWheelSpinController>();
                if (wheelSpinController == null)
                {
                    wheelSpinController = targetCarRoot.GetComponentInChildren<VehicleWheelSpinController>();
                }
            }
            if (wheelSpinController == null)
            {
                wheelSpinController = UnityEngine.Object.FindFirstObjectByType<VehicleWheelSpinController>();
            }
        }

        #region [ 스플라인 순차 점등 및 발광 페이드 인 연출 ]

        private SplineGlowData CreateSplineGlowData(GameObject obj, Color defaultEmission, Color defaultBase)
        {
            if (obj == null) return null;
            MeshRenderer mr = obj.GetComponentInChildren<MeshRenderer>(true);
            if (mr == null) return null;

            Material mat = Application.isPlaying ? mr.material : mr.sharedMaterial;
            if (mat == null) return null;
            mat.EnableKeyword("_EMISSION");

            Color em = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : defaultEmission;
            if (em == Color.black || em.maxColorComponent < 0.1f) em = defaultEmission;

            Color baseCol = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : defaultBase;

            // 초기 상태는 어둡게 꺼둠
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.black);
            obj.SetActive(false);

            return new SplineGlowData
            {
                obj = obj,
                renderer = mr,
                material = mat,
                targetEmission = em,
                targetBaseColor = baseCol
            };
        }

        private void EnsureSplineGlowData()
        {
            if (redGlowData == null && splineRed != null)
                redGlowData = CreateSplineGlowData(splineRed, new Color(8f, 0f, 0f, 1f), new Color(1f, 0.27f, 0f, 1f));

            if (blueGlowData == null && splineBlue != null)
                blueGlowData = CreateSplineGlowData(splineBlue, new Color(0f, 0.22f, 16f, 1f), new Color(0.04f, 0f, 1f, 1f));

            if (yellowGlowData == null && splineYellow != null)
                yellowGlowData = CreateSplineGlowData(splineYellow, new Color(7.2f, 8f, 0f, 1f), new Color(0.97f, 1f, 0f, 1f));

            if (orangeGlowData == null && splineOrange != null)
                orangeGlowData = CreateSplineGlowData(splineOrange, new Color(8f, 3.5f, 0f, 1f), new Color(1f, 0.5f, 0f, 1f));
        }

        /// <summary>
        /// 터빈 회전 시작 시: 빨강 -> 파랑 -> 노랑 -> 주황 순으로 서서히 빛이 진해지며 켜짐
        /// </summary>
        [ContextMenu("✨ 스플라인 순차 점등 (빨강->파랑->노랑->주황)")]
        public void StartSplineSequence()
        {
            if (splineSequenceCoroutine != null) StopCoroutine(splineSequenceCoroutine);
            splineSequenceCoroutine = StartCoroutine(AnimateSplineSequence());
        }

        /// <summary>
        /// 부스터 수납 시: 스플라인 빛이 서서히 소등
        /// </summary>
        [ContextMenu("🌑 스플라인 전체 소등")]
        public void StopSplineSequence()
        {
            if (splineSequenceCoroutine != null) StopCoroutine(splineSequenceCoroutine);
            splineSequenceCoroutine = StartCoroutine(AnimateSplineFadeOut());
        }

        private IEnumerator AnimateSplineSequence()
        {
            EnsureSplineGlowData();

            // 순서: 1. 빨강 -> 2. 파랑 -> 3. 노랑 -> 4. 주황
            SplineGlowData[] sequence = new SplineGlowData[] { redGlowData, blueGlowData, yellowGlowData, orangeGlowData };

            for (int i = 0; i < sequence.Length; i++)
            {
                SplineGlowData data = sequence[i];
                if (data != null && data.obj != null)
                {
                    StartCoroutine(AnimateSplineGlow(data, true, splineGlowDuration));
                }

                // ★ 2. 대형단발제트부스터 노즐에 마지막 주황색 네온(sequence[3])이 들어올 때 pf_redbooster_1 점등!
                if (i == 3)
                {
                    SetMainBoosterFxActive(true);
                }

                if (i < sequence.Length - 1 && splineInterval > 0f)
                {
                    yield return new WaitForSeconds(splineInterval);
                }
            }

            splineSequenceCoroutine = null;
        }

        private IEnumerator AnimateSplineFadeOut()
        {
            SetMainBoosterFxActive(false);
            EnsureSplineGlowData();

            SplineGlowData[] sequence = new SplineGlowData[] { redGlowData, blueGlowData, yellowGlowData, orangeGlowData };
            List<Coroutine> cors = new List<Coroutine>();

            foreach (var data in sequence)
            {
                if (data != null && data.obj != null && data.obj.activeSelf)
                {
                    cors.Add(StartCoroutine(AnimateSplineGlow(data, false, splineFadeOutDuration)));
                }
            }

            foreach (var cor in cors)
            {
                yield return cor;
            }

            splineSequenceCoroutine = null;
        }

        private IEnumerator AnimateSplineGlow(SplineGlowData data, bool fadeIn, float duration)
        {
            if (data == null || data.obj == null || data.material == null) yield break;

            if (fadeIn)
            {
                data.obj.SetActive(true);
            }

            Color fromEmission = fadeIn ? Color.black : data.targetEmission;
            Color toEmission = fadeIn ? data.targetEmission : Color.black;

            Color fromBase = fadeIn ? Color.black : data.targetBaseColor;
            Color toBase = fadeIn ? data.targetBaseColor : Color.black;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = t * t * (3f - 2f * t); // SmoothStep

                if (data.material.HasProperty("_EmissionColor"))
                {
                    data.material.SetColor("_EmissionColor", Color.Lerp(fromEmission, toEmission, curveT));
                }
                if (data.material.HasProperty("_BaseColor"))
                {
                    data.material.SetColor("_BaseColor", Color.Lerp(fromBase, toBase, curveT));
                }

                yield return null;
            }

            if (data.material.HasProperty("_EmissionColor"))
            {
                data.material.SetColor("_EmissionColor", toEmission);
            }
            if (data.material.HasProperty("_BaseColor"))
            {
                data.material.SetColor("_BaseColor", toBase);
            }

            if (!fadeIn)
            {
                data.obj.SetActive(false);
            }
        }

        #endregion

        #region [ 🚀 부스터 VFX 이펙트 제어 (pf_redbooster_1 & pf_redbooster_2) ]

        /// <summary>
        /// 보조 부스터 4종 (pf_redbooster_2, (1), (2), (3)) 점등 / 소등 제어
        /// </summary>
        public void SetSubBoosterFxActive(bool active)
        {
            if (boosterFxSub == null || boosterFxSub.Length == 0 || boosterFxSub[0] == null || boosterEffectRoot == null)
            {
                AutoBindBoosterFx();
            }

            if (boosterFxSub != null)
            {
                for (int i = 0; i < boosterFxSub.Length; i++)
                {
                    SetFxActive(boosterFxSub[i], active);
                }
            }

            CheckAndSyncBoosterRootState();
        }

        /// <summary>
        /// 메인 대형 제트 부스터 (pf_redbooster_1) 점등 / 소등 제어
        /// </summary>
        public void SetMainBoosterFxActive(bool active)
        {
            if (boosterFxMain == null || boosterEffectRoot == null)
            {
                AutoBindBoosterFx();
            }

            SetFxActive(boosterFxMain, active);
            CheckAndSyncBoosterRootState();
        }

        private void SetFxActive(GameObject fxObj, bool active)
        {
            if (fxObj == null) return;

            // Progress can be sampled every frame by a cinematic director. Do not
            // clear/reinitialize an already running flame: its particles need to age
            // to form the exhaust tail. A disabled parent still requires activation.
            if (active ? fxObj.activeInHierarchy : !fxObj.activeSelf) return;

            if (active)
            {
                // 최상위 Booster_effect 부모 오브젝트가 꺼져 있다면 반드시 먼저 켜줌
                if (boosterEffectRoot != null && !boosterEffectRoot.activeSelf)
                {
                    boosterEffectRoot.SetActive(true);
                }

                // 중간 부모 계층이 비활성화되어 있다면 모두 활성화
                Transform p = fxObj.transform.parent;
                while (p != null && p != transform.root)
                {
                    if (!p.gameObject.activeSelf) p.gameObject.SetActive(true);
                    p = p.parent;
                }

                if (!fxObj.activeSelf)
                {
                    fxObj.SetActive(true);
                }

                // 파티클 시스템 및 VFX Graph 즉시 재생 트리거
                foreach (var ps in fxObj.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
                foreach (var vfx in fxObj.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true))
                {
                    vfx.Reinit();
                    vfx.Play();
                }
            }
            else
            {
                if (fxObj.activeSelf)
                {
                    fxObj.SetActive(false);
                }

                foreach (var ps in fxObj.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                foreach (var vfx in fxObj.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true))
                {
                    vfx.Stop();
                    vfx.Reinit();
                }
            }
        }

        private void CheckAndSyncBoosterRootState()
        {
            if (boosterEffectRoot == null) return;

            bool anyChildActive = false;
            if (boosterFxMain != null && boosterFxMain.activeSelf) anyChildActive = true;
            if (boosterFxSub != null)
            {
                foreach (var s in boosterFxSub)
                {
                    if (s != null && s.activeSelf) { anyChildActive = true; break; }
                }
            }

            if (boosterEffectRoot.activeSelf != anyChildActive)
            {
                boosterEffectRoot.SetActive(anyChildActive);
            }
        }

        /// <summary>
        /// 차량 하위(비활성화 상태 포함)에서 Booster_effect, pf_redbooster_1 및 pf_redbooster_2 (1~3)를 완벽하게 탐색하여 연결
        /// </summary>
        public void AutoBindBoosterFx()
        {
            // 최상위 차량 루트 (Blue_Car_Final) 기준으로 전체 탐색 (비활성화 오브젝트 포함)
            Transform searchRoot = transform.root;
            if (searchRoot == null) searchRoot = transform;

            // 0. Booster_effect 부모 오브젝트 탐색
            if (boosterEffectRoot == null)
            {
                Transform bRoot = FindPartFlexible(searchRoot, "Booster_effect");
                if (bRoot == null && targetCarRoot != null) bRoot = FindPartFlexible(targetCarRoot, "Booster_effect");
                if (bRoot == null) bRoot = FindPartFlexible(transform, "Booster_effect");
                if (bRoot != null) boosterEffectRoot = bRoot.gameObject;
            }

            Transform subSearchTarget = (boosterEffectRoot != null) ? boosterEffectRoot.transform : searchRoot;

            // 1. 메인 부스터 (pf_redbooster_1) 탐색
            if (boosterFxMain == null)
            {
                Transform m = FindPartFlexible(subSearchTarget, "PF_RedBooster_1");
                if (m == null) m = FindPartFlexible(searchRoot, "PF_RedBooster_1");
                if (m != null) boosterFxMain = m.gameObject;
            }

            // 2. 보조 부스터 4종 (pf_redbooster_2, pf_redbooster_2 (1), pf_redbooster_2 (2), pf_redbooster_2 (3))
            if (boosterFxSub == null || boosterFxSub.Length != 4)
            {
                boosterFxSub = new GameObject[4];
            }

            string[] subNames = new string[] { "PF_RedBooster_2", "PF_RedBooster_2 (1)", "PF_RedBooster_2 (2)", "PF_RedBooster_2 (3)" };

            for (int i = 0; i < subNames.Length; i++)
            {
                if (boosterFxSub[i] != null) continue;
                Transform s = FindPartFlexible(subSearchTarget, subNames[i]);
                if (s == null) s = FindPartFlexible(searchRoot, subNames[i]);
                if (s != null) boosterFxSub[i] = s.gameObject;
            }

            // 만약 괄호 번호가 조금 다른 경우 대비: Booster_effect 직속 자식 중 PF_RedBooster_2 포함하는 오브젝트 자동 수집
            if (boosterEffectRoot != null)
            {
                int fillIdx = 0;
                for (int c = 0; c < boosterEffectRoot.transform.childCount && fillIdx < 4; c++)
                {
                    Transform child = boosterEffectRoot.transform.GetChild(c);
                    string cName = child.name.ToLowerInvariant();
                    if (cName.Contains("booster_2") || cName.Contains("booster2"))
                    {
                        if (boosterFxSub[fillIdx] == null)
                        {
                            boosterFxSub[fillIdx] = child.gameObject;
                        }
                        fillIdx++;
                    }
                }
            }

            int boundSubCount = 0;
            if (boosterFxSub != null)
            {
                foreach (var s in boosterFxSub) { if (s != null) boundSubCount++; }
            }

            Debug.Log($"[VehicleTransformationController] 🎯 부스터 VFX 연결 완료! Parent: {(boosterEffectRoot != null ? boosterEffectRoot.name : "null")}, Main: {(boosterFxMain != null ? boosterFxMain.name : "null")}, Sub개수: {boundSubCount}/4");
        }

        private Transform FindPartFlexible(Transform root, string targetName)
        {
            if (root == null) return null;
            string cleanTarget = targetName.Replace(" ", "").Replace("_", "").ToLowerInvariant();

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string cleanChild = child.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (cleanChild == cleanTarget)
                {
                    return child;
                }
            }
            return null;
        }

        #endregion

        private IEnumerator AnimateTurbineSpeed(float targetSpeed, float duration, AnimationCurve curve)
        {
            float startSpeed = currentTurbineSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = curve != null ? curve.Evaluate(t) : t;

                currentTurbineSpeed = Mathf.Lerp(startSpeed, targetSpeed, curveT);
                yield return null;
            }

            currentTurbineSpeed = targetSpeed;
            turbineCoroutine = null;
        }

        private void UpdateTurbineRotation()
        {
            if (currentTurbineSpeed <= 0.001f) return;

            if (turbineSpinPivot == null) SetupTurbinePivot();
            Transform target = turbineSpinPivot != null ? turbineSpinPivot : boosterTurbine;
            if (target == null) return;

            Vector3 axis = GetTurbineAxisVector();
            target.Rotate(axis * (currentTurbineSpeed * Time.deltaTime), Space.Self);
        }

        private Vector3 GetTurbineAxisVector()
        {
            switch (turbineAxis)
            {
                case TurbineAxis.Forward_Z: return Vector3.forward;
                case TurbineAxis.Back_NegZ: return Vector3.back;
                case TurbineAxis.Up_Y: return Vector3.up;
                case TurbineAxis.Down_NegY: return Vector3.down;
                case TurbineAxis.Right_X: return Vector3.right;
                case TurbineAxis.Left_NegX: return Vector3.left;
                default: return Vector3.forward;
            }
        }

        /// <summary>
        /// 부스터 노즐의 두 포즈 사이 부드러운 보간
        /// </summary>
        private IEnumerator AnimateBoosterBetweenPoses(TransformPose from, TransformPose to, float duration)
        {
            if (boosterNozzle == null) yield break;

            float elapsed = 0f;
            Quaternion rotFrom = Quaternion.Euler(from.rotationEuler);
            Quaternion rotTo = Quaternion.Euler(to.rotationEuler);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = motionCurve != null ? motionCurve.Evaluate(t) : t;

                boosterNozzle.localPosition = Vector3.Lerp(from.position, to.position, curveT);
                boosterNozzle.localRotation = Quaternion.Slerp(rotFrom, rotTo, curveT);

                yield return null;
            }

            boosterNozzle.localPosition = to.position;
            boosterNozzle.localRotation = rotTo;
        }

        /// <summary>
        /// 5단계: 부품 메인상판덮개 개폐 토글
        /// </summary>
        [ContextMenu("5번 메인상판덮개 개폐 토글")]
        public void ToggleMainCover()
        {
            if (coverCoroutine != null)
            {
                StopCoroutine(coverCoroutine);
            }

            isCoverOpened = !isCoverOpened;

            // 덮개가 열리기 시작할 때, 현재 수납된 보조 벤트의 위치와 회전을 기준점으로 기억
            if (isCoverOpened)
            {
                if (ventLeft != null)
                {
                    ventLeftBeforeCoverPos = ventLeft.localPosition;
                    ventLeftBeforeCoverRot = ventLeft.localRotation;
                }
                if (ventRight != null)
                {
                    ventRightBeforeCoverPos = ventRight.localPosition;
                    ventRightBeforeCoverRot = ventRight.localRotation;
                }
            }

            coverCoroutine = StartCoroutine(AnimateMainCoverSequence(isCoverOpened));
        }

        [ContextMenu("5번 사진 위치/각도 적용 (대기: -142.1도, Y:0.442, Z:-1.131)")]
        public void OptimizeCoverAngles()
        {
            coverStep2 = new TransformPose(new Vector3(0.003f, 0.442f, -1.131f), new Vector3(-142.1f, 180f, 0f));
            coverStep3 = new TransformPose(new Vector3(0.003f, -0.098f, -0.565f), new Vector3(-142.1f, 180f, 0f));
            Debug.Log("[VehicleTransformationController] 5번 상판 덮개 2단계 대기 위치(Y: 0.442, Z: -1.131, Rot: -142.1) 및 3단계 수납 설정을 완벽히 적용했습니다.");
        }

        /// <summary>
        /// 메인상판덮개 개폐 시퀀스 코루틴 (1단계 닫힘 <-> 2단계 틸트 오픈 <-> 3단계 차체 내부 수납)
        /// </summary>
        private IEnumerator AnimateMainCoverSequence(bool open)
        {
            if (open)
            {
                // 만약 카울이 아직 안 올라와서 덮개가 1단계(닫힘)에 있다면 1->2 틸트 오픈 먼저 진행
                if (!isCowlLifted)
                {
                    yield return AnimateCoverBetweenPoses(coverStep1, coverStep2, stepDuration);
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                }

                // 2 -> 3단계: 기울어진 각도를 유지하며 차체 내부로 슬라이드 수납 (조절된 부드러운 속도)
                yield return AnimateCoverBetweenPoses(coverStep2, coverStep3, coverRetractDuration);
            }
            else
            {
                // 3 -> 2단계: 차체 내부에서 위로 슬라이드 상승 (조절된 부드러운 속도)
                yield return AnimateCoverBetweenPoses(coverStep3, coverStep2, coverRetractDuration);

                // 카울이 내려가 있는 상태였다면 2 -> 1로 닫힘 복구
                if (!isCowlLifted)
                {
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                    yield return AnimateCoverBetweenPoses(coverStep2, coverStep1, stepDuration);
                }
            }

            coverCoroutine = null;
        }

        /// <summary>
        /// 메인상판덮개의 두 포즈 사이 부드러운 보간 이동 및 회전 (수납된 보조 벤트 2개 동반 이동)
        /// </summary>
        private IEnumerator AnimateCoverBetweenPoses(TransformPose from, TransformPose to, float duration)
        {
            if (mainCover == null) yield break;

            float elapsed = 0f;
            Quaternion rotFrom = Quaternion.Euler(from.rotationEuler);
            Quaternion rotTo = Quaternion.Euler(to.rotationEuler);

            Quaternion initialCoverRotInv = Quaternion.Inverse(Quaternion.Euler(coverStep1.rotationEuler));
            Vector3 coverPivot = coverStep1.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = motionCurve != null ? motionCurve.Evaluate(t) : t;

                // 1. 메인상판덮개 보간
                mainCover.localPosition = Vector3.Lerp(from.position, to.position, curveT);
                mainCover.localRotation = Quaternion.Slerp(rotFrom, rotTo, curveT);

                // 2. 덮개의 회전량 계산 (coverStep1 기준)
                Quaternion deltaRotCover = mainCover.localRotation * initialCoverRotInv;

                // 3. 수납된 보조 벤트판 1 (좌측) 동반 이동 및 회전
                if (ventLeft != null)
                {
                    ventLeft.localPosition = mainCover.localPosition + deltaRotCover * (ventLeftBeforeCoverPos - coverPivot);
                    ventLeft.localRotation = deltaRotCover * ventLeftBeforeCoverRot;
                }

                // 4. 수납된 보조 벤트판 2 (우측) 동반 이동 및 회전
                if (ventRight != null)
                {
                    ventRight.localPosition = mainCover.localPosition + deltaRotCover * (ventRightBeforeCoverPos - coverPivot);
                    ventRight.localRotation = deltaRotCover * ventRightBeforeCoverRot;
                }

                yield return null;
            }

            // 최종 포즈 고정
            mainCover.localPosition = to.position;
            mainCover.localRotation = rotTo;

            Quaternion finalDeltaRotCover = mainCover.localRotation * initialCoverRotInv;

            if (ventLeft != null)
            {
                ventLeft.localPosition = mainCover.localPosition + finalDeltaRotCover * (ventLeftBeforeCoverPos - coverPivot);
                ventLeft.localRotation = finalDeltaRotCover * ventLeftBeforeCoverRot;
            }

            if (ventRight != null)
            {
                ventRight.localPosition = mainCover.localPosition + finalDeltaRotCover * (ventRightBeforeCoverPos - coverPivot);
                ventRight.localRotation = finalDeltaRotCover * ventRightBeforeCoverRot;
            }
        }

        /// <summary>
        /// 1, 2, 3, 4, 5단계 풀 변형 시퀀스 토글 (전체 순차 전개 / 전체 복구)
        /// 1, 2, 3, 4번이 차례대로 착착착 진행되고, 5번은 4번 시작 후 한 박자 늦게 시작됩니다.
        /// </summary>
        [ContextMenu("🔥 전체 풀 변형 시퀀스 실행 / 토글")]
        public void ToggleFullTransformation()
        {
            if (isFullSequenceRunning) return;

            isFullyTransformed = !isFullyTransformed;
            fullSequenceCoroutine = StartCoroutine(AnimateFullSequence(isFullyTransformed));
        }

        private IEnumerator AnimateFullSequence(bool deploy)
        {
            isFullSequenceRunning = true;

            if (deploy)
            {
                // [1단계] 보조 벤트 수납
                if (!isVentHidden)
                {
                    isVentHidden = true;
                    yield return AnimateVentSequence(true);
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                }

                // [2단계] 3D 스캐너 전개 + 카울 연결부 2개 접기 동시 진행
                if (!isScannerDeployed || !isCouplerFolded)
                {
                    isScannerDeployed = true;
                    isCouplerFolded = true;
                    Coroutine sCor = StartCoroutine(AnimateScannerSequence(true));
                    Coroutine cCor = StartCoroutine(AnimateCouplerSequence(true));
                    yield return sCor;
                    yield return cCor;
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                }

                // [3단계] 기계식 상부 아머드 카울 + 접힌 연결부 + 메인 상판 덮개(1->2) 동반 상승!
                if (!isCowlLifted)
                {
                    isCowlLifted = true;
                    if (couplerLeft != null)
                    {
                        couplerLeftBeforeCowlPos = couplerLeft.localPosition;
                        couplerLeftBeforeCowlRot = couplerLeft.localRotation;
                    }
                    if (ventLeft != null)
                    {
                        ventLeftBeforeCoverPos = ventLeft.localPosition;
                        ventLeftBeforeCoverRot = ventLeft.localRotation;
                    }
                    if (ventRight != null)
                    {
                        ventRightBeforeCoverPos = ventRight.localPosition;
                        ventRightBeforeCoverRot = ventRight.localRotation;
                    }

                    yield return AnimateCowlWithCoverSequence(true);
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                }

                // [4단계 & 5단계] 4번 부스터 노즐 돌출 시작 + 한 박자 늦게 5번 메인상판덮개 수납 (2->3) 시작!
                if (!isBoosterDeployed || !isCoverOpened)
                {
                    isBoosterDeployed = true;
                    isCoverOpened = true;

                    // 4번 부스터 노즐 먼저 출발
                    Coroutine boosterCor = StartCoroutine(AnimateBoosterSequence(true));

                    // 한 박자 늦게 대기 (기본 약 0.08초)
                    if (stage5DelayAfterStage4 > 0) yield return new WaitForSeconds(stage5DelayAfterStage4);

                    // 5번 메인 상판 덮개: 이미 카울과 함께 2단계로 올라와 있으므로, 차체 내부 수납(2->3)만 부드러운 속도로 실행!
                    Coroutine coverCor = StartCoroutine(AnimateCoverBetweenPoses(coverStep2, coverStep3, coverRetractDuration));

                    yield return boosterCor;
                    yield return coverCor;
                }
            }
            else
            {
                // [역순 원복] 5 + 4 -> 3 -> 2 -> 1
                if (isBoosterDeployed || isCoverOpened)
                {
                    isBoosterDeployed = false;
                    isCoverOpened = false;

                    // 상판 덮개 차체 내부에서 2단계로 솟아나옴 + 부스터 수납
                    Coroutine coverCor = StartCoroutine(AnimateCoverBetweenPoses(coverStep3, coverStep2, coverRetractDuration));
                    Coroutine boosterCor = StartCoroutine(AnimateBoosterSequence(false));

                    yield return coverCor;
                    yield return boosterCor;
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                }

                // 3번 원복: 카울 하강과 함께 상판 덮개도 2->1로 원래 자리 하강
                if (isCowlLifted)
                {
                    isCowlLifted = false;
                    yield return AnimateCowlWithCoverSequence(false);
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                }

                if (isScannerDeployed || isCouplerFolded)
                {
                    isScannerDeployed = false;
                    isCouplerFolded = false;
                    Coroutine sCor = StartCoroutine(AnimateScannerSequence(false));
                    Coroutine cCor = StartCoroutine(AnimateCouplerSequence(false));
                    yield return sCor;
                    yield return cCor;
                    if (stepPause > 0) yield return new WaitForSeconds(stepPause);
                }

                if (isVentHidden)
                {
                    isVentHidden = false;
                    yield return AnimateVentSequence(false);
                }
            }

            isFullSequenceRunning = false;
            fullSequenceCoroutine = null;
        }
    }
}
