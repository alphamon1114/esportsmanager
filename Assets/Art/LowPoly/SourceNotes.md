# CS2 스타일 로우폴리 애셋 팩

절차적으로 생성한 게임용 로우폴리 세트. 리깅·애니메이션된 CT/T 캐릭터 2종과 소켓 부착용 무기 4종을 **GLB와 FBX 양쪽**으로, 라이플 2종은 언리깅 OBJ로도 넣었습니다.

---

## 1. 캐릭터 — `char_ct_operator.glb` / `char_t_operator.glb`

| | CT | T |
|---|---|---|
| 폴리곤 | 2,556 tris | 2,632 tris |
| 본 | 27 joints + 소켓 4 | 동일 |
| 애니메이션 | 6 클립 | 동일 |
| GLB | 325 KB | 332 KB |
| FBX | 2.2 MB | 2.2 MB |

**비율** — 신장 1.75 m, 머리 0.25 m 정확히 **7등신**. 바인드 포즈는 T-pose라 Unity Humanoid 아바타 설정이 바로 통과합니다.

**좌표계** — Y-up, 오른손 좌표계, 미터 단위. 캐릭터는 **+Z를 정면**으로 보고 서 있고 캐릭터 기준 왼쪽이 +X입니다. glTF 표준 방향이라 Unity(glTFast/UnityGLTF)·Blender 모두 그대로 들어갑니다.

### 리그

본 이름은 Mixamo 규격(`mixamorig:Hips`, `mixamorig:LeftForeArm` …)이라 Unity에서 Rig → **Humanoid**로 바꾸면 자동 매핑됩니다. 리타게팅·아바타 마스크가 바로 동작합니다.

```
Hips → Spine → Spine1 → Spine2 → Neck → Head → HeadTop_End
                  └ Left/RightShoulder → Arm → ForeArm → Hand → Hand_End
 Hips → Left/RightUpLeg → Leg → Foot → ToeBase → Toe_End
```

손가락 본은 없습니다. 손은 쥔 형태로 모델링돼 있고 Humanoid에서 손가락은 선택 항목입니다.

### 소켓

무기는 파일에 포함하지 않았습니다. 대신 빈 노드 4개가 들어 있습니다.

| 소켓 | 부모 | 용도 |
|---|---|---|
| `Socket_RightHand` | RightHand | 주무기·보조무기 부착 |
| `Socket_LeftHand` | LeftHand | 지지손 IK 타깃 |
| `Socket_Back` | Spine2 | 등에 메는 라이플 |
| `Socket_Holster` | Hips | 허벅지 홀스터 피스톨 |

무기 GLB를 `Socket_RightHand` 자식으로 붙이고 **로컬 트랜스폼을 0으로 두면** 그대로 손에 잡힙니다. 손 소켓은 라이플 파지 자세에서 역산해 배치했기 때문에 오차 0 mm로 맞습니다.

### 애니메이션 클립 6종

상반신 클립은 `Spine~Head` + 양팔만, 하반신 클립은 `Hips` + 양다리만 건드립니다. **두 집합이 겹치는 본이 하나도 없어서** 레이어로 그냥 섞으면 됩니다.

| 클립 | 길이 | 구동 본 |
|---|---|---|
| `upper_rifle_idle` | 2.40 s | 양손 라이플 파지, 지지손이 핸드가드 위 |
| `upper_pistol_idle` | 2.40 s | 양손 피스톨, 팔 전방 신전 |
| `upper_knife_idle` | 2.40 s | 나이프 순수 그립, 반대손 가드 |
| `lower_idle` | 2.80 s | 체중 이동 대기 |
| `lower_walk` | 1.05 s | 걷기 1사이클 |
| `lower_run` | 0.60 s | 달리기 1사이클 (체공 구간 포함) |

세 상반신 클립 모두 호흡에 따른 총구 흔들림이 들어가 있고, 6개 전부 첫 키와 마지막 키가 일치해 **이음매 없이 루프**합니다. 길이는 60 fps 기준 각각 144 / 144 / 144 / 168 / 63 / 36 프레임으로, 모든 키가 정수 프레임에 놓입니다.

**Unity에서 합성하기** — Base Layer에 `lower_*`를 깔고, 그 위에 Additive가 아닌 **Override 레이어**를 하나 올려 상반신 아바타 마스크(Spine 이하 + 양팔 체크, Hips·다리 해제)를 지정한 뒤 `upper_*`를 재생하세요. 각 클립은 Inspector에서 Loop Time을 켜면 됩니다. `preview_layered_rifle_walk.png`가 이 조합(라이플 상반신 + 걷기 하반신)의 결과입니다.

---

## 2. FBX — `fbx/` 폴더

같은 애셋을 Blender 4.0 경유로 변환한 바이너리 FBX 7.4입니다. glTF 패키지 없이 Unity·Unreal에 바로 넣을 수 있습니다.

