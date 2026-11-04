# 운전자 IK · Timeline 작업 통합 기록

작성일: 2026-09-17

대상: NITROZERO 트레일러의 v1·v2·v3 운전자, 차량 내부 조작 및 촬영용 Timeline.

이 문서는 대화에서 사용자가 확정한 요구, 기존 `RH_boostOn_DriverIK_WorkPlan.md`, 작성 시점의 파일 확인 결과를 취합한다. 이전 assistant의 ‘완료’, ‘정상 복원’, ‘원인 확인’ 발언만으로 정상 동작을 인정하지 않는다. 이 문서 작성으로 Unity 씬·프리팹·스크립트·애니메이션을 수정하지 않았다.

## 1. 가장 중요한 보존 규칙

- 사용자 수동 조정 Position·Rotation·Scale, 캐릭터 배율, 좌석 배치는 임의 변경 금지.
- 사용자가 전달한 회전값을 누락하지 않는다. Position·Rotation·Scale을 각각 확인한다.
- 기존 IK 위치·회전 키는 사용자가 만든 이동 경로와 접촉 자세다. 충돌 가능성만으로 삭제하거나 Blend 곡선으로 교체하지 않는다.
- 기어봉이 타이밍 기준이다. 오른손을 수정하라는 요청으로 기어봉·페달까지 수정하지 않는다.
- 기본자세, 접촉 위치, 손목 보정, 손 이동 타이밍을 구분해서 확인한다.
- 공유 스크립트 변경은 v1·v2 및 다른 동작에도 영향을 줄 수 있다. 한 동작의 문제를 고치면서 전체 손목 영향도를 일괄 변경하지 않는다.
- 씬 인스턴스, 프리팹 원본, Timeline 에셋은 별개다. 실제 사용 중인 대상과 Overrides를 확인한다.
- Mute된 트랙은 활성 트랙과 구분한다. 같은 Animator에 연결됐다는 사실만으로 충돌 원인이라고 단정하지 않는다.
- 파일 비교와 Play 검증을 구분한다. 실행 화면을 확인하지 않았다면 ‘정상 동작 검증 완료’라고 보고하지 않는다.
- 기존 문서에 명시된 재발 방지: 외부 Cockpit Target을 Constraint에 직접 연결하라고 두 번 잘못 안내했다. 본 프로젝트에서는 Rig 내부 Target을 사용한다.

## 2. 차량별 역할과 사용자 확인 이력

| 버전 | 차량/작업 대상 | 연출 범위 | 확인 수준 |
|---|---|---|---|
| v1 | Final_RedCarCockpit, 이후 Animation_RedCarCockpit 및 fix 계열 | 기어 변경, 부스터 레버, 드리프트, 권총 조준, 고개 회전 | 사용자가 초기 동작 완성을 확인했으나 이후 기어 변경 수정에서 오른팔 문제가 반복됨 |
| v2 | 파란 차량, v2_Driver | 기어 변경, 빨간 버튼 누르기, 오른쪽 보기·끄덕임 | 사용자가 손발 접점을 맞추고 Timeline 작업을 진행함. 현재 실행 상태는 재검증 필요 |
| v3 | 초록 차량 | 양손 핸들 유지, 왼쪽 보기·끄덕임 | 팔 IK 작업 및 시선 연출 진행 이력 있음. 현재 실행 상태는 재검증 필요 |

최초 작업 씬은 RH_boostOn. 이후 RH_avoidMissile, RH_insidecockpit 등에서 촬영 작업이 이어졌다. 최근 기록의 저장된 RH_insidecockpit은 fix 프리팹을 참조했으며, 이를 현재 열린 씬의 상태로 단정하지 않는다.

사용자가 운전자 크기를 여러 번 직접 맞췄다. ‘6.5배’는 작업 이력일 뿐 현재 적용할 절대 Scale 값이 아니다. 현재 값을 보존한다.

## 3. IK 계층과 역할

본 프로젝트의 연결 원칙:

```text
Cockpit
├─ 좌석 기준점 → Driver (Animator, Rig Builder)
│  └─ DriverRig (Rig)
│     ├─ RightArmIK / LeftArmIK
│     ├─ RightHandIKTarget / LeftHandIKTarget
│     ├─ RightElbowHint / LeftElbowHint
│     ├─ RightLegIK / LeftLegIK
│     └─ 발 IK Target 및 무릎 Hint
└─ 조작계 Pivot 또는 움직이는 손잡이
   └─ 외부 접촉 Target / GripTarget
```

위 구조는 역할 설명이다. 기존 실제 오브젝트를 이 모양으로 임의 재배치하지 않는다. 타겟들을 서로의 자식으로 연속 배치하라는 뜻도 아니다.

- Two Bone IK의 Root/Mid/Tip은 해당 팔 또는 다리의 본이다.
- Target은 DriverRig 내부의 IKTarget을 사용한다.
- Cockpit의 외부 Target은 내부 Target이 추적할 접촉 기준이다.
- Hint는 팔꿈치/무릎의 굽힘 방향을 정한다. 손발 접촉 위치를 대신 지정하는 오브젝트가 아니다.
- 외부 GripTarget은 손잡이가 회전할 때 함께 움직이는 계층에 둔다.
- 회전 Pivot은 기계 부품의 회전축에, GripTarget은 손이 실제 닿는 위치에 둔다. 두 기준점은 다르다.

v1의 기록상 주요 이름:

| 역할 | 오브젝트 |
|---|---|
| 외부 핸들 접점 | v1_RightHandTarget, v1_LeftHandTarget |
| 내부 손 IK | v1_RightHandIKTarget, v1_LeftHandIKTarget |
| 팔 Constraint | v1_RightArmIK, v1_LeftArmIK |
| 시프터 접점 | v1_ShifterGripTarget |
| 부스터 접점 | v1_BoosterGripTarget |
| 핸드브레이크 접점 | v1_HandbrakeGripTarget |
| 권총 접점 | v1_GunGripTarget |
| 외부 발 접점 | v1_RightFootTarget, v1_LeftFootTarget |
| 내부 발 IK | v1_RightFootIKTarget, v1_LeftFootIKTarget |
| 시선 | V1_HeadAim, v1_WindowAimTarget |

## 4. Pivot 구성

사용자 배치 완료 보고:

- v1_SteeringPivot
- v1_BoosterPivot
- v1_AccelPivot
- v1_ClutchPivot

Ctrl_Handbrake_Lever와 Ctrl_Shifter_Lever는 기존 피봇이 맞아 별도 피봇을 만들지 않았다.

부모를 바꾸거나 Pivot을 추가하면 로컬 좌표 기준이 바뀔 수 있다. 기존 Timeline의 Transform 곡선과 Offset을 함께 검토해야 한다. 기존 키가 있는데 위치를 옮기면 재생 시 기록된 위치로 돌아갈 수 있다.

## 5. 기본 주행과 핸들 추적

사용자 확정 기본자세:

- 양손은 핸들을 잡는다.
- 오른발은 엑셀을 밟는다.
- 왼발은 클러치 위에서 대기하며 클러치를 밟지 않는다.
- 핸들의 미세 회전에 양손 접점이 함께 움직여야 한다.

DriverIKTargetFollower는 외부 기준점을 내부 Target이 추적하는 용도로 기록돼 있다. 발 작업에서는 사용자가 맞춘 접촉 간격을 보존하기 위해 고정 Offset 방식과 `Lock Current Offset For Future Plays` 작업이 제안됐다. 현재 값과 적용 여부를 확인한 뒤 사용하며 새 Offset을 임의로 덮어쓰지 않는다.

DriverHandTargetBlend의 기록상 필드:

| 기준/동작 | Source | Blend |
|---|---|---|
| 핸들 | Steering Source | 다른 동작이 0일 때의 기준 |
| 기어 | Shifter Source | Shifter Blend |
| 부스터 | Booster Source | Booster Blend |
| 핸드브레이크 | Handbrake Source | Handbrake Blend |
| 권총 | Gun Source | Gun Blend |
| 버튼 | Button Source | Button Blend |

