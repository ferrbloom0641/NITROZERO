# RH_boost: V1 / V2 병렬 스플라인 주행

## 사용

`Assets/01RAEHYEON/RHScenes/RH_boost.unity`를 열고 Play하면 두 차가 함께 출발한다. Game 화면을 클릭하고 **Space**를 누르면 두 차의 기존 핸들·카메라 부스터 연출이 시작된다.

- 새 프리팹: `Assets/01RAEHYEON/Prefab/BoostRun_V1_Red.prefab`
- 새 프리팹: `Assets/01RAEHYEON/Prefab/BoostRun_V2_Blue.prefab`
- 기존 `Animation_RedCarCockpit_fix`, `Animation_BlueCarCockpit` 및 `RH_insidecockpit`은 변경하지 않았다.

새 프리팹은 최근 보정이 저장된 RH_insidecockpit의 차량에서 추출했다. 양쪽 모두 손 IK, 핸들 부스터, 내부 카메라 12개와 해당 부스터 연결을 포함한다. 루트의 이름/활성 상태/배치 좌표를 정리하고 내부 Transform과 Timeline 바인딩은 유지했다. Blue의 기존 CAM_04_Booster에 남아 있던 끊긴 TrackingTarget 참조는 새 프리팹 안의 버튼 타깃으로 연결했다.

## 씬 구성과 배치

```
BoostRun_RoadSpline
BoostRun_ParallelFormation
├─ BoostRun_V1_Red  (연결된 프리팹 인스턴스)
├─ BoostRun_V2_Blue (연결된 프리팹 인스턴스)
└─ CAM_BoostRun_TwoCarFollow
```

기존 Asphalt Road Navigation 아래의 실제 도로 메시와 그 부모 회전을 기준으로 확인했다. 도로는 월드 X 방향으로 6,000m, 폭 30m, 표면 높이 Y=-23.65다.

- 도로 중심 스플라인: `(2940, -23.65, 0)` → `(-2940, -23.65, 0)`, 총 5,880m. 양 끝 60m 여유.
- 차량 중심 간격: 12m, 스플라인 기준 좌우 6m. 같은 진행 위치에 배치.
- 차량 폭은 각각 약 9.97m이므로 차 사이 약 2.03m, 도로 양 가장자리까지 약 4.01m의 차체 여유가 있다.
- 원본 차량 스케일을 유지했다. 바퀴 메시 최저점과 노면 사이 0.02m가 되도록 차량 루트 높이를 설정했다. 원본 네 바퀴의 높이 차이는 유지한다.
- 두 차 모두 -X 방향을 바라본다.

## 이동 조정

`BoostRun_ParallelFormation`의 기존 `Spline Formation Controller`에서 조정한다.

| 항목 | 설정 |
|---|---:|
| Target Speed | 45m/s (162km/h) |
| Acceleration | 12m/s² |
| Deceleration | 15m/s² |
| Play On Start | 켬 |
| Loop | 끔: 경로 끝에서 정지 |
| Follow Rotation | 켬 |

공통 부모가 움직이므로 두 차가 따로 가속하면서 앞뒤로 벌어지지 않는다. 차 간격은 각 프리팹 인스턴스의 Local Position X(-6 / +6)를 조정한다. Local Position Y는 접지 높이이므로 바꾸면 도로 접촉 높이도 달라진다.

스플라인은 Unity Splines의 Spline Container이며 7개의 편집 가능한 매듭이 있다. 현재 직선 도로에 맞춘 직선 경로다. 이후 커브로 바꿀 경우 차폭과 내부/외부 차선의 도로 이탈 여부를 다시 확인해야 한다.

## 카메라

기본 화면은 `CAM_BoostRun_TwoCarFollow`이며 두 차를 후방 대각선에서 함께 본다. 차량 부모를 따라 이동하며 우선순위 20으로 내부 카메라(10)보다 우선한다. 부스터 카메라 효과는 V1 인스턴스와 연결하고 Shot Intensity=0.65로 설정했다.

개별 콕핏 샷을 보려면 이 추적 카메라를 비활성화하고 원하는 차량의 내부 카메라 우선순위를 다른 활성 카메라보다 높인다. 두 차량의 내부 카메라 설정은 원본에서 보존했다.

부스터 강도·길이는 각 차량 루트의 `Cockpit Boost Feedback`에서 조정한다. 상세: [Cockpit_Boost_Feedback.md](Cockpit_Boost_Feedback.md).

변경 전 백업: `C:/UnityProject/NITROZEROtrailer/Backups/IKRecovery/20260917_BoostRoad`.

## 검증

- 새 프리팹의 모든 내부 참조와 차량별 카메라 Source 24개를 검사했다. RH_boost의 기존 448개 객체는 보존하고 SceneRoots에 새 루트 2개를 추가했다.
- 별도 Unity 6000.3.18f1에서 새 프리팹과 씬의 추가 부분을 그대로 불러와 연결된 프리팹 인스턴스임을 확인했다. 공용 카메라가 프리팹 자산이 아닌 실제 V1 인스턴스의 부스터를 참조하는 것도 확인했다.
- 실제 외장 메시를 가져와 631 Play 프레임 동안 병렬 간격, 진행 방향, 30m 도로 안 배치, 바퀴 최저점의 0.02m 여유, 부스터 동시 동작, 경로 끝 정지/재시작/일시정지를 검사했다. 최대 접지 계산 오차 약 0.000004m, 간격 오차 약 0.000002m.
- 공용 카메라가 우선순위를 갖고 두 차체의 경계가 카메라 화면 안에 들어가는 것을 투영 좌표로 확인했다.

검증 프로젝트에는 외장 메시만 가져왔으므로 캐릭터 Animator/손 리그/Timeline은 해당 배치 검증 동안만 비활성화했다. 실제 새 프리팹의 설정은 원본에서 유지했다. 전체 RH_boost의 재질·조명을 포함한 최종 화면 시각 검수는 미실시다. 기존 부스터와 IK의 별도 실행 결과는 Cockpit_Boost_Feedback 문서에 기록돼 있다.

결과 파일: 작업공간 루트의 `tools/IKSteeringValidation/boost-road-validation-result.txt`, `tools/IKAudit/boost-road-asset-validation.txt`.
