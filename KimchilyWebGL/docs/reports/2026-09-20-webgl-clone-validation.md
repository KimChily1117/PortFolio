# 기존 Android 보존 및 Unity 6 / WebGL 전환 검증

작성: 2026-09-20 (KST)

## 결과

기존 프로젝트를 보존한 별도 복제본에서 **Unity 6 Web 실행기 → Creator의 WebGL 월드 빌드 → 서버 게시·QR → Chrome에서 월드 입장**까지 동작한다. TypeScript 클래스·Map·코루틴, FBX 플레이어, 연결한 걷기·달리기·점프 애니메이션과 3인칭 카메라를 확인했다. **iPhone 실기기에서도 정상 실행된다는 사용자 확인을 받았다(2026-09-20).**

| 구분 | 기존 기준 버전 | 새 개발 복제본 |
| --- | --- | --- |
| 작업공간 | `E:\task\Unity_Project` | `E:\task\KimchilyWebGL` |
| Unity | 2022.3.16f1 | 6000.3.24f1 / `4e7b9b5b6244` |
| 이번 실행 대상 | 기존 Android 구현 보존 | WebGL 2 / IL2CPP |
| 게시 서버 | 8787 | 8788 |
| 토큰·게시 저장소 | 원본 유지 | 별도 `.local` 생성 |

## 보존 범위

Creator·SDK·UnityRuntime·Android·Publish·문서를 복사했다. `.meta`와 자산 GUID, 로컬 UPM 상대 경로를 유지했다. Library/Temp, 캐시, 기존 서버 토큰·실행 상태는 복사하지 않았다.

`preservation/source-manifest.json`에 원본 소스·설정·문서 **2,148개**, 별도 APK·테스트·Android 화면 증거 **8개**를 기록했다. 원본과 보존한 증거를 다시 해시 비교한 결과 모두 일치했으며, 클론의 로컬 참조 **16개**도 복제본 안에서 해결된다. 검사 결과는 [baseline-verification.json](../../Artifacts/baseline-verification.json)에 있다. 이 검사는 기록한 파일의 내용 일치를 확인하며 원본 폴더 전체의 디스크 이미지 백업을 의미하지 않는다.

기존 Android APK SHA-256: `1fcf60d536c30366217acc17d918c262cd20a4d63a2f76b53ed18cb1d0545e32`.

## 구현한 변경

- Unity 6000.3.24f1과 Web Build Support 설치. 공통 버전 설정·실행 도구가 지정한 버전과 복제본 경로를 확인한다.
- Unity 6의 Scene 타입과 테스트 패키지 변경을 반영. 개인 AppData 캐시 참조와 원본 프로젝트에 기대던 Lua 테스트 파일을 제거하고 클론 안에서 해결한다.
- 기존 호스트 프로토콜을 연결하는 Web 브리지와 모바일 크기 대응 HTML 플레이어 추가. 사용자 시작 버튼, 로딩·오류 표시, 나가기·재입장, 화면 크기/포커스 변경 시 입력 초기화를 제공한다.
- Creator 게시 대상에 WebGL 추가. 원본 Android 링크와 별도로 웹 실행 링크를 발급하고, 게시 응답의 월드·revision·manifest hash·경로를 검증한다.
- 서버가 `/player/` 실행기 파일과 WebGL 월드·QR을 제공한다. 기존 파일 무결성 검사와 revision 불변 정책을 유지하며 MIME과 압축 파일 헤더를 처리한다.
- 월드에는 기존 범용 모델/애니메이션 프로필을 사용한다. 테스트 예제에 UnityChan을 썼지만 새 실행 코드를 UnityChan 타입에 종속시키지 않았다. 개별 FBX의 rig·shader·외부 스크립트 호환성은 별도로 확인해야 한다.

## 실제 브라우저에서 발견하고 해결한 문제

1. Jint의 할당량 제한이 WebGL에서 미구현인 `GC.GetAllocatedBytesForCurrentThread`를 호출했다. WebGL 플레이어에서만 이 제한 등록을 제외했다. 문장 수·시간·재귀·배열·소스 크기 제한은 유지한다. 일반/Editor 프로필의 메모리 제한은 그대로다. WebGL에는 이와 동등한 VM별 메모리 한도가 없으므로, 공개 UGC 서비스의 격리 설계는 추가 작업이 필요하다.
2. Acornima/Jint의 DLL 내장 리소스가 최종 Web 데이터에서 빠져 초기화가 실패했다. Unity의 표준 `PlayerSettings.WebGL.useEmbeddedResources = true`를 설정했다. 재빌드 데이터에서 Acornima 17,353 bytes, Jint 125,745 bytes, mscorlib 337,563 bytes 리소스를 확인했다. 벤더 DLL을 수정하거나 데이터 파일을 수동 패치하지 않았다. [Unity 6.3 내장 리소스 문서](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-embeddedresources.html)
3. 증분 빌드는 내용이 같은 HTML 파일의 타임스탬프를 유지한다. 실행 스크립트가 이를 실패로 판정하던 부분을 수정해 이번 실행의 성공 로그와 결과 파일 존재를 확인한다. 수정 후 같은 빌드를 다시 실행해 정상 종료했다.

## 검증 결과

