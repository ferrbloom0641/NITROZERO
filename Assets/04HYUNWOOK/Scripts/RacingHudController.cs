using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class RacingHudController : MonoBehaviour
{
    [Header("UI Toolkit Document")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private VisualTreeAsset tpsDocument;
    [SerializeField] private VisualTreeAsset fpsDocument;

    [Header("Camera View Selection")]
    [Tooltip("카메라별 컷씬 구성을 관리하는 CutsceneManager")]
    [SerializeField] private CutsceneManager cutsceneManager;
    [Tooltip("TPS HUD를 표시할 첫 번째 카메라")]
    [SerializeField] private CutsceneCameraType tpsCamera6 = CutsceneCameraType.Camera6;
    [Tooltip("TPS HUD를 표시할 두 번째 카메라")]
    [SerializeField] private CutsceneCameraType tpsCamera7 = CutsceneCameraType.Camera7;
    [Tooltip("FPS HUD를 표시할 카메라")]
    [SerializeField] private CutsceneCameraType fpsCamera8 = CutsceneCameraType.Camera8;
    [Tooltip("CutsceneManager가 없는 독립 레이싱 씬에서 TPS HUD를 표시합니다.")]
    [SerializeField] private bool showHudWithoutCutsceneManager;

    [Header("Track Settings (X-Axis Progress)")]
    [Tooltip("트랙의 시작 X 좌표")]
    [SerializeField] private float startX = 0f;
    [Tooltip("트랙의 완주 X 좌표")]
    [SerializeField] private float finishX = 1000f;

    [Header("Tachometer Settings")]
    [Tooltip("게이지 바 100% 기준 최고 속도 수치")]
    [SerializeField] private float tachometerMaximum = 300f;
    [Tooltip("실제 차량 속도를 HUD 속도로 환산하는 배율 (600km/h -> 300km/h)")]
    [SerializeField] private float speedDisplayScale = 0.5f;
    [Tooltip("HUD 속도가 초당 증가할 수 있는 최대값")]
    [Min(1f)] [SerializeField] private float speedRiseRate = 180f;
    [Tooltip("HUD 속도가 초당 감소할 수 있는 최대값")]
    [Min(1f)] [SerializeField] private float speedFallRate = 240f;

    [Header("Minimap Settings")]
    [Tooltip("화살표를 실제 위치보다 5~10% 추가 전진시키는 오프셋")]
    [Range(0.05f, 0.10f)]
    [SerializeField] private float arrowForwardOffset = 0.075f;

    #region External Script Compatibility (타 스크립트 참조 에러 방지용)
    private CarCinemachineSetup viewCamera;
    public CarCinemachineSetup ViewCamera
    {
        get
        {
            if (viewCamera == null)
                viewCamera = FindFirstObjectByType<CarCinemachineSetup>();
            return viewCamera;
        }
    }
    public bool StartupShieldClosed => false;
    public float StartupOpacity => 1f;
    public bool IsFirstPersonHud => cutsceneManager != null && cutsceneManager.activeCamera == fpsCamera8;
    public bool IsHudVisible => cutsceneManager != null
        ? cutsceneManager.activeCamera == tpsCamera6 ||
          cutsceneManager.activeCamera == tpsCamera7 ||
          cutsceneManager.activeCamera == fpsCamera8
        : showHudWithoutCutsceneManager;
    public float StartupShieldCoverage => 0f;
    public float StartupShieldOpacity => 0f;
    #endregion

    // 차량 데이터 구조
    private class VehicleData
    {
        public string objectName;
        public string driverId;
        public Transform transform;
        public BoxCollider rankingCollider;
        public Rigidbody rigidbody;
        public CarSpeedController speedController;
        public MidRaceSpeedController midRaceSpeedController;
        public float currentX;
        public float progressRatio;
        public float previousX;
        public bool hasPositionSample;
        public float measuredSpeedKmh;
    }

    private List<VehicleData> vehicles = new List<VehicleData>();
    private VehicleData playerVehicle;

    // UI Elements
    private RacingUI.DigitalReadout speedValueLabel;
    private RacingUI.DigitalReadout gearValueLabel;
    private RacingUI.DigitalReadout coolantValueLabel;
    private VisualElement coolantFill;
    private RacingUI.DigitalReadout fuelPercentLabel;
    private VisualElement fuelFill;
    private RacingUI.DigitalReadout boostValueLabel;
    private VisualElement boostFill;
    private RacingUI.DigitalReadout timeValueLabel;
    private Label routeDistanceLabel;

    // Minimap UI Elements
    private RacingUI.NavigationMap navigationMap;

    // Ranking Rows UI Elements (0 ~ 5)
    private Label[] driverLabels = new Label[6];
    private Label[] progressLabels = new Label[6];

    // RPM Bars (40 bars)
    private VisualElement[] rpmBars = new VisualElement[40];

    // Status
    private float currentTimer = 0f;
    private float displayedSpeedKmh = 0f;
    private bool hasDisplayedSpeedSample = false;
    [SerializeField] private float currentCoolant = 80f;
    [SerializeField] private float currentFuel = 100f;
    [SerializeField] private float currentBoost = 0f;

    private void OnEnable()
    {
        displayedSpeedKmh = 0f;
        hasDisplayedSpeedSample = false;
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;

        if (cutsceneManager == null)
            cutsceneManager = FindFirstObjectByType<CutsceneManager>();
        ApplyCameraDocument();
        BindVisualTree();
        SetHudVisibility();
    }

    private void BindVisualTree()
    {
        VisualElement root = uiDocument.rootVisualElement;

        // 계기판 기본 UI 캐싱
        speedValueLabel = root.Q<RacingUI.DigitalReadout>("speed-value");
        gearValueLabel = root.Q<RacingUI.DigitalReadout>("gear-value");
        timeValueLabel = root.Q<RacingUI.DigitalReadout>("time-value");
        routeDistanceLabel = root.Q<Label>("route-distance");

        // 미니맵 UI 요소 캐싱
        navigationMap = root.Q<RacingUI.NavigationMap>("navigation-road");

        // 냉각수 / 연료 UI
        coolantValueLabel = root.Q<RacingUI.DigitalReadout>("coolant-value");
        coolantFill = root.Q<VisualElement>("coolant-fill");
        fuelPercentLabel = root.Q<RacingUI.DigitalReadout>("fuel-percent");
        fuelFill = root.Q<VisualElement>("fuel-fill");
        boostValueLabel = root.Q<RacingUI.DigitalReadout>("boost-value");
        boostFill = root.Q<VisualElement>("boost-fill");

        // 순위표 캐싱
        for (int i = 0; i < 6; i++)
        {
            driverLabels[i] = root.Q<Label>($"driver-{i}");
            progressLabels[i] = root.Q<Label>($"progress-{i}");
        }

        // RPM 바 캐싱
        for (int i = 0; i < 40; i++)
        {
            rpmBars[i] = root.Q<VisualElement>($"rpm-bar-{i}");
        }
    }

    private void Start()
    {
        InitializeVehicles();
    }

    private void InitializeVehicles()
    {
        vehicles.Clear();
        var mapping = new Dictionary<string, string>
        {
            { "Red_Car", "X-01" },
            { "Blue_Car", "R-07" },
            { "Green_Car", "T-04" },
            { "extraCar1", "A-02" },
            { "extraCar2", "B-03" },
            { "extraCar3", "C-05" }
        };

        foreach (var pair in mapping)
        {
            GameObject obj = FindVehicleObject(pair.Key);
            VehicleData data = new VehicleData
            {
                objectName = pair.Key,
                driverId = pair.Value,
                transform = obj != null ? obj.transform : null,
                rankingCollider = obj != null ? obj.GetComponent<BoxCollider>() : null,
                speedController = obj != null ? obj.GetComponent<CarSpeedController>() : null,
                midRaceSpeedController = obj != null ? obj.GetComponent<MidRaceSpeedController>() : null,
                rigidbody = obj != null
                    ? (obj.GetComponent<Rigidbody>() ?? obj.GetComponentInChildren<Rigidbody>())
                    : null
            };

            vehicles.Add(data);
            if (pair.Key == "Red_Car") playerVehicle = data;
        }
    }

    private void ApplyCameraDocument()
    {
        if (uiDocument == null) return;
        VisualTreeAsset nextDocument = IsFirstPersonHud ? fpsDocument : tpsDocument;
        if (nextDocument == null || uiDocument.visualTreeAsset == nextDocument) return;
        uiDocument.visualTreeAsset = nextDocument;
    }

    private void SetHudVisibility()
    {
        if (uiDocument?.rootVisualElement == null) return;
        uiDocument.rootVisualElement.style.display =
            IsHudVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static GameObject FindVehicleObject(string objectName)
    {
        GameObject vehicle = GameObject.Find(objectName);
        if (vehicle == null && objectName == "Green_Car")
            vehicle = GameObject.Find("Green_Car_SmokeWork_CinematicFinal");
        return vehicle;
    }

    private static float GetVehicleFrontX(VehicleData vehicle)
    {
        if (vehicle.rankingCollider != null && vehicle.rankingCollider.enabled)
            return vehicle.rankingCollider.bounds.max.x;
        return vehicle.transform != null ? vehicle.transform.position.x : float.NegativeInfinity;
    }

    private void Update()
    {
        VisualTreeAsset expectedDocument = IsFirstPersonHud ? fpsDocument : tpsDocument;
        if (expectedDocument != null && uiDocument != null && uiDocument.visualTreeAsset != expectedDocument)
        {
            ApplyCameraDocument();
            BindVisualTree();
        }
        SetHudVisibility();
        UpdateTimer();
        UpdateProgressAndMinimap();
        UpdateInstrumentCluster();
        UpdateGauges();
    }

    private void UpdateTimer()
    {
        currentTimer += Time.deltaTime;
        int minutes = (int)(currentTimer / 60f);
        int seconds = (int)(currentTimer % 60f);
        int milliseconds = (int)((currentTimer * 1000f) % 1000f);

        if (timeValueLabel != null)
            timeValueLabel.text = $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
    }

    private void UpdateProgressAndMinimap()
    {
        float totalTrackLength = Mathf.Max(1f, finishX - startX);

        foreach (var v in vehicles)
        {
            if (v.transform != null)
            {
                // Use the forward face of the parent Box Collider. Child mesh
                // pivots and per-part Mesh Colliders do not affect the ranking.
                v.currentX = GetVehicleFrontX(v);
                v.progressRatio = v.midRaceSpeedController != null &&
                    v.midRaceSpeedController.ProvidesSequenceProgress
                    ? v.midRaceSpeedController.SequenceProgress
                    : Mathf.Clamp01((v.currentX - startX) / totalTrackLength);
                if (v.hasPositionSample && Time.deltaTime > 0f)
                    v.measuredSpeedKmh = Mathf.Abs(v.currentX - v.previousX) / Time.deltaTime * 3.6f;
                v.previousX = v.currentX;
                v.hasPositionSample = true;
            }
        }

        // X축 위치 기준 순위 정렬
        var sortedRank = vehicles.OrderByDescending(v => v.currentX).ToList();
        for (int i = 0; i < 6; i++)
        {
            if (i < sortedRank.Count)
            {
                if (driverLabels[i] != null) driverLabels[i].text = sortedRank[i].driverId;
                if (progressLabels[i] != null) progressLabels[i].text = $"{Mathf.RoundToInt(sortedRank[i].progressRatio * 100f)}%";
            }
        }

        // 미니맵 전진 위치 및 꼬리(Trail) 높이 계산
        if (playerVehicle != null && playerVehicle.transform != null)
        {
            float remainingDistance = Mathf.Max(0f, finishX - playerVehicle.currentX);
            if (routeDistanceLabel != null)
                routeDistanceLabel.text = $"DISTANCE LEFT: {remainingDistance:F0}m";

            if (navigationMap != null)
                navigationMap.SetProgress(playerVehicle.progressRatio, arrowForwardOffset);
        }
    }

    private void UpdateInstrumentCluster()
    {
        if (playerVehicle == null) return;

        // CarSpeedController가 부모 Transform을 직접 이동하므로 자식 Rigidbody의
        // 순간 속도는 사용하지 않는다. 카메라/차체 흔들림에 의한 숫자 튐도 방지한다.
        bool useMidRaceSpeed = playerVehicle.midRaceSpeedController != null &&
            playerVehicle.midRaceSpeedController.IsSequenceActive;
        float actualSpeedKmh = useMidRaceSpeed
            ? playerVehicle.midRaceSpeedController.CurrentSpeedKmh
            : playerVehicle.speedController != null
                ? playerVehicle.speedController.CurrentSpeedKmh
                : playerVehicle.measuredSpeedKmh;
        float targetDisplaySpeed = Mathf.Clamp(
            actualSpeedKmh * Mathf.Max(0f, speedDisplayScale),
            0f, Mathf.Max(1f, tachometerMaximum));
        if (!hasDisplayedSpeedSample)
        {
            displayedSpeedKmh = targetDisplaySpeed;
            hasDisplayedSpeedSample = true;
        }
        else
        {
            float response = targetDisplaySpeed >= displayedSpeedKmh ? speedRiseRate : speedFallRate;
            displayedSpeedKmh = Mathf.MoveTowards(displayedSpeedKmh, targetDisplaySpeed,
                Mathf.Max(1f, response) * Time.deltaTime);
        }
        float speedKmH = displayedSpeedKmh;

        if (speedValueLabel != null)
            speedValueLabel.text = $"{Mathf.RoundToInt(speedKmH):D3}";

        // 50km/h 단위 변속 (1~6단)
        int gear = useMidRaceSpeed
            ? playerVehicle.midRaceSpeedController.CurrentGear
            : playerVehicle.speedController != null
            ? playerVehicle.speedController.CurrentGear
            : Mathf.Clamp(Mathf.CeilToInt(speedKmH / 50f), 1, 6);
        if (gearValueLabel != null) gearValueLabel.text = gear.ToString();

        // 50km/h 단위 내 게이지 계산 및 40km/h 이상 시 레드라인
        float speedInCurrentGear = speedKmH % 50f;
        if (speedKmH >= tachometerMaximum) speedInCurrentGear = 50f;

        float gearGaugeRatio = speedInCurrentGear / 50f;
        int fillBarCount = Mathf.RoundToInt(gearGaugeRatio * 40f);

        bool isRedline = speedInCurrentGear >= 40f;
        Color barColor = isRedline ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.61f, 0.21f);

        for (int i = 0; i < 40; i++)
        {
            if (rpmBars[i] != null)
            {
                if (i < fillBarCount)
                {
                    rpmBars[i].style.opacity = 1.0f;
                    rpmBars[i].style.backgroundColor = barColor;
                }
                else
                {
                    rpmBars[i].style.opacity = 0.12f;
                }
            }
        }
    }

    private void UpdateGauges()
    {
        if (coolantValueLabel != null) coolantValueLabel.text = $"{Mathf.RoundToInt(currentCoolant)}";
        if (coolantFill != null) coolantFill.style.width = Length.Percent(Mathf.Clamp01(currentCoolant / 120f) * 100f);

        if (fuelPercentLabel != null) fuelPercentLabel.text = $"{Mathf.RoundToInt(currentFuel)}";
        if (fuelFill != null) fuelFill.style.width = Length.Percent(Mathf.Clamp01(currentFuel / 100f) * 100f);

        if (boostValueLabel != null) boostValueLabel.text = $"{Mathf.RoundToInt(currentBoost)}";
        if (boostFill != null) boostFill.style.width = Length.Percent(Mathf.Clamp01(currentBoost / 100f) * 100f);
    }
}
