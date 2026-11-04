using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Isolated Cinemachine shots, evaluated after interpolated vehicle presentation.</summary>
[DefaultExecutionOrder(1100)]
public sealed class RaceCinematicDirector : MonoBehaviour
{
    public AutonomousRaceController race;
    public Camera outputCamera;
    public CinemachineBrain brain;
    public RaceCinematicShot[] shots;
    [Range(0, 5)] public int selectedShot;
    [Range(0, 5)] public int subjectIndex = 3;
    public bool showControls = true;
    public bool smoothTransitions;
    [System.NonSerialized] public bool recordingLocked;
    [Range(0, 2)] public float shakeIntensity = 1;
    [Range(.1f, 2)] public float transitionSeconds = .65f;
    public float ShotTime { get; private set; }
    public int PartnerIndex { get; private set; }
    readonly Dictionary<Camera, bool> savedCameras = new Dictionary<Camera, bool>();
    readonly Dictionary<AudioListener, bool> savedListeners = new Dictionary<AudioListener, bool>();
    readonly Dictionary<CinemachineBrain, bool> savedBrains = new Dictionary<CinemachineBrain, bool>();
    int lastShot = -1, lastSubject = -1;
    float lastRaceTime = -1;
    bool acquired;
    Transform redEngine;
    static readonly string[] ShotNames = { "1 Front pack", "2 Rear duel", "3 Side dolly", "4 Corner pan", "5 Red engine deployment", "6 Overhead checkpoint" };

    void Start() { AcquireOutput(); }

