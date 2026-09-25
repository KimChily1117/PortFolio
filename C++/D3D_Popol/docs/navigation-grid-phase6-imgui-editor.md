# Phase 6: ImGui Navigation Grid Editor V1

## 1. 결과 요약

기존 DirectX11 Client의 ImGui, GameObject/MonoBehaviour, Scene Picking을 그대로 사용해 Room 0의 `Walkable`, `Blocked`, `Slow` Cell을 편집하고 NavGrid V1으로 안전하게 저장하는 V1 Editor를 구현했다.

핵심 결과:

- Qt, Asset Database, 범용 Editor Framework를 추가하지 않았다.
- Phase 1 `NavGridAssetLoader`, 좌표 규칙, BCrypt SHA-256을 그대로 재사용한다.
- 편집 데이터와 로드 완료 불변 Asset을 분리했다.
- Navigation Mode에서 게임 우클릭 MoveRequest, Scene 오브젝트 클릭, 게임 UI Picking을 차단한다.
- Bresenham Drag 연결, Square/Circle Brush, Stroke 단위 Undo/Redo를 제공한다.
- NavGrid V1 Writer는 임시 파일 작성, 기존 Loader 재검증, 원자적 교체 순서로 저장한다.
- Client Source Asset과 Server Content는 byte-identical한 Room 0 Asset을 사용한다.
- 실제 Room 0에 작은 Blocked 벽/통로와 Slow 검증 영역을 배치했다.

현재 Room 0 Content Hash:

```text
0eb3670c39ac4e3fd2422b98da07e60f471b40e4a72cd1023e5465eff5b2a9f8
```

파일 전체 SHA-256은 Header의 ContentHash 규칙과 다른 값이며 다음과 같다.

```text
5709241fcd50b9068a00334f5e7d33c8e32541008e41821cb4042b58bfc1eb41
```

## 2. 기존 Editor/Inspector 분석

### GameObject / Component

- `GameObject`는 고정 `Component` 배열과 `MonoBehaviour` 목록을 소유한다.
- Component 생성은 코드에서 `make_shared` 후 `AddComponent`하는 방식이다.
- Reflection, RTTI 등록표, Inspector Add Component 메뉴가 없다.
- Scene 전체 직렬화가 없고 `GameObject`의 Transform JSON sidecar 저장만 존재한다.
- Component별 공용 `GUIRender` virtual dispatch가 없다.

따라서 V1에서는 Terrain GameObject에 `NavigationGridComponent`를 코드로 부착하고, `TestScene::GUIRender`가 Navigation Inspector/Tool을 호출한다. 존재하지 않는 범용 Add Component나 Scene Serializer를 새로 만들지 않았다.

### Inspector / Selection

- `ImGuiManager::Render`가 `Scene::GUIRender`를 호출한다.
- 기존 Inspector는 `_enableGUI`인 모든 GameObject Transform을 순회한다.
- 선택된 단일 GameObject를 보관하는 중앙 Selection Manager는 없다.
- Scene의 일반 좌클릭 Picking은 `TestScene::Update`, 게임 UI Picking은 `Scene::PickUI`에 있다.
- 기존 코드는 `ImGuiIO::WantCaptureMouse`를 검사하지 않았다.
- Viewport는 독립 ImGui Window가 아니라 Win32 전체 D3D Viewport이다.
- Transform Gizmo는 현재 존재하지 않는다.

### Picking

- `Scene::TryCreatePickingRay`가 Perspective Camera의 View/Projection과 전체 D3D Viewport 좌표로 World Ray를 생성한다.
- `Terrain::Pick`은 Terrain Local Ray로 변환한 뒤 local `y=0` Plane과 교차하고 `[0,sizeX) x [0,sizeZ)`를 검사한다.
- Navigation Tool은 같은 World Ray 생성 코드와 XZ/floor 기반 `WorldToCell`을 사용한다.
- NavGrid V1은 World XZ 축 정렬 데이터이므로 NavigationRoot 회전은 V1에서 지원하지 않는다. 표시 평면 Y만 부착 Terrain의 World Y를 사용하고 Nav Asset의 Origin은 서버 좌표 `(0,0,0)`을 유지한다.

