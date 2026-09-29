# Kimchily Networking 0.4.0

KimchilyServer protocol 1의 게스트 방·채팅·캐릭터 동기화 클라이언트입니다.
Unity 6의 uGUI 2.0.0/TextMeshPro를 사용하며 Creator 콘텐츠 SDK 0.1.0의 버전 계약을 유지합니다.

## 입장과 퇴장

홈/QR 화면에서 닉네임을 정하면 검증된 게시 링크의 fragment에 닉네임과 방 코드가 붙습니다.
호스트는 `ConfigureSession(json)`으로 설정을 전달하고, 월드가 준비되면
`EnterWorld(scene, worldId, revisionId)`를 호출합니다. 이때 로컬 플레이어를 찾고 자동으로
같은 월드·게시 버전·방에 접속합니다. 닉네임 없이 연 직접 링크는 실행 전 입력을 받습니다.

`ExitWorld()`는 소켓, 원격 캐릭터, 말풍선과 채팅 기록을 정리합니다. 연결 세대가 다른
콜백은 무시합니다. 수동 재접속 시에는 새 서버 ID를 받으며 자동 세션 복구는 하지 않습니다.

## Unity UI와 입력

`UnityChatPanel`은 Screen Space Overlay Canvas, TextMeshProUGUI, TMP_InputField로
구성됩니다. Editor/네이티브/WebGL 모두 같은 UI를 사용합니다. WebGL `.jslib`는
WebSocket 송수신만 담당하며 HTML 채팅 패널을 생성하지 않습니다.
열린 패널은 기본 KimchilyMobileControls를 잠시 끄고, 닫으면 이전 활성 상태만 복원합니다.
닉네임과 메시지는 richText를 끈 텍스트로 표시합니다. Noto Sans CJK KR 동적 폰트와 TMP
필수 리소스를 패키지에 포함합니다. 폰트 출처·라이선스는 Resources/Fonts/README.md를 참고합니다.
TMP_InputField의 WebGL/기기 키보드 동작은 실제 배포 브라우저에서 별도 확인해야 합니다.

## 캐릭터와 말풍선

서버의 playerId를 키로 내 캐릭터와 원격 캐릭터를 구분합니다. 로컬 플레이어 상태는
초당 10회 전송하고 원격 위치·회전을 보간합니다. 걷기/달리기/점프는 기존 애니메이션
드라이버와 프로필을 재사용합니다. 원격 캐릭터는 시각 모델만 복제하고 사용자 스크립트,
카메라, 입력, 물리 충돌은 제거합니다. 두 번째 이후 입장자는 가능한 안전한 주변 바닥에
분산 배치합니다. 늦게 입장한 사람은 최신 위치 스냅샷을 받습니다.

캐릭터 위 이름표와 6초짜리 말풍선도 World Space Canvas/TextMeshPro입니다.
실시간 chat 이벤트의 playerId로 해당 캐릭터에 표시하며, 과거 대화는 말풍선으로 재생하지 않습니다.
퇴장·월드 전환 때 원격 객체를 정리합니다.

현재 위치는 클라이언트가 계산하고 서버가 범위·빈도·순서·이동량을 검증하여 중계합니다.
서버 물리 판정이나 완전한 부정행위 방지 기능은 아닙니다. 임의 텔레포트는 지원하지 않으며,
낙하 후 자신의 최초 스폰으로 돌아오는 기본 리스폰만 허용합니다.

## 제작과 게시

Runtime 호스트와 Creator Editor가 실행 중에 UI를 설치하므로 제작 씬을 수정할 필요가 없습니다.
Creator의 Play Mode는 editor-preview/v1에 수동 접속하며, 게시된 월드는 실제 worldId/revisionId를 씁니다.
공통 WebGL 실행기를 새로 빌드하면 기존/신규 게시 월드에 적용됩니다.

기본 개발 주소는 ws://127.0.0.1:8790/ws입니다. WebGL에서는 페이지 호스트를 사용합니다.
다른 서버 주소는 실행 전 연결 설정 또는 연결 해제 상태의 Unity 패널에서 지정합니다.
HTTPS 페이지에는 WSS가 필요합니다. LAN 실행·허용 출처 설정은 KimchilyServer/README.md를 참고합니다.
독립 웹 로비는 유지합니다.

## 범용 스크립트 게임 통신

Networking SDK는 발판 개수, 점유 판정, 유지 시간, 승리 조건을 모릅니다.
C#은 소켓·방·플레이어 동기화와 스크립트 신원·메시지 한도·수명만 담당하고,
게임 규칙은 서버에서 실행되는 TypeScript 모듈, 게임 연출·문구는 제작 씬의 TypeScript가 담당합니다.

제작 씬은 `Room.useGame(scriptId, scriptHash)`로 실행할 서버 스크립트를 지정합니다.
ID는 영문·숫자·하이픈·밑줄 1–80자이며 해시는 소문자 16진수 64자리 SHA-256입니다.
동일한 방에서 다른 스크립트 신원이나 해시로 조용히 전환하지 않습니다.
해시로 고정된 서버 스크립트와 게시 콘텐츠가 같은 규칙 버전을 사용하는지 확인해야 합니다.

