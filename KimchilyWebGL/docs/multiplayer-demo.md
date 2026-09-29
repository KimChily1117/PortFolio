# 칠리 아일랜드 — 고장 난 포털

같은 게시 링크로 들어온 사람들이 채팅으로 역할을 정하고 발판을 나누어 밟아 포털을 여는 협동 데모다. 한 명으로도 확인할 수 있고, 2~4명이 함께 시연하면 이동·말풍선·공동 게임 상태가 잘 드러난다. 방에는 최대 8명까지 들어오며 한 라운드의 필수 발판은 최대 4개다.

## 시연 순서

| 단계 | 참가자 행동 | 확인할 장면 |
|---|---|---|
| 입장 | 홈/QR에서 닉네임을 정하고 같은 월드·게시 버전·방 코드로 입장 | 서버 자동 연결, 원격 캐릭터와 이름표 |
| 인사 | Unity 채팅을 열고 “나는 별 발판!” 전송 | TMP 채팅과 해당 캐릭터 위 말풍선 |
| 시작 | 친구들이 도착한 뒤 Unity 게임 HUD에서 시작 | 현재 인원에 맞춘 1~4개 필수 발판 |
| 협동 | 각자 다른 필수 발판 위에서 3초 유지 | 같은 점유 상태와 카운트다운 |
| 방해와 복구 | 한 사람이 내려갔다가 다시 올라감 | 유지 시간이 초기화되고 다시 시작 |
| 성공 | 모두 자리를 유지 | 모든 화면의 포털 개방과 TS 연출 |
| 재도전 | 다시 준비한 뒤 시작 | 현재 인원으로 새 라운드 |

성공 상태는 다시 준비하거나 방이 비기 전까지 유지한다. 늦게 온 사람도 현재 라운드와 성공 상태를 받는다. 게임 도중 인원이 줄어 필수 발판을 채울 수 없다면 다시 준비하고 시작한다. 각 화면이 다른 게시 버전이나 방 코드를 사용하면 같은 게임에 모이지 않는다.

## 직접 제작한 3D 리소스

전용 씬은 [ChiliIsland.unity](../KimchilyCreator/Assets/Demos/ChiliIsland/Scenes/ChiliIsland.unity)다. 기존 `Assets/World/MyWorld.unity`와 분리했다. [생성기](../KimchilyCreator/Assets/Editor/ChiliIslandArt.cs)는 Unity 메시를 직접 만들고 재질·프리팹·씬과 OBJ/MTL을 저장한다.

- 떠 있는 정원 섬, 장식 포털, 별·달·해·잎 발판 4종.
- 민트 나무, 꽃빛 나무, 관목, 데이지, 조약돌, 정원등, 새싹 마스코트, 안내판.
- 합계 **14개 원본 프리팹**. `Assets/Demos/ChiliIsland/Prefabs`, `Meshes`, `Materials`, `Models`에서 편집·재사용한다.
- Unity 메뉴 **Kimchily → Demos → Generate Chili Island**는 생성한 데모를 다시 만든다. 수작업으로 수정한 데모 씬·재질·메시를 유지하려면 재생성 전 별도 보관한다.
- 플레이어 모델은 기존 UnityChan 자산을 재사용하며, 위의 직접 제작한 환경·소품 모델과 구분한다.

## 제작자가 바꿔 보여줄 내용

[PortalGarden.ts](../KimchilyCreator/Assets/Demos/ChiliIsland/Scripts/PortalGarden.ts)는 발판 점유에 따른 불빛, 포털 개방, 성공 후 장식 애니메이션을 제어한다. Inspector의 `pulseSpeed`와 `celebrationScale`을 바꾸거나 TS 연출과 소품·재질을 수정하고, 전용 씬을 저장한 뒤 **Kimchily → Publish World**에서 Web 대상으로 게시한다. 새 QR/링크로 다시 입장해 달라진 연출을 확인한다. 빌드·게시 시간은 플레이 시연과 별도로 잡는다.

현재 **발판 좌표·필수 인원 산정·3초 유지 규칙은 C# 서버에 등록된 고정 프리셋**이다. TS의 숫자만 바꾸어 서버 승리 조건을 변경할 수는 없다. “동시에 밟기”를 “정해진 순서로 밟기”로 교체하는 것은 다음 서버 프리셋 확장 작업이다.

## SDK와 서버 역할

| 구성 | 버전 / 책임 |
|---|---|
| Creator 콘텐츠 SDK | 0.1.0, 씬·메시·TS 자산의 빌드와 게시 계약 |
| TypeScript SDK | 0.2.0, 제한된 Unity API와 `Kimchily.Network`의 `Room` API |
| Networking SDK | 0.3.0, 입장·채팅·캐릭터·게임 HUD·서버 상태 전달 |
| C# 서버 | .NET 10, `chili-portal-v1` 점유·시간·라운드·성공 판정 |

```ts
import { Room } from "Kimchily.Network";

// Start에서 opt-in. 연결 중이면 요청을 기억한다. 라운드는 자동으로 시작하지 않는다.
Room.enableGame("chili-portal-v1");

// Update에서 읽는다. 이 호출이 소켓이나 서버 요청을 만들지는 않는다.
const room = Room.getState();
const game = room.game; // 상태가 도착하기 전에는 null

// 필요할 때만 호출한다. 기본 데모는 Unity HUD 버튼이 요청을 보낸다.
Room.startRound();
Room.replay();
```

`room`은 `{connected, selfId, players, game}`인 동결된 JavaScript 복사본이다. `game`에는 `phase`, `round`, `requiredPlayers`, `holdSeconds`, `remainingMs`, `pads`, `version`이 있다. 발판의 `active`는 이번 라운드에 필요한지, `playerId`는 점유자를 나타낸다. 요청의 boolean 결과는 전송 요청 접수 여부이며 서버 성공을 뜻하지 않는다.

서버는 검증된 클라이언트 위치·접지 상태로 점유를 계산하며, 이동 자체의 지형 물리는 클라이언트가 수행한다. 서버 물리 엔진이나 임의 TS 서버 코드 실행 기능은 아니다. 전체 API와 경계는 [TypeScript 패키지](../KimchilySDK/Packages/com.kimchily.typescript/README.md), 정확한 좌표·통신 필드는 [서버 규격](../KimchilyServer/docs/protocol.md#칠리-아일랜드-협동-포털)을 참고한다.

새 Room API를 사용하는 콘텐츠를 처음 실행하려면 Networking 0.3.0·TypeScript 0.2.0을 포함한 공통 WebGL 실행기가 필요하다. 그 실행기를 준비한 뒤 지원 API 내 모델·TS 연출 변경은 새 콘텐츠 버전으로 게시한다.

## 현재 검증과 작업 위치

2026-09-29 통합 Unity PlayMode **115개**, C# 서버 **40개**, TypeScript 컴파일러 **20개**, facade **20개**, 실제 Jint VM **32개** 검사가 통과했다. strict 타입 검사와 LanguageService 자동완성도 확인했다. 과거 iPhone 실행 확인을 이번 협동 게임의 실기기 검증으로 합산하지 않는다.

제작·수정·검증은 `E:\task\KimchilyWebGL`에서 수행한다. Git 관리본은 `E:\GItHub\PortFolio\KimchilyWebGL`이며, 작업을 끝낸 뒤 의도한 변경만 반영·병합·커밋한다. 게시·실행기 절차는 [WebGL 가이드](webgl-guide.md), 채팅 서버와 LAN 설정은 [서버 README](../KimchilyServer/README.md)를 따른다.
