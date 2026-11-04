# RH_avoidMissile — 실내 미사일 슬로 모션

`Assets/01RAEHYEON/RHScenes/RH_avoidMissile.unity`를 열고 Play하면 8.5초 실내 컷이 재생됩니다.

- Hierarchy: `RH_CockpitMissile_SlowMotion`
- Timeline: `Assets/01RAEHYEON/Timeline/RH_CockpitMissile_SlowMotion.playable`
- Camera: 기존 `Animation_RedCarCockpit/TrailerCams/CAM_06_HoodToDriver`. 위치·방향은 유지하고 화각을 30도로 조정했습니다.
- 기존 `PF_TrailerMissile`과 연결된 `PF_MissileVFXSystem` / `VFX_MissileSmokeTrail`을 사용했습니다. 원본 프리팹과 그래프는 수정하지 않았습니다.

## 연출

0–0.8초 진입 준비, 0.8–6.8초 **보조석 창문 → 운전자와 핸들 사이 → 운전석 창문**으로 천천히 통과, 6.8–8.5초 이탈과 시선 복귀입니다. 보조석 쪽 Entry는 차량 로컬 X=+4.5, 운전석 쪽 Exit는 X=-4.5입니다. 약 4.76초에 운전자 앞을 지납니다. 미사일 길이는 약 1.3m입니다. 목과 머리는 미사일을 약간 늦게 따라보며, 손과 핸들의 기존 리그를 유지합니다.

차량과 카메라를 함께 70도 회전시키고 약간 옆으로 이동시켜 창밖 지형이 도는 느낌을 만듭니다. 전역 Time.timeScale은 변경하지 않습니다. 연기는 기존 월드 공간 VFX를 0.32배 시간으로 시뮬레이션합니다.

## 조정

`RH_CockpitMissile_SlowMotion`의 Inspector에서 Preview 슬라이더 / Play preview / Reset으로 확인할 수 있습니다. Timeline에서도 스크럽할 수 있습니다.

- `Missile_Entry_Window`, `Missile_Exit_Window`: 차량 아래 경로 마커. 높이와 통과 방향을 조정합니다.
- `Drift Degrees`: 회전량.
- `Max Head Yaw`, `Gaze Strength`, `Gaze Lag`, `Gaze Soft Distance`: 고개 추적 범위·강도·반응 지연·부드러움.
- `Smoke Width`, `Smoke Expansion`, `Smoke Spawn Rate`: 연기 크기·팽창·농도.
- 컷 길이 변경 시 컴포넌트 Duration과 Timeline 클립 길이를 함께 조정합니다.

이 실내 컷에 맞춰 기존 다른 Director의 자동 재생과 다른 가상 카메라는 비활성화했습니다. `PathAnchor`의 기존 600m/s `SplineCarController`도 비활성화해 외부 주행이 실내 연출 및 월드 연기와 충돌하지 않도록 했습니다. 원래 Timeline 에셋은 보존했습니다. 양쪽 창문 및 도어넷을 이 씬 인스턴스에서 비활성화했고, Main Camera의 깊이 텍스처를 켰습니다.

## 검증 / 복구

Unity 6000.3.18f1 복사본에서 실제 Play 모드로 재생했습니다. Timeline 연결, 70도 회전, 역방향 스크럽, 손 위치 유지, 시선 복귀를 검증했고 실제 연기 입자와 카메라 프레임을 확인했습니다. `validation.txt`, `playback-validation.txt`, `frame_168.png` 등을 참고하세요.

이후 요청에 따라 통과 방향을 보조석 → 운전석으로 반전했습니다. 경로 마커와 최초 미사일 위치·방향을 변경했으며, 고개 추적과 후방 배기는 경로를 자동으로 따릅니다. `direction-correction.txt`는 변경 후 좌표 검증입니다. 기존 `frame_*.png` 및 Play 모드 검증 기록은 **방향 반전 전** 자료입니다.

원래 씬 백업: `C:/UnityProject/NITROZEROtrailer/Backups/CockpitMissilePass/RH_avoidMissile_before_install.unity`.
작업 도중 검증용으로 추가 로드된 씬은 닫으며, `RH_domeInTheMoon.unity`에는 연출을 추가하거나 저장하지 않았습니다.
