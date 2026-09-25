# Phase 3: Authoritative Move Request and Destination Validation

기준 문서:

- [`navigation-grid-phase0-analysis.md`](./navigation-grid-phase0-analysis.md)
- [`navgrid-v1-format.md`](./navgrid-v1-format.md)
- [`navigation-grid-phase1-implementation.md`](./navigation-grid-phase1-implementation.md)
- [`navigation-grid-phase2-server-runtime.md`](./navigation-grid-phase2-server-runtime.md)

## 1. 결론

클라이언트 우클릭 목적지를 서버가 현재 `ClientSession.Player`, `ClientSession.GameRoom`, `GameRoom.Navigation`을 기준으로 검증하고 승인·보정·거부하는 별도 명령 흐름을 구현했다. 승인 시 서버 위치를 목적지로 순간이동시키지 않으며 `PlayerMovementState`만 교체한다. A*, 서버 Tick 이동, Snapshot 및 다른 플레이어 Broadcast는 구현하지 않았다.

## 2. 기존 C_Move와 공존 정책

- 새 DirectX 클라이언트는 기본적으로 `C_MoveRequest`를 사용한다.
- 클라이언트의 권위 이동 모드는 `NetworkManager`의 명시적 Feature Flag로 분리되며 기본값은 활성화다.
- 새 모드에서는 우클릭 직후 `MoveTo()`를 실행하지 않으므로 로컬 Transform이 승인 전에 이동하지 않는다.
- 레거시 `C_Move` Handler와 메시지는 삭제하지 않았다.
- 서버의 레거시 처리는 기본 비활성화다. 개발 호환 실행에서만 환경 변수 `D3D_ALLOW_LEGACY_MOVE=1`로 허용한다.
- DummyClient는 계속 `C_Move`를 생성할 수 있지만 기본 서버에서는 거부된다.
- 새 Handler는 `S_Move`를 재사용하거나 Broadcast하지 않는다.

## 3. 추가 프로토콜

기존 ID 0~19를 유지하고 다음 ID를 끝에 추가했다.

| ID | 메시지 | 방향 | 역할 |
|---:|---|---|---|
| 20 | `S_NavigationInfo` | Server → Client | RoomId, FormatVersion, NavigationMapId, ContentHash 전달 |
| 21 | `C_MoveRequest` | Client → Server | Sequence와 요청 XZ, Navigation identity 전달 |
| 22 | `S_MoveAccepted` | Server → Client | ServerMoveId, 요청/승인/서버 시작 위치, 보정 여부 전달 |
| 23 | `S_MoveRejected` | Server → Client | enum 거부 사유와 서버 권위 위치 전달 |

`C_MoveRequest`에는 ObjectId가 없다. 서버 이동 대상은 패킷 값이 아니라 `ClientSession.Player`로만 결정된다.

`MOVE_REJECT_REASON`에는 NoPlayer, NoRoom, RoomMismatch, InvalidPlayerState, InvalidSequence, InvalidCoordinate, NavigationMapMismatch, NavigationHashMismatch, OutsideNavigationBounds, DestinationBlocked, NoNearbyWalkableCell, MovementNotAllowed, ServerError가 정의되어 있다.

## 4. Navigation identity 획득

`GameRoom.EnterRoom()`의 Room Job에서 Player 등록과 `S_MyPlayer` 전송 후 `S_NavigationInfo`를 요청자에게 전송한다.

```text
RoomId
FormatVersion = GameRoom.Navigation.FormatVersion
NavigationMapId = GameRoom.NavigationMapId
NavigationContentHash = GameRoom.NavigationContentHash
```

클라이언트 `NetworkManager`가 이 값을 보관한다. NavigationInfo가 없거나 잘못된 버전/빈 identity이면 우클릭 MoveRequest를 보내지 않는다. MapId 또는 Hash는 클라이언트 코드에 하드코딩하지 않았다.

## 5. ClientMoveSequence

- TCP MoveRequest 전용 `uint32` Sequence다.
- NavigationInfo 수신 시 0으로 초기화하고 첫 요청은 1이다.
- 서버는 Session별 마지막 처리 Sequence를 저장한다.
- 0, 동일 번호, 과거 번호는 `InvalidSequence`로 거부한다.
- Session/Player/Room 귀속을 확인한 뒤 Sequence를 소비하므로 좌표·MapId·Hash가 잘못된 요청도 같은 번호로 재사용할 수 없다.
- V1은 uint32 wrap-around를 지원하지 않는다. 클라이언트가 `UINT32_MAX`에 도달하면 추가 요청을 중단하며 재접속이 필요하다.
- 클라이언트는 보낸 번호보다 크거나 이미 처리한 번호 이하인 응답을 무시한다.

## 6. Room JobQueue와 귀속 검증

Handler는 패킷을 파싱하고 현재 Session의 Player/Room 참조를 확보한 뒤 `room.Push()`만 수행한다. Job 내부 `AuthoritativeMoveService.Process()`가 다음을 다시 검사한다.

