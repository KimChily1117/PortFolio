# 서버 관리와 모바일 기본 조작

## 적용 방향

사용자 선택에 따라 **기본 플레이어 + 원하는 FBX 연결**, **가로 기본 + 세로 선택**으로 구현했다. 서버는 Unity 게시 창의 켜기·상태 확인·끄기 버튼으로 관리하고 주소·토큰을 자동 연결한다. 전체 사용 순서는 [모바일 월드 안내](../mobile-world-guide.md)에 있다.

## 서버

- `KimchilyPublish/tools/start.ps1`: 활성 물리 LAN IPv4 선택, 같은 서버 재사용, 준비 확인 후 결과 반환.
- `status.ps1`: 실제 프로세스와 HTTP health를 다시 조회한다.
- `stop.ps1`: PID뿐 아니라 Python·이 프로젝트의 실행 파일·데이터 폴더·설정·프로세스 시작 시각을 확인하고 해당 서버만 종료한다. 게시 파일과 토큰은 남긴다.
- `connection.json`: 상태와 주소를 저장하며 토큰은 포함하지 않는다.
- Windows WMI를 통해 서버를 분리 실행한다. 검증 중 발견된 자식 프로세스의 stdout 파이프 상속으로 CLI/Unity 창이 끝없이 대기하는 문제를 해결했다. Python 실행 helper가 로그를 파일에 기록한다.
- Unity `LocalPublisherController`는 비동기로 명령을 실행하고 세션 메모리에만 토큰을 둔다. 주소를 바꾸면 토큰을 비우며, 자동 연결한 자격 증명의 목적지를 게시 직전에 다시 검증한다. 창을 닫아도 서버는 계속 실행된다.

[분리 서버 회귀 결과](../../KimchilyPublish/Artifacts/server-management/c225916484d04e5e9e1111a9b60535fe/results.json): **21/21 통과**. 실제 시작·재사용·상태·종료·재시작, 포트 충돌, 다른 프로세스로 재사용된 PID, 변조된 시작 시각·상태 파일 거절을 검증했다. 테스트 서버는 종료했고 기존 개발 서버 PID 1748은 정상 유지했다. 기본 옵션 실행에서도 `192.168.0.4`를 자동 선택하고 같은 서버를 재사용했다. 최종 상태 확인 시각 `2026-09-19 23:13:41`에도 PID 1748의 health가 정상이었다.

## 화면 방향

Android 네이티브 Activity가 방향 선택을 소유한다. 첫 사용은 가로이며 상단 메뉴에서 가로·세로·자동을 선택하고 저장한다. `configChanges`와 Unity의 방향 변경 콜백으로 기존 UnityPlayer를 유지하며 월드를 다시 열지 않는다. 홈은 자동 회전을 지원하고 스크롤로 짧은 화면에 대응한다. 상단 버튼에 시스템·화면 잘림 영역 여백을 적용했다.

Kotlin 통합 컴파일과 Android 단위 테스트 **31/31 통과**. 화면 방향 테스트 5개를 포함한다.

## 플레이어

UPM Core의 `Runtime/Mobile`에 CharacterController 이동·중력·점프, 조이스틱·카메라 드래그·점프 버튼, 추적 카메라를 추가했다. 서로 다른 손가락을 구분하며 회전·일시 정지·포커스 상실·비활성화에서 입력을 초기화한다. 화면 안전 영역에 맞춰 조작판을 배치하고, 바닥 아래로 떨어지면 시작 위치로 돌아온다.

`KimchilyMobilePlayerBootstrap.EnsureForScene`은 제작자가 배치한 플레이어를 우선 사용하고 없으면 씬 안에 기본 플레이어를 만든다. 퇴장 시 해당 씬과 함께 제거된다. 모델은 자식 복제본으로 맞추고 원본 모델의 위치·크기·Collider를 바꾸지 않는다. 자기 플레이어 또는 플레이어를 포함한 모델의 재귀 복제를 차단하고 게시 전에도 검증한다.

에디터 메뉴는 `Kimchily > World > Add Mobile Player`, `Use Selected Model for Mobile Player`다. Inspector의 `Model Prefab`에도 모델을 연결할 수 있다. 기본 캡슐은 애니메이션 없는 이동 플레이어이며, 내장 샘플은 FBX 모델을 연결한 플레이어를 제공한다. 걷기·점프 Animator 및 멀티플레이는 이번 구현 범위에 포함되지 않는다.

