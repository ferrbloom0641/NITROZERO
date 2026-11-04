# 콕핏 IK 방해 요소 조사 — 2026-09-17

> 후속 변경: 같은 날 복구 1단계에서 `v1_GearShift_New`의 오른손 AnimationClip만 수정 직전 백업으로 복구했다. 아래는 복구 전 조사 기록이다. 이후 상태는 `IK_Recovery_Progress.md`를 따른다. Play 정상 여부는 아직 미검증이다.

## 조사 전제와 범위

- 기준: `IK_Timeline_Consolidated.md` 및 사용자의 추가 설명: **DriverHandTargetBlend는 기존 IK를 비틀어서 의도적으로 삭제했다. 복구하지 않는다.**
- 대상: `Animation_RedCarCockpit_fix`, `Animation_BlueCarCockpit`, 관련 Timeline, 저장된 `RH_insidecockpit` 씬. `fix_v2`, 원래 Red 프리팹, 다른 씬의 사용처도 교차 확인했다.
- 프로젝트: Unity 6000.3.18f1, Animation Rigging 1.4.1, Timeline 1.8.12.
- 이 보고서는 디스크의 직렬화 데이터와 실제 설치된 패키지 소스를 조사한 결과다. Unity Play/Preview 화면, 저장하지 않은 Inspector 상태, 현재 로드된 어셈블리는 확인하지 못했다. Editor.log도 읽지 못했으므로 컴파일 오류/콘솔 경고를 확인했다고 주장하지 않는다.
- 씬·프리팹·애니메이션·실행 스크립트는 수정하지 않았다. 조사 자료와 보고서만 추가했다.
- 아래의 ‘확정’은 파일/코드에서 확인했다는 뜻이다. 모든 시각적 증상의 실행 원인을 확정했다는 뜻은 아니다.

## 핵심 결론

현재 구성은 **기존 Rig 자체가 전부 끊긴 상태가 아니라, 오른손 동작이 삭제된 Blend 스크립트에 의존하는 상태로 남아 있는 것**이 가장 큰 문제다. 여기에 살아 있는 Follower의 Transform 쓰기, Timeline 타이밍, 손목 회전 Weight, 자동재생 및 씬별 설정 차이가 겹친다.

삭제된 Missing 컴포넌트가 지금도 실행되어 팔을 비트는 것은 아니다. 정상적인 재컴파일/리로드 이후에는 그 코드가 실행되지 않는다. 문제는 삭제 이전에 애니메이션과 추적 구성이 그 코드에 의존하도록 바뀌었고, 삭제만으로 기존 Transform 키나 추적 경로가 돌아오지 않는다는 점이다.

| 부위 | Red fix의 현재 제어 | Blue의 현재 제어 | 의미 |
|---|---|---|---|
| 오른손 | 활성 Follower → 핸들 기준점. Timeline은 삭제된 스크립트의 float만 기록 | Follower 없음. 삭제된 Blend 참조 + Animator만 존재 | Red는 핸들 추적이 남고, Blue는 조작계 추적 경로가 끊김 |
| 왼손 | 활성 Follower, 실행 시작 자세에서 Offset 캡처 | 동일 | Play 중 수동 Target 변경을 계속 유지하는 구조가 아님 |
| 오른발/왼발 | 활성 Follower, 저장 Offset 사용 | 활성 Follower, 시작 시 Offset 재캡처 | 두 차의 보정 보존 방식이 다름 |
| 머리 | DriverHeadAimBlend가 MultiAim Weight 제어 | MultiAim Weight 0.2 + 시선 Target Timeline | 팔 IK와 분리해서 판단해야 함 |

## 1. Red 오른손의 원래 Transform 키가 실제로 교체되어 있음 — 확정, 최우선

`Animation_RedCarCockpit_fix.prefab:6272`의 `v1_GearShift` Director는 `v1_GearShift_New.playable`을 사용한다. `Right Hand IK`는 Mute가 아니고 내부 `v1_RightHandIKTarget`의 Animator에 바인딩되어 있다.

그러나 해당 클립에는 다음 상태가 저장되어 있다.