### Rendering

- 별도 Debug Line Renderer가 없다.
- ImGui DX11 backend는 Dynamic VB/IB, alpha blend, 상태 백업/복구를 이미 제공한다.
- V1 Overlay는 Cell World Corner를 Main Camera로 화면 투영하고 `ImDrawList` 한 배치에 Quad와 Grid Line을 추가한다.
- Cell마다 GameObject, MeshRenderer 또는 엔진 Draw Call을 만들지 않는다.
- 기존 `ImGuiManager`의 매 프레임 `std::cout` 디버그 출력은 Editor 성능 측정을 왜곡하므로 제거했다.

### Asset

- 기존 Client Runtime Asset은 `Binaries/Client.exe` 기준 `../Resources/...` 상대경로를 사용한다.
- 파일 저장 대화상자는 없다.
- Navigation Component 설정은 `.navgrid.component.json` sidecar에 상대경로로 저장한다.
- 서버 Export 경로도 설정 sidecar의 상대경로이며 서버 절대 경로를 C++ 코드에 하드코딩하지 않는다.

## 3. 책임 분리

### NavigationGridComponent

`GameCoding2/NavigationGridComponent.*`

책임:

- MapId, Asset 상대경로, Server Export 상대경로
- 편집 가능한 Cell 데이터 소유
- Create, Load, Reload, Save, Export
- `WorldToCell`, `CellToWorldCenter`, `GetCellType`, `SetCellType`
- Dirty, 저장된 ContentHash, 표시 옵션
- Component 설정 sidecar 저장/로드

일반 `Update`에서 입력을 읽거나 페인팅하지 않는다.

### EditableNavGridData / NavGridAssetWriter

`EngineCore/NavGridAssetWriter.*`

- `NavGridAsset`은 기존 불변 로드 결과로 유지한다.
- `EditableNavGridData`가 Editor Cell 변경을 소유한다.
- Writer는 V1 규격과 canonical hash만 책임진다.
- Renderer나 Inspector가 Cell byte 배열을 직접 변경하지 않는다.

### NavigationEditorTool

`GameCoding2/NavigationEditorTool.*`

- 모드 진입/종료
- Hover/Paint 입력
- Brush와 Drag Stroke
- Undo/Redo
- Inspector와 Navigation Tool UI
- 저장/Reload/Export 요청
- Overlay 데이터 생성

### NavigationPaintAlgorithms

`GameCoding2/NavigationPaintAlgorithms.h`

- Bresenham Cell Line
- Square/Circle Brush Cell 열거
- Grid 경계 clipping

Tool과 자동 테스트가 같은 순수 함수를 사용한다.

## 4. Navigation Mode와 입력 충돌 방지

진입:

```text
Navigation Grid Component
 -> Enter Navigation Edit Mode
 -> 대상 Component 고정
 -> Scene World Input Blocked
 -> Grid Overlay / Hover / Paint 활성
```

종료:

- `ESC` 또는 `Exit`
- 진행 중 Stroke 종료
- Hover/이전 Drag Cell 초기화
- World Input 복원
- Dirty 데이터는 메모리에 유지

차단 범위:

- `PlayerController` 우클릭 MoveRequest
- `TestScene` 일반 좌클릭 Picking
- `Scene::PickUI`
- Inspector/Navigation Tool 위 입력은 `WantCaptureMouse`로 Paint에서 제외
- Viewport 밖 좌표는 Hover/Paint 무효

## 5. Brush, Drag, Undo/Redo

입력:

- 좌클릭 Down: Stroke 시작
- 좌클릭 Hold: 이전 Hover와 현재 Hover 사이 Bresenham rasterization
- 좌클릭 Up: 한 Stroke Command 확정

