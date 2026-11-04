# IK 단계별 복구 기록

## 추가 작업 — V2 DriverControls 기어 손 재연결

- 기존 V2 전용 컨트롤러에 shifterSource/shifterBlend 처리를 추가했다. Blue 프리팹과 RH_insidecockpit의 Blue를 각각 해당 v2_ShifterGripTarget에 연결했다.
- Assets/v2_GearShift.playable의 기존 shifterBlend 곡선에 남아 있던 삭제된 공용 스크립트 GUID 3개를 현재 V2 컴포넌트 GUID로 교체했다. 키 값/시각, 기어봉/엑셀/클러치 곡선은 변경하지 않았다.
- 가중치 0에서는 사용자가 보정한 핸들 자세, 1에서는 회전하는 기어 접점의 위치/회전을 따른다. 기존 키대로 60~80번 접촉을 유지하고 100번에 핸들로 복귀한다.
- 기존 버튼 처리는 유지했다. 두 가중치를 동시에 켜면 버튼 처리가 나중에 적용되므로 독립된 촬영에서는 Director를 순차 재생한다.
- 적용 직전 현재 씬은 GearShift 자동재생=1, BoosterSwitch 자동재생=0이었다. 이 상태는 유지했다. 프리팹 원본의 BoosterSwitch 자동재생만 1→0으로 맞춰 두 동작의 동시 자동재생을 방지했다. 프리팹의 GearShift 자동재생은 1 그대로다.
- 기능은 이전과 같이 Play 전용이다. Edit Timeline Preview의 손 이동은 이번 변경에 포함되지 않는다.
- 모든 수동 Transform과 V1은 이번 변경에서 건드리지 않았다.

검증: 별도 Unity에서 실제 v2_GearShift 및 v2_BoosterSwitch 에셋을 순차 평가한 206프레임 검사 통과. 기어 접근·60~80번 접촉·100번 복귀, 움직이는 차량/핸들/기어봉, 타겟과 실제 팔 Tip의 위치/회전, 버튼 동작 회귀, 0/70번 재평가, 종료 시 상태 복원을 확인했다. 최대 Tip 접촉 오차=0.0003992192 Unity unit. 이는 합성 팔 Rig에서의 동작 검사이며 실제 Blue 메시 손가락 접촉의 화면 검증과 구분한다.

백업: `C:/UnityProject/NITROZEROtrailer/Backups/IKRecovery/20260917-060040_v2_gear_connect/`.

검증 결과: `C:/UnityProject/NITROZEROtrailer/tools/IKSteeringValidation/gear-button-validation-result.txt`.

## 추가 작업 — V2 BoosterSwitch 오른손 연결

사용자 요청: 작동하지 않는 v2_BoosterSwitch를 오른손 IK에 연결.

- `V2ButtonHandDriver`를 새로 추가하고 Blue 프리팹과 RH_insidecockpit의 Blue 오른손에 각각 연결했다. 삭제된 공용 DriverHandTargetBlend는 복원하지 않았다.
- 기존 Missing 컴포넌트 슬롯을 V2 전용 컴포넌트로 교체했다. 각 대상의 RigBuilder, v2_RightHandTarget, v2_RedSwitchPressTarget을 해당 대상 내부 참조로 연결했다.
- v2_BoosterSwitch.playable의 기존 buttonBlend 곡선에 남아 있던 스크립트 GUID 3개를 새 컴포넌트로 연결했다. 곡선값, 타이밍, 접점 회전 애니메이션, 트랙/Director 바인딩은 보존했다.
- 시작 시 손의 핸들 상대 위치·회전을 캡처한다. buttonBlend=0에서는 이를 보존하며 핸들을 따라가고, 1에서는 기존 버튼 접점 위치·회전에 도달한다.
- Timeline/핸들 움직임 평가 후 LateUpdate에서 내부 Target을 갱신하고 Rig를 수동 평가한다. Driver Animator는 실행 중 AlwaysAnimate로 설정해 가시성에 따른 중단을 막고, 비활성화 시 원래 상태를 복원한다.
- 실행은 Play 전용이다. Edit 모드 Timeline Preview에서 손을 움직이는 기능은 이번 작업에 포함하지 않았다.
- 기존 60fps 타이밍: 0~30번 buttonBlend=0, 30~70번 접근, 70~100번 buttonBlend=1. 버튼 접점의 누름 회전은 84번 키를 포함한 기존 곡선 그대로다. 마지막 값/Director WrapMode도 임의 변경하지 않았다.
- V1 코드/설정, 양쪽 차의 Transform, 기어·페달 키는 변경하지 않았다. 적용 당시 씬 Blue 루트는 이미 활성 상태였으며 활성 상태도 변경하지 않았다.

