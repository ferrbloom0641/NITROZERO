# V1 / V2 부스터 핸들·카메라 연출

## 사용

1. `RH_insidecockpit` 씬에서 촬영할 차량을 활성화한다. 저장된 선택은 V2 활성 / V1 비활성이다.
2. Play를 누르고 **Game 화면을 클릭한 다음 Space**를 누른다.
3. 즉시 핸들이 거칠게 튀고 반대 방향 보정과 작은 재반동이 이어진다. 카메라는 큰 충격에서 약 2.2초에 걸쳐 중심 근처의 빠른 진동으로 모인다.
4. 기본 6초간 재생하고 마지막 0.4초에 부드럽게 종료한다. 재생 중 Space를 다시 누르면 처음 충격부터 다시 시작한다.

차량 루트의 `Cockpit Boost Feedback`에서 Play 중 `Play Boost` / `Stop Boost` 버튼으로도 조작할 수 있다. 두 차량을 모두 활성화하면 양쪽이 Space에 반응한다. 차량별 `Space Key Enabled`로 입력을 제외할 수 있다. Edit 모드 및 Timeline Preview에서는 이 효과를 적용하지 않는다.

Space는 이번 핸들·카메라 효과를 시작한다. 기어/부스터 버튼 Timeline을 자동 재생하거나 V1의 꺼 둔 손 동작을 다시 활성화하지 않는다. 이 씬에는 기존 `BoosterDeploymentController` 인스턴스가 없다. 다른 씬에서 그 컴포넌트가 Space를 쓰면 기존 부스터 전개 기능도 함께 입력을 받는다.

## 조정 위치

`Animation_RedCarCockpit_fix` 또는 `Animation_BlueCarCockpit` 루트 → `Cockpit Boost Feedback`.

| 항목 | 기본값 | 의미 |
|---|---:|---|
| Intensity | 1 | 핸들·카메라 전체 강도 |
| Duration | 6초 | 총 길이. 0이면 Stop Boost까지 유지 |
| Convergence Time | 2.2초 | 카메라가 좁은 진동으로 모이는 시간 |
| Release Time | 0.4초 | 종료 완화 시간 |
| Initial Steering Angle | 24도 | 초반 반동 크기의 기준 |
| Sustained Steering Angle | 9도 | 수렴 후에도 남는 운전자 보정 크기 |
| Steering Tremor | 1.5도 | 핸들에 겹치는 빠른 떨림 |
| Initial / Sustained Position Amplitude | 0.09 / 0.006 | 카메라 이동 크기, 월드 단위 |
| Initial / Sustained Rotation Amplitude | 4.5 / 0.55도 | 카메라 회전 크기 |
| Initial / Sustained Frequency | 3.5 / 21Hz | 수렴하면서 빨라지는 기본 진동 속도 |
| Seed | V1=1 / V2=2 | 차량별 재현 가능한 패턴 |

핸들 수치는 최대 회전 한계가 아니라 반동 패턴에 곱하는 크기이며 잔떨림이 더해진다. 카메라 위치/회전 값도 축별 비율과 점화 충격이 함께 적용된다. 촬영 프레임 속도가 낮으면 높은 진동 주파수가 다르게 보일 수 있으므로 실제 촬영 화면에서 최종 강도와 속도를 조절한다.

각 Cinemachine 카메라의 `Cinemachine Boost Feedback` → `Shot Intensity`로 해당 샷만 조절한다. 0이면 그 샷의 부스터 카메라 흔들림은 꺼지고 기존 엔진 떨림을 유지한다. 기존 엔진 떨림은 부스터 동안 완화되고 종료하면 복귀하여 큰 엔진 이동이 수렴 효과를 가리지 않게 했다.

## 연결 범위

