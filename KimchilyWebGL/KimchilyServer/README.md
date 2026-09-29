# KimchilyServer — C# UGC 서버, 1단계

Project Dawn C# 서버의 작업 큐·예약 작업 코드를 재사용한 .NET 10 서버입니다.
WebSocket으로 같은 방에 입장하고 텍스트 채팅을 주고받는 첫 실행 단위를 제공합니다.
기존 게임 서버, DB, Unity 프로젝트 없이 이 디렉터리만으로 빌드·실행할 수 있습니다.

## 작업 위치와 SDK

- 수정·실행·검증: `E:\task\KimchilyWebGL\KimchilyServer`
- Git 갱신·변경분 병합·커밋: `E:\GItHub\PortFolio\KimchilyWebGL\KimchilyServer`
- .NET SDK **10.0.401**, ASP.NET Core/.NET 런타임 **10.0.12**, 대상 프레임워크 `net10.0`.
- `global.json`에서 SDK 계열을 고정합니다. 설치 도구는 Microsoft 공식 Windows x64 ZIP의
  SHA-512를 확인하고 이 서버의 `.tools/dotnet-10.0.401`에 설치합니다.
  시스템 PATH와 기존 .NET 3.1/7/8 설치는 변경하지 않습니다.
- 서버용 .NET SDK를 올리고 Unity에는 별도 `com.kimchily.networking@0.1.0` 패키지를 추가했습니다.
  기존 Creator SDK 0.1.0과 발행 콘텐츠 버전 계약은 유지합니다.

## 시작하기

PowerShell에서:

```powershell
Set-Location 'E:\task\KimchilyWebGL\KimchilyServer'
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task install
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task test
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task start
```

`http://127.0.0.1:8790/`를 두 브라우저 탭에서 열어 서로 다른 닉네임으로 입장합니다.
기본 월드는 `network-demo`, 게시 버전은 `v1`, 방 코드는 `playground`입니다.
한쪽에서 입력한 메시지와 입퇴장 상태가 다른 쪽에도 나타납니다. `Ctrl+C`로 종료합니다.
실행기는 빌드 실패 시 서버를 시작하지 않습니다. 별도 백그라운드 프로세스를 만들지 않습니다.

같은 네트워크의 휴대폰에서 테스트할 때는 명시적으로 LAN 바인딩을 사용합니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task start -BindAddress 0.0.0.0 -Port 8790
```

각 폰에서 `http://<PC의 LAN IPv4>:8790/`를 엽니다. `127.0.0.1`은 각 기기 자신을 뜻하므로
폰에는 PC의 LAN 주소를 사용해야 합니다. 그 주소로 연 화면의 **초대 주소**를 공유하면
월드·버전·방 코드가 함께 전달됩니다. 방화벽·라우터 설정은 실행기가 변경하지 않습니다.
클립보드 API를 사용할 수 없는 LAN HTTP에서도 선택 가능한 주소 입력란을 제공합니다.

기존 게시 서버 **8788**, 원본 Android 게시 서버 **8787**, Project Dawn의 TCP **8080**·UDP **8081**과
포트를 분리했습니다. 이 서버는 기존 게시 상태·토큰·월드 파일을 읽거나 바꾸지 않습니다.

다른 OS에서는 `global.json`에 맞는 SDK를 설치한 뒤:

```text
dotnet build KimchilyServer.slnx -c Release
dotnet tests/Kimchily.Server.Checks/bin/Release/net10.0/Kimchily.Server.Checks.dll
dotnet src/Kimchily.Server.Host/bin/Release/net10.0/Kimchily.Server.Host.dll --urls http://127.0.0.1:8790
```

현재 검증 환경은 Windows x64입니다. 외부 NuGet 패키지는 사용하지 않습니다.

## 구성

| 디렉터리 | 역할 |
| --- | --- |
| `src/Kimchily.Server.Core` | 재사용 작업 큐, UGC 방·참가자·채팅 규칙, 통신 계약 |
| `src/Kimchily.Server.Host` | ASP.NET Core WebSocket 접속, 20ms 작업 큐 처리, HTTP 상태 API |
| `src/Kimchily.Server.Host/wwwroot` | 모바일 폭을 지원하는 매칭 전 로비 채팅 데모 |
| `tests/Kimchily.Server.Checks` | 핵심 동작과 실제 Kestrel/WebSocket 통합 검사 |
| `tools` | SDK 설치·빌드·검증·실행 |

재사용 범위와 원본 해시는 [재사용 기록](docs/reuse.md), 메시지 규격과 오류는
[통신 규격](docs/protocol.md)에 정리했습니다.

## 구현한 동작

- `(worldId, revisionId, roomId)` 조합별 독립 방, 방당 최대 8명.
- 서버에서 발급하는 연결 ID, 닉네임, 입퇴장 알림, 현재 참가자 스냅샷.
- 한글 텍스트 채팅과 최근 20개 메시지. HTML은 화면에서 텍스트로 표시합니다.
- 명시적 퇴장, 소켓 종료 정리, 마지막 참가자 퇴장 시 빈 방 제거.
- 잘못된 메시지·필드·통신 버전 검사, 메시지 크기·발송 빈도·송신 큐 제한.
- `/health`, `/api/rooms`, 같은 출처 또는 명시적으로 허용한 WebGL 출처를 위한 `/ws`.

