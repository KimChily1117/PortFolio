# Phase 4: Server Authoritative Grid Pathfinding

기준 문서: Phase 0~3 분석/구현 문서와 `navgrid-v1-format.md`.

## 1. 결론과 승인 순서

Phase 3 목적지 검증 뒤에 서버 A*를 추가했다. 시작과 목적 Cell이 각각 Walkable이어도 둘 사이에 경로가 없으면 거부한다.

```text
Session/Player/Room/Sequence 검증
Navigation identity 및 요청 좌표 검증
목적지 Cell 확인과 Blocked 목적지 보정
Player.Info.Position에서 시작 Cell 계산
시작 Cell 유효성/통과 가능 여부 확인
A* 및 경로 검증
ServerMoveId 발급
PlayerMovementState 교체
S_MoveAccepted 전송
```

경로 실패 시 ServerMoveId와 새 MovementState를 만들지 않는다. Sequence는 처리한 요청으로 소비하되 기존 활성 MovementState는 유지한다. Tick 이동, Snapshot, Broadcast, 경로 전송과 클라이언트 이동은 구현하지 않았다.

## 2. Phase 3 흐름 재검토

- `AuthoritativeMoveService.Process()`가 Blocked 보정과 Cell 중심 계산 뒤 `GenerateMoveId()`와 `ReplaceMovementState()`를 호출하던 구조였다.
- `S_MoveAccepted`는 서비스가 Accepted decision을 반환한 뒤 `PacketHandler`의 같은 Room Job에서 생성·전송된다.
- Sequence는 귀속 검증 뒤, 상태·Map/Hash·좌표·경로 검증 전에 소비된다. 실패 요청도 같은 Sequence로 재처리할 수 없다.
- 시작 Cell은 서버 `Player.Info.Position`과 `NavGridAsset.TryWorldToCell()`로 계산 가능하다.
- `Slow`는 Phase 3까지 `IsWalkable == true` 의미만 있었다.
- 서버 Player/Stat에는 Player별 이동 속도 필드가 없다. 이번 Phase에서 만들지 않았다.
- 독립 콘솔 테스트 프로젝트가 실제 서버 프로젝트와 `AuthoritativeMoveService`를 호출한다.

## 3. A* 구조와 격리

`Server/Game/Navigation/NavGridPathfinder.cs`에 다음을 추가했다.

```text
NavGridPathfinder
NavGridPathOptions
NavGridPathResult / NavGridPathStatus
NavGridPath
```

Pathfinder는 전달받은 `NavGridAsset`, 시작/목적 Cell, 옵션만 사용한다. Registry, 파일, Room, Player, Packet을 참조하지 않는다. netcoreapp3.1에 표준 `PriorityQueue<T>`가 없어 내부 Binary Min Heap을 구현했다.

Pathfinder 인스턴스는 변경 버퍼를 보유하지 않는다. 요청별 `gScore`, `cameFrom`, closed 배열과 Open Set을 사용하므로 여러 Room 호출이 상태를 공유하지 않는다. 145×145에서 고정 배열은 약 189 KB에 Open Set/경로 메모리가 추가된다. V1에서는 정확성과 격리를 우선해 Pool을 넣지 않았다.

## 4. 이동·비용·Corner 규칙

- XZ 8방향
- 직선 10, 대각선 14
- Walkable 진입 배율 1
- Slow 진입 배율 2
- Blocked 통과 불가
- 비용은 다음 Cell에 진입하는 기준
- 경로는 시작과 목적 Cell 모두 포함
- 시작과 목적이 같으면 Cell 1개, TotalCost 0

```text
직선 Walkable 10 / 대각선 Walkable 14
직선 Slow     20 / 대각선 Slow     28
```

대각선은 목적 대각 Cell과 이를 구성하는 두 직교 Cell이 모두 통과 가능해야 한다. 하나라도 Blocked면 Corner Cutting으로 판단해 금지한다. NavGrid V1 파일 포맷은 바꾸지 않았다.

## 5. Heuristic과 결정론

```text
dx = abs(goalX - x)
dz = abs(goalZ - z)
H = 14 * min(dx,dz) + 10 * (max(dx,dz) - min(dx,dz))
```

Slow 추가 비용은 Heuristic에 넣지 않아 admissible 조건을 유지한다.

Open Set 우선순위는 F, H, 삽입 순서, Cell index 오름차순이다. 이웃 순서는 북, 서, 동, 남, 북서, 북동, 남서, 남동이다. 같은 장애물 요청을 20회 실행해 동일 Cell 경로를 확인했다.

## 6. 제한과 RejectReason

```text
MaxExpandedNodes = 25,000
MaxPathCells      = 4,096
```

Room 0 전체 21,025 Cell을 탐색할 수 있으면서 더 큰 Grid 요청을 제한한다. `NoPath`, `SearchLimitExceeded`, `PathTooLong`, 잘못되거나 Blocked인 시작/목적을 구분한다.

기존 enum 0~13은 유지하고 끝에만 추가했다.

```text
14 NoPath
15 PathSearchLimitExceeded
16 PathTooLong
17 InvalidNavigationStart
```

