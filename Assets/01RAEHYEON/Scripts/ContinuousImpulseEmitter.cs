using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Keeps a CinemachineImpulseSource firing at a steady rate, so a passing vehicle
/// produces a continuous rumble instead of a single thump.
///
/// Impulse is built for one-off hits (an explosion, a landing). Firing it repeatedly
/// at a short interval is how you get a sustained effect out of it, and it keeps the
/// two things Impulse gives you for free:
///   - distance falloff (set ImpulseType to Dissipating on the source)
///   - automatic summing when several vehicles are in range at once
///
/// Put this on each vehicle alongside a Cinemachine Impulse Source.
/// The camera needs a Cinemachine Impulse Listener.
/// </summary>
[AddComponentMenu("NITROZERO/Continuous Impulse Emitter")]
[RequireComponent(typeof(CinemachineImpulseSource))]
[DisallowMultipleComponent]
public class ContinuousImpulseEmitter : MonoBehaviour
{
    [Tooltip("Seconds between impulses. Should be a little shorter than the source's " +
             "Impulse Duration so the pulses overlap into a continuous rumble rather " +
             "than a series of separate thumps. A fast-moving vehicle covers a lot of " +
             "ground between pulses, so shorten this as speed goes up.")]
    [Range(0.01f, 1f)]
    public float Interval = 0.05f;

    [Tooltip("Overall strength multiplier passed to each impulse.")]
    [Range(0f, 5f)]
    public float Force = 1f;

    [Tooltip("Randomises each pulse's force by this fraction, so the rumble does not " +
             "sound mechanical. 0.3 means each pulse lands between 70% and 130%.")]
    [Range(0f, 1f)]
    public float ForceVariation = 0.3f;

    [Tooltip("Emit only while the object is actually moving. Uses the distance travelled " +
             "between frames, so it works with Spline Animate, Timeline or anything else " +
             "that drives the Transform — no Rigidbody needed.")]
    public bool OnlyWhileMoving = true;

    [Tooltip("Speed below which nothing is emitted, in units per second.")]
    [Min(0f)]
    public float MinSpeed = 0.5f;

    [Tooltip("Scale the force by speed as well, so a faster pass shakes harder. " +
             "Force reaches its full value at this speed.")]
    [Min(0.01f)]
    public float SpeedForFullForce = 30f;

    [Tooltip("If off, speed does not affect the force at all.")]
    public bool ScaleForceBySpeed = true;

    CinemachineImpulseSource m_Source;
    float m_Timer;
    Vector3 m_LastPosition;
    bool m_HasLastPosition;

    void Awake()
    {
        m_Source = GetComponent<CinemachineImpulseSource>();
    }

    void OnEnable()
    {
        m_HasLastPosition = false;
        m_Timer = 0f;
    }

    // LateUpdate, not Update: Spline Animate, Timeline and Cinemachine all write the
    // Transform during Update, so reading the position in Update would be a frame behind.
    // At speed that lag is very visible — the rumble trails the vehicle.
    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        // Measure speed straight from the Transform so this works no matter what
        // is moving the object.
        Vector3 pos = transform.position;
        float speed = 0f;
        if (m_HasLastPosition)
            speed = Vector3.Distance(pos, m_LastPosition) / dt;
        m_LastPosition = pos;
        m_HasLastPosition = true;

        if (OnlyWhileMoving && speed < MinSpeed)
            return;

        m_Timer -= dt;
        if (m_Timer > 0f)
            return;
        m_Timer = Interval;

        float force = Force;
        if (ScaleForceBySpeed)
            force *= Mathf.Clamp01(speed / SpeedForFullForce);
        if (ForceVariation > 0f)
            force *= 1f + Random.Range(-ForceVariation, ForceVariation);
        if (force <= 0f)
            return;

        m_Source.GenerateImpulseWithForce(force);
    }
}