- `v1_GearShift_New.playable:948`: 오른손 클립.
- 위치 곡선 `m_PositionCurves: []`, 회전 곡선 `m_EulerCurves: []`.
- `:1053`: `shifterBlend`만 애니메이션함.
- `:1057`: 삭제된 스크립트 GUID `e5f05e0071e7a20498ddcb5aaf0bf256`을 참조.

수정 직전 백업과 Unity 객체 단위로 비교하면 **변경된 객체는 오른손 AnimationClip 한 개(fileID -3590271882105414346)**다. 클러치·기어봉·엑셀 클립과 트랙 설정은 이 백업과 동일하다. 따라서 손 키 교체가 실제로 있었다는 것은 문서의 주장에만 의존하지 않고 확인했다.

백업의 오른손 클립에는 위치 X/Y/Z 각각 13키와 회전 X/Y/Z 각각 3키가 있다. 회전 키 값은 해당 클립에서 0이지만, 트랙의 회전 Offset이 별도로 있으므로 ‘손목 회전 정보가 없다’거나 ‘원점 자세’로 해석하면 안 된다.

백업: `C:/UnityProject/NITROZEROtrailer/Backups/RightHandFollowFix/v1_GearShift_New_2026-09-17_before_right_hand_follow_fix.playable:948`.

**영향:** 현재 Timeline은 기존 손 이동 경로를 재생하지 않는다. 스크립트를 삭제해도 그 이동 경로는 되살아나지 않는다.

**복구 시 주의:** 백업에는 3000번(50초) 유지 키도 있다. 키와 Offset을 보존하되 백업 전체를 덮어써서 정상 복원이라고 판단하면 안 된다. 타이밍/종료 길이까지 별도로 확인해야 한다.

## 2. 삭제 후 남아 있는 의존성 — 확정, 복원 대상 아님

Missing 컴포넌트 설정이 남은 위치:

| 위치 | 줄 | 남은 내용 |
|---|---:|---|
| Red fix 프리팹 | 10050 | steering/shifter/booster/handbrake/gun Source와 Blend |
| Blue 프리팹 | 10790 | steering/shifter/button Source와 Blend |
| RH_insidecockpit 씬 | 24737 | 씬 Blue 오른손의 같은 컴포넌트 |
| 원래 Red, fix_v2 프리팹 | 별도 참조 확인 | 동일 스크립트 GUID |

삭제된 스크립트 GUID에 의존하는 Timeline은 7개다.

| Timeline | 남아 있는 손 제어 방식 |
|---|---|
| v1_GearShift_New | shifterBlend; 현재 fix에서 사용 |
| v1_GearShift | shifterBlend; 직접 Transform 트랙도 있으나 그 트랙은 Mute |
| v1_GearShift_v2 | 위와 같은 구조, 직접 Transform 트랙 Mute |
| v1_Booster | boosterBlend, shifterBlend |
| v1_Drift | handbrakeBlend, gunBlend; 별도 headAimBlend는 살아 있는 다른 스크립트용 |
| Assets/v2_GearShift | shifterBlend |
| v2_BoosterSwitch | buttonBlend |

특히 부스터 레버/권총/버튼 접점의 자체 Transform 애니메이션은 남아 있으므로, **조작계는 움직이는데 손은 안 따라가는 현상**이 가능한 구성이다.

Missing 항목 정리와 손 이동 경로 복구는 다른 작업이다. Missing 항목만 지워도 손 동작은 돌아오지 않는다. 반대로 삭제한 DriverHandTargetBlend를 다시 넣는 것도 사용자의 해결 방향에 맞지 않는다.

## 3. DriverIKTargetFollower는 여전히 IK 위치·회전을 쓴다 — 확정

소스: `Assets/01RAEHYEON/Scripts/DriverIKTargetFollower.cs`.

- `:7` 실행 순서 -500.
- `:23` OnEnable에서 Offset을 캡처하거나 저장된 Source를 사용.
- `:35` Update.
- `:70` Source의 TransformPoint로 위치 계산.
- `:73` Source 회전 × Offset으로 회전 계산.
- `:77` 내부 Target에 SetPositionAndRotation.
- Play 중에만 쓰고, Edit에서는 Update가 반환한다.