Brush:

- Square: `abs(dx) <= radius && abs(dz) <= radius`
- Circle: `dx*dx + dz*dz <= radius*radius`
- Radius: `0..20`
- Grid 밖 Cell은 제외

중복 방지:

- 동일 타입 Cell은 기록하지 않는다.
- 한 Stroke 안에서 Cell index별 최초 before 값 하나만 유지한다.
- 빠른 Drag도 인접 Cell 연결을 유지한다.

Undo/Redo:

- 한 Stroke가 한 Command이다.
- `Ctrl+Z`, `Ctrl+Y`와 UI 버튼을 지원한다.
- 새 Stroke 확정 시 Redo Stack을 제거한다.
- 최대 128 Stroke를 유지한다.

## 6. Overlay

색상:

- Walkable: 낮은 alpha 녹색, 기본 숨김
- Blocked: 붉은 반투명
- Slow: 노란 반투명
- Hover/Brush Preview: 파란 반투명

145x145 최대 표시량:

```text
전체 Walkable Fill: 21,025 Quad
Fill Vertex: 84,100
Fill Index: 126,150
Grid Border: 146 vertical + 146 horizontal line
```

현재 Room 0 기본 표시(Blocked 24 + Slow 50)는 74 Fill Quad이다. ImGui Background DrawList의 동일 texture/clip 배치이므로 Cell 수만큼 엔진 Draw Call이 생기지 않는다. Editor 창 Draw Call은 별도이다.

Z-fighting 방지를 위해 부착 Terrain World Y에 `+0.02` 표시 offset을 사용한다. 다만 V1 Overlay는 ImGui 화면 투영 방식이므로 D3D Terrain Depth Buffer에 의해 가려지지 않는다. 실제 Depth Occlusion이 필요한 경우 다음 Renderer Phase에서 전용 world-space dynamic mesh/pass로 교체해야 한다.

## 7. NavGrid V1 Writer

출력 순서와 Hash canonicalization은 `docs/navgrid-v1-format.md`를 변경하지 않았다.

Writer 검증:

- UTF-8 MapId와 128 byte 상한
- Width/Height/Cell count 상한
- finite positive CellSize
- finite Origin
- finite non-negative AgentRadius
- CellEncoding 1
- Cell 값 0/1/2

저장 순서:

```text
Editable data validation
Canonical bytes SHA-256
Sibling temporary file write + flush + close
기존 NavGridAssetLoader로 temporary file 재로드
MapId/metadata/hash/모든 Cell 비교
MoveFileEx(REPLACE_EXISTING | WRITE_THROUGH)
```

검증 또는 교체 실패 시 기존 Asset은 유지된다. 골든 4x3 Asset의 Writer 출력은 Phase 1 골든 파일과 byte-identical하다.

## 8. Inspector 및 사용법

Client를 기존처럼 `Binaries` Working Directory에서 실행한다.

`Navigation Grid Component` 창:

- Map ID / Asset Path / Server Export Path
- Width/Height/CellSize/Origin/AgentRadius/FormatVersion/Hash
- Create/Recreate
- Load/Reload/Save As
- Discard Edits & Reload 명시 버튼
- Export Server Content
- Enter/Exit Edit Mode
- 표시 옵션과 Dirty 상태

`Navigation Editor` 창:

- Paint Type
- Brush Shape/Radius
- Hover Cell/Type/Center
- Brush Cell 수
- 전체 Walkable/Blocked/Slow 수
- Overlay CPU build ms
- Undo/Redo/Save/Exit

잘못된 Load는 candidate Asset 검증이 끝나기 전 기존 정상 Grid에 적용되지 않는다.

## 9. Server Content Export와 Hash 절차

Source Asset:

```text
D3D_Popol/Resources/Navigation/room-0-nav-v1.navgrid
```

Component 설정:

```text
D3D_Popol/Resources/Navigation/room-0-nav-v1.navgrid.component.json
```