    void AcquireOutput()
    {
        if (acquired || race == null || outputCamera == null || brain == null) return;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            // Disabling a Camera does not disable its Brain. An all-channel Brain can
            // cache our vcams before this frame's shot transforms are positioned.
            foreach (var other in root.GetComponentsInChildren<CinemachineBrain>(true))
            {
                if (other == brain) continue;
                savedBrains[other] = other.enabled;
                other.enabled = false;
            }
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            { savedCameras[camera] = camera.enabled; camera.enabled = camera == outputCamera; }
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
            { savedListeners[listener] = listener.enabled; listener.enabled = listener.gameObject == outputCamera.gameObject; }
        }
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        acquired = true;
    }

    void OnDisable()
    {
        foreach (var item in savedCameras) if (item.Key != null) item.Key.enabled = item.Value;
        foreach (var item in savedListeners) if (item.Key != null) item.Key.enabled = item.Value;
        foreach (var item in savedBrains) if (item.Key != null) item.Key.enabled = item.Value;
        savedCameras.Clear(); savedListeners.Clear(); savedBrains.Clear(); acquired = false;
    }

    void LateUpdate()
    {
        if (race == null || !race.Ready) return;
        AcquireOutput();
#if ENABLE_INPUT_SYSTEM
        var keys = Keyboard.current;
        if (keys != null && !recordingLocked)
        {
            if (keys.f1Key.wasPressedThisFrame) SelectShot(0);
            if (keys.f2Key.wasPressedThisFrame) SelectShot(1);
            if (keys.f3Key.wasPressedThisFrame) SelectShot(2);
            if (keys.f4Key.wasPressedThisFrame) SelectShot(3);
            if (keys.f5Key.wasPressedThisFrame) SelectShot(4);
            if (keys.f6Key.wasPressedThisFrame) SelectShot(5);
            if (keys.hKey.wasPressedThisFrame)
            { showControls = !showControls; race.showControls = showControls; }
        }
#endif
        Evaluate(race.paused ? 0 : Time.deltaTime * race.PlaybackSpeed);
        brain.ManualUpdate();
    }

    public void SelectShot(int index)
    {
        selectedShot = Mathf.Clamp(index, 0, shots != null ? Mathf.Max(0,shots.Length-1) : 0);
        lastShot = -1;
    }

    public void ReplayMove() { ShotTime = 0; }

    // Explicit shooting cue; normal camera selection never changes the race.
    public void CueCorner()
    {
        if (race == null || !race.Ready || shots == null || shots.Length < 4) return;
        race.RestartRace();
        float destination = Mathf.Clamp(race.storyMode ? race.storyCornerCue - 250 : shots[3].panRoadDistance - 230, 0, race.RoadLength);
        for (int i = 0; i < 10000 && race.racers[subjectIndex].distance < destination; i++)
            race.SimulateStep(.02f);
        race.PresentCurrentState();
        SelectShot(3);
        Evaluate(0);
    }

    public void CueEngine()
    {
        if (race == null || !race.Ready || shots == null || shots.Length < 5) return;
        subjectIndex = 3;
        race.RestartRace();
        // Lead-in before the story's deployment at (cue - 30)m of White's road progress.
        float destination = Mathf.Clamp(race.storyCornerCue - 110, 0, race.RoadLength);
        for (int i=0;i<10000 && race.racers[2].distance<destination;i++) race.SimulateStep(.02f);
        race.PresentCurrentState(); SelectShot(4); Evaluate(0);
    }

    public void CueCheckpoint()
    {
        if (race == null || !race.Ready || shots == null || shots.Length < 6) return;
        race.RestartRace();
        float destination = Mathf.Clamp(shots[5].panRoadDistance-160,0,race.RoadLength);
        for(int i=0;i<10000;i++)
        {
            float lead=0; foreach(var car in race.racers) lead=Mathf.Max(lead,car.distance);
            if(lead>=destination)break;
            race.SimulateStep(.02f);
        }
        race.PresentCurrentState(); SelectShot(5); Evaluate(0);
    }

    public void Evaluate(float dt)
    {
        if (race == null || !race.Ready || shots == null || shots.Length == 0) return;
        selectedShot = Mathf.Clamp(selectedShot, 0, shots.Length-1);
        subjectIndex = Mathf.Clamp(subjectIndex, 0, race.racers.Length - 1);
        bool reset = lastShot != selectedShot || lastSubject != subjectIndex || race.Elapsed < lastRaceTime;
        if (reset)
        {
            ShotTime = 0;
            PartnerIndex = NearestPartner();
            lastShot = selectedShot; lastSubject = subjectIndex;
        }
        else if (!race.paused) ShotTime += Mathf.Max(0, dt);
        lastRaceTime = race.Elapsed;
        var subject = race.racers[subjectIndex];
        var partner = race.racers[PartnerIndex];
        float subjectDistance = race.PresentedDistance(subject);
        Vector3 forward = (race.RoadPosition(subjectDistance + 24, 0) - race.RoadPosition(subjectDistance - 24, 0)).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 pair = (subject.car.position + partner.car.position) * .5f;
        for (int i = 0; i < shots.Length; i++)
        {
            var shot = shots[i];
            if (shot == null || shot.cameraRig == null) continue;
            Vector3 focus = subject.car.position + Vector3.up * shot.aimHeight;
            Vector3 position;
            if (shot.kind == RaceCinematicShot.ShotKind.PackFront)
            {
                float lo = float.MaxValue, hi = 0;
                foreach (var r in race.racers) { float d = race.PresentedDistance(r); lo = Mathf.Min(lo, d); hi = Mathf.Max(hi, d); }
                float middle = (lo + hi) * .5f;
                Vector3 axis = (race.RoadPosition(middle + 24, 0) - race.RoadPosition(middle - 24, 0)).normalized;
                focus = race.RoadPosition(middle, 0) + Vector3.up * shot.aimHeight;
                position = race.RoadPosition(hi, 0) + axis * shot.offset.z
                    + Vector3.Cross(Vector3.up, axis) * shot.offset.x + Vector3.up * shot.offset.y;
            }
            else if (shot.kind == RaceCinematicShot.ShotKind.PairRear)
            {
                focus = pair + forward * 6 + Vector3.up * shot.aimHeight;
                float spread = Mathf.Abs(subjectDistance - race.PresentedDistance(partner)) * .5f;
                position = pair + side * shot.offset.x + Vector3.up * shot.offset.y
                    + forward * (shot.offset.z - spread);
            }
            else if (shot.kind == RaceCinematicShot.ShotKind.SideDolly)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(ShotTime / Mathf.Max(1, shot.dollyDuration)));
                float lateral = Mathf.Sign(shot.offset.x) * Mathf.Max(Mathf.Abs(shot.offset.x), subject.halfWidth + 10);
                position = subject.car.position + side * lateral + Vector3.up * shot.offset.y
                    + forward * (shot.offset.z + Mathf.Lerp(shot.dollyStart, shot.dollyEnd, t));
            }
            else if (shot.kind == RaceCinematicShot.ShotKind.RedBoosterRear)
            {
                var red = race.racers[3];
                if (redEngine == null || !redEngine.IsChildOf(red.car))
                {
                    var deployment = red.car.GetComponentInChildren<BoosterDeploymentController>(true);
                    redEngine = deployment != null ? deployment.transform : red.car;
                }
                focus = redEngine.position + Vector3.up * shot.aimHeight;
                position = red.car.position + red.car.right * shot.offset.x + Vector3.up * shot.offset.y
                    + red.car.forward * (shot.offset.z - red.halfLength);
            }
            else
            {
                float station = Mathf.Clamp(shot.panRoadDistance, 24, race.RoadLength - 24);
                Vector3 axis = (race.RoadPosition(station + 24, 0) - race.RoadPosition(station - 24, 0)).normalized;
                position = race.RoadPosition(station, 0) + Vector3.Cross(Vector3.up, axis) * shot.offset.x
                    + Vector3.up * shot.offset.y + axis * shot.offset.z;
                focus = shot.kind == RaceCinematicShot.ShotKind.OverheadCheckpoint
                    ? race.RoadPosition(station,0) + axis*shot.checkpointLookAhead + Vector3.up*shot.aimHeight
                    : pair + Vector3.up * shot.aimHeight;
            }
            // Respect the actual landscape when a side or trackside rig lies beyond the asphalt.
            foreach (var terrain in Terrain.activeTerrains)
            {
                Vector3 local = position - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z)
                    position.y = Mathf.Max(position.y, terrain.SampleHeight(position) + terrain.transform.position.y + 1.5f);
            }
            if (shot.kind == RaceCinematicShot.ShotKind.PackFront)
            {
                // Fit all six measured car envelopes, including the curved section of the road.
                Quaternion view = Quaternion.LookRotation(focus - position, Vector3.up);
                float tanY = Mathf.Tan(shot.cameraRig.Lens.FieldOfView * .5f * Mathf.Deg2Rad);
                float tanX = tanY * (outputCamera != null ? Mathf.Max(.5f, outputCamera.aspect) : 16f / 9f);
                float retreat = 0;
                foreach (var r in race.racers)
                {
                    Vector3 local = Quaternion.Inverse(view) * (r.car.position + Vector3.up * 2 - position);
                    float radius = Mathf.Sqrt(r.halfWidth * r.halfWidth + r.halfLength * r.halfLength + 16);
                    retreat = Mathf.Max(retreat, (Mathf.Abs(local.x) + radius) * 1.15f / tanX - local.z,
                        (Mathf.Abs(local.y) + radius) * 1.15f / tanY - local.z);
                }
                position -= view * Vector3.forward * retreat;
            }
            Vector3 direction = focus - position;
            Quaternion desired = direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction, Vector3.up) : shot.transform.rotation;
            float weight = reset || shot.kind == RaceCinematicShot.ShotKind.OverheadCheckpoint ? 1 : 1 - Mathf.Exp(-Mathf.Max(0, dt) / Mathf.Max(.05f, shot.rotationResponse));
            shot.transform.SetPositionAndRotation(position, Quaternion.Slerp(shot.transform.rotation, desired, weight));
            shot.cameraRig.Priority = i == selectedShot ? 100 : 0;
            if (reset) shot.cameraRig.PreviousStateIsValid = false;
        }
        if (reset && shots[selectedShot] != null) shots[selectedShot].cameraRig.Prioritize();
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(
            smoothTransitions ? CinemachineBlendDefinition.Styles.EaseInOut : CinemachineBlendDefinition.Styles.Cut,
            smoothTransitions ? transitionSeconds : 0);
    }

    int NearestPartner()
    {
        int found = subjectIndex == 0 ? 1 : 0;
        float closest = float.MaxValue;
        for (int i = 0; i < race.racers.Length; i++)
        {
            if (i == subjectIndex) continue;
            float gap = Mathf.Abs(race.racers[i].distance - race.racers[subjectIndex].distance);
            if (gap < closest) { closest = gap; found = i; }
        }
        return found; // Lock the pair for the shot: no sudden subject swaps mid-take.
    }

    void OnGUI()
    {
        if (!showControls || race == null || !race.Ready) return;
        GUILayout.BeginArea(new Rect(Mathf.Max(12, Screen.width - 340), 16, 324, 410), GUI.skin.box);
        GUILayout.Label("RACE CINEMACHINE / F1-F6 / H: hide UI");
        for (int i = 0; i < shots.Length; i++)
            if (GUILayout.Toggle(selectedShot == i, i<ShotNames.Length?ShotNames[i]:shots[i].name, GUI.skin.button) && selectedShot != i) SelectShot(i);
        GUILayout.Label(selectedShot==4 ? "Subject: Red engine (fixed)" : selectedShot==5 ? "Subject: fixed road checkpoint" : "Subject: " + race.racers[subjectIndex].car.name);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Next car")) subjectIndex = (subjectIndex + 1) % race.racers.Length;
        if (GUILayout.Button("Replay dolly")) ReplayMove();
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Cue corner (restart race + seek)")) CueCorner();
        if (GUILayout.Button("Cue engine deployment (restart + seek)")) CueEngine();
        if (GUILayout.Button("Cue checkpoint pass (restart + seek)")) CueCheckpoint();
        GUILayout.Label("Camera shake: " + shakeIntensity.ToString("0.00"));
        shakeIntensity=GUILayout.HorizontalSlider(shakeIntensity,0,2);
        if (GUILayout.Button("Hide all controls (H restores)")) { showControls = false; race.showControls = false; }
        GUILayout.EndArea();
    }
}
