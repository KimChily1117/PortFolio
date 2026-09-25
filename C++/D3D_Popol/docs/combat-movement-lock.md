# V13: 공격·스킬 사용 중 이동 제한

애니와 가렌의 일반 공격 및 Q/W/E/R 동작 중 이동을 막는다. **가렌 E만 사용 중 기존 이동을 계속하고 새로운 이동 명령도 받을 수 있다.**

- 공격·시전 시작 시 진행 중인 서버 이동 경로를 취소한다. 클라이언트의 목적지, 이동 마커, 남은 위치 보간도 정리한다.
- 동작 중 우클릭 이동과 서버 `C_MoveRequest`를 차단한다. 입력은 예약하지 않으며, 동작 종료 후 다시 클릭하면 이동한다.
- 다른 공격·스킬이 진행 중인 동작을 덮어쓰지 못하게 한다. 가렌 E의 회전 애니메이션은 이동 응답으로 RUN/IDLE로 바뀌지 않는다.
- 애니 W의 사거리 밖 자동 접근은 유지한다. 요청 전송부터 잠그고, 서버 승인 후 시전 동작으로 이어진다. 승인되지 않은 요청의 임시 잠금은 2초 후 해제된다.
- 같은 서버 틱의 MOVING → CANCELLED 전환을 허용하고, 취소된 이동 ID의 뒤늦은 MOVING 응답은 무시한다. 취소 시 서버의 최종 위치를 즉시 반영한다.
- Q/W 이펙트 잔상과 투사체 비행 시간은 이동 잠금을 연장하지 않는다. 기존 Q/W 크기·불투명도·범위·Assimp 변환 리소스는 그대로 사용한다.

서버 잠금 시간은 기존 `.clip`의 `frameCount / frameRate`에 맞춘다. 애니 일반 공격은 약 1.54초, Q/W/R은 약 1.04초, E는 약 0.58초다. 가렌 일반 공격은 2초, Q는 1초, W는 약 2.17초, R은 약 1.54초다. 가렌 E의 3초 동작은 이동 잠금에서 제외한다. 클라이언트는 기존 동작 종료 처리를 유지하며 로컬 가렌은 애니메이션 종료 콜백을 사용한다.

가렌 W는 기존 `Idle.clip` 대체 모션을 사용한다. 서버에 효과 처리가 없던 가렌 W 및 애니 E/R에는 시전 상태와 이동 잠금만 연결했다. 피해·보호막·소환 기능은 추가하지 않았다. 애니 E/R은 대상이 없어도 시전 패킷을 전송한다.

## 적용 파일 및 실행

클라이언트는 `BasePlayerController`, `PlayerController`, 챔피언 컨트롤러들과 애니 주문 요청 부분에 반영했다. 서버는 `Player`, `CombatActionMovement`, `AuthoritativeMoveService`, `PacketHandler`, 두 챔피언 스킬 핸들러에 반영했다. 프로토콜 변경은 없다.

`Binaries/Client.exe`와 `Binaries/Client_AnnieQW_AssimpV13.exe`는 동일 빌드다. **새 서버와 클라이언트를 모두 재실행해야 서버 이동 제한까지 적용된다.** 실행 중인 게임/서버를 이 작업에서 재시작하지는 않았다.

원본 백업: `tmp/attack-movement-before`. 클라이언트 비교: `tmp/attack-movement-changes.patch`. 서버 원본·적용본·해시·비교: `tmp/attack-movement-server`. 서버 적용 도구: `tools/apply_attack_movement_server.py`.

## 검증

- x64 Debug 솔루션 전체 Rebuild 및 C# 서버 빌드 성공.
- 숨겨진 DirectX 창의 클라이언트 검사 161개 통과: 기존 Q/W 렌더링·사거리 표시와 이동 정책, 오래된 응답, 가렌 E 예외, 잠금 해제, W 승인/타임아웃, 사망 상태 유지.
- 실제 서버 핸들러·이동 서비스 검사 86개 통과: 두 챔피언의 모든 공격·스킬, 경로 취소, 요청 거부, 종료 후 이동, 실제 클립 길이 비교, 가렌 E 6회 × 47 피해 유지.
- 기존 W 범위/피해 검사 12개 통과.
- 실제 두 클라이언트를 접속한 수동 플레이 검사는 수행하지 않았다.

서버 검사는 `Tests/CombatMovement`에서 `dotnet run --project ServerMovementTests.csproj`, W 검사는 `Tests/AnnieW`에서 `dotnet run --project ServerRangeTests.csproj`로 실행한다. 클라이언트 검사는 `Binaries`에서 `Client.exe --annie-q-smoke`로 실행한다. 결과는 각각 테스트 폴더의 `results`에 기록한다.
