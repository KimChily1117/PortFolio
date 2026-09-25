# Navigation Grid Phase 0 분석

> 분석일: 2026-07-27  
> 클라이언트: `E:\task\C++\D3D_Popol`  
> 권위 서버: `E:\task\C++\D3D_Server`  
> 범위: 소스 코드 수정 없는 구조·권위·프로토콜·에셋 분석

## 1. 필수 결론

| 질문 | 확인 결과와 결론 |
| --- | --- |
| 현재 서버 권위 범위 | 요청 Cell이 존재하고 walkable인지 확인한 뒤 서버 위치를 Cell 중심으로 즉시 바꾸는 **종착점 필터와 복제 권위**만 가진다. 경로, 속도, 이동 시간, 도착과 경로 이탈은 서버 권위가 아니다. |
| 클라이언트의 좌표 결정 자유도 | 매우 크다. 클라이언트가 목적지, Cell, 대상 `objectId`, Y를 보낸다. 서버는 X/Z만 Cell 중심으로 보정하고 거리·속도·경로를 검사하지 않는다. 로컬 플레이어는 서버 `S_Move`도 무시한다. |
| 기존 이동 패킷 유지 가능성 | 필드 추가는 가능하지만 현재 `C_Move`는 즉시 위치 변경 의미와 신뢰하면 안 되는 `objectId/cellPos`를 포함한다. 장기 권위 모델에 그대로 유지하는 것은 안전하지 않다. |
| 목적지 요청 패킷 | 별도 `MoveRequest/Accepted/Rejected` 계열을 권장한다. 기존 `C_Move`를 재정의하려면 클라이언트와 서버의 lockstep 배포 및 구버전 차단이 필요하다. |
| 경로 계산 주체 | 서버가 최종 A* 경로를 계산해야 한다. 클라이언트 A*는 미리보기와 예측 전용이어야 한다. |
| 서버 직접 이동 시뮬레이션 | 가능하다. 서버에 100ms Room Tick, JobQueue, 서버 위치와 빈 `Player.Update()`가 있다. 이동 상태, Delta Time, Snapshot과 클라이언트 보정은 새로 필요하다. |
| Nav Asset 배포 위치 | 에디터가 생성하는 원본 Asset 하나를 정적 콘텐츠 산출물로 만들고 빌드/패키징 단계에서 양쪽 Resources에 동일 바이트로 복사해야 한다. 현재 수동 복제 방식은 부적합하다. |
| NavigationMapId 연결 | 현재 Room 0과 단일 Tilemap만 있고 Scene/Map 매핑이 없다. 서버에 `RoomId → NavigationMapId + Version + Hash + AssetPath` 설정이 필요하다. |
| 구현 난이도 | Phase 1 로더는 중간, 에디터 V1은 높음, 서버 목적지 검증은 중간, 서버 A*는 중간~높음, 서버 직접 이동·보정은 높음이다. |
| 최소 Phase 1 | 공용 바이너리 명세, 골든 Asset, C++/C# 로더, SHA-256, World/Cell 교차 테스트만 구현한다. Component, 에디터, A*, 이동 프로토콜은 제외한다. |

현재 구조를 완전한 서버 권위 이동이라고 부르기 어렵다. 서버는 최종 Cell을 검사하지만 실제 이동 과정은 클라이언트가 독립적으로 수행하고, 서버는 목적지에 즉시 도착한 상태를 저장한다.

## 2. 분석 대상과 프로토콜

실제 서버는 `E:\task\C++\D3D_Server`다. 처음 확인했던 `E:\task\Server`는 다른 Proto 계약이므로 이번 결론에서 제외했다.

DirectX 클라이언트와 D3D_Server의 이동 wire 계약은 대응한다.

```proto
message C_Move {
    uint64 objectId = 1;
    Vector3 targetPos = 2;
    Vector2Int cellPos = 3;
}

message S_Move {
    ObjectInfo Info = 1;
}
```

