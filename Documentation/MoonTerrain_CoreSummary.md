# NITRO ZERO 달 표면 터레인 핵심 정리

최종 갱신: 2026-09-17

이 문서는 달 표면 터레인과 도로를 수정할 때 반드시 유지해야 하는 현재 프로젝트 기준을 정리한다. 씬 파일만 수정했다고 생각해도 실제로는 공용 `TerrainData` 또는 공용 도로 메쉬가 바뀔 수 있으므로, 아래의 공유 관계를 먼저 확인해야 한다.

## 1. 최종 시각 방향

- 달 표면은 매끈한 설원이나 물 표면이 아니라 저주파 언덕, 중간 크기 요철, 미세한 암석 질감이 함께 보여야 한다.
- 지형에 반복적인 격자, 바둑판, 빗금, 동심원, 케이크 단면 같은 높이 띠가 보이면 안 된다.
- 도로 평탄화 구간과 원래 지형 사이에 수직 단차가 생기면 안 된다.
- 도로에 가까운 능선은 비교적 가파를 수 있지만, 도로 높이부터 연속적으로 상승해야 한다. 갑자기 솟거나 잘린 벽처럼 보여서는 안 된다.
- 도로 아래만 평탄하게 유지하고, 도로 바깥은 거친 달 표면을 유지한다.
- 특별히 요청하지 않은 크레이터를 추가하지 않는다. 도로 주변에 컵처럼 파인 인공적인 함몰도 만들지 않는다.
- 강한 지구광에서도 윤슬이나 물광처럼 반짝이지 않는 무광 표면을 유지한다.

## 2. 공용 데이터와 씬 관계

| 구분 | TerrainData | 대표 크기 | 원점 Y / 도로 Y | 공유되는 주요 씬 |
|---|---|---:|---:|---|
| Avoid Missile | `Assets/Terrain/AvoidMissile_MoonTerrain.asset` | 4000 × 2000 × 320m | -60 / -30m | `avoidMissile`, `CarTest`, `RH_avoidMissile`, `RH_CarTest`, DM 파생 씬 |
| Boost On | `Assets/Terrain/BoostOn_MoonOpenPlain_v5.asset` | 6000 × 2500 × 360m | -80 / -24m | `boostOn`, `RH_boostMode`, `RH_inside`, Yujeong 파생 씬 |
| Dome | `Assets/Terrain/DomeInTheMoon_Basin.asset` | 6000 × 6500 × 560m | -100 / -20m | `domeInTheMoon`, `RH_domeInTheMoon`, `HW_domeInTheMoon` |
| Racing | `Assets/Terrain/Racing_JCurveMoonTerrain.asset` | 5000 × 4000 × 520m | -80 / 약 -20m | `racing`, `RH_racing`, `HW_racing` |

### 매우 중요한 공유 규칙

- 위 TerrainData는 씬마다 복사된 데이터가 아니라 여러 씬이 같은 에셋을 참조한다.
- 예를 들어 `RH_racing`의 높이를 수정하면 기본 `racing`과 `HW_racing`에서도 같은 지형이 보인다.
- 씬 하나만 다르게 만들 필요가 있으면 먼저 TerrainData를 복제하고 해당 씬의 `Terrain`과 `TerrainCollider`가 모두 복제본을 참조하도록 바꿔야 한다.
- TerrainData는 약 20MB의 Unity binary asset이다. 텍스트 병합으로 해결하지 말고 충돌 시 어느 쪽 데이터를 보존할지 먼저 결정한다.

## 3. 도로 기준

현재 모든 달 도로의 목표 폭은 `30m`이다.

- 도로 반폭: `15m`
- 지형의 완전 평탄 구간: 도로 중심선 기준 약 `15m` 이내
- 흰색 외곽선: 도로 양쪽에 한 줄씩 배치
- 차량 유도봉 중심 오프셋: 중심선에서 좌우 각각 `14.55m`
- 차량 유도봉 높이: 아스팔트 표면 위 `0.08m`
- 유도봉은 좌우가 같은 진행방향 위치에 한 쌍씩 있어야 한다.
- 도로, 흰 선, 먼지 오버레이는 동일한 중심선과 동일한 시작·끝 범위를 사용해야 한다.

관련 검증 도구:

- `Documentation/RoadWidth30Update.js`
- 이 도구는 도로 폭, 유도봉 좌우 오프셋과 높이를 검사한다.
- 돔 씬의 `차량유도봉_2_빨강` 프리팹 GUID도 검사 대상에 포함되어 있다.

