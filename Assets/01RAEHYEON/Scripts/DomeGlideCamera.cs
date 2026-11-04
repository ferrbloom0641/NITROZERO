using UnityEngine;

/// <summary>A terrain overview flight driving a Cinemachine camera transform.</summary>
[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public sealed class DomeGlideCamera : MonoBehaviour
{
    [Min(1)] public float duration = 70f;
    public bool playOnStart = true;
    [Range(0, 1)] public float preview;
    public Vector3 start = new Vector3(650, 140, 28);
    public Vector3 control1 = new Vector3(200, 200, -90);
    public Vector3 control2 = new Vector3(-240, 260, -420);
    public Vector3 finish = new Vector3(-520, 220, -330);
    public Vector3 roadLook = new Vector3(0, -20, 28);
    public Vector3 domeLook = new Vector3(-724.3f, 35, 27.2f);
    [Min(0)] public float terrainClearance = 65;
    [Tooltip("지형 안전 높이에 추가할 비행 고도")]
    [Min(0)] public float altitudeOffset = 150;
    [Range(-5, 5)] public float bankDegrees = -2;
    readonly float[] lengths = new float[257];
    float elapsed, lift;
    bool playing, rebuildRequested;

    void OnEnable()
    {
        BuildPath();
        elapsed = 0;
        playing = playOnStart;
        Apply(Application.isPlaying ? 0 : preview);
    }
    void OnValidate() { duration = Mathf.Max(1, duration); rebuildRequested = true; }
    void Update()
    {
        if (rebuildRequested) { BuildPath(); rebuildRequested = false; }
        if (!Application.isPlaying) { Apply(preview); return; }
        if (!playing) return;
        elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
        Apply(elapsed / duration);
        if (elapsed >= duration) playing = false;
    }
    Vector3 Position(float t)
    {
        float u = 1 - t;
        return u*u*u*start + 3*u*u*t*control1 + 3*u*t*t*control2 + t*t*t*finish;
    }
    void BuildPath()
    {
        lift = 0; lengths[0] = 0;
        var previous = Position(0);
        var terrains = Terrain.activeTerrains;
        for (int i = 0; i < lengths.Length; i++)
        {
            var p = Position(i / 256f);
            if (i > 0) lengths[i] = lengths[i-1] + Vector3.Distance(previous, p);
            previous = p;
            foreach (var terrain in terrains)
            {
                if (terrain.gameObject.scene != gameObject.scene || !terrain.terrainData) continue;
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (p.x < origin.x || p.x > origin.x+size.x || p.z < origin.z || p.z > origin.z+size.z) continue;
                lift = Mathf.Max(lift, terrain.SampleHeight(p) + origin.y + terrainClearance - p.y);
            }
        }
    }
    void Apply(float progress)
    {
        // Short ease-in/out, then almost constant travel speed; no repeated oscillation.
        float t = Mathf.Clamp01(progress);
        const float ramp = .12f;
        float distanceFraction = t < ramp ? t*t/(2*ramp) :
            t > 1-ramp ? 1-ramp-(1-t)*(1-t)/(2*ramp) : t-ramp*.5f;
        distanceFraction /= 1-ramp;
        float distance = distanceFraction * lengths[256];
        int index = 1;
        while (index < 256 && lengths[index] < distance) index++;
        float pathT = (index-1 + Mathf.InverseLerp(lengths[index-1], lengths[index], distance))/256f;
        Vector3 p = Position(pathT) + Vector3.up*(lift + altitudeOffset);
        Vector3 look = Vector3.Lerp(roadLook, domeLook, Mathf.SmoothStep(0,1,pathT));
        Quaternion rotation = Quaternion.LookRotation(look-p, Vector3.up);
        float bank = bankDegrees * Mathf.Sin(Mathf.PI*pathT);
        transform.SetPositionAndRotation(p, rotation * Quaternion.AngleAxis(bank, Vector3.forward));
    }
    [ContextMenu("Restart Glide")]
    public void RestartGlide() { BuildPath(); elapsed = 0; playing = true; Apply(0); }
    [ContextMenu("Pause Glide")]
    public void PauseGlide() { playing = false; }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var previous = Position(0)+Vector3.up*(lift + altitudeOffset);
        for (int i=1; i<=64; i++)
        {
            var next = Position(i/64f)+Vector3.up*(lift + altitudeOffset);
            Gizmos.DrawLine(previous,next); previous=next;
        }
        Gizmos.color=Color.yellow;
        Gizmos.DrawWireSphere(domeLook,12);
    }
}