- Red fix 프리팹: 핸들 컨트롤러 + 내부 Cinemachine 카메라 12개.
- Blue 프리팹: 핸들 컨트롤러 + 왼손 후반 갱신. 이 프리팹 자체에는 카메라가 없다.
- 현재 `RH_insidecockpit` 씬: 프리팹과 별개로 저장된 V1·V2 양쪽 루트에 연결. 각 차량 내부 카메라 12개씩 총 24개 연결.

Blue 프리팹을 새 씬에 배치하고 외부 카메라로 촬영한다면 그 Cinemachine 카메라에 `Cinemachine Boost Feedback`을 추가하고 Source에 해당 Blue 차량 루트의 컨트롤러를 지정한다.

## 기존 IK / Timeline과의 관계

- 기존 수동 보정 Transform, 활성 차량, 카메라 구도, Timeline 키와 바인딩을 보존했다.
- 기존 idle 핸들 동작(500) → 부스터 회전 및 V2 왼손 갱신(1200) → V1/V2 손 타깃과 Rig 평가(1500) 순서.
- 다음 프레임에는 부스터가 가한 회전만 제거하고 기존 핸들 동작 위에 새 회전을 더한다. Transform에 회전이 계속 누적되지 않게 한다.
- V1 양손은 기존 `V1SteeringHandLock`이 회전한 핸들을 따른다.
- V2 오른손은 기존 `V2ButtonHandDriver`의 기어/버튼 blend를 유지한다. 기어/버튼에 손을 보내는 Timeline 구간이면 그 동작이 계속 우선한다. 왼손은 회전한 핸들 위치를 Rig 평가 전에 다시 읽는다.
- 삭제된 `DriverHandTargetBlend`는 사용하지 않는다.
- 카메라 Transform을 직접 바꾸지 않고 Cinemachine 최종 보정 단계에 적용한다. 샷 전환 시 해당 차량의 같은 부스터 시간에서 샘플링한다.

백업: `C:/UnityProject/NITROZEROtrailer/Backups/IKRecovery/20260917_SpaceBoost`.

## 검증 결과 (2026-09-17)

별도 Unity 6000.3.18f1 프로젝트에서 현재 소스와 같은 Rigging / Cinemachine / Input System 패키지로 컴파일·Play 검증했다.

- 부스터: 441프레임. Input System의 Space 상태를 주입해 입력 분기 확인, idle과 겹친 회전 누적 방지, 왼손 같은 프레임 추적, 카메라 로컬 방향 보정, 수렴 후 잔진동, 자동 종료/재시작/수동 종료/컴포넌트 비활성화 후 복귀 통과.
- V1: 부스터를 더한 움직이는 차량/핸들 120프레임. 양손 타깃·손목 및 실제 Rig 평가 통과. 합성 리그의 최대 접촉 오차 약 0.000405 Unity 단위.
- V2: 실제 `v2_GearShift`와 `v2_BoosterSwitch` Timeline을 합성 리그에 연결하고 부스터를 더해 206프레임 검증. 기어 접근·접촉·복귀, 버튼, 재탐색, 손목·IK 통과. 최대 접촉 오차 약 0.000400 Unity 단위.
- 프리팹 2개 및 씬의 기존 직렬화 객체는 추가 컴포넌트 목록 외 변경이 없음을 백업과 비교했다. 기존 Transform / Timeline 바인딩 / 활성 상태를 보존했다. 씬 카메라 24개의 차량별 Source 연결을 확인했다.

실제 캐릭터 메시와 촬영 구도에서의 시각 검수는 미실시다. 위 수치는 합성 리그 검증 결과이며 실제 차량 화면의 손 접촉 정확도를 보증하는 값은 아니다. 현재 보정된 손 위치를 바꾸는 기능은 포함하지 않는다.

검증 파일: `tools/IKSteeringValidation/boost-validation-result.txt`, `steering-boost-validation-result.txt`, `gear-button-boost-validation-result.txt`, `tools/IKAudit/boost-asset-validation.txt` (작업공간 루트 기준).
