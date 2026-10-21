> 이 폴더는 초기 2인 프로토타입 이력입니다. 최신 1~4인 서버 검증 통합본은 [Project Dawn — 바칼 협동 레이드](../ProjectDawnIntegration/README.md)를 참고하세요.

# DNF Raid Demo

Unity 2022.3.16f1 / C# / Windows x64 / Mono · v0.1.2

최대 2인이 플레이하는 호스트 권한 방식의 협동 액션 데모다. 기본 3타, Q/E/R 스킬, 회피·점프, 보스 패턴 6종, 2페이즈·무력화·승패를 구현했다. 네트워크는 JSON UDP V3이며 전투 60Hz, 입력 최대 30Hz, 스냅샷 최대 20Hz로 동작한다.

이 폴더에는 실제 Unity 프로젝트와 빌드·검사 스크립트를 포함한다. 강의자료·역공학 보고서·녹화 영상은 포함하지 않는다.

## 열기와 빌드

1. Unity Hub에 `UnityProject` 폴더를 추가하고 **2022.3.16f1**로 연다.
2. Windows Build Support를 준비한다.
3. `Assets/Scenes/Demo.unity`를 열고 Play하거나, 아래 명령을 실행한다.

```powershell
# 이 README가 있는 폴더에서 실행
.\BuildDemo.ps1
# Unity 설치 위치가 다르면 지정
.\BuildDemo.ps1 -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.16f1\Editor\Unity.exe'
```

빌드 결과는 `Build/DnfRaidDemo.exe`, 기록은 `Logs/`에 생성된다. Unity 메뉴 `DNF Demo/Build Windows Demo`로도 빌드할 수 있다. 생성된 빌드·캐시·로그는 Git에서 제외한다.

## 실행과 조작

한 프로세스에서 호스트를 시작하고 다른 프로세스에서 같은 주소·포트로 참가한다. 같은 PC 검토 주소는 `127.0.0.1`, 기본 포트는 UDP `27077`이다. 기존 통신 검증 범위는 같은 PC의 loopback과 독립 EXE 두 개다.

| 기능 | 입력 |
|---|---|
| 이동 / 기본공격 | WASD 또는 방향키 / J |
| 스킬 / 각성 | Q / E / R |
| 회피 / 점프 | Space / K 또는 C |
| 도움말 / 판정 범위 | F1 / F2 |

지면 전용 충격파는 점프로 회피할 수 있다. 일반 폭발·운석은 공중에서도 맞는다.

## 코드

| 파일 | 책임 |
|---|---|
| [RaidSimulation.cs](UnityProject/Assets/Scripts/RaidSimulation.cs) | 전투 상태, 판정·피해, 스킬, 점프, 보스 패턴 |
| [RaidNetwork.cs](UnityProject/Assets/Scripts/RaidNetwork.cs) | UDP 연결, 입력 카운터·ACK, 공개 상태 전달 |
| [RaidMotion.cs](UnityProject/Assets/Scripts/RaidMotion.cs) | 상태·경과 시간에 따른 클립과 프레임 선택 |
| [RaidDemoApp.cs](UnityProject/Assets/Scripts/RaidDemoApp.cs) | 입력, 화면·HUD·음향, 실행 모드 |
| [RaidShowcase.cs](UnityProject/Assets/Scripts/RaidShowcase.cs) | 일반 입력을 사용하는 자동 시연 명령 |
| [Editor 검사 코드](UnityProject/Assets/Editor) | 전투·모션·실제 UDP 검사 |

`Assets/Resources/Art`에는 데모 실행에 사용하는 프레임과 manifest가 있다. `Packages`와 `ProjectSettings`, Unity `.meta`도 함께 유지한다.

## 검사

Unity 메뉴 `DNF Raid Demo/Run Simulation Checks`, `DNF Raid Demo/Run Motion Checks`에서 전투·모션 검사를 실행한다. 실제 UDP 검사는 Editor 정적 메서드 `NetworkChecks.Run`을 사용한다. 빌드 후에는 다음 스크립트로 두 EXE 연결과 실행 상태를 확인할 수 있다.

```powershell
.\TestNetwork.ps1
.\SmokeTest.ps1
```

## 제작과 리소스

사용자가 목표·2인 협동·점프 회피·모션 기준을 제시했고 AI 코딩 에이전트가 구현·수정·검증을 지원했다. 전투 규칙과 통신은 데모용 신규 구현이며 원작 서버·게임 소스를 복원한 결과가 아니다. 원본 그림 제작은 기여도에 포함하지 않는다. [리소스 출처](THIRD-PARTY.md)
