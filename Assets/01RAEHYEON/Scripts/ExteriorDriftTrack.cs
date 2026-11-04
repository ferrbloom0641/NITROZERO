using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
[TrackColor(.85f,.19f,.12f),TrackClipType(typeof(ExteriorDriftClip)),TrackBindingType(typeof(ExteriorMissileDrift))]
public sealed class ExteriorDriftTrack : TrackAsset
{
 public override Playable CreateTrackMixer(PlayableGraph graph,GameObject go,int inputs)=>ScriptPlayable<ExteriorDriftMixer>.Create(graph,inputs);
}
public sealed class ExteriorDriftMixer:PlayableBehaviour
{
 ExteriorMissileDrift shot;
 public override void ProcessFrame(Playable playable,FrameData info,object playerData){shot=playerData as ExteriorMissileDrift;if(!shot)return;for(int i=0;i<playable.GetInputCount();i++)if(playable.GetInputWeight(i)>0){shot.Evaluate((float)playable.GetInput(i).GetTime());return;}}
 public override void OnGraphStop(Playable playable){if(shot)shot.ResetShot();}
}
