# 1~4인 선택 매칭과 캐릭터 말풍선

## 사용하는 방법

던전 입구 오른쪽의 **입장 인원 1인 / 2인 / 3인 / 4인**을 선택한다. 1인은 혼자 입장하고, 2~4인은 같은 입장 인원을 선택한 참가자가 해당 수만큼 모이면 시작한다. 기본 선택은 기존과 같은 4인이다. 서로 다른 인원 설정은 한 파티로 합치지 않는다. 입장 후 신규 참가자 충원은 없고 원래 참가자의 재접속만 허용한다.

보스의 최대 HP는 기존 4인 기준 **32,500**, 공격 피해량도 기존과 같다. 소수 인원 난이도 자동 조절은 이번 변경에 포함하지 않았다. 3×3 방과 기존 스킬·연출·보상은 유지한다.

**Enter → 내용 입력 → Enter**로 전송한다. Esc는 취소, 전송 버튼도 지원한다. 입력 중 이동·공격·스킬·창 단축키는 막고 서버 전투는 계속한다. 한글 조합 중 Enter와 조합 완료 직전 프레임은 전송으로 처리하지 않는다.

서버에서 수신한 메시지를 채팅창과 캐릭터 위 말풍선에 함께 표시한다. 말풍선은 5초 유지 후 마지막 0.6초 동안 사라지며, 같은 사람이 새 메시지를 보내면 최신 내용으로 교체한다. 현재 보이는 캐릭터만 표시하고, 던전에서 다른 내부 방의 파티원은 채팅창으로만 대화한다. 말풍선은 점프 높이·이동 위치를 따라간다. 마을 채널/던전 전환 시 이전 내용은 지운다. 대화 본문은 서버 DB에 저장하지 않는다.

## 개발자가 확인할 계약

| 영역 | 변경 |
|---|---|
| 프로토콜 | C_CreateRoom.party_size(2), S_CreateRoom.party_size(3). 0은 기존 4인 요청, 1~4만 허용. 기존 ID 유지 |
| 매칭 | MatchQueueKey.PartySize를 동등성·해시·표시 키에 포함. 목표 인원이 모여야 파티 구성/전환 |
| 서버 전투 | 최초 참가자1~4명의 실제 슬롯 배열. 중복 ID·존재하지 않는 호스트·초과 인원 거절 |
| 스냅샷/클라이언트 | 실제 인원 수를 허용하고 서버 슬롯·Fighter·스냅샷 수가 일치하는지 검사 |
| Lua | ctx.player_count 제공. 메테오4회/돌진3회 대상이 실제 슬롯 안에서 순환 |
| 방 표식 | 생존/참여 마스크를 사용해 최대2명. 1인은1명, 한 회차 안에 중복 없음. 소수 인원에서는 연속 회차 중복을 완전히 피할 수 없음 |
| 재접속 | 실제 참가자 수로 정책 상태 생성. 기존 비방장3분/방장 이탈 시 중단 규칙 유지 |
| 보상 | 실제 최초 참가자1~4명 영수증. 슬롯·계정·캐릭터 유효성 유지. MySQL 중복 저장 검사는 실제 수신자 수 사용 |
| 채팅 | 동일 Enter 처리 함수를 실제 입력과 자동 시험이 사용. 서버 발신자 ID를 마을 ObjectId/레이드 슬롯으로 매핑 |

공통 규칙은 **dawn-party-size-v14**다. 서버와 Unity를 함께 배포해야 한다. v13 친구 ZIP을 그대로 사용하지 않는다. DB 스키마 변경은 없다. 방장 계산·서버 검증/확정과 기존 480ms 확정 경계는 유지했다.

## 소스 위치

- `Protocol/Protocol.proto`, `Server/Server/Game/Match/MatchManager.cs`, `MatchQueueKey.cs`
- `Server/Server/Game/Room/GameRoom_Raid.cs`, `GameRoom_RaidLifecycle.cs`, `RaidParticipation.cs`
- `Server/Server/DB/RaidRewardStore.cs`, `MySqlRewards.cs`
- `Packages/com.projectdawn.combat/Runtime/EncounterEngine.cs`, `Content/RaidContentState.cs`, `Content/RaidSnapshotCodec.cs`, `Content/BossPatternProgram.cs`
- `Client/RaidNetworkClient.cs`, `Client/LuaPatternPlanner.cs`, `Lua/bakal_patterns.lua`
- `UnityClient/Assets/Scripts/RaidDemoApp.Flow.cs`, `RaidDemoApp.Chat.cs`, `RaidDemoApp.Dawn.cs`

## 검증 기록