다만 Proto 원본은 하나가 아니다.

- C++: `D3D_Popol/Common/protoc-21.12-win64/bin/Protocol.proto`
- C#: `D3D_Server/Server/Common/protoc-3.12.3-win64/bin/Protocol.proto`
- C++ 생성: `GenPackets.bat`
- C# 생성: `GenProto.bat`와 서버 PacketGenerator
- C++ Client MsgId/switch는 수동 관리

두 Proto 파일은 필드가 대응하지만 SHA-256은 다르다. 새 패킷은 양쪽 원본, 생성 코드, 서버 PacketManager, C++ enum/switch를 함께 갱신해야 한다.

## 3. 클라이언트 분석

### 3.1 GameObject와 Component

- `ComponentType`과 고정 Component 배열을 사용한다.
- `MonoBehaviour`는 별도 `_scripts` vector에 들어간다.
- Reflection, Component Factory, 등록형 Inspector는 확인되지 않았다.
- Navigation Component 추가 시 enum, GameObject getter, Inspector 경로와 vcxproj를 명시적으로 수정해야 한다.

근거: `EngineCore/Component.h:6`, `EngineCore/Component.h:29`, `EngineCore/GameObject.cpp:338`.

### 3.2 Inspector와 에디터 기반

현재 Inspector는 `GameObject::GUIRender`에 직접 구현된 Transform 편집 창이다.

- 중앙 Selection Model이 없다.
- Component별 `OnInspectorGUI`가 없다.
- Transform Gizmo가 확인되지 않았다.
- 별도 Scene View RenderTarget/ImGui Image Viewport가 없다.
- 엔진 Undo/Redo Command Stack이 없다.
- 전용 Debug Line/Navigation Overlay Renderer가 없다.

근거: `EngineCore/GameObject.cpp:185`, `EngineCore/Scene.cpp:71`, `EngineCore/ImGuiManager.cpp:30`.

따라서 Phase 2는 단순 Component 추가가 아니다. 최소 Editor Selection, Tool Mode, 입력 소유권, Command Stack과 Debug Renderer 기반을 함께 만들어야 한다.

### 3.3 입력과 ImGui

Win32 메시지는 ImGui handler에 먼저 전달되지만 게임 입력 경로에서 `ImGuiIO::WantCaptureMouse`를 검사하지 않는다. `GUI->Update()` 뒤에 `SCENE->Update()`가 실행되고 PlayerController와 TestScene이 전역 INPUT을 직접 읽는다.

근거: `EngineCore/Game.cpp:91`, `EngineCore/Game.cpp:119`, `EngineCore/Game.cpp:121`, `GameCoding2/TestScene.cpp:45`, `GameCoding2/PlayerController.cpp:185`.

Navigation Tool은 Component `Update()`가 아니라 중앙 Editor Tool이 소유해야 한다. 입력 조건은 최소한 Tool Mode, ImGui Capture, 창 Hover, Stroke 상태와 Play/Edit 상태를 함께 확인해야 한다.

### 3.4 직렬화와 Asset

일반 Scene/Component 직렬화는 확인되지 않았다.

- Transform은 Object 이름별 JSON으로 저장된다.
- `Tilemap::Load`는 C++ raw binary struct를 읽는다.
- `Tilemap::Save`는 텍스트를 쓴다.
- 따라서 현재 Tilemap Load/Save는 상호 호환되지 않는다.
- Save는 모든 Cell을 무조건 walkable로 기록한다.
- Terrain 생성 때 파일을 자동 저장해 편집 데이터를 덮어쓸 수 있다.

근거: `EngineCore/GameObject.cpp:110`, `EngineCore/Tilemap.cpp:15`, `EngineCore/Tilemap.cpp:37`, `EngineCore/Terrain.cpp:39`.