오른손에 직접 기록한 Transform 곡선과 Blend 추적이 어떤 순서로 적용되는지 실제로 확인해야 한다. 둘이 자동으로 합성된다고 보장할 수 없다. 기본주행 추적과 사용자가 만든 이동 키를 함께 살리려면 구간별 제어 주체 또는 별도 보정 타겟 구조를 검증해야 한다. 아직 검증된 최종 해결책으로 기록하지 않는다.

기존 스크립트는 내부 손 자세와 Steering Source 사이 Offset을 캡처해 기본자세를 보존했다. 외부 Target의 위치·회전을 그대로 복사하는 방식으로 바꾸면 사용자 보정이 사라질 수 있다.

## 6. Timeline 공통 원칙

- 사용자에게 타이밍은 Timeline의 눈금 번호로 안내한다.
- 눈금이 프레임인지, Timeline Frame Rate가 무엇인지 먼저 확인한다.
- 현재 확인한 v1_GearShift_New는 60fps다. 이 에셋에서 60번=1초, 180번=3초다. 모든 에셋에 30fps를 가정하지 않는다.
- Timeline 에셋의 트랙과 씬 Playable Director의 바인딩을 함께 확인한다.
- 녹화는 해당 트랙에 바인딩된 오브젝트/속성을 대상으로 한다. Animator가 필요했던 조작계에 연결했던 이력이 있다.
- Preview/녹화/Play에서 값이 평가되면 Inspector 수동 변경이 덮어써질 수 있다. 현재 평가 주체를 확인한다.
- Play에서 손발 접촉을 확인한 값을 Edit 상태에 보존하는 절차가 필요하다. Play 중 값이 자동 저장된다고 안내하지 않는다.
- 다른 동작 Timeline은 별도 Director/에셋으로 관리하고 컷에 맞게 호출하는 방향으로 작업했다.
- 공용 Timeline에서 다른 Director를 Control Track으로 호출하는 작업 이력이 있다. 하위 Director의 자동재생과 중복 평가 여부를 확인한다.
- 트랙 Mute, Play On Awake, Wrap Mode, Control Track의 제어 상태는 각각 다르다. 하나가 꺼졌다고 모든 재생이 중단됐다고 단정하지 않는다.

## 7. v1 기어 변경 — 최신 사용자 기준

기어봉 회전 기준은 **115번 하강 시작 → 150번 하강 완료**다. 오른손을 맞추려고 이 기준을 변경하지 않는다.

| 순서 | 엑셀 | 클러치 | 오른손/기어봉 |
|---|---|---|---|
| 기본 | 밟음 | 안 밟음 | 양손 핸들 |
| 변속 준비 | 점차 해제 | 점차 밟음 | 클러치를 다 밟을 타이밍에 손이 기어봉으로 이동 |
| 115번 직전 | 해제 | 완전히 밟음 | 손이 기어봉을 잡은 상태 확보 |
| 115~150번 | 해제 유지 | 밟음 유지 | 기어봉 하강, 손은 회전하는 접점을 따라감 |
| 150번 이후 | 다시 밟기 시작 | 동시에 해제 시작 | 기어봉 조작 완료 |
| 엑셀 완전 작동 무렵 | 완전히 밟음 | 해제 | 오른손이 기어봉에서 핸들로 이동 |
| 종료 | 밟음 | 해제 | 양손 핸들 기본자세로 복귀 |

‘교차’는 엑셀과 클러치를 반대 방향으로 전환하는 연출이다. 두 페달을 함께 밟거나 함께 떼는 동일 곡선으로 만들지 않는다. 전환 중 중간값이 존재하는 것과 두 페달을 완전히 함께 밟는 것은 구분한다.

