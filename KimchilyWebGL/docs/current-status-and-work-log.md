# Kimchily UGC 현재 상태와 전체 작업 이력

갱신일: **2026-10-06**. UGC 기능 기준 커밋: **`2672fce9c`**. 이 문서는 현재 실행 방법, 완료한 작업, 검증 근거와 남은 확인 사항을 정리한다. 이후 운영·가독성 변경은 아래 이력에 별도로 기록하고, 날짜가 붙은 과거 보고서의 버전·주소·테스트 수치는 당시 기록으로 보존한다.

10-06 가독성 갱신: 서버·SDK·공통 실행기의 C# 축약 문법과 한 줄 실행문을 일반 생성자·중괄호 블록·함수별 간격으로 정리했다. .NET·Unity 버전과 TS 게임 규칙은 유지했다. 서버 57개, managed 17개, TS VM 34개, Lua VM 16개, SDK Unity 45/57개, 공통 실행기 Unity 60/121개 검사가 통과했다. [C# 스타일 안내](csharp-style.md)와 [변경·검증 기록](reports/2026-10-06-csharp-readability.md)에 범위를 기록했다.

10-01 운영 갱신: Project Dawn과 UGC를 이 PC에서 실행하고 Windows 부팅 시 자동 실행 작업을 등록했다. ipTIME WireGuard VPN·외부 노트북용 피어·서버 IP 예약을 적용했다. 다른 장소에서의 접속 절차와 암호화된 VPN 프로필은 [RemotePlay 안내](../../RemotePlay/README.md)에 있다. 실제 외부 VPN handshake와 종료 후 WOL 부팅은 아직 사용자 환경에서 확인해야 한다. UGC 게임 코드와 기존 게시 revision은 변경하지 않았다.

## 현재 결과

Unity에서 모델과 TypeScript를 제작하고 WebGL 콘텐츠로 게시하면, QR로 들어온 여러 참가자가 닉네임·Unity TMP 채팅·캐릭터 말풍선·이동을 공유한다. Chili Island는 **첫 정원 동시 발판 → 포털 통과 → 다리 이동 → 두 번째 정원 릴레이 → ALL CLEAR → 재시작**까지 구현했다.

게임 판정은 서버 `PortalRules.ts`, HUD·버튼·연출은 클라이언트 `PortalGarden.ts`에 있다. 기존 C# 서버의 작업 큐를 재사용한 .NET 호스트가 방·통신·위치 검증·Jint 실행·상태 버전을 담당한다. Node.js는 TypeScript 빌드 도구이며 별도의 Node 게임 서버를 운영하는 구조는 아니다. 매칭 전 웹 로비 채팅도 유지한다.

## 작업 경로와 버전

| 구분 | 현재 기준 |
|---|---|
| 수정·실행·검증 | `E:\task\KimchilyWebGL` |
| Git 갱신·관리·커밋 | `E:\GItHub\PortFolio\KimchilyWebGL` |
| Unity 제작 프로젝트 | `KimchilyCreator` |
| 데모 씬 | `KimchilyCreator/Assets/Demos/ChiliIsland/Scenes/ChiliIsland.unity` |
| 공통 플레이어 | `KimchilyUnityRuntime` |
| Unity / 렌더러 | `6000.3.24f1` / Built-in / WebGL |
| Creator / TypeScript / Networking UPM | `0.1.0` / `0.3.0` / `0.4.0` |
| TypeScript 컴파일러 / Jint / Acornima | `5.9.3` / `4.16.2` / `1.7.0` |
| 서버 SDK / 타깃 | `.NET SDK 10.0.401` / `net10.0` |
| 게시 서버 / 실시간 서버 | `8788` / `8790` |

Task에서 작업한 변경만 검토해 Git 관리본으로 반영한 후 커밋한다. 캐시·빌드·로컬 게시 데이터·토큰·인증서는 커밋하지 않는다. 사용자 작업 씬 `Assets/World/MyWorld.unity`와 생성된 채팅 폰트의 직렬화 차이는 이번 데모 반영 대상에서 제외했다. 원본 Unity 2022/Android 기준본과 Project Dawn 원본은 보존한다. Lyra는 별도 관리 대상으로 이 UGC 작업에 포함하지 않는다.

## 완료한 작업 이력