Navigation Asset은 기존 Tilemap 저장과 완전히 분리해야 한다. raw C++ struct dump는 padding과 bool 표현 때문에 C# 공용 포맷으로 사용할 수 없다.

### 3.5 피킹과 좌표

현재 Ray-Plane Picking은 재사용할 수 있다.

```text
Scene::CreatePickingRay
→ World Ray를 Terrain inverse world로 변환
→ Local y=0 Plane 교차
→ Local XZ Bounds 검사
→ World Position 반환
```

근거: `EngineCore/Scene.cpp:179`, `EngineCore/Terrain.cpp:45`.

`CreatePickingRay`는 private이므로 Navigation Tool용 제한된 public API 또는 Camera 공용 유틸리티로 분리해야 한다.

현재 Terrain은 145×145, 로컬 X/Z `[0,145]`, World Position `(0,2,0)`, Rotation 0이다. 기존 Tile 변환은 origin/rotation/scale을 고려하지 않고 `floor(world / tileSize)`만 수행한다.

V1의 가장 작은 안전한 제약은 Rotation 0, Scale 1, 명시적인 World Origin이다. 회전 Grid를 허용하려면 서버도 해석할 yaw 또는 basis를 Asset에 저장해야 한다.

### 3.6 현재 이동과 서버 보정

우클릭 시 클라이언트가 `_dest`를 결정하고 `C_Move`를 TCP로 한 번 보낸 뒤, 승인 응답을 기다리지 않고 `PlayerController::MoveTo`가 매 프레임 Transform을 이동한다.

근거: `GameCoding2/PlayerController.cpp:185`, `:243`, `:251`, `:305`.

`S_Move` 수신 시 다른 플레이어는 목적지로 보간하지만 자기 ObjectId이면 무시한다. Reject와 자기 위치 보정 경로도 없다.

근거: `GameCoding2/ClientPacketHandler.cpp:419`, `:428`, `GameCoding2/OtherPlayerController.cpp:64`.

현재 D3D_Popol과 D3D_Server 사이에는 TCP 8080 이동만 확인됐다. UDP 이동, UDP Sequence, UDP Rate Limit은 이 프로젝트 쌍에 없다.

## 4. 서버 분석

### 4.1 Session, Room, Tick

- 연결 시 모든 Session이 고정 Room 0에 들어간다.
- `ClientSession`은 자신의 Player와 GameRoom을 보유한다.
- SceneReady/MapReady handshake는 없다.
- `GameRoom`은 JobSerializer를 상속하고 Handler는 `room.Push`로 상태를 변경한다.
- Main loop는 약 100ms마다 Room을 Update한다.
- Room은 Job Flush 후 Player.Update를 호출하지만 Player.Update는 비어 있다.

근거: `ClientSession.cs:18`, `:51`, `Program.cs:54-57`, `GameRoom.cs:483-488`, `Player.cs:15`.

서버 직접 이동을 넣을 자리는 있지만 현재 10Hz Sleep과 실제 Delta Time 부재는 보완해야 한다.

### 4.2 현재 C_Move 검증

서버는 다음 순서로 처리한다.

```text
패킷 objectId로 Room Object 조회
패킷 cellPos 사용
음수 Cell 거부
Tile 존재/IsWalkable 확인
X/Z를 Cell 중심으로 보정
Y는 targetPos.Y 신뢰
서버 위치를 목적지로 즉시 변경
S_Move Broadcast
```

근거: `PacketHandler.cs:50`, `:66`, `:70`, `:80`, `:89`, `:91`, `:98`.

검사하지 않는 항목:

- 패킷 ObjectId와 Session Player의 일치
- NaN/Infinity
- World Bounds
- Sequence와 Rate Limit
- 시작 위치/시작 Cell
- 경로 존재와 장애물 통과
- 거리, 속도, 이동 시간
- 캐릭터 상태와 도착
- NavigationMapId/Version/Hash

