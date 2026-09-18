# 8팀 대회 로스터와 스탯 — 2026-09-18

## 로스터

| 팀 | 선수 5명 | IGL | 오퍼 |
|---|---|---|---|
| Spirit | donk, sh1ro, magixx, tN1R, zont1x | magixx | sh1ro |
| Falcons | NiKo, m0NESY, TeSeS, kyousuke, karrigan | karrigan | m0NESY |
| The MongolZ | bLitz, 910, cobrazera, tikuak, DarkMeister | bLitz | 910 |
| Vitality | apEX, ZywOo, flameZ, ropz, mezii | apEX | ZywOo |
| Aurora | XANTARES, woxic, Jimpphat, kyxsan, Wicadia | kyxsan | woxic |
| FURIA | FalleN, KSCERATO, yuurih, YEKINDAR, molodoy | FalleN | molodoy |
| MOUZ | torzsi, Spinx, xertioN, PR, xelex | xertioN | torzsi |
| FUT | Krabeni, cmtry, dem0n, dziugss, xfl0ud | Krabeni | cmtry |

MongolZ는 사용자 지정 **FISSURE Playground 3 출전 명단**이다. 9월 15일 tikuak/DarkMeister가 벤치로 이동했으므로 현재 확정 주전 5인이라고 해석하면 안 된다. Vitality의 jL은 임시 출전 이력이므로 고정 로스터에는 mezii를 유지한다. FUT의 SunPayus 협상 보도는 확정 이적이 아니므로 cmtry를 유지한다.

## 수치의 의미

새 6팀은 포지션별 게임용 능력치 기준값에 최근 팀 성적을 참고한 에임 보정을 적용했다. 실제 선수의 유틸·무빙·카리스마·침착함을 계측한 값이 아니다. 통일된 최근 3개월 개인 통계 전체를 확보하지 못했으므로 HLTV Rating을 수학적으로 변환했다고 주장하지 않는다. 기존 Spirit/Falcons 능력치는 유지했다.

- 에임: 선수 역할/실력을 참고한 설계 기준값 + 최근 팀 폼 보정. 범위 65~98.
- 최근 폼 보정: MOUZ +2, FUT +1, Vitality 0, FURIA/Aurora -1, MongolZ -3. 팀 결과를 개인 능력에 약하게 반영하는 초기 튜닝값이다.
- 근거 요약: MOUZ BLAST Bounty S2 우승·Porto 준우승, FUT EWC 준우승, FURIA EWC 4위, Vitality Porto 3~4위·EWC 5~8위, Aurora Porto 7~8위·EWC 9~16위, MongolZ EWC 9~16위 및 최근 부진.
- 유틸: IGL 90, 앵커 86, 그 외 80을 기본값으로 사용.
- 무빙: 엔트리 에임+2, 그 외 에임-3을 기본으로 하되 일부 선수는 개별 조정. 이동속도는 바꾸지 않는다.
- 침착함: 앵커 91, 오퍼/IGL 88, 그 외 82를 기본값으로 사용.
- 카리스마와 총기 숙련도도 게임용 추정치다. 실제 인물의 성격 평가가 아니다.
- 시드: Spirit, MOUZ, Falcons, FUT, Vitality, FURIA, Aurora, MongolZ. 최근 성적을 참고한 게임 시드이며 공식 VRS 순위를 그대로 복제한 것이 아니다.

원본과 선수별 기준값/보정은 `Assets/Resources/tournament_roster.json`에 기록했다. 밸런스 측정 후 조정할 수 있다.

## 확인 출처

