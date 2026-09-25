**Kimchily Android 앱 — 첫 개발 버전**

네이티브 Kotlin 홈에서 내장 Unity 월드를 전체 화면으로 열고, 퇴장 후 홈으로 돌아오는 프로젝트다. Android Studio에서 이 폴더를 프로젝트로 연다. 원래 Creator·World·ToolManager 프로젝트와 분리되어 있다.

앱에 포함한 `demo / builtin-v1` 샘플과, 에디터에서 게시한 월드의 QR·링크 입장을 지원한다. QR 카메라 스캔, 링크 붙여넣기, 외부 `kimchily://world` 링크가 같은 검증·입장 경로를 사용한다. 실제 콘텐츠 다운로드와 번들·스크립트 실행은 연결된 Unity 실행기가 담당한다. 로그인과 운영용 게시 서비스는 별도 단계다.

| 현재 소스의 빌드 구성 | 동작 | 결과 파일 |
|---|---|---|
| Android 단독 | 홈·QR 스캔·링크 검증. 실제 입장은 Unity 미연결 안내 | `Artifacts/kimchily-native-debug.apk` |
| Unity 포함 | 홈 → 샘플 또는 게시 월드 입장 → 전체 화면 Unity → 나가기/뒤로 가기 → 홈 | `Artifacts/kimchily-unity-debug.apk` |

두 APK는 같은 applicationId `com.kimchily.app`을 사용하므로 동시에 설치하는 별도 앱이 아니다. Unity 포함 버전으로 단독 미리보기 버전을 교체한다. APK는 개발용 debug 서명이며 통합 버전의 네이티브 ABI는 ARM64다.

**구현 범위**

월드는 가로로 시작하며 상단 메뉴에서 세로·가로·자동을 선택한다. 기존 UnityPlayer와 월드 세션을 유지한다. 기본 플레이어의 조이스틱·점프·카메라 드래그 및 FBX 연결은 [사용 안내](../docs/mobile-world-guide.md), 최신 검증은 [서버·모바일 구현 기록](../docs/reports/2026-09-19-mobile-controls-server.md)을 참고한다. 아래의 이전 APK 해시·검증 자료는 당시 기록이다.

- 네이티브 홈과 샘플 월드 입장 버튼.
- QR 카메라 스캔, 링크 붙여넣기, 외부 앱의 VIEW 링크 수신과 `onNewIntent` 처리.
- 월드 주소·revision·SHA-256 검증과 `OpenWorld.manifestUrl`·`manifestSha256` 전달.
- 다른 월드나 revision을 요청하면 CloseWorld → 일치하는 WorldClosed 이후 새 OpenWorld 전송. 정리 중 여러 링크를 받으면 마지막 요청만 입장하고, 정리 실패 시 새 월드를 열지 않는다.
- `WorldSession`에서 초기화 대기·입장·퇴장·실패 상태 관리.
- RuntimeReady 이전 요청 보관, Initialize 재시도와 시작 timeout.
- requestId·worldId·revisionId 확인, 중복 입장 및 이전 요청의 늦은 이벤트 처리.
- Android UI 스레드에서 콜백 처리.
- Back/나가기 → CloseWorld → WorldClosed 이후 홈 복귀.
- Unity Activity를 일반 퇴장 때 finish/Quit하지 않고 재사용. 홈에서 Back을 누르면 task를 배경으로 전환.
- Unity 기본 launcher를 병합 manifest에서 제거하고 네이티브 MainActivity를 유일한 앱 진입점으로 사용.

**현재 검증**

기존 `device-final-*`, `device-camera-investigation.*` 증거와 기존 APK 해시는 브랜드 변경 전 기록이다. 새 Kimchily 검증은 `device-kimchily-*`와 아래 결과를 사용한다.

