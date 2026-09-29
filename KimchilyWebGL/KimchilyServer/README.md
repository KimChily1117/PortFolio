# KimchilyServer — C# 기반 기능 + TypeScript 게임 규칙

Project Dawn의 직렬 작업 큐·예약 작업을 재사용한 .NET 10 WebSocket 서버입니다. 방·채팅·플레이어 위치를 관리하고 **서버에서도 제작자가 작성한 TypeScript 규칙을 실행**합니다. Chili Island의 발판·인원·시간·승리 판정은 C#에 하드코딩되어 있지 않습니다.

## 수정할 파일

| 파일 | 역할 |
| --- | --- |
| `../KimchilyCreator/ServerScripts/chili-portal/PortalRules.ts` | 발판 배치, 필요 인원, 점유·만료 조건, 시간, 성공·재도전 |
| `../KimchilyCreator/Assets/Demos/ChiliIsland/Scripts/PortalGarden.ts` | 클라이언트 연출·게임 HUD |
| `src/Kimchily.Server.Core/RoomHub.cs` | 방 수명, 순서 보장, 위치·명령 검증, 상태 전송 |
| `src/Kimchily.Server.Core/ScriptRoomGame.cs` | 게임을 모르는 범용 Jint 실행기와 JSON 제한 |
| `src/Kimchily.Server.Core/ApprovedScriptCatalog.cs` | 로컬 번들의 ID·월드·SHA-256 검증 |
| `games/<scriptId>/<scriptHash>.json` | 실행 승인된 불변 JavaScript 번들 |
| `tools/compile-script.ps1` | TS 검사·컴파일·번들 및 Unity identity 생성 |

수정·실행은 `E:\task\KimchilyWebGL`에서 하고, 검증 후 변경분을 Git 관리 폴더에 반영합니다. 원본 Project Dawn, Lyra, 원본 Unity 프로젝트를 수정하지 않습니다.

## 실행

```powershell
Set-Location 'E:\task\KimchilyWebGL\KimchilyServer'
.\tools\run.ps1 -Task install
.\tools\compile-script.ps1
.\tools\run.ps1 -Task test
.\tools\run.ps1 -Task start
```

- .NET SDK 10.0.401, 대상 net10.0. 설치기는 공식 ZIP의 SHA-512를 확인해 서버의 `.tools`에 설치합니다.
- Jint 4.16.2·Acornima 1.7.0은 고정 버전입니다. `NuGet.Config`는 SDK `Tools~/Vendor`의 로컬 NuGet 파일만 참조합니다.
- TS 컴파일에는 SDK의 로컬 Node와 TypeScript 5.9.3을 재사용합니다. 최초 설치는 Unity의 **Kimchily / TypeScript / Install or Repair Compiler**입니다.
- 서버 소스 빌드에는 같은 KimchilyWebGL 폴더의 KimchilySDK와 Creator 서버 TS가 필요합니다. 빌드 결과는 함께 복사된 DLL·games로 실행할 수 있습니다.
- Node는 빌드에만 필요합니다. 실행 중인 C# 서버에서는 Jint가 JS를 해석합니다.

`http://127.0.0.1:8790/`는 유지 중인 **매칭 전 웹 로비 채팅**입니다. 두 탭에서 다른 닉네임으로 채팅을 확인할 수 있습니다. Unity 게임은 게시 서버의 8788 포트에서 실행합니다.

같은 Wi-Fi의 폰 시연:

```powershell
# PC의 실제 IPv4와 게시 페이지 origin을 사용합니다.
.\tools\run.ps1 -Task start -BindAddress 0.0.0.0 -Port 8790 `
  -AllowedOrigin @('http://127.0.0.1:8788','http://localhost:8788','http://192.168.0.4:8788')
```

폰에서는 PC의 LAN 주소를 사용합니다. 127.0.0.1은 폰 자신입니다. 방화벽 설정은 변경하지 않습니다. Ctrl+C로 종료하며 기존 게시 서버 8788/8787, Project Dawn TCP 8080·UDP 8081은 유지합니다.

## TS 규칙 배포

1. `PortalRules.ts`의 `config.holdSeconds: 3`을 예를 들어 `5`로 바꿉니다.
2. `tools/compile-script.ps1`을 실행합니다. 타입 오류가 있으면 배포 파일을 만들지 않습니다.
3. JS SHA-256을 파일명으로 하는 **새 번들**과 `PortalRuleIdentity.ts`가 생성됩니다. 기존 번들은 덮어쓰지 않습니다.
4. Creator에서 변경된 identity와 클라이언트 TS를 포함한 월드 콘텐츠를 다시 빌드·게시합니다.
5. 새 월드가 정확한 scriptId + scriptHash를 watch로 요청하면 서버는 월드와 코드를 검사하고 방별 VM을 생성합니다.

run.ps1은 `--Realtime:ScriptsRoot <원본 games 절대경로>`를 지정합니다. 새 번들을 추가하면 신규 방의 최초 watch에서 읽으므로 **규칙 변경에 C# 재빌드·서버 재시작은 필요하지 않습니다.** 기존 방의 코드와 상태는 바뀌지 않습니다. 카탈로그 전체를 메모리에 적재하는 무제한 캐시는 없습니다.

독립 배포 기본 경로는 `AppContext.BaseDirectory/games`입니다. 새 번들을 해당 경로에 배치하거나 `--Realtime:ScriptsRoot` / `Realtime__ScriptsRoot`로 지정합니다. 폴더 배치 권한이 코드 승인 권한입니다. 클라이언트가 파일 경로나 JS를 보내는 업로드 API는 없습니다. 해시는 내용 일치를 검증하며 작성자 서명은 아닙니다.

현재 도구는 단일 CommonJS 모듈을 컴파일하며 서버 TS의 다른 파일 import/require는 지원하지 않습니다. 서버 TS는 Unity Behaviour importer와 구분하기 위해 Assets 밖의 ServerScripts에 둡니다.

## 판정과 동기화

```text
Unity 로컬 이동 → 초당 10회 pose
 → C# RoomHub: 발신자/번호/빈도/이동량 검증
 → 서버 TS reduce: 점유·인원·서버 시간으로 상태 계산
 → C# 호스트: 변경 JSON에 단조 증가 version 부여
 → 같은 방 전체에 game 이벤트
 → PortalGarden.ts: Unity 문구·진행률·발판·포털 표시
