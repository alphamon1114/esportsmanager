# 공개 CS2 맵 도입 상태 (2026-09-18)

## 현재 구현한 범위

Inferno, Dust II, Mirage, Nuke, Vertigo의 실제 공개 지형과 NAV 데이터를 확보하고 Unity 미리보기 프리팹으로 변환했다. **5개 맵 모두 실제 토너먼트 경기에 연결했다.** 일반 실행의 Inferno도 공개 지형을 사용한다. 기존 더미 지형은 독립적인 이동/전투 회귀 검사에만 남긴다. 단순한 레이더 이미지 추적이나 임의의 통로 배치로 원본 재현을 대신하지 않았다.

- 원본: [awpy-data release 2000908](https://github.com/pnxenopoulos/awpy-data/releases/tag/2000908), CS2 ClientVersion 2000908 / Steam build 25218825 / 2026-09-09 패치, 9월 10일 배포.
- `Assets/MapSources/2000908/`: 원본 AWMH 지형, NAV, 릴리스 manifest, 레이더 좌표 변환표.
- `Assets/Maps/PublicPreviews/`: 다섯 맵의 mesh asset·단색 material·preview prefab.
- Unity에서 프리팹을 열어 Scene 뷰로 건물 내부를 확인한다. `Navigation areas (select to inspect)` 오브젝트를 선택하면 이동 영역 경계 Gizmo를 표시한다.
- 재생성: `FPS Manager > Maps > Build public map previews`.
- 다시 내려받기: `powershell -ExecutionPolicy Bypass -File Tools/FetchPublicMaps.ps1`. geometry/nav 압축파일은 배포 manifest의 SHA-256과 비교한다. 원본 파일을 바꾸려면 버전과 검사 결과도 갱신한다.

| 맵 | 정점 | 삼각형 | 이동 영역 | 같은 hull 연결 수 | 이동 영역 높이 범위(변환 후) |
|---|---:|---:|---:|---:|---:|
| Dust II | 122106 | 239917 | 2242 | 6275 | 9.3472 |
| Mirage | 44157 | 73286 | 2544 | 6381 | 9.6520 |
| Nuke | 46009 | 89563 | 3040 | 7676 | 19.1516 |
| Vertigo | 41077 | 66048 | 2105 | 5321 | 10.6680 |

## 좌표와 경로

`SourceMapData.cs`는 원본 Hammer Z-up을 Unity Y-up으로 바꾸며 일률적으로 0.0254 배율을 적용한다. 기존 100×100 평면에 강제로 압축하지 않는다. Y/Z 교환에 따른 삼각형 감기 방향도 반전한다. 경기에서는 같은 metre 단위를 사용하며, NAV 중심을 옮기되 가로·세로 비율과 높이는 보존한다.

NAV v30~36의 영역·3차원 꼭짓점·방향성 연결·사다리 참조 ID를 읽는다. 현재 확보한 v36 파일은 정렬된 KV3 v5 비압축 메타데이터를 포함한다. 지원하지 않는 압축 메타데이터나 움직이는 nav mesh는 명시적으로 거부한다. 원본 NAV의 사다리 구조체/커스텀 데이터 전체를 실행 가능한 이동으로 변환한 것은 아니다.

`Route`는 실제 연결된 같은 hull의 영역 ID를 탐색한다. 위아래 층의 평면 좌표가 겹친다는 이유로 두 영역을 합치지 않는다. 경기 봇은 이 그래프와 인접 영역 경계의 통과점을 사용한다. 지형 삼각형의 BVH로 시야와 탄도를 검사하고, 선수·폭탄·아이템의 층 높이를 유지한다. 사다리별 전용 동작과 원본 점프 애니메이션은 아직 구현하지 않았다.

## 데이터의 범위와 한계

[awpy-data 생성 코드](https://github.com/pnxenopoulos/awpy-data/blob/main/scripts/process_geometry.py)는 시야 분석용으로 지형을 단순화한다. playerclip, grenadeclip, sky, passbullets, glass, window 재질은 제외하며 기본 격자 단순화는 8 Hammer units다. 따라서 원본 충돌 전체/정밀 유틸 라인업/유리 파괴를 그대로 재현한다고 보장할 수 없다. 경기에서는 NAV와 단순화 지형을 함께 사용한다. 원본과 동일한 정밀 유틸 라인업 검증은 별도로 필요하다.

텍스처와 장식은 원본 복제가 아닌 단색 더미로 표시한다. 데이터 제공 저장소의 MIT 표시는 생성 스크립트에 해당하며 Valve 맵 데이터 자체의 권리를 바꾸지 않는다.

## 경기 연결과 검증

- `SourceArena.cs`: 지형 BVH, 높이를 보존하는 NAV 탐색과 경계 통과점.
- `SourceMatch.cs`: 맵 교체, 스폰/사이트/전술 지점, 이동·조준·교전·폭탄·소리 높이 연결. Nuke와 Vertigo는 위층/아래층 지도 전환을 제공한다.
- 사이트와 스폰은 공개 좌표를 참고한 기준점을 NAV에 맞춘 프로토타입이다. 원본 엔티티의 정확한 설치 가능 영역 전체를 복제한 것은 아니다. 참고: [관전자 구역 좌표](https://raw.githubusercontent.com/lexogrine/cs2-react-hud/main/aco), [공개 스폰 좌표](https://steamcommunity.com/sharedfiles/filedetails/?id=3046546468). 기준 좌표는 `SourceArena.cs`에 명시했다.
- `PublicMapBuild.cs`: 빌드 직전에 다섯 맵 데이터를 StreamingAssets/PublicMaps로 복사한다. 이 생성 폴더는 Git에서 제외한다. Unity 에디터 컴파일은 검증했으며 배포용 Player 빌드 실행은 별도 확인이 필요하다.
- SourceGameplayChecks: 다섯 맵 각각 양 진영에서 양 사이트까지 20개 경로의 벽 통과 여부, Nuke 높이 분리, 실제 AI 교전과 라운드 종료, Inferno 복귀를 검사한다.
- 단색 더미 그래픽을 유지한다. Unity 실제 카메라 캡처는 `docs/measurements/match-maps/`에 있다.

## Inferno 리워크

- 원본은 같은 2000908 배포본이다. 이미 받은 geometry.zip/navs.zip를 manifest SHA-256과 다시 비교하고 de_inferno.mesh/nav를 추출했다. 정점 285,745개, 삼각형 547,966개, NAV 영역 2,738개다.
- 일반 `Start()`와 토너먼트의 `de_inferno` 선택은 공개 지형을 로드한다. `Initialize()`만 호출하는 기존 격리 검사는 더미 지형을 유지하므로 BeginDeployment와 기존 좌표 기반 안전망을 바꾸지 않는다.
- 스폰 좌표는 [공개 CS2 스폰 기록](https://steamcommunity.com/sharedfiles/filedetails/?id=3046546468), A/B 기준점은 [CS2 Retakes 설치 가능 지점](https://raw.githubusercontent.com/B3none/cs2-retakes/master/RetakesPlugin/map_config/de_inferno.json)을 참고했다. 모두 현재 NAV에 맞추며 설치 가능 영역 전체를 가져온 것은 아니다. 미드는 지형에 맞춘 설계 기준점이다.
- CT: (2353,1977,199), T: (-1650,718,0), A: (2060.5105,422.71582,160.03125), B: (176.47,2768.02,164.03), Mid: (1100,1100,140). 좌표는 변환 전 Hammer x/y/z다.
- 인페르노 단독 프리팹 생성: SourceMapImport.BuildInferno. 전체 재다운로드/프리팹 생성/StreamingAssets 빌드 복사에도 포함했다.
- 실제 AI 라운드 종료, 세이브 엄폐 접근, 4개 스폰→사이트 경로의 벽 통과 여부, 다른 맵에서의 전환, 일반 게임 시작 경로를 검사했다. Unity 실제 카메라 A/B·미드·CT/T 스폰·지도 캡처: `docs/measurements/inferno/`.