Kimchily 변경 후 Android 단독·Unity 통합 구성에서 각각 **26개** 단위 테스트(링크 8·세션 14·QR 4)가 통과했다. 통합 APK는 **29,610,920바이트**, SHA-256 `0D9E0E02FB31054A11002967E02AA1499C796F35FBD05E5C54E3E4AB415BB366`이다. 실제 APK manifest의 `com.kimchily.app`·`KimchilyUnityActivity`·`kimchily` scheme과 IL2CPP metadata의 새 Runtime 어셈블리·JNI 브리지 이름을 확인했다. 상세 결과는 `Artifacts/kimchily-apk-verification.json`과 `Artifacts/rebrand-integrated-unit-tests`에 있다.

새 앱을 SM-G955N에 설치하고 최종 게시 revision `20260918T143533635Z-e5604d16`의 링크 입장·FBX 표시·Lua 실행·홈 복귀를 확인했다. 휴대전화의 manifest SHA가 게시본과 일치하고 퇴장 후 다운로드 임시 폴더가 비었다. [기기 검증 결과](Artifacts/device-kimchily-verification.json)와 [전환 기록](../docs/reports/kimchily-rebrand-2026-09-18.md)에 증거를 보존했다. 새 앱의 물리 카메라 촬영은 재실행하지 않았고 실제 게시 QR PNG의 크기·회전 검출을 별도로 확인했다.

최종 게시 revision `20260918T143533635Z-e5604d16`의 실제 QR도 기본 검출·원본/절반/1⁄4 크기·4방향 총 12개 조건에서 통과했고, 199자 링크 및 Android 파서 결과가 게시 응답과 일치했다. PNG는 `../KimchilyCreator/Artifacts/world-qr-kimchily.png`, 결과는 `Artifacts/qr-decode-kimchily-final.json`과 `Artifacts/qr-compatibility/final-summary.txt`에 있다. 새 브랜드의 실기기 실행 결과는 이 빌드·정적 검사와 별도로 기록한다.

2026-09-18 최종 통합 APK는 29,609,043바이트, ARM64·IL2CPP이며 SHA-256은 `495E910EF7661862FE1D2D9A589BE3D711D7AEFEE1DECB15293367316A8FE1FA`다. SM-G955N에 설치했고 같은 APK/프로세스 `19568`에서 게시 revision 1과 2를 순서대로 다운로드·실행했다. FBX 렌더링, Lua 회전·상하 이동·beacon 코루틴, 나가기 후 네이티브 홈 복귀와 임시 다운로드 정리를 확인했다. APK 설치 시각은 23:00:05로 콘텐츠 전환 중 바뀌지 않았다.

증거는 `Artifacts/device-final-revision-switch.log`, `Artifacts/device-final-published-world.png`에 있다. 최종 실행 로그에서 이전 MoonSharp Resources 로더 초기화 예외가 사라진 것을 확인했다. 기존 내장 샘플의 입장·퇴장·재입장도 앞선 검증에서 통과했다. Unity가 이름으로 찾는 문자열 리소스는 네이티브 호스트의 `src/unity/res`에 보존한다.

자동 검증은 Unity PlayMode **42개**, 네이티브 **25개**(링크 8·세션 14·QR 3), 게시 서버 **13개**가 통과했다. QR 검사는 실제 게시 PNG 두 개의 기본·축소·회전 검출과 링크 내용을 확인한다. manifest 경로·해시·release HTTP 거절, 다운로드 취소·오류 복구와 revision 변경 시 정리 순서도 자동 테스트에 포함한다. `device.ps1 -Action Install`은 실제 APK manifest를 검사해 QR 이전 버전을 거절한다.

카메라 촬영도 SM-G955N에서 확인했다. 23:03:53 QR 인식, 23:03:54 revision 2 Lua 실행이 같은 프로세스에 기록됐고 사용자가 인식 성공을 확인했다. `Artifacts/device-camera-investigation.log`와 `Artifacts/device-after-camera-scan.png`가 증거다. 잠금 후 복귀, 프로세스 재생성, 장시간 반복 및 메모리·오디오 정량 측정, 다른 기기·OS는 후속 검증 범위다.

**빌드 방법**

워크스페이스 루트 `E:/task/Unity_Project`에서 실행한다. 스크립트는 설치된 Unity 2022.3.16f1의 SDK·NDK·OpenJDK·Gradle을 사용한다.