```

게임은 create와 reduce를 export합니다. 입력은 서버 시각 nowMs, 정렬된 참가자, 검증된 pose·실제 수신 시각, 서버가 정한 요청자 ID입니다. C# 객체·연결·파일을 넘기지 않습니다. 이전 상태와 입력은 JSON으로 복사합니다.

RoomPump는 20ms마다 직렬 작업 큐를 처리하고 게임 tick은 최대 초당 10회입니다. 위치·입퇴장·명령 때도 규칙을 실행합니다. 패킷이 끊겨도 tick이 만료를 판단합니다. 모든 연결이 같은 직렬 큐를 이용하므로 한 방의 상태를 동시에 쓰지 않습니다.

현재 PortalRules.ts는 시작 인원 1~4명을 고정하고 첫 N개의 발판을 요구합니다. XZ 반경 1.1m, 높이 ±1.5m, 접지 상태, 1.2초 이내 pose를 모두 검사합니다. 한 사람은 한 발판만 점유하고 겹친 후보는 playerId 순서로 선택합니다. 모두 3초 연속 점유하면 성공하며 비거나 pose가 만료되면 진행을 초기화합니다. 시간은 폰이 아닌 서버 nowMs로 계산합니다. 남은 시간은 100ms 단위로 올림합니다. 성공은 reset 또는 빈 방 삭제까지 유지하고 늦은 입장은 joined.game으로 받습니다.

세부 메시지는 [통신 규격](docs/protocol.md), 원본 재사용 범위는 [재사용 기록](docs/reuse.md)에 있습니다.

## 채팅·캐릭터

- worldId + revisionId + roomId별 방, 방당 8명, 서버 발급 연결 ID.
- 한글 채팅과 최근 20개 기록. Unity Canvas/TMP 채팅창·캐릭터 위 말풍선.
- WebGL은 JS WebSocket 전송, Editor/네이티브는 ClientWebSocket을 사용합니다.
- 원격 위치는 보간합니다. 채팅창을 열면 캐릭터 조작을 멈추고 닫으면 복원합니다.
- 퇴장·월드 전환 때 연결·원격 캐릭터·게임 상태를 정리합니다. 마지막 퇴장 때 방과 VM도 해제합니다.
- /health, /api/rooms, /ws 및 매칭 전 웹 로비를 제공합니다.

## 제한과 범위

범용 실행기는 호출당 50,000문장, 100ms, 8MiB 할당량, 재귀 깊이 48, 배열 길이 16,384, 정규식 25ms를 제한합니다. 상태는 UTF-8 16KiB, 깊이 12, 값 1,024개 이내의 JSON 객체입니다. 명령 payload는 UTF-8 1,024바이트이며 전체 수신 패킷 4,096바이트 제한도 적용합니다.

8MiB는 **호출별 할당 제한**이며 영구 VM 힙의 총량 제한이 아닙니다. 제한·승인 카탈로그는 다수의 악성 코드 실행에 필요한 OS 프로세스 격리를 대신하지 않습니다. 데모는 운영자가 승인한 코드만 실행합니다.

사용자 명령에서 JS 오류를 던지면 명령을 거부하고 기존 상태를 보존합니다. 실행 예산 초과·잘못된 출력·tick 오류는 해당 방의 게임만 중단하고 다른 방·채팅은 유지합니다. 오류가 난 게임은 새 방에서 시작합니다.

이동 물리는 클라이언트가 계산합니다. 서버 범위·순서·이동량 검증은 지형 충돌까지 시뮬레이션하는 서버 물리가 아닙니다. 게스트 닉네임·Origin 검사는 계정 인증이 아닙니다. 상태 영속 저장·계정·인터넷용 HTTPS/WSS·세션 복구는 이번 범위에 없습니다.

## 검증

검사는 별도 loopback 포트를 사용하고 사용자 서버를 종료하지 않습니다. `Artifacts/checks/<UTC>`에 결과와 Kestrel 로그를 남깁니다.

2인 발판·시간·이탈·만료·재도전·늦은 입장·실제 WebSocket 동기화를 검사합니다. 해시/월드 불일치, 파일 변조, JSON 크기·깊이·값 수, 무한 루프의 방 격리도 포함합니다. TS 복사본의 holdSeconds만 3→5로 바꿔 컴파일한 뒤 같은 C# 호스트에서 정확히 5초째 성공하는 검증과, 전혀 다른 counter 게임을 실행하는 범용성 검증이 있습니다.
