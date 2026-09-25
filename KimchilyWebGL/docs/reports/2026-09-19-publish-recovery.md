# 월드 게시 연결 복구와 실행기 수정 — 2026-09-19

사용자 월드의 Android 콘텐츠 빌드는 성공했으며, `Publish failed`의 직접 원인은 로컬 게시 서버 `127.0.0.1:8787`에 연결할 수 없었던 것이다. 포트 점유를 해소한 뒤 **이미 만들어진 빌드 폴더를 그대로 게시**했고 실제 QR 해독과 Android 링크 파서 검증을 통과했다. 사용자 씬·모델·TypeScript 파일은 변경하지 않았다.

이후 휴대전화 입장에서 별도의 TypeScript 초기화 시간 초과가 확인됐다. 초기화와 일반 실행의 시간 제한을 분리했으며, 관리 코드 VM 29/29와 실제 Unity PlayMode 54/54 검사를 통과했다. **수정 런타임을 포함한 새 APK를 SM-G955N / Android 9에 설치해 같은 게시물의 입장·두 모델 표시·퇴장·동일 프로세스 재입장을 확인했다.** 게시 연결과 앱 실행을 각각 검증했으며 [통합 검증 JSON](../../KimchilyAndroid/Artifacts/publish-recovery-verification-20260919.json)에 결과를 기록했다.

## 장애 원인과 복구 범위

| 단계 | 확인한 사실 | 처리 |
|---|---|---|
| 사용자 Editor의 월드 빌드 | 모델을 포함한 Android 월드 빌드 성공 | 기존 산출물 재사용 |
| 게시 HTTP 요청 | HTTP 응답 코드 0, `127.0.0.1:8787` 연결 실패 | 게시 서버 상태·포트 점유 조사 |
| 로컬 서버 시작 | ADB 서버가 8787을 `Bound` 상태로 점유했고 Python bind가 `WinError 10013`으로 실패 | 점유 주체로 확인된 ADB 서버만 종료한 뒤 게시 서버 복구 |
| 복구된 서버 | Python 게시 서버 PID 1748의 `/health` 정상 | 기존 빌드 게시 및 QR 검증 |
| 수정 전 첫 휴대전화 입장 | `Character` 초기화에서 `TypeScript execution budget exceeded` | 초기화 5초·lifecycle 100ms로 분리, 실행 단계 진단 추가 |
| 수정 APK 입장·재입장 | 두 번의 TypeScript Start, 모델 표시, 오류 overlay 없음 | 같은 게시물을 같은 앱 프로세스에서 재검증 |

HTTP 0은 서버가 반환한 HTTP 상태 코드가 아니라 요청이 정상 응답을 받지 못한 결과다. 이번에는 서버 연결 장애로 확인됐다. ADB의 포트 점유를 모든 게시 실패의 일반 원인으로 가정하지 않는다. 포트 진단에서 PID·프로세스명만 기록하며 인증 토큰이나 전체 프로세스 명령줄을 출력하지 않는다.

## 복구된 게시물

