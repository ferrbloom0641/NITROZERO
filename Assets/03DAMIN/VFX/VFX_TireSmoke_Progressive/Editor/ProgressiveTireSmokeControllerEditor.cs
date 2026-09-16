using UnityEditor;
using UnityEngine;
using Damin.VFX.TireSmoke.Progressive;
using System;
using System.Linq;

[CustomEditor(typeof(ProgressiveTireSmokeController))]
public sealed class ProgressiveTireSmokeControllerEditor : Editor
{
    const string PrefabPath="Assets/Prefabs/VFX/PF_TireSmoke_Progressive.prefab";
    [MenuItem("GameObject/NITRO ZERO/Add Tire Smoke Rig",false,20)]
    static void AddRigToSelectedVehicle()
    {
        Transform vehicle=Selection.activeTransform;if(!vehicle){EditorUtility.DisplayDialog("Tire Smoke","Hierarchy에서 차량 루트를 선택해 주세요.","확인");return;}
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);if(!prefab){Debug.LogError("Tire Smoke prefab not found: "+PrefabPath);return;}
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,vehicle.gameObject.scene);Undo.RegisterCreatedObjectUndo(instance,"Add Tire Smoke Rig");
        Undo.SetTransformParent(instance.transform,vehicle,"Parent Tire Smoke Rig");instance.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);instance.name="TireSmokeRig";
        var controller=instance.GetComponent<ProgressiveTireSmokeController>();controller.vehicleRoot=vehicle;AutoBind(controller);
        // Do not replace the current Inspector target here. Unity can rebuild prefab-instance
        // objects during the same event, leaving GameObjectInspector with a destroyed target.
        EditorGUIUtility.PingObject(instance);
    }

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("독립 Progressive 버전입니다. 기존 Burnout/Final은 변경하지 않습니다.\n연기 부모에 붙이는 스크립트이며 뒷바퀴 두 개만 연결합니다. 연결하지 않으면 현재 위치에서 단독 연기를 미리 봅니다.",MessageType.Info);
        serializedObject.Update();
        EditorGUILayout.LabelField("트레일러 시간표 / Play 또는 시작 버튼 기준",EditorStyles.boldLabel);
        string[] timing={"previewOnPlay","sequenceStartDelay","previewBuildTime","previewHoldTime","previewFadeTime","previewRamp","loopPreview","previewRestTime"};
        string[] labels={"Play 시작 시 자동 실행","연기 시작 지연 (초)","연기가 커지는 시간 (초)","최대 연기 유지 (초)","발산 감소 시간 (초)","증가 곡선","반복 재생","반복 전 대기 (초)"};
        for(int i=0;i<timing.Length;i++)EditorGUILayout.PropertyField(serializedObject.FindProperty(timing[i]),new GUIContent(labels[i]));
        EditorGUILayout.Space();DrawPropertiesExcluding(serializedObject,"m_Script","previewOnPlay","sequenceStartDelay","previewBuildTime","previewHoldTime","previewFadeTime","previewRamp","loopPreview","previewRestTime");
        serializedObject.ApplyModifiedProperties();var c=(ProgressiveTireSmokeController)target;
        float a=c.sequenceStartDelay,b=a+c.previewBuildTime,d=b+c.previewHoldTime,e=d+c.previewFadeTime;
        EditorGUILayout.HelpBox($"{a:0.0}s 생성 시작 → {b:0.0}s 최대 강도 → {d:0.0}s 감소 시작 → {e:0.0}s 새 연기 생성 종료\n남아 있는 연기는 그 이후에도 수명에 따라 소멸합니다. 시간표 재생 중에는 수동 Build Up/Fade Out 지연을 중복 적용하지 않습니다.",MessageType.Info);
        EditorGUILayout.LabelField("연출 경과 시간",c.SequenceTime.ToString("0.00")+" s");
        EditorGUILayout.LabelField("실제 연기 강도",c.CurrentPower.ToString("0.00"));
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("차량에 빠르게 설치",EditorStyles.boldLabel);
        if(GUILayout.Button("뒷바퀴 자동 연결 + 접지 앵커 만들기"))AutoBind(c);
        if(GUILayout.Button("왼쪽 접지점 위치 확인")&&c.rearLeft.groundContactPoint)EditorGUIUtility.PingObject(c.rearLeft.groundContactPoint);
        if(GUILayout.Button("오른쪽 접지점 위치 확인")&&c.rearRight.groundContactPoint)EditorGUIUtility.PingObject(c.rearRight.groundContactPoint);
        using(new EditorGUI.DisabledScope(!Application.isPlaying)){
            if(GUILayout.Button("트레일러 연출 시작 / 시간표 재시작"))c.StartPreview();
            if(GUILayout.Button("발산 중지 / 남은 연기 자연 소멸"))c.StopPreview();
            if(GUILayout.Button("뒷바퀴 연결 다시 적용"))c.RebuildBindings();
        }
        EditorGUILayout.HelpBox("Play에서 Smoke Power를 0~1로 조절하세요. Build Up/Fade Out은 반응 시간, Layers의 Response는 레이어별 증가 곡선입니다. 미리보기 실행 중에는 자동 재생이 Smoke Power를 제어합니다.\nUse Slip Amount를 켜면 외부 입력 Slip Amount도 곱합니다. 속도/슬립/접지는 자동 측정하지 않습니다. 차체 Collision/SDF 및 이동 후 World-space 잔류 연기는 아직 포함하지 않았습니다.",MessageType.None);
        if(Application.isPlaying)Repaint();
    }

    void OnSceneGUI()
    {
        var c=(ProgressiveTireSmokeController)target;Handles.color=new Color(.75f,.82f,.9f,.9f);
        DrawContact(c.rearLeft);DrawContact(c.rearRight);
    }
    static void DrawContact(ProgressiveTireSmokeController.WheelBinding b)
    {
        if(b==null||!b.groundContactPoint)return;float size=HandleUtility.GetHandleSize(b.groundContactPoint.position)*.08f;
        Handles.DrawWireDisc(b.groundContactPoint.position,Vector3.up,size);Handles.DrawLine(b.groundContactPoint.position,b.groundContactPoint.position+Vector3.up*size*2);
    }

    static void AutoBind(ProgressiveTireSmokeController c)
    {
        Transform root=c.vehicleRoot?c.vehicleRoot:FindVehicleRoot(c.transform);
        if(!root){EditorUtility.DisplayDialog("Tire Smoke","차량 루트를 먼저 Vehicle Root에 연결해 주세요.","확인");return;}
        Transform left=Find(root,"RL_Rolling","RMCar26WheelRearLeft","RL wheel","RL");
        Transform right=Find(root,"RR_Rolling","RMCar26WheelRearRight","RR wheel","RR");
        if(!left||!right){EditorUtility.DisplayDialog("Tire Smoke","후륜 좌/우를 모두 찾지 못했습니다. Vehicle Root가 차량 최상위인지 확인해 주세요.","확인");return;}
        Transform leftFrame=Find(root,"RL_Steering","RMCar26WheelRearLeft","RL wheel","RL")??left.parent;
        Transform rightFrame=Find(root,"RR_Steering","RMCar26WheelRearRight","RR wheel","RR")??right.parent;
        Undo.RecordObject(c,"Auto Bind Tire Smoke");c.vehicleRoot=root;
        Bind(c.rearLeft,left,leftFrame,"RL");Bind(c.rearRight,right,rightFrame,"RR");
        c.previewOnPlay=false;EditorUtility.SetDirty(c);
        if(PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        Debug.Log($"Tire Smoke: {root.name}의 후륜 연결과 접지 앵커를 만들었습니다.",c);
    }
    static Transform FindVehicleRoot(Transform from)
    {
        for(Transform p=from.parent;p;p=p.parent)if(Find(p,"RL_Rolling","RMCar26WheelRearLeft","RL wheel","RL")&&Find(p,"RR_Rolling","RMCar26WheelRearRight","RR wheel","RR"))return p;
        return null;
    }
    static Transform Find(Transform root,params string[] names)
    {
        var all=root.GetComponentsInChildren<Transform>(true);
        foreach(string wanted in names){string key=wanted.Trim();var found=all.FirstOrDefault(t=>string.Equals(t.name.Trim(),key,StringComparison.OrdinalIgnoreCase));if(found)return found;}
        return null;
    }
    static void Bind(ProgressiveTireSmokeController.WheelBinding binding,Transform wheel,Transform frame,string side)
    {
        binding.wheel=wheel;binding.directionReference=frame?frame:wheel.parent;
        var renderers=wheel.GetComponentsInChildren<Renderer>(true).Where(r=>!(r is ParticleSystemRenderer)).ToArray();
        Bounds bounds=new Bounds(wheel.position,Vector3.one*.4f);if(renderers.Length>0){bounds=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);}
        binding.wheelRadius=Mathf.Max(.05f,bounds.extents.y);
        Vector3 axle=(binding.directionReference?binding.directionReference:wheel).right.normalized, e=bounds.extents;
        binding.wheelWidth=Mathf.Max(.05f,2*(Mathf.Abs(axle.x)*e.x+Mathf.Abs(axle.y)*e.y+Mathf.Abs(axle.z)*e.z));
        Transform parent=binding.directionReference?binding.directionReference:wheel.parent;
        binding.centerPoint=GetAnchor(parent,"TireSmoke_Center_"+side,bounds.center);
        binding.groundContactPoint=GetAnchor(parent,"TireSmoke_Contact_"+side,new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
        binding.wheelAxis=Vector3.right;binding.grounded=true;
    }
    static Transform GetAnchor(Transform parent,string name,Vector3 worldPosition)
    {
        Transform anchor=parent.Find(name);if(!anchor){var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create Tire Smoke Anchor");anchor=go.transform;Undo.SetTransformParent(anchor,parent,"Parent Tire Smoke Anchor");}
        Undo.RecordObject(anchor,"Place Tire Smoke Anchor");anchor.position=worldPosition;anchor.rotation=parent.rotation;anchor.localScale=Vector3.one;return anchor;
    }
}
