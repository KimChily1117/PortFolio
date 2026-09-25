# Phase 5: Server Path Following and Authoritative Movement Snapshot

## 결과 요약

Phase 4의 `PlayerMovementState.Path`를 서버 Room Tick에서 실제로 추종하도록 연결했다. 서버가 `Player.Info.Position`을 시간 기반으로 갱신하고, 이동 중·도착·취소 상태를 신규 `S_MovementSnapshot`으로 Room 전체에 전달한다. 클라이언트는 자기 Player와 원격 Player 모두 같은 Snapshot을 처리하며, 입력 예측 없이 100ms 시간 기반 보간을 사용한다.

이번 Phase에는 Path Smoothing, Local Avoidance, 동적 장애물, UDP, 입력 예측 및 Reconciliation을 추가하지 않았다.

## 1. 서버 Tick과 Delta Time

실제 서버 루프는 약 100ms마다 `RoomManager.UpdateRooms()`를 호출한다. 기존 코드는 고정 `0.1f`를 Player에 전달하지도, 실제 경과시간을 측정하지도 않았다.

변경 후 규칙:

- `RoomManager`가 `Stopwatch.GetTimestamp()`로 Room 갱신 사이의 실제 경과시간을 계산한다.
- `GameRoom.Update(float deltaTime)`에 초 단위로 전달한다.
- `GameRoom.Update()` 호환 overload는 테스트/기존 호출을 위해 0.1초를 전달한다.
- 음수, NaN, Infinity는 0초로 거부한다.
- 0초는 유효하며 위치를 진행시키지 않는다.
- 0.25초 초과는 0.25초로 Clamp한다.
- Clamp와 invalid delta는 개발 로그에 남긴다.
- `GameRoom.Update` 순서는 `delta 정규화 -> tick 증가/계측 초기화 -> Flush -> Player.Update(delta) -> Projectile.Update`이다.

이 구조에서는 Room JobQueue에 들어온 MoveRequest가 같은 Tick의 `Flush()`에서 승인된 뒤 곧바로 Player 이동 단계에 진입할 수 있다.

## 2. 이동 속도와 Slow 정책

서버의 `Player.MovementSpeed`가 권위 속도이다.

- 기본 속도: 2 world units/second
- 허용 범위: 유한값, 0 이상, 50 이하
- 클라이언트는 속도를 전송하지 않는다.
- V1 Slow 실제 이동 정책: 현재 Segment가 진입하는 목적 Cell이 `Slow`이면 기본 속도의 50%
- Phase 4 A* 비용 정책인 Slow 진입 비용 2배와 의미가 대응한다.

## 3. Waypoint 진행 규칙

`NavGridPath.Cells`는 시작 Cell과 목적 Cell을 모두 포함한다.

- `Path[0]`: 서버 현재 위치가 속한 Start Cell
- `CurrentWaypointIndex` 초기값: 1
- 실제 첫 목표: `Path[1]` Cell 중심
- 서버 현재 위치가 Start Cell 중심이 아니어도 현재 위치에서 `Path[1]` 중심으로 이동한다.
- 길이 1 경로는 첫 Room Tick에서 즉시 도착한다. Delta Time이 0이어도 처리한다.
- 한 Tick의 `remainingTime`을 사용하여 여러 Waypoint를 통과한다.
- 각 Segment마다 해당 Cell 타입으로 속도를 다시 계산한다.
- 마지막 Waypoint를 통과하면 승인된 `MovementState.Destination`으로 정확히 Snap한다.

빈 Path, 범위를 벗어난 WaypointIndex, Goal/Destination 불일치, NavGrid 밖 Waypoint, Blocked Waypoint, 비유한 위치가 발견되면 이동을 취소하고 로그와 Cancelled Snapshot을 만든다.

## 4. 권위 위치와 Tile 동기화

이동은 XZ 평면에서 계산한다. 승인 목적지와 Waypoint 중심의 Y는 직접 사용하지 않고 서버 시작 위치의 Y를 유지한다. 따라서 현재 Room 0 Player의 Y=2 규칙이 보존된다.

위치가 바뀔 때:

- `Player.Info.Position.X/Y/Z` 갱신
- `Navigation.TryWorldToCell`로 현재 Cell 검증
- `Player.TileX/TileZ` 갱신
- `GameRoom.UpdatePlayerTilePosition`으로 기존 Tile 점유 목록 갱신

이동 후 현재 위치가 NavGrid 밖이거나 Blocked Cell이면 이동을 취소한다. 정적 Path를 전제로 미세 위치마다 별도 A*를 재실행하지 않는다.

