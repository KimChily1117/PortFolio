# Kimchily Networking 0.3.0

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

## 칠리 아일랜드 협동 게임

TypeScript SDK 0.2.0에서 `import { Room } from "Kimchily.Network"`로 API를 가져옵니다.
이 기능은 제작 씬의 TS 스크립트가 `Room.enableGame("chili-portal-v1")`을
호출한 월드에만 표시됩니다. 아직 접속하지 않았다면 요청을 기억했다가 joined 이후 서버에
`game/watch`를 보냅니다. 자동 입장으로 게임을 시작하지 않으며, 모두 모인 뒤 Unity HUD의
시작 버튼으로 현재 1–4명에 맞춘 라운드를 시작합니다. 한 명으로도 시연할 수 있습니다.

고정 프리셋의 발판은 star(-3,0,2), moon(3,0,2), sun(-3,0,6), leaf(3,0,6)이고
서버가 필요한 발판 수, 점유한 플레이어, 3초 유지 시간과 성공 상태를 결정합니다.
`active`는 이번 라운드에 필요한 발판, `playerId`는 현재 점유자입니다.
모든 발판이 3초 동안 채워지면 `complete`가 되며, 명시적으로 다시 준비하기를 누르기 전까지
성공 상태를 유지합니다. 중도 참가자도 같은 상태를 받습니다.
플레이어가 줄어들어 필요한 인원보다 적어지면 HUD에서 인원을 다시 맞출 수 있습니다.

TS는 `Room.getState()`로 연결 상태·참가자·공동 게임 상태를 읽고,
`Room.startRound()` / `Room.replay()`로 서버에 요청합니다. 시각 효과와 모델은 제작 씬의
TS 스크립트가 상태를 읽어 연출합니다. 임의 TS 코드를 서버에서 실행하는 기능은 아닙니다.
기존 위치 검증을 통과한 클라이언트 위치로 판정하므로 서버 물리나 완전한 치트 방지와는 다릅니다.

네이티브 C#에서는 보존된 정적 API `CoopPortalApi.EnableGame`, `StartRound`, `Replay`,
`GetStateJson`을 사용합니다. JSON은 `{connected,selfId,players,game}`이며, 아직 opt-in하지
않았거나 상태가 도착하지 않았으면 `game:null`입니다. 스냅샷 조회만으로 소켓이나 UI를 만들지
않습니다. 월드 퇴장 시 opt-in·HUD·게임 상태를 지우며, 재접속 시 최신 서버 상태를 다시 받습니다.