다른 플레이어의 ObjectId를 알면 그 플레이어의 서버 위치를 변경할 수 있는 구조다. 서버는 요청의 ObjectId가 아니라 `clientSession.Player`를 기준으로 처리해야 한다.

### 4.3 현재 Tilemap과 정적 데이터

서버는 Working Directory 의존 상대 경로 `../../../../Resources/TilemapData.txt`를 읽는다. 파일 로드 실패 시 Room 활성화를 막지 않으며 `GetTileAt`은 전체 Tile을 선형 순회한다.

현재 145×145 파일은 21,025 Cell 전부 walkable이다. 클라이언트와 서버 사본의 SHA-256은 현재 같지만 자동 패키징이 아닌 수동 복제다.

근거: `GameRoom.cs:465`, `Tilemap.cs:68`, `Tilemap.cs:133`.

NavGrid는 `cells[z * width + x]`의 O(1) 조회로 구현해야 한다.

### 4.4 Map/Scene과 테스트

- Room 0과 고정 Tilemap 하나만 존재한다.
- NavigationMapId/SceneId/MapId 매핑이 없다.
- `C_RequestMap/S_UpdateMap`은 정의됐지만 Handler가 비어 있다.
- 자동 테스트 프로젝트는 확인되지 않았다.
- 서버 정적 데이터 로더는 Tilemap 텍스트 로더만 확인됐다.

## 5. 현재 권위 판정

| 단계 | 현재 결정 주체 | 판정 |
| --- | --- | --- |
| 클릭 World 목적지 | 클라이언트 | 정상 입력 역할 |
| 목적 Cell | 클라이언트 | 서버가 재계산해야 함 |
| 이동 가능 여부 | 서버가 클라이언트 Cell만 검사 | 부분 권위 |
| 경로 | 없음 | 권위 없음 |
| 이동 속도 | 클라이언트 `_speed=2` | 클라이언트 권위 |
| 실제 로컬 위치 | 클라이언트 매 프레임 | 클라이언트 권위 |
| 서버 위치 | 목적지로 즉시 변경 | 상태 권위지만 시뮬레이션 아님 |
| 자기 위치 보정 | 자기 S_Move 무시 | 없음 |
| 도착 판정 | 각 클라이언트 | 클라이언트 권위 |

가장 먼저 제거할 신뢰는 `objectId`, `cellPos`, `targetPos.Y`다.

## 6. 이동 프로토콜 권장

기존 `C_Move`에 Sequence, MapId, Version, Hash를 추가할 수는 있다. 그러나 기존 즉시 이동 의미와 충돌하고 `S_Move`에는 승인/거부와 Sequence가 없다.

Phase 3 이후에는 별도 의미를 권장한다.

```text
C_MoveRequest
- clientMoveSequence
- requestedDestinationX/Z
- navigationMapId
- navigationContentVersion
- navigationContentHash
- optional clientTimestamp

S_MoveAccepted
- clientMoveSequence
- serverMoveId
- acceptedDestination
- serverStartPosition
- navigationMapId/version
- pathRevision
- 제한된 Waypoint 또는 주요 Waypoint

S_MoveRejected
- clientMoveSequence
- rejectReason
- serverPosition
- navigationMapId/version
```

PlayerId는 Session에서 얻고 요청에서 제거한다. 기존 `C_MOVE=2`를 교체하는 방식은 구버전 혼용을 확실히 차단할 때만 선택한다.

목적지 요청과 승인/거부는 TCP로 시작할 수 있다. UDP는 별도 Phase이며 존재하지 않는 기존 Sequence/Rate Limit을 재사용한다고 가정하면 안 된다.

## 7. 서버 경로와 클라이언트 예측 비교