## 4. Racing 도로의 현재 형태

Racing 도로는 과거의 J 모양을 강조하기보다 촬영 시 진입 방향을 이해할 수 있는 직선–커브–출구 구조로 사용한다.

- 시작 직선 중심선: `x=-2500`에서 시작해 터레인 왼쪽 끝까지 연결
- 커브 중심: 대략 `(-200, 0)`
- 커브 반경: 약 `600m`
- 출구 중심선: 터레인 아래쪽 끝을 완전히 덮도록 `z=-2010`까지 연장
- 아스팔트, 흰색 외곽선, 먼지 오버레이 모두 같은 끝점까지 연장됨
- 길이를 늘린 뒤 UV도 재계산하여 끝 구간 텍스처가 늘어나 보이지 않도록 처리함

관련 에셋:

- `Assets/Terrain/Racing_DriftRoad.asset`
- `Assets/Terrain/Racing_EdgeLines.asset`
- `Assets/Terrain/Racing_EdgeDust.asset`
- `Assets/Editor/JCurveDriftTerrainGenerator.cs`

`JCurveDriftTerrainGenerator.BuildRoadPoints()`의 현재 재생성 기준도 시작점 `-2500m`, 출구 길이 `1916m`로 갱신되어 있다. 이전 값인 시작점 `-2050m`, 출구 길이 `1450m`로 되돌리면 도로가 터레인 끝까지 닿지 않는다.

## 5. 도로와 지형을 자연스럽게 연결하는 방법

### 권장 방식

1. 도로 중심선과 각 지형 샘플의 최단거리를 구한다.
2. 중심선에서 `15m` 이내만 도로보다 약간 낮게 평탄화한다.
3. 도로 가장자리 높이와 같은 방향의 바깥 원지형 높이를 두 끝점으로 삼는다.
4. 두 높이를 `SmoothStep` 계열 곡선으로 연속 보간한다.
5. 경사 중간에만 저주파와 중주파 노이즈를 넣는다. 시작점과 끝점에서는 노이즈 진폭이 0이 되어야 한다.
6. 계단 픽셀을 없애는 최소한의 스무딩만 적용한다.

### 피해야 하는 방식

- 일정 거리마다 높이를 단계적으로 제한하는 방식
- 기존 지형에 단순한 `Min(current, cap)`만 넓게 적용하는 방식
- 도로 주변 수백 미터를 같은 높이로 만든 뒤 외부 지형과 한 번에 연결하는 방식
- 같은 반경을 따라 동일한 값을 주는 방식
- 10회 이상의 강한 스무딩으로 표면 전체를 녹이는 방식

이 방식들은 수직 단차, 빗금, 동심원, 계단형 능선, 지나치게 매끈한 표면을 만든다.

### Racing 전이 기준

- 완전 평탄 범위는 반폭인 약 `15m`까지만 유지한다.
- 도로 가까이에서 가파른 경사를 만들더라도 높이는 반드시 도로 가장자리부터 연속적으로 변해야 한다.
- 외부 원지형과 합쳐지는 구간은 카메라에서 띠가 보이지 않도록 충분히 넓게 잡되, 산 전체를 깎는 광범위한 cap 방식은 사용하지 않는다.
- 거칠기는 높이 변화의 방향과 관계없는 2D 노이즈로 넣어 방사형 빗금이 생기지 않도록 한다.

## 6. Dome 지형 핵심 설정

- 돔은 대형 크레이터 내부에 있다.
- 크레이터 목표 지름은 약 `4.2km` 규모다.
- 크레이터 내부 경사는 외부보다 가파르고, 외부 경사는 지평선으로 이어지는 것처럼 완만해야 한다.
- 림 높이는 일정한 원형 벽이 아니라 불규칙해야 한다.
- 도로가 크레이터 원의 끊긴 틈을 통과해 밖으로 이어진다.
- 도로 양쪽 림은 갑자기 끊기지 않고 도로 방향으로 길게 이어지며, 길어진 만큼 완만하게 내려와야 한다.
- 돔 내부를 완전한 평지로 만들지 않는다. 낮은 요철과 완만한 높이 변화가 있어야 한다.
- 돔 이외의 불필요한 소형 크레이터는 제거한다.

## 7. 무광 지형 머티리얼

현재 반사 제거용 기준 머티리얼:

- 머티리얼: `Assets/Terrain/Dome_MatteLunarTerrain.mat`
- 셰이더: `Assets/Shaders/DomeMatteTerrain.shader`
- 셰이더 이름: `NITRO ZERO/Terrain/Matte Lunar`
- 머티리얼 GUID: `84ad8e01e6401fa439e5aca4dfe39720`
- 셰이더 GUID: `e41b3d870bba4d54280350fc73201fca`

