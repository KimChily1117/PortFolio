# Kimchily room protocol 1

WebSocket `/ws`는 UTF-8 JSON 명령을 받습니다. 분할 프레임은 EndOfMessage까지 조립하며 최대 합계 4,096바이트입니다. 명령은 protocolVersion:1과 type을 포함합니다. 미등록 필드는 거부합니다. 발신자 ID·서버 시간은 서버가 정합니다.

## 입장·채팅·캐릭터

```json
{"protocolVersion":1,"type":"join","worldId":"chili-island","revisionId":"published-revision","roomId":"playground","name":"칠리"}
{"protocolVersion":1,"type":"chat","text":"안녕하세요!"}
{"protocolVersion":1,"type":"state","state":{"sequence":1,"x":0,"y":1,"z":0,"yaw":90,"speed":3,"grounded":true,"verticalVelocity":0}}
{"protocolVersion":1,"type":"leave"}
{"protocolVersion":1,"type":"ping"}
```

- worldId/revisionId/roomId는 각각 ASCII 영문·숫자·밑줄·하이픈 1~80자입니다. 하나라도 다르면 다른 방입니다.
- 닉네임은 앞뒤 공백 제거 후 1~24 UTF-16 코드 단위, 채팅은 1~300입니다. 제어 문자를 거부하고 HTML도 텍스트로 표시합니다.
- Unity는 초당 10회 pose를 보냅니다. 서버는 최소 70ms 간격, 증가하는 sequence만 받습니다.
- 좌표 ±10,000, yaw [0,360), speed [0,25], verticalVelocity ±100 범위 유한수만 허용합니다.
- 최대 2초의 경과 시간을 적용한 수평 25m/s + 1.5m, 수직 100m/s + 2m 이동량을 검사합니다. 최초 스폰보다 20m 아래로 낙하한 뒤 스폰 주변 복귀는 리스폰 예외입니다.
- 위치는 클라이언트가 계산합니다. 서버 지형 충돌·계정 인증을 의미하지 않습니다.

## 스크립트 게임

최초 watch는 방을 운영자가 승인한 정확한 스크립트 버전에 바인딩합니다. 일반 채팅 방에는 게임이 자동 생성되지 않습니다. 바인딩 후 다른 ID·해시로 교체할 수 없습니다.

```json
{
  "protocolVersion": 1,
  "type": "game",
  "scriptId": "chili-portal-ts-v1",
  "scriptHash": "5acf8acbf0153a0b5f7672984c27498826c31c527e91131cdb6f61ce9fb04290",
  "action": "watch"
}
```

예제 해시는 문서 작성 시점 값입니다. 실제로는 월드와 함께 생성된 PortalRuleIdentity.ts의 SCRIPT_HASH를 사용합니다. games/scriptId/scriptHash.json의 파일명·manifest·실제 JS SHA-256과 worldId 승인이 일치해야 합니다.

| 필드 | 제한/의미 |
| --- | --- |
| scriptId | ASCII 영문·숫자·밑줄·하이픈 1~80자 |
| scriptHash | 소문자 16진수 64자 |
| action | ASCII 영문·숫자·밑줄·하이픈 1~80자. watch는 호스트 구독 동작 |
| payloadJson | 선택적 JSON 문자열. UTF-8 최대 1,024바이트. 객체·배열·기본값 가능 |

action을 start/reset으로 바꾸면 현재 포털 TS가 시작·재도전을 실행합니다. 범용 C# 호스트는 이 이름이나 승리 조건을 모릅니다. 다른 게임은 add와 payloadJson:'{"amount":7}'처럼 자기 명령을 정의할 수 있습니다. 전체 명령은 JSON escape까지 포함해 4,096바이트 이내여야 합니다. watch 이외의 명령은 참가자당 5초에 최대 4회입니다.

임의 JS, 파일 경로, 발신자 ID, 시간, 결과 상태를 직접 보내는 필드는 없습니다. 포털 TS는 payload로 규칙을 덮어쓰지 않습니다.

## 서버 TS 계약

