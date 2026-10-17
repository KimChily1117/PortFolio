# ASTER 0.2.0 검증 기록

> 아래는 2026-10-14 방치형 시안의 과거 기록입니다. 현재 4인 수동 턴·원작 리소스 통합 버전은 [최종 검증 기록](FINAL_VALIDATION.md)을 기준으로 합니다.

검증일: 2026-10-14. 소스 프로젝트: `E:\GItHub\Cocos2dxPortfolio`.

## 빌드와 자동 테스트

- Windows: Visual Studio 2022 MSVC 19.38, Win32 Release 빌드 성공.
- Android: Gradle `:app:assembleDebug` 성공. 패키지 `com.portfolio.asteridle`, versionCode 2, versionName 0.2.0, arm64-v8a.
- CTest: `game_rules`, `save_roundtrip` 2개 모두 통과.
- 전투/경제/진행도: 679회 검증. 결정적 전투 재현, HP 범위, 승패, 상한, 강화 비용, 중복 정산, 방치 상한, 시간 역행, 잠긴 스테이지, 3-1 선택, 과거 스테이지 재도전 처리 포함.
- 저장: 왕복 직렬화, 기존 파일 교체, 손상 JSON, 지원하지 않는 버전, 값 범위 관련 8회 검증.
- Python bootstrap 구문 확인 및 고정 엔진 commit 확인 성공.

## Android 실기기

Galaxy S8+ SM-G955N, Android 9에서 설치와 Activity 실행이 성공했습니다. 초기에 가로 버전의 streamed update가 기기 검증에서 오래 대기하여 해당 설치 세션을 취소한 뒤 일반 파일 전송 설치로 재시도했습니다. 보안 설정은 변경하지 않았습니다.

업데이트 전의 1,525 G, 최고 클리어 7, 영웅 레벨 1/1/1이 0.2.0에서 유지된 것을 앱 전용 저장 파일로 확인했습니다. 가로 월드 선택 화면은 2-1과 2-2가 클리어 상태, 2-3이 다음 스테이지, 2-4/2-5가 잠긴 상태로 표시됐습니다.

전투 화면에서 영웅 공격 프레임, 피해 숫자, HP·행동 게이지, 스킬 재사용 대기시간을 관찰했습니다. 이전 월드 재도전 후에도 기존 최고 진행도가 유지됩니다. 확인 시점의 AndroidRuntime/libc 로그에 앱 충돌이 없었습니다.

스크린샷은 `images/world-map.png`와 `images/battle.png`입니다. Windows는 컴파일과 코어 테스트를 검증했으며, 사용자가 PC 화면 자동 조작을 Esc로 중단해 Windows UI 자동 점검은 진행하지 않았습니다.

## 범위

장시간 발열·프레임 타임·메모리 프로파일링과 다양한 화면/OS 기기 매트릭스는 아직 수행하지 않았습니다. 현재 결과를 모든 Android 기기나 스토어 배포 호환성으로 확대하지 않습니다.
