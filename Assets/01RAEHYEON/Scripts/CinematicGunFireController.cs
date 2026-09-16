using UnityEngine;

public class CinematicGunFireController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private GunProjectile projectilePrefab;
    [SerializeField] private GunSlideRecoil recoil;

    [Header("Projectile")]
    [Min(1f)]
    [SerializeField] private float projectileSpeed = 300f;

    [Header("Timeline Control")]
    [Range(0f, 1f)]
    [SerializeField] private float progress = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float firePoint = 0.5f;

    private float previousProgress = 0f;
    private bool hasFired = false;

    public float Progress
    {
        get => progress;
        set => SetProgress(value);
    }

    private void Awake()
    {
        if (recoil == null)
            recoil = GetComponent<GunSlideRecoil>();

        if (recoil != null)
            recoil.previewWithLeftMouse = false;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        CheckProgress();
    }

    private void CheckProgress()
    {
        // Timeline을 뒤로 돌렸을 때 다시 발사 가능하도록 초기화
        if (progress < previousProgress)
        {
            hasFired = false;
        }

        // 지정한 Fire Point를 통과하는 순간 1회 발사
        if (!hasFired &&
            previousProgress < firePoint &&
            progress >= firePoint)
        {
            Fire();
        }

        previousProgress = progress;
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
    }

    public void Fire()
    {
        if (!Application.isPlaying)
            return;

        if (hasFired)
            return;

        if (muzzle == null)
        {
            Debug.LogWarning(
                "[CinematicGunFireController] Muzzle이 없습니다."
            );
            return;
        }

        if (projectilePrefab == null)
        {
            Debug.LogWarning(
                "[CinematicGunFireController] Projectile Prefab이 없습니다."
            );
            return;
        }

        GunProjectile shot = Instantiate(
            projectilePrefab,
            muzzle.position,
            muzzle.rotation
        );

        float scale = Mathf.Max(
            0.0001f,
            Mathf.Abs(transform.lossyScale.x)
        );

        shot.transform.localScale *= scale;
        shot.radius *= scale;

        shot.Launch(
            transform,
            projectileSpeed
        );

        if (recoil != null)
            recoil.PlayRecoil();

        hasFired = true;
    }

    public void ResetFire()
    {
        hasFired = false;
        previousProgress = 0f;
        progress = 0f;
    }
}