# Navigation Grid Phase 1 구현 결과

기준 문서: [`navigation-grid-phase0-analysis.md`](./navigation-grid-phase0-analysis.md)  
파일 명세: [`navgrid-v1-format.md`](./navgrid-v1-format.md)

## 결론

Phase 1 범위인 공용 NavGrid V1 파일 명세, 골든 에셋, C++/C# 독립 로더, SHA-256, 공용 좌표 규칙, 정상/오류 테스트와 교차 언어 결과 비교를 구현했다. 이동 패킷과 이동 처리의 의미는 변경하지 않았고, Phase 0에서 확인한 `C_Move.objectId` 취약점만 별도 최소 보안 패치로 차단했다.

## 구현 전 구조 재확인

| 항목 | 확인 결과 |
|---|---|
| C++ Tilemap 바이너리 Load | `EngineCore/Tilemap.cpp:15` |
| C++ Tilemap 텍스트 Save | `EngineCore/Tilemap.cpp:37` |
| C++ Asset 모듈 | `ResourceManager`, `ResourceBase`, `BinaryReader`; 기존 `void Load` 계약은 구조화된 실패가 필요한 NavGrid에 부적합 |
| C++ 벡터 타입 | `EngineCore/Types.h`의 `Vec3 = DirectX::SimpleMath::Vector3` |
| C# 정적 데이터 Load | `D3D_Server/Server/Server/Server/Game/Room/GameRoom.cs:465`에서 작업 디렉터리 상대 Tilemap 경로 사용 |
| C# 테스트 | 기존 테스트 프로젝트와 테스트 프레임워크 없음 |
| C++ 테스트 | 기존 테스트 프로젝트 없음 |
| SHA-256 | 기존 래퍼 없음; C++ Windows CNG BCrypt, C# `System.Security.Cryptography.SHA256` 사용 |
| 현재 콘텐츠 배포 | 클라이언트 `Resources`, 서버 `Server/Server/Resources`에 수동 복제 |

기존 Tilemap Load와 Save는 서로 다른 형식이므로 NavGrid V1에 재사용하지 않았다. 테스트에는 외부 프레임워크나 새 패키지를 추가하지 않고 각각 작은 Console runner를 사용했다.

## C_Move objectId 최소 보안 패치

수정 파일:

- `E:/task/C++/D3D_Server/Server/Server/Server/Packet/PacketHandler.cs`

수정 내용:

1. 수신 시 `ClientSession.Player`를 이동 대상으로 고정한다.
2. 패킷의 `objectId`는 세션 Player ID와 일치하는지 검증하는 용도로만 유지한다.
3. 불일치하면 Room Job을 만들지 않고 거부 로그를 남긴다.
4. 비동기 Room Job 실행 시 세션의 Room과 Player 귀속이 아직 같은지 다시 확인한다.
5. `room.FindObject(movePacket.ObjectId)` 호출을 제거했다.

프로토콜, 좌표 보정, Tile 검사, Broadcast 방식은 변경하지 않았다. 따라서 위험성은 낮고, 정상 클라이언트 패킷의 동작은 유지된다. 별도 패킷 기반 통합 테스트 기반이 없어 정적 회귀 검사와 서버 전체 빌드로 검증했다.

검증:

- 패킷 `objectId` 기반 Room 조회 없음
- 세션 Player ID 불일치 거부 조건 존재
- 실제 변경 대상이 세션 Player임
- D3D_Server Debug 빌드 성공

## NavGrid V1 구현

### C++ 클라이언트

- `EngineCore/NavGridAsset.h`
- `EngineCore/NavGridAsset.cpp`
- `EngineCore/EngineCore.vcxproj`
- `EngineCore/EngineCore.vcxproj.filters`

주요 타입:

- `NavGridAsset`: 검증 후 생성되는 데이터와 좌표 조회
- `NavGridAssetLoader`: 명시적 Little-Endian 파싱과 구조화된 오류
- `NavGridHash`: Windows CNG SHA-256
- `NavGridCoordinate`, `NavCellType`

기존 `ResourceManager::Load`는 로드 실패 상태를 표현하지 못하고 실패한 객체도 캐시에 넣을 수 있어 이번 단계에는 연결하지 않았다.

### C# 서버

- `E:/task/C++/D3D_Server/Server/Server/Server/Game/Navigation/NavGridAsset.cs`
- `E:/task/C++/D3D_Server/Server/Server/Server/Game/Navigation/NavGridAssetLoader.cs`

Cell 배열과 해시 배열은 내부 복사본으로 보관하고 외부에는 복제본만 제공한다. `BinaryReader.ReadString`을 사용하지 않고 길이가 명시된 UTF-8 bytes를 직접 읽는다. 서버 런타임 맵 등록은 Phase 1 범위가 아니므로 연결하지 않았다.

