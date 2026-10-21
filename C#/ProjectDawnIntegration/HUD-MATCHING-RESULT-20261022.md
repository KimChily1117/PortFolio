# 보스 HP · 매칭 대기 · 보상 연출 · Tab 회피

## 이번 요청과 승인

- AWS는 계속 정지 상태로 두며 접근·배포하지 않는다.
- 매칭 대기 화면의 비율과 기존 캐릭터 슬롯 복원. 닉네임은 머리 위.
- 보스 HP를 원본 스프라이트로 재구성. 투명하게 보이던 문제 제거.
- 사용자가 승인한 결과 타임라인: 시네마틱 종료/과반수 스킵 확정 후 시작, 3→2→1 카운트 후 5.2초부터 1.8초씩 카드 공개. 4인은 12.4초에 공개 구간 종료. 그 뒤 자동 귀환 대기 15초.
- Liberation 전체 음원을 결과 전용 로컬 AudioSource로 재생.
- 사용자가 선택한 `newepicd01`/`newepicd02`로 금빛 방사 광선과 세로 섬광 구현.
- 긴급회피를 Tab으로 변경. Space는 커스텀 커맨드용으로 유지.
- 사용자 선택: 3번 bk_ui_count로 3→2→1, 카드마다 6번 bk_ui_card_unlock → 1번 bk_ui_card_open. 카드 등장에는 bk_ui_card_appear.

## 보스 HP 원인과 수정

첫 번째 문제는 Canvas HUD가 IMGUI 전장 뒤에 가리는 표시 순서였다. 같은 IMGUI 패스로 옮긴 뒤에도 원본 `monster_hp.img` 프레임의 검은 내부 영역을 게이지 위에 그려 HP가 가려졌다. 원본 프레임은 투명 테두리 전용 이미지가 아니었다.

현재는 검은 바탕/금색 프레임 → 흰 피해 잔상 → 빨간 HP → 원본 분할 셀 → 상단 광택 순서다. `monster_hp_ver2.img`의 11, 13, 18, 20, 22번 프레임을 추가 추출했다. 두꺼운 HP, 보스 얼굴, 남은 퍼센트, 별도 금색 무력화 게이지를 사용한다. HP·피해량 등 전투 판정은 바꾸지 않았다.

REA `open_binary`는 해당 NPK를 `unsupported binary format`으로 거절했다. 원본의 실행 코드를 복원했다고 주장하지 않는다. 기존 PyDnfEx 디코더로 NPK/IMG를 읽었으며 파일 범위·프레임·SHA-256은 `Tools/original-combat-hud-provenance.json`에 기록한다. 거절 결과는 `Artifacts/hud-matching-20261022/rea-open-result.json`이다.

## 매칭 대기 화면

원본 `DawnOriginal/PartyPopUp.prefab`의 크기는 1920×1080이다. 이를 1280×800 Canvas 기준으로 두어 잘리고 있었다. CanvasScaler는 원본 해상도와 Expand를 사용하고, 배경은 비율을 유지한 채 화면을 채운다. 배경의 일부 가장자리는 화면 비율에 따라 잘릴 수 있지만 캐릭터 슬롯·텍스트는 화면 안에 유지한다.

캐릭터 발 위치는 원본 `Slot_1`~`Slot_4` RectTransform에서 변환한다. 고정 화면 픽셀로 임의 배치하지 않는다. 원본 코디 레이어와 Idle 프레임을 그리며 닉네임은 머리/날개 위로 옮겼다. `LobbyPlayerInfo.appearance = 4`를 추가해 서버가 보유한 코디를 매칭 참가자 순서대로 전송한다. 캐릭터가 다른 마을에 있어도 외형 정보가 전달된다.

실제 Unity 창에서 1280×800, 1280×720, 1024×768, 1600×686으로 실행 중 해상도를 바꿔 확인했다. `Artifacts/hud-matching-20261022/layouts-final`은 가상 참가자 4명의 UI 검사이고, 실제 SQL/네트워크 매칭 검사는 별도 4인 실행이다.

## 결과 연출과 권위

