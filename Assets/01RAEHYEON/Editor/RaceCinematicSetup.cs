using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

public static class RaceCinematicSetup
{
    public static RaceCinematicDirector Build(AutonomousRaceController race)
    {
        var root = new GameObject("RACE CINEMACHINE - 4 Hero Shots");
        SceneManager.MoveGameObjectToScene(root, race.gameObject.scene);
        var director = root.AddComponent<RaceCinematicDirector>();
        director.race = race;
        race.createPreviewCamera = false;
        var output = new GameObject("Race Cinemachine Output");
        output.transform.SetParent(root.transform, false);
        director.outputCamera = output.AddComponent<Camera>();
        director.outputCamera.tag = "MainCamera";
        director.outputCamera.allowHDR = true;
        director.outputCamera.farClipPlane = 12000;
        director.outputCamera.nearClipPlane = .3f;
        output.AddComponent<AudioListener>();
        director.brain = output.AddComponent<CinemachineBrain>();
        director.brain.ChannelMask = OutputChannels.Channel04;
        director.brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        director.brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
        director.shots = new RaceCinematicShot[4];
        string[] names = { "CM01 - Front Pack", "CM02 - Rear Duel", "CM03 - Side Dolly", "CM04 - Corner Pan" };
        Vector3[] offsets = { new Vector3(4, 9, 105), new Vector3(7, 6, -36), new Vector3(-24, 3.5f, 0), new Vector3(-42, 7, 0) };
        float[] fov = { 28, 48, 52, 48 };
        for (int i = 0; i < 4; i++)
        {
            var child = new GameObject(names[i]); child.transform.SetParent(root.transform, false);
            var cm = child.AddComponent<CinemachineCamera>();
            cm.OutputChannel = OutputChannels.Channel04;
            cm.Priority = i == 0 ? 100 : 0;
            cm.Lens.FieldOfView = fov[i]; cm.Lens.NearClipPlane = .3f; cm.Lens.FarClipPlane = 12000;
            var shot = child.AddComponent<RaceCinematicShot>();
            shot.kind = (RaceCinematicShot.ShotKind)i; shot.cameraRig = cm; shot.offset = offsets[i];
            director.shots[i] = shot;
        }
        Upgrade(director);
        return director;
    }

    public static void Upgrade(RaceCinematicDirector director)
    {
        director.gameObject.name="RACE CINEMACHINE - 6 Race Shots";
        var shots=new System.Collections.Generic.List<RaceCinematicShot>(director.shots);
        while(shots.Count<6)
        {
            int index=shots.Count;
            var go=new GameObject(index==4?"CM05 - Red Engine Deployment":"CM06 - Overhead Checkpoint");
            go.transform.SetParent(director.transform,false);
            var cm=go.AddComponent<CinemachineCamera>();cm.OutputChannel=OutputChannels.Channel04;
            cm.Lens.FieldOfView=index==4?42:52;cm.Lens.NearClipPlane=.2f;cm.Lens.FarClipPlane=12000;
            var shot=go.AddComponent<RaceCinematicShot>();shot.cameraRig=cm;
            shot.kind=(RaceCinematicShot.ShotKind)index;
            shot.offset=index==4?new Vector3(4.5f,4.2f,-7):new Vector3(14,23,-12);
            shot.aimHeight=index==4?.7f:1.5f;shot.rotationResponse=index==4?.1f:.18f;
            shot.panRoadDistance=3150;shots.Add(shot);
        }
        director.shots=shots.ToArray();
        for(int i=0;i<director.shots.Length;i++)
        {
            var shot=director.shots[i];
            var forces=shot.GetComponent<RaceCameraForces>();
            if(forces!=null)continue;
            forces=shot.gameObject.AddComponent<RaceCameraForces>();forces.director=director;forces.shot=shot;forces.seed=3+i*7;
            switch(shot.kind)
            {
                case RaceCinematicShot.ShotKind.PackFront:
                    forces.enginePosition=.003f;forces.engineRotation=.035f;forces.roadPosition=.015f;forces.roadRotation=.065f;forces.windRotation=.09f;forces.deploymentKick=0;break;
                case RaceCinematicShot.ShotKind.PairRear:
                    forces.engineFrequency=15;forces.engineRotation=.11f;forces.roadPosition=.04f;forces.roadRotation=.22f;forces.windRotation=.28f;break;
                case RaceCinematicShot.ShotKind.SideDolly:
                    forces.engineFrequency=19;forces.engineRotation=.09f;forces.roadSpatialFrequency=.085f;forces.roadPosition=.025f;forces.roadRotation=.16f;forces.windPosition=.045f;forces.windRotation=.38f;forces.deploymentKick=.12f;break;
                case RaceCinematicShot.ShotKind.RedBoosterRear:
                    forces.engineFrequency=21;forces.enginePosition=.018f;forces.engineRotation=.24f;forces.roadPosition=.045f;forces.roadRotation=.26f;forces.windRotation=.24f;forces.deploymentKick=.85f;break;
                default:
                    forces.enginePosition=forces.engineRotation=forces.roadPosition=forces.roadRotation=forces.deploymentKick=0;
                    forces.windFrequency=shot.kind==RaceCinematicShot.ShotKind.OverheadCheckpoint?4.1f:3.2f;
                    forces.windPosition=.018f;forces.windRotation=shot.kind==RaceCinematicShot.ShotKind.OverheadCheckpoint?.28f:.18f;
                    forces.pressureRange=shot.kind==RaceCinematicShot.ShotKind.OverheadCheckpoint?32:48;break;
            }
        }
    }

    [MenuItem("Tools/Trailer/Install or Upgrade Race Cinemachine Shots")]
    public static void Install()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("RH_racing_Autonomous.unity") || EditorApplication.isPlaying)
            throw new System.InvalidOperationException("Open RH_racing_Autonomous in Edit mode.");
        AutonomousRaceController race = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var existing=root.GetComponentInChildren<RaceCinematicDirector>(true);
            if(existing!=null)
            {
                Undo.RegisterFullObjectHierarchyUndo(existing.gameObject,"Upgrade race cinematography");
                Upgrade(existing);EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=existing.gameObject;return;
            }
            var candidate = root.GetComponentInChildren<AutonomousRaceController>(true);
            if (candidate != null) race = candidate;
        }
        if (race == null) throw new System.InvalidOperationException("Race controller is missing.");
        Undo.RecordObject(race, "Install race cinematography");
        var rig = Build(race);
        Undo.RegisterCreatedObjectUndo(rig.gameObject, "Install race cinematography");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = rig.gameObject;
    }
}