검증: 별도 Unity 6000.3.18f1 프로젝트에서 실제 수정된 Timeline 에셋과 합성 팔 Rig를 사용했다. 0~100번 및 0/70번 재평가, 움직이는 차량/핸들, 내부 Target 위치·회전과 실제 IK Tip 위치·회전, 종료 시 Rig/Culling 복원이 모두 통과했다. 최대 Tip 접촉 오차는 0.0003476068 Unity unit이다. 실제 Blue 캐릭터 메시의 손가락 접촉은 화면 확인이 남아 있다.

백업: `C:/UnityProject/NITROZEROtrailer/Backups/IKRecovery/2026-09-16T20-35-02-448Z_v2_button_connect/`.

검증 결과: `C:/UnityProject/NITROZEROtrailer/tools/IKSteeringValidation/button-validation-result.txt`.

## 현재 범위 — V1 양손 핸들 유지로 변경

사용자 요청: “아니다 이거 V1 핸들에만 손 잘잡게 해줘”. 이전 변속 복구 검증 순서는 중단하고, `Animation_RedCarCockpit_fix`의 양손 핸들 유지에 한정했다.

적용 내용:

- `V1SteeringHandLock`을 V1 Driver에 추가했다. 핸들 미세회전 이후 LateUpdate에서 사용자 배치의 상대 Offset으로 양손 Target을 갱신하고, Animation Rigging의 수동 평가 방식으로 Rig를 계산한다.
- 기존 왼손/오른손 Follower는 이 프리팹에서만 비활성화했다. 공용 Follower 코드는 변경하지 않았다.
- V1 기어·부스터·드리프트 Director를 비활성화하고, 오른손 Target/오른팔 Constraint 트랙 바인딩을 분리했다. Timeline 에셋의 키는 삭제하지 않았다.
- 오른손 Target 및 오른팔 Constraint의 별도 Animator를 비활성화했다.
- V1 오른손의 삭제된 DriverHandTargetBlend 컴포넌트 잔여 항목을 제거했다. 해당 스크립트를 복원하지 않았다.
- 오른손 회전 Weight가 원래 0이었으므로, 실행 시 기존 손 본의 회전을 기준으로 잡고 핸들 회전을 따라가게 했다. 저장된 Target 회전값/Constraint Weight는 바꾸지 않는다. 컨트롤러 비활성화 시 원래 회전 Weight와 RigBuilder 상태를 복원한다.
- 저장된 RH_insidecockpit 씬에서 Red fix 루트만 활성화했다. fix_v2 및 Blue는 변경하지 않았다.

보존 검사:

- 프리팹의 모든 Transform 객체가 변경 전과 동일하다. 손발 Position/Rotation/Scale, 좌석, 외부 접점 보존.
- 씬 변경은 Red fix 루트의 m_IsActive 0→1 한 줄뿐이다.
- 새 컨트롤러의 Rig/Constraint/Source 참조가 모두 실제 프리팹 객체로 해석된다.
- 1단계에서 복구했던 Timeline 손 키는 유지하되 현재 V1 핸들 동작에는 연결하지 않는다.

백업: `C:/UnityProject/NITROZEROtrailer/Backups/IKRecovery/2026-09-16T20-05-17-154Z_v1_steering_only/`.

검증:

- 실제 Unity 6000.3.18f1 어셈블리를 사용한 C# 컴파일 통과.
- 격리 Unity 프로젝트에서 합성 양팔 Rig로 120프레임 Play 검사 통과: 차량 이동/회전 + 핸들 ±25도 회전, 양손 Target 상대 위치/회전 유지, 양팔 Tip의 Target 도달, 컨트롤러 종료 시 RigBuilder와 오른손 Weight 복원.
- 위 테스트의 최대 Tip 위치 오차: 0.0004113884 Unity unit.
- 테스트 결과: `C:/UnityProject/NITROZEROtrailer/tools/IKSteeringValidation/steering-validation-result.txt`.
- 실제 V1 캐릭터 메시/손가락 접촉을 촬영 화면에서 확인한 것은 아니다. 사용자 배치와 씬 Override를 보존한 상태이며, 최종 시각 확인은 남아 있다.