| 검사 | 결과 |
| --- | --- |
| Unity 6 SDK EditMode | 45/45 통과 |
| Unity 6 SDK PlayMode | 57/57 통과 |
| Unity 6 Runtime PlayMode | 90/90 통과 |
| 관리 코드 SDK 컴파일 | 오류 0, 기존 obsolete 경고 1 |
| TypeScript 컴파일러 / Facade | 19 / 17 통과, 엄격 타입 검사·IDE 확인 |
| TypeScript VM 일반 / WebGL define | 각각 30/30 통과 |
| Web 호스트 JavaScript | 25/25 통과 |
| 게시 서버 HTTP / 관리 흐름 | 20 / 22 통과 |
| 보존 검사 도구 단위 검사 | 8/8 통과 |
| 실제 Web 실행기·월드 번들·Editor 게시 | 성공 |
| Chrome 기본 데모 | TypeScript·Map·코루틴, 이동 3.06m, 점프·재입장 통과 |
| Chrome 게시 월드 | 원격 번들 입장, TypeScript, 이동 3.00m, Run/Jump, 재입장 통과 |
| Chrome 터치 에뮬레이션 | 조이스틱 Walk 1.17m/s·Run 약 3.95m/s, Jump, 이동 약 2.62m, 재입장 통과 |
| Chrome 화면 크기 변경 | 844×390 → 390×844, 진행 중 이동 입력 해제 확인 |
| iPhone 실기기 Web 실행 | 사용자 정상 실행 확인(기존에 알려준 모델: iPhone 13 Pro) |
| 복제본 Unity 6 Android APK | 미검증, 원본 APK 유지 |

브라우저 테스트는 별도 프로필의 headless Chrome과 소프트웨어 WebGL로 수행했다. 데스크톱에서 로컬 서버까지의 첫 입장은 약 6초였으며 iPhone 성능 수치가 아니다. 리사이즈 검사 첫 실행은 월드 동작은 성공했지만 WebGL.data 요청에 `ERR_ABORTED`가 기록되어 실패로 남겼다. 같은 검사 재실행은 요청 오류 없이 통과했다. 실패 기록을 지우지 않았으며 Safari 실기기에서 네트워크/재입장도 함께 확인해야 한다.

### iPhone 사용자 확인 기록 — 2026-09-20

게시한 `my-first-world` / `webgl-20260919T182904009Z-012a9d19`의 QR·Safari 실행 절차 안내 후 사용자가 **“아이폰에서 잘됨”**이라고 응답했다. 이를 iPhone 실기기 정상 실행의 수동 확인으로 기록한다. 정확한 iOS·브라우저 버전, 기능별 검사 결과, FPS·메모리·장시간 실행 수치는 수집하지 않았다. 기존 Chrome 자동 검사 JSON의 `iphoneVerified: false`는 해당 자동 검사가 iPhone에서 수행되지 않았다는 사실이므로 그대로 유지한다.

증거:

- [Unity SDK EditMode](../../KimchilySDK/Artifacts/editmode.xml), [SDK PlayMode](../../KimchilySDK/Artifacts/playmode.xml), [Runtime PlayMode](../../KimchilyUnityRuntime/Artifacts/runtime-playmode.xml)
- [기본 데모 브라우저 결과](../../Artifacts/web-browser/demo/result.json), [게시 월드 결과](../../Artifacts/web-browser/published-world/result.json)
- [터치·화면 전환 최종 결과](../../Artifacts/web-browser/published-touch-resize/result.json), [첫 리사이즈 실행 기록](../../Artifacts/web-browser/published-touch/result.json)
- [걷기](../../Artifacts/web-browser/published-touch-resize/world-walking.png), [달리기](../../Artifacts/web-browser/published-touch-resize/world-moving.png), [점프](../../Artifacts/web-browser/published-touch-resize/world-jump.png), [세로 화면](../../Artifacts/web-browser/published-touch-resize/world-portrait.png)

## 지금 실행할 월드

World: `my-first-world` / revision: `webgl-20260919T182904009Z-012a9d19`.

[게시 QR 페이지](http://192.168.0.4:8788/w/my-first-world/webgl-20260919T182904009Z-012a9d19)를 PC에서 열고 iPhone 기본 카메라로 스캔한 뒤 Safari의 **월드 시작**을 누른다. 같은 LAN과 실행 중인 8788 게시 서버가 필요하다. 최종 주소는 [게시 결과](../../KimchilyCreator/Artifacts/publish-result.json), 재빌드·서버 관리 절차는 [WebGL 가이드](../webgl-guide.md)에 있다.

현재 실행기는 압축하지 않은 개발 빌드로 약 **70MB**이고 월드 번들은 약 **6.7MB**다. 외부 공개 전에는 실기기 메모리·로딩 시간과 렌더링, 압축·캐시·HTTPS 배포를 검증해야 한다. Safari의 방향 잠금과 전체 화면은 브라우저 지원 범위를 따른다. 현재 iPhone 앱 서명이나 Mac은 이 웹 접속 경로에 필요하지 않으며, 네이티브 iOS 앱은 별도 작업이다.

## 포트폴리오에 정리할 때

원본 Android의 안정된 시연 자료와 새 WebGL 전환 기록을 구분해 제시할 수 있다. 자체 구현한 UPM·Unity 래퍼·TypeScript API/컴파일·실행 제약·게시/QR·네이티브 호스트·모델 애니메이션 연결을 설명하고, UnityChan 등 외부 모델과 Jint/Acornima 등의 라이브러리 출처를 함께 표기한다. 전체 ZEPETO 기능을 구현했다고 표현하지 않고 실제 검증한 제작→게시→입장 시나리오를 시연 범위로 삼는다.
