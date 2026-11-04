using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AutonomousRaceValidation
{
    const string ScenePath = "Assets/01RAEHYEON/RHScenes/RH_racing_Autonomous.unity";
    const string Request = "Documentation/AutonomousRace/validate.request";

    [InitializeOnLoadMethod]
    static void Schedule()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += ValidateRequested;
    }

    static void ValidateRequested()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += ValidateRequested; return; }
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        Validate();
    }

    [MenuItem("Tools/Trailer/Validate Autonomous Race")]
    public static void Validate()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        var report = new StringBuilder();
        try
        {
            var race = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AutonomousRaceController>(true)).Single();
            race.createPreviewCamera = true;
            race.Initialize();
            Check(race.racers.Select(r => r.drivingStyle).Distinct().Count() == 6, "six driving styles are not assigned");
            report.AppendLine("Road length: " + race.RoadLength + "m; usable half width: " + race.LaneExtent + "m; lanes=" + race.LaneCount);
            report.AppendLine("Detected corners: " + race.CornerCount);
            foreach (var r in race.racers)
                report.AppendLine(r.car.name + " style=" + r.drivingStyle + " half width=" + r.halfWidth + " half length=" + r.halfLength + " ride height=" + r.rideHeight);
            int rankChanges = 0;
            int[] oldRanks = Ranking(race);
            for (int frame = 0; frame < 6000; frame++)
            {
                race.SimulateStep(0.02f);
                foreach (var r in race.racers)
                {
                    Check(!float.IsNaN(r.distance) && r.distance >= r.previousDistance && r.distance <= race.RoadLength, "invalid progress");
                    Check(r.speed >= 0 && r.speed * 3.6f <= 300.1f, "speed outside bounds");
                    Check(Mathf.Abs(r.lateral) + r.halfWidth <= race.LaneExtent + 0.01f, "outside lanes");
                    foreach (var other in race.racers)
                    {
                        if (r == other) continue;
                        if (Mathf.Abs(r.lateral - other.lateral) >= r.halfWidth + other.halfWidth + 0.5f) continue;
                        Check(Mathf.Abs(r.distance - other.distance) >= r.halfLength + other.halfLength + 0.5f, "vehicle envelopes overlap");
                    }
                }
                int[] ranks = Ranking(race);
                if (!ranks.SequenceEqual(oldRanks)) rankChanges++;
                oldRanks = ranks;
                if (frame == 749 || frame == 1499)
                {
                    race.PresentCurrentState();
                    report.AppendLine("At " + race.Elapsed + "s: " + string.Join("; ", race.racers.Select(r => r.car.name + " d=" + r.distance + " speed=" + r.speed * 3.6f)));
                    Capture(race, frame == 749 ? "preview-15s.png" : "preview-30s.png");
                }
            }
            report.AppendLine("Lane decisions=" + race.OvertakeDecisions + "; rank changes=" + rankChanges);
            report.AppendLine("Completed pass and return sequences=" + race.CompletedOvertakes);
            Check(race.CompletedOvertakes > 0, "no completed staged overtake");
            Check(race.OvertakeDecisions > 0 && rankChanges > 0, "no overtaking occurred");
            Check(race.racers.All(r => r.speed < 0.05f), "vehicles did not stop at the open road end");
            race.RestartRace();
            for (int i = 0; i < 1000; i++) race.SimulateStep(0.02f);
            var positions = race.racers.Select(r => new Vector2(r.distance, r.lateral)).ToArray();
            race.RestartRace();
            for (int i = 0; i < 1000; i++) race.SimulateStep(0.02f);
            for (int i = 0; i < positions.Length; i++)
                Check(Vector2.Distance(positions[i], new Vector2(race.racers[i].distance, race.racers[i].lateral)) < 0.001f, "restart is not repeatable");
            report.AppendLine("PASS: 120 seconds / 6000 steps; six vehicles; bounds, separation, overtaking, end stop, deterministic restart. Rank changes: " + rankChanges);
            Debug.Log(report.ToString());
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); }
        finally
        {
            Directory.CreateDirectory("Documentation/AutonomousRace");
            File.WriteAllText("Documentation/AutonomousRace/unity-validation.txt", report.ToString());
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static int[] Ranking(AutonomousRaceController race) => Enumerable.Range(0, race.racers.Length).OrderByDescending(i => race.racers[i].distance).ToArray();
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    static void Capture(AutonomousRaceController race, string file)
    {
        var camera = race.GetComponentInChildren<Camera>();
        camera.scene = race.gameObject.scene;
        var texture = new RenderTexture(1280, 720, 24);
        var old = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Directory.CreateDirectory("Documentation/AutonomousRace");
            File.WriteAllBytes("Documentation/AutonomousRace/" + file, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = old;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