Red fix의 왼손·오른손·양발 모두 Enabled=1, followPosition=1, followRotation=1이다. Blue는 왼손·양발만 이 컴포넌트가 있고 오른손에는 없다.

**직접적인 영향:** Play 중 내부 IK Target을 손으로 옮겨도 다음 Update에서 추적값으로 되돌아갈 수 있다. Red 오른손의 수동 기어 이동 경로와 핸들 추적을 함께 유지하려면 구간별로 어느 쪽이 제어하는지 정해야 한다. 현재 Follower에는 기어/부스터/권총 동작 구간을 인식하거나 양보하는 기능이 없다.

**단정하지 않은 부분:** Transform 키를 복구했을 때 매 프레임 최종적으로 Follower와 Animator 중 어느 결과가 Rig에 들어가는지는 실제 그래프 평가를 확인해야 한다. Follower가 무조건 최종 승자라고 주장하지 않는다.

Blue 오른손은 삭제된 Blend 외에 접점을 따라갈 Follower가 없으므로, 단순 삭제 이후 핸들/시프터/버튼의 상대 움직임을 전달할 경로가 없다. 저장된 내부 Target에 대한 TwoBoneIK 계산 자체와 외부 접점 추적을 구분해야 한다.

## 4. Red 손목은 Target 회전을 받지 않도록 저장되어 있음 — 확정

`Animation_RedCarCockpit_fix.prefab:12394`의 오른팔:

- Constraint Weight=1.
- Target Position Weight=1.
- **Target Rotation Weight=0** (`:12414`).

이 상태에서는 Target의 위치를 바꿔 팔을 움직여도 Target 회전만으로 손목 접촉 각이 적용되지 않는다. 현재 기어 Timeline에는 이 회전 Weight를 켜는 트랙도 없다.

Blue 오른팔은 Target Rotation Weight=1이다. 다른 세 사지 역시 두 프리팹에서 1이다.

Red 부스터에는 오른팔 Constraint 오브젝트의 별도 Animator를 통해 `m_Data.m_TargetRotationWeight`를 쓰는 트랙이 있다. 이것이 Driver Rig의 해당 프레임에 원하는 대로 전달되는지는 실행 검증 대상이다. 기존 문서에도 이 경로가 기대대로 작동하지 않았다는 이력이 있다.

**전체를 1로 변경하지 않는다.** 기어, 기본 주행, 부스터, 권총에서 원하는 손목 동작이 다르므로 동작별 판단이 필요하다.

## 5. 현재 기어 Timeline은 문서의 타이밍 요구와도 다르다 — 확정

현재/백업의 해당 Timeline은 60fps다. 아래는 키 자체를 읽은 값이다.

| 채널 | 현재 키 |
|---|---|
| 기어봉 X 회전 | 115번=0 → 130번=-3 → 150번=-10.017, 156번까지 유지 |
| 오른손 shifterBlend | 90번=1, 120번=1 → 135번=0.85 → 150번=0.6 → 180번=0 |
| 클러치 X 회전 | 61번=-18.16384, 130번까지 유지 → 149번=0 |
| 엑셀 X 회전 | 65번=-5.238, 121번까지 유지 → 165번=11.224 |

문서는 115~150번 동안 손 접촉과 페달 준비 상태를 유지하고, 150번 이후 페달 복귀를 시작하도록 명시한다.

- 현재 Blend 곡선을 다시 사용하면 손은 120번 이후부터 기어 접점에서 이탈하도록 계산된다. **지금은 삭제된 스크립트용 곡선이므로 이 곡선이 실제 손을 움직이고 있다는 뜻은 아니다.**
- 페달은 현재도 Transform 애니메이션이므로, 클러치 130번 이후/엑셀 121번 이후의 조기 복귀는 별도 문제다.
- 페달 각도의 물리적 ‘밟음/해제’ 방향은 현재 Pivot 배치에서 확인해야 하지만, 변환값의 복귀가 150번 이전에 시작한다는 사실은 확정이다.
- 이 페달 키는 손 키 교체 직전 백업과 동일하다. 최근 오른손 클립 교체가 페달까지 바꿨다고 주장하지 않는다.

