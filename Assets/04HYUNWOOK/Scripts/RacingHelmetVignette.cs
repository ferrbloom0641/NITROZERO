using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Camera-owned override also supplies zero in edit mode so shared volume
// defaults cannot darken the helmet preview before gameplay starts.
[ExecuteAlways, DisallowMultipleComponent]
public sealed class RacingHelmetVignette : MonoBehaviour
{
    [Header("Cinematic first-person fallback")]
    [SerializeField] RacingHudController hudOverride;
    [SerializeField] bool cinematicFirstPerson;
    [SerializeField] bool cinematicVignette = true;
    [SerializeField] bool cinematicLensDistortion = true;
    [Range(-1f,1f)] [SerializeField] float cinematicLensIntensity = .7f;
    [Range(.01f,5f)] [SerializeField] float cinematicLensScale = .95f;
    [Range(0f,1f)] [SerializeField] float cinematicVignetteIntensity = .5f;
    [Range(.01f,1f)] [SerializeField] float cinematicVignetteSmoothness = .5f;

    CarCinemachineSetup settings;
    RacingHudController hud;
    Volume overlay;
    VolumeProfile profile;
    Vignette vignette;
    LensDistortion lens;
    UniversalAdditionalCameraData renderCameraData;
    bool previousPostProcessing;
    bool capturedPostProcessing;

    void OnEnable() { settings=GetComponent<CarCinemachineSetup>(); hud=hudOverride; UpdateEffect(); }
    void Update() => UpdateEffect();
    void LateUpdate() => UpdateEffect();
    void UpdateEffect()
    {
        if(!gameObject.scene.IsValid() || !gameObject.scene.isLoaded || (!settings && !cinematicFirstPerson))return;
        if(!overlay)
        {
            var owner=new GameObject("RacingCamera Helmet Vignette (temporary)");
            owner.hideFlags=HideFlags.HideAndDontSave;
            owner.SetActive(false);owner.transform.SetParent(transform,false);
            overlay=owner.AddComponent<Volume>();
            overlay.isGlobal=true;overlay.priority=10000;overlay.weight=1;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags=HideFlags.HideAndDontSave;
            vignette=profile.Add<Vignette>();vignette.hideFlags=HideFlags.HideAndDontSave;
            vignette.intensity.Override(0);
            lens=profile.Add<LensDistortion>();lens.hideFlags=HideFlags.HideAndDontSave;
            lens.intensity.Override(0);lens.scale.Override(1);
            overlay.sharedProfile=profile;owner.SetActive(true);
        }
        overlay.gameObject.layer=gameObject.layer;
        if(settings && hud && hud.ViewCamera!=settings)hud=null;
        if(Application.isPlaying && (!hud || !hud.isActiveAndEnabled))
            foreach(var candidate in FindObjectsByType<RacingHudController>(FindObjectsSortMode.None))
                if(candidate.isActiveAndEnabled && candidate.gameObject.scene==gameObject.scene
                    && (!settings || candidate.ViewCamera==settings)){hud=candidate;break;}
        bool worn=settings
            ? Application.isPlaying && settings.isActiveAndEnabled
                && settings.IsFirstPerson && hud && hud.isActiveAndEnabled && hud.StartupShieldClosed
            : Application.isPlaying && cinematicFirstPerson && hud && hud.isActiveAndEnabled
                && hud.IsHudVisible && hud.IsFirstPersonHud;
        UpdateRenderCameraPostProcessing(worn);
        bool vignetteEnabled=settings ? settings.enableHelmetVignette : cinematicVignette;
        bool lensEnabled=settings ? settings.enableHelmetLensDistortion : cinematicLensDistortion;
        float lensIntensity=settings ? settings.helmetLensDistortionIntensity : cinematicLensIntensity;
        float lensScale=settings ? settings.helmetLensDistortionScale : cinematicLensScale;
        float vignetteIntensity=settings ? settings.helmetVignetteIntensity : cinematicVignetteIntensity;
        float vignetteSmoothness=settings ? settings.helmetVignetteSmoothness : cinematicVignetteSmoothness;
        bool show=worn && vignetteEnabled;
        overlay.priority=worn?10001:10000;
        bool distort=worn && lensEnabled;
        lens.intensity.Override(distort?lensIntensity:0);
        lens.scale.Override(distort?lensScale:1);
        vignette.intensity.Override(show?Mathf.Clamp01(vignetteIntensity)*hud.StartupOpacity:0);
        vignette.smoothness.Override(Mathf.Clamp(vignetteSmoothness,.01f,1));
        vignette.color.Override(Color.black);
        vignette.center.Override(new Vector2(.5f,.5f));
        vignette.rounded.Override(false);
    }

    void UpdateRenderCameraPostProcessing(bool enabledForHelmet)
    {
        if (!renderCameraData)
        {
            Camera renderCamera = Camera.main;
            if (renderCamera) renderCameraData = renderCamera.GetComponent<UniversalAdditionalCameraData>();
            if (renderCameraData && !capturedPostProcessing)
            {
                previousPostProcessing = renderCameraData.renderPostProcessing;
                capturedPostProcessing = true;
            }
        }

        if (renderCameraData)
            renderCameraData.renderPostProcessing = enabledForHelmet;
    }

    void OnDisable()
    {
        if(renderCameraData && capturedPostProcessing)
            renderCameraData.renderPostProcessing=previousPostProcessing;
        if(overlay){overlay.enabled=false;overlay.sharedProfile=null;Release(overlay.gameObject);}
        if(profile){foreach(var component in profile.components)Release(component);Release(profile);}
        overlay=null;profile=null;vignette=null;lens=null;hud=null;renderCameraData=null;capturedPostProcessing=false;
    }
    static void Release(Object value)
    {
        if(!value)return;
        if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
    }
}
