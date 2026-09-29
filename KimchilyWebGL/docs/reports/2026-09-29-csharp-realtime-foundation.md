# C# UGC 실시간 서버 1단계 — 2026-09-29

## 결과

`KimchilyWebGL/KimchilyServer`를 추가했습니다. 기존 Project Dawn C# 서버의 작업 큐와
예약 작업을 공통 라이브러리로 이식하고, .NET 10 ASP.NET Core 서버에 WebSocket 방 입장과
텍스트 채팅을 구현했습니다. Unity WebGL 두 세션과 별도 웹 로비를 같은 방에 연결하여
한글 채팅을 확인했습니다. 기존 웹 화면은 매칭 전 로비 데모로 유지합니다.

작업은 `E:\task\KimchilyWebGL`에서 수행하고, 소스만 `E:\GItHub\PortFolio`에 반영합니다.
기존 `E:\task\Server`, Unity 기준본, Lyra 작업본은 수정하지 않았습니다.

## 도구와 코드

- 공식 SDK 10.0.401 / 런타임 10.0.12, `net10.0`.
- 설치 위치는 `KimchilyServer/.tools/dotnet-10.0.401`. SDK와 압축 파일은 Git에서 제외합니다.
- 외부 NuGet 패키지 없이 SDK에 포함된 .NET/ASP.NET Core 프레임워크를 사용합니다.
- 작업 큐 전체 실행 잠금, 대기 개수 제한, 가장 이른 예약 우선 처리와 64비트 시계 적용.
- UGC 방은 `worldId + revisionId + roomId` 기준으로 분리합니다.
- 기본 포트 8790, loopback 기본값, LAN 바인딩은 실행 옵션으로 선택합니다.
- 코드 출처·원본 해시는 [재사용 기록](../../KimchilyServer/docs/reuse.md)에 남겼습니다.

## 자동 검증

Windows x64에서 Release 빌드 **경고 0 / 오류 0**, 자체 검사 **24개 통과**.
Unity 6000.3.24f1에서 **PlayMode 107개, EditMode 60개 통과**, WebGL IL2CPP 빌드 성공.
Unity 검사는 새 네트워크 DTO·입력 검증, 채팅 중 조작 차단/복원, 월드 컨텍스트 변경과
이전 세대 콜백 무시, 기존 월드 로딩·종료·스크립팅 회귀 검사를 포함합니다.

- 큐 FIFO, 예약 순서·동일 시각 순서, 용량 제한, 동시 Flush 배제, 요청 오류 격리.
- 방 입장 스냅샷, 참가자 ID, 한글 채팅, 월드·게시 버전·방 코드별 격리.
- 늦은 입장자의 최근 채팅, 입력 검증, 채팅 크기·속도 제한, 최근 20개 기록, 정원, 빈 방 정리.
- 실제 임시 포트 Kestrel 프로세스와 ClientWebSocket 클라이언트.
- HTTP 화면·health, 두 연결의 양방향 채팅, 잘못된 JSON과 발신자 위조 필드 거부.
- 나뉜 WebSocket 프레임 조립, 소켓 종료 알림, 퇴장 후 새 입장.
- 같은 출처 및 명시적으로 등록한 Unity 출처 허용·외부 출처 거부,
  비텍스트/과대 메시지 거부, 연결별 요청 속도 제한.

증거는 로컬 `KimchilyServer/Artifacts/checks/20260929-095519/results.json`과 `host.log`,
`KimchilyUnityRuntime/Artifacts/runtime-playmode.xml`, `runtime-editmode.xml`, `webgl-build.log`에 있습니다.
기존 8788 게시 서버가 실행 중임을 확인하여 브라우저 검증에 사용했습니다. 해당 프로세스와
사용자가 열어 둔 Creator Editor는 종료하지 않았습니다. Creator 전용 배치 검증은 프로젝트가
이미 열려 있어 실행하지 못했지만, 열린 Editor의 자동 재컴파일 로그에서 새 네트워크와
Creator 프로젝트 Editor 어셈블리의 컴파일 성공을 확인했습니다. Editor 패널의 실제 화면·
네이티브 소켓 연결은 별도 확인 대상입니다. Task의 별도 `MyWorld.unity` 수정은 이번 변경분에 포함하지 않습니다.

## 브라우저 검증

PC의 Codex 내장 브라우저에서 독립 로비 두 탭, 이후 Unity WebGL 두 탭과 로비 한 탭을 사용했습니다.

- `칠리`, `새싹` 두 참가자가 같은 방에 표시됨.
- 두 방향 한글 메시지가 상대방 화면에 나타남.
- 한쪽 페이지를 다시 열어 입장하면 기존 상대방과 최근 메시지를 다시 수신함.
- 모바일 크기 뷰포트에서 가로 넘침 없이 배치됨. 입장 후 설정을 접어 채팅 공간 확보.
- 데스크톱·모바일 스크린샷은 `KimchilyServer/Artifacts/browser-desktop.png`, `browser-mobile.png`.
- Unity 월드의 `월드칠리`, `월드새싹`과 로비의 `로비친구`가 같은 방에서 3명으로 표시됨.
- 인게임 → 로비, 로비 → 인게임, 인게임 → 다른 인게임의 한글 채팅 수신 확인.
- 두 번째 Unity 접속 시 이전 메시지 수신, 채팅 닫힘 시 조이스틱·점프 UI 복원 확인.
- 월드의 **나가기** 실행 시 다른 클라이언트 참가자 수가 3→2로 바뀌고, 떠난 클라이언트는
  연결·기록을 정리하여 로비 컨텍스트로 돌아감.
- 390×844 뷰포트에서 인게임 패널·입력·전송 버튼이 가로 넘침 없이 배치됨.
- `KimchilyServer/Artifacts/ingame-desktop.png`, `ingame-mobile.png`, `ingame-controls-restored.png` 저장.

**실제 iPhone/Android 기기 연결, LAN·인터넷 접속과 TLS 배포는 이번 검증에 포함하지 않습니다.**
화면 크기 검증은 실기기 검증을 대신하지 않습니다.

## 다음 단계

Unity 게스트 인게임 채팅과 매칭 전 로비 채팅을 구현했습니다. 기존 Creator SDK와 Unity Editor
버전은 유지하며 네트워크 기능은 `com.kimchily.networking@0.1.0`으로 분리했습니다.
TypeScript 네트워크 API, 자동 매칭, 원격 캐릭터·이동 동기화는 아직 추가하지 않았습니다.
기존 C# 서버의 DB 로그인·던전·전투·2D 이동 프로토콜은 이번 UGC 서버에 포함하지 않습니다.

다음 작업은 원격 플레이어 생성과 3차원 상태 동기화입니다. 이후 Room/Players TypeScript API와
[협동 미니게임 시연안](../multiplayer-demo.md)의 규칙을 연결합니다.
