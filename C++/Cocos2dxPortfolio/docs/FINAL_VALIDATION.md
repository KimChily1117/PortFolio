# 2026-10-15 통합 검증 기록

대상은 4인 수동 턴 전투와 로컬 원작 리소스를 연결한 현재 Cocos2d-x 프로젝트다. 이전 ASTER 방치형 빌드의 검증 기록과 구분한다. 원작 실행 화면과의 픽셀 비교나 서버 규칙 검증은 이 결과에 포함하지 않는다.

## 자동 검증

| 항목 | 결과 | 의미 |
|---|---|---|
| Windows Win32 Release | 빌드 성공 | 실제 Cocos 렌더러·Spine·WebP·오디오 링크 |
| CTest | 7/7 통과 | 전투 규칙, 스킬 시계, 저장, WebP, 레거시 포즈, 2개 Spine fixture |
| Python 스킬 컴파일러 | 6/6 통과 | 원본 cue 해석·호환·제한 처리 회귀 |
| Spine scale 어댑터 | 1/1 통과 | 생략 scale=1, 명시적 scale=0 보존 |
| S3 의존 모델 | 21/21 통과 | 실제 번들 Spine 런타임으로 동작·메시 좌표 검사 |
| WebP 컷인 전체 | 427/427 통과 | 실제 프로젝트 디코더로 프레임 수·시간·크기 검사 |
| 전체 음원 | 29,003/29,003 통과 | Ogg 헤더·채널·샘플레이트·EOS·정확한 최종 샘플 수 |
| S3 음향 단계 | 17/17 통과 | 원본 샘플명, 시각 cue 참조·해시·순서·연출 범위 |

오디오 검증은 무손실 변환을 뜻하지 않는다. Ogg Vorbis q3 전달본이며 원본 PCM은 별도로 보존했다. 숫자 시점은 원본 시각 cue에서 취했지만, 어떤 소리를 그 cue에 연결할지는 재구현한 연결이다. 원본 FMOD 이벤트 타이밍을 복원했다는 주장은 하지 않는다.

## Windows 실제 렌더러와 입력

테스트 실행은 숨긴 GLFW 창과 별도 저장 디렉터리를 사용한다. 실제 Cocos 입력 dispatcher와 OpenGL 렌더러를 거치며 결과 PNG와 진단 TXT를 함께 보존한다.

| 시나리오 | 관찰 결과 | 결과 폴더 |
|---|---|---|
| 수동 입력 대기 | 52초 후에도 turns=0, active=3, manual=1. HP·행동 순서 유지 | `captures/turn-manual` |
| 대상 검증 | S1 선택→아군 터치 시 turns=0, 적 터치 후 turns=1 | `captures/turn-input/input-*.txt` |
| 수동 S3 입력 | 실제 우측 S3 버튼을 눌러 3개 연출/3개 표시 콜백, 이후 적 차례 진행 | `captures/turn-input` |
| 자동 전투 | 4개 S3/4개 표시 콜백 + 일반 행동 6개/타격 6회, 10행동 후 승리. 적 행동 3개, 종료 시 배우 전원 제자리 | `captures/turn-auto` |
| BGM 연속 재생 | 최종 자동 전투 52초·일시정지 후 50.8초 재생 위치에서 PLAYING 유지 | 위 전투 오디오 진단 TXT |
| 전장 복구 | 모든 연출 후 stage=(0,0,1,1), 배경 표시, 적 그룹=(0,0) | 위 전투 진단 TXT |
| 스킵 | FIRE 이전 스킵: 타격 콜백 0, 완료 1, 남은 사운드 단계 취소 | `captures/skip` |
| 일시정지·재개 | 멈춘 동안 시각 시계·오디오 시계·재생 위치 불변, 재개 후 완료 | `captures/pause` |
| 일시정지 중 Escape | 실제 키 이벤트로 정지→Escape. 입력 리스너는 유지하고 연출 시계만 정지. 해제 후 4개 S3·승리 완료, paused=0 | `captures/turn-paused-skip` |
| 화면 | 로비, 지도, 4인 편성, 여성 영웅 목록, 움직이는 상세, 음원 29,003개 라이브러리 | 해당 이름의 `captures` 폴더 |
| 기본 공격 | 17프레임에서 준비·접근·공격·복귀, 타격 전 HP 유지/이후 모델 HP 일치, 적 4회 공격 확인 | `captures/action-basic` |
| 기본 공격 2배속 | 17프레임, 행동 21개·적 공격 9개 시작. 타격 단일 적용·접근·복귀 확인 | `captures/action-fast` |
| 평타 중 메뉴 정지 | 17프레임, 정지 전후 행동 시계·위치·HP·오디오 시계 일치, 재개 후 적 4회 공격 | `captures/action-pause` |
| S3 연출 OFF | 일반 행동 10개/타격 10회·적 행동 3개 후 승리. 전원 제자리, 지연 음향 0개, BGM 재생 유지 | `captures/action-no-cutscenes` |

