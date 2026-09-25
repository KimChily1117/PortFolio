# Phase 2: Server Navigation Runtime Registration

기준 문서:

- [`navigation-grid-phase0-analysis.md`](./navigation-grid-phase0-analysis.md)
- [`navgrid-v1-format.md`](./navgrid-v1-format.md)
- [`navigation-grid-phase1-implementation.md`](./navigation-grid-phase1-implementation.md)

## 1. 구현 전 서버 구조

- 프로세스 시작점은 `D3D_Server/Server/Server/Server/Program.cs:24`의 `Main`이다.
- 기존에는 `Listener.Init()`으로 포트를 연 뒤 `RoomManager.Instance.Add(0)`을 호출했다.
- 기존 설정 시스템, Content Root 및 `AppContext.BaseDirectory` 사용은 없었다.
- Room 0은 Program에서 직접 생성되며, `ClientSession.OnConnected`가 Room 0을 고정 조회한다.
- Scene/Map 식별자와 Room의 기존 연결은 없었다.
- `RoomManager.Add`는 기본 `GameRoom`을 Dictionary에 먼저 넣고 `GameRoom.Init()`을 호출했다.
- `GameRoom.Init()`은 Working Directory 상대 경로 `../../../../Resources/TilemapData.txt`를 직접 사용했다.
- 기존 Room 0 Tilemap은 145×145, X/Z Bounds `[0,145) × [0,145)`이고 21,025 Cell이다.
- Room 생성은 네트워크 수신 및 Room JobQueue 사용 전이므로 생성자에서 불변 정적 참조를 설정해도 안전하다.
- 테스트 기반은 Phase 1의 Console runner뿐이어서 실제 Server 프로젝트 참조 방식으로 확장했다.

## 2. 최종 시작 흐름

```text
Program.Main
  Content Root 결정
  Content/Navigation/navigation-maps.json 로드
  모든 enabled 설정 검증
  Phase 1 NavGridAssetLoader로 모든 required Asset 로드
  MapId / FormatVersion / ContentHash 검증
  NavigationRegistry 완성
  현재 접속 흐름에 필요한 Room 0 매핑 확인
  RoomManager.Initialize(registry)
  설정에 등록된 Room 생성
  Legacy Tilemap 로드
  NavGrid / Tilemap Bounds 검증
  완전 초기화된 Room만 Dictionary 등록
  Listener.Init
```

필수 설정이나 Room 초기화가 실패하면 Listener를 열지 않고 프로세스 시작을 종료한다. Lazy Load와 전체 Walkable fallback은 없다.

## 3. Content Root와 경로 정책

기본 Content Root:

```text
AppContext.BaseDirectory/Content
```

Debug 빌드에서만 다음 명령행 Override를 허용한다.

```text
Server.exe --content-root=<absolute-path>
```

Release 빌드는 Override를 거부한다. Asset과 설정 경로는 Content Root 기준 상대 경로만 허용한다.

거부 항목:

- `/` 또는 `\`로 시작하는 절대 경로
- 드라이브 절대/상대 경로
- UNC 경로
- 경로 Segment `..`
- 정규화 후 Content Root 밖으로 나가는 경로

최종 경로는 `Path.GetFullPath`로 정규화하고 Root prefix를 다시 검사한다. Registry 테스트는 다른 Working Directory로 이동한 상태에서도 같은 Root와 Asset이 로드됨을 확인한다.

## 4. Navigation 설정

파일:

```text
Content/Navigation/navigation-maps.json
```

현재 개발 설정:

```json
{
  "navigationMaps": [
    {
      "roomId": 0,
      "sceneId": 0,
      "navigationMapId": "room-0-nav-v1",
      "assetPath": "Navigation/room-0-nav-v1.navgrid",
      "formatVersion": 1,
      "contentHash": "e0871d500c776eedf3080ac0a8f98af61834efb0cbfd8a33c80da34b36474ff1",
      "enabled": true,
      "required": true
    }
  ]
}
```

`SceneId`는 현재 서버에 Scene 시스템이 없어 Registry와 Room의 정적 메타데이터로만 보존한다. RoomId와 NavigationMapId의 실제 연결은 이 파일에만 정의한다.

정책:

- enabled + required 항목의 경로/로드/검증 실패: 서버 시작 실패
- enabled + optional 항목의 파일 누락/로드 실패: 명시적 로그 후 제외
- 중복 MapId 또는 RoomId: 항상 설정 오류
- MapId/Version/Hash 불일치: required 여부와 관계없이 무결성 오류
- disabled 항목: Registry 대상에서 제외

## 5. NavigationRegistry

추가 타입:

- `NavigationMapCatalogConfig`
- `NavigationMapConfig`
- `NavigationContentPath`
- `NavigationRegistration`
- `NavigationRegistry`
- `NavigationRegistryLoadResult`
- `NavigationBoundsValidator`

Registry는 로컬 Dictionary를 완성한 뒤에만 성공 결과를 반환한다. 외부 mutation API가 없으며 서버 종료까지 동일 `NavGridAsset` 인스턴스를 유지한다.

조회:

```csharp
TryGetByMapId(...)
TryGetByRoomId(...)
TryGetRegistrationByRoomId(...)
GetRequiredForRoom(...)
```

파일 포맷은 다시 파싱하지 않고 Phase 1의 `NavGridAssetLoader.Load`만 사용한다.

## 6. GameRoom 연결

`GameRoom` 생성자는 `NavigationRegistration`을 필수로 받는다.

읽기 전용 정보:

```text
RoomId
SceneId
NavigationMapId
NavigationContentHash
NavGridAsset Navigation
```

Room 내부 파일 Load, Tick별 Registry 조회, 이동 패킷별 Hash 재계산은 없다. Room 생성 이후 Navigation 교체 API도 없다.

Phase 1 `NavGridAsset`에 서버용 읽기 전용 편의 API만 추가했다.

```text
IsValidWorldPosition
TryWorldToCell(float x, float y, float z, ...)
IsWalkable
GetCellType
TryGetCellWorldCenter
```

`Slow`는 `Blocked`가 아니므로 조회 API상 walkable로 반환하지만, 이동 비용이나 속도 정책에는 연결하지 않았다.

## 7. Bounds와 기존 Tilemap

테스트 골든 4×3 Asset은 Origin `(-2, 0, -1)`이므로 Room 0 운영 데이터로 사용하지 않는다.

Room 0 개발 Asset:

```text
MapId = room-0-nav-v1
Width = 145
Height = 145
CellSize = 1
Origin = (0, 0, 0)
AgentRadius = 0.5
Cell = 전부 Walkable
ContentHash = e0871d500c776eedf3080ac0a8f98af61834efb0cbfd8a33c80da34b36474ff1
```

기존 Tilemap과 비교:

```text
NavGrid XZ  = [0,145) × [0,145)
Tilemap XZ  = [0,145) × [0,145)
결과        = Match
```

V1에서는 완전 동일 Bounds를 요구한다. 불일치하면 자동 보정하지 않고 Room 생성과 서버 시작을 실패시킨다. Y는 평면 Grid Bounds 비교 대상이 아니다.

기존 Tilemap도 `Content/Legacy/TilemapData.txt`로 빌드 출력에 복사한다. `Tilemap.LoadFile`은 완전한 21,025 Cell을 읽었는지 반환하고, 실패하면 Room이 등록되지 않는다.

## 8. Content 배포

소스:

```text
Server/Content/Navigation/navigation-maps.json
Server/Content/Navigation/room-0-nav-v1.navgrid
Server/../Resources/TilemapData.txt
```

Debug 출력:

```text
Server/bin/Debug/netcoreapp3.1/
  Server.exe
  Content/
    Navigation/
      navigation-maps.json
      room-0-nav-v1.navgrid
    Legacy/
      TilemapData.txt
