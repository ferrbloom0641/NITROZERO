using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CockpitDisplayController : MonoBehaviour
{
    [SerializeField] private MidRaceSpeedController speedController;
    [SerializeField] private RacingHudController hudController;
    [Min(0f)] [SerializeField] private float refreshInterval = 0f;

    static readonly Color32 Background = new Color32(55, 43, 46, 255);
    static readonly Color32 Amber = new Color32(255, 84, 40, 255);
    static readonly Color32 DimAmber = new Color32(135, 61, 43, 255);
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    sealed class Surface
    {
        public Renderer renderer;
        public Texture2D texture;
        public Color32[] pixels;
        public Color32[] originalPixels;
        public MaterialPropertyBlock properties;
        public int width;
        public int height;
    }

    Surface cluster;
    Surface dash;
    Surface tunnel;
    float nextRefresh;

    static readonly Dictionary<char, int[]> Font = new Dictionary<char, int[]>
    {
        ['A']=new[]{14,17,17,31,17,17,17}, ['B']=new[]{30,17,17,30,17,17,30},
        ['C']=new[]{15,16,16,16,16,16,15}, ['D']=new[]{30,17,17,17,17,17,30},
        ['E']=new[]{31,16,16,30,16,16,31}, ['F']=new[]{31,16,16,30,16,16,16},
        ['G']=new[]{15,16,16,23,17,17,15}, ['I']=new[]{31,4,4,4,4,4,31},
        ['L']=new[]{16,16,16,16,16,16,31}, ['M']=new[]{17,27,21,21,17,17,17},
        ['O']=new[]{14,17,17,17,17,17,14}, ['P']=new[]{30,17,17,30,16,16,16},
        ['R']=new[]{30,17,17,30,20,18,17}, ['S']=new[]{15,16,16,14,1,1,30},
        ['T']=new[]{31,4,4,4,4,4,4}, ['U']=new[]{17,17,17,17,17,17,14},
        [' ']=new[]{0,0,0,0,0,0,0}, ['%']=new[]{17,2,4,4,8,16,17}
    };

    void Awake()
    {
        if (speedController == null) speedController = GetComponent<MidRaceSpeedController>();
        if (hudController == null) hudController = GetComponentInChildren<RacingHudController>(true);
        cluster = CreateSurface("Disp_Cluster_Screen", "Camera8 Cluster Live");
        dash = CreateSurface("Disp_Monitor_Dash", "Camera8 Dash Live");
        tunnel = CreateSurface("Disp_Monitor_Tunnel", "Camera8 Tunnel Live");
        RefreshDisplays();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        // Camera 7 차량 세트가 화면에 있을 때 Camera 8의 고해상도 실내
        // 텍스처를 갱신하지 않습니다.
        if (hudController != null &&
            (!hudController.IsHudVisible || !hudController.IsFirstPersonHud))
            return;
        if (refreshInterval > 0f)
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
        }
        RefreshDisplays();
    }

    void OnDisable()
    {
        Restore(cluster); Restore(dash); Restore(tunnel);
    }

    Surface CreateSurface(string objectName, string textureName)
    {
        Renderer renderer = GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(value => value.name == objectName);
        if (renderer == null) return null;

        Texture source = renderer.sharedMaterial != null
            ? renderer.sharedMaterial.GetTexture(BaseMapId)
            : null;
        if (source == null && renderer.sharedMaterial != null)
            source = renderer.sharedMaterial.GetTexture(MainTexId);

        int width = source != null ? source.width : 256;
        int height = source != null ? source.height : 128;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        if (source != null)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            texture.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }

        var surface = new Surface
        {
            renderer = renderer,
            texture = texture,
            pixels = texture.GetPixels32(),
            properties = new MaterialPropertyBlock(),
            width = width,
            height = height
        };
        surface.originalPixels = (Color32[])surface.pixels.Clone();
        ApplyTexture(surface);
        return surface;
    }

    void ApplyTexture(Surface surface)
    {
        if (surface?.renderer == null) return;
        surface.renderer.GetPropertyBlock(surface.properties);
        surface.properties.SetTexture(BaseMapId, surface.texture);
        surface.properties.SetTexture(MainTexId, surface.texture);
        surface.properties.SetTexture(EmissionMapId, surface.texture);
        surface.properties.SetColor(BaseColorId, Color.white);
        surface.properties.SetColor(ColorId, Color.white);
        surface.properties.SetColor(EmissionColorId, Color.white * 1.4f);
        surface.renderer.SetPropertyBlock(surface.properties);
    }

    void Restore(Surface surface)
    {
        if (surface?.renderer != null) surface.renderer.SetPropertyBlock(null);
        if (surface?.texture == null) return;
        if (Application.isPlaying) Destroy(surface.texture); else DestroyImmediate(surface.texture);
    }

    void RefreshDisplays()
    {
        float speed = speedController != null ? speedController.DisplaySpeedKmh : 0f;
        int gear = speedController != null ? speedController.CurrentGear : 1;
        float coolant = hudController != null ? hudController.CurrentCoolantValue : 95f;
        float fuel = hudController != null ? hudController.CurrentFuelValue : 74f;
        float boost = hudController != null ? hudController.CurrentBoostValue : 66f;
        float timer = hudController != null ? hudController.CurrentTimerSeconds : 76f;

        DrawCluster(speed, gear);
        DrawDash(coolant, fuel, boost);
        DrawTunnel(timer, coolant);
    }

    void DrawCluster(float speed, int gear)
    {
        if (cluster == null) return;
        RestoreOriginal(cluster);

        // 원본 대형 SPEED 숫자 영역만 지우고 같은 위치에 실시간 속도를 표시합니다.
        Rect(cluster, 92, 42, 350, 225, Background);
        Seven(cluster, Mathf.RoundToInt(speed).ToString("000"), 105, 58, 90, 180, 18, 12, Amber);

        // 원본 TACHOMETER 막대 영역을 현재 단수 내 속도 비율로 다시 그립니다.
        Rect(cluster, 492, 115, 785, 83, Background);
        float rpm = Mathf.Clamp01((speed % 50f) / 50f);
        for (int i = 0; i < 31; i++)
        {
            int barHeight = 18 + Mathf.RoundToInt(i * 1.55f);
            Rect(cluster, 500 + i * 25, 191 - barHeight, 15, barHeight,
                i / 30f <= rpm ? Amber : DimAmber);
        }

        // 원본 우측 연료 수치 위치를 유지합니다.
        Rect(cluster, 1512, 130, 188, 145, Background);
        Seven(cluster, Mathf.RoundToInt(hudController != null ? hudController.CurrentFuelValue : 74f).ToString("00"),
            1530, 140, 70, 120, 14, 10, Amber);
        Upload(cluster);
    }

    void DrawDash(float coolant, float fuel, float boost)
    {
        if (dash == null) return;
        RestoreOriginal(dash);

        // SYSTEMS 패널의 원래 FUEL P / BOOST 숫자와 막대 위치를 사용합니다.
        Rect(dash, 190, 103, 125, 57, Background);
        Seven(dash, Mathf.RoundToInt(fuel).ToString("00"), 202, 108, 43, 50, 8, 8, Amber);
        DrawDashBar(dash, fuel, 380, 116);

        Rect(dash, 190, 236, 125, 62, Background);
        Seven(dash, Mathf.RoundToInt(boost).ToString("00"), 202, 241, 43, 52, 8, 8, Amber);
        DrawDashBar(dash, boost, 380, 250);
        Upload(dash);
    }

    static void DrawDashBar(Surface surface, float value, int x, int y)
    {
        Rect(surface, x, y, 245, 24, Background);
        int active = Mathf.RoundToInt(12f * Mathf.Clamp01(value / 100f));
        for (int i = 0; i < 12; i++)
            Rect(surface, x + i * 20, y, 13, 22, i < active ? Amber : DimAmber);
    }

    void DrawTunnel(float timer, float coolant)
    {
        if (tunnel == null) return;
        RestoreOriginal(tunnel);

        // LAP 시간, GEAR, WATER 영역에 각각 타이머/기어/냉각수를 반영합니다.
        Rect(tunnel, 45, 38, 290, 104, Background);
        Seven(tunnel, timer.ToString("000.0"), 50, 47, 48, 82, 10, 7, Amber);

        Rect(tunnel, 540, 38, 90, 128, Background);
        Seven(tunnel, Mathf.Clamp(speedController != null ? speedController.CurrentGear : 1, 1, 6).ToString(),
            550, 45, 70, 115, 14, 0, Amber);

        Rect(tunnel, 118, 227, 112, 66, Background);
        Seven(tunnel, Mathf.RoundToInt(coolant).ToString("000"), 124, 232, 29, 55, 7, 6, Amber);
        Upload(tunnel);
    }

    static void RestoreOriginal(Surface s) => Array.Copy(s.originalPixels, s.pixels, s.pixels.Length);
    static void Upload(Surface s) { s.texture.SetPixels32(s.pixels); s.texture.Apply(false, false); }

    static void Text(Surface s, string text, int x, int y, int scale, Color32 color)
    {
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (!Font.TryGetValue(c, out int[] rows)) rows = Font[' '];
            for (int row = 0; row < 7; row++)
            for (int col = 0; col < 5; col++)
                if ((rows[row] & (1 << (4 - col))) != 0)
                    Rect(s, x + col * scale, y + row * scale, scale, scale, color);
            x += 6 * scale;
        }
    }

    static readonly int[] DigitSegments = { 0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f };
    static void Seven(Surface s, string value, int x, int y, int w, int h, int t, int spacing, Color32 color)
    {
        foreach (char c in value)
        {
            if (c >= '0' && c <= '9')
            {
                int mask = DigitSegments[c - '0'];
                Segment(s, x, y, w, h, t, mask, color);
                x += w + spacing;
            }
            else if (c == ':') { Rect(s, x, y + h / 3, t, t, color); Rect(s, x, y + h * 2 / 3, t, t, color); x += t + spacing; }
            else if (c == '.') { Rect(s, x, y + h - t, t, t, color); x += t + spacing; }
            else x += w / 2 + spacing;
        }
    }

    static void Segment(Surface s, int x, int y, int w, int h, int t, int mask, Color32 c)
    {
        int half = h / 2;
        if ((mask & 1) != 0) Rect(s,x+t,y,w-2*t,t,c);
        if ((mask & 2) != 0) Rect(s,x+w-t,y+t,t,half-t,c);
        if ((mask & 4) != 0) Rect(s,x+w-t,y+half,t,half-t,c);
        if ((mask & 8) != 0) Rect(s,x+t,y+h-t,w-2*t,t,c);
        if ((mask & 16) != 0) Rect(s,x,y+half,t,half-t,c);
        if ((mask & 32) != 0) Rect(s,x,y+t,t,half-t,c);
        if ((mask & 64) != 0) Rect(s,x+t,y+half-t/2,w-2*t,t,c);
    }

    static void Outline(Surface s, int x, int y, int w, int h, Color32 c)
    { Rect(s,x,y,w,1,c); Rect(s,x,y+h-1,w,1,c); Rect(s,x,y,1,h,c); Rect(s,x+w-1,y,1,h,c); }

    static void Rect(Surface s, int x, int y, int w, int h, Color32 c)
    {
        int x0 = Mathf.Clamp(x, 0, s.width), x1 = Mathf.Clamp(x + w, 0, s.width);
        int y0 = Mathf.Clamp(y, 0, s.height), y1 = Mathf.Clamp(y + h, 0, s.height);
        for (int py = y0; py < y1; py++)
        {
            int row = (s.height - 1 - py) * s.width;
            for (int px = x0; px < x1; px++) s.pixels[row + px] = c;
        }
    }
}
