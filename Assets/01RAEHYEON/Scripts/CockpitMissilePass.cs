using UnityEngine;
using UnityEngine.VFX;

/// <summary>Deterministic, camera-relative cockpit insert. All times are shot seconds.</summary>
[ExecuteAlways, DefaultExecutionOrder(3000)]
public sealed class CockpitMissilePass : MonoBehaviour
{
    public Transform car, missile, exhaust, head, neck;
    public VisualEffect smoke;
    public Transform entry, exit;
    [Header("Shot timing")]
    [Min(1)] public float duration = 8.5f;
    public float entryTime = 0.8f, exitTime = 6.8f;
    [Header("Drift / gaze")]
    public float driftDegrees = 70f;
    public Vector3 driftPivot = new Vector3(0, 1, -8);
    public Vector3 slide = new Vector3(1.2f, 0, 2.5f);
    [Range(0, 45)] public float maxHeadYaw = 24f;
    [Range(0, 1)] public float gazeStrength = .8f;
    [Min(0)] public float gazeLag = .18f;
    [Min(.1f)] public float gazeSoftDistance = 3f;
    [Header("Existing smoke graph / slow simulation")]
    [Range(.05f, 1)] public float smokeTimeScale = .32f;
    public float smokeWidth = .065f, smokeExpansion = .16f, smokeSpawnRate = 420f;
    [Min(0)] public float smokeCoreIntensity = .65f;
    [Header("Drift smoke / exhaust glow")]
    public Damin.VFX.TireSmoke.Progressive.ProgressiveTireSmokeController tireSmoke;
    public Light exhaustGlow;
    [Range(0,1)] public float tireSmokePower = .6f;
    [Min(0)] public float exhaustGlowIntensity = .65f;
    [HideInInspector] public Vector3 carRestPosition;
    [HideInInspector] public Quaternion carRestRotation, headRestRotation, neckRestRotation;
    [HideInInspector] public Vector3 headRestInCar;
    [HideInInspector] public bool calibrated;
    [System.NonSerialized] public float previewTime;
    float smokeTime = -1;
    bool evaluated;

    void OnValidate()
    {
        duration = Mathf.Max(1f, duration);
        entryTime = Mathf.Clamp(entryTime, 0, duration - .2f);
        exitTime = Mathf.Clamp(exitTime, entryTime + .1f, duration - .1f);
        smokeTimeScale = Mathf.Clamp(smokeTimeScale, .05f, 1f);
    }

    public void CaptureRestPose()
    {
        carRestPosition = car.localPosition;
        carRestRotation = car.localRotation;
        headRestRotation = head.localRotation;
        neckRestRotation = neck.localRotation;
        headRestInCar = car.InverseTransformPoint(head.position);
        calibrated = true;
    }

    public Vector3 MissileLocalPosition(float time)
    {
        // Constant slow speed inside the cabin; faster entry and exit outside it.
        float u = (time - entryTime) / Mathf.Max(.01f, exitTime - entryTime);
        if (u < 0) u *= 2.2f;
        if (u > 1) u = 1 + (u - 1) * 3.5f;
        return Vector3.LerpUnclamped(entry.localPosition, exit.localPosition, u);
    }

