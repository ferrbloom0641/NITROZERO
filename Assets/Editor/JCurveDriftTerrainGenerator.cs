using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class JCurveDriftTerrainGenerator
{
    private const string ScenePath = "Assets/Scenes/racing.unity";
    private const string DataPath = "Assets/Terrain/Racing_JCurveMoonTerrain.asset";
    private const string RoadMeshPath = "Assets/Terrain/Racing_DriftRoad.asset";
    private const string RoadMaterialPath = "Assets/01RAEHYEON/Material/Asphalt010_2K-JPG/Asphalt.mat";
    private const string LayerPath = "Assets/02YUJEONG/Moon/moon_04_2k.blend/textures/NewLayer.terrainlayer";
    private const int Resolution = 2049;
    private const float Length = 5000f;
    private const float Width = 4000f;
    private const float Height = 520f;
    private const float OriginY = -80f;
    private const float RoadY = -20f;
    private const float RoadHalfWidth = 15f;
    private const float RoadBlendWidth = 360f;
    private const float AsphaltTileWorldSize = 4f;

    [MenuItem("NITRO ZERO/Moon Terrain/Create J Curve Drift Scene")]
    public static void Generate()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid())
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        else if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, DataPath);
        }
        data.heightmapResolution = Resolution;
        data.size = new Vector3(Length, Height, Width);
        data.alphamapResolution = 1024;
        data.baseMapResolution = 2048;
        data.SetHeights(0, 0, BuildHeights());

        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
        if (layer != null)
        {
            data.terrainLayers = new[] { layer };
            float[,,] alpha = new float[data.alphamapResolution, data.alphamapResolution, 1];
            for (int z = 0; z < data.alphamapResolution; z++)
                for (int x = 0; x < data.alphamapResolution; x++) alpha[z, x, 0] = 1f;
            data.SetAlphamaps(0, 0, alpha);
        }

        Terrain terrain = FindTerrain(scene);
        if (terrain == null)
        {
            GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
            SceneManager.MoveGameObjectToScene(terrainObject, scene);
            terrain = terrainObject.GetComponent<Terrain>();
        }
        terrain.gameObject.name = "Racing J Curve Moon Terrain 5000x4000";
        terrain.terrainData = data;
        terrain.transform.position = new Vector3(-Length * 0.5f, OriginY, -Width * 0.5f);
        terrain.heightmapPixelError = 4f;
        terrain.basemapDistance = 5000f;
        terrain.drawInstanced = true;
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null) collider = terrain.gameObject.AddComponent<TerrainCollider>();
        collider.terrainData = data;

        CreateRoadGuide(scene);
        CreateRoadPreview(scene);
        ConfigureCamera(scene, terrain);
        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[NITRO ZERO] racing 5000x4000 terrain and drift reveal road generated.");
    }

    [MenuItem("NITRO ZERO/Moon Terrain/Rebuild Racing Natural Heights Only")]
    public static void RebuildNaturalHeightsOnly()
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
        if (data == null) throw new MissingReferenceException(DataPath);
        data.SetHeights(0, 0, BuildHeights());
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("[NITRO ZERO] Racing terrain rebuilt from the continuous natural-height source.");
    }

    private static float[,] BuildHeights()
    {
        float[,] heights = new float[Resolution, Resolution];
        float roadLocal = RoadY - OriginY;
        for (int z = 0; z < Resolution; z++)
        {
            float worldZ = z / (float)(Resolution - 1) * Width - Width * 0.5f;
            for (int x = 0; x < Resolution; x++)
            {
                float worldX = x / (float)(Resolution - 1) * Length - Length * 0.5f;
                Vector2 warp = Warp(worldX, worldZ);
                float h = roadLocal + (Fbm(warp.x, warp.y, 0.00075f, 4, 17f) - 0.5f) * 30f;
                h += (Fbm(warp.x, warp.y, 0.0031f, 3, 61f) - 0.5f) * 12f;
                h += EdgeHighland(worldX, worldZ);
                h += RevealMountainRange(worldX, worldZ);
                h += MarkedMountainRanges(worldX, worldZ);
                h += BackgroundRockGroups(worldX, worldZ);
                h += HorizonBackdrop(worldX, worldZ);

                float roadDistance = DistanceToJRoad(worldX, worldZ);
                float transitionNoise = (Mathf.PerlinNoise(worldX * 0.0011f + 8f, worldZ * 0.0011f + 3f) - 0.5f) * 55f;
                float blend = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(RoadHalfWidth, RoadBlendWidth + transitionNoise, roadDistance));
                h = Mathf.Lerp(roadLocal, h, blend);
                if (roadDistance <= RoadHalfWidth) h = roadLocal;
                heights[z, x] = Mathf.Clamp01(h / Height);
            }
        }
        Smooth(heights, roadLocal / Height);
        ApplySurfaceDetail(heights);
        return heights;
    }

    private static float DistanceToJRoad(float x, float z)
    {
        // Extend the terrain transition beyond both mesh endpoints so it never closes as a circular shelf.
        float straight = DistanceToSegment(new Vector2(x, z), new Vector2(-2500f, 600f), new Vector2(-200f, 600f));
        Vector2 center = new Vector2(-200f, 0f);
        Vector2 local = new Vector2(x, z) - center;
        float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        Vector2 arcStart = new Vector2(-200f, 600f);
        float endAngle = -20f * Mathf.Deg2Rad;
        Vector2 arcEnd = center + new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle)) * 600f;
        float arc = angle >= -20f && angle <= 90f
            ? Mathf.Abs(local.magnitude - 600f)
            : Mathf.Min(Vector2.Distance(new Vector2(x, z), arcStart), Vector2.Distance(new Vector2(x, z), arcEnd));
        Vector2 exitDirection = new Vector2(Mathf.Sin(endAngle), -Mathf.Cos(endAngle));
        float exit = DistanceToSegment(new Vector2(x, z), arcEnd, arcEnd + exitDirection * 1900f);
        return Mathf.Min(straight, Mathf.Min(arc, exit));
    }

    private static float RevealMountainRange(float x, float z)
    {
        float h = 0f;
        h += Mound(x, z, -200f,  20f, 470f, 410f, 88f, 31f);
        h += Mound(x, z, -430f, 115f, 390f, 330f, 64f, 47f);
        h += Mound(x, z,   35f,  85f, 370f, 315f, 59f, 63f);
        h += Mound(x, z, -285f,-235f, 340f, 285f, 52f, 79f);
        h += Mound(x, z,  -70f, 270f, 310f, 250f, 44f, 97f);
        return h;
    }

    private static float BackgroundRockGroups(float x, float z)
    {
        float h = 0f;
        h += Mound(x, z, -1350f, -1380f, 390f, 210f, 66f, 113f);
        h += Mound(x, z, -1080f, -1450f, 260f, 160f, 45f, 131f);
        h += Mound(x, z,  1780f,  1320f, 410f, 235f, 72f, 149f);
        h += Mound(x, z,  2050f,  1450f, 240f, 155f, 43f, 167f);
        return h;
    }

    private static float HorizonBackdrop(float x, float z)
    {
        float h = 0f;
        // Overlapping broad summits hide the far +Z terrain boundary from the eye-level camera.
        h += Mound(x, z, -2050f, 1640f, 760f, 390f, 112f, 409f);
        h += Mound(x, z, -1420f, 1710f, 690f, 410f, 138f, 431f);
        h += Mound(x, z,  -760f, 1600f, 720f, 380f, 126f, 457f);
        h += Mound(x, z,   -80f, 1690f, 750f, 420f, 157f, 479f);
        h += Mound(x, z,   650f, 1580f, 710f, 370f, 132f, 503f);
        h += Mound(x, z,  1320f, 1700f, 740f, 410f, 166f, 521f);
        h += Mound(x, z,  2050f, 1620f, 680f, 390f, 121f, 547f);
        // Side wings close the frame without forming a straight wall.
        h += Mound(x, z, -2240f,  920f, 430f, 520f, 105f, 569f);
        h += Mound(x, z,  2260f,  860f, 410f, 500f,  98f, 593f);
        return h;
    }

    private static float MarkedMountainRanges(float x, float z)
    {
        float h = 0f;
        // Long inner ridge under the incoming straight. Unequal overlapping shoulders
        // keep the silhouette high without reading as a straight artificial wall.
        h += Mound(x, z, -1510f, 105f, 610f, 340f, 54f, 181f);
        h += Mound(x, z, -1170f, 135f, 580f, 350f, 67f, 199f);
        h += Mound(x, z,  -820f,  85f, 600f, 335f, 63f, 223f);
        h += Mound(x, z,  -470f, 125f, 560f, 350f, 74f, 239f);
        h += Mound(x, z,  -140f,  70f, 520f, 325f, 68f, 257f);
        h += Mound(x, z,   150f,  20f, 470f, 310f, 57f, 271f);

        // Diagonal ridge continues toward the camera side of the curve. It stays on
        // the inside of the exit road, so the car is hidden and then revealed at apex.
        h += Mound(x, z, -150f, -180f, 470f, 350f, 69f, 277f);
        h += Mound(x, z, -240f, -410f, 490f, 365f, 78f, 293f);
        h += Mound(x, z, -340f, -650f, 510f, 380f, 72f, 317f);
        h += Mound(x, z, -460f, -900f, 530f, 400f, 61f, 337f);
        return h;
    }

    private static float Mound(float x, float z, float cx, float cz, float rx, float rz, float peak, float seed)
    {
        float angle = seed * 0.057f;
        float dx = x - cx, dz = z - cz;
        float px = (dx * Mathf.Cos(angle) - dz * Mathf.Sin(angle)) / rx;
        float pz = (dx * Mathf.Sin(angle) + dz * Mathf.Cos(angle)) / rz;
        float d = Mathf.Sqrt(px * px + pz * pz);
        if (d >= 1.3f) return 0f;
        float body = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d / 1.3f));
        float rough = Mathf.Lerp(0.76f, 1.15f, Fbm(x, z, 0.006f, 3, seed));
        return peak * body * rough;
    }

    private static float EdgeHighland(float x, float z)
    {
        float ex = Mathf.Abs(x) / (Length * 0.5f);
        float ez = Mathf.Abs(z) / (Width * 0.5f);
        float edge = Mathf.Pow(Mathf.Pow(ex, 4f) + Mathf.Pow(ez, 4f), 0.25f);
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, edge));
        return rise * Mathf.Lerp(48f, 105f, Fbm(x, z, 0.00065f, 3, 211f));
    }

    private static float SparseCrater(float x, float z, float cx, float cz, float radius, float depth, float rim, float seed)
    {
        float dx = x - cx, dz = z - cz;
        float d = Mathf.Sqrt(dx * dx + dz * dz) / radius;
        if (d > 1.42f) return 0f;
        float angle = Mathf.Atan2(dz, dx);
        float angularNoise = Mathf.PerlinNoise(Mathf.Cos(angle) * 1.8f + seed, Mathf.Sin(angle) * 1.8f + seed);
        float shape = 1f + (angularNoise - 0.5f) * 0.17f + Mathf.Sin(angle * 3f + seed) * 0.035f;
        float rimDistance = d / shape;
        float rimWidth = Mathf.Lerp(0.055f, 0.17f, Fbm(x, z, 0.010f, 2, seed + 31f));
        float rimNoise = Mathf.Lerp(0.34f, 1.68f, Fbm(x, z, 0.018f, 3, seed + 17f));

        bool intact = seed < 2f;
        float gap = 1f;
        if (!intact)
        {
            float firstCenter = seed * 1.73f;
            float firstDelta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, firstCenter * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float firstGap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.72f, firstDelta));
            float secondCenter = firstCenter + 2.35f;
            float secondDelta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, secondCenter * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float secondGap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.14f, 0.48f, secondDelta));
            gap = firstGap * secondGap;
        }

        float floorNoise = Mathf.Lerp(0.86f, 1.12f, Fbm(x, z, 0.021f, 2, seed + 67f));
        float bowl = -depth * floorNoise * Mathf.Pow(Mathf.Clamp01(1f - d), 2.05f);
        float rimShape = rim * rimNoise * gap * Mathf.Exp(-Mathf.Pow((rimDistance - 1f) / rimWidth, 2f));
        float apron = rim * 0.12f * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(rimDistance - 1.14f) / 0.27f), 2f) * angularNoise;
        return bowl + rimShape + apron;
    }

    private static void ApplySurfaceDetail(float[,] heights)
    {
        for (int z = 0; z < Resolution; z++)
        {
            float worldZ = z / (float)(Resolution - 1) * Width - Width * 0.5f;
            for (int x = 0; x < Resolution; x++)
            {
                float worldX = x / (float)(Resolution - 1) * Length - Length * 0.5f;
                float mask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadHalfWidth, RoadHalfWidth + 190f, DistanceToJRoad(worldX, worldZ)));
                float detail = (Fbm(worldX, worldZ, 0.009f, 3, 307f) - 0.5f) * 9f;
                detail += (Fbm(worldX, worldZ, 0.035f, 2, 353f) - 0.5f) * 3.5f;
                heights[z, x] = Mathf.Clamp01(heights[z, x] + detail * mask / Height);
            }
        }
    }

    private static void Smooth(float[,] heights, float roadHeight)
    {
        float[,] copy = new float[Resolution, Resolution];
        for (int pass = 0; pass < 5; pass++)
        {
            Array.Copy(heights, copy, heights.Length);
            for (int z = 1; z < Resolution - 1; z++)
                for (int x = 1; x < Resolution - 1; x++)
                {
                    float worldX = x / (float)(Resolution - 1) * Length - Length * 0.5f;
                    float worldZ = z / (float)(Resolution - 1) * Width - Width * 0.5f;
                    if (DistanceToJRoad(worldX, worldZ) <= RoadHalfWidth) { heights[z, x] = roadHeight; continue; }
                    heights[z, x] = (copy[z, x] * 4f + copy[z - 1, x] + copy[z + 1, x] + copy[z, x - 1] + copy[z, x + 1]) / 8f;
                }
        }
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static Vector2 Warp(float x, float z)
    {
        float wx = (Mathf.PerlinNoise(x * 0.0006f + 7f, z * 0.0006f + 19f) - 0.5f) * 210f;
        float wz = (Mathf.PerlinNoise(x * 0.00055f + 37f, z * 0.00055f + 53f) - 0.5f) * 210f;
        return new Vector2(x + wx, z + wz);
    }

    private static float Fbm(float x, float z, float scale, int octaves, float seed)
    {
        float sum = 0f, amplitude = 1f, total = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * scale + seed, z * scale + seed * 0.37f) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            scale *= 2f;
        }
        return sum / total;
    }

    private static Terrain FindTerrain(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Terrain terrain = root.GetComponentInChildren<Terrain>(true);
            if (terrain != null) return terrain;
        }
        return null;
    }

    private static void CreateRoadGuide(Scene scene)
    {
        GameObject old = GameObject.Find("J Road Centerline Guide");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        old = GameObject.Find("Racing Road Centerline Guide");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        GameObject guide = new GameObject("Racing Road Centerline Guide");
        SceneManager.MoveGameObjectToScene(guide, scene);
        List<Vector3> points = BuildRoadPoints();
        int index = 0;
        foreach (Vector3 point in points) AddPoint(guide, ref index, point);
        AddMarker(guide, "Road Start", points[0]);
        AddMarker(guide, "Drift Apex", new Vector3(400f, RoadY, 0f));
        AddMarker(guide, "Boost Exit", points[points.Count - 1]);
    }

    private static List<Vector3> BuildRoadPoints()
    {
        List<Vector3> points = new List<Vector3>();
        for (int i = 0; i <= 14; i++)
            points.Add(Vector3.Lerp(new Vector3(-2500f, RoadY, 600f), new Vector3(-200f, RoadY, 600f), i / 14f));
        for (int i = 1; i <= 20; i++)
        {
            float angle = Mathf.Lerp(90f, -20f, i / 20f) * Mathf.Deg2Rad;
            points.Add(new Vector3(-200f + Mathf.Cos(angle) * 600f, RoadY, Mathf.Sin(angle) * 600f));
        }
        float endAngle = -20f * Mathf.Deg2Rad;
        Vector3 arcEnd = new Vector3(-200f + Mathf.Cos(endAngle) * 600f, RoadY, Mathf.Sin(endAngle) * 600f);
        Vector3 exitDirection = new Vector3(Mathf.Sin(endAngle), 0f, -Mathf.Cos(endAngle));
        for (int i = 1; i <= 10; i++) points.Add(arcEnd + exitDirection * (1916f * i / 10f));
        return points;
    }

    private static void CreateRoadPreview(Scene scene)
    {
        List<Vector3> points = BuildRoadPoints();
        Vector3[] vertices = new Vector3[points.Count * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(points.Count - 1) * 6];
        float travelled = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0) travelled += Vector3.Distance(points[i - 1], points[i]);
            Vector3 tangent = i == 0 ? points[1] - points[0]
                : i == points.Count - 1 ? points[i] - points[i - 1]
                : points[i + 1] - points[i - 1];
            Vector3 lateral = Vector3.Cross(Vector3.up, tangent.normalized);
            Vector3 center = points[i] + Vector3.up * 0.35f;
            vertices[i * 2] = center - lateral * RoadHalfWidth;
            vertices[i * 2 + 1] = center + lateral * RoadHalfWidth;
            float across = RoadHalfWidth * 2f / AsphaltTileWorldSize;
            float along = travelled / AsphaltTileWorldSize;
            uvs[i * 2] = new Vector2(0f, along);
            uvs[i * 2 + 1] = new Vector2(across, along);
            if (i >= points.Count - 1) continue;
            int t = i * 6, v = i * 2;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RoadMeshPath);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, RoadMeshPath); }
        mesh.Clear();
        mesh.name = "Racing Drift Road Mesh";
        mesh.vertices = vertices; mesh.triangles = triangles; mesh.uv = uvs;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
        if (material != null)
        {
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureScale("_MainTex", Vector2.one);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1.65f);
            if (material.HasProperty("_Parallax")) material.SetFloat("_Parallax", 0.03f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.075f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.72f, 1f));
            EditorUtility.SetDirty(material);
        }

        GameObject road = GameObject.Find("Racing Asphalt Road Preview");
        if (road == null)
        {
            road = new GameObject("Racing Asphalt Road Preview");
            SceneManager.MoveGameObjectToScene(road, scene);
            road.AddComponent<MeshFilter>();
            road.AddComponent<MeshRenderer>();
        }
        road.GetComponent<MeshFilter>().sharedMesh = mesh;
        road.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void AddPoint(GameObject parent, ref int index, Vector3 position)
    {
        GameObject point = new GameObject($"Road Point {index++:00}");
        point.transform.SetParent(parent.transform);
        point.transform.position = position;
    }

    private static void AddMarker(GameObject parent, string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent.transform);
        marker.transform.position = position;
    }

    private static void ConfigureCamera(Scene scene, Terrain terrain)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        camera.gameObject.name = "Drift Reveal Camera Guide";
        Vector3 horizontalPosition = new Vector3(-760f, 0f, -1220f);
        float groundY = terrain.SampleHeight(horizontalPosition) + terrain.transform.position.y;
        camera.transform.position = new Vector3(horizontalPosition.x, groundY + 10f, horizontalPosition.z);
        camera.transform.LookAt(new Vector3(-70f, RoadY + 5f, 10f));
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 5500f;
    }
}