```powershell
# Android 홈만 빌드 + 단위 테스트
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyAndroid/tools/build.ps1

# Unity 샘플 준비 및 라이브러리 export
python KimchilyUnityRuntime/tools/prepare_runtime.py
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyUnityRuntime/tools/export_android.ps1

# Unity 브리지 PlayMode 테스트
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyUnityRuntime/tools/test_runtime.ps1

# 실제 Unity 모듈을 포함한 앱 빌드 + 네이티브 단위 테스트
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyAndroid/tools/build.ps1 -WithUnity
```

빌드 스크립트는 이 프로젝트 안의 `.gradle-user-home`과 `.android-user-home`을 사용하고, `local.properties`에 SDK 경로를 기록한다. 첫 Gradle 의존성 다운로드에는 네트워크가 필요하다. `-Offline`은 필요한 의존성이 준비된 뒤 사용한다. 다른 설치 위치는 `-UnityEditorRoot`·`-AndroidSdkRoot`로 지정한다.

Windows에서 상속된 환경에 `PATH`와 `Path`가 동시에 있으면 Unity Bee/Stevedore가 중복 키 오류로 중단될 수 있다. `build.ps1`은 빌드용 PowerShell 프로세스 안에서 이 별칭들을 제거하고 단일 `PATH`로 복원한다. 사용자·시스템 환경 설정은 변경하지 않는다. 이 처리 후 실제 통합 IL2CPP 빌드가 통과했다.

프로젝트 폴더를 바꾸면 Unity export가 성공해도 Gradle의 IL2CPP 캐시에 이전 절대 경로가 남을 수 있다. 이번 이름 변경에서는 `unityLibrary/build/il2cpp_arm64-v8a_Release/il2cpp_cache`만 워크스페이스의 `.kimchily-migration/Android-il2cpp-before-rebrand-20260918-143824`로 격리한 후 새 경로에서 통합 빌드가 통과했다. 작성한 Runtime 소스나 기존 검증 APK는 이 처리로 변경하지 않았다.

기기 작업도 **같은 Unity SDK의 `platform-tools/adb.exe`**를 사용한다. 사용자 Android SDK나 PATH의 ADB를 혼용하지 않는다. 이 환경에서 ADB 서버가 응답하지 않아 Unity의 Android 초기화와 `adb devices`가 함께 대기한 사례가 있었고, 같은 Unity SDK ADB로 서버를 복구한 뒤 초기화가 진행됐다. 설치 도구는 데이터를 지우거나 ADB 서버를 자동 종료하지 않는다.

```powershell
# 기본 SDK: Unity 2022.3.16f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyAndroid/tools/device.ps1 -Action Status

# 새 통합 APK 빌드 완료 후에만 실행. 기존 앱 데이터를 유지하며 교체한다.
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyAndroid/tools/device.ps1 -Action Install -Serial ce041714d973ad3005
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyAndroid/tools/device.ps1 -Action Launch -Serial ce041714d973ad3005

# 카메라와 별도로 링크 수신부터 검증한다. URI의 &sha256 인자를 보존해 전달한다.
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyAndroid/tools/device.ps1 -Action OpenWorld -Serial ce041714d973ad3005 -PublishResponse KimchilyCreator/Artifacts/publish-result.json
```

`-Serial`을 생략하면 연결·승인된 기기가 정확히 한 대일 때만 진행한다. 다른 휴대폰을 사용하면 그 기기의 serial을 지정한다. `-NativePreview`는 설치할 APK가 Android 단독 버전임을 명시할 때 사용한다. 같은 SDK를 쓰려면 `device.ps1`의 `-UnityEditorRoot`·`-AndroidSdkRoot`도 빌드와 동일하게 설정한다.

