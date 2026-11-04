using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>Cinematic, road-constrained racing. Owns vehicle roots; no tyre physics required.</summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed partial class AutonomousRaceController : MonoBehaviour
{
    public enum DriverStyle { Balanced, Patient, Precise, Attacker, Opportunist, CornerSpecialist }
    public enum RacePhase { Cruise, Prepare, MoveOut, Pass, Return, Recover }

    public struct DrivingProfile
    {
        public float aggression, preparation, gapScale, lanePace, cornerConfidence, apexBias, lineUse;
        public DrivingProfile(float aggression, float preparation, float gapScale, float lanePace,
            float cornerConfidence, float apexBias, float lineUse)
        {
            this.aggression = aggression; this.preparation = preparation; this.gapScale = gapScale;
            this.lanePace = lanePace; this.cornerConfidence = cornerConfidence;
            this.apexBias = apexBias; this.lineUse = lineUse;
        }
    }

    public static DrivingProfile Profile(DriverStyle style)
    {
        switch (style)
        {
            case DriverStyle.Patient: return new DrivingProfile(.25f, 2.2f, 1.4f, .8f, .4f, .03f, .62f);
            case DriverStyle.Precise: return new DrivingProfile(.15f, 2.8f, 1.5f, .75f, .55f, 0, .68f);
            case DriverStyle.Attacker: return new DrivingProfile(.95f, .55f, 1, 1.05f, .85f, -.035f, .88f);
            case DriverStyle.Opportunist: return new DrivingProfile(.8f, .85f, 1.1f, 1, .7f, .02f, .8f);
            case DriverStyle.CornerSpecialist: return new DrivingProfile(.65f, 1.1f, 1.15f, .95f, 1, .10f, .92f);
            default: return new DrivingProfile(.5f, 1.4f, 1.2f, .9f, .7f, .035f, .78f);
        }
    }

    struct Corner { public float entry, exit, insideSign; }
    [Serializable]
    public sealed class Racer
    {
        public Transform car;
        [Range(240, 300)] public float preferredSpeedKph = 270;
        public DriverStyle drivingStyle;
        [Tooltip("Optional four tyre contact points measured from mesh vertices in vehicle-local space. Avoids rotated mesh-bounds inflation; scales with the vehicle.")]
        public Vector3[] asphaltContacts = Array.Empty<Vector3>();
        [NonSerialized] public float distance, lateral, speed, targetLane, cooldown;
        [NonSerialized] public float previousDistance, previousLateral, halfWidth, halfLength, rideHeight;
        [NonSerialized] public float startDistance;
        [NonSerialized] public float lateralVelocity, yaw, yawVelocity, wheelAngle, wheelVelocity;
        [NonSerialized] public float previousSpeed, previousWheelAngle;
        [NonSerialized] public Quaternion rotation, previousRotation;
        [NonSerialized] public Vector3 up;
        [NonSerialized] public Vector3[] tyreContacts;
        [NonSerialized] public TrailerCruiseMotion cruise;
        [NonSerialized] public RacePhase phase;
        [NonSerialized] public float phaseTime, homeLane, passingLane;
        [NonSerialized] public Racer opponent;
        [NonSerialized] public int completedPasses, abortedPasses, phaseVisits;
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
    [Header("Smooth driving")]
    [Tooltip("Time to ease into and out of a lane change.")]
    [Range(0.2f, 3)] public float laneChangeResponse = 1.1f;
    [Range(.5f, 6)] public float maxLateralAcceleration = 3;
    [Tooltip("Distance either side of the car used to average the road heading.")]
    [Range(6, 60)] public float headingSampleDistance = 24;
    [Range(0.1f, 1.5f)] public float steeringResponse = 0.4f;
    [Range(5, 60)] public float maxYawSpeed = 28;
    [Range(0.1f, 1)] public float wheelSteeringResponse = 0.25f;
    [Header("Racing line and tactics")]
    public bool useRacingLine = true;
    [Min(60)] public float cornerApproachDistance = 180;
    [Min(60)] public float cornerExitDistance = 120;
    [Min(8)] public float passingTimeout = 22;
    public bool createPreviewCamera = true;
    public bool showControls = true;
    [Range(0, 2)] public int cameraView;
    public bool paused;
    [Header("Playback speed (preserves story and boost timing)")]
    [Range(.25f, 3f)] public float playbackSpeed = 1f;
    public float PlaybackSpeed => Mathf.Clamp(playbackSpeed, .25f, 3f);

    public float RoadLength { get; private set; }
    public float Elapsed { get; private set; }
    public int OvertakeDecisions { get; private set; }
    public bool Ready { get; private set; }
    public string Status { get; private set; } = "Not initialized";
    public float LaneExtent { get; private set; }
    public int LaneCount { get; private set; }
    public int CornerCount => corners.Count;
    public int CompletedOvertakes { get; private set; }
    readonly List<Corner> corners = new List<Corner>();
    float usableHalfWidth;
    const float Step = 0.02f;
    Vector3[] left, right, centres;
    float[] cumulative;
    int[] order;
    float accumulator;
    float presentedAlpha = 1;
    public float PresentedTime => Mathf.Max(0, Elapsed - Step * (1 - presentedAlpha));
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
        BuildSmoothRoad();
        BuildCorners();
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
        usableHalfWidth = minWidth * 0.5f - edgeMargin - smoothRoadInset;
        if (usableHalfWidth <= widest + 0.5f)
            throw new InvalidOperationException("A vehicle is wider than the usable road.");
        LaneCount = usableHalfWidth - widest >= widest * 2 + 1 ? 3 : 2;
        LaneExtent = usableHalfWidth;
        order = new int[racers.Length];
        Ready = true;
        InitializeStory();
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
        // Measure current world-sized tyre geometry; authored spin radii can predate scene scaling.
        if (r.cruise != null && r.cruise.Wheels != null && r.cruise.Wheels.Length == 4)
        {
            float bottom = 0; int valid = 0;
            var contacts = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var wheel = r.cruise.Wheels[i]; if (wheel == null) continue;
                float radius = r.cruise.WheelRadii != null && r.cruise.WheelRadii.Length == 4
                    ? r.cruise.WheelRadii[i] : r.cruise.wheelRadius;
                var rolling = r.cruise.WheelSpinRoots;
                Transform source = rolling != null && rolling.Length == 4 && rolling[i] != null ? rolling[i] : wheel;
                Bounds tyre = new Bounds(); bool measured = false;
                foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    if (!renderer.gameObject.activeInHierarchy) continue;
                    var b = renderer.localBounds;
                    for (int n = 0; n < 8; n++)
                    {
                        var corner = b.center + Vector3.Scale(b.extents, new Vector3((n & 1)==0?-1:1, (n & 2)==0?-1:1, (n & 4)==0?-1:1));
                        var point = inverse * (renderer.transform.TransformPoint(corner) - r.car.position);
                        if (!measured) { tyre = new Bounds(point, Vector3.zero); measured = true; }
                        else tyre.Encapsulate(point);
                    }
                }
                var contact = inverse * (wheel.position - r.car.position) - Vector3.up * radius;
                if (measured) contact = new Vector3(tyre.center.x, tyre.min.y, tyre.center.z);
                contacts[i] = contact;
                bottom += contact.y; valid++;
            }
            if (valid == 4) { r.rideHeight = -bottom / valid + .005f; r.tyreContacts = contacts; }
        }
        if (r.asphaltContacts != null && r.asphaltContacts.Length == 4)
        {
            r.tyreContacts = new Vector3[4];
            float bottom = 0;
            for (int i = 0; i < 4; i++)
            {
                r.tyreContacts[i] = inverse * r.car.TransformVector(r.asphaltContacts[i]);
                bottom += r.tyreContacts[i].y;
            }
            r.rideHeight = -bottom * .25f + .005f;
        }
    }

    [ContextMenu("Restart race")]
    public void RestartRace()
    {
        if (!Ready) return;
        Elapsed = 0; accumulator = 0; OvertakeDecisions = 0; CompletedOvertakes = 0; paused = false;
        if (storyMode) { RestartStory(); return; }
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
            r.homeLane = r.lateral; r.passingLane = r.lateral; r.opponent = null;
            r.phase = RacePhase.Cruise; r.phaseTime = 0; r.phaseVisits = 1;
            r.completedPasses = r.abortedPasses = 0;
            r.cooldown = i * 0.17f;
            r.previousDistance = r.distance; r.previousLateral = r.lateral;
            r.lateralVelocity = r.yawVelocity = r.wheelVelocity = r.wheelAngle = r.previousWheelAngle = 0;
            r.previousSpeed = r.speed;
            r.up = Vector3.up;
            Vector3 forward = Direction(r.distance);
            r.yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            r.rotation = r.previousRotation = Quaternion.LookRotation(forward, r.up);
            Place(r, r.distance, r.lateral, r.rotation, 0, r.speed, 0);
        }
    }

    void Update()
    {
        if (!Ready || paused) return;
        accumulator += Time.deltaTime * PlaybackSpeed;
        while (accumulator >= Step) { SimulateStep(Step); accumulator -= Step; }
    }

    public void SimulateStep(float dt)
    {
        if (!Ready || dt <= 0) return;
        if (storyMode) { SimulateStory(dt); return; }
        Elapsed += dt;
        for (int i = 0; i < racers.Length; i++)
        {
            order[i] = i;
            racers[i].previousDistance = racers[i].distance;
            racers[i].previousLateral = racers[i].lateral;
            racers[i].previousSpeed = racers[i].speed;
            racers[i].previousRotation = racers[i].rotation;
            racers[i].previousWheelAngle = racers[i].wheelAngle;
        }
        Array.Sort(order, (a, b) => racers[b].distance.CompareTo(racers[a].distance));
        float leaderDistance = racers[order[0]].distance;
        float tailDistance = racers[order[order.Length - 1]].distance;
        float packMiddle = (leaderDistance + tailDistance) * 0.5f;
        foreach (int i in order)
        {
            Racer r = racers[i]; r.cooldown -= dt;
            DrivingProfile profile = Profile(r.drivingStyle);
            float desiredKph = Mathf.Clamp(r.preferredSpeedKph + Mathf.Lerp(4, 16, profile.aggression) * Mathf.Sin(Elapsed * 0.17f + i * 1.7f), minimumSpeedKph, maximumSpeedKph);
            if (leaderDistance - tailDistance > preferredPackLength)
                desiredKph = Mathf.Clamp(desiredKph - (r.distance - packMiddle) * 0.3f, minimumSpeedKph, maximumSpeedKph);
            Vector3 f0 = Direction(r.distance + 15), f1 = Direction(r.distance + 85);
            float bend = Vector3.Angle(f0, f1);
            desiredKph = Mathf.Min(desiredKph, Mathf.Lerp(maximumSpeedKph, minimumSpeedKph + 20 * profile.cornerConfidence, Mathf.Clamp01(bend / 20)));
            Racer ahead = FindAhead(r, r.lateral, out float gap);
            PlanRace(r, ahead, gap, desiredKph, profile, dt);
            if (r.phase == RacePhase.MoveOut || r.phase == RacePhase.Pass)
                desiredKph = Mathf.Min(maximumSpeedKph, desiredKph + 8 * profile.aggression);
            // A lane change is a gradual lateral acceleration, not an instant sideways velocity.
            float lateralLimit = laneChangeSpeed * profile.lanePace * Mathf.Clamp01(r.speed / 15f);
            float omega = 2 / Mathf.Max(.2f, laneChangeResponse / profile.lanePace);
            float lateralAcceleration = Mathf.Clamp(omega * omega * (r.targetLane - r.lateral) - 2 * omega * r.lateralVelocity,
                -maxLateralAcceleration, maxLateralAcceleration);
            float nextLateralVelocity = Mathf.MoveTowards(r.lateralVelocity,
                Mathf.Clamp(r.lateralVelocity + lateralAcceleration * dt, -lateralLimit, lateralLimit), maxLateralAcceleration * dt);
            float lateral = r.lateral + (r.lateralVelocity + nextLateralVelocity) * .5f * dt;
            r.lateralVelocity = nextLateralVelocity;
            lateral = Mathf.Clamp(lateral, -Extent(r), Extent(r));
            float desired = desiredKph / 3.6f;
            float remaining = Mathf.Max(0, RoadLength - 15 - r.halfLength - r.distance);
            desired = Mathf.Min(desired, Mathf.Sqrt(2 * braking * remaining));
            float maxDistance = RoadLength - 15 - r.halfLength;
            foreach (Racer other in racers)
            {
                if (other == r || other.distance <= r.distance) continue;
                if (!SweptLanesOverlap(r, lateral, other)) continue;
                float available = other.distance - r.distance - r.halfLength - other.halfLength - followingGap * profile.gapScale;
                desired = Mathf.Min(desired, Mathf.Max(0, other.speed + (available - 15) * 0.5f));
                maxDistance = Mathf.Min(maxDistance, other.distance - r.halfLength - other.halfLength - followingGap);
            }
            r.speed = Mathf.MoveTowards(r.speed, desired, (desired < r.speed ? braking : acceleration * Mathf.Lerp(.85f, 1.1f, profile.aggression)) * dt);
            float next = Mathf.Max(r.distance, Mathf.Min(r.distance + r.speed * dt, maxDistance));
            r.speed = (next - r.distance) / dt;
            r.distance = next; r.lateral = lateral;
        }
        foreach (var r in racers) UpdateSteering(r, dt);
    }

    void BuildCorners()
    {
        corners.Clear();
        int first = -1, last = -1; float sign = 0;
        for (int i = 1; i < centres.Length - 1; i++)
        {
            Vector3 incoming = (centres[i] - centres[i - 1]).normalized;
            Vector3 outgoing = (centres[i + 1] - centres[i]).normalized;
            float angle = Vector3.SignedAngle(incoming, outgoing, Vector3.up);
            if (Mathf.Abs(angle) < .4f) continue;
            float inside = Mathf.Sign(angle) * Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, incoming), right[i] - left[i]));
            if (first >= 0 && (inside != sign || cumulative[i] - cumulative[last] > 350))
            {
                corners.Add(new Corner { entry = cumulative[first], exit = cumulative[last], insideSign = sign });
                first = -1;
            }
            if (first < 0) { first = i; sign = inside; }
            last = i;
        }
        if (first >= 0) corners.Add(new Corner { entry = cumulative[first], exit = cumulative[last], insideSign = sign });
    }

    /// <summary>Outside, apex, outside. Traffic can defer this preferred line at any time.</summary>
    public float RacingLineTarget(Racer r, float distance)
    {
        if (!useRacingLine) return r.homeLane;
        DrivingProfile profile = Profile(r.drivingStyle);
        foreach (var c in corners)
        {
            float approach = c.entry - cornerApproachDistance;
            float release = c.exit + cornerExitDistance;
            if (distance < approach - 200 || distance > release + 200) continue;
            float outside = -c.insideSign * Extent(r) * profile.lineUse;
            float inside = -outside;
            float apex = Mathf.Lerp(c.entry, c.exit, .5f + profile.apexBias);
            if (distance < approach) return Mathf.Lerp(r.homeLane, outside, Ease(Mathf.InverseLerp(approach - 200, approach, distance)));
            if (distance < apex) return Mathf.Lerp(outside, inside, Ease(Mathf.InverseLerp(approach, apex, distance)));
            if (distance < release) return Mathf.Lerp(inside, outside, Ease(Mathf.InverseLerp(apex, release, distance)));
            return Mathf.Lerp(outside, r.homeLane, Ease(Mathf.InverseLerp(release, release + 200, distance)));
        }
        return r.homeLane;
    }

    static float Ease(float t) => t * t * t * (t * (6 * t - 15) + 10);

    void SetPhase(Racer r, RacePhase phase)
    {
        r.phase = phase; r.phaseTime = 0; r.phaseVisits |= 1 << (int)phase;
    }

    void AbortPass(Racer r)
    {
        r.abortedPasses++;
        // Stay in the already reserved lane, rather than suddenly steering back into traffic.
        r.opponent = null;
        r.cooldown = Mathf.Lerp(6, 3, Profile(r.drivingStyle).aggression);
        SetPhase(r, RacePhase.Recover);
    }

    void FollowRacingLine(Racer r)
    {
        float line = RacingLineTarget(r, r.distance);
        if (Mathf.Abs(line - r.targetLane) < .01f || CanChange(r, line)) r.targetLane = line;
    }

    void PlanRace(Racer r, Racer ahead, float gap, float desiredKph, DrivingProfile profile, float dt)
    {
        r.phaseTime += dt;
        switch (r.phase)
        {
            case RacePhase.Cruise:
                FollowRacingLine(r);
                if (r.cooldown <= 0 && ahead != null && gap < Mathf.Lerp(130, 230, profile.aggression) &&
                    desiredKph / 3.6f > ahead.speed + Mathf.Lerp(1.5f, .4f, profile.aggression))
                {
                    r.opponent = ahead;
                    SetPhase(r, RacePhase.Prepare);
                }
                break;
            case RacePhase.Prepare:
                if (r.opponent == null || r.opponent.distance <= r.distance || r.opponent.distance - r.distance > 300)
                { AbortPass(r); break; }
                // Observe a gap for this driver's reaction time before committing to a passing lane.
                if (r.phaseTime >= profile.preparation && ChoosePassingLane(r, desiredKph, out float passingLane))
                {
                    r.passingLane = passingLane; r.targetLane = passingLane;
                    OvertakeDecisions++;
                    SetPhase(r, RacePhase.MoveOut);
                }
                else if (r.phaseTime > 8 + profile.preparation) AbortPass(r);
                break;
            case RacePhase.MoveOut:
                r.targetLane = r.passingLane;
                if (r.opponent == null) { AbortPass(r); break; }
                if (Mathf.Abs(r.lateral - r.passingLane) < .35f) SetPhase(r, RacePhase.Pass);
                else if (r.phaseTime > passingTimeout) AbortPass(r);
                break;
            case RacePhase.Pass:
                r.targetLane = r.passingLane;
                if (r.opponent == null) { AbortPass(r); break; }
                float clearance = r.halfLength + r.opponent.halfLength + followingGap * profile.gapScale + 5;
                if (r.distance - r.opponent.distance > clearance) SetPhase(r, RacePhase.Return);
                else if (r.phaseTime > passingTimeout) AbortPass(r);
                break;
            case RacePhase.Return:
                if (r.opponent == null) { AbortPass(r); break; }
                float returnLane = RacingLineTarget(r, r.distance);
                if (CanChange(r, returnLane)) r.targetLane = returnLane;
                float safeLead = r.halfLength + r.opponent.halfLength + followingGap;
                if (Mathf.Abs(r.lateral - returnLane) < .5f && r.distance - r.opponent.distance > safeLead)
                {
                    r.completedPasses++; CompletedOvertakes++;
                    r.opponent = null;
                    r.cooldown = Mathf.Lerp(7, 3, profile.aggression);
                    SetPhase(r, RacePhase.Recover);
                }
                else if (r.phaseTime > passingTimeout) AbortPass(r);
                break;
            case RacePhase.Recover:
                FollowRacingLine(r);
                if (r.cooldown <= 0) SetPhase(r, RacePhase.Cruise);
                break;
        }
    }

    bool ChoosePassingLane(Racer r, float desiredKph, out float lane)
    {
        DrivingProfile profile = Profile(r.drivingStyle);
        Racer leader = FindAhead(r, r.lateral, out float currentGap);
        float best = Mathf.Min(currentGap, 160) + (leader != null ? leader.speed : desiredKph / 3.6f) * 12;
        lane = r.targetLane; bool found = false;
        for (int l = -1; l <= 1; l++)
        {
            if (LaneCount == 2 && l == 0) continue;
            float candidate = l * Extent(r);
            if (Mathf.Abs(candidate - r.lateral) < 2 || !CanChange(r, candidate)) continue;
            Racer next = FindAhead(r, candidate, out float space);
            float score = Mathf.Min(space, 160) + (next != null ? next.speed : desiredKph / 3.6f) * 12;
            if (score <= best + Mathf.Lerp(15, 5, profile.aggression)) continue;
            best = score; lane = candidate; found = true;
        }
        return found;
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
        DrivingProfile profile = Profile(r.drivingStyle);
        float horizon = Mathf.Abs(candidate - r.lateral) / Mathf.Max(0.1f, laneChangeSpeed * profile.lanePace) + laneChangeResponse / profile.lanePace * 2 + 0.5f;
        foreach (var other in racers)
        {
            if (other == r) continue;
            float width = r.halfWidth + other.halfWidth + 1;
            if (Mathf.Max(other.lateral, other.targetLane) < lo - width || Mathf.Min(other.lateral, other.targetLane) > hi + width) continue;
            float delta = other.distance - r.distance;
            float predicted = delta + (other.speed - r.speed) * horizon;
            float clearance = r.halfLength + other.halfLength + followingGap * profile.gapScale + 5;
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
        return SmoothRoadSample(index, distance, lateral);
    }

    Vector3 Direction(float d) => (RoadPosition(d + headingSampleDistance, 0) - RoadPosition(d - headingSampleDistance, 0)).normalized;

    void UpdateSteering(Racer r, float dt)
    {
        Vector3 p = RoadPosition(r.distance, r.lateral);
        Vector3 roadUp = Vector3.up;
        if (roadCollider != null && roadCollider.Raycast(new Ray(p + Vector3.up * 8, Vector3.down), out RaycastHit hit, 16))
            roadUp = hit.normal;
        r.up = Vector3.Slerp(r.up, roadUp, 1 - Mathf.Exp(-dt / 0.3f)).normalized;
        // Compensate gently for steering response, without aiming a long way across a corner.
        Vector3 forward = Direction(r.distance + Mathf.Min(12, r.speed * steeringResponse * 0.5f));
        Vector3 side = (RoadPosition(r.distance, 1) - RoadPosition(r.distance, 0)).normalized;
        float lateralSpeed = (r.lateral - r.previousLateral) / dt;
        // A braking car must not suddenly face sideways as its forward speed approaches zero.
        Vector3 heading = forward * Mathf.Max(20, r.speed) + side * lateralSpeed;
        float targetYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
        float nextYaw = Mathf.SmoothDampAngle(r.yaw, targetYaw, ref r.yawVelocity,
            Mathf.Max(0.1f, steeringResponse), maxYawSpeed, dt);
        float delta = Mathf.Clamp(Mathf.DeltaAngle(r.yaw, nextYaw), -maxYawSpeed * dt, maxYawSpeed * dt);
        r.yaw += delta;
        r.yawVelocity = Mathf.Clamp(r.yawVelocity, -maxYawSpeed, maxYawSpeed);
        Vector3 smoothForward = Quaternion.Euler(0, r.yaw, 0) * Vector3.forward;
        r.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(smoothForward, r.up), r.up);
        float wheelbase = r.cruise != null && r.cruise.steeringWheelbase > 0
            ? r.cruise.steeringWheelbase : Mathf.Max(2.5f, r.halfLength);
        float targetWheel = Mathf.Atan(delta / dt * Mathf.Deg2Rad * wheelbase / Mathf.Max(10, r.speed)) * Mathf.Rad2Deg;
        r.wheelAngle = Mathf.SmoothDamp(r.wheelAngle, Mathf.Clamp(targetWheel, -20, 20),
            ref r.wheelVelocity, Mathf.Max(0.1f, wheelSteeringResponse), 45, dt);
    }

    void LateUpdate()
    {
        if (!Ready) return;
        PresentInterpolatedState(Mathf.Clamp01(accumulator / Step));
    }

    public void PresentCurrentState()
    {
        PresentInterpolatedState(1);
    }

    public float PresentedDistance(Racer racer) => Mathf.Lerp(racer.previousDistance, racer.distance, presentedAlpha);

    public void PresentInterpolatedState(float alpha)
    {
        if (!Ready) return;
        alpha = Mathf.Clamp01(alpha);
        presentedAlpha = alpha;
        float time = Mathf.Max(0, Elapsed - Step * (1 - alpha));
        foreach (var r in racers)
            Place(r, Mathf.Lerp(r.previousDistance, r.distance, alpha), Mathf.Lerp(r.previousLateral, r.lateral, alpha),
                Quaternion.Slerp(r.previousRotation, r.rotation, alpha), Mathf.Lerp(r.previousWheelAngle, r.wheelAngle, alpha),
                Mathf.Lerp(r.previousSpeed, r.speed, alpha), time);
        if (storyMode) PresentStoryBoost(time);
        UpdateCamera();
    }

    void Place(Racer r, float distance, float lateral, Quaternion rotation, float wheelAngle, float speed, float time)
    {
        Vector3 p = RoadPosition(distance, lateral);
        Vector3 up = Vector3.up;
        if (roadCollider != null && roadCollider.Raycast(new Ray(p + Vector3.up * 8, Vector3.down), out RaycastHit hit, 16))
        { p = hit.point; up = hit.normal; }
        r.car.SetPositionAndRotation(p + up * r.rideHeight, rotation);
        // Query only the asphalt collider, never nearby terrain, scenery or another car.
        // Cached rest contacts do not oscillate with the visual wheel spin.
        if (roadCollider != null && r.tyreContacts != null)
        {
            float correction = float.NegativeInfinity;
            foreach (var contact in r.tyreContacts)
            {
                Vector3 point = r.car.position + rotation * contact;
                if (roadCollider.Raycast(new Ray(point + Vector3.up * 20, Vector3.down), out RaycastHit tyreHit, 40))
                    correction = Mathf.Max(correction, tyreHit.point.y + .005f - point.y);
            }
            if (!float.IsNegativeInfinity(correction)) r.car.position += Vector3.up * correction;
        }
        if (r.cruise != null)
        {
            r.cruise.speedKph = speed * 3.6f; r.cruise.wheelSpeedKph = speed * 3.6f;
            r.cruise.steeringWheelAngle = wheelAngle / Mathf.Max(1, r.cruise.wheelAngleAt360) * 360;
            r.cruise.steering.startHandleAngle = r.cruise.steeringWheelAngle;
            r.cruise.steering.targetHandleAngle = r.cruise.steeringWheelAngle;
            r.cruise.ApplyTimelinePose(this, time, Mathf.Max(0, distance - r.startDistance), r.cruise.steeringWheelAngle);
        }
    }

    void OnDestroy()
    {
        if (storyBooster != null) { storyBooster.SetCinematicOwner(null); storyBooster.enabled = boosterWasEnabled; }
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
        foreach (var r in racers)
        {
            float distance = Mathf.Lerp(r.previousDistance, r.distance, presentedAlpha);
            min = Mathf.Min(min, distance); max = Mathf.Max(max, distance);
        }
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
        GUILayout.BeginArea(new Rect(16, 16, 350, 490), GUI.skin.box);
        GUILayout.Label("RH AUTONOMOUS RACE / " + Elapsed.ToString("0.0") + " s");
        GUILayout.Label("재생 배속: " + PlaybackSpeed.ToString("0.00") + "x (속도 표시는 시나리오 기준)");
        playbackSpeed = GUILayout.HorizontalSlider(PlaybackSpeed, .25f, 3f);
        GUILayout.BeginHorizontal();
        foreach (float rate in new[] { .5f, 1f, 1.5f, 2f, 3f })
            if (GUILayout.Button(rate.ToString("0.#") + "x")) playbackSpeed = rate;
        GUILayout.EndHorizontal();
        if (storyMode) GUILayout.Label(StoryBeat);
        foreach (var r in racers)
        {
            int rank = 1;
            foreach (var other in racers) if (other.distance > r.distance) rank++;
            GUILayout.Label(rank + "위  " + r.car.name.Replace("_Car_Final", "").Replace("_Final", "") + "  " + (r.speed * 3.6f).ToString("0") + " km/h");
            GUILayout.Label((storyMode ? StoryRoles[Array.IndexOf(racers,r)] : StyleNames[(int)r.drivingStyle]) + " / " + PhaseNames[(int)r.phase]);
        }
        GUILayout.Label("추월 완료: " + CompletedOvertakes);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(paused ? "Resume" : "Pause")) paused = !paused;
        if (GUILayout.Button("Restart")) RestartRace();
        GUILayout.EndHorizontal();
        if (createPreviewCamera) cameraView = GUILayout.Toolbar(cameraView, new[] { "Wide", "Chase", "Top" });
        if (GUILayout.Button("Hide controls (Inspector to restore)")) showControls = false;
        GUILayout.EndArea();
    }

    static readonly string[] StyleNames = { "균형형", "안정형", "정밀형", "공격형", "기회 포착형", "코너 특화형" };
    static readonly string[] PhaseNames = { "주행", "추월 준비", "옆 차선 진입", "추월 중", "주행선 복귀", "재정비" };
}