| 날짜 / 커밋 | 완료 내용 | 상세 기록 |
|---|---|---|
| 09-18 | Kimchily 이름 정리, Creator SDK·게시·QR·Android 호스트 기반, 원본 자산 보존 | [이름 변경](reports/kimchily-rebrand-2026-09-18.md), [SDK 검증](reports/kimchily-sdk-unity-verification-2026-09-18.md), [보존 기록](../preservation/README.md) |
| 09-19 | TypeScript importer·타입 선언·Inspector 필드·Jint VM·코루틴·IDE 자동완성, Android에서 콘텐츠 재게시 | [TS 구현](reports/2026-09-19-typescript-runtime.md), [모바일 조작·서버](reports/2026-09-19-mobile-controls-server.md), [게시 복구](reports/2026-09-19-publish-recovery.md) |
| 09-20 | Unity 6/WebGL 복제본, 모델 게시 호환·3인칭 애니메이션, Expo/WebApp 홈·QR 흐름 | [WebGL 검증](reports/2026-09-20-webgl-clone-validation.md), [모델 호환](reports/2026-09-20-publish-model-compatibility.md), [애니메이션](reports/2026-09-20-third-person-animation.md), [Expo/WebApp](reports/2026-09-20-expo-webapp.md) |
| 09-25 / `d20e4e562` | KimchilyWebGL 소스를 포트폴리오 Git 저장소로 이주 | [이주 기록](../MIGRATION.md) |
| 09-29 / `83a89c970` | 새 환경의 TS 컴파일러 준비와 게시 서버 오류 안내 개선 | [제작·게시 가이드](webgl-guide.md) |
| 09-29 / `2cbb3d134` | Project Dawn 직렬 작업 큐·예약 작업 재사용, 독립 .NET 10 WebSocket 서버·방·채팅·웹 로비 | [서버 기반 구현](reports/2026-09-29-csharp-realtime-foundation.md), [재사용 범위](../KimchilyServer/docs/reuse.md) |
| 09-29 / `d55055e27` | QR/홈 닉네임, 자동 연결, Unity TMP 채팅·이름표·말풍선·원격 이동, 게시 월드 공통 적용 | [Unity 채팅 구현](reports/2026-09-29-unity-multiplayer-chat.md) |
| 09-29 / `1c856d134` | Chili Island 환경·소품 프리팹 14종, 전용 씬·포털 협동 데모·별도 빌드 클론 | [아트 구성](chili-island-art.md), [당시 검증](chili-island-validation.md) |
| 09-29 / `fed673dfa` | C# 게임 전용 판정·HUD를 TS로 이관, 범용 Room/Hud API, 승인된 서버 번들과 SHA-256 신원, 포트폴리오 설명·주석 | [TS 판정·동기화 설계](portfolio-typescript-multiplayer.md) |
| 09-30 / `2672fce9c` | 모바일 채팅/나가기 분리, 포털 입장 기록·다리·두 번째 정원 릴레이, 공통 위치 회전값 경계 수정 | [현재 검증](chili-island-validation.md), [시연 순서](multiplayer-demo.md) |
| 10-06 | C# 생성자·함수·제어문 가독성 정리, 편집기 스타일 설정, WebGL 관리 코드 검사 참조 보완 | [가독성 변경과 회귀 검사](reports/2026-10-06-csharp-readability.md), [작성 기준](csharp-style.md) |

09-30 문서 정리에서는 현재 상태·작업 이력을 이 문서로 묶고, 서버 통신 규격·프로젝트 안내·실행 가이드의 이전 1단계 상태 설명과 검증 수치를 정리했다. 실행 코드 변경이나 새 빌드·실기기 검증을 수행한 것으로 기록하지 않는다.

10-01에는 최신 Git `6340d2327`을 받은 후 Task에서 서버를 실행했다. 비대화형 Windows 작업으로 Dawn·게시·실시간 서버를 새로 띄워 확인했고, UGC의 실제 LAN Origin으로 WebSocket 두 연결의 입장·채팅·pose 전달·승인 TS 번들 초기화를 검증했다. 공개 Git에는 개인키 원본 대신 암호화 프로필과 Windows PowerShell 복호화 도구를 보관한다. 복호화 왕복·잘못된 암호/변조 거부·독립 구현 검증·피어 키 일치 검사를 통과했다. Unity 빌드나 57개 서버 테스트를 새로 실행한 것으로 합산하지 않는다.

## 현재 시연의 판정과 동기화

