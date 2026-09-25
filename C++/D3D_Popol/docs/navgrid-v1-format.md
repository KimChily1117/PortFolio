# NavGrid V1 Binary Format

이 문서는 C++ 클라이언트와 C# 권위 서버가 공유하는 정적 평면 Navigation Grid 에셋의 V1 명세다. 기존 `Tilemap` 바이너리/텍스트 형식과 호환되지 않으며, C++ 구조체 메모리 덤프를 사용하지 않는다.

## 기본 규칙

- 파일의 모든 정수와 IEEE-754 `float32`는 Little-Endian이다.
- 문자열은 길이 접두사가 있는 엄격한 UTF-8이며 BOM과 NUL 종료 문자를 저장하지 않는다.
- 이동 평면은 XZ, 높이축은 Y다.
- Grid 좌표는 `(x, z)`이고 배열 인덱스는 `index = z * width + x`다.
- V1 로더는 CellData 뒤의 추가 바이트를 거부한다.
- V1 상한은 Map ID 128바이트, 각 차원 4096, 총 4,194,304셀이다.
- 부분적으로 읽거나 검증에 실패한 에셋은 사용 가능한 결과로 노출하지 않는다.

## 파일 레이아웃

`L = NavigationMapIdLength`, `H = HeaderSize = 84 + L`, `N = CellDataLength`이다.

| Offset | 크기 | 타입 | 필드 | V1 규칙 |
|---:|---:|---|---|---|
| 0 | 4 | byte[4] | Magic | ASCII `NVG1` (`4E 56 47 31`) |
| 4 | 4 | uint32 | FormatVersion | `1` |
| 8 | 4 | uint32 | HeaderSize | 정확히 `84 + L` |
| 12 | 4 | uint32 | NavigationMapIdLength | UTF-8 바이트 수, 최대 128 |
| 16 | L | byte[L] | NavigationMapId | 엄격한 UTF-8 |
| 16+L | 4 | uint32 | Width | `1..4096` |
| 20+L | 4 | uint32 | Height | `1..4096` |
| 24+L | 4 | float32 | CellSize | 유한하고 `> 0` |
| 28+L | 4 | float32 | OriginX | 유한값 |
| 32+L | 4 | float32 | OriginY | 유한값 |
| 36+L | 4 | float32 | OriginZ | 유한값 |
| 40+L | 4 | float32 | DefaultAgentRadius | 유한하고 `>= 0` |
| 44+L | 4 | uint32 | CellEncoding | V1은 `1`만 지원 |
| 48+L | 4 | uint32 | CellDataLength | Encoding 1에서는 `Width * Height` |
| 52+L | 32 | byte[32] | ContentHash | 아래 정규화 바이트의 SHA-256 |
| H | N | byte[N] | CellData | row-major, Cell당 1바이트 |

곱셈은 할당 전에 최소 64비트 부호 없는 정수로 수행한다. `Width * Height`가 4,194,304를 넘으면 거부한다. 실제 파일 길이는 정확히 `H + N`이어야 하므로 잘린 데이터와 trailing data를 모두 거부한다.

## Cell Encoding 1

언어별 enum 메모리 표현을 저장하지 않고 아래 `uint8` 값을 명시적으로 변환한다.

| 값 | 의미 |
|---:|---|
| 0 | Walkable |
| 1 | Blocked |
| 2 | Slow |

다른 값은 V1에서 오류다.

## SHA-256 정규화

ContentHash 자기 참조를 막기 위해 파일 전체가 아니라 다음 필드를 순서대로 다시 직렬화한다.

1. FormatVersion `uint32`
2. NavigationMapIdLength `uint32`
3. NavigationMapId UTF-8 bytes
4. Width `uint32`
5. Height `uint32`
6. CellSize `float32`
7. OriginX, OriginY, OriginZ `float32`
8. DefaultAgentRadius `float32`
9. CellEncoding `uint32`
10. CellDataLength `uint32`
11. CellData bytes

모든 숫자는 파일과 동일한 Little-Endian 표현이다. Magic, HeaderSize, ContentHash 필드 자체, 파일 경로와 수정 시간은 해시 대상에서 제외한다. 비교는 원본 32바이트로 수행하며 Hex는 로그와 테스트 전용 소문자 표현이다.