단일 CommonJS 모듈은 create()와 reduce(previousState,input)를 export합니다. create는 JSON 객체를 반환하고 reduce는 변경된 JSON 객체 또는 변경 없음인 null을 반환합니다.

```ts
type Input = {
    kind: "watch" | "command" | "tick";
    action?: string;
    payload?: unknown;
    nowMs: number; // 서버 단조 증가 시각. UTC timestamp 아님
    selfId?: string; // 서버가 정한 요청자 연결 ID
    players: Array<{
        playerId: string;
        name: string;
        state?: PlayerPose;
        receivedAtMs: number;
    }>;
};
```

참가자는 playerId로 정렬합니다. 아직 pose가 없으면 state 필드를 생략할 수 있습니다. C# 객체·소켓·Unity 객체는 전달하지 않고 이전 상태와 입력은 JSON으로 복사합니다. tick에는 action/selfId가 없습니다. JS VM은 Node 환경이 아니며 require·filesystem·network API를 제공하지 않습니다.

## 서버 이벤트

| type | 주요 필드 |
| --- | --- |
| hello | 연결 직후 selfId. 아직 방 입장 전 |
| joined | selfId, room, players, history, 선택적 game |
| playerJoined | player:{playerId,name}, 기존 참가자에게 전달 |
| playerLeft | player:{playerId,name,state?} |
| chat | chat:{id,playerId,name,text,sentAtUtc}, 보낸 사람 포함 방 전체 |
| state | player:{playerId,name,state}, 같은 방 다른 참가자 |
| game | 범용 게임 상태 envelope, 보낸 사람 포함 방 전체 |
| left | 자신의 퇴장 확인. 같은 소켓으로 다시 입장 가능 |
| pong | ping 응답 |
| error | code, 표시용 message |

모든 이벤트는 protocolVersion:1을 포함합니다. room은 {worldId,revisionId,roomId}, joined.players는 자신을 포함합니다. 채팅 sentAtUtc는 UTC ISO 8601입니다.

아래는 임의 counter 게임의 envelope 예시입니다. 실제 실행에는 예제 해시 대신 승인된 번들이 있어야 합니다.

```json
{
  "protocolVersion": 1,
  "type": "game",
  "game": {
    "scriptId": "counter",
    "scriptHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "version": 2,
    "stateJson": "{\"count\":7}"
  }
}
```

stateJson은 JSON **문자열**입니다. Unity JsonUtility는 모르는 게임 객체를 해석하지 않고 문자열을 보관하며, TS bridge가 파싱해 읽기 전용 스냅샷을 제공합니다. 게임마다 C# DTO를 추가할 필요가 없습니다.

version은 C#이 상태 JSON 변경 때만 증가시킵니다. 스크립트가 version을 확정하지 않습니다. 변경 없는 watch는 요청자에게 같은 스냅샷을 반환합니다. 늦은 입장의 joined.game도 동일 envelope입니다. 클라이언트는 ID·해시·증가 버전을 확인하고 재입장 시 이전 버전 캐시를 초기화합니다.

상태는 UTF-8 16KiB, 객체 root, 깊이 12, 값 1,024개 이내여야 합니다. 오류는 마지막 정상 상태를 덮어쓰지 않습니다. 마지막 퇴장 때 방과 VM을 제거합니다.

## Chili Island 콘텐츠 상태

이것은 공통 네트워크 계약이 아닌 PortalRules.ts의 계약입니다.

```ts
interface PortalState {
    phase: "waiting" | "playing" | "holding" | "complete";
    round: number;
    requiredPlayers: number;
    holdSeconds: number;
    remainingMs: number;
    _holdingSince: number | null;
    pads: Array<{
        id: string; x: number; y: number; z: number; radius: number;
        active: boolean; playerId: string | null;
    }>;
}
```

| 순서/id | 중심 | 반경 |
| --- | --- | --- |
| 1/star | (-3,0,2) | 1.1m |
| 2/moon | (3,0,2) | 1.1m |
| 3/sun | (-3,0,6) | 1.1m |
| 4/leaf | (3,0,6) | 1.1m |

