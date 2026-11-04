using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class CockpitMissileClip : PlayableAsset, ITimelineClipAsset
{
    public ClipCaps clipCaps => ClipCaps.None;
    public override double duration => 8.5;
    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        => ScriptPlayable<CockpitMissileClock>.Create(graph);
}

public sealed class CockpitMissileClock : PlayableBehaviour { }
