using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
public sealed class ExteriorDriftClip : PlayableAsset, ITimelineClipAsset
{
 public ClipCaps clipCaps=>ClipCaps.None;
 public override double duration=>14;
 public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)=>ScriptPlayable<ExteriorDriftClock>.Create(graph);
}
public sealed class ExteriorDriftClock : PlayableBehaviour {}