## 기준

- DriverHandTargetBlend는 사용자가 의도적으로 삭제했다. 복구하지 않는다.
- 기존 수동 IK 위치·회전·배율, 접점, 기어봉/페달 키를 임의 변경하지 않는다.
- 파일 복구와 Unity Preview/Play 검증을 구분한다.

## 1단계 — 2026-09-17 KST: Red 오른손 키 복구

수정 파일: `Assets/01RAEHYEON/Timeline/v1_GearShift_New.playable`.

오른손 AnimationClip(fileID `-3590271882105414346`)만 수정 직전 백업에서 복구했다. `shifterBlend`만 남았던 클립을 기존 위치 X/Y/Z 및 회전 X/Y/Z 곡선으로 되돌렸다. 회전 곡선의 0 값은 트랙 회전 Offset과 함께 해석해야 한다.

복구 출처:
`C:/UnityProject/NITROZEROtrailer/Backups/RightHandFollowFix/v1_GearShift_New_2026-09-17_before_right_hand_follow_fix.playable`

이번 수정 전 백업 및 검증 결과:
`C:/UnityProject/NITROZEROtrailer/Backups/IKRecovery/2026-09-16T19-41-38-594Z_step1_red_hand/`

파일 검증:

- 변경된 Unity 객체는 위 AnimationClip 1개뿐이다.
- 복구된 클립은 출처 백업의 클립과 일치한다.
- 나머지 객체, 트랙 Offset, 기어봉/페달 키, 바인딩에 쓰는 ID는 유지했다.
- .meta GUID도 유지했다.
- 씬/프리팹/Follower/손목 Weight/Blue는 변경하지 않았다.
- 기존 3000번 유지 키를 보존했다. 60fps 기준 50초까지 키가 있으므로 Timeline 길이가 이전 3.5초보다 늘어난다. 이를 임의 삭제하지 않았다.

검증 상태:

- [x] 파일의 기존 손 키 복구 및 변경 범위 검사.
- [ ] Unity에서 재임포트된 키 확인.
- [ ] Red fix의 v1_GearShift Timeline 0~210번 Preview 확인.
- [ ] 기존 Follower와 함께 Play할 때 손 Target/Rig 결과 확인.
- [ ] 115~150번 접촉, 이후 복귀와 손목 확인.

다음 단계는 Preview 결과와 Play 결과를 구분해 Follower/Animator 제어를 확인하는 것이다. 현재 단계만으로 정상 동작 복구를 선언하지 않는다.

참고: 저장된 RH_insidecockpit에서 Red fix 루트는 비활성이다. 화면 검증 시 실제 선택한 인스턴스/활성 상태를 확인해야 한다. 조사 보고서의 ‘오른손 키가 없음’은 이번 1단계 이전 상태를 설명한다.

## 2026-09-17: V1 / V2 Space 부스터 핸들·카메라 효과

현재 V1 `V1SteeringHandLock`, V2 `V2ButtonHandDriver`에 맞춰 `CockpitBoostFeedback`을 양쪽 프리팹 및 RH_insidecockpit의 양쪽 차량에 추가했다. Space로 6초 재생, 2.2초에 걸쳐 큰 카메라 흔들림이 좁고 빠른 진동으로 수렴한다. 기본 상태에서는 자동 시작하지 않는다.

핸들 반동 뒤의 반대 방향 보정과 잔떨림을 기존 핸들 움직임에 더하고 V2 왼손을 Rig 평가 전에 갱신한다. 씬의 차량별 Cinemachine 카메라 12개씩에 연결했다. 기존 Transform/Timeline 키/바인딩/활성 상태는 보존했다. 부스터 종료 시 엔진 idle 카메라 떨림과 기본 핸들 움직임으로 복귀한다.

별도 Unity 실행에서 부스터 441프레임, V1 IK 120프레임, 실제 V2 기어/버튼 Timeline과 합성 리그 206프레임 검증 통과. 실제 캐릭터·샷의 시각 검수는 미실시.

사용법과 조정값: [Cockpit_Boost_Feedback.md](Cockpit_Boost_Feedback.md).
