# Kimchily — Unity 6 / WebGL 개발 복제본

소스 이주와 새 환경 실행 방법은 [MIGRATION.md](MIGRATION.md)에 정리했습니다.

**개인 iPhone·Android 시연용 앱은 [KimchilyExpo](KimchilyExpo/README.md)에서 개발합니다.** Windows에서 EAS 내부 배포용 iOS 앱과 Android APK를 빌드하는 설정을 준비했습니다. 현재 iOS는 Apple 개발자 팀 연결 문제로 설치용 빌드를 보류했으며 Expo Go로 시연할 수 있습니다. 브라우저/PWA 보조 클라이언트는 `KimchilyWebApp`, 웹 카메라용 인증서 설정은 [LAN HTTPS 가이드](docs/lan-webapp-guide.md)에 있습니다.

앱·QR·서버 변경과 검증 범위는 [크로스플랫폼 시연 앱 보고서](docs/reports/2026-09-20-expo-webapp.md)에 기록했습니다.

기존 Android 구현을 보존하고, Unity 6.3.24f1에서 모델·TypeScript 월드를 WebGL로 게시하여 iPhone Safari에서 실행하도록 확장하는 작업공간입니다.

개발 경로는 **`E:\GItHub\PortFolio\KimchilyWebGL`** 입니다. 원본 **`E:\task\Unity_Project`** 은 Unity 2022.3.16f1/Android 기준 버전으로 유지합니다. [보존 기록](preservation/README.md)에 원본 소스 해시와 APK·검증 자료를 기록했습니다.

새 제작기는 이 폴더의 **[KimchilyCreator](KimchilyCreator/README.md)** 입니다. Unity 6 설치, Web 실행기·월드 번들 빌드, QR 게시와 PC Chrome 실행을 확인했습니다. TypeScript·코루틴, 키보드/터치 에뮬레이션 이동·걷기·달리기·점프, 3인칭 카메라와 재입장까지 검증했습니다. **iPhone 실기기에서도 정상 실행된다는 사용자 확인을 받았습니다(2026-09-20).** 복제본 게시 서버는 **8788**, 원본 서버는 **8787**로 분리합니다.

처음 사용할 때는 **[WebGL 제작·게시 가이드](docs/webgl-guide.md)**를 따릅니다. 이번 변경과 테스트 범위는 [복제·전환 검증 보고서](docs/reports/2026-09-20-webgl-clone-validation.md)에 기록했습니다. 복제한 Android 호스트는 보관되어 있으나 Unity 6 기반 APK 빌드는 이번 검증 범위에 포함하지 않습니다.

| 프로젝트 | 역할 |
| --- | --- |
| [KimchilyCreator](KimchilyCreator/README.md) | FBX·TypeScript 편집, Unity 메뉴의 Build & Publish, QR 발행 |
| [KimchilySDK](KimchilySDK/README.md) | Creator·TypeScript·Lua 호환 UPM 패키지, 모바일 플레이어 |
| [KimchilyUnityRuntime](KimchilyUnityRuntime/README.md) | 공통 Unity 실행기와 Web 플레이어 빌더 |
| [KimchilyAndroid](KimchilyAndroid/README.md) | 네이티브 홈·QR 카메라·전체 화면 Unity 연결 |
| [KimchilyExpo](KimchilyExpo/README.md) | React Native 홈·QR·Unity WebView, Expo Go 시연 및 EAS 설정 |
| KimchilyWebApp | 브라우저 홈·QR 스캔·최근 입장·PWA |
| [KimchilyPublish](KimchilyPublish/README.md) | 로컬 개발용 월드 게시·QR 서버 |

Unity Editor 메뉴는 **Kimchily → Publish World**입니다. WebGL 게시 링크는 `/player/?manifest=...&sha256=...`이며, Android의 `kimchily://world` 링크와 구분합니다. 새 타깃의 실행기와 월드 번들은 같은 Unity 버전으로 빌드해야 합니다.

Web 서버 켜기·끄기와 iPhone 접속은 [WebGL 가이드](docs/webgl-guide.md), 모델과 애니메이션 클립 연결은 [애니메이션 안내](docs/animation-import-guide.md)를 따릅니다. 이전 [모바일 월드 사용 안내](docs/mobile-world-guide.md)의 Android 게시 절차는 원본 기준입니다.

마지막 게시 주소는 [게시 결과](KimchilyCreator/Artifacts/publish-result.json)에 있습니다. LAN 게시 서버가 실행 중이고 휴대전화에서 PC 주소에 접속할 수 있어야 합니다.

[이름 변경 범위와 검증 기록](docs/reports/kimchily-rebrand-2026-09-18.md)에 프로젝트 전환 내역을 기록합니다. 이전 빌드·게시 revision·검증 자료와 원본 백업은 보존하며, 원래 제작한 자산을 삭제하지 않습니다.