active는 필요한 발판이라는 뜻이고, 점유는 active && playerId!==null입니다. 서버 TS는 XZ 반경·높이 ±1.5m·grounded·수신 후 1,200ms 이내를 검사합니다. 한 사람은 한 발판만 차지하고 겹친 후보자는 playerId 순서로 고릅니다.

start는 방 인원 1~4명을 고정하며 대기 중에는 현재 인원을 표시합니다. 중간 입퇴장으로 목표 인원이 바뀌지 않습니다. 모든 발판을 config.holdSeconds만큼 연속 점유하면 complete가 됩니다. 비거나 pose가 만료되면 시간을 초기화합니다. 점유자가 교체되어도 전부 채워진 상태가 연속 유지되면 진행은 유지합니다.

서버 nowMs로 계산하고 남은 시간을 100ms 단위로 올림합니다. 100ms tick으로 패킷이 없어도 검사합니다. complete는 reset 또는 빈 방 삭제까지 유지합니다. 웹 로비의 같은 방 참가자도 인원에 포함되므로 협동 시연에서는 Unity 플레이어로 참여해야 발판을 채울 수 있습니다.

## 제한과 오류

- 방 8명·동시 방 32개·소켓 128개는 성능 측정 결과가 아닌 설정 상한입니다.
- 연결당 1초 20개 명령, 참가자당 5초 5개 채팅입니다. RATE_LIMIT / CHAT_RATE_LIMIT으로 제한합니다.
- 송신 큐 64개가 가득 차면 느린 연결을 중단합니다. 작업·타이머 큐 합계는 1,024개입니다.
- JS 호출 50,000문장·100ms·8MiB 할당·재귀 48을 제한합니다. VM 전체 힙 한도나 OS 프로세스 격리가 아닙니다.

| 게임 오류 | 의미 |
| --- | --- |
| INVALID_GAME | ID·해시·action 형식 오류 |
| INVALID_GAME_PAYLOAD | payload JSON·바이트·깊이·값 수 위반 |
| GAME_NOT_WATCHED | 최초 watch 없이 action 요청 |
| GAME_NOT_APPROVED | 승인 번들 없음 또는 월드·내용 검증 실패 |
| GAME_SCRIPT_MISMATCH | 방에 바인딩한 ID·해시와 다름 |
| GAME_RATE_LIMIT | 5초당 4개 게임 action 초과 |
| GAME_COMMAND_REJECTED | TS가 명령 실행 중 JS 오류를 던짐. 상태 유지 |
| GAME_SCRIPT_FAULT | 초기화·실행 제한·출력·tick 오류. 해당 게임 중단, 다른 방·채팅 유지 |

기존 오류는 INVALID_MESSAGE / PROTOCOL_MISMATCH / UNKNOWN_MESSAGE / INVALID_ROOM / INVALID_NAME / INVALID_CHAT / INVALID_STATE / STATE_TOO_FAR / NOT_JOINED / ALREADY_JOINED / ROOM_FULL / SERVER_FULL / SERVER_BUSY / TEXT_REQUIRED / MESSAGE_TOO_LARGE입니다.

## 연결 경계

/health는 서비스와 버전, /api/rooms는 방 식별자·인원·정원을 반환합니다. WebSocket이 아닌 /ws는 400, 허용하지 않은 Origin은 403, 소켓 정원 초과는 503입니다. 같은 origin 또는 Realtime:AllowedOrigins의 정확한 origin을 허용하며 경로·쿼리·자격 증명·와일드카드는 거부합니다. Origin 없는 네이티브 테스트도 지원하며 이것은 계정 인증이 아닙니다. HTTPS 플레이어에는 WSS가 필요합니다.

운영자 로컬 승인 폴더의 코드만 실행합니다. 해시는 코드 내용 검증이며 작성자 인증·서명이 아닙니다. 일반 게스트 채팅은 월드 존재를 등록 서버에서 검사하지 않지만 게임 번들은 worldId까지 검사합니다. 영속 저장·재전송·자동 세션 복구·원격 스크립트 업로드는 제공하지 않습니다.