1. Session, Player, Room 존재
2. 연결 종료 여부
3. `Player.Session == ClientSession`
4. `ClientSession.Player`와 캡처한 Player 동일성
5. `ClientSession.GameRoom`과 캡처한 Room 동일성
6. `Player.Room`과 Room 동일성
7. Room Player Dictionary에 같은 인스턴스가 실제 등록되어 있는지
8. Sequence 단조 증가

기존 `GameRoom.EnterRoom()`이 `player.Room`을 설정하지 않던 문제도 함께 정상화했다. Room 제거 Job은 MovementState를 비활성화하고 Player/Session의 상호 참조를 해제한다. `Session.IsDisconnected` 읽기 전용 상태도 추가했다.

## 7. 목적지 검증 순서

1. 서버 권위 시작 위치 X/Y/Z 유한값 검사
2. 지원 가능한 Player 상태 검사 (`Skill` 상태는 현재 거부)
3. NavigationMapId 정확 일치
4. ContentHash 64자리 Hex 및 값 일치
5. 요청 X/Z의 NaN·Infinity 검사
6. 절댓값 1,000,000 초과 좌표 거부
7. `GameRoom.Navigation.TryWorldToCell`
8. 목적 Cell Walkable/Slow 여부 확인
9. Blocked이면 제한된 최근접 Walkable 검색
10. 승인 Cell 중심 계산
11. Room 단조 증가 ServerMoveId 발급
12. PlayerMovementState 교체

클라이언트 Y는 패킷에 포함하지 않았다. 요청 표시용 Y와 승인 목적지 Y는 NavGrid Origin.Y를 사용한다. StartPosition은 서버의 `Player.Info.Position`만 사용한다.

## 8. Blocked 목적지 정책

A*와 독립된 작은 BFS 보정을 사용한다.

- 시작 Cell이 Walkable 또는 Slow이면 그대로 승인
- Blocked이면 8방향 BFS
- 최대 Chebyshev 반경 4
- 최대 방문 128 Cell
- 맵 밖 Cell 제외
- 고정 Tie-break 순서: 북, 서, 동, 남, 북서, 북동, 남서, 남동
- Walkable과 Slow를 목적지로 허용
- 결과는 Cell 중심
- 제한 내 후보가 없으면 `NoNearbyWalkableCell`
- 맵 밖 요청은 보정하지 않고 `OutsideNavigationBounds`

골든 Grid의 Blocked Cell `(2,0)` 요청은 고정 순서에 따라 `(1,0)` 중심으로 보정되는 것을 테스트했다.

## 9. PlayerMovementState

```text
ServerMoveId
ClientMoveSequence
StartPosition
Destination
AcceptedServerTicks
IsActive
```

- ServerMoveId는 Room Job 안에서 Room별 `ulong` 단조 증가 값으로 발급한다.
- StartPosition은 서버 권위 위치다.
- Destination은 서버가 승인한 Cell 중심이다.
- 새 요청 승인 시 이전 상태를 비활성화하고 새 상태로 교체한다.
- Room 이탈 시 현재 상태를 비활성화한다.
- Phase 3에서는 MovementState를 따라 위치를 갱신하지 않는다.

정적 감사와 테스트에서 새 흐름은 `Player.Info.Position`, Tile 좌표, `ObjectInfo.State`를 변경하지 않고 `S_Move`를 Broadcast하지 않음을 확인했다. 위치 즉시 변경 코드는 환경 변수로만 활성화되는 레거시 `C_Move` 블록에만 남아 있다.

## 10. 클라이언트 승인·거부 처리

- 우클릭: 기존 Ray-Plane Picking 결과로 요청 X/Z를 만들고 목적지 마커를 표시한다.
- MoveAccepted: 응답 Sequence와 Navigation identity를 검증하고 마커를 승인 Cell 중심으로 옮긴다. 보정 여부를 로그에 표시한다.
- MoveRejected: 마커를 제거하고 거부 사유와 서버 위치를 로그에 표시한다.
- 두 경우 모두 Phase 3에서는 실제 로컬 자동 이동을 시작하지 않는다.
- 기존 자기 `S_Move` 무시 코드는 레거시 흐름에만 남아 있고 새 승인/거부 패킷에는 적용되지 않는다.

## 11. 주요 추가·수정 파일

### C++ 클라이언트

- `Common/protoc-21.12-win64/bin/Protocol.proto`
- `Common/protoc-21.12-win64/bin/Enum.proto`
- `Common/protoc-21.12-win64/bin/*.pb.h`, `*.pb.cc`
- `GameCoding2/Protocol.pb.*`, `Enum.pb.*`, `Struct.pb.*`
- `GameCoding2/ClientPacketHandler.h/.cpp`
- `GameCoding2/PlayerController.h/.cpp`
- `EngineCore/NetworkManager.h/.cpp`
- `Libraries/Include/EngineCore/NetworkManager.h`

### C# 서버

