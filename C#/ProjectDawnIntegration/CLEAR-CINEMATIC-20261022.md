# 클리어 → 암전 → 시네마틱 → 과반수 스킵 → 보상

## 동작

서버가 보스 처치와 클리어를 확정하면 약 0.5초의 시작 여유를 두고 전원 화면을 2초간 암전한다. 이어 사용자가 제공한 바칼 시네마틱 전체 약 40.7초를 재생한다. 음성도 해당 영상의 실제 오디오다. 화면 비율은 유지하고 여백은 검정색으로 표시한다.

ESC는 시청 중 스킵에 한 표를 넣는다. 서버가 연결된 시청자의 과반수를 확인하면 전원 영상이 종료되고 기존 바칼 보상창을 연다. 4인=3표, 3인=2표, 2인=2표, 1인=1표다. 영상이 정상적으로 끝나는 경우 투표 없이 모두 보상창으로 간다. 영상 도중 연결이 종료된 사람은 투표 분모에서 제외하되, 클리어 시점에 확정한 보상 자격은 바꾸지 않는다. 영상 로딩에 실패한 PC도 안내 문구와 투표 기능을 제공하고 서버 종료 시간에 맞춰 보상창으로 진행한다.

보상은 기존 서버 outbox/MySQL 경로로 영상 시청과 동시에 저장한다. 영상 스킵이 장비를 생성하거나 보상을 중복 지급하지 않는다. DB 저장이 늦으면 시네마틱 종료 뒤 기존 보상 저장 중 화면을 표시한다. 보상 저장과 시네마틱 종료가 모두 끝난 뒤 기존 15초 결과 대기·마을 복귀가 시작된다. 전투 계산은 기존 방장 Lua/공통 C# → 서버 검증·확정 구조다. AWS 변경은 없다.

## 서버와 클라이언트

| 구분 | 책임 |
|---|---|
| `RaidClearCinematic` | 서버 시계·엄격한 과반수·중복 투표 방지·종료 상태 고정 |
| `C_RaidCinematicSkip` | encounter만 전달. 슬롯·보상값은 클라이언트가 지정할 수 없음 |
| `S_RaidCinematic` | encounter, revision, 경과시간, 투표 슬롯, 필요 표 수, 종료 사유 |
| `GameRoom_RaidCinematic` | 인증된 세션→실제 슬롯, 현재 레이드 확인, 초당 상태 전송 |
| `GameRoom_RaidLifecycle` | 클리어 즉시 보상 저장, 영상 종료 전 마을 복귀 차단 |
| `RaidDemoApp.ClearCinematic` | 암전·로컬 VideoPlayer·ESC 요청·서버 종료 후 보상 공개 |

영상 파일은 빌드에 포함되며 서버에서 스트리밍하지 않는다. 재생 경과는 서버 상태와 측정 RTT의 절반을 사용해 맞추고 큰 재생 오차는 보정한다. 프레임 단위 완전 동시 재생을 보장하는 미디어 전송 프로토콜은 아니다. 오래된 encounter/낮은 revision/완료 후 과거 상태는 무시한다. SQL 영수증 수신만으로 영상이 닫히지 않도록 화면에서 보류한다.

## 리소스와 빌드

사용자 파일: `C:/Users/YOUR_USER/Downloads/[던파] 신규 스토리 - 기계혁명 _ 개전, 바칼 시네마틱 컷신 #2.mp4`.

원본 MP4는 Windows Unity에서 duration 읽기 오류가 발생했다. 전체 영상·음성을 H.264 Baseline(1280×720, 30fps, B프레임 없음)과 AAC 스테레오 48kHz로 변환한다. 편집·생략은 하지 않는다. 출처와 해시는 `Artifacts/clear-cinematic-20261022/provenance.json`에 기록한다.

```powershell
cd E:\task\ProjectDawnIntegration
python Tools/ImportClearCinematic.py '<제공 MP4 경로>' --ffmpeg E:/task/ProjectDawnTools/media/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe
.\Build-Unity.ps1
.\Start-LocalGame.ps1
```