1. 같은 게시 revision과 `roomId`로 입장한다. 방에는 최대 8명이 접속하며 닉네임과 채팅은 서버 발급 참가자 ID에 연결된다.
2. 시작 시 필요한 인원을 1~4명으로 고정한다. 첫 N개 발판을 서로 다른 참가자가 3초 동안 동시에 점유하면 포털이 열린다.
3. 서버 TS는 접지·높이 ±1.5m·수신 후 1,200ms 이내 위치를 검사한다. 발판은 XZ 반경 1.1m다. 클라이언트가 완료 결과를 직접 보내지 않는다.
4. 열린 포털 `(0,0,10)`의 X ±1.35, Z ±0.85 통로에 들어온 참가자를 기록한다. 실제 다리를 걸어 같은 씬의 다음 구역으로 이동한다.
5. 별 `(-3,0,34)` → 달 `(3,0,34)` → 해 `(-3,0,38)` → 잎 `(3,0,38)`을 순서대로 1.5초씩 충전한다. 처음에는 시작 인원만큼 서로 다른 참가자가 기여하고, 이후 다인 플레이에서는 직전 담당자와 교대한다. 혼자서는 네 발판 모두 진행할 수 있다.
6. 점프·이탈·위치 만료는 현재 충전만 초기화한다. 완료된 릴레이 단계는 남는다. 네 번째 성공 후 `finished`와 축하 연출이 유지된다.
7. 늦은 참가자는 현재 상태를 받지만 릴레이에 기여하려면 포털을 통과해야 한다. 재시작은 두 미션·입장 목록·기여 기록을 지우고 첫 정원으로 돌아가게 한다. 다리 바닥은 유지된다.

호스트는 변경된 게임 상태 전체에 증가하는 `version`을 붙여 같은 방에 전달한다. TS가 상태를 읽어 TMP 문구·진행률·버튼·발판·포털을 표시한다. 이동은 클라이언트 물리와 서버의 범위·순서·이동량 검사 방식이며 서버가 지형 물리를 재현하는 구조는 아니다. 자세한 계약은 [통신 규격](../KimchilyServer/docs/protocol.md), 설명·그림은 [포트폴리오 문서](portfolio-typescript-multiplayer.md)에 있다.

## 모바일·공통 통신 수정

- 폭 600 CSS px 이하 **또는** 높이 600 CSS px 이하이면 DOM 상단 버튼과 Unity Canvas를 별도 행으로 배치한다. 채팅은 계속 Unity TMP UI이고 나가기/전체 화면은 웹 상단 버튼이다. 템플릿을 수정해 이후 공통 플레이어 빌드에도 적용한다.
- 정북 방향에서 Unity가 반환하는 작은 음수 회전값 때문에 위치 전체가 거절되던 문제를 보완했다. 서버는 yaw `[-0.006,360]`의 유한 입력을 받아 `[0,360)`으로 정규화한다. 다른 위치·이동량 검증은 유지한다. 이 변경은 공통 통신 처리이며 게임 판정은 TS에 남아 있다.
- 두 번째 정원은 기존 메시·재질을 재사용한다. 다리의 연속 충돌 바닥, 난간과 길 안내 조명, 완주 기념물을 추가했다. C# 아트 생성기는 에디터 제작 도구다.

## 현재 게시본과 재실행

| 항목 | 값 |
|---|---|
| World / Room | `chili-island` / `playground` |
| Revision | `webgl-20260930T014824479Z-2d905a55` |
| 규칙 ID | `chili-portal-ts-v1` |
| 규칙 JS SHA-256 | `0e7ac1ee9620519d7b9b55696fb7a03527260096a7f0c57a706cc7c784a1d8e4` |
| 월드 manifest SHA-256 | `cd2a2ec343f50c3006cd7b721d4fa83003adb328564bf3bc6b0eaeadfbd50b7d` |