## 5. 이동 상태, 도착, 취소

- MoveRequest 성공: 이전 상태를 비활성화하고 새 `PlayerMovementState`를 등록하며 `ObjectInfo.State=Move`
- MoveRequest 실패: 기존 활성 MovementState 유지
- 이동 중: `Moving` Snapshot
- 도착: 정확한 Destination Snap, `MovementState.Complete()`, `CurrentWaypointIndex=Path.Count`, `ObjectInfo.State=Idle`, `Arrived` Snapshot 1회
- 취소: 현재 권위 위치 유지, MovementState 비활성화, Move 상태라면 Idle, `Cancelled` Snapshot 1회

연결된 취소 지점:

- 새 MoveRequest 성공 시 기존 상태 교체
- Room에서 Player 제거
- 연결 종료가 호출하는 Room 제거
- Garen 스킬 사망 처리
- Annie 스킬 사망 처리
- Projectile 사망 처리
- Player.Update에서 HP<=0, Skill 상태, Room/Path/위치 이상 감지

별도 사망/스턴 상태 머신은 만들지 않았다. 현재 프로젝트에는 Stun 및 Casting 상태 모델이 없다.

## 6. Snapshot 프로토콜

신규 Packet ID:

```text
S_MOVEMENT_SNAPSHOT = 24
```

필드:

| 번호 | 필드 | 타입 | 의미 |
|---:|---|---|---|
| 1 | ObjectId | uint64 | 서버가 선택한 이동 Player |
| 2 | ServerMoveId | uint64 | Room이 발급한 이동 ID |
| 3 | ServerTick | uint64 | Room Update tick |
| 4 | Position | Vector3 | 서버 권위 위치 |
| 5 | MovementState | enum | Moving / Arrived / Cancelled |
| 6 | CurrentWaypointIndex | uint32 | 다음 목표 또는 완료 index |
| 7 | ClientMoveSequence | uint32 | 원 요청 sequence |
| 8 | RoomId | int32 | Room 전환 경계 검증 |

`S_Move`는 레거시 순간 위치 변경 의미와 자기 Player 무시 동작이 있으므로 재사용하지 않았다. 신규 Snapshot은 자기 Player를 포함한 Room의 모든 연결 Player에게 전송된다. AOI 시스템은 현재 존재하지 않는다.

전송 정책:

- `S_MoveAccepted`: 요청 승인 직후 요청자에게 전송
- 이동 중: 위치가 실제로 바뀐 Tick마다 Snapshot 1개
- 도착: 최종 Snapshot 즉시 1개
- 취소: 취소 Snapshot 즉시 1개
- 정지 Player: 반복 전송 없음
- 현재 Room 주기가 약 100ms이므로 이동 Player당 최대 약 10 Snapshot/second

Room 이동 Tick 내부에서는 기존 `Broadcast()`의 재-enqueue 동작을 사용하지 않고, Room Job 문맥에서 세션을 즉시 순회한다. 테스트에서는 `MovementSnapshotSink`로 직렬화 수와 payload를 계측한다.

## 7. 클라이언트 처리와 보간

`BasePlayerController`에 자기/원격 공용 권위 Snapshot 상태를 추가했다.

순서 검증:

- 현재보다 작은 `ServerMoveId`: 무시
- 같은 `ServerMoveId`에서 같거나 작은 `ServerTick`: 무시
- `S_MoveAccepted.ServerMoveId`: 로컬 Player의 최소 허용 이동 ID로 등록
- Snapshot RoomId가 현재 `NavigationRoomId`와 다르면 무시
- 비유한 위치 또는 Unknown 상태 거부

보간:

- 보간 시작점: Snapshot 수신 시 현재 Transform 위치
- 목표: 최신 서버 권위 위치
- 기간: 서버 주기와 같은 0.1초
- 방식: 경과시간을 Clamp한 유한 `Vec3::Lerp`, t=1에서 정확히 목표 도달
- 오차가 3 world units 초과: 즉시 Snap
- Arrived: 즉시 최종 위치 Snap
- Cancelled: 현재 서버 위치까지 짧게 보정

Moving은 Run, Arrived/Cancelled는 Idle 애니메이션을 사용하되 공격/사망 애니메이션은 덮어쓰지 않는다. 로컬 권위 모드에서는 기존 `MoveTo()`가 실행되지 않으며, 원격 Player는 첫 권위 Snapshot 이후 기존 `S_Move` 목표 추종을 중단한다. 따라서 두 이동 시스템이 같은 Transform을 동시에 수정하지 않는다.