| 파일 | 내용 |
|---|---|
| `fbx/char_ct_operator.fbx` | 메시 + 27본 + 소켓 4 + 애니메이션 6 테이크 (2.2 MB) |
| `fbx/char_t_operator.fbx` | 동일 (2.2 MB) |
| `fbx/weapon_ak47.fbx` | 2,396 tris + `Grip`·`Muzzle` 노드 |
| `fbx/weapon_m4a1s.fbx` | 2,480 tris |
| `fbx/weapon_pistol.fbx` | 484 tris |
| `fbx/weapon_knife.fbx` | 344 tris |

- 축은 **Y-up / -Z forward**, 단위 1 유닛 = 1 m, 60 fps. Unity에서 Scale Factor 1 그대로 두면 됩니다.
- 소켓 4개는 본에 부모로 묶인 **빈 노드**로 들어갑니다. Unity에서는 Transform으로 계층에 나타나므로 무기 프리팹을 그 아래에 붙이면 됩니다.
- Leaf bone은 생성하지 않았습니다(`add_leaf_bones=False`). 본 이름은 GLB와 동일한 Mixamo 규격이라 Humanoid 자동 매핑이 그대로 됩니다.
- 애니메이션은 6개 테이크(`upper_rifle_idle`, `lower_walk` …)로 한 파일에 들어 있습니다. 프레임 수는 각각 144 / 144 / 144 / 168 / 63 / 36이고, **모든 키가 60 fps 기준 정수 프레임**에 놓이도록 클립 길이를 맞춰 두었습니다.

### FBX에서 달라지는 점 하나

GLB 클립은 자기가 담당하는 본의 채널만 가집니다. 반면 FBX는 포맷 특성상 **모든 테이크가 전신으로 베이크**됩니다. 그래서 `upper_rifle_idle`을 마스크 없이 단독 재생하면 다리가 바인드 포즈(차렷)로 고정됩니다. 레이어에 아바타 마스크를 씌우면 GLB와 완전히 동일하게 동작하니, **FBX를 쓸 때는 마스크가 선택이 아니라 필수**라고 보시면 됩니다. Humanoid 클립은 어차피 마스크가 필요하므로 실무상 차이는 없습니다.

(참고로 Blender의 FBX 익스포터는 테이크 사이에 포즈를 초기화하지 않아, 그냥 내보내면 상반신 클립에 직전 하반신 클립의 마지막 다리 자세가 섞여 들어갑니다. `src/convert_fbx.py`가 각 액션에 누락 채널을 명시적으로 고정해 이 문제를 제거합니다.)

### 변환 검증

`src/dump_posed.py`가 GLB와 FBX를 각각 Blender로 불러 같은 클립·같은 프레임에서 스키닝된 정점을 뽑아 대조합니다.

- 본 27개, 이름·계층 완전 일치
- 메시 4,788 정점 / 2,556 삼각형, 머티리얼 6종 일치
- 6개 클립 × 5개 시점, 대응 정점 최대 차이 **0.000002 m**
- 무기 4종 정점 최대 차이 **0.000001 m**
- 신장 1.7677 m 보존

`preview_fbx_roundtrip_*.png`는 GLB가 아니라 **FBX에서 다시 읽어낸 데이터만으로** 렌더한 것입니다. 소켓 행렬도 FBX에서 읽어 무기를 배치했습니다.

---

## 3. 무기 — `weapon_*.glb`

| 파일 | 폴리곤 | 비고 |
|---|---|---|
| `weapon_ak47.glb` | 2,396 tris | 목재 핸드가드, 바나나 탄창 |
| `weapon_m4a1s.glb` | 2,480 tris | 소음기, RIS 핸드가드 |
| `weapon_pistol.glb` | 484 tris | 슬라이드·레일·탄창 |
| `weapon_knife.glb` | 344 tris | 톱날 스파인, 클립 포인트 |

**무기 공간(weapon space)** — 네 자루 모두 **그립이 원점**, **총구/칼끝이 +Z**, **가늠자가 +Y**입니다. 손 소켓이 기대하는 좌표계라서 부모로 붙이기만 하면 정렬이 끝납니다. 각 파일에는 총구 위치에 `Muzzle` 노드(머즐 플래시·탄피 배출 VFX용)와 원점의 `Grip` 노드가 들어 있습니다.

T 진영에 AK-47, CT 진영에 M4A1-S를 쓰는 걸 기본으로 잡았지만 소켓은 공통이라 아무 조합이나 됩니다.

---

## 4. 라이플 OBJ — `ak47_lowpoly.obj` / `m4a1s_lowpoly.obj`

