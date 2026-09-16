using Damin.VFX.AirDistortion.Reusable;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Cinematic/Bullet Air Distortion Binder")]
public sealed class BulletAirDistortionCinematicBinder : MonoBehaviour
{
    [SerializeField] private ReusableBulletAirDistortion distortion;
    [SerializeField] private Transform bulletRoot;
    [SerializeField] private Camera viewCamera;

    private void Reset()
    {
        bulletRoot = transform;
    }

    private void Awake()
    {
        Bind();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        if (distortion != null && distortion.Target == EffectiveBulletRoot)
            distortion.Detach();
    }

    [ContextMenu("Bind Cinematic Distortion")]
    public void Bind()
    {
        if (distortion == null)
            return;

        DisablePreviewDrivers();
        distortion.Bind(EffectiveBulletRoot, viewCamera);
        distortion.MeasureTargetSpeed = false;
        distortion.ClearTrail();
    }

    private Transform EffectiveBulletRoot => bulletRoot != null ? bulletRoot : transform;

    private void DisablePreviewDrivers()
    {
        var previewFlight = distortion.GetComponent<AirPreviewFlight>();
        if (previewFlight != null)
            previewFlight.enabled = false;

        var previewCamera = distortion.GetComponent<AirPreviewCameraFollow>();
        if (previewCamera != null)
            previewCamera.enabled = false;
    }
}
