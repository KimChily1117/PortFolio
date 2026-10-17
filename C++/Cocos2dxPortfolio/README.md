# 에픽세븐 리소스 기반 Cocos2d-x 턴제 전투

Cocos2d-x 4 / C++17 로컬 프로토타입입니다. 설치본을 REA와 리소스 파서로 분석하고 원본 배경·움직이는 초상화·Spine 모델·UI·S3 연출·사운드를 별도 C++ 코드에 연결합니다. 방치 수입·자동 반복 전투·전투 중 강화 패널은 제거했습니다.

현재 기본 흐름은 **로비 → 지도 → 스테이지 → 4인 편성 → 수동 턴 전투 → 결과**입니다. 원본 서버·전체 스킬·정확한 능력치·상태이상을 복원한 프로젝트는 아닙니다. 관찰과 추론, 구현 범위는 [구조·분석 문서](docs/EPICSEVEN_RECONSTRUCTION.md)에 구분했습니다.

## 저장소 안내

이 프로젝트는 `PortFolio/C++/Cocos2dxPortfolio`에 정리했습니다. [포트폴리오 소개와 실기기 영상](docs/portfolio/README.md), [구현 지원과 리소스 출처](docs/portfolio/CREDITS.md)를 먼저 볼 수 있습니다.

소스·테스트·분석 문서·선별 시연 자료를 포함하며, 엔진과 외부 도구(`.deps`), 빌드 결과물, APK, 추출·변환한 실행 리소스(`Resources/reference`)는 포함하지 않습니다. 따라서 저장소를 내려받는 것만으로 참조 리소스를 사용하는 모든 화면과 테스트가 재현되지는 않습니다. 아래 엔진 비의존 테스트는 별도 게임 리소스 없이 실행할 수 있습니다.

Visual Studio 2022 C++ 도구와 CMake가 설치된 환경에서, 이 프로젝트 폴더를 기준으로 실행합니다.

```powershell
cmake -S . -B build-core -G "Visual Studio 17 2022" -A Win32 -DASTER_CORE_ONLY=ON
cmake --build build-core --config Release
ctest --test-dir build-core -C Release --output-on-failure
```

`ASTER_CORE_ONLY`는 전투 규칙과 스킬 시간표 테스트 2개를 구성합니다. 저장·Spine·WebP 검사는 아래 전체 빌드와 해당 의존성 또는 로컬 fixture가 필요합니다. Python 스킬 변환기 검사 역시 `.deps/epicseven-assets/tools`의 외부 변환 도구를 필요로 하며 엔진 bootstrap만으로 준비되지 않습니다. 2026-10-15 실행 기록은 [통합 검증 문서](docs/FINAL_VALIDATION.md), 이번 경로 정리 후 확인 결과는 [저장소 정리 검증](docs/REPOSITORY_IMPORT.md)에 별도로 보존합니다.

## 실행과 조작

- Windows: `Run-Reference-Game.cmd` 또는 `Run-Windows.cmd`.
- 전투: 우측 하단 S1/S2/S3 선택 후 대상을 터치합니다. 전체 대상 스킬은 버튼 한 번으로 사용합니다. 아군 차례에서 입력하지 않으면 전투가 기다립니다.
- 자동/배속/메뉴는 우측 상단에 있습니다. 자동은 기본 OFF이며 승패 후 반복 출전하지 않습니다.
- 영웅 컬렉션: 여성 필터, 이름·ID 검색, 움직이는 상세와 표정, 컷인·지원 S3 미리보기.
- 사운드 라이브러리: 원본 샘플명·영웅 ID·뱅크 검색, 음악·음성·효과음 분류와 개별 재생.
- 기준 해상도 1280×720. 다른 화면 비율은 레터박스로 보존합니다.

## 구현 범위