`RaidRewardPresentation`이 공개 준비/카운트 5.2초 + 참가자 수 × 1.8초 + 귀환 대기 15초를 서버·클라이언트에 공유한다. 1~3인에서는 실제 참가자 카드만 중앙 정렬해 공개한다. 보상 지급은 계속 SQL 트랜잭션·영수증으로 확정하며 카드 연출은 지급을 실행하지 않는다.

| 결과 화면 기준 시간 | 연출 |
|---|---|
| 0초 | 영상 종료/스킵 확정 후 결과 시계 초기화, Liberation 시작 |
| 0~1.2초 | 성공 문구와 금빛 광채 |
| 1.2~2.2초 | 참가자 이름·초상화·카드 등장 |
| 2.2~5.2초 | 원본 숫자 스프라이트 3→2→1 + 선택된 카운트 효과음 |
| 5.2초부터 | 참가자별 1.8초: 빛 모으기 → 원본 카드 애니메이션·방사광 → 세로 섬광·장비 공개 |
| 4인 12.4초 | 전체 공개 구간 종료, 15초 귀환 카운트 시작 |

Liberation을 처음 여는 순간 약 0.4초의 로딩 정지가 측정되어, 레이드 입장 시 LoadAudioData로 미리 디코딩하도록 수정했다. Resources.Load만으로는 preloadAudioData가 꺼진 클립의 첫 Play 정지를 없애지 못했다. 음악 데이터는 네트워크로 보내지 않으며 서버는 결과 상태만 확정한다. 카드 이미지와 에픽 광선은 원본이고, 타이밍·합성·배치는 재구현이다. 검은 바탕의 가산합성용 광선은 RGB 최대값으로 알파를 복원해 IMGUI에서 근사 표시한다. 원본 픽셀은 Artifacts에 보존한다.

## 입력

`RaidDemoApp.cs`의 긴급회피 입력은 `KeyCode.Tab`이다. Space는 커맨드 인식기에만 연결되며 커맨드 완료 시 회피를 취소하던 이전 보정 코드도 제거했다. 하단 HUD·F1 도움말·F6 커맨드 창에 변경 내용을 표시한다. 서버의 회피 판정·쿨타임·호스트 계산 및 서버 검증 방식은 유지한다.

## 재현과 주요 파일

```powershell
python Tools/ImportOriginalCombatHud.py
python Tools/ImportEpicRewardFlash.py
python Tools/ImportBakalCountdown.py
.\Build-Unity.ps1
.\Play-RewardFixture4.ps1 -Port 28446 -DelayMs 100 -Cinematic full
python Tools/VerifyClearCinematic.py <실행폴더>
python Tools/VerifyResultPresentation.py <실행폴더>
.\Tools\VerifyRewards4.ps1 -Run <실행폴더>
```

- `UnityClient/Assets/Scripts/RaidDemoApp.DawnHud.cs`: 보스 HP 구성
- `UnityClient/Assets/Scripts/RaidDemoApp.Matching.cs`: 원본 슬롯·코디·반응형 배치
- `UnityClient/Assets/Scripts/RaidDemoApp.Rewards.cs`: 결과 애니메이션·음악
- `Packages/com.projectdawn.combat/Runtime/RaidRewardPresentation.cs`: 공통 결과 시간
- `Tools/ImportEpicRewardFlash.py`: 선택된 에픽 광선 20프레임 임포트
- `Artifacts/result-direction-20261022/sfx/index.html`: 바칼 UI 효과음 후보 8개

4인 검증은 동일 PC의 실제 Unity 프로세스 4개와 로컬 MySQL/TCP 서버, 편도 100ms 지연 프록시를 사용한다. 테스트 시간을 줄이기 위해 보스 HP를 낮춘 전용 fixture를 사용하며 일반 난이도 플레이 검증과 구분한다. AWS 테스트는 하지 않았다.


## 원본 카운트다운과 작전 시작 (v21)