자동 전투는 테스트를 위한 명시적 옵션이다. 일반 실행은 수동 입력을 기다리며 승패 후 자동으로 다음 전투를 시작하지 않는다. 캡처 시각은 wall-clock FPS 측정치가 아니므로 성능 벤치마크로 해석하지 않는다.

중간 검증에서 Windows 스트리밍 음악이 사라진 현상을 발견했다. 번들 Cocos의 버퍼 소진 처리 경로를 조사하고, 반복 음악의 비정상 완료 시 다음 스케줄러 tick에서 복구하도록 했다. 최종 정상 재생에서는 복구 횟수가 0이므로 비정상 완료 분기의 실제 발동까지 검증한 것으로 적지는 않는다. [오디오 분석](analysis/AUDIO_PIPELINE_ANALYSIS.md)에 원인 추론과 확인 범위를 분리했다.

추가 평타 검증에서는 메뉴를 열 때 아직 INITIALIZING 상태였던 UI 효과음이 비동기 로딩 완료 뒤 재생되는 문제가 확인되었다. 정지 중 PLAYING으로 바뀌는 음원도 매 tick 정지하도록 보완했다. 수정 후에는 메뉴 정지 전후 4개 음원의 재생 위치와 상태가 동일했다. `tools/validate-battle-actions.py`가 일반 행동 51개 캡처의 타격 시점·HP·중복 적용·적 접근과 자동 전투 완료 후 위치 복구를 검사한다. 원본 공격/피격 클립과 호스트 이동·이펙트의 구분은 [기본 공격 분석](analysis/BASIC_ATTACK_ANALYSIS.md)에 기록한다.

## 저장 파일 검증

v1의 골드·레벨·스테이지와 이미 적립된 pendingGold만 v2로 1회 이전한다. 더 이상 시간 경과로 보상을 계산하지 않는다. 원본 v1 백업을 먼저 원자적으로 발행하며, 파일이 없는 경우와 열기 실패를 구분한다.

실제 Windows 파일 공유 잠금, 디렉터리 경로, 손상 JSON, 지원하지 않는 버전, 불완전하거나 의미상 잘못된 백업, 임시 파일 쓰기 실패, 기존의 유효한 다른 v1 백업 보존을 회귀 검사했다. 읽기/백업 실패 후 새 기본값으로 덮어쓰는 동작을 막았다.

첫 Android 설치 검증에서는 하드 링크를 통한 백업 발행이 실패했다. 기존 v1 파일과 임시 백업은 보존되었고 저장 차단 안내가 표시되었다. Android 전용 발행을 `renameat2(RENAME_NOREPLACE)` 시스템 호출로 바꿨다. API 30부터 제공되는 libc wrapper를 호출하지 않아 Android 9에서도 빌드되며, 대상이 이미 존재하면 덮어쓰지 않는다. 저장 호환 수정본 설치에서 v2 골드 3,296·스테이지 8·최고 클리어 7·승리 14·레벨 4개를 확인했고, 원본 v1 148바이트와 동일한 백업도 확인했다. 후속 실행에서 적립금을 중복 합산하지 않는다.

## Android