[현재 QR·입장 페이지](http://192.168.0.4:8788/w/chili-island/webgl-20260930T014824479Z-2d905a55). 이 주소는 검증 당시 PC LAN 주소이며 서버가 켜져 있고 같은 네트워크에서 접근할 수 있어야 한다. Git 복제에는 실행기 빌드와 게시 저장소가 포함되지 않는다.

```powershell
Set-Location 'E:\task\KimchilyWebGL'
# 최초 환경 준비는 WebGL 가이드와 Server README의 설치 절차를 따른다.
# 게시 서버 시작
.\KimchilyPublish\tools\start.ps1
# 실시간 서버: 별도 PowerShell에서 실행하고 실제 PC LAN origin을 지정한다.
.\KimchilyServer\tools\run.ps1 -Task start -BindAddress 0.0.0.0 `
  -AllowedOrigin @('http://127.0.0.1:8788','http://localhost:8788','http://192.168.0.4:8788')
```

```powershell
Set-Location 'E:\task\KimchilyWebGL'
# SDK·런타임·WebGL 템플릿 변경 시 공통 플레이어 갱신. 같은 Runtime Editor와 동시 실행하지 않는다.
.\KimchilyUnityRuntime\tools\export_webgl.ps1
# 기존 데모 씬을 별도 빌드 클론에서 게시: 서버 TS 컴파일·identity 갱신도 포함한다.
.\KimchilyCreator\tools\build_chili_island.ps1 -Publish
# 규칙·공통 서버 변경 검증. 별도 loopback 서버를 사용한다.
.\KimchilyServer\tools\run.ps1 -Task test
```

데모 도구의 게시 응답은 `Artifacts/ChiliIslandBuild/Artifacts/chili-island-publish-result.json`이다. 일반 Creator 게시 응답 `KimchilyCreator/Artifacts/publish-result.json`과 구분한다. `-RegenerateArt`는 생성된 데모 자산을 다시 쓰므로 수작업 배치를 유지하려면 생략한다. 클라이언트 TS·서버 TS·씬 좌표를 함께 맞추고 규칙 변경 시 새 해시와 새 월드 revision을 게시한다. 기존 승인 번들과 게시 revision은 덮어쓰지 않는다.

## 검증과 근거

| 검증 시점 | 확인 결과 | 근거 / 범위 |
|---|---|---|
| 09-30 최종 서버 | **57 passed / 0 failed**, 경고·오류 0 | `KimchilyServer/Artifacts/checks/20260930-020458/results.json`; 3참가자 릴레이 7개와 회전 경계 1개 포함 |
| 09-30 콘텐츠 | 클라이언트 TS strict 검사, 공통 WebGL 빌드, 별도 Creator 클론의 씬 생성·빌드·게시 성공 | [현재 검증 기록](chili-island-validation.md) |
| 09-30 브라우저 | 최종 서버로 처음부터 두 미션 완주, ALL CLEAR, 오류 콘솔 0건, 채팅 연결 유지 | `Artifacts/portal-relay/verification.json`, `relay-complete.png`, `portal-crossing.png` |
| 09-30 화면 크기 | 390×844, 844×390, 896×520 모바일 배치; 1280×720 PC 배치 | 최종 896×520에서 toolbar y=0..72, Canvas y=72..520, 나가기 끝 y=58. `mobile-final-chat.png` |
| 09-30 사용자 확인 | “동시 3종까진 확인” | 기종·OS·브라우저·세부 범위 미기록. 2단계 확장 전 전달본에 대한 확인 |
| 09-29 TS 이관 | Unity EditMode 60, PlayMode 121; TS 컴파일러 20, facade 24, VM 34, 서버 번들 컴파일러 7; 당시 서버 49 통과 | [이전 전달본 검증](chili-island-validation.md). 09-30에 Unity 181개를 다시 실행한 것은 아님 |

`Artifacts`, `Builds`, `Library`, `.local`은 로컬 산출물이다. Git에는 재현할 소스·테스트·문서·승인 규칙 번들이 들어가며, 위 증거 파일과 스크린샷은 Task 작업본에 보관한다. 문서 갱신에서는 Markdown 링크·현재 해시·프로젝트 경로·Git 반영 범위를 검사하며 실행 테스트를 새로 수행한 것으로 합산하지 않는다.

## 남은 확인 사항

- 새 두 번째 미션을 실제 여러 휴대전화에서 처음부터 함께 완주하고 기종·OS·브라우저를 기록한다. 자동 3참가자 테스트와 브라우저 화면 크기 검사는 이를 대신하지 않는다.
- 재접속은 새 연결 ID를 사용한다. 포털 재통과와 참여 인원 부족 시 재시작 흐름을 실기기에서 확인한다.
- 이번 포털은 같은 게시 월드 안의 구역 이동이다. 다른 월드를 로드하는 전환, 계정·매칭 서비스·영속 진행 저장, 인터넷용 HTTPS/WSS 운영 배포는 구현 범위 밖이다.
- Unity 2022/Android의 이전 실기기 결과를 현재 Unity 6 네트워킹 빌드의 Android/iOS 네이티브 앱 검증으로 합산하지 않는다.

## 관련 문서

- [시연 순서](multiplayer-demo.md), [판정·동기화·포트폴리오 설명](portfolio-typescript-multiplayer.md), [아트 구성](chili-island-art.md), [검증 이력](chili-island-validation.md)
- [WebGL 제작·게시](webgl-guide.md), [LAN HTTPS](lan-webapp-guide.md), [모델 애니메이션](animation-import-guide.md), [기존 Android 사용법](mobile-world-guide.md)
- [서버 실행](../KimchilyServer/README.md), [통신 규격](../KimchilyServer/docs/protocol.md), [기존 C# 서버 재사용](../KimchilyServer/docs/reuse.md)
- [초기 플랫폼 분석](reports/kimchily-independent-platform-analysis-2026-09-18.md), [Creator·QR 범위](reports/kimchily-creator-sdk-qr-scope-2026-09-18.md), [Android 호스트 설계](reports/kimchily-android-host-architecture-2026-09-18.md)
- [Unity 6 검토](reports/2026-09-20-unity6-webgl-review.md), [SpringBone 빌드 수정](reports/2026-09-20-springbone-build-fix.md)