이 셰이더는 직접 스페큘러 하이라이트와 스카이/리플렉션 프로브 반사를 제거하도록 제작되었다. 현재 Dome 계열과 Racing 계열 씬은 이 머티리얼을 사용한다.

### 물광 방지 원칙

- Metallic은 `0`을 유지한다.
- TerrainLayer의 Smoothness는 가능하면 `0`에 가깝게 유지한다.
- Normal Scale은 입체감을 남기되 과도한 반짝임이 생기지 않는 범위로 사용한다. 기존 조정 기준은 약 `0.5`였다.
- 조명을 약하게 하는 것만으로 해결하지 않는다. Lit 셰이더의 스페큘러와 환경 반사가 남으면 낮은 각도에서 다시 반짝인다.
- 지형의 입체감은 반사광 대신 높이, 노멀, 음영 대비로 표현한다.

### 현재 확인이 필요한 항목

Avoid Missile과 Boost On 계열 일부 씬은 `594ea882c5a793440b60ff72d896021e` 머티리얼 GUID를 참조한다. 현재 프로젝트 검색에서는 해당 GUID의 `.meta` 파일이 확인되지 않았으므로, Unity Inspector에서 Missing 상태인지 반드시 확인해야 한다. 파랗게 보이거나 기본 Terrain Lit처럼 반짝이는 현상이 있으면 이 참조부터 점검한다.

## 8. 텍스처와 레이어 혼합

- 동일 텍스처 하나를 일정한 타일 크기로 반복하면 바둑판과 격자가 보인다.
- 서로 다른 스케일과 회전감을 가진 2~3개의 달 표면 텍스처를 혼합한다.
- 색상 차이가 큰 텍스처를 강하게 섞으면 도로 주변 평탄화 영역만 다른 색으로 보인다.
- 도로 주변 레이어는 같은 방향의 바깥 지형 레이어 값을 참조해 점진적으로 혼합한다.
- 알파맵 블렌딩 후 각 픽셀의 레이어 가중치 합을 다시 1로 정규화한다.
- 규칙적인 거리 마스크에만 의존하지 말고 약한 저주파 노이즈로 경계를 흐트러뜨린다.
- 노멀과 디스플레이스먼트가 너무 강하면 달 표면이 젖거나 유리처럼 보일 수 있다.

## 9. 씬별 특징과 주의점

### Avoid Missile

- 크기: 4000 × 2000m
- 도로 Y: 약 -30m
- 길고 곧은 도로와 낮은 카메라에서 터레인과 도로가 겹치면 검은 선이 나타날 수 있다.
- 터레인을 내렸을 때 검은 선이 사라진다면 도로와 지형의 깊이 충돌 가능성이 높다.
- 도로 아래 지형은 아스팔트보다 명확히 낮게 유지한다.

### Boost On

- 크기: 6000 × 2500m
- 도로 Y: 약 -24m
- 크레이터가 너무 많아지지 않도록 한다.
- 도로 평탄화 영역과 외부 지형의 색상 차이를 특히 확인한다.

### Dome

- 크기: 6000 × 6500m
- 대형 크레이터와 돔이 핵심이다.
- 표면의 격자와 물광이 가장 잘 드러나는 씬이므로 Matte Lunar 머티리얼을 유지한다.
- `gate`, `gate2`는 돔 입구 내부를 가리는 오브젝트이며 `DomeEntranceDark` URP Unlit 머티리얼을 사용한다.

### Racing

- 크기: 5000 × 4000m
- 도로 반폭: 15m
- 커브 안쪽 능선은 차량을 가렸다가 드리프트 순간 보여주는 촬영 장치다.
- 능선을 지우지 말고 높이와 경사만 조절한다.
- 케이크 단면, 뾰족한 봉우리, 도로 옆 수직 단차가 반복적으로 발생했던 구간이다.
- 도로 끝은 현재 터레인 경계까지 연장되어 있다.

## 10. 검은 선과 깊이 충돌 진단

도로 중앙 또는 소실점에 검은 선이 보일 때 다음 순서로 확인한다.