Server Content:

```text
D3D_Server/Server/Server/Server/Content/Navigation/room-0-nav-v1.navgrid
```

Editor의 `Export Server Content`는 설정된 상대경로에 같은 Writer로 저장한다. `navigation-maps.json`은 조용히 자동 변경하지 않으며 UI가 새 Hash를 표시하고 명시적 갱신을 요구한다.

현재 설정에는 다음 Hash를 명시적으로 반영했다.

```json
"contentHash": "0eb3670c39ac4e3fd2422b98da07e60f471b40e4a72cd1023e5465eff5b2a9f8"
```

서버 재빌드 후 Source Content가 `bin/<Configuration>/netcoreapp3.1/Content/Navigation`으로 복사되고 시작 시 Registry 검증을 통과한다. 서버 Hot Reload는 하지 않는다.

## 10. 실제 Room 0 검증 영역

Bounds, Origin, CellSize와 알려진 spawn Cell은 유지했다.

```text
Vertical Blocked wall:
  x = 70
  z = 60..84
  z = 72만 Walkable 통로
  Blocked 24 Cell

Slow region:
  x = 62..66
  z = 67..76
  Slow 50 Cell

Known spawn safety:
  (6,3) Walkable 확인
```

총 Cell:

```text
Walkable 20,951
Blocked      24
Slow         50
Total    21,025
```

서버 통합 테스트는 `(69,71) -> (71,71)` 경로가 Blocked `(70,71)`을 통과하지 않고 통로 `(70,72)`를 사용하는지 실제 배포 Asset으로 검증한다.

## 11. 변경 파일

### Client / Engine

- `EngineCore/NavGridAssetWriter.h`
- `EngineCore/NavGridAssetWriter.cpp`
- `EngineCore/Scene.h`
- `EngineCore/Scene.cpp`
- `EngineCore/InputManager.h`
- `EngineCore/ImGuiManager.cpp`
- `EngineCore/EngineCore.vcxproj`
- `GameCoding2/NavigationGridComponent.h`
- `GameCoding2/NavigationGridComponent.cpp`
- `GameCoding2/NavigationEditorTool.h`
- `GameCoding2/NavigationEditorTool.cpp`
- `GameCoding2/NavigationPaintAlgorithms.h`
- `GameCoding2/TestScene.h`
- `GameCoding2/TestScene.cpp`
- `GameCoding2/PlayerController.cpp`
- `GameCoding2/GameCoding2.vcxproj`
- `Resources/Navigation/room-0-nav-v1.navgrid`
- `Resources/Navigation/room-0-nav-v1.navgrid.component.json`

### Tests

- `Tests/NavGrid/Cpp/NavGridTests.cpp`
- `Tests/NavGrid/Cpp/NavGridCppTests.vcxproj`

### Server

- `Server/Server/Server/Content/Navigation/room-0-nav-v1.navgrid`
- `Server/Server/Server/Content/Navigation/navigation-maps.json`
- `Server/Server/NavGridTests/RuntimeRegistrationTests.cs`

Protocol Buffers와 이동 패킷은 변경하지 않았다.

## 12. 빌드 및 테스트

### C++

- EngineCore Debug x64: 성공
- Client Debug x64 Rebuild: 성공, `Binaries/Client.exe`
- C++ NavGrid: **279 assertions 성공**
- 골든 Writer output: byte-identical
- failed save existing file preservation: 성공
- 빠른 Drag/Square/Circle/경계 Brush: 성공

### C# / Server

- Server Debug: 성공
- Server Release: 성공
- DummyClient Debug: 성공
- DummyClient Release: 성공
- C# Phase 1~5 + Room 0 통합: **408 assertions 성공**
- Room 0 Bounds `[0,145) x [0,145)`: 성공
- Room 0 wall/gap/Slow/spawn Cell: 성공
- 실제 Room 0 A* wall gap route: 성공

### 교차 언어