- `Server/Common/protoc-3.12.3-win64/bin/Protocol.proto`
- `Server/Common/protoc-3.12.3-win64/bin/Enum.proto`
- 생성된 `Protocol.cs`, `Enum.cs`, `Struct.cs`, `ServerPacketManager.cs`
- `Server/Server/Server/Game/Movement/PlayerMovementState.cs`
- `Server/Server/Server/Game/Movement/MovementProtocolPolicy.cs`
- `Server/Server/Server/Game/Movement/AuthoritativeMoveService.cs`
- `Server/Server/Server/Packet/PacketHandler.cs`
- `Server/Server/Server/Session/ClientSession.cs`
- `Server/Server/Server/Game/Objects/Player.cs`
- `Server/Server/Server/Game/Room/GameRoom.cs`
- `Server/Server/ServerCore/Session.cs`
- `Server/Server/NavGridTests/MovementPhase3Tests.cs`
- `Server/Server/NavGridTests/Program.cs`

## 12. 빌드와 테스트 결과

- Protocol Buffers C++ 21.12 생성: 성공
- Protocol Buffers C# 3.12.3 및 PacketGenerator 생성: 성공
- 서버 Debug: 성공
- 서버 Release: 성공
- DummyClient Debug: 성공
- EngineCore Debug x64: 성공
- DirectX Client Debug x64: 성공
- C++ NavGrid 회귀: 162 assertions 통과
- C# 전체 회귀: 228 assertions 통과
- 이 중 Phase 3: 71 assertions 통과
- C++/C# NavGrid 결과 JSON: byte-identical
- 교차 결과 SHA-256: `aadb9f2d02ce75d00281534c7a3c45e2e5fed05a313758f8ee62836d05aa1952`

클라이언트 Release x64는 Phase 3 코드와 무관한 기존 프로젝트 설정 누락으로 실패한다. `EngineCore.vcxproj`의 Release x64에 C++17 이상, Libraries Include 경로 및 Debug x64와 동등한 빌드 설정이 없어서 PCH 단계에서 실패한다. 이번 이동 Phase 범위를 넓혀 Release 설정 전체와 shader/PCH 조건을 수정하지 않았다.

## 13. 테스트 범위

- 새 패킷 직렬화/역직렬화 및 ID 유지
- C_MoveRequest에 ObjectId가 없음을 descriptor로 확인
- 첫/증가/동일/과거/0 Sequence
- 새 Session Sequence 초기화
- 다른 Player를 이동 대상으로 선택할 수 없음
- Job 실행 전 Room이 변경된 상황 거부
- Player Skill 상태 거부
- 최소 경계, 최대 경계 직전, 최대 경계 exact, Origin 미만
- NaN, Infinity, 비정상 대형 좌표
- MapId/Hash 불일치
- Walkable 승인과 Blocked 보정 Tie-break
- MovementState 생성·교체·이전 상태 비활성화
- StartPosition이 서버 위치인지 확인
- 승인 후 실제 Player 위치와 Object State가 바뀌지 않음
- ServerMoveId 증가
- Phase 1 파일/좌표/Hash 및 Phase 2 Registry/Room/Working Directory 회귀

## 14. 알려진 제한

- A* 및 경로 존재 검증이 없다. 근처 Walkable Cell 보정은 목적지 유효성만 보장한다.
- 서버 Tick이 MovementState를 소비하지 않으므로 승인 후 캐릭터는 아직 이동하지 않는다.
- 다른 클라이언트에 이동 시작 또는 Snapshot을 보내지 않는다.
- 현재 상태 enum에는 Dead, Stun, Casting, RoomTransition, SceneReady가 없어 `Skill` 이외의 세밀한 상태 검증은 할 수 없다.
- 요청 빈도 제한과 시간 기반 Rate Limit은 아직 없다.
- uint32 Sequence wrap-around는 지원하지 않는다.
- 런타임 Room 0 NavGrid는 현재 전체 Walkable 개발 Asset이므로 실제 장애물 보정은 골든 테스트 Asset에서 검증했다.
- Client UI에는 구조화된 거부 메시지 창이 없으며 개발 로그와 클릭 마커 처리만 제공한다.
- 레거시 C_Move를 개발 환경 변수로 다시 켜면 기존 즉시 위치 변경 의미가 돌아온다. 새 이동 흐름과 동시에 사용하면 안 된다.

## 15. 다음 서버 Tick 이동 Phase 연결점

정확한 연결점은 현재 Main loop가 호출하는 다음 경로다.

```text
Program room update loop
  -> GameRoom.Update()
       -> Flush()             // MoveRequest Job이 먼저 MovementState 확정
       -> Player.Update()     // 다음 Phase의 서버 이동 시뮬레이션 위치
```

다음 Phase에서는 `Player.Update()`가 활성 `PlayerMovementState`를 읽고 서버 Delta Time과 승인 경로를 기준으로 `Player.Info.Position`을 갱신해야 한다. 위치 갱신 후 별도의 Snapshot/Broadcast와 도착·취소 처리를 추가한다. A*를 먼저 도입한다면 MovementState에 Path/Revision을 추가하되, Handler가 아닌 Room Job에서 서버 현재 위치를 시작점으로 계산한다.

이번 Phase의 `AuthoritativeMoveService.Process()`와 `Player.ReplaceMovementState()`는 목적지 승인 경계이며, 다음 Phase에서도 파일 시스템이나 전역 NavigationRegistry 조회를 추가하지 않는다.