# Kimchily room protocol 1

WebSocket endpoint: `/ws`. UTF-8 JSON 텍스트 메시지 한 개가 명령 한 개입니다.
TCP 패킷 길이 헤더나 Protobuf는 붙이지 않습니다. 프레임이 나뉘어 도착하면 EndOfMessage까지
조립하고 JSON을 해석합니다. 최대 합계는 4,096바이트입니다.

모든 클라이언트 명령은 `protocolVersion: 1`과 `type`을 보냅니다.
등록되지 않은 필드는 거부합니다. 발신자 ID는 서버가 연결별로 결정하므로 클라이언트가 보내지 않습니다.

## 클라이언트 명령

```json
{"protocolVersion":1,"type":"join","worldId":"network-demo","revisionId":"v1","roomId":"playground","name":"칠리"}
```

ID 세 개는 각각 ASCII 영문·숫자·밑줄·하이픈 1~80자이며 대소문자를 구분합니다.
다른 게시 버전은 같은 방 코드를 사용해도 별개 방입니다. 닉네임은 공백을 제거한 1~24자이며
제어 문자를 허용하지 않습니다. 닉네임의 고유성을 요구하지 않습니다.

```json
{"protocolVersion":1,"type":"chat","text":"안녕하세요!"}
{"protocolVersion":1,"type":"leave"}
{"protocolVersion":1,"type":"ping"}
```

채팅은 앞뒤 공백을 제거한 1~300 UTF-16 코드 단위이며 제어 문자를 허용하지 않습니다.
HTML 문자를 포함할 수 있으므로 소비자는 반드시 텍스트로 렌더링해야 합니다.

입장 후 캐릭터 상태를 보낼 수 있습니다. 기존 채팅 전용 클라이언트도 계속 사용할 수 있습니다.

```json
{"protocolVersion":1,"type":"state","state":{"sequence":1,"x":0,"y":1,"z":0,"yaw":90,"speed":3,"grounded":true,"verticalVelocity":0}}
```

Unity는 초당 10회 전송합니다. 서버는 최소 70ms 간격과 증가하는 sequence만 허용하며,
중복/이전 번호와 너무 잦은 갱신은 무시합니다. 좌표는 ±10,000, yaw는 [0,360),
speed는 [0,25], verticalVelocity는 ±100 범위의 유한수여야 합니다.
이동량은 최대 2초의 간격을 적용하여 수평 25m/s + 1.5m, 수직 100m/s + 2m까지 허용합니다.
최초 스폰보다 20m 이상 낙하한 뒤 최초 스폰 주변으로 복귀하는 기본 리스폰은 예외입니다.
필드 범위 오류는 INVALID_STATE, 이동량 초과는 STATE_TOO_FAR입니다.
서버는 월드 충돌/지형을 시뮬레이션하지 않습니다.

## 서버 이벤트

| type | 필드와 의미 |
| --- | --- |
| `hello` | 연결 직후 `selfId`. 아직 방에 들어간 상태가 아님 |
| `joined` | `selfId`, `room`, `players`, `history`. 자신을 포함한 전체 참가자와 최근 채팅 |
| `playerJoined` | `player: {playerId, name}`. 기존 참가자에게만 전달 |
| `playerLeft` | `player: {playerId, name}` |
| `chat` | `chat: {id, playerId, name, text, sentAtUtc}`. 보낸 사람을 포함해 방 전체 전달 |
| `state` | `player: {playerId, name, state}`. 같은 방의 다른 참가자에게 최신 캐릭터 상태 전달 |
| `left` | 자신의 퇴장 확인. 소켓은 계속 열려 있어 재입장 가능 |
| `pong` | ping 응답 |
| `error` | `code`, 사용자에게 표시할 `message` |

`room`은 `{worldId, revisionId, roomId}`입니다. 모든 이벤트에 `protocolVersion: 1`이 있습니다.
참가자 객체는 선택적 `state`를 포함합니다. 아직 위치를 보내지 않은 참가자는 null이며,
joined 스냅샷에는 기존 참가자의 최신 state가 포함됩니다. playerId/name은 서버가 결정합니다.
시간은 UTC ISO 8601입니다. 같은 소켓에는 하나의 송신 루프만 쓰며, 방 작업은 순서대로 실행합니다.
서로 다른 연결에서 동시에 보낸 메시지의 처리 순서는 서버가 받은 순서로 결정합니다.
채팅 ID는 중복 표시 방지를 위한 식별자로 활용할 수 있지만, 현재 재전송/전달 보장 프로토콜은 없습니다.

## 제한과 오류

- 방당 8명, 동시에 32개 방, 열린 소켓 128개. **성능 측정 결과가 아닌 설정 상한**입니다.
- 전체 명령은 연결당 최근 1초에 최대 20개. 초과하면 `RATE_LIMIT` 후 연결을 종료합니다.
- 채팅은 참가자당 최근 5초에 최대 5개. `CHAT_RATE_LIMIT` 후에도 연결은 유지합니다.
- 클라이언트당 송신 큐 64개. 소비가 늦어 큐가 가득 차면 그 연결을 중단합니다.
- 대기 작업과 타이머 합계 1,024개. 포화 시 `SERVER_BUSY`로 연결을 종료합니다.
- 비텍스트 메시지 `TEXT_REQUIRED`, 크기 초과 `MESSAGE_TOO_LARGE`도 연결 종료 대상입니다.
- 잘못된 JSON·미등록 필드 `INVALID_MESSAGE`, 버전 불일치 `PROTOCOL_MISMATCH`,
  알 수 없는 명령 `UNKNOWN_MESSAGE`는 오류 응답을 반환합니다.
- 방·닉네임·채팅 검증: `INVALID_ROOM`, `INVALID_NAME`, `INVALID_CHAT`.
- 입장 상태/정원: `NOT_JOINED`, `ALREADY_JOINED`, `ROOM_FULL`, `SERVER_FULL`.

최근 채팅은 방마다 20개만 유지하고, 마지막 사람이 나가면 방과 기록을 제거합니다.
재접속은 신규 참가자로 다시 join하는 방식입니다. 장치·계정 ID나 복구 토큰은 아직 없습니다.

## HTTP 및 연결 경계

`GET /health`는 서비스명 `kimchily-realtime`과 통신 버전을 반환합니다.
`GET /api/rooms`는 방 식별자·참가자 수·정원을 반환하며 채팅 내용을 반환하지 않습니다.
WebSocket이 아닌 `/ws` 요청은 400, 다른 브라우저 Origin은 403, 연결 정원 초과는 503입니다.

같은 HTTP(S) origin은 기본 허용합니다. 별도 Unity 플레이어 출처는
`Realtime:AllowedOrigins`에 정확한 origin을 등록합니다. 경로·쿼리·자격 증명은 허용하지
않으며 와일드카드는 지원하지 않습니다. `tools/run.ps1`은 개발용 127.0.0.1/localhost:8788을
기본 추가합니다. Origin이 없는 네이티브 Unity/CLI 클라이언트도 허용하며, 이는 인증이 아닙니다.
HTTPS 플레이어는 WSS 서버 주소를 사용해야 합니다.

게스트 데모에서는 월드 ID 등록 검사, 계정 인증, 영속 저장, 메시지 재전송, 자동 세션 복구를
제공하지 않습니다. 서버에 임의 `.ts`를 업로드·실행하는 명령도 없습니다.
