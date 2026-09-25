# Annie W Cone Targeting and Attack Cursor

작성일: 2026-07-28

## 구현 결과

이번 변경은 기존 서버 권위 이동과 스킬 처리 구조를 유지하면서 다음 기능을 추가한다.

- Annie W를 누르면 바닥에 월드 공간 부채꼴 가이드가 표시된다.
- 마우스로 방향을 정하고 왼쪽 버튼으로 확정한다.
- 사거리 4 안이면 즉시 서버에 스킬 요청을 보낸다.
- 사거리 밖이면 서버 권위 `C_MoveRequest`로 시전 가능 거리까지 이동한다.
- 해당 이동의 `ServerMoveId`가 `ARRIVED` 상태가 된 뒤에만 W 요청을 보낸다.
- 서버가 확정한 시전 원점과 방향으로 클라이언트의 W 애니메이션과 파티클을 재생한다.
- 일반 공격 가능 적 또는 Annie/Garen Q 대상으로 적을 가리키면 기존 `singletarget` 검 모양 커서를 표시한다.

## 조작

### Annie W

1. `W`를 누른다.
2. 마우스로 부채꼴 방향을 정한다.
3. 왼쪽 버튼으로 위치를 확정한다.
4. 사거리 밖이라면 캐릭터가 자동으로 시전 가능 거리까지 이동한다.
5. 서버 도착 Snapshot을 받은 뒤 W가 시전된다.
6. `Esc` 또는 오른쪽 버튼으로 조준을 취소한다.

이동 중 새 우클릭 이동이나 Q/E/R 입력을 하면 대기 중인 W 시전은 취소된다. 서버가 이동을 거부하거나 해당 이동을 취소해도 W는 전송되지 않는다.

### 공격 커서

- 스킬을 선택하지 않은 상태에서 적 위에 마우스를 올리면 평타용 검 커서가 표시된다.
- Annie Q 또는 Garen Q를 선택하고 적 위에 마우스를 올려도 검 커서가 표시된다.
- UI가 입력을 점유하거나 Annie W를 조준·이동 대기 중이면 기본 커서로 복구된다.

## 서버 권위 Annie W 판정

- 이동 평면: XZ
- 사거리: 4 world units
- 전체 각도: 50도, 반각 25도
- 시전 원점: 서버에 저장된 caster 권위 위치
- 시전 방향: 클라이언트의 목표 위치에서 계산하되 서버에서 정규화하고 검증
- 허용 요청 거리: 0.01 초과, 4.1 이하
- 적중 조건:
  - 적 팀
  - 원점에서 거리 4 이하
  - 방향 내적이 `cos(25 degrees)` 이상
- 피해: 기존 정책 유지, 200ms 간격으로 `3 damage x 10 ticks`
- 원점과 방향은 첫 승인 때 고정되며 후속 tick 동안 바뀌지 않는다.

서버의 Room JobQueue 안에서 Handler가 실행되므로 Annie/Garen Handler 내부의 중복 `room.Push`는 제거했다. 이로써 즉발 스킬에 불필요하게 Room Flush 한 번이 추가되던 지연도 제거한다.

## 프로토콜

기존 필드 번호는 유지하고 `S_SkillResult` 끝에 다음 필드를 추가했다.

```protobuf
Vector3 castOrigin = 8;
Vector3 castDirection = 9;
```

클라이언트는 Annie W에 한해 자신의 로컬 선재생을 하지 않고 이 서버 결과를 받아 애니메이션과 이펙트를 재생한다. 다른 기존 스킬의 로컬 연출 정책은 변경하지 않았다.

## 가이드 렌더링

- 기존 `conicrangeindicator.dds`와 `conicrangeindicator` Material을 재사용한다.
- 별도 `AnnieWConeIndicator` GameObject와 `SkillIndicatorController`가 가이드 상태를 관리한다.
- 일반 Mesh pass와 분리된 alpha blend/depth-read/no-depth-write pass를 사용한다.
- 지면보다 Y를 0.025 올려 Z-fighting을 줄인다.
- 숨김은 Transform scale을 0으로 만들어 처리한다.

