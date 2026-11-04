using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(2000)]
public sealed class Camera5FovSequence : MonoBehaviour
{
    [SerializeField] private CutsceneManager cutsceneManager;
    [SerializeField] private CinemachineCamera virtualCamera;
    [Min(0f)] [SerializeField] private float hold60UntilSeconds = 1f;
    [Min(0f)] [SerializeField] private float reach80AtSeconds = 1.5f;
    [Min(0f)] [SerializeField] private float reach90AtSeconds = 3f;
    [Min(1)] [SerializeField] private int fovStepCount = 4;

    float elapsed;
    bool wasActive;

    void Awake()
    {
        if (virtualCamera == null)
            virtualCamera = GetComponent<CinemachineCamera>();
    }

    void LateUpdate()
    {
        bool isActive = cutsceneManager != null &&
            cutsceneManager.activeCamera == CutsceneCameraType.Camera5;
        if (!isActive)
        {
            wasActive = false;
            elapsed = 0f;
            return;
        }

        if (!wasActive)
        {
            elapsed = 0f;
            wasActive = true;
        }
        else
            elapsed += Time.deltaTime;

        if (virtualCamera == null) return;
        float fov;
        if (elapsed <= hold60UntilSeconds)
            fov = 60f;
        else if (elapsed < reach80AtSeconds)
        {
            float t = Mathf.InverseLerp(hold60UntilSeconds, reach80AtSeconds, elapsed);
            float stepped = Mathf.Floor(t * fovStepCount) / fovStepCount;
            fov = Mathf.Lerp(60f, 80f, stepped);
        }
        else if (elapsed < reach90AtSeconds)
        {
            float t = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(reach80AtSeconds, reach90AtSeconds, elapsed));
            fov = Mathf.Lerp(80f, 90f, t);
        }
        else
            fov = 90f;

        LensSettings lens = virtualCamera.Lens;
        lens.FieldOfView = fov;
        virtualCamera.Lens = lens;
    }
}
