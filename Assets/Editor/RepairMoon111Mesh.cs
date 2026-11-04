using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RepairMoon111Mesh
{
    const string Prefix = "Assets/01RAEHYEON/VFX/";
    static RepairMoon111Mesh()
    {
        EditorApplication.delayCall += AutoRepair;
        EditorSceneManager.sceneOpened += OnOpened;
    }
    static void OnOpened(Scene scene, OpenSceneMode mode) { AutoRepair(); }
    static void AutoRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetSceneByPath("Assets/01RAEHYEON/RHScenes/111.unity");
        if (!scene.IsValid() || !scene.isLoaded) return;
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Prefix + "MoonSurfaceCloseup_Native.asset");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Prefix + "MoonSurfaceCloseup.mat");
        if (!mesh || !material) return;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "Moon Surface - Cratered 3D") continue;
            var filter = root.GetComponent<MeshFilter>();
            var renderer = root.GetComponent<MeshRenderer>();
            if (!filter || !renderer) continue;
            bool wasDirty = scene.isDirty;
            if (filter.sharedMesh != mesh)
            {
                Undo.RecordObject(filter, "Repair Moon Mesh");
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Moon111MeshVerification.txt", "Mesh connected: " + mesh.name + "\nVertices: " + mesh.vertexCount + "\nBounds: " + mesh.bounds + "\nShader supported: " + material.shader.isSupported);
            SceneView.RepaintAll();
            foreach (var cameraRoot in scene.GetRootGameObjects())
            {
                var camera = cameraRoot.GetComponent<Camera>();
                if (!camera) continue;
                var previous = camera.targetTexture;
                var active = RenderTexture.active;
                var target = RenderTexture.GetTemporary(960, 540, 24);
                var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = target;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    image.Apply();
                    File.WriteAllBytes("Logs/Moon111Preview.png", image.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = previous;
                    RenderTexture.active = active;
                    RenderTexture.ReleaseTemporary(target);
                    Object.DestroyImmediate(image);
                }
            }
        }
    }
    [MenuItem("Tools/NITRO ZERO/Repair Moon 111 Mesh")]
    static void RepairNow() { AutoRepair(); }
}