- `Logs/party-size-tests.log`: **125검사 통과**. 실제 TCP 세션10개로 1/2/3/4인 대기열 격리·입장, 잘못된 크기 거절, 2인 재접속, 각 인원별 실제 SQL 보상/장비 저장·중복 재처리·마을 복귀 확인. 클리어는 테스트 안에서만 최종 전투 상태를 승리로 설정한 fixture이며 자연 플레이 클리어는 아니다.
- 기존 검사: 코어26, 스킬/콘텐츠86, Lua50, AI22, 방 기믹72, 재접속/보상46 통과. 해당 로그는 `Logs/party-size-*-tests.log`에 보존.
- 기존 Lua 테스트가 생략했던 최초 슬롯/스냅샷을 실제 서버 계약에 맞게 채웠다. 기존 재접속 테스트의 350/400ms 대기는 v13부터의 480ms 확정 경계보다 짧아 설정값을 기준으로 수정했다. 실제 판정 경계를 완화하지 않았다.
- Unity 빌드 `Logs/party-size-unity-build.log`, Linux 빌드 `Logs/party-size-cloud-build.log` 성공.

### 실제 AWS Unity 결과

| 인원 | 실행 | 입장·채팅·말풍선·복귀 검사 | 공통 상태/이벤트 |
|---|---|---|---|
| 1 | `Artifacts/aws-flow-1p-20261018-183305` | 17/17 통과, 거절0 | 81/78 |
| 2 | `Artifacts/aws-flow-2p-20261018-183415` | 17/17 통과, 거절0 | 81/128 |
| 3 | `Artifacts/aws-flow-3p-20261018-183533` | **16/17: 전체 미통과**, 동일 TooLate 입력1건 | 81/199 |
| 4 | `Artifacts/aws-flow-4p-20261018-184028` | 17/17 통과, 거절0 | 181/395 |

4인 실행은 별도 전체 전투26개·흐름10개 검사도 통과했다. 화면 점프22/22가 서버 결과와 일치했고, 활성 표시96개 대조에서 계산 틱 오차0, 화면 관측 지연0~1틱이었다. 21개 스킬, 메테오4연속, 본인 컷신/타인 컷신 억제, 코디 유지·마을 복귀를 확인했다. 전투 종료는 90초 후 `PartyReturn_NoRewards`이며 자연 클리어 보상 검증은 아니다.

3인 실패는 `Logs/party-size-3p-aws-diagnostics.log`에 보존했다. 거절1건이 세 클라이언트 모두에 전달된 것이며 서로 다른3건이 아니다. 4인 통과를 이 문제의 수정 증거로 취급하지 않는다. 자동 Enter 함수 시험은 물리 키보드·Windows IME 입력 검증과 구분한다. 실제 AWS 공인 TLS를 사용했지만 실행 PC는 한 대다. 서로 다른 물리 PC·회선 시험은 별도다.

### 비밀번호8자와 계정 관리

게이트웨이·클라이언트·Unity의 최소 길이를8자로 맞췄다. PBKDF2와 기존 비밀번호는 유지한다. `Logs/password8-aws-tests.log`의9검사에서 실제 AWS7자 거절·8자 가입/로그인을 확인했다. [아이디 조회·비밀번호 재설정·잠금 안내](Cloud/ACCOUNT-MANAGEMENT-KO.md). 실제 사용자 계정을 변경하지 않았다.

## 배포

현재 실행 Linux 패키지: `Artifacts/cloud-release-20261018-183815/release.zip`, SHA256 `aa1ffd1e6d62ff15227674c1700df32277ac76ca1b1387df0f8a1764212750de`. 접속자0 확인 후 기존 승인된 AWS 인스턴스에 반영했다. 이전 서버는 `/opt/projectdawn/release-before-password8-20261018-183815`에 보존했고 두 서비스 active를 확인했다. 최초 v14 배포는183117이며 후속183815에 비밀번호8자 정책을 포함했다. 신규 AWS 자원·유료 플랜 전환 없음.

계정 운영 도구 `Cloud/freevm/admin-members.py`는 이후 별도로 root:root0750으로 설치했다.183815 ZIP에는 이 추가 도구가 없으므로 해당 ZIP 재배포 시 원본을 `ops/freevm/admin-members.py`에 설치해야 한다. 다음 `Cloud/Build-Cloud.ps1` 빌드부터 자동 포함된다. 단위11검사와 AWS 읽기 전용 조회/서비스 active 확인을 마쳤으며 실계정 비밀번호·잠금은 변경하지 않았다.

최신 친구 배포: `Artifacts/friend-playtest-20261018-184505/ProjectDawn-FriendTest.zip`,144파일, SHA256 `8df33fdc503efd1e0db14d0ff0a3b1df918d32e3d9e78bb31953634e2b6d90a8`. CRC·모든 파일 해시·실제 시험 Build 일치·민감 파일 제외 확인.
