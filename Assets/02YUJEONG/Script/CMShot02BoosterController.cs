using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace YUJEONG
{
    /// <summary>
    /// CM_Shot02 가상 카메라 전용 부스터 & 부스터 이펙트 ON/OFF 컨트롤러
    /// 인스펙터에서 체크박스 하나로 두 차량의 부스터 전개 및 VFX 이펙트를 간편하게 켜고 끌 수 있습니다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CMShot02BoosterController : MonoBehaviour
    {
        [Header("[ ⚙️ 전체 부스터 & 이펙트 마스터 설정 ]")]
        [Tooltip("체크 해제 시 부스터 변형/노즐 전개를 끄고 기본 차체 모습으로 유지합니다.")]
        [SerializeField] private bool enableBooster = false;

        [Tooltip("체크 해제 시 불꽃, 연기, 파티클, 네온 등 모든 부스터 VFX 이펙트를 완전히 끕니다.")]
        [SerializeField] private bool enableBoosterEffects = false;

        [Header("[ 🏎️ 대상 차량 자동 연결 (비워둘 시 자동 탐색) ]")]
        [Tooltip("파란차 부스터 변형 컨트롤러")]
        public VehicleTransformationController blueCarController;

        [Tooltip("빨간차 부스터 전개 컨트롤러")]
        public BoosterDeploymentController redCarController;

        [Tooltip("파란차 최상위 오브젝트")]
        public GameObject blueCarRoot;

        [Tooltip("빨간차 최상위 오브젝트")]
        public GameObject redCarRoot;

        [Header("[ 🛠️ 부스터 이펙트 수동/캐시 목록 ]")]
        [SerializeField] private List<GameObject> cachedEffectObjects = new List<GameObject>();
        [SerializeField] private List<ParticleSystem> cachedParticleSystems = new List<ParticleSystem>();

        // 상태 추적
        private bool lastBoosterState;
        private bool lastEffectState;

        public bool EnableBooster
        {
            get => enableBooster;
            set
            {
                enableBooster = value;
                ApplySettings();
            }
        }

        public bool EnableBoosterEffects
        {
            get => enableBoosterEffects;
            set
            {
                enableBoosterEffects = value;
                ApplySettings();
            }
        }

        private void OnEnable()
        {
            AutoFindReferences();
            ApplySettings();
            lastBoosterState = enableBooster;
            lastEffectState = enableBoosterEffects;
        }

        private void Start()
        {
            AutoFindReferences();
            ApplySettings();
        }

        private void Update()
        {
            // 인스펙터 값 변경 감지 시 즉시 반영
            if (enableBooster != lastBoosterState || enableBoosterEffects != lastEffectState)
            {
                lastBoosterState = enableBooster;
                lastEffectState = enableBoosterEffects;
                ApplySettings();
            }
        }

        private void OnValidate()
        {
            // 에디터에서 체크박스 클릭 즉시 반영
            AutoFindReferences();
            ApplySettings();
            lastBoosterState = enableBooster;
            lastEffectState = enableBoosterEffects;
        }

        /// <summary>
        /// 씬에서 파란차와 빨간차, 컨트롤러, 이펙트 오브젝트들을 자동으로 찾습니다.
        /// </summary>
        [ContextMenu("대상 차량 및 이펙트 자동 탐색")]
        public void AutoFindReferences()
        {
            if (blueCarController == null)
            {
                blueCarController = FindFirstObjectByType<VehicleTransformationController>();
            }

            if (redCarController == null)
            {
                redCarController = FindFirstObjectByType<BoosterDeploymentController>();
            }

            if (blueCarRoot == null)
            {
                if (blueCarController != null) blueCarRoot = blueCarController.gameObject;
                else blueCarRoot = GameObject.Find("Blue_Car_Final_Booster");
            }

            if (redCarRoot == null)
            {
                if (redCarController != null) redCarRoot = redCarController.gameObject;
                else redCarRoot = GameObject.Find("Red_Car_Final_Booster");
            }

            CollectAllBoosterEffects();
        }

        /// <summary>
        /// 두 차량 하위에 있는 부스터 이펙트 오브젝트 및 파티클들을 캐싱합니다.
        /// </summary>
        private void CollectAllBoosterEffects()
        {
            cachedEffectObjects.Clear();
            cachedParticleSystems.Clear();

            CollectEffectsFromRoot(blueCarRoot);
            CollectEffectsFromRoot(redCarRoot);

            // 파란차 컨트롤러 필드 직접 캐시
            if (blueCarController != null)
            {
                if (blueCarController.boosterEffectRoot != null) AddEffectObject(blueCarController.boosterEffectRoot);
                if (blueCarController.boosterFxMain != null) AddEffectObject(blueCarController.boosterFxMain);
                if (blueCarController.boosterFxSub != null)
                {
                    foreach (var fx in blueCarController.boosterFxSub)
                    {
                        if (fx != null) AddEffectObject(fx);
                    }
                }
                if (blueCarController.boosterNeon != null) AddEffectObject(blueCarController.boosterNeon);
                if (blueCarController.splineRed != null) AddEffectObject(blueCarController.splineRed);
                if (blueCarController.splineBlue != null) AddEffectObject(blueCarController.splineBlue);
                if (blueCarController.splineYellow != null) AddEffectObject(blueCarController.splineYellow);
                if (blueCarController.splineOrange != null) AddEffectObject(blueCarController.splineOrange);
            }
        }

        private void CollectEffectsFromRoot(GameObject root)
        {
            if (root == null) return;

            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allChildren)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("booster_effect") || n.Contains("pf_redbooster") || n.Contains("boostervfx") ||
                    n.Contains("booster_vfx") || n.Contains("boosterfire") || n.Contains("boosterflame") ||
                    n.Contains("exhaust_fire") || n.Contains("jet_fire"))
                {
                    AddEffectObject(t.gameObject);
                }

                var ps = t.GetComponent<ParticleSystem>();
                if (ps != null && (n.Contains("boost") || n.Contains("fire") || n.Contains("flame") || n.Contains("thrust") || n.Contains("smoke") || n.Contains("exhaust")))
                {
                    if (!cachedParticleSystems.Contains(ps))
                    {
                        cachedParticleSystems.Add(ps);
                    }
                }
            }
        }

        private void AddEffectObject(GameObject go)
        {
            if (go != null && !cachedEffectObjects.Contains(go))
            {
                cachedEffectObjects.Add(go);
            }
        }

        /// <summary>
        /// 인스펙터의 설정(enableBooster, enableBoosterEffects)을 차량 및 이펙트에 즉시 적용합니다.
        /// </summary>
        [ContextMenu("지금 즉시 적용 (Apply Now)")]
        public void ApplySettings()
        {
            ApplyBoosterMeshSettings();
            ApplyBoosterEffectSettings();
        }

        private void ApplyBoosterMeshSettings()
        {
            // 1. 파란차 부스터 전개 설정
            if (blueCarController != null)
            {
                blueCarController.startWithFullTransformation = enableBooster;

                if (!enableBooster)
                {
                    // 변형 접기 (미변형 기본 상태로 복귀)
                    blueCarController.SnapToNormalState();
                }
                else
                {
                    // 부스터 전개 (100% 완료 상태)
                    blueCarController.SnapToFullTransformation();
                }
            }

            // 2. 빨간차 부스터 전개 설정
            if (redCarController != null)
            {
                if (!enableBooster)
                {
                    // 부스터를 미전개(0%) 상태로 접음
                    redCarController.SnapToFullRetraction();
                }
                else
                {
                    // 부스터 전개(100%)
                    redCarController.SnapToFullDeployment();
                }
            }
        }

        private void ApplyBoosterEffectSettings()
        {
            // 1. 캐시된 이펙트 게임오브젝트 On/Off
            foreach (var fx in cachedEffectObjects)
            {
                if (fx != null && fx.activeSelf != enableBoosterEffects)
                {
                    fx.SetActive(enableBoosterEffects);
                }
            }

            // 2. 파티클 시스템 일괄 정지/재생
            foreach (var ps in cachedParticleSystems)
            {
                if (ps != null)
                {
                    if (!enableBoosterEffects)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                    else
                    {
                        if (!ps.isPlaying) ps.Play(true);
                    }
                }
            }

            // 3. 컨트롤러 내부 VFX 처리
            if (blueCarController != null)
            {
                blueCarController.SetSubBoosterFxActive(enableBoosterEffects);
                blueCarController.SetMainBoosterFxActive(enableBoosterEffects);

                if (blueCarController.boosterNeon != null && blueCarController.boosterNeon.activeSelf != enableBoosterEffects)
                {
                    blueCarController.boosterNeon.SetActive(enableBoosterEffects);
                }

                if (!enableBoosterEffects)
                {
                    if (blueCarController.splineRed != null) blueCarController.splineRed.SetActive(false);
                    if (blueCarController.splineBlue != null) blueCarController.splineBlue.SetActive(false);
                    if (blueCarController.splineYellow != null) blueCarController.splineYellow.SetActive(false);
                    if (blueCarController.splineOrange != null) blueCarController.splineOrange.SetActive(false);
                }
            }

            if (redCarController != null)
            {
                var type = redCarController.GetType();
                var effectRootField = type.GetField("boosterEffectRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                if (effectRootField != null)
                {
                    GameObject rootGo = effectRootField.GetValue(redCarController) as GameObject;
                    if (rootGo != null && rootGo.activeSelf != enableBoosterEffects)
                    {
                        rootGo.SetActive(enableBoosterEffects);
                    }
                }
            }
        }
    }
}