## 좌표 규칙

Origin은 Cell `(0, 0)`의 최소 XZ 모서리다. 유효 범위는 `0 <= x < width`, `0 <= z < height`이고 맵 최대 경계는 exclusive다.

```text
cellX = floor((worldX - originX) / cellSize)
cellZ = floor((worldZ - originZ) / cellSize)

worldX = originX + (cellX + 0.5) * cellSize
worldY = originY
worldZ = originZ + (cellZ + 0.5) * cellSize
```

WorldToCell 입력의 X/Y/Z 중 하나라도 NaN 또는 Infinity이면 실패한다. 계산은 양쪽 모두 `double` 중간값과 `floor`를 사용하고, 최종 유효 범위를 검사한다. Grid GameObject 회전/스케일은 V1 에셋 자체에 포함되지 않으며 향후 에디터가 월드 좌표를 Nav-local 좌표로 변환한 뒤 이 규칙을 사용해야 한다.

예를 들어 골든 Grid의 Origin `(-2, 0, -1)`, CellSize `1`에서:

- `(-2, 0, -1)` → `(0, 0)`
- `(-1.5, 0, -0.5)` → `(0, 0)`
- `(-1, 0, -0.5)` → `(1, 0)`
- `(2, 0, 1.5)` → 실패: 최대 X 경계

## 골든 Asset

경로: `Tests/NavGrid/Assets/golden-grid-v1.navgrid`

```text
NavigationMapId = golden-grid-v1
Width = 4
Height = 3
CellSize = 1.0
Origin = (-2.0, 0.0, -1.0)
DefaultAgentRadius = 0.5
CellEncoding = 1

z=0: 0 0 1 0
z=1: 0 1 1 0
z=2: 0 0 0 0
```

- Map ID 길이 `L = 14`
- HeaderSize `H = 98 (0x62)`
- 전체 파일 크기 `110`바이트
- 기대 ContentHash: `d74739cc31aa532815ce2d3551d58962fc430e9e871df4fd374f8f2f26480a77`

Hex dump:

```text
00000000  4E 56 47 31 01 00 00 00 62 00 00 00 0E 00 00 00
00000010  67 6F 6C 64 65 6E 2D 67 72 69 64 2D 76 31 04 00
00000020  00 00 03 00 00 00 00 00 80 3F 00 00 00 C0 00 00
00000030  00 00 00 00 80 BF 00 00 00 3F 01 00 00 00 0C 00
00000040  00 00 D7 47 39 CC 31 AA 53 28 15 CE 2D 35 51 D5
00000050  89 62 FC 43 0E 9E 87 1D F4 FD 37 4F 8F 2F 26 48
00000060  0A 77 00 00 01 00 00 01 01 00 00 00 00 00
```

`Tests/NavGrid/generate-golden.ps1`은 테스트 fixture 생성 책임만 가지며 프로덕션 로더에는 저장 기능이 없다.

## 배포 권장

원본 Nav Asset의 단일 기준 위치를 별도의 공유 콘텐츠 디렉터리 또는 빌드 입력으로 정하고, 빌드/패키징 단계에서 다음 두 출력에 동일한 바이트를 복사한다.

- 클라이언트: `Resources/Navigation/<NavigationMapId>.navgrid`
- 서버: 서버 실행 출력의 `Resources/Navigation/<NavigationMapId>.navgrid`

현재 서버의 `../../../../Resources` 작업 디렉터리 상대 경로는 실행 위치에 따라 깨질 수 있으므로 Phase 3의 맵 등록 시 `AppContext.BaseDirectory` 기준 콘텐츠 루트를 도입하는 것이 안전하다. V1에서는 런타임 맵 등록이나 네트워크 전송을 구현하지 않는다.

## 버전 변경 규칙

- 의미, 필드 순서, 좌표 규칙 또는 해시 정규화가 바뀌면 FormatVersion을 올린다.
- V1 로더는 알 수 없는 Version을 즉시 거부하며 추측해서 읽지 않는다.
- 새 버전은 별도 파서와 명시적 migration 도구를 제공한다.
- HeaderSize가 다르더라도 V1 로더는 확장 필드를 건너뛰지 않는다. V1 레이아웃과 정확히 일치해야 한다.