Galaxy S8+ SM-G955N(Android 9 / API 28)에 같은 패키지로 업데이트 설치했다. 최종 APK는 2,571,368,020바이트다. 전달본의 SHA-256은 `511c76a1bcaa30cc2f67ec06979e8076a5c6e260ad6a693614fcc6bbd6d5906c`다. 35,599개 리소스의 경로·크기·CRC와 전체 ZIP CRC를 검증했다. 설치된 APK와의 대조·서명·기기 동작 검증은 아래 Android 배포 기록에 구분해 기록한다.

기존 2-3 전투에서 자동 전투 승리를 확인했다. 보상 275골드가 한 번 지급되어 골드 3,296→3,571, 스테이지 8→9, 최고 클리어 7→8, 승리 14→15로 저장되었다. 실제 S3 cue 실행과 OpenSL 음원 재생도 확인했다. 시작 시 `getActiveAttributes`의 GL 0x0501 경고가 한 번 기록되었으므로 GL 경고가 전혀 없었다고 주장하지 않는다. 해당 실행에서 S3 셰이더 컴파일·링크 실패는 관찰되지 않았다.

공격 모션 검증본에서는 아군·적 일반 행동 31개의 타격·완료 로그를 확인했고, 25초 화면 녹화에서 클라릿사의 접근 공격과 적의 공격 예고·접근을 확인했다. 오우거가 도미니엘 앞에 접근한 상태로 메뉴를 열었을 때 4초 간격의 정지 화면 SHA-256이 일치했다. 영상에는 녹음이 없다. 이후 S3 연출 OFF의 지연 음향 취소까지 포함한 최종 전달본을 다시 설치했으며, 기기 APK의 SHA-256도 위 전달본과 일치했다. Home 후 재개와 골드 3,571·스테이지 9 보존을 확인했다. 영상·장시간 행동 검증본과 최종 설치본의 구분은 Android 기록에 남긴다.

최종 기기 검사에서 사운드 라이브러리의 개별 음원 재생 버튼은 자동 터치 후 재생 상태를 확인하지 못했다. 라이브러리 표시·재개 및 앞선 전투 음원 백엔드 재생 확인과 구분하며, 개별 음원 재생까지 통과했다고 적지 않는다.

수동 입력 대기, 앱 재개, 화면 캡처와 세부 기기 검증은 [Android 배포 기록](analysis/ANDROID_DEPLOYMENT.md)에 기록한다. 데스크톱 결과만으로 Android 성공을 대체하지 않는다.

## 재현 명령

```powershell
powershell -ExecutionPolicy Bypass -File tools/build-windows.ps1
python tools/test-epicseven-skills.py
python -m unittest discover -s tests -p test_spine_adapter.py
python tools/test-epicseven-audio.py
./tools/smoke-reference.ps1 -Mode battle -Manual -OutputDirectory work/manual
./tools/smoke-reference.ps1 -Mode battle -DriveInput -OutputDirectory work/input
./tools/smoke-reference.ps1 -Mode battle -Pause -OutputDirectory work/paused-skip
./tools/smoke-reference.ps1 -Mode battle -Basic -OutputDirectory work/actions/captures/action-basic
./tools/smoke-reference.ps1 -Mode battle -Basic -Fast -OutputDirectory work/actions/captures/action-fast
./tools/smoke-reference.ps1 -Mode battle -Basic -Pause -OutputDirectory work/actions/captures/action-pause
./tools/smoke-reference.ps1 -Mode battle -OutputDirectory work/actions/captures/turn-auto
./tools/smoke-reference.ps1 -Mode battle -NoCutscenes -OutputDirectory work/actions/captures/action-no-cutscenes
python tools/validate-battle-actions.py work/actions
./tools/smoke-reference.ps1 -Mode skill -Item c1017.s3 -Pause -OutputDirectory work/pause
```

기존 3인/방치형 캡처는 최종 산출물에서 제외했다. 모든 영웅과 전체 264개 S3의 재현을 검증한 결과는 아니다. 현재 전투에 연결된 복합 S3는 4개이며, 427개 짧은 WebP 컷인은 별도의 자료다.