## 골든 에셋과 테스트

- 골든 파일: `Tests/NavGrid/Assets/golden-grid-v1.navgrid`
- 생성기: `Tests/NavGrid/generate-golden.ps1`
- 공용 좌표 벡터: `Tests/NavGrid/coordinate-vectors.csv`
- C++ runner: `Tests/NavGrid/Cpp/NavGridTests.cpp`
- C++ test project: `Tests/NavGrid/Cpp/NavGridCppTests.vcxproj`
- C# runner: `E:/task/C++/D3D_Server/Server/Server/NavGridTests/Program.cs`
- C# test project: `E:/task/C++/D3D_Server/Server/Server/NavGridTests/NavGridTests.csproj`
- 교차 비교: `Tests/NavGrid/compare-results.ps1`

골든 ContentHash:

```text
d74739cc31aa532815ce2d3551d58962fc430e9e871df4fd374f8f2f26480a77
```

검사 범위:

- 정상 메타데이터, 모든 Cell, ContentHash
- 최소 모서리, 중심, 경계 직전/정확한 경계, 최대 경계, 음수 Origin
- NaN, Positive/Negative Infinity
- Magic, Version, HeaderSize, UTF-8, MapId 상한
- 0 차원, 차원 상한, 셀 수 상한과 곱셈 안전성
- CellSize 0/음수/NaN/Infinity
- Origin Infinity, AgentRadius 음수
- CellEncoding, 잘린 Header/CellData
- CellDataLength 과대/불일치
- 알 수 없는 Cell, Hash 변조, trailing data

최종 결과:

- C++: 162 assertions 통과
- C#: 110 assertions 통과
- 두 결과 JSON: 바이트 동일
- 결과 JSON SHA-256: `aadb9f2d02ce75d00281534c7a3c45e2e5fed05a313758f8ee62836d05aa1952`

assertion 수 차이는 C++ runner가 fixture 파일 I/O의 각 단계도 assertion으로 계산하기 때문이며, 공통 NavGrid 검증 시나리오는 대칭이다.

## 빌드 결과

### C++

- `EngineCore` Debug x64: 성공
- `GameCoding2`/Client Debug x64 링크: 성공
- NavGrid C++ test runner: 성공

기존 경고가 남아 있다.

- 여러 프로젝트가 `Intermediate/Debug`를 공유한다는 MSB8028
- 기존 C++ 축소 변환/enum 경고
- 기존 FXC deprecated 및 shader 초기화 경고
- 기존 DirectXTex PDB LNK4099

NavGrid 신규 소스의 컴파일 오류는 없다.

### C#

- D3D_Server Debug: 성공
- NavGrid C# test runner: 성공

기존/환경 경고가 남아 있다.

- `netcoreapp3.1` 지원 종료 경고 NETSDK1138
- 기존 nullable 문맥 및 미사용 필드 경고

## 배포 권장

단일 원본 Nav Asset을 빌드 입력으로 관리하고 클라이언트와 서버 산출물에 byte-for-byte 복사한다.

```text
Shared Navigation Source
 ├─ Client/Resources/Navigation/<MapId>.navgrid
 └─ ServerOutput/Resources/Navigation/<MapId>.navgrid
```

서버는 향후 `AppContext.BaseDirectory` 기준으로 콘텐츠 루트를 고정하는 것이 안전하다. 현재 `../../../../Resources` 방식은 실행 작업 디렉터리에 의존한다.

## 이번 Phase에서 구현하지 않은 항목

- ImGui Navigation Editor와 `NavigationGridComponent`
- A*와 경로 시각화
- 이동 패킷 추가/변경
- 서버 이동 목적지 검증과 런타임 Map 등록
- 서버 이동 시뮬레이션
- Navigation Bake와 동적 장애물
- 압축과 네트워크 Asset 전송

## 다음 Phase 권장

원래 계획의 Phase 2보다 서버 권위 기반을 먼저 강화하려면, 다음 작업은 “에디터”와 “서버 검증”을 분리해 결정해야 한다. 안전 우선 순서는 다음과 같다.

1. 공유 원본 Nav Asset의 실제 빌드 복사 단계와 서버 콘텐츠 루트 확정
2. `NavigationMapId`와 기존 Room/Scene 연결 테이블 설계
3. Phase 2 클라이언트 `NavigationGridComponent` + ImGui 편집 V1
4. Phase 3 서버 런타임 등록, 목적지 Bounds/Blocked/Version/Hash 검증

Phase 1 로더는 어느 쪽 런타임에도 자동 등록되지 않으므로 기존 이동 동작에는 영향을 주지 않는다.
