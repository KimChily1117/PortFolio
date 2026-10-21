# 마을·던전 채팅

최신 v14에는 **Enter → 입력 → Enter** 공통 처리와 캐릭터 위5초 말풍선을 추가했다. 현재 결과는 [1~4인/말풍선 통합 기록](PARTY-SIZE-BUBBLES-20261018.md)을 우선한다. 아래는 최초 채팅 버전의 구현·실행 기록이다.

- 마을: 같은 GameRoom/마을 채널의 접속자에게 전달. 개인 세리아방/공개 입구도 같은 마을 채널 채팅을 공유한다.
- 던전: 같은 레이드 GameRoom의 파티 전체. 3×3 내부 RoomId로 대화를 나누지 않는다.
- 다른 채널, 다른 레이드, 마을/던전 간에는 전달하지 않는다.
- Enter 열기/전송, Esc 취소, 전송 버튼, 한글 IME 조합 중 Enter는 제출하지 않음.
- 입력 중 이동/평타/스킬/인벤토리 단축키 차단. 온라인 시뮬레이션·피격은 계속 진행.
- 화면에는 현재 채널 메시지 최대80개를 유지한다. 서버 DB에 대화 내용이나 채팅 이력을 저장하지 않는다.
- 채널/던전 전환 시 화면 이력 초기화, 재접속은 새 컨텍스트. 이전 방 대화 재생 기능 없음.

## 서버 계약

Protocol의 메시지 ID58/59/60에 C_Chat/S_Chat/S_ChatContext를 추가했다. 기존 메시지 번호나 전투 ruleset은 바꾸지 않았다. C_Chat은 본문과 서버 발급 컨텍스트만 보내고 닉네임·수신자·채널을 지정할 수 없다.

서버는 인증/세션/현재 방/전환 상태/컨텍스트를 접수 시 검사하고 소유 방 큐에서 다시 검사한다. 방 전환과 수신이 겹쳐도 이전 방에서 보내는 메시지가 새 방으로 새지 않는다. 캐릭터의 실제 이름과 ObjectId를 서버에서 채운다.

본문120 UTF-16자/UTF-8 360바이트 제한, 한 줄, 공백만 입력·제어문자·방향 제어·비정상 surrogate 거절. 클라이언트의 표시 스타일 richText=false로 <b> 등도 일반 글자로 표시한다. 서버 토큰버킷은 burst3/초당1회 회복, 잘못된 요청도 예산을 사용한다. 과도한 요청은 방 큐에 들어가기 전에 버린다. 패킷512바이트 상한을 유지한다.

## 검증

실제 MySQL/FlowHost/TCP9클라이언트, 서로 다른 마을2채널 및 동시 레이드2파티를 사용한 15검사 통과: `Logs/chat-isolation-tests.log`. 인증 전 거절, 한글·emoji, 서버 닉네임, 채널/파티 격리, 이전컨텍스트·전환중 거절, 길이/제어문자, 도배 제한, 마을 복귀 범위 전환 포함.

Unity/AWS 실제 검증은 `Artifacts/aws-flow-4p-20261018-171407`에 보존했다. 자동 메시지 시험은 실제 전송/수신/그리기 경로를 사용하되, 물리 키보드의 IME 조합·전송 동작 검증과 구분한다.

재현: 로컬 FlowHost를 사용하지 않는 포트에서 실행한 뒤 `dotnet run --project ChatTests/ChatTests.csproj -c Release -- <포트>`. 테스트는 별도 chat_ 접두사 계정9개와 시작 장비를 생성한다. 테스트를 마친 뒤 해당 서버를 종료해야 Unity 전체빌드의 파일 잠금을 피할 수 있다.

## 실제 AWS 4인 결과

- `python Tools/VerifyChat4.py Artifacts/aws-flow-4p-20261018-171407`: 채팅 감사 **9/9 통과**. 마을2채널의 수신자 격리, 동일 레이드에서 네 송신자 메시지를 각각 한 번씩 같은 순서로 수신, 복귀 후 마을 컨텍스트 발급, Unity 오류0, 채팅 캡처8장.
- `python Verify-Flow4.py Artifacts/aws-flow-4p-20261018-171407`: 전투 **25/26**, 흐름 **9/10**. 흐름의 실패 항목은 전투 회귀 집계이며 네 명 모두 정상 마을 복귀, 장비·코디 유지, 로딩·본인 컷신 분리 확인.
- 공통 확정 상태181개·콘텐츠 이벤트401개 일치. 화면상 점프 회피19/19 확정, 메테오/충격파 비교96건의 활성 틱 오차0·표시 지연0틱.
- 전체 회귀 실패 원인: 서버에서 `TooLate` 입력6건(전투 약60.7초/69.6초)을 거절. `Logs/chat-aws-raid-diagnostics.log`에 서버 원문을 보존했다. 검증 기준을 완화하지 않았다. 지연 발생 근본 원인 또는 채팅과의 인과관계는 확정하지 못했다. 방장 Lua20건은 모두 확정됐고 프로토콜 중단 없이 `PartyReturn_NoRewards`로 종료했다.
- 한 개발 PC의 네 Unity 프로세스가 실제 AWS 공인 TLS 서버에 접속한 시험이다. 다른 물리 PC·회선, 물리 키보드 한글 조합, 3×3 서로 다른 방에 흩어진 상태의 채팅을 각각 검증했다고 주장하지 않는다. 내부 방 무관 파티 전달은 서버 GameRoom 단위 라우팅으로 구현했다.

（화면 자료: 마을 채팅 — Notion 참조）
（화면 자료: 던전 채팅 — Notion 참조）

## 배포와 수정 위치

- 친구용: 새 채팅 빌드 ZIP (`Artifacts/friend-playtest-20261018-171806/ProjectDawn-FriendTest.zip`, 로컬 검증 자료). 전체 압축 해제 후 `Start-ProjectDawn.cmd`. 144파일 CRC·SHA256·실제 Unity 빌드와 일치 검사 통과, `package-verification.json`에 기록.

- Linux 서버/게이트웨이: `Artifacts/cloud-release-20261018-171158/release.zip`, SHA256 `9695ece4edbb41d9d1e1ab7160df3adee0a9c0d417f8d4d38bd1a39c244d2ff1`.
- AWS 기존 접속자0 확인 후 게임 서비스만 재시작해 배포. 이전 프로그램은 `/opt/projectdawn/release-before-chat-20261018`에 보존. DB 스키마 변경 없음. EC2는 중지하지 않았다.
- 클라이언트: `UnityClient/Assets/Scripts/RaidDemoApp.Chat.cs` 및 `RaidDemoApp.cs`, `RaidDemoApp.Flow.cs`, `DawnRaidTransport.cs`.
- 서버: `Server/Server/Session/ClientSession_Chat.cs`, `Server/Server/Game/Room/GameRoom_Chat.cs`, 방 입장/재접속 시 컨텍스트 발급.
- 계약: `Protocol/Protocol.proto`, `Protocol/Raid.proto`. 서버·친구용 클라이언트를 함께 갱신한다.
- 빌드 로그: `Logs/chat-unity-build.log`, `Logs/chat-cloud-build.log`, 배포 로그: `Logs/chat-aws-deployment.log`.