| 기능 | 현재 범위 |
|---|---|
| 전투 | 4 대 4, 속도 기반 CR, 수동 타깃, 스킬·쿨타임 검증, 선택적 자동, 단일 승패 정산 |
| S3 | 아카테스·아라민타·클라릿사·도미니엘 4종의 SEQANI/CFX/camera 연결 |
| 전장 | 원본 atlas의 숲·성소·폐허, 원본 캐릭터·적 Spine 모델 |
| HUD | 원본 CSB에서 확인한 행동 순서, 하단 행동자 HP, 우측 하단 3개 스킬과 원본 아이콘 |
| 갤러리 | 488 애니메이션 세트, 427 WebP 컷인. 짧은 컷인과 전체 S3는 별개 |
| 사운드 | 전체 해독 가능한 뱅크 샘플과 설치 영상의 오디오 카탈로그, Ogg 지연 재생 |
| 저장 | 기존 3인 v1 보존 → 4인 v2 이전, 기존 적립금 1회 이전, 이후 방치 수입 없음 |

S3 264개 시퀀스 중 실행용 연결은 4개입니다. 현재 모델과 맞지 않는 동작, 미확인 카메라 보간, 일부 원작 셰이더·파티클 확장, 미해독 DB/서버 전투 규칙은 제한으로 기록합니다. 원본과 동일한 전체 게임 복원이 완료됐다는 뜻은 아닙니다.

## 빌드

엔진과 변환 자료는 `.deps`, 실행용 원작 리소스는 `Resources/reference`에 두며 Git에서 제외합니다. 별도 로컬 자원이 있어야 분석 대상 화면이 재생됩니다.

```powershell
python tools/bootstrap.py
python tools/bootstrap-webp.py
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1
```

Windows는 Visual Studio 2022 C++ 도구와 CMake/Python을 사용하며 Win32로 빌드합니다. 실행 파일만 옮기지 말고 출력 폴더와 Resources를 함께 사용하세요. 리소스 증분 복사는 변환 중 임시 파일을 제외합니다.

Android는 JDK11, Gradle6.7.1, AGP4.2.2, NDK21.0.6113669, SDK33, CMake3.18.1을 사용합니다. arm64-v8a, minSdk24입니다. 전체 자료의 빌드 중간 파일은 공간이 넉넉한 드라이브를 지정할 수 있습니다.

```powershell
./tools/build-android.ps1 -PrepareOnly -BuildRoot D:\CodexBuilds\Cocos2dxPortfolio\android
./tools/build-android.ps1 -BuildRoot D:\CodexBuilds\Cocos2dxPortfolio\android -Install
```

설치는 기존 패키지 `com.portfolio.asteridle`의 데이터를 유지하는 업데이트입니다. 기기 식별자와 로컬 경로는 Git에 넣지 않습니다. [Android 빌드·설치 검증](docs/analysis/ANDROID_DEPLOYMENT.md)을 참고하세요.

## 코드와 증거

- [포트폴리오 구조·기술 분석](docs/EPICSEVEN_RECONSTRUCTION.md)
- [원본 EXE의 REA 함수·참조·의사코드](docs/analysis/ORIGINAL_BINARY_ANALYSIS.md)
- [S3 변환 및 전투 연출](docs/analysis/SKILL_PIPELINE_ANALYSIS.md)
- [로비 CSB·화면 흐름](docs/analysis/LOBBY_FLOW_ANALYSIS.md)
- [전투 HUD·배경 리소스](docs/analysis/BATTLE_UI_ANALYSIS.md)
- [전투 모델·저장 마이그레이션](docs/analysis/BATTLE_LOGIC_ANALYSIS.md)
- [사운드 추출·재생·출처](docs/analysis/AUDIO_PIPELINE_ANALYSIS.md)
- [라이선스와 자체 구현/원작 자산 구분](THIRD_PARTY.md)

`GameModel`은 렌더러와 분리한 전투 규칙, `GameScene`은 입력과 이벤트 표시, `SkillPresentation`은 카메라·이펙트·컷인의 합성, `BattleHud`는 snapshot 표시, `ReferenceAudio`는 음원 수명 관리를 담당합니다. `Resources/art`와 `heroes.json`은 이전 ASTER 시안 자료이며 현재 전투 정의는 4인 `GameModel`과 참조 스킬 카탈로그를 사용합니다.
