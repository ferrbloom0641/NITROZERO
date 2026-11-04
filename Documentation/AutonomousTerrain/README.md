# RH_racing_Autonomous 지형 재구성

- 기준: MoonTerrain_CoreSummary.md, 2026-09-17 작업 직전의 기존 Racing 지형.
- 도로, 흰 선, 먼지, 유도봉, 차량, 카메라 및 조명은 기존 상태 유지. 씬 텍스트 변경은 Terrain/TerrainCollider의 지형 참조 2곳뿐.
- 전용 지형: Assets/Terrain/Racing_Autonomous_Natural.asset (GUID 1949c64cff9042b4198c4d02f42839ec). 공용 원본 SHA-256 보존 확인.
- 기존 높이장에 100m 공간 필터를 한 번 적용해 급격한 꺾임을 제거하고 큰 능선을 유지. 실제 도로 메쉬 중심선에서 15m까지 도로 아래 0.20m 확보.
- 기본 280~380m에 기존 높이 차이의 2.2배를 더한 전이 폭에서 5차 연속 보간으로 주변 기존 높이에 연결. 고정 반경 cap, 높이 단계 제한, 크레이터 추가 없음.
- 좌표 기반 중/고주파 요철 및 원본 텍스처 혼합을 유지. 전용 레이어의 Metallic/Smoothness는 0. 기존 씬의 Matte Lunar 머티리얼 유지.
- 미리보기는 별도 Unity 검증 프로젝트의 지형·도로와 검사용 조명으로 렌더링. 원래 씬 전체의 차량/조명/후처리를 재현한 최종 화면은 아님.
- 백업: C:/UnityProject/NITROZEROtrailer/Backups/RH_racing_Autonomous_Terrain_20260917

## 검증 결과

```text
Road: 45 actual mesh cross sections, Y=-19.65, width=29.9999..30.0001m. Mesh unchanged.
Layer 0: moon_04_diff_2k; tile=(57.00, 57.00); metallic=0; smoothness=0; normal=0.5
Layer 1: moon_dusted_01_diff_2k; tile=(93.00, 93.00); metallic=0; smoothness=0; normal=0.34
Layer 2: Rock030_4K-JPG_Color; tile=(37.00, 37.00); metallic=0; smoothness=0; normal=0.5
Layer 3: Gravel035_4K-JPG_Color; tile=(46.00, 46.00); metallic=0; smoothness=0; normal=0.5
Layer 4: Ground054_4K-JPG_Color; tile=(127.00, 127.00); metallic=0; smoothness=0; normal=0.3
Heightmap 2049x2049; size=(5000.00, 520.00, 4000.00); existing source preserved. RMS change=34.457m.
Maximum neighbor rise/run: before=12.03386; after=0.99851; within 50m of road=0.02267.
Road foundation clearance=0.200m; alpha normalization max error=2.384186E-07.
Dense interpolated asphalt clearance minimum=0.20229m.
Final surface: 5 non-square texture scales, independent offsets, 2D noise blend; weights normalized.
Final layer Racing_Autonomous_Layer0: tile=(67.00, 83.00), offset=(13.70, 27.10)
Final layer Racing_Autonomous_Layer1: tile=(109.00, 97.00), offset=(45.00, 46.80)
Final layer Racing_Autonomous_Layer2: tile=(47.00, 59.00), offset=(76.30, 66.50)
Final layer Racing_Autonomous_Layer3: tile=(79.00, 113.00), offset=(107.60, 86.20)
Final layer Racing_Autonomous_Layer4: tile=(151.00, 127.00), offset=(138.90, 105.90)
PASS
```
