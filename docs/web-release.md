# itch.io 웹 업로드 안내

1. `Builds/*-itch.zip`이 업로드 파일입니다. 프로젝트 폴더나 소스 ZIP은 올리지 않습니다.
2. itch.io에서 새 프로젝트를 만들고 Kind of project를 **HTML**로 설정합니다.
3. ZIP을 업로드하고 **This file will be played in the browser**를 선택합니다.
4. 실행 방식은 **Click to launch in fullscreen**을 권장합니다. 페이지 내 실행이면 1600×900 및 전체화면 버튼을 사용합니다.
5. 현재는 PC용으로 검증하는 프로토타입이므로 Mobile friendly는 체크하지 않습니다.
6. 처음에는 Draft로 저장하고 업로드된 페이지에서 팀 선택, 맵 실행, 경기 건너뛰기를 확인한 뒤 공개합니다.
7. 소개문은 `docs/discord-introduction-ko.txt`에 20줄로 준비했습니다. 팀 육성은 향후 계획입니다.

웹판에서는 종료 버튼이 메인 메뉴로 돌아갑니다. 해상도 옵션은 관전 화면의 렌더 해상도를 조절하며, 브라우저 크기는 사용자가 조절합니다. 대회 저장은 해당 브라우저의 사이트 저장소에 남습니다.

## 다시 빌드하기

PowerShell에서 프로젝트 루트를 기준으로 실행합니다.

```powershell
./Tools/BuildWeb.ps1
```

Unity 6000.6.0f1의 Web Build Support가 필요합니다. 스크립트가 Assets/Packages/ProjectSettings를 임시 프로젝트에 복사한 뒤 WebGL로 빌드하므로 열려 있는 원본 에디터의 플랫폼을 바꾸지 않습니다. 결과 폴더와 ZIP은 `Builds/`, 로그 위치와 임시 프로젝트 경로는 `Builds/latest-web-build.json`에 기록됩니다. 빌드 실패 시 ZIP을 만들지 않습니다.

WebGL2, Gzip 및 Decompression Fallback을 사용합니다. ZIP 최상위에 index.html이 있으며, itch.io 기본 파일 수·크기 제한을 포장 단계에서 검사합니다. 맵 바이너리는 생성된 Resources/PublicMaps/*.bytes로 포함해 브라우저의 직접 파일 접근 제한을 피합니다. Nanum Gothic 글꼴과 OFL 라이선스를 포함합니다.

근거: [itch.io HTML5 업로드 안내](https://itch.io/docs/creators/html5), [Unity StreamingAssets 접근 안내](https://docs.unity.com/en-us/engine/6000.6/manual/building-and-publishing/streaming-assets), [Nanum Gothic 원본과 OFL](https://github.com/google/fonts/tree/main/ofl/nanumgothic).