| 방식 | 판단 |
| --- | --- |
| A. 서버 경로/주요 Waypoint 전송 | 서버 A*와 클라이언트 표시를 맞추기 쉽다. 크기 제한과 Path Revision이 필요하다. Phase 4 중간 단계로 권장한다. |
| B. 목적지만 승인하고 양쪽 A* | 패킷은 작지만 C++/C# tie-break, 경계, 우선순위 큐까지 맞아야 한다. 현재 공용 테스트가 없어 주 경로로 비권장한다. |
| C. 서버 직접 시뮬레이션 + Snapshot | 가장 강한 권위다. Room Tick과 Player.Update를 활용할 수 있지만 보간·보정·Snapshot이 필요하다. 최종 목표로 권장한다. |

권장 전환은 `Phase 3 목적지 검증 → Phase 4 서버 A*와 제한 Waypoint → Phase 5 서버 직접 이동과 Snapshot`이다. Phase 3만으로 완전한 서버 권위라고 부르면 안 된다.

## 8. 공용 Nav Asset V1 권장

### 포맷

명시적 Little-Endian 바이너리를 권장한다. JSON보다 canonical hash가 명확하고 raw struct dump 없이 고정 크기 검증이 가능하다.

V1 Cell은 3바이트가 적합하다.

```text
byte 0: bakedType
byte 1: manualType, 0xFF면 override 없음
byte 2: movementCost
```

Manual Override와 Cost를 함께 보존하려면 1바이트로는 부족하다. V1에서는 RLE/Bit Packing을 사용하지 않는다.

필수 헤더:

```text
Magic = NVG1
FormatVersion
HeaderSize
NavigationContentVersion
NavigationMapIdLength
GridWidth/GridHeight
CellSize
OriginX/Y/Z
DefaultAgentRadius
CellEncoding/CellStride
CellCount/CellDataLength
NavigationContentHash[32]
NavigationMapId UTF-8 bytes
Cell bytes
```

규칙:

- Little-Endian, IEEE-754 binary32
- Width×Height overflow 및 최대 크기 검사
- CellCount와 Width×Height 일치
- CellDataLength와 CellCount×Stride 일치
- 알 수 없는 버전/인코딩 거부
- 서버 로드 실패 시 Room 활성화 금지
- SHA-256 hash field 자체, 절대 경로, 수정 시간은 Hash 입력에서 제외
- Hash에는 MapId, Version, Grid 설정, Agent 설정, baked/manual/cost 전체 포함

### 좌표 규칙

```text
논리 평면: World XZ
index = z * width + x
0 <= x < width, 0 <= z < height
x = floor((worldX - originX) / cellSize)
z = floor((worldZ - originZ) / cellSize)
Cell center = origin + ((x+0.5)*cellSize, 0, (z+0.5)*cellSize)
상단/우측 최대 경계는 Grid 밖
NaN/Infinity는 변환 전에 거부
```

V1은 Rotation 0, Scale 1로 제한하는 것이 가장 안전하다. 회전 Grid가 필요하면 yaw 또는 basis를 새 FormatVersion에 포함한다.

## 9. NavigationMapId와 배포

서버에는 다음 명시적 연결이 필요하다.

```text
RoomId
→ NavigationMapId
→ ContentVersion
→ ExpectedHash
→ Packaged Asset Path
```

현재 `C_RequestMap/S_UpdateMap`을 전체 Nav Asset 전송 용도로 부활시키는 것은 권장하지 않는다. Nav Asset은 정적 콘텐츠로 함께 배포한다.

가장 자연스러운 authoring 위치는 클라이언트의 새 `Resources/Navigation`이다. 서버가 런타임에 클라이언트 작업 폴더를 직접 참조해서는 안 된다. 패키징 단계가 동일 파일을 서버 `Resources/Navigation`과 양쪽 출력 디렉터리에 복사하고 SHA-256 불일치 시 실패해야 한다. 서버는 Working Directory가 아니라 `AppContext.BaseDirectory` 기준 경로를 사용해야 한다.

## 10. 예상 수정 범위와 난이도