기어봉 115~150번은 보존 기준이며, 이 조사에서 페달이나 기어봉 키를 변경하지 않았다.

## 6. 핸들 미세회전과 손 추적의 적용 시점이 다름 — 코드상 확정, 시각적 영향은 검증 필요

`SteeringIdleMotion.cs:43`은 LateUpdate에서 핸들을 돌리고, `DriverIKTargetFollower.cs:35`는 Update에서 손 Target을 갱신한다. 따라서 Follower의 그 프레임 Update는 아직 그 프레임 LateUpdate에서 추가될 핸들 회전을 읽을 수 없다.

Red fix의 SteeringIdleMotion은 maximumAngle=2.3, timelineSteeringOverride=0으로 켜져 있다 (`prefab:4927`). Blue 프리팹 원본에는 없지만 `RH_insidecockpit`의 Blue에는 추가되어 있고 같은 2.3/0 설정이다 (`scene:22888`).

- 핸들은 후반에 움직이고 손 Target은 앞선 상태를 기준으로 갱신되는 구조라 접점 지연/미세 미끄러짐 후보가 된다.
- Blue 오른손은 추적 경로 자체가 끊겨 있어 왼손과 증상이 다를 수 있다.
- 현재 `v1_Drift.playable`에는 `timelineSteeringOverride` 곡선이 없다. 따라서 해당 구성이 실행되면 Timeline 조향 중에도 미세 조향이 추가될 수 있다.
- 무조건 LateUpdate로 옮기면 해결된다고 판단하지 않는다. Rig 평가까지 포함해 순서를 맞춰야 한다.

현재 설치된 RigBuilder 소스는 Update에서 SyncLayers를 호출하며, RigLayer 소스에는 Animator의 그래프 평가 전에 동기화하는 취지가 명시되어 있다. 조사 근거는 추측성 실행 순서 설명이 아니라 현재 로컬 패키지 코드다.

## 7. 씬 상태와 프리팹 상태가 다름 — 확정

`Library/LastSceneManagerSetup.txt`에 저장된 활성 씬은 `RH_insidecockpit`이다. 현재 UI에서 열린 씬/저장하지 않은 상태라고 단정하지 않는다.

저장된 `RH_insidecockpit.unity`:

| 대상 | 저장 상태 |
|---|---|
| Red fix 루트 | m_IsActive=0 (`:23858`) |
| Red fix_v2 루트 | m_IsActive=0 (`:14967` 주변) |
| Red fix의 GearShift | Play On Awake=0 Override (`:23810`) |
| Blue | 활성, 주요 오브젝트/컴포넌트가 씬 자체에 직렬화됨 |
| Blue GearShift | Enabled=1, Play On Awake=0 (`:28452`) |
| Blue BoosterSwitch | Enabled=1, Play On Awake=1 (`:22152`) |
| Blue RightLookNod | Enabled=1, Play On Awake=1 (`:20867`) |
| Blue 기본 Driving Director | Enabled=0, Play On Awake=0 (`:27666`) |
| PathAnchor의 SplineController | Enabled=0 (`:6324`) |

따라서 저장 상태에서 Red fix의 ‘Play 눌러도 안 움직임’은 IK 수치 이전에 루트 비활성으로 설명할 수 있다. Blue는 Play만 하면 기어 대신 버튼/고개 연출이 자동 시작되는 구성이다.

Blue는 해당 프리팹 GUID를 참조하는 PrefabInstance 형태로 저장되어 있지 않고, 오른손 IK 등을 씬에 직접 가지고 있다. 프리팹 원본만 수정하면 현재 씬 Blue가 함께 바뀐다고 기대하면 안 된다. 비교한 v2_ Transform들의 Position/Rotation/Scale은 현재 원본과 같지만, Director 자동재생과 SteeringIdleMotion 존재 여부는 다르다.

Red fix에는 오른손 Target 로컬 Y를 0.5549997에서 **0.5271**로 바꾼 씬 Override도 있다 (`:23802`). 사용자 조정일 수 있으므로 잘못된 값으로 판정하거나 원본값으로 되돌리지 않는다.

다른 사용처:

