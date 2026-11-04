using UnityEngine;
using UnityEngine.Playables; // Timeline 제어를 위한 네임스페이스
using Unity.Cinemachine;     // Unity 6 / Cinemachine v3 기준

public enum CutsceneCameraType
{
    Camera1,
    Camera2,
    Camera3,
    Camera4,
    Camera5,
    Camera6,
    Camera7,
    Camera8
}

[ExecuteAlways] // 에디터(플레이 전) 상태에서도 실시간 작동
public class CutsceneManager : MonoBehaviour
{
    [Header("8개의 시네마틱 카메라 등록")]
    public CinemachineCamera camera1;
    public CinemachineCamera camera2;
    public CinemachineCamera camera3;
    public CinemachineCamera camera4;
    public CinemachineCamera camera5;
    public CinemachineCamera camera6;
    public CinemachineCamera camera7;
    public CinemachineCamera camera8;

    [Header("카메라별 차량 그룹")]
    [Tooltip("Camera 5에서만 표시할 차량 그룹입니다.")]
    [SerializeField] private GameObject camera5VehicleRoot;
    [Tooltip("별도 Camera 7/8 그룹이 없으면 Camera 6~8에서, 있으면 Camera 6에서만 표시할 차량 그룹입니다.")]
    [SerializeField] private GameObject camera6VehicleRoot;
    [Tooltip("Camera 7에서만 표시할 차량 그룹입니다.")]
    [SerializeField] private GameObject camera7VehicleRoot;
    [Tooltip("Camera 8에서만 표시할 차량 그룹입니다.")]
    [SerializeField] private GameObject camera8VehicleRoot;

    [Header("플레이 전 시점 선택 (드롭다운)")]
    public CutsceneCameraType activeCamera = CutsceneCameraType.Camera1;

    [Header("자동 플레이 종료 설정")]
    [Tooltip("체크하면 컷씬(Timeline) 재생 완료 시 플레이 모드가 자동으로 꺼집니다.")]
    public bool autoStopOnFinish = true;

    [Tooltip("연동할 Timeline의 PlayableDirector (비워두면 이 오브젝트에서 자동으로 찾습니다)")]
    public PlayableDirector playableDirector;

    [Tooltip("Timeline이 없을 경우 사용할 백업 타이머(초)")]
    public float fallbackDuration = 3.0f;

    [Header("카메라별 재생 시간 (최대 5초)")]
    [Tooltip("Timeline을 사용하지 않는 카메라는 아래의 개별 시간으로 자동 종료합니다.")]
    public bool usePerCameraDurations = true;
    [Range(0.1f, 5f)] public float camera1Duration = 5f;
    [Range(0.1f, 5f)] public float camera2Duration = 4.5f;
    [Range(0.1f, 5f)] public float camera3Duration = 4f;
    [Range(0.1f, 5f)] public float camera4Duration = 5f;
    [Range(0.1f, 5f)] public float camera5Duration = 4f;
    [Range(0.1f, 5f)] public float camera6Duration = 4f;
    [Range(0.1f, 5f)] public float camera7Duration = 5f;
    [Range(0.1f, 5f)] public float camera8Duration = 5f;

    [Tooltip("체크하면 지정한 카메라를 선택했을 때만 연결된 Timeline을 재생하고 자동 종료합니다.")]
    public bool restrictTimelineToCamera;

    [Tooltip("연결된 Timeline을 사용할 카메라입니다.")]
    public CutsceneCameraType timelineCamera = CutsceneCameraType.Camera1;

    [Tooltip("플레이 시작 시 연결된 Timeline을 0초부터 재생합니다.")]
    public bool playTimelineFromStart = true;

    private bool timelineStopSubscribed;
    private bool stopRequested;

    public CinemachineCamera ActiveCamera => GetCamera(activeCamera);

    public float ActiveCameraDuration
    {
        get
        {
            if (!usePerCameraDurations)
                return Mathf.Clamp(fallbackDuration, 0.1f, 5f);

            float duration = activeCamera switch
            {
                CutsceneCameraType.Camera1 => camera1Duration,
                CutsceneCameraType.Camera2 => camera2Duration,
                CutsceneCameraType.Camera3 => camera3Duration,
                CutsceneCameraType.Camera4 => camera4Duration,
                CutsceneCameraType.Camera5 => camera5Duration,
                CutsceneCameraType.Camera6 => camera6Duration,
                CutsceneCameraType.Camera7 => camera7Duration,
                CutsceneCameraType.Camera8 => camera8Duration,
                _ => fallbackDuration
            };
            return Mathf.Clamp(duration, 0.1f, 5f);
        }
    }

    public bool IsActiveCamera(CinemachineCamera camera)
    {
        return camera != null && ActiveCamera == camera;
    }

    private CinemachineCamera GetCamera(CutsceneCameraType type)
    {
        switch (type)
        {
            case CutsceneCameraType.Camera1: return camera1;
            case CutsceneCameraType.Camera2: return camera2;
            case CutsceneCameraType.Camera3: return camera3;
            case CutsceneCameraType.Camera4: return camera4;
            case CutsceneCameraType.Camera5: return camera5;
            case CutsceneCameraType.Camera6: return camera6;
            case CutsceneCameraType.Camera7: return camera7;
            case CutsceneCameraType.Camera8: return camera8;
            default: return null;
        }
    }

