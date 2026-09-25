# Kimchily Expo — iPhone / Android 시연 앱

Windows에서 개발하는 React Native 프로젝트다. 현재 시연 경로는 **iPhone은 Expo Go, Android는 EAS APK**다. 사용자는 Apple Developer Program에 가입하지 않았고, 이전 TestFlight 사용은 테스터 경험이었다고 확인했다. iPhone 독립 설치용 internal/ad hoc 빌드 설정은 유지하되, 개발자 팀과 서명이 준비될 때 재개한다.

홈·월드 목록·최근 입장·링크 입력·카메라 QR은 앱에서 처리하고, 월드는 기존 Unity 6 WebGL 실행기를 WebView로 연다. 기존 Android/Unity 원본은 수정하지 않는다.

## 1. 현재 연결 상태

Expo 로그인과 프로젝트 생성·연결은 완료했다. 기본 프로젝트는 [@kimchily/kimchily-mobile](https://expo.dev/accounts/kimchily/projects/kimchily-mobile)이며 ID는 `5f59c8b9-6991-4594-9e23-b4c861d2b5e7`이다. `app.config.ts`에 owner와 프로젝트 ID가 저장돼 있으므로 **UUID를 다시 설정하거나 `eas init`을 반복할 필요가 없다.** `EAS_PROJECT_ID`는 다른 프로젝트로 명시적으로 변경할 때만 쓰는 선택적 재정의다.

이 문서 갱신 시점의 진행 상태는 다음과 같다.

- **iOS:** Apple Developer Program 미가입 상태이며 `You have no team associated with your Apple account` 오류를 확인했다. **iOS 클라우드 빌드는 시작하지 않았다.** 현재는 Expo Go에서 시연하고, 독립 설치용 설정은 보존한다. TestFlight 테스터 참여나 기기의 개발자 메뉴는 앱 서명 자격을 뜻하지 않는다.
- **Android:** [EAS preview 빌드](https://expo.dev/accounts/kimchily/projects/kimchily-mobile/builds/3db01052-2c08-4394-81e8-51d53cfe292e)가 성공했다. 버전 0.1.0, versionCode 1이다. [APK 다운로드](https://expo.dev/artifacts/eas/QreifKQemhGv3vatzSDPVSXEQlE4i8egrSWMgFkfiz0.apk). 실기기 설치·카메라·월드 실행은 별도로 확인한다.

현재 로그인 계정을 확인할 때는 다음 명령을 사용한다.

```powershell
Set-Location 'E:\GItHub\PortFolio\KimchilyWebGL\KimchilyExpo'
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task eas-status
```

다른 PC에서 로그인해야 하면 `-Task eas-login`을 사용한다. `-Task eas-init`은 별도 프로젝트를 처음 연결할 때 사용하는 도구로 남아 있다. 실행기는 EAS 명령 동안만 `EAS_NO_VCS=1`, `EAS_PROJECT_ROOT=KimchilyExpo`를 적용해 이 앱 폴더를 기준으로 업로드하며, 종료 후 기존 환경 값을 복구한다. Unity 작업공간에 Git 초기화나 커밋을 요구하지 않는다.

## 2. 본인 iPhone 등록과 iOS 빌드

`Authentication with Apple Developer Portal failed! You have no team associated with your Apple account`는 로그인한 Apple ID에서 개발자 팀을 찾지 못했다는 뜻이다. 다른 계정/조직에서 서명을 준비했다면 해당 팀의 멤버인 Apple ID로 로그인하고 초대 수락·멤버십 상태를 확인한다. EAS internal/ad hoc 빌드는 Apple Developer 팀의 서명과 기기 프로비저닝이 필요하며 iPhone 개발자 모드 활성화만으로 충족되지 않는다. 이 조건이 아직 준비되지 않았다면 같은 명령을 반복해도 해결되지 않는다. [EAS 내부 배포 조건](https://docs.expo.dev/build/internal-distribution/)

**서명 준비와 설치할 실제 기기 등록은 별개다.** ad hoc 앱에는 빌드 시점에 해당 iPhone UDID가 프로비저닝 프로필에 포함돼 있어야 한다. 이미 등록했다면 다시 등록하지 않고 목록과 빌드의 기기 선택을 확인한다. EAS 등록만 끝난 상태와 Apple 프로비저닝에 반영된 상태도 구분한다. 새 internal 빌드에서 등록 기기를 포함한 프로필을 생성하거나 갱신한다. [Expo internal 배포·기기 등록 안내](https://docs.expo.dev/build/internal-distribution/)

기기 등록과 목록 확인도 실행기에서 지원한다. 프로젝트 폴더에서 본인이 직접 조작할 수 있는 PowerShell 터미널로 진행한다.

```powershell
Set-Location 'E:\GItHub\PortFolio\KimchilyWebGL\KimchilyExpo'

# 처음 사용할 iPhone 등록: 안내되는 등록 URL/QR을 해당 iPhone에서 연다.
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task register-ios

# 등록 목록에서 본인 iPhone을 확인한다.
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task list-ios-devices

# preview / internal iOS 빌드. 등록한 iPhone을 프로비저닝 대상에 포함한다.
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task build-ios
```

`build-ios`에서 Apple ID와 관련 확인을 요청하면 본인 터미널에서 입력해 준비된 서명 정보를 이 EAS 프로젝트에 연결한다. 등록한 iPhone이 프로비저닝 대상에 포함됐는지 확인한다. 이 연결과 기기 선택을 마친 뒤 클라우드 빌드가 접수되는지 확인한다.

빌드가 성공하면 EAS가 제공하는 설치 페이지를 **등록한 iPhone**에서 열어 앱을 설치한다. 기기를 나중에 추가하면 새 프로필이 반영된 빌드 또는 재서명이 필요하다. 이 문서는 실행 절차이며, **이 프로젝트의 실제 EAS 클라우드 빌드 성공이나 IPA 설치를 완료했다는 기록은 아니다.**

## 3. Android APK 빌드

첫 Android preview 빌드가 성공했다. 이미 생성된 APK로 시연할 수 있으며, 이후 코드 변경으로 새 빌드가 필요할 때 같은 Expo 프로젝트에서 아래 명령을 실행한다. `preview` 프로필은 Android 설치용 APK를 생성한다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task build-android
```

빌드 성공 후 제공되는 APK를 본인 Android 기기에 설치한다. iPhone용 UDID 등록 절차는 Android APK 설치에 적용되지 않는다. 실제 클라우드 APK 산출물과 설치 결과는 별도로 확인한다.

새 앱의 Android 패키지는 `com.kimchily.mobile`이며 기존 Unity 네이티브 앱의 `com.kimchily.app`과 다르다. 두 앱은 함께 설치할 수 있다.

확인한 첫 APK는 [Artifacts/kimchily-preview.apk](Artifacts/kimchily-preview.apk)에 저장했다. 패키지·버전·서명 검사는 통과했으며 [빌드 검증 기록](Artifacts/android-preview-build.json)에 크기와 SHA-256을 남겼다. 휴대폰 설치와 실제 카메라·Unity 조작 확인은 아직 진행하지 않았다.

| 프로필 | 용도 |
| --- | --- |
| preview | 기본 시연 경로. Android APK / iOS internal 앱, 개발용 LAN HTTP 허용 |
| development | Expo 개발 클라이언트, internal 배포 |
| production | HTTPS 서버만 허용하는 별도 프로필 |

## 4. 설치한 앱에서 월드 열기

1. PC와 휴대폰을 같은 Wi-Fi/LAN에 연결한다.
2. Windows에서 월드 서버를 켠다.

   ```powershell
   Set-Location 'E:\GItHub\PortFolio\KimchilyWebGL'
   powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyPublish\tools\start.ps1
   ```

3. 설치한 **Kimchily** 앱을 연다. **연결 설정**에서 Windows PC의 서버 주소를 입력한다. 현재 개발 주소는 `http://192.168.0.4:8788`이며 PC 주소가 바뀌면 앱의 설정도 갱신한다. 휴대폰에서 `localhost`는 PC가 아닌 휴대폰을 가리킨다.
4. 월드 카드를 누르거나 **월드 QR 스캔**으로 Unity에서 게시한 월드 QR을 읽는다. 카메라 권한 요청을 허용하고, 월드 화면에서 **월드 시작**을 누른다.
5. 걷기·달리기·점프·시점 회전·나가기·다시 입장을 확인한다.
6. 테스트 후 월드 서버는 `KimchilyPublish\tools\stop.ps1`로 종료한다.

`preview` 앱에는 앱 코드가 포함되므로 **Metro/Expo 개발 서버는 필요하지 않다.** Unity WebGL 플레이어와 게시된 월드를 제공하는 **PC 월드 서버 8788은 켜져 있어야 한다.** 현재 `eas.json`의 `preview.env.EXPO_PUBLIC_WORLD_SERVER_URL`에는 `http://192.168.0.4:8788`이 기본값으로 설정돼 있다. PC 주소가 바뀌면 설치된 앱의 연결 설정에서 수정하면 된다. 이후 빌드의 기본값을 바꾸려면 해당 preview 환경 값을 갱신한다. 공개 환경 변수에는 비밀키나 게시 토큰을 넣지 않는다.

preview의 HTTP 허용은 로컬 개발 목적이며 앱은 HTTP 목적지를 LAN/loopback 주소로 제한한다. production은 같은 HTTP 예외를 넣지 않는다. 연결이 막히면 PC 주소, 같은 네트워크, 앱의 로컬 네트워크 권한, Windows 방화벽의 사설 네트워크 **8788** 접근을 확인한다. 도구가 공유기 포트 포워딩이나 외부 공개 터널을 만들지는 않는다.

## 선택 사항: Expo Go로 빠르게 확인하기

설치용 앱을 다시 빌드하기 전에 코드 변경을 확인하고 싶을 때 [Expo Go](https://expo.dev/go)를 사용할 수 있다. 이 경우 실행 호스트는 Expo Go이며 앞의 Kimchily internal 앱과는 구분한다.

**실제 iPhone의 Expo Go는 PC의 Expo CLI와 같은 Expo 계정으로 로그인해야 프로젝트를 연다.** 현재 PC CLI에서 확인한 계정은 `kimchily`다. iPhone의 Expo Go를 열어 **오른쪽 위 프로필 아이콘**에서 같은 `kimchily` Expo 계정으로 로그인한다. 여기서 사용하는 계정은 Apple ID가 아니다. [Expo 공식 실행·계정 문제 안내](https://docs.expo.dev/get-started/start-developing/)

```powershell
Set-Location 'E:\GItHub\PortFolio\KimchilyWebGL\KimchilyExpo'
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run.ps1 -Task start
```

최신 Expo Go를 설치한 뒤 iPhone 카메라 또는 Android Expo Go에서 터미널의 **Expo 실행 QR**을 읽는다. Expo 개발 서버 기본 포트는 **8081**, 월드 게시 서버는 **8788**이다. 앱 안에서 읽는 **Unity 월드 QR**은 입장할 콘텐츠를 고르는 QR이므로 두 QR을 구분한다. 종료는 개발 서버 터미널에서 `Ctrl+C`를 누른다.

이 PC에서 실행 중인 주소는 `exp://192.168.0.4:8081`이며, [실행 QR 이미지](Artifacts/expo-go-qr.png)를 iPhone 기본 카메라로 읽으면 된다. PC와 iPhone을 같은 Wi-Fi에 연결하고 [Expo Go](https://expo.dev/go)를 먼저 설치한다. 앱이 뜨면 카메라·로컬 네트워크 접근을 허용하고 앱 안의 월드 목록이나 월드 QR을 사용한다. 이 방법은 Expo Go 안에서 실행되며 독립 IPA 설치는 아니다.

`YOU'RE SIGNED ... EXPO CLI`처럼 계정 관련 안내가 나타나면 다음 순서로 다시 연결한다.

1. Expo Go 오른쪽 위 프로필에서 `kimchily` 계정인지 확인하고, 다른 Expo 계정이라면 로그아웃한 뒤 `kimchily`로 로그인한다.
2. PC 개발 서버를 **로그인 전에 시작했다면** 이전 익명 연결 정보가 남을 수 있다. 해당 Metro 터미널에서 `Ctrl+C`로 종료한 뒤 위의 `-Task start` 명령으로 다시 시작한다. 월드 서버 8788은 그대로 유지한다.
3. iPhone에서 **Try Again**을 누르거나, 다시 시작한 터미널의 Expo 실행 QR을 재스캔한다.

이 계정 안내를 보완한 시점에는 iPhone에서 앱이 정상으로 열렸는지 아직 사용자 확인 전이다.

Expo Go의 네이티브 설정은 설치용 앱의 `app.config.ts` 설정과 동일하게 적용되지 않을 수 있으므로, Expo Go에서의 연결·카메라 결과도 별도로 확인한다.

## 개발 환경

- Expo SDK 57 / React Native 0.86.3 / React 19.2.3 / TypeScript 6.
- Node **22.13 이상** 또는 24 LTS. 현재 시스템 Node 18은 새 Expo SDK 요구조건을 충족하지 않는다.
- `tools/run.ps1`은 적합한 PATH의 Node를 사용하며, 현재 PC에서는 사용 가능한 Codex 번들 Node 24로 대체한다. 다른 PC에서는 Node 24를 설치하거나 `-Node 'C:\...\node.exe'`를 지정한다. 전역 Node를 변경하지 않는다.
- 새 환경 설치: `powershell -File tools/run.ps1 -Task install` (`npm ci`).
- 검사: `-Task typecheck`, `-Task test`, `-Task doctor`, `-Task export`.
- 로컬 개발에서는 `.env.example`을 `.env.local`로 복사해 서버 주소를 정한다. `.env.local`은 Git/EAS 업로드에서 제외한다.

## 구현 구조와 확인 범위

- `src/world-client.ts`: 서버·QR·게시 응답 검증, 카탈로그, 취소/타임아웃.
- `src/CameraScreen.tsx`: 네이티브 카메라 QR, 앱 비활성/화면 종료 시 카메라 해제.
- `src/WorldPlayer.tsx`: Unity WebView와 홈 복귀. 기존 Android의 Unity as a Library 대신 Unity WebGL 실행기를 사용한다.
- `KimchilyWebApp`은 브라우저/PWA 보조 클라이언트다. 웹 카메라를 사용할 때는 [LAN HTTPS 안내](../docs/lan-webapp-guide.md)를 따른다. Expo 네이티브 QR 사용을 위해 개발 CA를 설치할 필요는 없다.

Safari에서 월드가 실행됐다는 기존 사용자 확인은 유지한다. **설치한 Expo 앱 및 Expo Go 안의 실제 카메라·WebView 실행은 별도의 기기 확인 항목**이다. 타입 검사, 자동 테스트, iOS/Android JS 번들 생성은 EAS 클라우드 빌드 성공·앱 설치·GPU/메모리 실기기 검증을 대신하지 않는다. 서버 없이 월드를 완전히 오프라인으로 실행하는 기능은 구현하지 않았다.

이번 앱은 기존 TypeScript 코드와 EAS 빌드 흐름을 활용하는 Expo/React Native로 구성했다. Flutter로 바꾸는 작업은 현재 구현 범위에 포함되지 않는다.
