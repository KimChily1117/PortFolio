# 기존 C# 서버 코드 재사용 기록

2026-09-29, 원본 `E:\task\Server`의 Project Dawn 서버를 읽고 공통 기반을 새 UGC 서버로 옮겼습니다.
원본은 수정하지 않았습니다. Git 관리본의 기준 커밋은 `83a89c9700a162841ccc83aa955e9fd148b3ede7`입니다.

## 실제 코드 재사용

| 원본 경로 (`E:\task\Server` 기준) | 새 파일 | 변경 |
| --- | --- | --- |
| `Server/Server/Game/Job/Job.cs` | `src/Kimchily.Server.Core/Jobs/Job.cs` | `IJob.Execute`, Action 작업과 제네릭 인자 작업을 이식. 사용하지 않는 다중 인자 변형은 생략 |
| `Server/Server/Game/Job/JobSerializer.cs` | `src/Kimchily.Server.Core/Jobs/JobSerializer.cs` | 큐에 넣고 Flush하는 구조 유지. 실행 전체 잠금, 용량 제한, 비동기 요청 결과 추가 |
| `Server/Server/JobTimer.cs` | 같은 `JobSerializer.cs`의 예약 작업 | 예약 후 Flush하는 흐름 유지. .NET 최소 힙과 64비트 단조 시계를 사용하도록 수정 |

원본 `JobTimerElem.CompareTo`는 실행 시각을 오름차순으로 비교하지만, 연결된
`ServerCore/PriorityQueue.cs`는 최대 힙입니다. 이 조합에서는 늦은 예약 작업이 Peek를 차지하여
앞선 시각의 작업을 막을 수 있습니다. 새 구현은 `(실행 시각, 등록 순서)`의 최소 힙을 사용하며,
100ms 작업을 먼저 등록하고 20ms 작업을 나중에 등록하는 회귀 검사로 순서를 확인했습니다.

원본 큐는 각 Pop만 잠급니다. 새 구현은 전체 Flush도 잠가 둘 이상의 호출자가 작업 본문을
동시에 실행하지 못하도록 했습니다. 현재 서버는 단일 주기 루프에서 모든 방 작업을 처리합니다.
방별 실행 큐나 다중 프로세스 분산은 아직 구현하지 않았습니다.

## 설계를 참고하고 새로 구현한 부분

`GameRoom`, `RoomManager`의 방 등록·입퇴장·초기 상태·빈 방 정리 방식을 참고했습니다.
현재 `RoomHub`는 UGC 식별자와 게스트 채팅에 맞춘 새 구현입니다. 기존 `GameRoom`을
그대로 상속하거나 기존 서버 프로세스에 접속하는 구조는 아닙니다.

`ClientSession : PacketSession`은 TCP 소켓, DB 로그인, 캐릭터 선택, UDP 토큰과 결합되어 있습니다.
UGC에서는 전송과 규칙 사이에 `IRoomPeer`를 두고 WebSocket 구현을 별도로 추가했습니다.
기존 Protobuf 패킷 생성기와 TCP 프레이밍은 이번 JSON 텍스트 통신에 포함하지 않았습니다.

기존 이동은 2차원 좌표와 마을/던전 맵 규칙에 의존하므로 이번 단계에서 복사하지 않았습니다.
3차원 이동·회전·점프 상태와 범위 검증은 다음 단계에서 별도 메시지로 추가합니다.

## 읽은 원본의 SHA-256

```text
4A6D76F20035E350D4481516063A71FFA19E282C0954C1869E690750B99BDCAB  Server/Server/Game/Job/Job.cs
6FC0441BC18A118938D216C524FB93CD807AE1ED9FBBBFE7F6C1C961C91342DE  Server/Server/Game/Job/JobSerializer.cs
A2750F63380198F9A98A1BB88C0973D135916A998F7153E5054F28112E5569BD  Server/Server/JobTimer.cs
4C5A1B66BAD1E2E0473B726D41DD937677F50DD4FB48D0945C59209124F859EB  Server/ServerCore/PriorityQueue.cs
```

Git 관리본에서는 `C#/Server/` 아래 같은 상대 경로로 찾을 수 있습니다.
새 서버는 원본 경로에 ProjectReference를 걸지 않았으므로 다른 PC에서도 독립적으로 빌드됩니다.
