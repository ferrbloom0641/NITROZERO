using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Prevents Unity 6's GameObjectInspector from rebuilding against a destroyed target
/// during a script/domain reload. This project frequently creates runtime/editor-only
/// helper objects, so a locked Inspector can otherwise retain an invalid native object.
/// </summary>
[InitializeOnLoad]
internal static class InspectorReloadGuard
{
    const string RepairMenu = "Tools/NITRO ZERO/Repair Inspector Targets";

    static InspectorReloadGuard()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= PrepareForReload;
        AssemblyReloadEvents.beforeAssemblyReload += PrepareForReload;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += RepairAfterReload;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // Scene objects are destroyed immediately after these two notifications.
        // Clear Inspector editors while their native targets are still valid.
        if(state != PlayModeStateChange.ExitingEditMode && state != PlayModeStateChange.ExitingPlayMode) return;
        Selection.objects = Array.Empty<UnityEngine.Object>();
        ResetInspectors();
    }

    static void PrepareForReload()
    {
        Selection.objects = Array.Empty<UnityEngine.Object>();
        ResetInspectors();
    }

    static void RepairAfterReload()
    {
        ResetInspectors();
        // Remove only invalid Unity objects. Keep a valid user's selection intact.
        var current = Selection.objects;
        if (current == null || Array.Exists(current, item => !item))
            Selection.objects = Array.Empty<UnityEngine.Object>();
    }

    [MenuItem(RepairMenu, priority = 2000)]
    static void RepairNow()
    {
        Selection.objects = Array.Empty<UnityEngine.Object>();
        ResetInspectors();
        InternalEditorUtility.RepaintAllViews();
    }

    static void ResetInspectors()
    {
        try
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.InspectorWindow");
            var property = type?.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var clearLocked = type?.GetMethod("ClearSerializedLockedObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            var setLocked = type?.GetMethod("SetObjectsLocked", BindingFlags.Instance | BindingFlags.NonPublic);
            var rebuild = type?.GetMethod("ClearEditorsAndRebuild", BindingFlags.Instance | BindingFlags.NonPublic);
            if (type == null) return;
            foreach (var window in Resources.FindObjectsOfTypeAll(type))
            {
                if(property != null && property.CanWrite) property.SetValue(window, false);
                setLocked?.Invoke(window,new object[]{new List<UnityEngine.Object>()});
                clearLocked?.Invoke(window,null);
                rebuild?.Invoke(window,null);
                if (window is EditorWindow editorWindow) editorWindow.Repaint();
            }
        }
        catch
        {
            // The guard must never create a Console error while repairing an Inspector.
        }
    }
}