- `RH_avoidMissile`: Blue 프리팹과 원래 `Animation_RedCarCockpit` 사용. fix가 아님.
- `RH_inside`: Blue 프리팹과 원래 Red 사용. fix가 아님.
- `RH_insidecockpit`: fix/fix_v2 인스턴스와 씬에 저장된 Blue.

## 8. 자동재생·다중 Animator·동작 유지 상태 — 확정 설정, 조건부 충돌

Blue **프리팹 원본**은 GearShift, BoosterSwitch, RightLookNod 세 Director 모두 Play On Awake=1이다. GearShift와 BoosterSwitch는 같은 오른손 Animator에 연결되지만 각각 `shifterBlend`, `buttonBlend`라는 다른 속성을 쓴다.

따라서 ‘같은 Animator이므로 같은 Transform을 동시에 덮어쓴다’고 단정하면 틀린다. 다만 순차 연출을 위한 설정은 아니며, 예전 Blend 구현이 다시 실행되는 환경이라면 기어/버튼 가중치가 동시에 켜져 섞이는 경로다. 삭제된 현 상태에서는 두 곡선의 손 추적 기능 자체가 없다.

Blue의 buttonBlend는 70~100번에 1이며 마지막도 1이다. Director WrapMode는 2(None)다. ‘끝까지 Hold가 설정되어 있다’고 해석하지 않는다. 종료 시 복원/연결된 다른 그래프의 영향은 Play에서 확인해야 한다.

Red에는 Driver 본체 Animator 외에 오른손 Target Animator, 오른팔 Constraint Animator 등이 있다. 서로 다른 그래프에서 Target/Constraint 속성을 평가하는 구조를 실행 검사해야 하지만, **Animator가 여러 개라는 사실만으로 고장 원인이라고 하지 않는다.**

## 9. 드리프트→권총 요구와 현재 핸들 키가 다름 — 확정

`v1_Drift.playable`의 핸들 Y 회전:

- 115번=0.
- 150번=54.589.
- 250번=54.589.
- **340번=0**, 420번=0.

문서는 핸들 각도를 유지한 채 250번부터 권총 동작을 이어 가는 요구다. 현재 키는 250~340번에 핸들을 정면으로 되돌린다. 따라서 ‘조준 중 왼손/핸들이 달라지는 현상’은 IK만의 문제가 아닐 수 있다.

54.589도를 90도로 바꾸거나 복귀 키를 삭제하지 않았다. 현재 키와 문서의 차이로 기록한다.

## 10. 통합 Timeline이 현재 씬 재생을 책임진다고 볼 근거가 없음 — 확정 범위

- `v1_InsideTake_Master.playable`은 `m_Tracks: []`인 빈 에셋이다.
- `v1_insidetake.playable`에는 Control Track과 ExposedReference가 있지만, Assets 아래 직렬화된 씬/프리팹에서 두 에셋 GUID를 사용하는 Director를 찾지 못했다.
- 따라서 현재 저장된 대상에서 ‘마스터 Timeline이 자동으로 각 동작을 순서대로 호출한다’고 볼 수 없다. 런타임 동적 할당까지 실행으로 검증한 것은 아니다.

## 원인으로 확정하지 않았거나, 정상 연결을 확인한 부분