```

`Server.csproj`의 `CopyToOutputDirectory=PreserveNewest` 규칙을 사용한다. 런타임은 소스 트리를 참조하지 않는다.

Room 0 NavGrid:

- ContentHash: `e0871d500c776eedf3080ac0a8f98af61834efb0cbfd8a33c80da34b36474ff1`
- 파일 전체 SHA-256: `cd78039c27c7ba27cd1bda2b89c75c4d28c2ff01f6f3346b19ba3eb2db74beda`
- 소스/출력 파일 크기: 21,122 bytes
- 소스/출력 파일 전체 SHA-256 일치

## 9. 테스트 및 빌드 결과

서버 전체:

- Debug `Server.csproj`: 성공
- 기존 로그인/접속 초기화 코드: 컴파일 성공
- 기존 netcoreapp3.1 지원 종료, nullable 문맥, 미사용 필드 경고는 유지

NavGridTests:

- Phase 1 C# 테스트: 회귀 통과
- Phase 2 Runtime 테스트: 47 assertions 추가
- C# 전체: 157 assertions 통과
- C++/C# Phase 1 결과 JSON: 계속 byte-identical
- 결과 JSON SHA-256: `aadb9f2d02ce75d00281534c7a3c45e2e5fed05a313758f8ee62836d05aa1952`

Phase 2 테스트 범위:

- 상대 경로 정상 해석
- `/../`, `\..\`, 절대, 드라이브 상대, UNC 경로 거부
- 상대 Content Root Override 거부
- 정상 Registry와 MapId/RoomId 조회
- MapId/RoomId 중복 거부
- 파일 MapId, Hash, Version 불일치 거부
- required 파일 누락 실패
- optional 파일 누락 명시적 skip
- 잘못된 설정 거부
- Working Directory 변경 후 동일 Asset 로드
- Room 0 등록 및 메타데이터
- Room WorldToCell, Walkable, Blocked, 외부 좌표, Cell 중심
- Navigation 없는 Room 생성 거부
- Room 0 NavGrid/Tilemap Bounds 일치

실행 smoke test:

- Working Directory를 `D3D_Popol`로 설정하고 서버 실행
- 실행 파일 기준 Content Root 선택 확인
- Registry 로드, Room 0 Asset/Hash 로그 확인
- Legacy Tilemap 21,025 Cell 로드 확인
- Bounds Match 확인
- 이후 `Listening...` 확인
- 테스트가 시작한 서버 프로세스만 종료

## 10. 이동 및 프로토콜 회귀

이번 Phase에서 다음 파일과 의미는 변경하지 않았다.

- Protocol Buffer 원본 및 생성 코드
- `C_Move` 필드
- 목적지/Cell 검증 방식
- 서버 위치 갱신과 `S_Move` Broadcast

Phase 1의 세션 Player 귀속 보안 패치도 유지했다.

- `room.FindObject(movePacket.ObjectId)` 없음
- 패킷 objectId와 세션 Player ID 불일치 거부
- 실제 변경 대상은 `ClientSession.Player`

NavGrid는 현재 Room에서 읽기 전용으로 조회할 수 있을 뿐 `C_Move`에서 사용하지 않는다.

## 11. 알려진 제한

- 현재 접속 흐름은 여전히 Room 0을 코드에서 고정 조회한다.
- SceneId는 메타데이터일 뿐 별도 Scene 런타임이 없다.
- 모든 Room이 현재 동일한 legacy Tilemap 파일을 사용한다.
- Room 0 개발 NavGrid는 기존 Tilemap과 마찬가지로 전부 Walkable이다.
- optional 설정은 지원하지만 현재 Room 0은 required다.
- `Slow` 비용은 이동 처리에 적용하지 않는다.
- Registry hot reload와 Asset 교체는 없다.
- 서버 타깃 `netcoreapp3.1`은 지원 종료 상태다.

## 12. 다음 MoveRequest 단계의 연결 지점

다음 Phase에서 목적지 요청 프로토콜을 도입할 때 서버 Handler는 전역 Registry나 파일을 직접 찾지 않고 다음 흐름을 사용한다.

```text
ClientSession.GameRoom
  -> GameRoom.Navigation
  -> TryWorldToCell
  -> IsValidCell / GetCellType / IsWalkable
  -> GameRoom.NavigationMapId
  -> GameRoom.NavigationContentHash
```

이후 프로토콜의 MapId/Version/Hash 검증과 목적지 Cell 검증을 이 지점에 연결한다. 이번 Phase에서는 그 호출을 추가하지 않았다.