현재 가이드는 기존 Quad와 텍스처 축을 기준으로 배치한다. 실제 카메라 화면에서 텍스처 방향 또는 길이 축이 반대로 보이는지는 실행 시각 검증 대상으로 남는다.

## 변경 파일

클라이언트:

- `GameCoding2/SkillIndicatorController.h`
- `GameCoding2/SkillIndicatorController.cpp`
- `GameCoding2/TestScene.cpp`
- `GameCoding2/BasePlayerController.h`
- `GameCoding2/BasePlayerController.cpp`
- `GameCoding2/PlayerController.h`
- `GameCoding2/PlayerController.cpp`
- `GameCoding2/AnnieWSpell.h`
- `GameCoding2/AnnieWSpell.cpp`
- `GameCoding2/AnniePlayerController.h`
- `GameCoding2/AnniePlayerController.cpp`
- `GameCoding2/AnnieOtherPlayerController.cpp`
- `GameCoding2/ClientPacketHandler.cpp`
- `EngineCore/UIManager.cpp`
- `Shaders/23. RenderDemo.fx`
- C++ Protocol generated files

서버:

- `Server/Common/protoc-3.12.3-win64/bin/Protocol.proto`
- `Server/Server/Server/Packet/Protocol.cs`
- `Server/Server/DummyClient/Packet/Protocol.cs`
- `Server/Server/Server/Game/ChampSpell/AnnieSkillHandler.cs`
- `Server/Server/Server/Game/ChampSpell/GarenSkillHandler.cs`
- `Server/Server/NavGridTests/CombatPhaseTests.cs`

## 빌드 및 테스트

- Client/Engine Debug x64 full sequential Rebuild: 성공
- Server Debug DLL (`UseAppHost=false`): 성공
- Server/DummyClient Release solution: 성공
- C++ NavGrid 회귀: 279 assertions 통과
- C# NavGrid/이동/A*/Snapshot/전투 회귀: 436 assertions 통과
- Annie W 검증:
  - 정면 적에게 `3 x 10` 적용
  - 아군 제외
  - 90도 측면 적 제외
  - caster 후방 적 제외
  - authoritative cast origin/direction 패킷 확인

기존 서버 Debug 프로세스가 `Server.exe`를 실행 중이라 apphost 파일 교체만 잠겨 있었다. 프로세스를 임의 종료하지 않고 `UseAppHost=false`로 갱신된 `Server.dll`을 빌드했으며, Release 전체 솔루션은 정상 빌드했다. 실제 플레이 검증 전에는 실행 중인 Debug 서버를 종료하고 새 빌드로 재시작해야 한다.

## 알려진 제한

- Annie W 가이드의 실제 화면 방향·크기는 DirectX 실행 화면에서 최종 시각 확인이 필요하다.
- W 이동 후 시전은 서버 Snapshot이 활성화된 권위 이동 모드가 전제다.
- 이동 요청 실패 시 기존 이동 명령 정책은 서버 Phase 4/5 정책을 그대로 따른다.
- W는 서버 승인 후 연출하므로 네트워크 왕복 시간만큼 시작 반응이 늦을 수 있다. 입력 예측은 이번 변경에 포함하지 않았다.
- 범용 스킬 Indicator Framework로 확장하지 않았으며 Annie W에 필요한 최소 구현만 추가했다.

## 2026-07-28 가이드 표시 보정

실행 화면 확인 후 다음을 추가 보정했다.

- 원본 `conicrangeindicator.dds`가 알파가 없는 DXT1 텍스처여서 보이던 검은 사각 Quad를 전용 Pixel Shader의 부채꼴 마스크로 제거했다.
- 마스크 폭은 전체 각도 50도, 길이는 4 world units로 구성하여 서버 Annie W 판정과 맞췄다.
- 텍스처 방향을 반전하여 꼭짓점이 caster, 넓은 호가 최대 사거리 방향을 향하게 했다.
- 가이드 월드 Y를 고정 `2.0f`로 설정했다.
- 기본 커서와 공격 커서 크기를 `75 x 75`에서 `37.5 x 37.5`로 50% 축소했다.
- 전용 셰이더를 포함한 Client/Engine Debug x64 전체 Rebuild가 성공했다.
## 2026-07-28 Rift 가림 보정