로컬 목적지 마커는 MoveAccepted의 승인 목적지로 보정되며 Arrived/Cancelled 또는 MoveRejected에서 제거된다.

## 8. A* 계측과 Rate Limit

`AuthoritativeMoveService`는 A* 전후를 `Stopwatch`로 측정한다.

- 요청별 expanded node count
- 요청별 elapsed milliseconds
- 현재 Room Tick의 누적 expanded nodes
- 현재 Room Tick의 누적 elapsed milliseconds
- 10ms 이상 누적 시 slow pathfinding 로그

A*는 Room JobQueue 안에서 동기 실행한다. `Task.Run()`은 사용하지 않았다.

세션별 MoveRequest 토큰 버킷:

- refill: 초당 12개
- burst capacity: 4개
- Sequence가 최신이면 먼저 소비한 뒤 Rate Limit 검사
- Rate Limit으로 거부된 sequence도 재처리할 수 없음
- Rate Limit 거부는 기존 MovementState를 제거하지 않음
- 새 세션/Player 진입에서 sequence와 token bucket 초기화
- 신규 RejectReason: `MOVE_REJECT_REASON_RATE_LIMITED = 18`; 기존 enum 숫자는 유지

## 9. 테스트 결과

C# 전체 결과:

```text
Phase 3: 71 assertions
Phase 4: 97 assertions
Phase 5: 72 assertions
전체 NavGrid/Registry/Movement: 397 assertions
```

Phase 5는 다음을 포함한다.

- 정상/0/음수/NaN/Infinity/Clamp Delta Time
- 일부 Segment 이동
- 정확한 Waypoint 및 다중 Waypoint 통과
- 길이 1 Path 도착
- 최종 Destination Snap 및 중복 도착 방지
- 기본 속도, 0/잘못된 속도, Slow 50%
- Player.Info.Position Y 보존과 Tile Cell 동기화
- Moving/Arrived/Cancelled Snapshot과 protobuf round-trip
- 정지 Player Snapshot 미전송
- 세션별 token bucket burst/refill
- Rate Limit 거부 시 기존 MovementState 유지
- 1/10/50/100명 이동 Tick 성능

C++ NavGrid 회귀:

```text
162 assertions passed
Content SHA-256: d74739cc31aa532815ce2d3551d58962fc430e9e871df4fd374f8f2f26480a77
```

C++/C# 교차 결과:

```text
byte-identical
Result SHA-256: aadb9f2d02ce75d00281534c7a3c45e2e5fed05a313758f8ee62836d05aa1952
```

## 10. 성능 측정

145x145 열린 Grid, 100ms 한 Tick, 각 Player가 동일한 145 Cell 대각선 Path를 추종하는 테스트 결과이다. 실행 시간은 개발 환경 참고값이며 통과 기준이 아니다.

| 이동 Player | Room Update | Snapshot | 단일 payload 합 | Room 전체 Broadcast 추정/초 | Thread allocation |
|---:|---:|---:|---:|---:|---:|
| 1 | 0.240 ms | 1 | 39 B | 390 B/s | 208 B |
| 10 | 0.026 ms | 10 | 390 B | 39,000 B/s | 1,216 B |
| 50 | 0.078 ms | 50 | 1,950 B | 975,000 B/s | 5,696 B |
| 100 | 0.134 ms | 100 | 3,900 B | 3,900,000 B/s | 11,296 B |

Broadcast 추정값은 AOI가 없는 현재 구조에서 `payload * 수신 Player 수 * 10 tick/s`로 계산했다. 100명에서는 약 3.9 MB/s이므로 다음 네트워크 확장 전에 AOI 또는 snapshot batching/delta compression 검토가 필요하다.

Phase 4 A* 참고 재측정:

- open diagonal: 145 expanded, 약 0.652ms
- partial wall: 5,882 expanded, 약 17.269ms
- no path wall: 10,440 expanded, 약 30.101ms

## 11. 빌드 결과

- Server Debug: 성공
- Server Release: 성공
- DummyClient Debug: 성공
- DummyClient Release: 성공
- Client Debug x64 `Client` 타깃: 성공, `Binaries/Client.exe`
- Client Release x64: 실패. 기존 `FXC X3501: main entrypoint not found` 설정 문제이며 Phase 5 C++ 컴파일 전에 셰이더 단계에서 실패한다.

전체 Debug 솔루션은 Client 자체는 성공했지만 AssimpTool이 `GameCoding2/ClientPacketHandler.cpp`를 포함하면서 Protocol 및 PlayerController 구현을 링크하지 않는 기존 프로젝트 구성 때문에 AssimpTool 링크가 실패한다. 실제 Client 타깃은 별도로 성공했다. 또한 여러 vcxproj가 `Intermediate/Debug`를 공유하는 기존 MSB8028 경고가 있으므로 `/m:1`로 검증했다.