    public void Evaluate(float time, bool simulateSmoke = true)
    {
        if (!calibrated || !car || !missile || !entry || !exit || !head || !neck) return;
        previewTime = Mathf.Clamp(time, 0, duration);
        evaluated = true;
        if (simulateSmoke && smoke) SimulateTrail(previewTime);
        Pose(previewTime);
        ApplyHead();
        float fade = Mathf.SmoothStep(0,1,previewTime/.8f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-1.2f,duration,previewTime)));
        if(tireSmoke){tireSmoke.SetTimelineState(this,tireSmokePower*fade,1.05f,1.05f,1,.7f,1.1f);tireSmoke.SetVehicleSpeed(5);}
        if(exhaustGlow)exhaustGlow.intensity=exhaustGlowIntensity*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(exitTime+.35f,duration,previewTime)));
    }

    void Pose(float time)
    {
        float u = Mathf.SmoothStep(0, 1, time / duration);
        Quaternion turn = Quaternion.Euler(0, driftDegrees * u, 0);
        car.localRotation = carRestRotation * turn;
        car.localPosition = carRestPosition + carRestRotation * (driftPivot - turn * driftPivot + slide * u);
        missile.position = car.TransformPoint(MissileLocalPosition(time));
        missile.rotation = Quaternion.LookRotation(car.TransformDirection(exit.localPosition - entry.localPosition), car.up);
    }

    void LateUpdate()
    {
        // After the existing hand-lock RigBuilder evaluates, preserve the seated hands
        // and apply only the neck/head contribution, without accumulating rotations.
        if (evaluated && calibrated) ApplyHead();
    }

    void ApplyHead()
    {
        Vector3 delta = MissileLocalPosition(previewTime - gazeLag) - headRestInCar;
        float fade = Mathf.SmoothStep(0, 1, previewTime / .65f) *
                     (1 - Mathf.SmoothStep(0, 1, (previewTime - exitTime) / (duration - exitTime)));
        float yaw = Mathf.Clamp(Mathf.Atan2(delta.x, Mathf.Max(gazeSoftDistance, delta.z)) * Mathf.Rad2Deg, -maxHeadYaw, maxHeadYaw) * gazeStrength * fade;
        float pitch = Mathf.Clamp(-Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg, -5, 5) * fade;
        neck.localRotation = neckRestRotation;
        head.localRotation = headRestRotation;
        neck.rotation = Quaternion.AngleAxis(yaw * .3f, car.up) * neck.rotation;
        head.rotation = Quaternion.AngleAxis(yaw * .7f, car.up) * Quaternion.AngleAxis(pitch, car.right) * head.rotation;
    }

    void Float(string name, float value) { if (smoke.HasFloat(name)) smoke.SetFloat(name, value); }
    void Vector(string name, Vector3 value) { if (smoke.HasVector3(name)) smoke.SetVector3(name, value); }

    void SimulateTrail(float time)
    {
        smoke.pause = true;
        if (smokeTime < 0 || time < smokeTime - .001f)
        {
            smoke.resetSeedOnPlay = false;
            smoke.startSeed = 3249;
            smoke.Reinit();
            smoke.pause = true;
            smokeTime = 0;
        }
        Float("SpawnRate", smokeSpawnRate);
        Float("SmokeDensity", 1.15f);
        Float("ParticleSize", smokeWidth);
        Float("Expansion", smokeExpansion);
        Float("Turbulence", .025f);
        Float("Speed", .12f);
        Float("TrailLength", 5f);
        Float("MaxSmokeLifetime", 2.4f);
        Float("TailFadeStart", .55f);
        Float("CoreIntensity", smokeCoreIntensity);
        if (smoke.HasVector4("SmokeColor")) smoke.SetVector4("SmokeColor", new Vector4(.82f, .85f, .89f, 1));
        const float step = 1f / 60f;
        // Replaying every emission position makes backwards scrubbing reproducible.
        while (smokeTime + .00001f < time)
        {
            float dt = Mathf.Min(step, time - smokeTime);
            Pose(smokeTime);
            Vector3 previous = exhaust.position;
            smokeTime += dt;
            Pose(smokeTime);
            Vector("PreviousEmitterPosition", previous);
            Vector("EmitterPosition", exhaust.position);
            Vector("EmitterDirection", exhaust.forward);
            Float("MissileSpeed", Vector3.Distance(previous, exhaust.position) / Mathf.Max(.00001f, dt * smokeTimeScale));
            Float("Emission", smokeTime < exitTime + .35f ? 1 : 0);
            smoke.Simulate(dt * smokeTimeScale, 1);
        }
    }

    public void ResetShot()
    {
        evaluated = false;
        previewTime = 0;
        smokeTime = -1;
        if(tireSmoke)tireSmoke.ReleaseTimeline(this);
        if(exhaustGlow)exhaustGlow.intensity=exhaustGlowIntensity;
        if (!calibrated) return;
        if (car) { car.localPosition = carRestPosition; car.localRotation = carRestRotation; }
        if (neck) neck.localRotation = neckRestRotation;
        if (head) head.localRotation = headRestRotation;
        if (missile && entry && exit) Pose(0);
        if (smoke) { Float("Emission", 0); smoke.Reinit(); smoke.pause = true; }
    }

    void OnDisable() => ResetShot();
    void OnDrawGizmosSelected()
    {
        if (!entry || !exit) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(entry.position, exit.position);
        Gizmos.DrawWireSphere(entry.position, .13f);
        Gizmos.DrawWireSphere(exit.position, .13f);
    }
}