`bakal_reward/countdown.img` 0~9=3, 10~19=2, 20~29=1을 추출했다. `bakal_message.img` 7번에서 실제 **작전 시작** 문구를 확인했고 0/1/2번 배너 부품을 함께 쓴다. 숫자는 원본 150×150 캔버스와 앵커를 유지한다. 원본 출처/해시는 `Tools/bakal-countdown-provenance.json`, 추출 재현은 `Tools/ImportBakalCountdown.py`에 있다. 원본 ANI 시간표나 네트워크 코드를 복원한 것은 아니다.

사용자의 최종 지정 위치는 **매칭 대기 화면, 씬 전환 전**이다. 서버의 매칭 완료 패킷을 받은 뒤 숫자 하나당 1초(3→2→1), 마지막 1초는 원본 작전 시작 배너를 기존 캐릭터 슬롯 위에 표시한다. 이후 서버가 보낸 씬 이동 요청을 처리해 로딩 화면으로 넘어간다. 던전에서는 기존 14초 조우와 BGM을 즉시 시작한다.

카운트는 로컬 표시 시계이며 피해 판정을 결정하지 않는다. `S_SceneMove`는 카운트 동안 보관하고, 4초가 지난 뒤 처리한다. 서버는 모든 참가자의 `C_SceneReady`를 받아야 전투를 시작하므로 클라이언트가 표시 중인 동안 전투가 먼저 진행되지 않는다. 음원 디코딩/화면 리소스는 매칭 요청과 씬 준비 완료 전에 미리 준비한다. 조우 보호 구간은 기존 `EncounterLandingTick=708`, `EncounterIntroTicks=840`을 유지한다. 방장 계산·서버 검증 구조도 유지한다.

새 결과 카운트/공개 시간표를 구버전 클라이언트가 다르게 해석하지 않도록 규칙 ID를 `dawn-raid-countdown-v21`로 변경했다. 클라이언트와 서버를 함께 빌드해야 한다. AWS에는 배포하지 않았다.


## 최종 검증 및 빌드

- 최종 빌드: `Build/ProjectDawnRaid.exe` / `Logs/build-matching-countdown-final.log`. Unity 빌드 오류 0, 경고 0.
- 조우·전환·서버 권위 검사 53개, 시네마틱/결과 시간 정책 검사 54개 통과.
- 최종 4인: `Artifacts/rewards-4p-20261022-060356` (편도 100ms, 실제 Unity/MySQL/TCP).
- 매칭 숫자 3·2·1·시작 문구를 모두 표시, 약 4초 뒤 씬 전환, 기존 조우 동안 전투 정지 및 서버 동일 착지/해제 tick: 8개 검사 통과.
- 영상 이후 결과 시작·음악 전환·카운트 효과음·카드 unlock→open 순서·1.8초 간격·마지막 공개 후 15초 복귀: 8개 검사 통과.
- 4인 중 3명 투표로 영상 스킵, 선행 보상 노출 없음, 정상 복귀: 9개 검사 통과.
- 실제 MySQL 보상·장비 소유·중복 지급 방지·코디 유지: 12개 검사 통과.
- 자연 영상 종료 경로는 `Artifacts/rewards-4p-20261022-060037`에서 검사했다. 해당 실행의 입장 카운트 위치는 변경 전이며 최종 위치는 060356 실행을 기준으로 한다.
- 실제 HP 피해 잔상은 1.0에서 0.1615로 줄어드는 프레임을 확인했다. `Artifacts/hud-matching-20261022/boss-damage-trail-check.json` 참조. 이것은 HP를 낮춘 검증용 fixture의 큰 피해 변화다.
- 음악 첫 재생 지연은 LoadAudioData 적용 후 영상 종료→결과 시작 로그 간격 0~1ms로 줄었다. 프레임 표시 정밀도와 구분한다.
- 화면 모음: `Artifacts/hud-matching-20261022/index.html`.
- 녹화: `Artifacts/rewards-4p-20261022-060356/video/four-player-with-audio.mp4`. 실제 로컬 0번 플레이어 오디오와 네 화면을 UTC로 맞춘 영상. 매칭 카운트는 별도 PNG 증거(각 클라이언트 matching-count-0~3.png)이며 이 녹화는 던전 입장부터다.
- 로컬 직접 실행: 루트의 `Start-LocalGame.ps1`. AWS 및 원격 배포본은 변경하지 않았다.