`C_MoveRequest`, `S_MoveAccepted`, `S_MoveRejected`의 메시지와 필드 번호는 변경하지 않았다. 전체 경로도 전송하지 않는다.

## 7. 시작 Cell과 목적지 보정

시작점은 패킷에서 받지 않고 `Player.Info.Position`으로만 계산한다. 서버 위치가 맵 밖이거나 Blocked면 자동 보정하지 않고 `InvalidNavigationStart`로 거부하며 Room/Player/위치를 로그에 남긴다. Slow 시작 Cell은 허용한다.

Phase 3의 Blocked 목적지 BFS는 그대로 유지한다.

```text
반경 4 / 최대 방문 128
Walkable 또는 Slow 후보
후보를 A* Goal로 사용
시작점에서 도달 가능할 때만 승인
```

보정 후보가 다른 Walkable 섬이면 `NoPath`다.

## 8. NavGridPath와 MovementState

`NavGridPath`는 `StartCell`, `GoalCell`, 읽기 전용 `Cells`, `TotalCost`, `WasDestinationAdjusted`를 가진다. Cell 목록을 복사해 `ReadOnlyCollection`으로 노출하며 양 끝이 Start/Goal과 다르면 생성할 수 없다.

`PlayerMovementState`에는 다음을 추가했다.

```text
DestinationCell
NavGridPath Path
CurrentWaypointIndex
```

`Path.GoalCell == DestinationCell`을 생성자에서 검사한다. 경로가 둘 이상이면 다음 Waypoint index는 1, 같은 Cell이면 0이다. 이번 Phase에서는 index를 진행시키지 않는다.

새 요청이 성공한 순간 기존 상태를 비활성화하고 교체한다. 경로 실패 시 기존 MovementState는 유지되고, 실패 Sequence는 소비되며 재전송은 `InvalidSequence`다.

## 9. 변경 파일

서버:

- `Server/Game/Navigation/NavGridPathfinder.cs`
- `Server/Game/Movement/AuthoritativeMoveService.cs`
- `Server/Game/Movement/PlayerMovementState.cs`
- `Server/Properties/AssemblyInfo.cs`
- `NavGridTests/PathfindingPhase4Tests.cs`
- `NavGridTests/Program.cs`
- 서버 `Enum.proto`, 생성된 `Enum.cs` 및 PacketGenerator 산출물

클라이언트:

- C++ `Enum.proto`
- 생성된 `Enum.pb.h/.cc` 및 protobuf 산출물

문서:

- `docs/navigation-grid-phase4-server-pathfinding.md`
- `docs/README.md`

## 10. 테스트와 성능

- Phase 4 신규: 97 assertions
- C# 전체: 325 assertions
- Phase 3: 71 assertions 유지
- C++ NavGrid: 162 assertions
- C++/C# JSON: byte-identical
- 교차 SHA-256: `aadb9f2d02ce75d00281534c7a3c45e2e5fed05a313758f8ee62836d05aa1952`

검증 범위는 직선/대각선/같은 Cell, 장애물·긴 벽 우회, 고립 섬, Corner Cutting, 좁은 통로/테두리, Slow 비용/우회, Octile, 결정론, 시작/목적 오류, 탐색/길이 제한, MovementState 등록·보존과 Sequence 정책이다. 승인 전후 Player 위치가 바뀌지 않고 새 흐름에서 `S_Move`를 Broadcast하지 않는 기존 정책도 유지된다.

145×145 개발 환경 참고 측정:

| Fixture | 결과 | 확장 노드 | 경로 Cell | 시간 |
|---|---:|---:|---:|---:|
| 열린 Grid 대각선 | Success | 145 | 145 | 0.602 ms |
| 통과 구멍이 있는 긴 벽 | Success | 5,882 | 194 | 16.314 ms |
| 완전히 분리된 긴 벽 | NoPath | 10,440 | 0 | 28.434 ms |

시간은 참고값이며 통과 기준은 결과·비용·결정론과 노드 제한이다.

## 11. 빌드 결과

- 서버 Debug/Release 성공
- NavGridTests Debug 성공
- DummyClient Debug 성공
- EngineCore + DirectX Client Debug x64 성공
- C++/C# protobuf와 PacketGenerator 재생성 성공

기존 netcoreapp3.1 EOL, nullable, C++ 경고와 공유 Intermediate/PCH 경고는 유지된다.

## 12. 미구현 및 다음 연결점

미구현: 서버 Tick 경로 추종, index 진행, 이동 속도와 Slow 실제 감속, 도착/취소, Snapshot/Broadcast, 클라이언트 보간·예측·자동 이동, 경로 전송, Pool, Smoothing, Avoidance, 동적 장애물.

```text
Program Room update
  -> GameRoom.Update()
       -> Flush()
            MoveRequest Job + A* + MovementState 확정
       -> Player.Update()
            Path.Cells[CurrentWaypointIndex]
            다음 Cell 중심으로 서버 위치 진행
```

다음 Phase는 실제 경과 시간을 Update에 전달하고 다음 Cell 중심으로 서버 위치를 진행시킨다. Waypoint 도달 시 index 증가, Goal 도착 시 MovementState 비활성화를 수행한 뒤 Snapshot/Broadcast와 클라이언트 보간을 별도 계층으로 연결한다.