구형 starter의 납작한 원통 바닥에 기본 CapsuleCollider가 남아 플레이어가 떠 보이던 문제는 실행 중 호환 처리로 보정했다. 이름·기본 Cylinder 메시·위치·크기·회전·컴포넌트 구성·Collider 설정이 구형 생성 패턴과 모두 일치할 때만 MeshCollider로 교체한다. 사용자 씬 원본 파일과 이미 게시한 번들은 수정하지 않았다. 추적 카메라는 기존 활성 카메라의 배경색과 Clear Flags를 이어받는다.

## 검증과 산출물

| 검사 | 최종 결과 | 주요 범위 |
|---|---|---|
| Core Editor | **23/23 통과** | 서버 자격 증명 연결·목적지 변경 차단·null PID 처리·게시 작업 큐 |
| Runtime Play Mode | **72/72 통과** | 기존 월드·스크립트 기능, 모바일 입력·충돌·점프·모델 복제·카메라 복구·씬 정리, 구형 바닥 호환 처리 |
| Android | **31/31 통과** | 브리지 상태 전환·월드 링크·QR·화면 방향 정책 |
| 서버 관리 | **21/21 통과** | 시작·재사용·상태·종료·재시작·충돌 및 소유권 검증 |

Samsung **SM-G955N / Android 9**에 최종 APK를 설치했다. 앱은 `0.1.0`(versionCode `1`)이며 설치 기록의 lastUpdateTime은 `2026-09-19 23:06:24`다.

- 기존 게시 월드 `sample-world / 20260919T085157020Z-bcda5d79`를 그대로 열었다. manifest SHA-256은 `da47bf7733f113cf01d2d4c7aac570cfb38a94b88c6ae8b2b36a0ac3037eea45`로 유지했다.
- 가로 → 세로 → 가로 전환 동안 **PID 24412와 TypeScript Start 1회**를 유지했다. 화면 방향 변경으로 월드나 스크립트가 다시 시작되지 않았다.
- 가로에서 점프 시작 위치 `y=0.04`와 이동 거리 **0.61m**, 세로에서 점프와 이동 거리 **0.35m**를 기록했다. 구형 바닥 위에서 떠 보이던 현상도 보정된 상태로 확인했다.
- 퇴장 후 네이티브 홈 복귀와 월드 다운로드 캐시 삭제를 확인했다.
- FBX 모델을 연결한 내장 샘플도 같은 PID에서 입장·표시·조작을 확인했다. `23:13:12.073` 점프 시작 위치 `(0.78, 0.04, -0.29)`, `23:13:13.178` 이동 거리 **0.65m**, 도착 위치 `(0.32, 0.04, 0.17)`이 로그에 남았다.

최종 [Android APK](../../KimchilyAndroid/Artifacts/kimchily-unity-debug.apk)는 **38,340,908 bytes**이며 SHA-256은 다음과 같다.

```text
F46A6D358CE4A14BDE61EB888230680847014F2E105C9410229D938B05CB36A5
```

검증 증거:

- [검증 요약 JSON](../../KimchilyAndroid/Artifacts/mobile-controls-verification-20260919.json)
- [가로 화면](../../KimchilyAndroid/Artifacts/device-mobile-final.png), [세로 화면](../../KimchilyAndroid/Artifacts/device-mobile-final-portrait.png)
- [가로·세로 조작 로그](../../KimchilyAndroid/Artifacts/device-mobile-final-controls.log), [세로 전환 로그](../../KimchilyAndroid/Artifacts/device-mobile-final-portrait.log), [게시 월드 입장 로그](../../KimchilyAndroid/Artifacts/device-mobile-final.log)
- [퇴장 화면 XML](../../KimchilyAndroid/Artifacts/device-mobile-final-exit-ui.xml), [월드 정보](../../KimchilyAndroid/Artifacts/device-mobile-final-world.json)
- [FBX 내장 샘플 입장 화면](../../KimchilyAndroid/Artifacts/device-mobile-fbx-demo.png), [조작 후 화면](../../KimchilyAndroid/Artifacts/device-mobile-fbx-demo-after.png), [FBX 조작 로그](../../KimchilyAndroid/Artifacts/device-mobile-fbx-demo.log)

실기기 검증 범위는 위 Android 9 기기와 같은 LAN의 개발용 HTTP 게시 서버다. 다른 기기·OS, 인터넷 공개 HTTPS 운영, 모델별 애니메이션·멀티플레이에 대한 검증을 의미하지 않는다.

Unity 서버 버튼은 컴파일과 컨트롤러 검증을 마쳤으며, 에디터에서 직접 클릭하는 검증은 수행하지 않았다. 이번 최종 APK는 기존 링크로 입장했고 QR 카메라 재촬영은 하지 않았다. 동시 멀티터치 입력은 자동 검사로 검증했으며 기기에서 여러 손가락을 동시에 조작하는 검증은 수행하지 않았다.