115·150 외 최신 확정 프레임은 이 문서에서 새로 만들지 않는다. 기존 레버·페달 키를 읽고 손 접근/유지/복귀 구간을 맞춘다. 손이 순간이동하거나 기어봉보다 먼저 놓이는 동작은 사용자가 거부했다. 레버가 내려갈 때의 ‘덜컥’과 이후 자연스러운 핸들 복귀가 요구사항이다.

초기 작업표에는 손 40~60, 레버 60~75, 복귀 80~100 등이 남아 있다. 이는 이전 버전 이력이며 최신 115~150 기준에 덮어 적용하지 않는다.

## 8. v1 부스터

- 기본자세에서 오른손을 부스터 손잡이로 이동한다.
- v1_BoosterGripTarget은 움직이는 Ctrl_Booster_Grip을 따라야 한다.
- v1_BoosterPivot으로 앞쪽 끝까지 회전시키는 반달형 이동이다.
- 손잡이를 잡은 구간에는 손과 손목이 회전하는 접점에 맞아야 한다.
- 복귀 동작은 없다. 끝 자세를 유지한다.
- 손목 접촉 각은 GripTarget 기준으로 사용자가 보정했다.
- 초기 Target Rotation Weight 트랙이 Play에서 기대대로 반영되지 않아 Blend 스크립트가 회전 영향도를 제어하도록 작업한 이력이 있다. 전체 동작에 Rotation Weight=1을 강제하는 것은 사용자 확정 사항이 아니다.

## 9. v1 드리프트·권총

드리프트 순서:

1. 엑셀을 밟은 기본자세에서 엑셀 해제·클러치 작동으로 교차한다.
2. 오른손이 핸드브레이크 접점을 잡는다.
3. 핸드브레이크를 먼저 당긴다.
4. 이후 핸들을 운전자 기준 시계방향 90도까지 돌리는 것으로 최초 요청했다.
5. 복귀 없이 끝 자세를 유지한다.

이후 사용자가 핸들 과도 회전으로 손각도가 이상하다고 수정 요청했다. 따라서 90도를 현재 최종값으로 재적용하지 않는다. 현재 사용자 키를 기준으로 확인한다.

사용자는 드리프트를 180번에서 끝내고, 250번부터 권총 동작을 이어서 요청했다. 핸들 각을 유지한 채 오른손을 핸드브레이크에서 떼고 옆 권총을 뽑아 운전석 창문을 겨눈다. 고개도 조준 방향으로 움직인다.

기존 문서의 총 동작 계획: 300번까지 홀스터, 340번 중간 인출, 380번 창문 조준, 420번 유지. 사용자는 이후 전체 작업 완료와 머리 회전을 확인했지만 현재 파일의 키를 이 계획과 동일하다고 단정하지 않는다.

권총·손은 함께 움직여야 한다. v1_GunGripTarget, v1_GunMuzzleTarget, v1_WindowAimTarget을 사용한 이력이 있다. 시선 목표는 권총 자식으로 두지 않는 계획이었다. Multi-Aim의 축과 Source Weight를 확인하며 반대 방향 회전은 축 확인 없이 임의 보정하지 않는다.

## 10. v2·v3 작업

v2:

- v1의 구성 원리를 참고하되 손발 접점과 바인딩을 해당 운전자에 맞춘다.
- 기어 변경과 엑셀·클러치 연출을 작업했다.
- 부스터는 레버 당김이 아니라 Ctrl_SwitchPanel_Toggles의 빨간 스위치/버튼을 오른손으로 누르는 연출이다.
- 버튼 오브젝트 자체의 작동 없이 손을 올렸다가 누른 느낌을 요청했다.
- 오른쪽을 바라보고 한 번 끄덕인 뒤 정면으로 복귀한다.

v3:

- 양손 핸들 기본자세를 유지한다.
- 왼쪽을 바라보고 한 번 끄덕인 뒤 정면으로 복귀한다.

두 사람의 상대 순서: **v3가 먼저 바라봄 → v2가 바라봄 → v2가 먼저 끄덕임 → v3가 끄덕임 → v3가 v2보다 먼저 정면 복귀**. 숫자 프레임은 최종 확정 기록이 없어 임의 지정하지 않는다.