## Unity 인게임 채팅

기존 WebGL 실행기를 다시 빌드한 뒤 게시 서버의 `http://127.0.0.1:8788/player/`에서
월드를 실행하면 오른쪽 위 **채팅** 버튼이 나타납니다. 닉네임과 동일한 방 코드를 입력하면
같은 월드·버전의 다른 클라이언트와 채팅할 수 있습니다. 기본 서버는 현재 페이지의 호스트,
8790 포트입니다. **서버 연결 설정**에서 다른 WS/WSS 주소를 지정할 수 있습니다.

```powershell
# 별도 PowerShell에서 WebGL 실행기 재빌드
Set-Location 'E:\task\KimchilyWebGL\KimchilyUnityRuntime'
.\tools\export_webgl.ps1
```

- WebGL은 Unity 화면 위 HTML 패널과 브라우저 WebSocket을 사용합니다. 모바일 키보드는
  HTML 입력란이 담당하고, C# SDK가 접속·방 상태와 메시지를 관리합니다.
- Editor/네이티브 실행에서는 같은 SDK의 ClientWebSocket과 IMGUI 패널을 사용합니다.
- 채팅 패널을 열면 기본 캐릭터 조작을 멈추고, 닫으면 원래 켜져 있던 조작만 복원합니다.
- Runtime은 월드 준비 완료 시 해당 월드·버전으로 전환합니다. 퇴장 시 소켓을 닫고 기록을
  지웁니다. 로비 컨텍스트는 `lobby/v1`이며 자동 매치메이킹 기능은 아직 없습니다.
- Creator의 Play Mode에는 `editor-preview/v1` 채팅 패널을 추가합니다. 제작 씬·발행 번들에
  채팅 컴포넌트를 저장하지 않으므로 각 제작자가 씬을 수정할 필요가 없습니다.
- 독립 웹 로비와 대화하려면 로비의 월드·버전·방 코드를 Unity에 표시된 값과 맞춥니다.
  기본 내장 월드는 `demo/builtin-v1`입니다. 새로 연결할 때 닉네임은 같아도 서버 ID는 새로 받습니다.

실행 도구는 `http://127.0.0.1:8788`, `http://localhost:8788`만 추가로 허용합니다.
같은 Wi-Fi 휴대폰 시연에서는 실제 플레이어 페이지의 **origin**을 명시합니다.

```powershell
# 192.168.0.4는 예시이며 PC의 실제 IPv4로 교체합니다.
.\tools\run.ps1 -Task start -BindAddress 0.0.0.0 -AllowedOrigin 'http://192.168.0.4:8788'
```

허용 목록은 직접 실행 시 `--Realtime:AllowedOrigins:0 http://...` 또는
환경변수 `Realtime__AllowedOrigins__0`으로도 지정할 수 있습니다.

## 이번 단계의 범위

인게임 패널과 웹 로비 모두 **게스트 채팅 데모**입니다. 닉네임은 계정 인증이 아니며,
월드 ID도 게시 서버에서 존재 여부를 검증하지 않습니다. 초대 주소는 접근 권한 토큰이 아닙니다.
서버 재시작이나 방 소멸 시 참가자·채팅 기록은 사라집니다. 연결이 끊기면 다시 입장하여
새 참가자 ID와 현재 스냅샷을 받습니다. 기존 ID를 유지하는 자동 재접속은 아직 없습니다.

기본 실행은 PC 전용 HTTP/WS입니다. 휴대폰 LAN 시연은 명시적으로 켭니다.
인터넷 배포나 HTTPS인 기존 WebGL 플레이어에 붙이기 전에는 HTTPS/WSS, 접속 인증,
배포 환경을 추가해야 합니다. `/ws`는 같은 출처 및 설정된 출처의 브라우저를 허용하며,
Origin 검사는 인증을 대신하지 않습니다. Origin이 없는 비브라우저 테스트 클라이언트도 지원합니다.

캐릭터 생성·3차원 이동·애니메이션 동기화, `Kimchily.Network` TypeScript API,
서버 스크립트 실행과 미니게임 판정은 다음 단계입니다. 원래의 게임 서버 실행 파일 전체를
옮긴 것이 아니므로 기존 던전·전투·로그인·DB 기능을 제공한다고 해석해서는 안 됩니다.

## 검증

`-Task test`는 자체 임시 loopback 포트에서 서버 프로세스를 실행하고 종료합니다.
다른 실행 중인 서버는 종료하지 않습니다. 결과와 서버 로그는 `Artifacts/checks/<UTC 시각>`에 남습니다.
자동 검사와 브라우저 검증 결과는 [1단계 보고서](../docs/reports/2026-09-29-csharp-realtime-foundation.md)에 기록합니다.

다음 구현 순서는 **내 캐릭터/원격 캐릭터 구분 → 3차원 상태 동기화 →
TypeScript의 Room/Players API → 협동 발판 미니게임**입니다.