처음에 드린 언리깅 모델 그대로입니다. 수동 리토폴로지나 텍스처 작업용으로 쓰세요. OBJ와 MTL은 같은 폴더에 두어야 합니다.

**GLB/FBX판과의 관계** — 지오메트리는 완전히 같습니다. 정점 1,424 / 면 1,096(AK), 정점 1,472 / 면 1,090(M4)으로 개수와 연결 관계가 동일하고, 차이는 좌표계뿐입니다. GLB판 = OBJ판을 **Y축 기준 -90° 회전**한 뒤 그립이 원점에 오도록 이동한 것(AK는 (0, 0.078, 0.078), M4는 (0, 0.070, 0.078))이고, 잔차는 0.0000095 m로 OBJ 텍스트의 소수점 5자리 정밀도 한계와 같습니다.

즉 소켓에 붙일 거라면 GLB/FBX판을, 총구가 +X인 원본 좌표계가 필요하면 OBJ판을 쓰시면 됩니다. OBJ를 그대로 소켓에 붙이면 총이 옆으로 누우니 주의하세요.

---

## 공통 스펙

- **토폴로지** — 전 파트 워터타이트. 경계 엣지 0, 축퇴면 0, 노멀 전부 외향.
- **UV** — 페이스별 평면 투영을 1024px 아틀라스에 겹침 없이 패킹. AO·커브처 베이크에 바로 쓸 수 있습니다. 캐릭터에 본격적인 텍스처를 그릴 거라면 Blender에서 다시 펴는 쪽이 낫습니다.
- **머티리얼** — 캐릭터 6종, 라이플 5종, 사이드암 4종. 텍스처 없이 PBR 베이스 컬러 + 러프니스/메탈릭만 지정했습니다. 머티리얼 ID로 마스크를 뽑아 쓰기 좋습니다.
- **셰이딩** — 총열·소음기 같은 원통과 캐릭터의 유기적 파트만 스무스, 나머지는 플랫. 로우폴리 각면 느낌은 유지됩니다.
- **검증** — `src/verify_skin.py`가 내보낸 GLB를 **다시 읽어서** 파일 안의 스킨 웨이트·역바인드 행렬·노드 계층·애니메이션 샘플러만으로 스키닝을 재현하고, 원본 리그 결과와 대조합니다. 6개 클립 × 5개 시점에서 최대 오차 **0.000023 m**(float32 저장 정밀도 한계)로 일치합니다.

## 임포트 메모

**Unity** — GLB를 넣으려면 glTFast(`com.unity.cloud.gltfast`) 또는 UnityGLTF 패키지가 필요합니다. 임포트 후 캐릭터는 Rig → Animation Type을 **Humanoid**로, Avatar Definition은 Create From This Model로 두세요. 애니메이션 클립마다 Loop Time을 켜면 됩니다. glTF를 쓰고 싶지 않다면 Blender에서 열어 FBX로 내보내는 쪽이 가장 확실합니다.

**Blender** — `File > Import > glTF 2.0`. 아마추어와 6개 액션이 그대로 들어옵니다. 액션은 Dope Sheet → Action Editor에서 전환해 확인하세요.

**Unreal** — glTF Importer로 들어가지만, 스켈레탈 메시는 Blender 경유 FBX가 안정적입니다.

## 재생성

`src/`에 전체 생성 스크립트가 있습니다. 표준 라이브러리만 쓰고, 프리뷰 렌더에만 numpy/Pillow가 필요합니다.

```bash
cd src
python3 export.py          # 라이플 OBJ
python3 export_chars.py    # 캐릭터 + 무기 GLB + 프리뷰
python3 verify_skin.py     # GLB 스키닝 왕복 검증
python3 test_kit.py        # 프리미티브 단위 테스트

# FBX 변환 (Blender 4.0 이상 필요)
blender --background --python convert_fbx.py -- in.glb out.fbx
blender --background --python dump_posed.py  -- in.glb  dump_a.json
blender --background --python dump_posed.py  -- out.fbx dump_b.json   # 대조용
```

- 폴리곤 예산: `ak47.py` / `m4a1s.py`의 `ORG`·`RND`, `character.py`의 `limb(..., seg=)` 값을 조절합니다.
- 진영 색상·장비: `character.py` 상단의 `CT` / `T` 딕셔너리.
- 자세: `poses.py`의 `RIFLE_GRIP` / `PISTOL_GRIP` / `KNIFE_GRIP`과 방향 벡터만 바꾸면 IK가 팔을 다시 풉니다. 손 소켓은 라이플 자세에서 자동 재계산됩니다.
- 보행 사이클: `poses.py`의 `WALK` / `RUN` 테이블 (시간, 허벅지각, 무릎각, 발목각).

---

실물 형상을 참고한 오리지널 지오메트리입니다. Valve의 CS2 애셋을 추출하거나 변환한 것이 아닙니다.