- [MongolZ 벤치 변경](https://www.hltv.org/news/45522/the-mongolz-bench-darkmeister-tikuak), [FPG3 cobrazera 출전](https://www.hltv.org/news/45441/cobrazera-returns-to-the-mongolz-starting-five-for-fissure-playground-3)
- [Vitality 로스터](https://www.hltv.org/team/9565/vitality), [jL 임시 출전](https://www.hltv.org/news/45239/official-jl-to-join-vitality-as-stand-in)
- [Aurora 로스터](https://www.hltv.org/team/11861/aurora)
- [FURIA 로스터](https://www.hltv.org/team/8297/furia)
- [MOUZ 로스터](https://www.hltv.org/team/4494/mouz), [xertioN 지휘 체계](https://www.hltv.org/news/45527/torzsi-on-mouzs-new-structure-xertion-and-sycrone-want-the-best-out-of-me), [Bounty 우승](https://www.hltv.org/news/45226/mouz-subdue-spirit-to-win-blast-bounty-s2)
- [FUT 로스터](https://www.hltv.org/team/13286/fut), [Krabeni 연장](https://www.hltv.org/news/45476/krabeni-pens-contract-extension-with-fut), [Spirit–FUT EWC 결승](https://www.hltv.org/news/45377/spirit-beat-fut-3-1-to-win-esports-world-cup)
- [ESL 공식 Major 대회 형식](https://eslfaceitgroup.com/press/the-major-makes-a-grand-return-to-the-cathedral-of-counter-strike-as-iem-cologne-major-2026-begins/)

## 현재 선수 수치

순서: 에임 / 유틸 / 무빙 / 카리스마 / 침착함. 모두 게임용 추정치.

| 팀 | 선수 | 에임 | 유틸 | 무빙 | 카리스마 | 침착함 |
|---|---|---:|---:|---:|---:|---:|
| spirit | donk | 98 | 78 | 96 | 70 | 87 |
| spirit | sh1ro | 94 | 85 | 85 | 76 | 97 |
| spirit | magixx | 79 | 91 | 79 | 89 | 91 |
| spirit | tN1R | 88 | 81 | 88 | 65 | 83 |
| spirit | zont1x | 85 | 89 | 81 | 69 | 93 |
| falcons | NiKo | 97 | 86 | 89 | 84 | 90 |
| falcons | m0NESY | 97 | 81 | 96 | 72 | 92 |
| falcons | TeSeS | 84 | 91 | 85 | 74 | 89 |
| falcons | kyousuke | 94 | 77 | 94 | 61 | 81 |
| falcons | karrigan | 72 | 94 | 78 | 98 | 90 |
| mongolz | bLitz | 83 | 90 | 85 | 92 | 88 |
| mongolz | 910 | 86 | 80 | 83 | 73 | 88 |
| mongolz | cobrazera | 79 | 86 | 76 | 72 | 91 |
| mongolz | tikuak | 74 | 80 | 76 | 65 | 82 |
| mongolz | DarkMeister | 74 | 86 | 71 | 65 | 91 |
| vitality | apEX | 83 | 90 | 85 | 97 | 88 |
| vitality | ZywOo | 98 | 80 | 95 | 82 | 88 |
| vitality | flameZ | 92 | 80 | 94 | 76 | 82 |
| vitality | ropz | 94 | 86 | 91 | 77 | 91 |
| vitality | mezii | 87 | 86 | 84 | 75 | 91 |
| aurora | XANTARES | 94 | 80 | 96 | 80 | 82 |
| aurora | woxic | 86 | 80 | 83 | 79 | 88 |
| aurora | Jimpphat | 90 | 86 | 87 | 73 | 91 |
| aurora | kyxsan | 81 | 90 | 83 | 91 | 88 |
| aurora | Wicadia | 89 | 86 | 86 | 74 | 91 |
| furia | FalleN | 80 | 90 | 77 | 97 | 91 |
| furia | KSCERATO | 94 | 86 | 91 | 80 | 91 |
| furia | yuurih | 89 | 86 | 86 | 77 | 91 |
| furia | YEKINDAR | 87 | 80 | 89 | 81 | 82 |
| furia | molodoy | 91 | 80 | 88 | 73 | 88 |
| mouz | torzsi | 91 | 80 | 88 | 78 | 88 |
| mouz | Spinx | 93 | 86 | 90 | 79 | 91 |
| mouz | xertioN | 90 | 90 | 92 | 91 | 88 |
| mouz | PR | 85 | 86 | 82 | 72 | 91 |
| mouz | xelex | 87 | 80 | 89 | 68 | 82 |
| fut | Krabeni | 85 | 90 | 87 | 90 | 88 |
| fut | cmtry | 85 | 80 | 82 | 70 | 88 |
| fut | dem0n | 90 | 80 | 92 | 74 | 82 |
| fut | dziugss | 88 | 86 | 85 | 73 | 91 |
| fut | xfl0ud | 88 | 86 | 85 | 84 | 91 |