```text
C++/C# result JSON byte-identical
Result SHA-256:
aadb9f2d02ce75d00281534c7a3c45e2e5fed05a313758f8ee62836d05aa1952
```

Client Source Asset, C++ 생성 Fixture, Server Content의 전체 파일 SHA-256도 모두 동일하다.

### Release 기존 문제

- EngineCore Release x64는 기존 설정에 C++17/20 LanguageStandard와 Release IncludeDirectories가 없어 `Types.h`, `<filesystem>`, DirectXTex include에서 실패한다. Phase 1의 `std::bit_cast`도 이미 C++20을 요구하므로 Phase 6 기능 고유 오류가 아니다.
- Client Release x64는 기존 FXC `X3501: main entrypoint not found` 단계에서 C++ Phase 6 컴파일 전에 실패한다.
- 여러 vcxproj가 같은 `Intermediate/Debug`를 공유하는 기존 MSB8028 문제 때문에 EngineCore를 먼저 빌드하고 Client를 `/m:1` Rebuild했다.

## 13. 성능

145x145 Room 0 Debug 테스트 참고값:

```text
Canonical SHA-256: 0.0777 ms
Atomic Save + Loader full verification: 2.1601 ms
Load + SHA-256 verification: 0.5127 ms
```

Overlay:

- Overlay OFF는 Cell loop와 DrawList 생성이 수행되지 않는다.
- 기본 Room 0 표시에서는 74 Fill Quad만 생성한다.
- Walkable 전체 표시 최악값은 84,100 Vertex / 126,150 Index이다.
- Tool Window가 매 프레임 실제 Overlay CPU build ms를 표시한다.
- 실제 GPU FPS와 Drag Painting frame time은 GUI/카메라가 필요한 수동 계측 항목으로 남겼다. 이 실행 환경에서는 headless하게 신뢰할 수 있는 FPS를 만들지 않았다.

## 14. 알려진 제한

- 범용 Add Component/Selection Manager/Scene Serializer가 없어 Terrain 부착과 Component sidecar 경로가 `TestScene`에 연결돼 있다.
- NavigationRoot 회전/비균일 Scale은 NavGrid V1 서버 좌표 명세와 호환되지 않아 지원하지 않는다.
- Overlay는 ImGui screen projection이라 Terrain Depth Occlusion이 없다.
- 파일 다이얼로그가 없어 상대경로 문자열을 사용한다.
- Undo Stack은 메모리 내 128 Stroke이며 Client 재시작 후 유지되지 않는다.
- Dirty 상태에서 Client 종료 확인 Modal은 없다. Inspector에는 Dirty 표시와 명시적 Discard Reload만 있다.
- 실시간 서버 Hot Reload와 `navigation-maps.json` 자동 수정은 없다.
- 다중 Client GUI를 띄운 실제 Snapshot 보간 육안 검증은 자동 테스트 범위 밖이다. Phase 3~5 이동/Path/Snapshot 테스트와 실제 Room 0 A* 통합은 통과했다.

## 15. 다음 연결 지점

### Path Smoothing

`PlayerMovementState.Path` 생성 직후, 서버 `NavGridPathfinder` 결과를 권위 Cell Path로 보존한 상태에서 별도 waypoint simplifier를 적용한다. Corner Cutting/Blocked 검증 후 결과만 MovementState에 등록해야 하며 원본 A* Path는 진단용으로 유지하는 편이 안전하다.

### AOI

Phase 5 `GameRoom.BroadcastMovementSnapshot`의 Room 전체 Broadcast를 AOI 대상 집합으로 교체한다. Navigation Editor/Asset 형식과는 독립적으로 진행할 수 있다.

### Editor V2

- world-space depth-tested dynamic overlay pass
- 공용 Scene Selection/Component Inspector dispatch
- 종료 Dirty confirmation
- NavigationObstacle Bake와 Manual Override layer

위 항목은 NavGrid V1 포맷을 변경하지 않고 확장할 수 있다.
