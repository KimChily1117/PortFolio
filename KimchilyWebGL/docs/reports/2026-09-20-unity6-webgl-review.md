# Unity 6 업그레이드와 iPhone WebGL 배포 검토

검토일: 2026-09-20. 범위: 현재 코드·설정 정적 분석과 Unity 공식 문서 확인. Unity 6 설치, 프로젝트 변환, Web 빌드 및 iPhone 실기기 검증은 아직 수행하지 않았다.

## 판단

**Unity 6.3 LTS의 검증된 패치 버전으로 별도 복사본을 전환하고, iPhone Safari에서 실행하는 WebGL 경로를 먼저 검증하는 방식을 추천한다.** 현재처럼 Mac과 유료 Apple Developer 계정이 없는 환경에서 브라우저로 월드를 이용하려는 목적에 맞는다. 웹페이지를 배포하는 경로에는 iOS 앱의 빌드·서명이 필요하지 않다. Windows에서 Web 빌드를 만들고 서버에 게시한 뒤 iPhone 기본 카메라로 QR을 열면 된다.

Unity 2022.3 문서는 모바일 WebGL을 공식 지원하지 않는다고 명시한다. 일부 기기에서 실행될 수 있다는 것과 서비스 지원 대상으로 삼는 것은 구분해야 한다. Unity 6.3은 iOS Safari 15 이상을 지원 대상으로 명시한다. iPhone 13 Pro의 정확한 iOS 버전과 실제 월드의 성능은 실기기에서 확인해야 한다. [Unity 2022.3 호환성](https://docs.unity3d.com/2022.3/Documentation/Manual/webgl-browsercompatibility.html), [Unity 6.3 호환성](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-browsercompatibility.html)

| 선택 | 평가 |
| --- | --- |
| 현재 2022.3.16f1에 WebGL 추가 | 실험은 가능하지만 모바일 공식 지원 기반이 아니어서 장기 목표에는 비추천 |
| Unity 6.0 LTS | 모바일 Web 지원은 있지만 일반 LTS 지원이 2026년 10월까지라 신규 전환 대상으로 이점이 작음 |
| Unity 6.3 LTS | 추천 후보. 일반 LTS 지원이 2027년 12월까지이며 현재 서비스 제작 도구의 기준 버전을 정하기에 적합 |

지원 기간은 [Unity 공식 지원 정책](https://unity.com/releases/unity-6/support)을 확인했다. 실제 설치 시점에 6000.3 계열의 패치 릴리스 노트와 알려진 문제를 확인하고 **제작기·실행기·번들 빌드의 정확한 버전을 하나로 고정**한다. 단순히 버전 번호가 가장 높은 Update 릴리스를 자동 선택하지 않는다.

## 현재 프로젝트에서 확인한 사항

검토 대상은 현재 제작·배포 경로인 `KimchilyCreator`, `KimchilySDK`, `KimchilyUnityRuntime`, `KimchilyAndroid`, `KimchilyPublish`다. 기존 Unity 2021 계열의 `UnityKimchilyCreator`, `UnityKimchilyWorld`, `UnityToolManager`는 현재 게시 파이프라인과 별개이므로 이번 전환에 일괄 포함할 필요가 없다.

PC에는 Unity 2022.3.16f1과 Android/Windows 빌드 지원만 설치되어 있다. Unity 6 및 Web 빌드 모듈은 추가 설치가 필요하다.

| 부분 | 확인한 구현 | 전환 시 필요한 작업 |
| --- | --- | --- |
| 제작기와 SDK | 로컬 UPM 패키지 3개를 상대 경로로 참조 | SDK도 함께 복사하여 시험 프로젝트가 원본 SDK를 수정하지 않도록 구성 |
| 렌더링 | 게시 대상은 Built-in Render Pipeline | 최초 전환에서도 Built-in 유지. URP 전환은 별도 작업으로 취급 |
| 버전 검증 | `WorldManifestValidation.cs:16`에서 SDK 및 Unity 버전 완전 일치 요구 | 새 실행기와 같은 버전으로 모든 시험 월드를 다시 빌드. 검증을 삭제하지 않음 |
| 플랫폼 검증 | `WorldContentSession.cs:24`에 WebGL 플랫폼 분기 없음 | WebGL 런타임 플랫폼을 추가하고 다른 플랫폼 번들 거부 유지 |
| 게시 | CreatorWindow, WorldContentBuilder, WorldPublisherClient, `server.py:87`이 Android 전용 | WebGL 빌드·업로드·검증 경로 추가 |
| QR | `server.py:235`에서 `kimchily://world` 앱 링크 생성 | 브라우저 플레이 페이지 URL과 Web 월드 연결 추가 |
| 호스트 연결 | KimchilyHostBridge의 외부 이벤트 전달은 Android JNI | 웹페이지와 Unity 간 준비·입장·실패·나가기 콜백 구현 |
| 다운로드 | 임시 디렉터리에 파일 다운로드, SHA 확인, 파일 기반 번들 로딩 | 브라우저 저장소·메모리·동기 처리 비용을 검증하고 필요한 로더 분리 |
| 빌드 스크립트 | Unity 2022.3 경로와 Android 도구 버전이 고정됨 | 선택한 Editor 버전을 공통 설정으로 전달하고 플랫폼별 도구 연결 정비 |

### 모델·애니메이션·TypeScript 재사용

FBX, 프리팹, AnimationClip, Avatar, 애니메이션 매핑 프로파일과 이동·카메라 코드는 공통 자산/코드로 유지할 수 있는 구조다. UnityChan 전용으로 전환할 이유는 없다. 다만 모델의 Rig, Generic 본 경로, 셰이더, 텍스처, 포함된 C# 컴포넌트는 새 타깃에서도 검증해야 한다. 원본 FBX를 재사용한다는 것이 기존 Android AssetBundle을 Safari에 그대로 로드한다는 뜻은 아니다.

TypeScript는 에디터에서 JavaScript로 컴파일하고 런타임에서 Jint로 실행하는 현재 방식을 우선 유지한다. 현재 고정된 인터프리터는 Jint 4.16.2와 Acornima 1.7.0이다. 새 언어 또는 별도의 브라우저 JavaScript 실행기로 바꾸는 작업을 업그레이드에 섞을 필요는 없다.

관리 DLL의 대상은 주로 .NET Standard 2.1이며 Unity 6에서도 지원되는 범위다. 이것은 DLL 재사용의 출발 근거이며 WebAssembly 실행 성공의 증거는 아니다. Lua 호환용 MoonSharp에는 기존 초기화 문제를 해결한 로더 패치가 있으므로 순정 DLL로 교체하지 않는다. [Unity .NET 호환성](https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html)

SDK의 C# 코드와 관리형 DLL은 Web 플레이어 빌드 때 IL2CPP/WebAssembly로 포함한다. 배포 후 AssetBundle로 새 C# DLL 구현을 추가하는 방식은 현재 설계의 지원 범위가 아니다. 다운로드할 기능은 현재처럼 TypeScript와 미리 포함된 SDK API로 구현한다.

`KimchilyUnityRuntime/Assets/link.xml`은 Creator·TypeScript·Lua 런타임 및 Jint 관련 어셈블리를 보존한다. Web 실행기를 별도 프로젝트로 만들면 이 설정도 가져가야 한다. 에디터에서 정상인 코드도 IL2CPP의 AOT·코드 제거 단계에서 실패할 수 있으므로 Web 플레이어 검증이 필요하다. Unity Web은 C# 스레드와 동적 코드 생성에 제약이 있지만 코루틴 기반 흐름을 지원한다. [Unity Web 기술 제한](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-technical-overview.html)

정적 검색에서 발견한 `FindObjectsOfType` 계열 호출은 obsolete API 정리 후보이며, 이를 이미 확인된 컴파일 실패로 취급하지 않는다. 현재 대상 프로젝트에 UniVRM/UniGLTF 패키지는 발견되지 않았다. 우선 재임포트 대상은 임베디드 SpringBone과 실제 모델·커스텀 셰이더다. 업그레이드 자체가 외부 모델의 C# 기능을 게시 플레이어에 추가해 주지는 않는다.

### Android에 영향을 주는 확정 수정 지점

`KimchilyAndroid/tools/build.ps1:14`는 `gradle-launcher-7.2.jar`를 직접 찾으며 Android SDK 32도 검사한다. 따라서 Editor 경로만 Unity 6으로 바꾸는 방식은 충분하지 않다. 네이티브 프로젝트의 AGP·Kotlin·JDK 설정과 새 Unity가 생성하는 `unityLibrary`를 함께 맞춰야 한다. 정확한 도구 버전은 선택한 Unity 패치에서 생성한 프로젝트를 기준으로 정한다.

`KimchilyUnityActivity.kt`는 `UnityPlayerActivity`를 상속한다. 기존 진입점을 사용하는 설정을 명시한 뒤 Unity 6 export 결과와 병합 AndroidManifest, 테마, 생명주기, 키 입력을 대조하는 방식이 변경 범위를 줄인다. Unity 6의 GameActivity 관련 변경을 기본값에 맡기지 않는다. 현재 호스트는 UnityPlayer를 FrameLayout으로 직접 캐스팅하지 않아 해당 API 변경의 직접 영향은 작다. [Unity 6 업그레이드 안내](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity6.html), [Unity 6.3 업그레이드 안내](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html)

## 업그레이드 절차

1. **시험용 작업공간을 만든다.** 예를 들어 별도의 `Unity6Trial` 아래에 위 5개 디렉터리의 소스·Assets·Packages·ProjectSettings와 모든 `.meta`를 보존한다. Creator만 복사하면 상대 경로가 원본 SDK로 연결될 수 있으므로 UPM 참조를 먼저 확인한다. 생성물인 Library/Temp/obj/Builds는 시험용 프로젝트에서 새로 생성하고, 기존 APK·게시 revision·원본 백업은 현재 위치에 보존한다.
2. **Unity Hub에 Unity 6.3 LTS를 병렬 설치한다.** Web Build Support를 선택한다. Android 회귀 검증에는 해당 Editor의 Android Build Support, SDK/NDK/OpenJDK도 필요하다. Safari 웹 실행만을 위해 iOS Build Support나 Xcode를 설치할 필요는 없다.
3. **시험 복사본을 새 Editor로 연다.** SDK ValidationProject → Runtime → Creator 순서로 패키지 해석과 컴파일 오류를 좁힌다. 자동 API 변환의 변경 내역을 확인한다. 현재 Built-in, 입력 방식, 스크립트 어셈블리 이름, 자산 GUID를 유지한다. 6.0부터 6.3까지의 업그레이드 안내를 검토하되 중간 버전을 전부 설치하는 것을 필수 절차로 삼지는 않는다.
4. **패키지와 빌드 도구를 맞춘다.** 테스트 프레임워크가 개인 PC 캐시의 `file:C:/Users/...` 경로에 고정된 부분을 이식 가능한 의존성으로 정리한다. 임베디드 SpringBone 1.2.0-preview의 에디터 코드와 외부 모델 셰이더를 컴파일·재임포트한다. 관리 DLL 검사도 Unity 6 어셈블리를 참조하도록 변경한다.
5. **작은 Web 월드를 먼저 실행한다.** 플레이어에 월드를 내장한 임시 검증 빌드로 iPhone Safari의 렌더링, TypeScript Start/Update·이벤트·코루틴, 조이스틱, 3인칭 카메라, 실제 애니메이션 포즈를 확인한다. 이 단계는 최종 다운로드형 서비스 완료를 의미하지 않는다.
6. **QR 게시 파이프라인을 연결한다.** 같은 Unity 패치에서 Web 전용 실행기와 Web 전용 월드 번들을 빌드한다. 에디터에 Web 게시 타깃을 추가하고, QR의 웹페이지에서 해당 월드를 다운로드해 실행한다. 생성한 HTML은 서버로 제공한다. `index.html`을 파일 탐색기에서 직접 여는 방식으로 테스트하지 않는다. [Web 빌드·게시 절차](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-gettingstarted.html)
7. **Android 회귀 검증 후 전환한다.** 새 Unity의 APK 및 Android 번들을 함께 만들어 기존 입장·퇴장·재입장·방향 선택·애니메이션을 검증한다. 기준 통과 후 개발용 제작기를 전환한다. 되돌릴 때는 변환된 씬을 구버전으로 다시 저장하는 대신 이전 작업공간과 이전 APK/게시 revision을 사용한다.

## Web 게시 기능의 설계 방향

목표 흐름은 **Unity 에디터에서 모델·TypeScript 추가 → Web Build & Publish → 웹주소 QR → iPhone 기본 카메라 → Safari → 월드 입장**이다. 초기 QR 인식을 위해 웹페이지 자체에 카메라 스캐너를 만들 필요는 없다.

플레이어와 월드 파일은 우선 같은 출처에서 제공해 CORS 구성을 단순하게 한다. 외부 배포는 HTTPS를 기준으로 하고, `.wasm` MIME 형식과 gzip/Brotli 압축 헤더, 로딩 실패 UI, 브라우저 캐시 갱신을 함께 구현한다. 개발용 같은 Wi-Fi HTTP 접근과 외부 서비스용 HTTPS 배포는 별도 검증한다. [Web 서버 배포 설정](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-deploying.html)

첫 구현은 Web 전용 revision/QR로 검증할 수 있다. 이후 하나의 QR에서 Android 앱과 브라우저 중 경로를 고르게 하려면, 각 타깃 manifest URL·SHA·SDK·Unity 버전을 가리키는 상위 릴리스 정보를 설계해야 한다. 현재 `worlds/{worldId}/{revisionId}`는 하나의 immutable manifest를 저장하므로 같은 revision을 다른 플랫폼 파일로 덮어쓰면 안 된다. 기존 링크를 유지하면서 버전별 Web 실행기를 선택할 수 있어야 한다.

Web 번들은 LZ4를 사용하고 불필요한 번들을 즉시 해제한다. 브라우저에서는 다운로드·압축 해제·자산 로딩 시 메모리 사용이 중요하다. 현재 파일 기반 로더가 그대로 적합한지는 측정해야 하며, 파일 API 전체가 불가능하다고 단정하지 않는다. 파일 시스템 기반 AssetBundle 캐시와 브라우저 캐시는 다르다. SHA 검증은 브라우저용 로더에서도 유지한다. [Web AssetBundle 제한](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-assetbundles.html)

가로·세로 UI는 브라우저 화면 크기 변화에 대응하도록 구성한다. 가로 고정, 전체 화면, 소리 재생은 Safari의 실제 동작을 확인하고 필요한 시작 버튼·회전 안내를 제공한다. 네이티브 앱의 화면 방향 설정을 그대로 적용할 수 있다고 가정하지 않는다.

## 전환 완료 기준

| 검증 단계 | 확인할 결과 |
| --- | --- |
| 컴파일·에디터 | UPM과 사용자 모델 임포트 성공, Missing Script 없음, 프로파일 직접 매핑·Play 디버그 정상 |
| SDK 테스트 | 기존 EditMode/PlayMode 테스트 통과, 버전·플랫폼 불일치와 손상된 번들은 계속 거부 |
| iPhone Safari | QR 입장, 실제 이동·걷기·달리기·점프, 독립 3인칭 카메라, TypeScript 및 코루틴 정상 |
| 브라우저 자원 | 초기 다운로드·입장 시간과 메모리/프레임 측정, 재입장과 탭 복귀에서 크래시·누적 로딩 없음 |
| 일반 모델 | UnityChan 외 Humanoid 모델과 Generic 모델을 포함해 매핑·본 경로·셰이더 확인 |
| 게시 | 에디터 버튼에서 새 Web revision 발행, 재발행 후 새 콘텐츠 확인, 오류 원인 표시 |
| Android | 새 APK+새 Android 번들에서 기존 QR·입퇴장·화면 방향·입력·애니메이션 재검증 |

현재 보존된 Unity 2022.3 테스트 결과는 EditMode 32/32, Runtime PlayMode 90/90 통과다. 이 값은 이전 검증 기준이며 **Unity 6 또는 WebGL에서 통과한 결과가 아니다**. 이번에는 테스트 결과 파일을 재확인하고 업그레이드 영향만 조사했다.

TypeScript의 클래스·Map·generator, 에디터 필드 연결, 이벤트 해제, 종료 처리는 Web에서도 확인한다. 현재 초기화 5초/실행 100ms 제한이 Safari의 첫 실행에서 정상 스크립트를 중단시키지 않는지도 측정한다. 기존 제한을 무조건 높이는 대신 실제 지연 원인을 확인한다.

다음 구현 단위는 **시험 작업공간과 Unity 6.3 환경 준비 → 작은 월드의 iPhone Safari 실행 확인**이다. 해당 검증을 통과하면 에디터 Web 게시 버튼과 플랫폼별 번들 배포로 확장한다.
