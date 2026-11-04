using UnityEngine;
/// <summary>Seekable booster deployment and the existing exterior wheel rig.</summary>
[ExecuteAlways,DefaultExecutionOrder(-1000)]
public sealed class ExteriorDriftAction:MonoBehaviour
{
 public ExteriorMissileDrift shot;
 public BoosterDeploymentController booster;
 public TrailerCruiseMotion cruise;
 public float deployStart=8.4f,deployEnd=11.5f;
 public bool enableBooster = true;
 public float CurrentDeploy=>enableBooster?Mathf.SmoothStep(0,1,Mathf.InverseLerp(deployStart,deployEnd,shot.previewTime)):0;
 void Awake(){if(booster&&shot)booster.SetCinematicOwner(shot.car);}
 public void Evaluate(float t){
 if(!shot||!shot.car)return;
 if(booster&&enableBooster){booster.SetCinematicOwner(shot.car);booster.ApplyCinematicAmount(Mathf.SmoothStep(0,1,Mathf.InverseLerp(deployStart,deployEnd,t)));}
 if(cruise)cruise.ApplyTimelinePose(this,t,shot.travel.Evaluate(t)*1.6f,-Mathf.Sin(shot.clockwiseYaw.Evaluate(t)*Mathf.Deg2Rad)*300);
 }
 void OnDisable(){if(cruise)cruise.ReleaseTimeline(this);}
}
