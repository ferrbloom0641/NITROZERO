using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CloseMissileProbeOnce
{
    static CloseMissileProbeOnce() { EditorApplication.update += Close; }
    static void Close()
    {
        const string request = "Temp/CloseMissileProbe.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try
        {
            var scene = SceneManager.GetSceneByPath("Assets/01RAEHYEON/RHScenes/RH_avoidMissile.unity");
            var dome = SceneManager.GetSceneByPath("Assets/01RAEHYEON/RHScenes/RH_domeInTheMoon.unity");
            if (scene.IsValid() && scene.isLoaded && dome.IsValid() && dome.isLoaded)
            {
                Directory.CreateDirectory("../Backups/CockpitMissilePass");
                string backup = "../Backups/CockpitMissilePass/RH_avoidMissile_live_probe_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
                if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not snapshot additive scene before closing.");
                SceneManager.SetActiveScene(dome);
                if (!EditorSceneManager.CloseScene(scene, true)) throw new IOException("Could not close additive probe scene.");
                File.WriteAllText("missile-probe-closed.txt", "Closed additive RH_avoidMissile. RH_domeInTheMoon was not saved or changed. Snapshot: " + Path.GetFullPath(backup));
            }
            else File.WriteAllText("missile-probe-closed.txt", "No additive RH_avoidMissile alongside RH_domeInTheMoon to close.");
        }
        catch (Exception e) { File.WriteAllText("missile-probe-close-error.txt", e.ToString()); }
    }
}
