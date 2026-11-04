using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
public sealed class GreenCarEndClip:PlayableAsset,ITimelineClipAsset {
 public ClipCaps clipCaps=>ClipCaps.None;
 public override double duration=>4.8;
 public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)=>ScriptPlayable<GreenCarEndClock>.Create(graph);
}
public sealed class GreenCarEndClock:PlayableBehaviour {}