1. **8개 사지의 Root→Mid→Tip 직접 부모 연결이 모두 맞다.** Target/Hint 참조도 존재한다.
2. **8개 Target이 모두 각 DriverRig 내부다.** 외부 Cockpit Target을 Constraint에 직접 연결한 오류는 이번 두 프리팹에서 확인하지 못했다.
3. **Rig 및 사지 Constraint Weight=1, Enabled=1.** IK가 통째로 Weight=0이라서 안 되는 상태는 아니다. 단, Red 오른손 Rotation Weight=0은 별개다.
4. Red RigBuilder의 첫 슬롯은 null이지만 두 번째는 유효한 DriverRig다. 설치된 RigLayer 코드가 null Rig를 유효하지 않은 레이어로 거르므로 빈 슬롯만으로 전체 실패라고 하지 않는다.
5. Red 양발 저장 Offset은 source/storedSource가 일치하고 hasStoredOffset=1이다. 저장 Transform으로 계산한 현재 자세와 추적 예상 위치 차이는 왼발 약 0.000079, 오른발 약 0.000114 Unity unit이며 회전도 수치 오차 범위에서 일치한다. **현재 파일에서 발 보정이 크게 틀어진 증거는 없다.** 움직이는 페달에서의 오차까지 검증한 결과는 아니다.
6. 저장 기본자세의 Root→Target 거리는 8개 모두 두 뼈 길이 합보다 짧다(비율 약 0.708~0.831). 기본 Target이 팔/다리의 최대 길이 밖에 있어서 무조건 늘어나는 경우는 아니다. Hint 특이점/관절 회전/애니메이션 중 도달성은 별도다.
7. 두 Driver Animator는 Enabled=1, Controller 없음, ApplyRootMotion=0, UpdateMode=0, CullingMode=0이다. 현재 연결된 AnimatorController가 팔 본을 덮는 증거는 없다. Controller 부재만으로 Rig 실패라고 하지 않는다.
8. Blue 기본 Driving Director는 비활성이다. 이를 현재 활성 IK와 충돌하는 재생원으로 세면 안 된다.
9. Follower/HeadAim/Steering은 Play 전용 코드다. Edit Preview와 Play의 차이는 구조상 가능하며, Preview가 맞는다고 Play 정상이라고 할 수 없다.
10. Assets의 C# 검색에서 DriverHandTargetBlend를 대체하는 다른 같은 클래스나 OnAnimatorIK/SetIKPosition/SetIKRotation 기반 운전자 제어를 찾지 못했다. 확인한 대상에 붙은 사용자 스크립트 중 주요 손발 쓰기 주체는 Follower다. 카메라 진동/지형 스크립트는 손발 IK 직접 제어로 판정하지 않았다.

## 수정 작업으로 이어갈 때의 우선순위

1. 수정 대상 씬 인스턴스와 프리팹을 분리한다. 저장된 Red 비활성 상태 및 Blue 자동재생을 기준으로 검증 대상을 특정한다.
2. 삭제한 DriverHandTargetBlend를 다시 넣지 않고, 기존 손 Transform 키/Offset과 실제 접점 추적 역할을 복원 가능한 자료에서 분리한다.
3. Red 오른손의 Follower와 직접 Transform 키가 같은 구간의 제어권을 두고 경쟁하지 않게 한다. Blue에는 기존 수동 보정을 보존하는 추적/직접 애니메이션 경로가 필요하다.
4. 회전 Weight는 기본자세·기어·부스터·권총별로 검증한다. 전체 손목 값을 일괄 변경하지 않는다.
5. 기어봉 115~150번 및 사용자 페달 각도를 보존하면서 문서와 다른 손/페달 복귀 타이밍을 구분한다. 손 수정이라는 이유로 페달까지 자동 변경하지 않는다.
6. 핸들/페달 평가 → 내부 Target → Rig 결과를 같은 프레임에서 확인한다. Update/LateUpdate 변경만으로 해결됐다고 하지 않는다.
7. 기본주행, 기어 115~150, 손 복귀, 부스터, 드리프트/권총, Blue 버튼/고개를 각각 Play에서 확인한 뒤 조합한다.

이 우선순위는 분석 결과에 따른 후속 작업 순서이며, 해당 수정들을 이미 수행했다는 뜻이 아니다.

## 재검토용 조사 자료

- `C:/UnityProject/NITROZEROtrailer/tools/IKAudit/details.txt`: Director 바인딩, 실제 대상 계층, Mute, 자동재생, 씬 Override.
- `C:/UnityProject/NITROZEROtrailer/tools/IKAudit/curves.txt`: 관련 Timeline 키의 60fps 프레임/값.
- `C:/UnityProject/NITROZEROtrailer/tools/IKAudit/prefabs.json`: 조사 프리팹 컴포넌트와 GUID/오브젝트 참조.
- `C:/UnityProject/NITROZEROtrailer/tools/IKAudit/geometry.cjs`: 저장 자세 기준 발 Offset/도달 거리 계산. Unity 실행 검사를 대체하지 않는다.
