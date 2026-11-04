using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(.9f, .25f, .12f), TrackClipType(typeof(CockpitMissileClip)), TrackBindingType(typeof(CockpitMissilePass))]
public sealed class CockpitMissileTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        => ScriptPlayable<CockpitMissileMixer>.Create(graph, inputCount);
}

public sealed class CockpitMissileMixer : PlayableBehaviour
{
    CockpitMissilePass target;
    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        target = playerData as CockpitMissilePass;
        if (!target) return;
        for (int i = 0; i < playable.GetInputCount(); i++)
            if (playable.GetInputWeight(i) > 0)
            {
                target.Evaluate((float)playable.GetInput(i).GetTime());
                return;
            }
    }
    public override void OnGraphStop(Playable playable) { if (target) target.ResetShot(); }
}