아래 이름은 구현 제안이며 현재 존재하는 클래스로 간주하지 않는다.

### Phase 1 — 중간

- 공용 포맷 명세와 골든 `.navgrid`
- C++ Navigation Asset 데이터/로더와 vcxproj 등록
- C# Navigation Asset 데이터/로더
- 서버 Resources 패키징
- 공용 좌표·Hash·손상 파일 테스트

### Phase 2 — 높음

- `ComponentType` 및 GameObject 접근
- Inspector 구조 분리
- 중앙 Editor Selection/Tool Mode
- Picking Ray 공용화
- ImGui 입력 점유
- NavigationGridComponent/EditorTool/DebugRenderer
- Stroke 단위 Undo/Redo

기존에 Selection, Gizmo, Scene Viewport, Command Stack이 완성돼 있지 않아 난이도가 높다.

### Phase 3 — 중간

- 양쪽 Proto와 생성 코드
- C++ MsgId/switch
- 서버 PacketHandler, ClientSession, GameRoom/RoomManager
- Navigation Registry/Grid
- 클라이언트 승인/거부 처리

### Phase 4 — 중간~높음

- 서버 A*, Player 이동 상태, Path Revision
- Waypoint 제한/직선 압축
- 클라이언트 예측 경로 교정

### Phase 5 — 높음

- 서버 Delta Time과 이동 시뮬레이션
- Snapshot/Correction
- Sequence/Rate Limit
- 클라이언트 보간 및 상태 기반 거부

## 11. 가장 작은 안전한 Phase 1

포함:

1. `navgrid-v1-format.md`
2. Little-Endian V1 명세
3. 고정 3바이트 Cell Encoding
4. SHA-256 canonical hash
5. 작은 골든 Asset
6. C++ 로더
7. C# 로더
8. 양쪽 메타데이터/Cell/Hash 비교
9. WorldToCell/CellToWorld 공용 테스트 벡터
10. 손상, 길이, 버전, 과대 크기 거부 테스트

제외:

- Component와 ImGui Editor
- Grid 렌더링과 페인팅
- A*
- 이동 패킷과 서버 이동 변경
- 기존 Tilemap 제거

완료 조건은 같은 파일을 양쪽이 동일하게 읽고 같은 좌표 변환을 내며 잘못된 파일을 같은 정책으로 거부하는 것이다. 이 단계는 기존 게임 이동에 영향을 주지 않아야 한다.

## 12. 주요 위험과 선행 결정

즉시 인지할 위험:

1. 패킷 ObjectId로 다른 Room Object를 이동시킬 수 있음
2. 자기 서버 이동 응답을 클라이언트가 무시함
3. 거리·속도·경로 검증 없음
4. 서버 위치가 요청 즉시 목적지로 순간이동
5. 현재 21,025 Cell이 모두 walkable
6. 기존 Tilemap Load/Save 포맷 불일치
7. Terrain 생성 시 파일 자동 덮어쓰기
8. 정적 데이터 수동 복제
9. 서버 경로가 Working Directory 의존
10. Proto 원본이 양쪽에 따로 존재

구현 전 결정:

1. V1 Grid 회전 금지 또는 yaw/basis 저장
2. NavigationMapId naming 규칙
3. 막힌 목적지 Reject 또는 최근접 Walkable 보정
4. 전체 Path 또는 주요 Waypoint 전송
5. Snapshot 주기
6. TCP 유지 또는 향후 UDP Snapshot
7. 서버 이동 속도의 데이터 소스
8. 클라이언트 예측을 즉시 이동으로 할지 경로 표시만 할지

## 13. 권장 다음 단계

```text
명세 확정
→ 골든 Asset
→ C++ Loader
→ C# Loader
→ 교차 언어 테스트
→ 패키징 동일 Hash 검증
```

데이터 계약이 검증된 뒤에만 Navigation Component와 Editor Tool로 넘어간다.