서버는 netcoreapp3.1 EOL 경고와 기존 nullable/미사용 필드 경고만 남는다.

## 12. 변경 파일

클라이언트 저장소:

- `Common/protoc-21.12-win64/bin/Enum.proto`
- `Common/protoc-21.12-win64/bin/Protocol.proto`
- `Common/protoc-21.12-win64/bin/Enum.pb.h/.cc`
- `Common/protoc-21.12-win64/bin/Protocol.pb.h/.cc`
- `GameCoding2/Enum.pb.h/.cc`
- `GameCoding2/Protocol.pb.h/.cc`
- `GameCoding2/ClientPacketHandler.h/.cpp`
- `GameCoding2/BasePlayerController.h/.cpp`
- `GameCoding2/OtherPlayerController.cpp`
- `GameCoding2/GarenOtherPlayerController.cpp`
- `GameCoding2/AnnieOtherPlayerController.cpp`
- `GameCoding2/PlayerController.h/.cpp`
- `Tests/NavGrid/Results/cpp-phase5-result.json`
- `Tests/NavGrid/Results/csharp-phase5-result.json`
- `docs/navigation-grid-phase5-authoritative-movement.md`

서버 저장소:

- `Server/Common/protoc-3.12.3-win64/bin/Enum.proto`
- `Server/Common/protoc-3.12.3-win64/bin/Protocol.proto`
- 생성된 `Server/Packet/Enum.cs`, `Protocol.cs`, `ServerPacketManager.cs`
- 생성된 DummyClient Protocol/Enum/Struct 파일
- `Server/Game/Movement/ServerMovementSettings.cs`
- `Server/Game/Movement/PlayerMovementState.cs`
- `Server/Game/Movement/AuthoritativeMoveService.cs`
- `Server/Game/Objects/Player.cs`
- `Server/Game/Room/GameRoom.cs`
- `Server/Game/Room/RoomManager.cs`
- `Server/Session/ClientSession.cs`
- `Server/Packet/PacketHandler.cs`
- `Server/Game/ChampSpell/GarenSkillHandler.cs`
- `Server/Game/ChampSpell/AnnieSkillHandler.cs`
- `Server/Game/Objects/Projectile.cs`
- `NavGridTests/MovementPhase5Tests.cs`
- `NavGridTests/MovementPhase3Tests.cs`
- `NavGridTests/Program.cs`

## 13. 알려진 제한

- AOI가 없어 Snapshot이 Room 전체에 전달된다.
- Snapshot batching, delta compression, quantization이 없다.
- 이동 방향은 Protocol에 포함하지 않았다. 서버 ObjectInfo에도 기존 방향 표현이 없다.
- 서버 Tick은 여전히 `Thread.Sleep(100)` 기반이며 fixed-step accumulator는 아니다.
- A*가 Room JobQueue에서 동기 실행되므로 최악 요청이 Tick을 지연시킬 수 있다.
- 동적 장애물이 없어 승인 후 Path Cell이 바뀌는 정상 런타임 시나리오는 없다.
- Stun/Casting/Room transition 전용 상태 머신이 없다.
- 클라이언트 입력 예측과 reconciliation history가 없다.
- TCP Snapshot을 사용하므로 head-of-line blocking 가능성이 있다.
- Client Release 셰이더 entrypoint와 AssimpTool 링크 구성은 별도 빌드 설정 수정이 필요하다.

## 14. 다음 단계 연결점

Path Smoothing을 먼저 진행한다면 서버 `NavGridPath` 생성 직후, `PlayerMovementState` 등록 전에 Cell Path에서 시각/이동 Waypoint를 분리하는 것이 안전하다. A*의 검증용 Cell Path는 그대로 보존하고 smoothing 결과만 별도 Waypoint 컬렉션으로 두어야 한다.

Editor Phase를 진행한다면 Phase 1 NavGrid V1 포맷을 변경하지 않고 `NavigationGridComponent -> 별도 .navgrid 저장 -> 서버 Content 배포` 흐름을 구현할 수 있다.

네트워크 확장을 먼저 진행한다면 `GameRoom.BroadcastMovementSnapshot`이 정확한 연결점이다. 이 위치에서 AOI, multi-object Snapshot batch, 위치 quantization을 추가할 수 있으며 `Player.Update`의 권위 계산은 변경할 필요가 없다.