1. Terrain 오브젝트를 잠시 끄거나 아래로 내려 선이 사라지는지 확인한다.
2. 사라지면 도로 메쉬와 Terrain의 높이 겹침 또는 Z-fighting이 원인이다.
3. 도로 메쉬의 Receive Shadows만 끄는 것으로 해결되지 않으면 그림자 문제가 아니다.
4. 도로 아래 지형을 수 cm가 아니라 충분히 낮춰 깊이 차이를 확보한다.
5. 카메라 Near Clip을 지나치게 작게, Far Clip을 지나치게 크게 사용하면 원거리 깊이 정밀도가 악화된다.
6. 거대한 단일 도로 삼각형은 세분화한다. 프로젝트에서 500m가 넘는 삼각형 경고가 발생한 적이 있다.

## 11. Git과 TerrainData 안전 규칙

- `.unity` 씬과 `.asset` TerrainData를 같은 사람이 동시에 수정하면 충돌 위험이 높다.
- `git pull` 전에 로컬 변경을 커밋하거나 stash 또는 별도 백업한다.
- Unity TerrainData binary 충돌은 수동 텍스트 병합이 불가능하다.
- `Use the modified file from origin/main`을 선택하면 로컬 TerrainData/씬 수정이 사라질 수 있다.
- 공용 TerrainData를 수정하기 전에는 파일 단위 백업을 만든다.
- 씬 충돌에서 원격 파일을 선택한 뒤에는 도로, 머티리얼, TerrainData 참조가 사라졌는지 다시 검사한다.
- `.meta` 파일을 누락하면 GUID 연결이 끊어진다. 머티리얼, 셰이더, TerrainLayer와 함께 반드시 커밋한다.

## 12. 수정 후 필수 검수표

- [ ] 도로 폭이 정확히 30m인가?
- [ ] 도로와 Terrain 사이에 검은 선이나 Z-fighting이 없는가?
- [ ] 도로 양쪽 흰 선이 끝까지 연결되는가?
- [ ] 먼지 오버레이가 아스팔트와 같은 길이를 사용하는가?
- [ ] 유도봉이 중심에서 좌우 14.55m이고 도로 위 0.08m인가?
- [ ] 도로 옆에 수직 단차나 계단 띠가 없는가?
- [ ] 가파른 능선이 도로 높이부터 연속적으로 이어지는가?
- [ ] 표면에 격자, 바둑판, 빗금 또는 동심원 패턴이 없는가?
- [ ] 달 표면의 저·중·고주파 거칠기가 모두 보이는가?
- [ ] 지구광 아래에서 물광과 윤슬이 없는가?
- [ ] 도로 평탄화 영역과 외부 지형의 색이 자연스럽게 이어지는가?
- [ ] 새 크레이터가 의도하지 않은 위치에 생기지 않았는가?
- [ ] TerrainData를 공유하는 다른 씬도 확인했는가?
- [ ] Terrain과 TerrainCollider가 동일한 TerrainData를 참조하는가?
- [ ] Unity Console에 대형 삼각형, Missing Material 또는 Missing Shader 경고가 없는가?

## 13. 주요 관련 파일

### 생성기와 보정 도구

- `Assets/Editor/AvoidMissileMoonTerrainGenerator.cs`
- `Assets/Editor/BoostOnMoonTerrainGenerator.cs`
- `Assets/Editor/DomeInTheMoonTerrainGenerator.cs`
- `Assets/Editor/JCurveDriftTerrainGenerator.cs`
- `Assets/Editor/BlendMoonRoadShoulders.cs`
- `Assets/Editor/MoonRoadShoulderRoughnessGenerator.cs`
- `Assets/Editor/RefineRacingTerrainContinuity.cs`
- `Assets/Editor/RepairRacingCakeCliffs.cs`
- `Assets/Editor/FinalizeRacingTerrainHeight.cs`
- `Assets/Editor/FinalizeDomeLunarSurface.cs`
- `Assets/Editor/MoonRoadWidth35TerrainClearance.cs`
- `Documentation/RoadWidth30Update.js`

### 머티리얼과 셰이더

- `Assets/Terrain/Dome_MatteLunarTerrain.mat`
- `Assets/Shaders/DomeMatteTerrain.shader`
- `Assets/Shaders/DomeMatteTerrainAdd.shader`
- `Assets/Shaders/DomeMatteTerrainBase.shader`
- `Assets/Terrain/DomeEntranceDark.mat`

### 주의

과거 보정 스크립트는 당시 문제를 해결하기 위해 누적 방식으로 만들어진 것이 많다. 여러 메뉴를 순서 없이 다시 실행하면 지형을 반복해서 깎거나 과도하게 스무딩할 수 있다. 기존 TerrainData에 메뉴를 실행하기 전에 코드의 적용 범위와 공용 씬 영향을 확인하고 백업한다.
