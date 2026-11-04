using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
[TrackColor(.65f,.4f,.1f),TrackClipType(typeof(GreenCarEndClip)),TrackBindingType(typeof(GreenCarExplosionEnd))]
public sealed class GreenCarEndTrack:TrackAsset {
 public override Playable CreateTrackMixer(PlayableGraph graph,GameObject go,int inputs)=>ScriptPlayable<GreenCarEndMixer>.Create(graph,inputs);
}
public sealed class GreenCarEndMixer:PlayableBehaviour {
 public override void ProcessFrame(Playable playable,FrameData info,object playerData){var s=playerData as GreenCarExplosionEnd;if(!s)return;for(int i=0;i<playable.GetInputCount();i++)if(playable.GetInputWeight(i)>0){s.Evaluate((float)playable.GetInput(i).GetTime()+s.cutStartTime);return;}}
}