- Indicator GameObject는 부모가 없으므로 `SetPosition(y=2.0)`은 월드 좌표이며 로컬 좌표 누적 문제가 아니다.
- Terrain Transform도 Y=2.0이고 Rift 메시가 같은 지면 깊이를 기록한다.
- 기존 P17은 Depth Write가 꺼져 있어 Forward 렌더 순서에 따라 Rift가 Indicator를 덮을 수 있었다.
- Indicator 전용 `SkillIndicatorDepthState`에서 Depth Write를 켰다.
- Indicator 전용 Rasterizer에 negative Depth Bias를 적용하여 월드 Y=2.0을 유지하면서 지면보다 안정적으로 앞에 표시되게 했다.
- Client/Engine Debug x64 전체 순차 Rebuild 성공.
- 보존 빌드: `Binaries/Client_FireballV8_AnnieWConeCombatV3_RiftVisible.exe`
- SHA-256: `F6D8AE2D5C7626DCA79DD49F2EDC728ED44B6112FD255C4C64F858BFA59513C7`
## 2026-07-28 최종 회색 배경 제거와 전체 원인

최종 실행 화면에서 청록색 부채꼴 뒤에 밝은 회색 삼각 영역이 남는 현상을 확인했다.

### 원인 1: 원본 DDS에 알파가 없음

- `Resources/Textures/UI/indicator/conicrangeindicator.dds`는 512 x 512 DXT1 텍스처다.
- 이 파일은 투명 배경용 알파를 제공하지 않고 검정/회색 배경 픽셀을 실제 RGB 색상으로 저장한다.
- 일반 alpha blend와 `sampled.a` 기반 clip만으로는 Quad 배경을 제거할 수 없다.

### 원인 2: 첫 번째 밝기 기반 마스크의 오분류

- 초기 보정에서는 픽셀 최대 밝기로 cyan artwork와 배경을 구분했다.
- 텍스처의 곡선형 최대 사거리 arc 뒤에는 검정뿐 아니라 밝은 무채색 회색 픽셀이 존재한다.
- 이 회색 픽셀도 밝기가 높아서 artwork로 분류됐고 높은 alpha를 받아 화면에 남았다.
- 분석용 삼각 마스크는 직선 양쪽 경계만 제한하므로 텍스처 arc 뒤쪽의 회색 여백까지 포함할 수 있었다.

### 원인 3: Rift와 Indicator의 깊이 충돌

- Indicator에는 부모 Transform이 없어 `SetPosition(y=2.0)`은 월드 좌표다.
- Terrain도 Y=2.0이고 Rift 메시가 같은 지면 깊이를 기록한다.
- 기존 P17은 Depth Write가 꺼져 있어 Forward 렌더 순서에 따라 Rift가 Indicator를 덮었다.
- 이 문제는 Indicator 전용 Depth Write와 negative Depth Bias로 해결했으며 Y=2.0은 유지했다.

### 최종 해결

- 밝기가 아니라 cyan chroma를 사용한다.
- `max(G, B) - R` 값으로 청록색 픽셀과 무채색 회색을 구분한다.
- 회색은 R/G/B가 비슷해 chroma가 0에 가깝기 때문에 Pixel Shader에서 discard된다.
- 청록색 내부 채움과 외곽선은 G/B가 R보다 강하므로 그대로 유지된다.
- 방향, 사거리 4, 전체 각도 50도, 월드 Y=2.0, Depth 보정은 변경하지 않았다.

최종 검증:

- FXC Indicator Shader 컴파일 성공
- Client/Engine Debug x64 전체 순차 Rebuild 성공
- 오류 0
- 실행 파일: `Binaries/Client_FireballV8_AnnieWConeCombatV4_FinalNoGray.exe`
- SHA-256: `5D475078F880570CACACE23C23E8AB9AE4153363C943073937357B2FFDEEDC13`