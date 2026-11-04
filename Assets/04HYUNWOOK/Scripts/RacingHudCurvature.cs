using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

/// <summary>Render the editable UIDocument through a subtle curved-screen projection.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
public sealed class RacingHudCurvature : MonoBehaviour
{
    [Range(0f, .12f)] public float curvature = .095f;
    public Shader curvedShader;
    [Tooltip("CarCinemachineSetup이 없는 시네마틱 1인칭 카메라에서도 곡률을 적용합니다.")]
    public bool cinematicFirstPerson;
    UIDocument document;
    RacingHudController hudController;
    PanelSettings originalSettings, runtimeSettings;
    RenderTexture surface;
    Material material;
    GameObject overlay;
    RawImage image;
    int width, height;

    void OnEnable() { document = GetComponent<UIDocument>(); hudController = GetComponent<RacingHudController>(); }
    void LateUpdate()
    {
        bool visible = document != null && document.enabled && document.panelSettings != null;
        if (!visible) return;
        // Apply visor curvature only to the active first-person document, including FPS 2.
        // The authored perimeter still owns its outside mask independently of this projection.
        bool authoredLayout = document.rootVisualElement.Q("racing-hud")?.ClassListContains("production-hud") == true;
        bool firstPerson = document.rootVisualElement.Q("racing-hud")?.ClassListContains("fps-document") == true;
        bool activeHud = hudController != null && hudController.isActiveAndEnabled;
        var camera = activeHud ? hudController.ViewCamera : null;
        bool cinematicActive = cinematicFirstPerson && hudController != null
            && hudController.IsHudVisible && hudController.IsFirstPersonHud;
        bool vehicleCameraActive = hudController != null && hudController.StartupShieldClosed
            && camera != null && camera.isActiveAndEnabled && hudController.IsFirstPerson
            && hudController.EnableHelmetHudCurvature;
        bool applyCurvature = Application.isPlaying && firstPerson && activeHud
            && (cinematicActive || vehicleCameraActive);

        if (!applyCurvature)
        {
            if (image != null) image.enabled = false;
            if (runtimeSettings != null && document.panelSettings == runtimeSettings)
                SwitchPanelSettings(originalSettings);
            return;
        }

        if (runtimeSettings == null) Initialize();
        else if (document.panelSettings != runtimeSettings)
            SwitchPanelSettings(runtimeSettings);
        if (runtimeSettings == null) return;
        if (image != null) image.enabled = true;
        int w = Mathf.Max(64, Screen.width), h = Mathf.Max(64, Screen.height);
        if (surface == null || w != width || h != height) Resize(w, h);
        material.SetFloat("_Curvature", curvature);
        material.SetFloat("_OutsideOpacity", !authoredLayout && activeHud && hudController.IsFirstPersonHud ? hudController.StartupOpacity : 0f);
        material.SetFloat("_ShieldCoverage", activeHud ? hudController.StartupShieldCoverage : 0f);
        material.SetFloat("_ShieldOpacity", activeHud ? hudController.StartupShieldOpacity : 0f);
        // The shader composites this in screen space, after curvature, covering every pixel.
        var shield = document.rootVisualElement.Q("startup-shield");
        if (shield != null) shield.style.display = DisplayStyle.None;
    }

    void Initialize()
    {
        if (curvedShader == null) curvedShader = Shader.Find("UI/Racing HUD Curved");
        if (curvedShader == null) return;
        originalSettings = document.panelSettings;
        runtimeSettings = Instantiate(originalSettings);
        runtimeSettings.name = "Racing HUD curved surface (temporary)";
        runtimeSettings.hideFlags = HideFlags.HideAndDontSave;
        runtimeSettings.clearColor = true;
        runtimeSettings.colorClearValue = Color.clear;
        material = new Material(curvedShader) { hideFlags = HideFlags.HideAndDontSave };
        overlay = new GameObject("Racing HUD curved display", typeof(RectTransform), typeof(Canvas));
        overlay.hideFlags = HideFlags.HideAndDontSave;
        // Keep the temporary display grouped with the vehicle HUD in the
        // hierarchy. ScreenSpaceOverlay rendering is unchanged by this parent.
        overlay.transform.SetParent(transform, false);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = Mathf.Clamp(Mathf.RoundToInt(originalSettings.sortingOrder), -32768, 32767);
        var display = new GameObject("HUD", typeof(RectTransform), typeof(RawImage));
        display.hideFlags = HideFlags.HideAndDontSave;
        display.transform.SetParent(overlay.transform, false);
        image = display.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.material = material;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Resize(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height));
        SwitchPanelSettings(runtimeSettings);
    }

    void Resize(int w, int h)
    {
        var old = surface;
        width = w; height = h;
        surface = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = "Racing HUD surface", hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        surface.Create();
        ClearSurface();
        runtimeSettings.targetTexture = surface;
        image.texture = surface;
        if (old != null) { old.Release(); Release(old); }
    }

    public void ClearSurface()
    {
        if (surface == null || !surface.IsCreated()) return;
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = surface;
            GL.Clear(true, true, Color.clear);
        }
        finally { RenderTexture.active = previous; }
        if (document != null) document.rootVisualElement?.MarkDirtyRepaint();
    }

    // UIDocument unregisters its asset tracker in OnDisable using the CURRENT panel.
    // Disable before changing panels so the previous panel cannot retain a dead document.
    void SwitchPanelSettings(PanelSettings next)
    {
        if (document == null || document.panelSettings == next) return;
        bool wasEnabled = document.enabled;
        bool hadRoot = document.rootVisualElement != null;
        document.enabled = false;
        document.panelSettings = next;
        if (wasEnabled && (hadRoot || !document.gameObject.activeInHierarchy)) document.enabled = true;
    }

    void OnDisable()
    {
        if (document != null && runtimeSettings != null && document.panelSettings == runtimeSettings)
            SwitchPanelSettings(originalSettings);
        if (surface != null) { surface.Release(); Release(surface); }
        Release(overlay); Release(material); Release(runtimeSettings);
        surface = null; overlay = null; material = null; runtimeSettings = null; image = null;
    }

    static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
