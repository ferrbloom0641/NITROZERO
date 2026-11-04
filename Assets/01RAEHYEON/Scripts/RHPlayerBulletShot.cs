using UnityEngine;
using UnityEngine.Playables;
// Apply the recorded spline progress after Timeline has written its animation fields.
[ExecuteAlways,DefaultExecutionOrder(3200)]
public sealed class RHPlayerBulletShot:MonoBehaviour {
 public PlayableDirector director;
 public BulletSplineController bullet;
 public Transform bonnet;
 public Vector3 bonnetLocalPoint;
 public float flightStart=8.066667f;
 void LateUpdate(){Apply();}
 public void Apply(){if(!bullet||!bonnet||!director)return;float progress=bullet.Progress;bullet.Progress=1;Vector3 end=bullet.transform.position;bullet.Progress=progress;bullet.transform.position+=(bonnet.TransformPoint(bonnetLocalPoint)-end)*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,1,progress));}
}