입장 전 호출은 보관했다가 joined 이후 `game/watch`를 보냅니다. UI는 자동으로 만들지 않습니다.
사용자 입력은 `Room.sendAction(action, payload)`로 요청하고, 허용 여부와 상태 변화는 서버 TS가
결정합니다. C#에 특정 게임의 시작/재시작 분기나 상태 이름이 없습니다. 조회는
`Room.getState()`를 사용합니다. 받은 상태는 클라이언트 예측 결과로 덮어쓰지 않습니다.

C# 브리지의 정확한 표면은 `ScriptRoomApi.UseGame`, `SendAction`, `GetStateJson`입니다.
원시 JSON은 `{connected,selfId,players,game}`이며 game은
`{scriptId,scriptHash,version,stateJson}` 또는 null입니다. stateJson 내부 스키마는 게임 TS의
계약입니다. Unity 클라이언트는 다른 신원/해시 및 현재 연결에서 이미 받은 이하 버전을 버립니다.
연결 종료 시 상태를 비우고, 새 joined 스냅샷은 이전 소켓의 버전과 비교하지 않습니다.
월드 퇴장 시 구독 자체도 초기화합니다.

범용 경계는 다음과 같습니다.

- 액션 ID: 영문·숫자·하이픈·밑줄 1–80자.
- 액션 payload JSON: 최대 1,024자 및 UTF-8 1,024바이트.
- 최종 직렬화된 game 명령: JSON 이스케이프까지 포함해 UTF-8 4,096바이트 이하.
- 상태 JSON: 최대 16,384자 및 UTF-8 16,384바이트.
- 사용자 액션: 0.6초당 하나. 재접속 watch는 이 입력 제한과 별도로 처리합니다.

이 한도는 네트워크/VM 자원 보호 장치입니다. 점수, 시간, 좌표 등의 게임별 검증은 서버 TS의
책임입니다. 기존 이동은 클라이언트 물리와 서버 범위 검증을 사용하므로 서버 물리나 완전한
부정행위 방지 기능을 의미하지 않습니다.

## TypeScript가 작성하는 Unity HUD

`Kimchily.UI` 모듈의 `Hud.showPanel(model)`, `Hud.takeAction()`, `Hud.hide()`를 사용합니다.
모델은 eyebrow/title/body/progress/accent와 선택적인 action(id/label/enabled)으로 구성됩니다.
문구, 색상, 진행률 계산, 액션 ID의 의미는 모두 제작 TS에 있습니다.

C# `WorldHudPanel`은 모델을 검증하고 현재 TMP 레이아웃에 표시할 뿐 서버에 명령을 보내지 않습니다.
화면 중앙의 캐릭터 말풍선을 가리지 않도록 데스크톱에서는 좌측 상단에 놓으며, 모바일 버튼은
조이스틱과 점프 영역을 피합니다. body는 짧은 두 줄 안내에 맞춰 작성하고, 긴 내용은 생략 표시됩니다.
모든 텍스트는 richText=false로 처리합니다. 사용자가 보낸 마크업을 실행하거나 스타일로 해석하지 않습니다.

각 TS Behaviour는 자기 패널을 소유합니다. 버튼은 그 소유자에게만 한 개의 액션 ID를 남기며,
`takeAction`으로 한 번 소비하면 사라집니다. 패널 설정 변경·숨김·비활성화는 대기 입력을 지웁니다.
따라서 TS Update에서는 먼저 입력을 소비하고 새 상태에 맞춰 패널을 갱신하는 순서를 권장합니다.
Unity 버튼이 VM 콜백을 직접 재진입시키지 않는 구조입니다.

호스트는 TS가 비활성화·재로드·예외·파괴될 때 패널을 직접 정리합니다.
정리는 스크립트의 OnDisable/OnDestroy가 성공했는지에 의존하지 않습니다.

패널 문자열 한도는 eyebrow 48, title 96, body 1,200, action label 48자입니다.
action ID는 1–80자이며 progress는 유한한 0–1, accent는 #RRGGBB입니다.
일반 제어 문자는 거부하고 줄바꿈·탭은 허용합니다. C# 입력 JSON은 4,096자/UTF-8 8,192바이트 이하입니다.
이 한도는 렌더러 보호를 위한 최대치이며 실제 한 화면에 표시할 문구 길이를 보장하지 않습니다.

## 검증에서 확인하는 책임 경계

Networking 테스트는 특정 게임의 성공 조건을 다시 구현하지 않습니다.
스크립트 신원·UTF-8 한도·버전 순서, 입장 전 구독/재접속/퇴장, UI 미생성 조회,
HUD 입력 검증·소유자 격리·입력 소비·정리, 모바일 컨트롤과 말풍선 레이아웃을 검증합니다.
게임 규칙 테스트는 해당 서버 TypeScript 모듈과 함께 관리합니다.