애니메이션 이식은 같은 이름만으로 완료되지 않는다. 각 Timeline 복사본의 Animator, Rig 본, 내부 Target, 외부 Source, 보정값, Director 바인딩을 확인한다. v1 로컬 Transform 키가 v2에서도 같은 접점이 된다고 보장하지 않는다. v2 자동 설치용 OtherDriverIKSetup은 사용자 요청으로 제거한 이력이 있다.

## 11. 핸들 미세 떨림과 차량 대형

기존 작업표의 SteeringIdleMotion 기록: 로컬 Y축, 최대 1.8도, 한쪽 진행 0.38초, 불규칙성 45%, 중단 전환 0.15초, 재개 0.3초. 이는 기록된 설정이며 최종 ‘공격적인’ 설정으로 검증된 값은 아니다. 실제 핸들 회전축을 확인한다.

Timeline Steering Override: 0은 미세 조향, 1은 Timeline 조향 우선이라는 구성 이력이 있다. 복귀 없는 드리프트는 마지막까지 Timeline 조향을 유지하는 계획이다.

차량 대형 부모를 스플라인으로 이동시키고 자식 차량의 대형 내 위치를 Timeline으로 조정하는 작업이 이어졌다. 차량 이동 중에도 핸들·손잡이와 내부 IK의 상대 접촉이 유지되는지 확인한다. 정지 차량에서만 맞는 자세로 촬영 준비 완료를 선언하지 않는다.

## 12. 과거 사용자 제공 수치 — 자동 적용 금지

아래는 당시 스크린샷/대화의 값이며 현재 정답값이 아니다. 이후 사용자 수정과 부모 변경이 있었다.

| 대상 | 기록 값 |
|---|---|
| v1_LeftHandIKTarget Rotation | (-0.385, -274.702, 92.127) |
| v1_RightKneeHint Position | (0.0721, 0.4104, 0.2473) |
| v1_RightKneeHint Rotation / Scale | (0,0,0) / (1,1,1) |
| v1_RightFootIKTarget Position | (0.0606, 0.2896, 0.3295) |
| v1_RightFootIKTarget Rotation | (-106.182, 8.735001, 165.185) |
| v1_RightFootIKTarget Scale | (51.53846, 51.53843, 51.53844) |

## 13. 현재 문제와 잘못된 변경 기록

최근 실제 변경 이력:

1. fix_v2 프리팹의 Director를 v1_GearShift_v2에서 v1_GearShift_New로 변경했다. 이것만으로 정상 백업 복원 또는 팔 문제 해결이 입증되지 않았다.
2. v1_GearShift_New의 Right Hand IK 클립을 다른 에셋의 Shifter Blend 곡선으로 교체해 사용자가 만든 위치·회전 키를 제거했다. 사용자가 명확히 항의한 잘못된 변경이다.
3. DriverHandTargetBlend를 Update/Offset 방식에서 LateUpdate/외부 Target 직접 복사 방식으로 바꾸고 손목 Rotation Weight를 1로 강제했다. 사용자 보정 보존과 Rig 평가 순서에 대한 실행 검증이 없었다.
4. 이후 ‘복구하겠다’는 발언은 있었지만 해당 발언 뒤 복구 도구 실행은 없었다. 발언만으로 복구됐다고 기록하지 않는다.

원인 주장 정정:

- 같은 Animator에 두 트랙이 연결됐다는 이유로 충돌을 확정했지만 당시 직접 Transform 트랙 하나는 Mute 상태였다. 그 연결만으로 원인을 확정할 수 없다.
- 파일 내용이 fix와 같다는 사실은 fix 자체가 정상임을 증명하지 않는다.
- LateUpdate로 옮겼다는 사실만으로 같은 프레임의 Rig 평가에 올바르게 반영됨을 보장하지 않는다.
- 사용자 키와 Blend가 함께 필요하다는 요구와, 현재 구현이 둘을 안전하게 합성한다는 사실은 구분해야 한다.

작성 시점 읽기 확인:

- v1_GearShift_New.playable: 49,763바이트, Frame Rate=60, Right Hand IK 트랙 이름 및 shifterBlend 곡선 확인.
- DriverHandTargetBlend.cs: 기존 `Assets/01RAEHYEON/Scripts` 경로에서 찾을 수 없음. Assets 내 파일 목록 검색에서도 찾지 못함. 누가/언제 제거했는지, Unity 컴파일 상태는 확인하지 못함.
- DriverIKTargetFollower.cs, DriverHeadAimBlend.cs, SteeringIdleMotion.cs: Assets 파일 목록에서 확인.
- 아래 수정 전 백업 2개 존재 확인. 존재 여부가 정상 동작 보증은 아니다.

## 14. 관련 파일과 백업

프로젝트 루트: `C:/UnityProject/NITROZEROtrailer/NITROZERO`

- 기존 작업표: `Documentation/RH_boostOn_DriverIK_WorkPlan.md`
- 프리팹: `Assets/01RAEHYEON/Prefab/Animation_RedCarCockpit.prefab`
- 프리팹: `Assets/01RAEHYEON/Prefab/Animation_RedCarCockpit_fix.prefab`
- 프리팹: `Assets/01RAEHYEON/Prefab/Animation_RedCarCockpit_fix_v2.prefab`
- Timeline: `Assets/01RAEHYEON/Timeline/v1_GearShift.playable`
- Timeline: `Assets/01RAEHYEON/Timeline/v1_GearShift_New.playable`
- Timeline: `Assets/01RAEHYEON/Timeline/v1_GearShift_v2.playable`
- Timeline: `Assets/v2_GearShift.playable`

프로젝트 외부 작업 폴더의 백업(확인된 경로):

- `C:/UnityProject/NITROZEROtrailer/Backups/RightHandFollowFix/v1_GearShift_New_2026-09-17_before_right_hand_follow_fix.playable` — 65,699바이트. 위치·회전 키 교체 직전 백업.
- `C:/UnityProject/NITROZEROtrailer/Backups/RightHandFollowFix/DriverHandTargetBlend_2026-09-17_before_right_hand_follow_fix.cs` — 4,569바이트. LateUpdate/직접 추적 변경 직전 백업.
- `C:/UnityProject/NITROZEROtrailer/Backups/Animation_RedCarCockpit_before_GearShift_hold_2026-09-16.prefab` — 이전 턴에서 발견한 별도 프리팹 백업.

## 15. 촬영 전 남은 작업 — 완료로 처리하지 말 것

- [ ] 현재 열린 씬, 촬영 차량 인스턴스, 실행 Director와 Timeline 바인딩 확인.
- [ ] 현재 파일 및 사용자 새 수정분을 보존하고 수정 직전 백업과 대조.
- [ ] 삭제된 오른손 위치·회전 키와 현재 Blend 곡선의 차이를 확인. 복구 대상은 사용자 키 보존을 기준으로 결정.
- [ ] DriverHandTargetBlend 파일 부재 및 Unity의 Missing Script/컴파일 오류 여부 확인.
- [ ] 오른손을 쓰는 Timeline, Follower, Blend, Rig의 실제 평가 경로 확인.
- [ ] 기본자세에서 핸들 회전에 양손이 따라오는지 Play 확인.
- [ ] 115~150번 동안 기어봉 접점과 손·손목 유지 확인.
- [ ] 엑셀·클러치의 반대 전환과 사용자 페달 각도 보존 확인.
- [ ] 손의 접근/복귀가 순간이동하지 않는지, 종료 후 팔이 뒤로 돌아가지 않는지 확인.
- [ ] 차량 이동 및 촬영 카메라에서 확인.
- [ ] v1 부스터·드리프트·권총 및 v2 동작에 공유 코드 회귀가 없는지 확인.

진행 표시 원칙: 파일 변경, 정적 검사, Play 확인, 사용자 확인을 각각 기록한다. 현재는 촬영 준비 완료 상태로 판정할 수 없다.