| 항목 | 값 |
|---|---|
| World ID | `sample-world` |
| 기존 빌드·게시 revision | `20260919T085157020Z-bcda5d79` |
| Manifest SHA-256 | `da47bf7733f113cf01d2d4c7aac570cfb38a94b88c6ae8b2b36a0ac3037eea45` |
| 게시 페이지 | [월드·QR 열기](http://192.168.0.4:8787/w/sample-world/20260919T085157020Z-bcda5d79) |
| 게시 응답 | [publish-recovered-20260919.json](../../KimchilyCreator/Artifacts/publish-recovered-20260919.json) |
| 실제 QR | [PNG](../../KimchilyAndroid/Artifacts/qr-publish-recovered-20260919.png), [해독·파서 검사 JSON](../../KimchilyAndroid/Artifacts/qr-publish-recovered-20260919-check.json) |

ZXing core 3.5.3 기본 검출(`tryHarder=false`)로 PNG를 해독해 응답의 `launchUrl`과 전체 문자열이 일치함을 확인했다. 실제 Android `WorldLink` 파서도 같은 world ID·revision·manifest URL·SHA를 반환했다. **물리 카메라로 QR을 촬영한 검사는 아니다.** 주소는 개발 PC의 LAN 주소이며 PC와 휴대전화가 해당 서버에 접속할 수 있어야 한다.

## 적용한 수정

### 게시 서버 시작 도구

[start.ps1](../../KimchilyPublish/tools/start.ps1)은 프로세스를 만들었다는 사실만으로 성공을 알리지 않는다. 최대 10초 동안 자식 프로세스 종료 여부와 로컬 `/health`의 서비스명·schema·정상 상태를 확인하고, 준비가 확인된 뒤에만 성공 메시지와 PID 파일을 기록한다.

- 시작 실패·준비 시간 초과는 로그 경로를 포함한 오류로 알린다. 로그는 `KimchilyPublish/.local/server.log`와 `server-error.log`다.
- 같은 옵션의 재실행은 등록 PID의 실제 서버 경로·설정과 health를 확인한 뒤 실행 중인 서버를 재사용한다.
- `BindAddress`, `Port`, `PublicBaseUrl`이 기존 프로세스와 다르면 요청 설정이 적용되지 않았음을 명확히 알린다.
- 포트의 `Listen`·`Bound` 점유를 확인하되 다른 프로세스를 자동 종료하지 않는다. 준비 시간 초과 시에도 남은 프로세스의 PID를 안내한다.
- 토큰 값을 출력하지 않는다. public origin은 credentials·경로·query·fragment 없는 절대 HTTP(S) origin만 허용한다.

PowerShell 구문 파싱을 통과했고, 아래 명령으로 **이미 정상인 PID 1748을 재사용**하는 경로를 실제 확인했다. 이 검증에서 추가 서버를 만들거나 프로세스를 종료하지 않았다. 실패한 실제 bind를 다시 유발하는 검사는 수행하지 않았다.

```powershell
# 워크스페이스 루트. LAN 주소는 현재 개발 PC 주소에 맞춘다.
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyPublish\tools\start.ps1 -BindAddress 0.0.0.0 -Port 8787 -PublicBaseUrl http://192.168.0.4:8787
```

Unity Creator의 게시 서버 주소는 PC에서 접속하는 `http://127.0.0.1:8787`이고, QR에 담기는 public origin은 휴대전화에서 접속하는 `http://192.168.0.4:8787`이다. 사용법은 [게시 서버 README](../../KimchilyPublish/README.md)를 따른다.

### Creator 창과 연결 실패 메시지

[CreatorWindow](../../KimchilySDK/Packages/com.kimchily.creator/Editor/CreatorWindow.cs)의 작업은 `OnGUI` 내부에서 즉시 빌드·게시하지 않고 지연 큐로 실행한다. 대기·실행 중 중복 요청을 차단하고 창이 닫히면 대기 요청을 취소한다. 작업에서 사용할 타깃을 보존하며 닫힌 창으로 후속 알림이 돌아가지 않게 처리했다.

새 [큐 회귀 검사 5개](../../KimchilySDK/Packages/com.kimchily.creator/Tests/Editor/CreatorWindowActionQueueTests.cs)는 지연 실행, 중복 요청, 닫기 취소, 오류 후 명시적 재시도, 실행 중 창 닫기를 다룬다. 기존 7개와 함께 **실제 Unity Core SDK EditMode 12/12**를 통과했다. [결과 XML](../../KimchilySDK/Artifacts/editmode.xml)의 실행 시각은 2026-09-19 08:59:43–44 UTC다. 수정 후 GUI 버튼을 실제 클릭하는 재검증은 별도로 수행하지 않았다.

[WorldPublisherClient](../../KimchilySDK/Packages/com.kimchily.creator/Editor/Publishing/WorldPublisherClient.cs)는 HTTP 0 연결 실패에서 서버 실행 상태와 주소를 확인할 수 있도록 오류 안내를 보완했다.

### TypeScript 초기화와 일반 실행 제한 분리

복구 게시물의 첫 실기기 입장에서 `Character` 초기화가 기존 100ms 시간 제한에 걸렸다. [실기기 로그](../../KimchilyAndroid/Artifacts/device-publish-recovered-20260919.log)의 17:58:49 오류가 근거다. 이는 게시·다운로드 후 스크립트를 초기화하는 단계의 실패다.

[TypeScriptVm](../../KimchilySDK/Packages/com.kimchily.typescript/Runtime/TypeScriptVm.cs)은 bootstrap·모듈 로드·인스턴스 생성 등 초기화에 5초를 허용하고 lifecycle 실행은 100ms를 유지한다. 기존 명령 수 제한과 외곽 호출 단위의 예산은 유지하므로 상대 모듈 로드나 getter 호출로 예산이 새로 충전되지 않는다. 오류 진단에는 실행 단계와 초과한 제한을 구분할 정보를 추가했다.

관리 코드 VM **29/29**와 실제 Unity Runtime PlayMode **54/54**를 통과했다. [PlayMode XML](../../KimchilyUnityRuntime/Artifacts/runtime-playmode.xml)은 2026-09-19 09:07:03–04 UTC, 실패·skip 0이다. 초기화 시간 분리는 Android IL2CPP 앱에 포함되는 실행기 변경이므로 새 APK로 검증했다. 기존 콘텐츠 빌드는 그대로 사용했다.

## 검증 상태와 후속 기록

| 검사 | 상태·증거 |
|---|---|
| 기존 빌드 게시 | 완료; 위 게시 응답·revision·manifest SHA |
| QR 기본 해독·Android 링크 파싱 | 완료; 위 QR 검사 JSON |
| 서버 시작 스크립트 | 구문 파싱·정상 서버 재사용 확인; 실제 실패 bind 재현은 미수행 |
| Core SDK Unity EditMode | 12/12 통과(기존 7 + 새 큐 5) |
| TypeScript 관리 코드 VM | 29/29 통과 |
| Unity Runtime PlayMode | 54/54 통과, 실패·skip 0 |
| 수정된 Creator GUI 버튼 실제 클릭 | 미검증; 큐 로직의 Unity EditMode 검사와 구분 |
| 수정 런타임 Android export·통합 APK | 완료; Gradle 49초·71개 작업 중 13 실행/58 up-to-date. 기존 네이티브 단위 테스트 26개 작업은 `UP-TO-DATE`여서 이번에 새로 실행했다고 주장하지 않음 |
| 새 APK 설치·같은 게시물 재입장 | 완료; SM-G955N / Android 9, 18:10:22 KST 설치, 같은 PID 7098에서 Start 두 번 |
| 실기기 모델 표시·TypeScript 시작·퇴장 | 완료; 두 모델 표시, 오류 overlay 없음, 홈 복귀·다운로드 캐시 비어 있음 |
| 이번 모델 회전·코루틴 on/off·재질 충실도 | 별도 검증하지 않음 |

## 새 APK와 실기기 증거

- APK: [kimchily-unity-publish-fixed.apk](../../KimchilyAndroid/Artifacts/kimchily-unity-publish-fixed.apk), **38,277,793바이트**, SHA-256 **`3784067bbf698178a20468ca394ddad946a5a7ad25c59dbf2f8aea966edf3197`**. 버전은 0.1.0, 설치 갱신 시각은 **2026-09-19 18:10:22 KST**다.
- 첫 입장: **18:10:34.577**, PID **7098**에서 `Published TypeScript world started: revision 2`가 기록됐다. [실행 로그](../../KimchilyAndroid/Artifacts/device-publish-fixed-20260919.log)·[화면](../../KimchilyAndroid/Artifacts/device-publish-fixed-20260919.png)·[UI](../../KimchilyAndroid/Artifacts/device-publish-fixed-20260919-ui.xml)에서 금색 캐릭터와 하얀색 FBX 표시를 확인했다. 화면에 오류 overlay는 없고 Unity 화면과 나가기 버튼이 표시됐다.
- 내려받은 콘텐츠: [기기 manifest](../../KimchilyAndroid/Artifacts/device-publish-fixed-20260919-world.json)의 실제 SHA-256이 게시 응답의 `da47bf…eea45`와 일치했다.
- 퇴장: 나가기 버튼 후 [네이티브 홈 UI](../../KimchilyAndroid/Artifacts/device-publish-fixed-20260919-exit-ui.xml) 복귀와 `KimchilyWorldDownloads`의 `ls -A` 결과가 비어 있음을 확인했다.
- 재입장: APK 재설치 없이 같은 PID **7098**에서 **18:11:40.090**에 다시 Start를 기록했다. [재입장 로그](../../KimchilyAndroid/Artifacts/device-publish-fixed-20260919-reentry.log)·[UI](../../KimchilyAndroid/Artifacts/device-publish-fixed-20260919-reentry-ui.xml)에 초기화 budget 오류·`SCRIPT_INIT`·Exception·FATAL과 오류 overlay가 없었다.
- 서버: 같은 설정으로 시작 스크립트를 다시 호출해 `/health` 정상과 기존 PID **1748** 재사용을 최종 확인했다.

로그의 `revision 2`는 기존 TypeScript 파일에 들어 있던 시작 메시지다. 실제 게시 revision ID는 위의 `20260919T085157020Z-bcda5d79`이며 이번 복구에서 스크립트 메시지를 바꾸지 않았다. 물리 카메라 스캔 대신 **실제 PNG의 ZXing 기본 해독과 같은 앱 링크의 ADB 실행**을 사용했다. 화면의 모델 존재는 확인했지만 이번에는 애니메이션·코루틴 on/off나 재질 충실도를 별도 검증하지 않았다.

이전 새벽 TypeScript revision 1→2 실기기 성공 기록은 [기존 보고서](2026-09-19-typescript-runtime.md)에 남긴다. 당시 APK는 [kimchily-unity-typescript-initial.apk](../../KimchilyAndroid/Artifacts/kimchily-unity-typescript-initial.apk)로 별도 보존했으며 **38,284,262바이트**, SHA-256 **`B957A39C117B2B2FAB7C8033C3014CF6334029667F4FE2BA74D725FC2FD3C70B`**다. 이번 복구용 APK와 당시 실행 증거를 혼동하지 않는다.
