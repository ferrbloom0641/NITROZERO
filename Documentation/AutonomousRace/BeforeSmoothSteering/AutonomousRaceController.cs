using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Cinematic, road-constrained racing. Owns vehicle roots; no tyre physics required.</summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class AutonomousRaceController : MonoBehaviour
{
    [Serializable]
    public sealed class Racer
    {
        public Transform car;
        [Range(240, 300)] public float preferredSpeedKph = 270;
        [NonSerialized] public float distance, lateral, speed, targetLane, cooldown;
        [NonSerialized] public float previousDistance, previousLateral, halfWidth, halfLength, rideHeight;
        [NonSerialized] public float startDistance;
        [NonSerialized] public TrailerCruiseMotion cruise;
    }

    [Header("Scene references — road mesh must be a paired left/right strip")]
    public MeshFilter road;
    public Transform formationRoot;
    public Racer[] racers = Array.Empty<Racer>();
    [Header("Cinematic racing (km/h, metres)")]
    [Range(240, 300)] public float minimumSpeedKph = 240;
    [Range(240, 300)] public float maximumSpeedKph = 300;
    [Min(0)] public float edgeMargin = 2;
    [Min(1)] public float followingGap = 10;
    [Min(1)] public float acceleration = 5;
    [Min(1)] public float braking = 18;
    [Min(0.1f)] public float laneChangeSpeed = 2.4f;
    [Min(60)] public float preferredPackLength = 180;
    public bool createPreviewCamera = true;
    public bool showControls = true;
    [Range(0, 2)] public int cameraView;
    public bool paused;

    public float RoadLength { get; private set; }
    public float Elapsed { get; private set; }
    public int OvertakeDecisions { get; private set; }
    public bool Ready { get; private set; }
    public string Status { get; private set; } = "Not initialized";
    public float LaneExtent { get; private set; }
    public int LaneCount { get; private set; }
    float usableHalfWidth;
    const float Step = 0.02f;
    Vector3[] left, right, centres;
    float[] cumulative;
    int[] order;
    float accumulator;
    Camera previewCamera;
    Collider roadCollider;

    void Awake()
    {
        try { Initialize(); }
        catch (Exception e) { Status = e.Message; Debug.LogException(e, this); enabled = false; }
    }

    public void Initialize()
    {
        if (Ready) return;
        if (road == null || road.sharedMesh == null || !road.sharedMesh.isReadable)
            throw new InvalidOperationException("Assign a readable road strip mesh.");
        if (racers.Length != 6) throw new InvalidOperationException("Exactly six vehicle roots are required.");
        var vertices = road.sharedMesh.vertices;
        if (vertices.Length < 4 || vertices.Length % 2 != 0)
            throw new InvalidOperationException("The road must contain paired left/right vertices.");
        int count = vertices.Length / 2;
        left = new Vector3[count]; right = new Vector3[count]; centres = new Vector3[count];
        cumulative = new float[count];
        float minWidth = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            left[i] = road.transform.TransformPoint(vertices[i * 2]);
            right[i] = road.transform.TransformPoint(vertices[i * 2 + 1]);
            centres[i] = (left[i] + right[i]) * 0.5f;
            minWidth = Mathf.Min(minWidth, Vector3.Distance(left[i], right[i]));
            if (i > 0) cumulative[i] = cumulative[i - 1] + Vector3.Distance(centres[i - 1], centres[i]);
        }
        RoadLength = cumulative[count - 1];
        if (RoadLength < 400) throw new InvalidOperationException("This six-car setup needs at least 400m of road.");
        roadCollider = road.GetComponent<Collider>();
        // Only this scene's playback is replaced. Prefab assets and the source scene are untouched.
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var director in root.GetComponentsInChildren<PlayableDirector>(true))
            { director.playOnAwake = false; director.Stop(); director.enabled = false; }
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == this) continue;
                string type = behaviour.GetType().Name;
                if (type == "SplineCarController" || type == "SplineFormationController" ||
                    type == "RaceChoreography" || type == "SplineAnimate" ||
                    type == "ArcadeCarController" || type == "RaehyeonCarWheelSpin" ||
                    type == "ContinuousLocalRotation" || type == "ContinuousImpulseEmitter") behaviour.enabled = false;
            }
        }
        float widest = 0;
        foreach (var r in racers)
        {
            if (r.car == null) throw new InvalidOperationException("A vehicle reference is missing.");
            r.car.gameObject.SetActive(true);
            for (var p = r.car.parent; p != null; p = p.parent) p.gameObject.SetActive(true);
            foreach (var body in r.car.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            r.cruise = r.car.GetComponent<TrailerCruiseMotion>();
            Measure(r);
            widest = Mathf.Max(widest, r.halfWidth);
            if (r.cruise != null)
            {
                r.cruise.shotDirector = null; r.cruise.useManualTime = false;
                r.cruise.automaticSteering = false; r.cruise.manualSteeringWeight = 1;
                // Disable keyed steering; this controller supplies the road steering angle.
                r.cruise.steering = new TrailerSteeringSettings();
            }
        }
        usableHalfWidth = minWidth * 0.5f - edgeMargin;
        if (usableHalfWidth <= widest + 0.5f)
            throw new InvalidOperationException("A vehicle is wider than the usable road.");
        LaneCount = usableHalfWidth - widest >= widest * 2 + 1 ? 3 : 2;
        LaneExtent = usableHalfWidth;
        order = new int[racers.Length];
        Ready = true;
        RestartRace();
        if (createPreviewCamera) CreateCamera();
        Status = "Autonomous racing";
    }

    static void Measure(Racer r)
    {
        // World-sized envelope in the car's orientation, including its actual renderer bounds.
        var inverse = Quaternion.Inverse(r.car.rotation);
        Bounds envelope = new Bounds(); bool found = false;
        foreach (var renderer in r.car.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            if (!renderer.gameObject.activeInHierarchy) continue;
            Bounds b = renderer.localBounds;
            for (int n = 0; n < 8; n++)
            {
                Vector3 local = b.center + Vector3.Scale(b.extents,
                    new Vector3((n & 1) == 0 ? -1 : 1, (n & 2) == 0 ? -1 : 1, (n & 4) == 0 ? -1 : 1));
                Vector3 p = inverse * (renderer.transform.TransformPoint(local) - r.car.position);
                if (!found) { envelope = new Bounds(p, Vector3.zero); found = true; }
                else envelope.Encapsulate(p);
            }
        }
        r.halfWidth = found ? Mathf.Max(Mathf.Abs(envelope.min.x), Mathf.Abs(envelope.max.x)) : 1.3f;
        r.halfLength = found ? Mathf.Max(Mathf.Abs(envelope.min.z), Mathf.Abs(envelope.max.z)) : 3;
        r.rideHeight = found ? -envelope.min.y + 0.04f : 0.5f;
        // Wheels are more reliable than exhaust/accessory geometry for contact height.
        if (r.cruise != null && r.cruise.Wheels != null && r.cruise.Wheels.Length == 4)
        {
            float bottom = 0; int valid = 0;
            for (int i = 0; i < 4; i++)
            {
                var wheel = r.cruise.Wheels[i]; if (wheel == null) continue;
                float radius = r.cruise.WheelRadii != null && r.cruise.WheelRadii.Length == 4
                    ? r.cruise.WheelRadii[i] : r.cruise.wheelRadius;
                bottom += (inverse * (wheel.position - r.car.position)).y - radius; valid++;
            }
            if (valid == 4) r.rideHeight = -bottom / valid + 0.04f;
        }
    }

    [ContextMenu("Restart race")]
    public void RestartRace()
    {
        if (!Ready) return;
        Elapsed = 0; accumulator = 0; OvertakeDecisions = 0; paused = false;
        float spacing = 0;
        foreach (var r in racers) spacing = Mathf.Max(spacing, r.halfLength * 2 + followingGap + 6);
        // Put the exceptionally wide support vehicle at the back, leaving passing room up front.
        int[] startOrder = { 0, 1, 3, 4, 5, 2 };
        for (int i = 0; i < racers.Length; i++)
        {
            Racer r = racers[i];
            int rank = Array.IndexOf(startOrder, i);
            r.distance = 60 + (racers.Length - 1 - rank) * spacing;
            r.startDistance = r.distance;
            r.lateral = LaneCount == 3 ? (rank % 3 - 1) * Extent(r) : (rank % 2 == 0 ? -1 : 1) * Extent(r);
            r.targetLane = r.lateral; r.speed = minimumSpeedKph / 3.6f;
            r.cooldown = i * 0.17f;
            r.previousDistance = r.distance; r.previousLateral = r.lateral;
            Place(r, r.distance, r.lateral, 0);
        }
    }

    void Update()
    {
        if (!Ready || paused) return;
        accumulator += Time.deltaTime;
        while (accumulator >= Step) { SimulateStep(Step); accumulator -= Step; }
    }

    public void SimulateStep(float dt)
    {
        if (!Ready || dt <= 0) return;
        Elapsed += dt;
        for (int i = 0; i < racers.Length; i++)
        {
            order[i] = i;
            racers[i].previousDistance = racers[i].distance;
            racers[i].previousLateral = racers[i].lateral;
        }
        Array.Sort(order, (a, b) => racers[b].distance.CompareTo(racers[a].distance));
        float leaderDistance = racers[order[0]].distance;
        float tailDistance = racers[order[order.Length - 1]].distance;
        float packMiddle = (leaderDistance + tailDistance) * 0.5f;
        foreach (int i in order)
        {
            Racer r = racers[i]; r.cooldown -= dt;
            float desiredKph = Mathf.Clamp(r.preferredSpeedKph + 16 * Mathf.Sin(Elapsed * 0.17f + i * 1.7f), minimumSpeedKph, maximumSpeedKph);
            if (leaderDistance - tailDistance > preferredPackLength)
                desiredKph = Mathf.Clamp(desiredKph - (r.distance - packMiddle) * 0.3f, minimumSpeedKph, maximumSpeedKph);
            Vector3 f0 = Direction(r.distance + 15), f1 = Direction(r.distance + 85);
            float bend = Vector3.Angle(f0, f1);
            desiredKph = Mathf.Min(desiredKph, Mathf.Lerp(maximumSpeedKph, minimumSpeedKph, Mathf.Clamp01(bend / 25)));
            Racer ahead = FindAhead(r, r.lateral, out float gap);
            if (ahead != null && gap < 160 && desiredKph / 3.6f > ahead.speed + 0.5f &&
                r.cooldown <= 0 && Mathf.Abs(r.lateral - r.targetLane) < 0.1f)
            {
                float best = Mathf.Min(gap, 160) + ahead.speed * 12; float lane = r.targetLane;
                for (int l = -1; l <= 1; l++)
                {
                    if (LaneCount == 2 && l == 0) continue;
                    float candidate = l * Extent(r);
                    if ((LaneCount == 3 && Mathf.Abs(candidate - r.targetLane) > Extent(r) + 0.1f) || Mathf.Abs(candidate - r.targetLane) < 0.1f) continue;
                    if (!CanChange(r, candidate)) continue;
                    Racer candidateLeader = FindAhead(r, candidate, out float space);
                    float score = Mathf.Min(space, 160) + (candidateLeader != null ? candidateLeader.speed : desiredKph / 3.6f) * 12;
                    if (score > best + 8) { best = score; lane = candidate; }
                }
                if (lane != r.targetLane) { r.targetLane = lane; r.cooldown = 4; OvertakeDecisions++; }
            }
            float lateral = Mathf.MoveTowards(r.lateral, r.targetLane, laneChangeSpeed * dt);
            float desired = desiredKph / 3.6f;
            float remaining = Mathf.Max(0, RoadLength - 15 - r.halfLength - r.distance);
            desired = Mathf.Min(desired, Mathf.Sqrt(2 * braking * remaining));
            float maxDistance = RoadLength - 15 - r.halfLength;
            foreach (Racer other in racers)
            {
                if (other == r || other.distance <= r.distance) continue;
                if (!SweptLanesOverlap(r, lateral, other)) continue;
                float available = other.distance - r.distance - r.halfLength - other.halfLength - followingGap;
                desired = Mathf.Min(desired, Mathf.Max(0, other.speed + (available - 15) * 0.5f));
                maxDistance = Mathf.Min(maxDistance, other.distance - r.halfLength - other.halfLength - followingGap);
            }
            r.speed = Mathf.MoveTowards(r.speed, desired, (desired < r.speed ? braking : acceleration) * dt);
            float next = Mathf.Max(r.distance, Mathf.Min(r.distance + r.speed * dt, maxDistance));
            r.speed = (next - r.distance) / dt;
            r.distance = next; r.lateral = lateral;
        }
    }

    Racer FindAhead(Racer r, float lane, out float gap)
    {
        Racer best = null; gap = float.MaxValue;
        foreach (var other in racers)
        {
            if (other == r || other.distance <= r.distance ||
                Mathf.Min(Mathf.Abs(lane - other.lateral), Mathf.Abs(lane - other.targetLane)) > r.halfWidth + other.halfWidth + 0.8f) continue;
            float d = other.distance - r.distance - r.halfLength - other.halfLength;
            if (d < gap) { gap = d; best = other; }
        }
        return best;
    }

    float Extent(Racer r) => usableHalfWidth - r.halfWidth;

    bool CanChange(Racer r, float candidate)
    {
        float lo = Mathf.Min(r.lateral, candidate), hi = Mathf.Max(r.lateral, candidate);
        float horizon = Mathf.Abs(candidate - r.lateral) / laneChangeSpeed + 0.5f;
        foreach (var other in racers)
        {
            if (other == r) continue;
            float width = r.halfWidth + other.halfWidth + 1;
            if (Mathf.Max(other.lateral, other.targetLane) < lo - width || Mathf.Min(other.lateral, other.targetLane) > hi + width) continue;
            float delta = other.distance - r.distance;
            float predicted = delta + (other.speed - r.speed) * horizon;
            float clearance = r.halfLength + other.halfLength + followingGap + 5;
            if (Mathf.Abs(delta) < clearance || Mathf.Abs(predicted) < clearance || delta * predicted < 0) return false;
        }
        return true;
    }

    static bool SweptLanesOverlap(Racer r, float lateral, Racer other)
    {
        float width = r.halfWidth + other.halfWidth + 0.8f;
        return Mathf.Min(r.lateral, lateral) < Mathf.Max(other.previousLateral, other.lateral) + width &&
               Mathf.Max(r.lateral, lateral) > Mathf.Min(other.previousLateral, other.lateral) - width;
    }

    public Vector3 RoadPosition(float distance, float lateral)
    {
        distance = Mathf.Clamp(distance, 0, RoadLength);
        int index = Array.BinarySearch(cumulative, distance);
        if (index < 0) index = ~index - 1;
        index = Mathf.Clamp(index, 0, cumulative.Length - 2);
        float t = Mathf.InverseLerp(cumulative[index], cumulative[index + 1], distance);
        Vector3 a = Vector3.Lerp(left[index], left[index + 1], t);
        Vector3 b = Vector3.Lerp(right[index], right[index + 1], t);
        return (a + b) * 0.5f + (b - a).normalized * lateral;
    }

    Vector3 Direction(float d) => (RoadPosition(d + 6, 0) - RoadPosition(d - 6, 0)).normalized;

    void LateUpdate()
    {
        if (!Ready) return;
        float alpha = paused ? 1 : Mathf.Clamp01(accumulator / Step);
        foreach (var r in racers)
            Place(r, Mathf.Lerp(r.previousDistance, r.distance, alpha), Mathf.Lerp(r.previousLateral, r.lateral, alpha), (r.lateral - r.previousLateral) / Step);
        UpdateCamera();
    }

    public void PresentCurrentState()
    {
        if (!Ready) return;
        foreach (var r in racers) Place(r, r.distance, r.lateral, (r.lateral - r.previousLateral) / Step);
        UpdateCamera();
    }

    void Place(Racer r, float distance, float lateral, float lateralSpeed)
    {
        Vector3 p = RoadPosition(distance, lateral);
        Vector3 up = Vector3.up;
        if (roadCollider != null && roadCollider.Raycast(new Ray(p + Vector3.up * 8, Vector3.down), out RaycastHit hit, 16))
        { p = hit.point; up = hit.normal; }
        Vector3 f = Direction(distance);
        Vector3 side = (RoadPosition(distance, 1) - RoadPosition(distance, 0)).normalized;
        Vector3 heading = Vector3.ProjectOnPlane(f * Mathf.Max(1, r.speed) + side * lateralSpeed, up).normalized;
        r.car.SetPositionAndRotation(p + up * r.rideHeight, Quaternion.LookRotation(heading, up));
        if (r.cruise != null)
        {
            r.cruise.speedKph = r.speed * 3.6f; r.cruise.wheelSpeedKph = r.speed * 3.6f;
            float curvature = Vector3.SignedAngle(Direction(distance - 8), Direction(distance + 8), up) * Mathf.Deg2Rad / 16;
            float wheel = Mathf.Atan(curvature * Mathf.Max(2.5f, r.halfLength)) * Mathf.Rad2Deg;
            wheel += Vector3.SignedAngle(f, heading, up) * 0.4f;
            r.cruise.steeringWheelAngle = Mathf.Clamp(wheel, -20, 20) / Mathf.Max(1, r.cruise.wheelAngleAt360) * 360;
            r.cruise.steering.startHandleAngle = r.cruise.steeringWheelAngle;
            r.cruise.steering.targetHandleAngle = r.cruise.steeringWheelAngle;
            r.cruise.ApplyTimelinePose(this, Elapsed, Mathf.Max(0, distance - r.startDistance), r.cruise.steeringWheelAngle);
        }
    }

    void OnDestroy()
    {
        foreach (var r in racers) if (r != null && r.cruise != null) r.cruise.ReleaseTimeline(this);
    }

    void CreateCamera()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        }
        var go = new GameObject("Autonomous Race Preview Camera");
        go.transform.SetParent(transform, false);
        previewCamera = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
        previewCamera.tag = "MainCamera";
        previewCamera.nearClipPlane = 0.3f; previewCamera.farClipPlane = 12000; previewCamera.fieldOfView = 55;
        previewCamera.allowHDR = true;
        UpdateCamera();
    }

    void UpdateCamera()
    {
        if (previewCamera == null) return;
        if (cameraView == 1)
        {
            Transform subject = racers[3].car;
            Vector3 target = subject.position + subject.forward * 15 + Vector3.up * 1.5f;
            Vector3 position = subject.position - subject.forward * 26 + subject.right * 5 + Vector3.up * 10;
            previewCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            return;
        }
        float min = float.MaxValue, max = 0;
        foreach (var r in racers) { min = Mathf.Min(min, r.distance); max = Mathf.Max(max, r.distance); }
        float middle = (min + max) * 0.5f;
        Vector3 focus = RoadPosition(middle, 0) + Vector3.up * 2;
        Vector3 forward = Direction(middle), side = Vector3.Cross(Vector3.up, forward);
        float spread = Mathf.Max(65, max - min);
        Vector3 offset = cameraView == 2 ? Vector3.up * (spread * 1.15f + 45) - forward * 8
            : side * (spread * 0.8f + 35) - forward * (spread * 0.35f) + Vector3.up * (spread * 0.48f + 20);
        previewCamera.transform.SetPositionAndRotation(focus + offset, Quaternion.LookRotation(-offset, Vector3.up));
    }

    void OnGUI()
    {
        if (!showControls || !Ready) return;
        GUILayout.BeginArea(new Rect(16, 16, 300, 275), GUI.skin.box);
        GUILayout.Label("RH AUTONOMOUS RACE / " + Elapsed.ToString("0.0") + " s");
        foreach (var r in racers) GUILayout.Label(r.car.name + "  " + (r.speed * 3.6f).ToString("0") + " km/h");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(paused ? "Resume" : "Pause")) paused = !paused;
        if (GUILayout.Button("Restart")) RestartRace();
        GUILayout.EndHorizontal();
        cameraView = GUILayout.Toolbar(cameraView, new[] { "Wide", "Chase", "Top" });
        if (GUILayout.Button("Hide controls (Inspector to restore)")) showControls = false;
        GUILayout.EndArea();
    }
}
