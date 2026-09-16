using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Damin.VFX.TireSmoke.Progressive
{
    [System.Serializable]
    public sealed class TireSmokeClip : PlayableAsset, ITimelineClipAsset
    {
        [Range(0,1)] public float smokePower=1;
        [Range(.25f,3)] public float lifetime=1.2f;
        [Range(.25f,3)] public float expansion=1.4f;
        [Min(.1f)] public float groundWidth=1;
        [Min(.1f)] public float groundHeight=1;
        [Min(.1f)] public float trailLength=1;
        public ClipCaps clipCaps=>ClipCaps.Blending;
        public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)
        {
            var p=ScriptPlayable<TireSmokeClipBehaviour>.Create(graph);
            var b=p.GetBehaviour();b.power=smokePower;b.lifetime=lifetime;b.expansion=expansion;
            b.width=groundWidth;b.height=groundHeight;b.length=trailLength;return p;
        }
    }
    public sealed class TireSmokeClipBehaviour:PlayableBehaviour
    {public float power,lifetime,expansion,width,height,length;}

    [TrackColor(.72f,.74f,.78f)]
    [TrackClipType(typeof(TireSmokeClip))]
    [TrackBindingType(typeof(ProgressiveTireSmokeController))]
    public sealed class TireSmokeTrack:TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph,GameObject go,int inputCount)=>ScriptPlayable<TireSmokeMixer>.Create(graph,inputCount);
    }

    public sealed class TireSmokeMixer:PlayableBehaviour
    {
        ProgressiveTireSmokeController bound;
        public override void ProcessFrame(Playable playable,FrameData info,object playerData)
        {
            var target=playerData as ProgressiveTireSmokeController;
            if(bound!=target){if(bound)bound.ReleaseTimeline(this);bound=target;}
            if(!target||!target.isActiveAndEnabled)return;
            float weightSum=0,power=0,life=0,expansion=0,width=0,height=0,length=0;
            for(int i=0;i<playable.GetInputCount();i++){
                float w=playable.GetInputWeight(i);if(w<=0)continue;
                var b=((ScriptPlayable<TireSmokeClipBehaviour>)playable.GetInput(i)).GetBehaviour();
                weightSum+=w;power+=b.power*w;life+=b.lifetime*w;expansion+=b.expansion*w;
                width+=b.width*w;height+=b.height*w;length+=b.length*w;
            }
            if(weightSum>0)target.SetTimelineState(this,Mathf.Clamp01(power),life/weightSum,expansion/weightSum,width/weightSum,height/weightSum,length/weightSum);
            else target.SetTimelineState(this,0,target.smokeLifetime,target.smokeExpansion,target.groundWidth,target.groundHeight,target.trailLength);
        }
        public override void OnGraphStop(Playable playable)=>Release();
        public override void OnPlayableDestroy(Playable playable)=>Release();
        void Release(){if(bound)bound.ReleaseTimeline(this);bound=null;}
    }
}