Android Studio에서는 Gradle JDK를 **JDK 11**로 맞춘다. 현재 기준은 AGP 7.1.2, Gradle 7.2, Kotlin 1.6.21, compile/target SDK 32, build-tools 32.0.0, min SDK 23이다. 기존 설치 환경에서 통합을 검증하기 위한 버전 고정이며 출시용 도구·target SDK 구성을 확정한 것은 아니다. Unity 2022.3.16f1의 기본 Gradle 조합과 일치한다. [Unity Gradle 호환 표](https://docs.unity3d.com/2022.3/Documentation/Manual/android-gradle-overview.html)

표준 wrapper도 포함되어 있다. Unity export가 완료된 뒤 프로젝트 폴더에서 다음 명령을 사용할 수 있다.

```powershell
.\gradlew.bat -PwithUnity=true :app:assembleDebug :app:testDebugUnitTest
```

Android Studio에서 Unity 포함 구성을 유지하려면 로컬 `gradle.properties`에 `withUnity=true`를 설정하고 sync한다. 기본은 false다. Unity export 위치가 다르면 `-PunityLibraryDir=...`를 사용한다. 기본 경로는 `../KimchilyUnityRuntime/Builds/Android/unityLibrary`다.

**QR·링크 입장 계약과 테스트**

QR 내용은 다음 형식이다. `manifest` 값 전체를 URL 인코딩한다. `sha256`은 서버에서 전달하는 `world.json` 원본 바이트의 SHA-256이며 64자리 16진수다.

```text
kimchily://world?manifest=<URL-encoded manifest URL>&sha256=<64 hex characters>
```

월드 주소는 절대 HTTP(S) 주소이며 경로가 정확히 `/worlds/{worldId}/{revisionId}/world.json`이어야 한다. 두 ID는 각각 1~80자의 영문·숫자·`_`·`-`만 허용한다. 자격증명, fragment, 추가 query, 중복 링크 필드, 인코딩한 경로 구분자와 `..` 경로는 거절한다. 원본 경로를 검사하므로 `.`·`..`·역슬래시·percent encoding을 정상 경로로 바꾸어 허용하지 않는다. `worldId`와 `revisionId`를 URL에서 추출해 Unity에 전달하며, 같은 월드라도 revision·주소·해시가 달라지면 기존 월드를 정리한 뒤 연다.

HTTP는 `BuildConfig.DEBUG`에서만 파서가 허용하고 `src/debug/AndroidManifest.xml`에서만 cleartext를 켠다. Release는 HTTPS 링크와 HTTPS 네트워크 설정을 사용한다. 이 해시는 링크에 지정된 파일과 다운로드 결과가 같은지 검증하는 용도이며 게시자 인증·서명을 대신하지 않는다.

1. 게시 도구가 생성한 QR 또는 링크를 준비한다. 개발용 LAN 서버 주소는 휴대폰에서 접근 가능한 PC IP를 사용한다. `localhost`는 휴대폰 자신이므로 사용할 수 없다.
2. Unity 포함 APK에서 **월드 QR 스캔**을 누르고 최초 카메라 권한을 허용한다. 또는 같은 링크를 입력란에 붙여넣고 **링크로 입장**을 누른다.
3. 다운로드 진행 → 게시된 씬 표시 → 나가기 → 홈을 확인한다. 외부 링크도 같은 월드로 들어가는지 확인한다.
4. 다른 revision 링크를 열어 이전 월드가 닫힌 뒤 새 revision이 열리는지 확인한다. 다운로드 중 나가기, 해시 불일치, 서버 연결 실패도 각각 확인한다.

스캐너는 [ZXing Android Embedded 4.3.0](https://github.com/journeyapps/zxing-android-embedded/tree/v4.3.0)과 **ZXing core 3.5.3**을 사용한다. 공식 문서의 이전 SDK 지원 방식 중 desugaring을 적용했다(`desugar_jdk_libs:1.1.5`, `coreLibraryDesugaringEnabled`, `multiDexEnabled`). minSdk 23을 유지하며 AndroidX는 활성화하고 Jetifier는 사용하지 않는다. 카메라 권한은 라이브러리 manifest와 CaptureActivity의 런타임 요청으로 처리한다. Android 6(API 23)의 카메라 동작은 별도 기기 검증이 필요하다.

브랜드 변경 전 core 3.3.0은 첫 QR을 읽었지만 두 번째 정상 게시 QR에서 `NotFoundException`이 발생했고 `TRY_HARDER`도 해결하지 못했다. 같은 이미지를 core 3.5.3의 일반 QR 검출로 읽어 당시 198자 링크 전체 일치를 확인했다. Android와 검증 도구는 core 3.5.3을 유지한다.

`kimchily://`로 변경된 199자 QR 일부에서는 core 3.5.3도 데이터 영역을 위치 검출 패턴으로 잘못 선택했다. 게시 인코더는 M 오류 정정·모듈 크기 8·여백 4를 유지하고 마스크를 0으로 지정한다. 동일 조건에서 15개 payload × 3크기 × 4방향 180개 검출이 모두 통과했다. 자동 마스크는 같은 검사에서 174개가 통과했다. 비교 결과와 원인은 `Artifacts/qr-compatibility`에 보존한다. 이는 검사한 입력의 회귀 검증이며 모든 촬영 조건의 인식을 보장하지 않는다.

단위 테스트에는 이 게시 설정으로 다시 만든 Kimchily fixture 2개와 실제 신규 게시 서버에서 받은 PNG 1개를 사용한다. 일반 검출·원본/절반/1⁄4 크기·90도 단위 회전을 확인하고 `PURE_BARCODE`나 강제 `TRY_HARDER`에 의존하지 않는다. 합성 fixture 2개만 다시 만들려면 `python KimchilyAndroid/tools/regenerate_qr_fixtures.py`를 실행한다. 실제 게시 PNG는 이 도구가 덮어쓰지 않는다.

게시된 QR PNG 자체를 카메라 없이 확인하려면 다음 검증 도구를 사용한다. Android와 같은 ZXing core 3.5.3으로 이미지를 실제 디코딩하고 게시 응답의 `launchUrl`과 전체 문자열을 비교한다. 선택한 `--verify-world-link`는 이미 컴파일한 Android 링크 파서까지 호출해 월드·revision·주소·해시를 비교한다. Java helper는 `javac`로 명시적으로 컴파일한 뒤 실행하여 예외를 그대로 보고한다. Gradle 빌드나 기기 조작은 실행하지 않는다.

```powershell
python KimchilyAndroid/tools/verify_qr.py --image KimchilyAndroid/app/src/test/resources/qr-published-kimchily.png --publish KimchilyAndroid/Artifacts/rebrand-qr-fixtures/publish-kimchily.json --verify-world-link --allow-http --output KimchilyAndroid/Artifacts/qr-decode-kimchily.json
```

과거 브랜드의 198자 QR 검증 결과 `Artifacts/qr-decode-v1.json`·`Artifacts/qr-decode-editor-v2.json`은 원본 그대로 보존한다. 현재 검증에는 새 Kimchily PNG와 그 PNG를 생성한 게시 응답을 함께 사용해야 한다. 기본 결과 파일은 `Artifacts/qr-decode-verification.json`이다. 이 검증은 PNG·링크 검증이며 휴대폰 카메라의 인식 성공을 대신하지 않는다.

**파일과 다음 작업**

- `app/src/main`: 홈 화면, QR·링크 파서, 메시지 처리, 순수 Kotlin 상태머신.
- `app/src/standalone`: Unity가 없는 미리보기용 launcher.
- `app/src/unity`: Unity Activity와 실제 launcher. 재export로 덮어쓰지 않는다.
- `app/src/test`: 링크 검증·revision 전환·상태 전환 단위 테스트.
- `tools/build.ps1`: 설치된 도구로 테스트·APK 빌드와 결과 복사.
- `tools/device.ps1`: Unity SDK ADB로 기기 확인·APK 교체·앱 실행·게시 링크 열기. 기본 동작은 기기 목록 확인이다.
- `../KimchilyUnityRuntime`: Bootstrap·샘플 씬·C# 브리지와 Unity export 도구.

다음 단계는 게시 서비스 인증·접근 제어, 다운로드 캐시와 재시도, 운영용 앱 출시 설정이다. 월드의 스크립트 언어와 허용 API는 Unity 실행기에서 지원하는 범위를 따른다. 임의의 C# DLL을 QR로 다운로드해 Android 앱에 추가하는 구조는 아니다.