    private void OnEnable()
    {
        stopRequested = false;
        // 게임 실행(Play 모드) 시에만 자동 종료 타이머/이벤트 작동
        if (Application.isPlaying && autoStopOnFinish)
        {
            bool shouldUseTimeline = !restrictTimelineToCamera || activeCamera == timelineCamera;
            if (shouldUseTimeline && playableDirector == null)
            {
                playableDirector = GetComponent<PlayableDirector>();
            }

            if (shouldUseTimeline && playableDirector != null)
            {
                // Timeline 재생 종료 이벤트 등록
                playableDirector.stopped += OnTimelineStopped;
                timelineStopSubscribed = true;

                // 카메라의 기본 Transform이 첫 프레임에 노출되지 않도록
                // Timeline의 0초 상태를 먼저 평가한 뒤 재생합니다.
                if (playTimelineFromStart)
                {
                    playableDirector.time = 0d;
                    playableDirector.Evaluate();
                    playableDirector.Play();
                }

                // PlayableDirector의 Wrap Mode가 Hold이면 마지막 프레임에서
                // stopped 이벤트가 오지 않을 수 있습니다. Camera 5의 지정
                // 시간에도 종료를 예약해 어느 모드에서도 자동 종료합니다.
                CancelInvoke(nameof(StopPlayMode));
                Invoke(nameof(StopPlayMode), ActiveCameraDuration);
            }
            else
            {
                // Timeline 대상이 아닌 카메라 또는 Timeline이 없는 씬은
                // 선택된 카메라의 개별 재생 시간을 사용합니다.
                Invoke(nameof(StopPlayMode), ActiveCameraDuration);
            }
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(StopPlayMode));
        // 이벤트 해제
        if (playableDirector != null && timelineStopSubscribed)
        {
            playableDirector.stopped -= OnTimelineStopped;
            timelineStopSubscribed = false;
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall -= DelayedEditorCameraUpdate;
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Cinemachine priority changes can rebuild an editor camera.  Defer the
        // change until the Inspector has finished drawing the previous target.
        UnityEditor.EditorApplication.delayCall -= DelayedEditorCameraUpdate;
        UnityEditor.EditorApplication.delayCall += DelayedEditorCameraUpdate;
#else
        UpdateActiveCamera();
#endif
    }

#if UNITY_EDITOR
    private void DelayedEditorCameraUpdate()
    {
        if (this == null) return;
        UpdateActiveCamera();
    }
#endif

    private void Awake()
    {
        UpdateActiveCamera();
    }

    public void UpdateActiveCamera()
    {
        SetCameraPriority(camera1, activeCamera == CutsceneCameraType.Camera1);
        SetCameraPriority(camera2, activeCamera == CutsceneCameraType.Camera2);
        SetCameraPriority(camera3, activeCamera == CutsceneCameraType.Camera3);
        SetCameraPriority(camera4, activeCamera == CutsceneCameraType.Camera4);
        SetCameraPriority(camera5, activeCamera == CutsceneCameraType.Camera5);
        SetCameraPriority(camera6, activeCamera == CutsceneCameraType.Camera6);
        SetCameraPriority(camera7, activeCamera == CutsceneCameraType.Camera7);
        SetCameraPriority(camera8, activeCamera == CutsceneCameraType.Camera8);
        UpdateVehicleRootVisibility();
    }

    private void UpdateVehicleRootVisibility()
    {
        bool showCamera5Vehicles = activeCamera == CutsceneCameraType.Camera5;
        bool hasSeparateCamera7Or8Vehicles =
            camera7VehicleRoot != null || camera8VehicleRoot != null;
        bool showCamera6Vehicles = hasSeparateCamera7Or8Vehicles
            ? activeCamera == CutsceneCameraType.Camera6
            : activeCamera >= CutsceneCameraType.Camera6;
        bool showCamera7Vehicles = activeCamera == CutsceneCameraType.Camera7;
        bool showCamera8Vehicles = activeCamera == CutsceneCameraType.Camera8;

        if (camera5VehicleRoot != null &&
            camera5VehicleRoot.activeSelf != showCamera5Vehicles)
            camera5VehicleRoot.SetActive(showCamera5Vehicles);
        if (camera6VehicleRoot != null &&
            camera6VehicleRoot.activeSelf != showCamera6Vehicles)
            camera6VehicleRoot.SetActive(showCamera6Vehicles);
        if (camera7VehicleRoot != null &&
            camera7VehicleRoot.activeSelf != showCamera7Vehicles)
            camera7VehicleRoot.SetActive(showCamera7Vehicles);
        if (camera8VehicleRoot != null &&
            camera8VehicleRoot.activeSelf != showCamera8Vehicles)
            camera8VehicleRoot.SetActive(showCamera8Vehicles);
    }

    private void SetCameraPriority(CinemachineCamera cam, bool isActive)
    {
        if (cam != null)
        {
            cam.Priority = isActive ? 20 : 10;
        }
    }

    // Timeline 재생 완료 시 호출
    private void OnTimelineStopped(PlayableDirector director)
    {
        StopPlayMode();
    }

    // 유니티 플레이 모드 자동 종료 함수
    private void StopPlayMode()
    {
        if (stopRequested) return;
        stopRequested = true;
        CancelInvoke(nameof(StopPlayMode));
        Debug.Log("🎬 컷씬 재생이 완료되어 플레이 모드를 자동으로 종료합니다.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터 플레이 종료
#else
        Application.Quit(); // 빌드된 게임 종료
#endif
    }
}