빌드 리소스: `Build/ProjectDawnRaid_Data/StreamingAssets/Cinematics/bakal-clear.mp4`. 실행 파일만 옮기지 말고 Build 폴더 전체를 사용한다. 영상이 바뀌면 서버의 `VideoMs`도 실제 길이에 맞춰 변경하고 양쪽을 다시 빌드해야 한다.

## 검증

- 과반수 정책 46개 통과: 1~4인, 중복·이전 encounter·비참가자 거절, 정확한 종료 경계, 연결 해제, 종료 후 변경 방지.
- 실제 TCP·MySQL lifecycle 50개 통과: 시네마틱 전 마을 복귀 차단, 중복/오래된 투표 거절, 서버 공동 종료, 보상 중복 지급 방지·복구 포함.
- 기존 lifecycle 검사의 고정 sleep을 최종 퇴장 상태를 기다리는 방식으로 변경했다. 입력 유예 구간이 끝나기 전 검증하던 타이밍 의존성을 제거했으며, 퇴장 확정 조건을 유지했다.
- 4인 GUI 시험은 전용 `RewardPlaytestHost`가 보스 HP만 낮춰 시간을 줄인다. 실제 공격으로 처치하고, 실제 서버 클리어·투표·SQL 지급 경로를 통과한다. 일반 난이도 공략이나 서로 다른 네 PC 시험으로 표현하지 않는다.

```powershell
dotnet run --project ClearCinematicTests
.\Play-RewardFixture4.ps1 -Port 28416 -DelayMs 100 -Cinematic skip
# 또는 -Cinematic full
python Tools/VerifyClearCinematic.py <run>
.\Tools\VerifyRewards4.ps1 -Run <run>
python Tools/BuildClearRecording.py <run> --ffmpeg E:/task/ProjectDawnTools/media/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe
```

자동 GUI 시험의 투표는 ESC와 같은 요청 함수를 호출한다. 실제 키보드 경로는 `Input.GetKeyDown(KeyCode.Escape)`이며, 암전·영상 동안 채팅/메뉴/전투 입력을 먼저 처리하지 않는다.

## 후속 UI 수정

사용자 요청에 따라 결과창의 ‘영수증 저장됨’ 안내를 제거했다. SQL의 영수증/중복 지급 방지 자료는 유지하며 사용자에게는 ‘보상 지급 완료’만 표시한다.

상단 보스 HP는 시연용으로 투명하게 한 것이 아니었다. `DawnHudView`의 Canvas 위에 IMGUI 전장이 불투명하게 그려져 원래 보스 바를 가렸다. 추출한 `bossRed`/`bossWhite`는 alpha가 전 픽셀 255임을 확인했다. 기존 원본 프레임·붉은 HP·흰 피해 잔상을 `DrawOriginalBossHealth`로 전장 이후 같은 HUD 패스에 그린다. 검은 바탕과 퍼센트·보스 얼굴을 유지하며 서버 HP나 공격 판정에는 변경이 없다. 원래 프리팹의 상태/잔상 업데이트 경로는 유지하고 화면 출력만 중복되지 않게 끈다.


## 최종 후속 수정

HP의 두 번째 원인(원본 프레임의 검은 내부가 빨간 게이지를 덮음), 반응형 매칭 슬롯, Liberation·순차 카드·원본 에픽 섬광, Tab 회피는 `HUD-MATCHING-RESULT-20261022.md`에 정리했다. 결과 자동 귀환은 영상 종료 직후 15초가 아니라 **공개 구간(4인 12.4초; 사용자 추가 요청 3초 카운트 포함) 종료 후 15초**다.

초기 자연 종료 4인 검증: `Artifacts/rewards-4p-20261022-053701` (시네마틱 9 + 보상 12 통과). 최종 연출/입력 검증 경로는 후속 문서에서 확인한다.
