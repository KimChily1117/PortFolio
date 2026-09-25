# KimchilyWebGL 소스 이주

2026-09-25에 `E:\task\KimchilyWebGL`의 소스를
`E:\GItHub\PortFolio\KimchilyWebGL`로 복사했습니다. 원본과
`E:\task\Unity_Project`의 Unity 2022/Android 기준본은 그대로 보존했습니다.
Creator·SDK·UnityRuntime·Android·Expo·Publish·WebApp 사이 상대 경로도 유지했습니다.

## 포함한 파일과 제외한 파일

Unity `Assets`·`Packages`·`ProjectSettings`, 런타임 DLL·WebGL 플러그인,
WebApp의 jsQR, 문서·테스트, 손으로 작성한 `KimchilySDK/Build` 및 `Tools~`의
프로젝트 파일을 포함했습니다. 복사한 2,171개 파일(260,307,120바이트)은
복사 직후 각각 SHA-256으로 원본과 일치함을 확인했습니다. 이후 변경은
현재 경로 안내, Git 제외 규칙, 이 문서에 한정합니다.

캐시와 의존성 설치본(`Library`, `node_modules`, `.deps`), 빌드·APK·검증 산출물,
게시 상태·월드 저장소·토큰(`.local`), 로컬 환경 변수·인증서·자격 증명은 제외했습니다.
기존 QR과 게시 콘텐츠는 원본의 로컬 서버 상태에 남아 있습니다.
`preservation/source-manifest.json`과 과거 보고서는 당시 경로와 사실을 유지합니다.

`KimchilyUnityRuntime/Assets/Fixtures/Model.fbx`와 `.meta`는 샘플 씬 생성에
필요하여 보존했습니다. `Assets/Generated` 씬은 WebGL 빌더가 재생성합니다.
SDK의 선택적 구형 `ValidationProject` fixture/plugin 복사본은 제외했습니다.
그 검증 흐름은 원본 `Unity_Project/UnityKimchilyWorld` 자산을 요구하므로,
필요할 때 원본 기준본에서 준비해야 합니다. `prepare_runtime.py`,
`prepare_validation.py`, `tools/clone_baseline.py`는 과거 복제·검증용 도구이며
새 WebGL 작업본의 기본 설치 명령이 아닙니다.

## 새 경로에서 실행

PowerShell 작업 폴더:

```powershell
Set-Location 'E:\GItHub\PortFolio\KimchilyWebGL'
```

1. Node.js 22.13 이상을 준비합니다. Expo 의존성은
   `powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyExpo\tools\run.ps1 -Task install`
   로 설치합니다. `KimchilyExpo/.env.example`을 참고해 필요한 로컬 설정을 다시 만듭니다.
   이후 같은 실행기의 `-Task start`로 Expo Go 시연을 시작합니다.
2. Unity 6000.3.24f1 및 WebGL Build Support를 설치하고 `KimchilyCreator`를 엽니다.
   에디터 위치는 `tools/unity-version.json` 또는 `KIMCHILY_UNITY_EDITOR_ROOT`로 지정합니다.
   TypeScript 컴파일러 의존성은 다음 명령으로 준비합니다.

   ```powershell
   npm --prefix .\KimchilySDK\Packages\com.kimchily.typescript\Tools~\Compiler ci
   ```

3. Python 3.10 이상에서 `KimchilyPublish/tools/install.ps1`을 실행하면 qrcode/Pillow를
   로컬 `.deps`에 설치합니다. HTTPS가 필요하면 `requirements-tls.txt` 의존성도
   같은 `.deps`에 설치하고 [LAN HTTPS 안내](docs/lan-webapp-guide.md)에 따라 새 인증서를 준비합니다.
4. `KimchilyUnityRuntime/tools/export_webgl.ps1`로 플레이어를 빌드합니다.
   `Builds/WebGL`은 저장소에 없으므로 이 단계 전에는 `/player/`가
   `503 WEB_PLAYER_MISSING`을 반환합니다.
5. `KimchilyPublish/tools/start.ps1`로 게시 서버를 실행합니다.
   기본 포트 8788은 원본 WebGL 서버와 같으므로 동시에 실행하려면 원본 서버를
   정상 종료하거나 새 서버에 다른 포트를 지정합니다. 서버가 준비되면 Creator에서
   월드를 새로 Build & Publish합니다. 이주 과정에서는 서버를 실행하지 않았습니다.

`KimchilyWebApp`은 별도 npm 설치가 필요하지 않습니다. Android 네이티브 빌드는
이전 Unity 2022 기준 기록을 포함하므로 별도 검증이 필요합니다. 이주 과정에서
Unity/Android 빌드, 실제 휴대전화 실행, EAS 배포는 수행하지 않았습니다.

## 오프라인 검증

의존성 설치 없이 Node의 기존 WebApp·Expo·WebGL host 테스트와 Python 표준
라이브러리의 보존 검증 테스트를 새 경로에서 실행할 수 있습니다.

```powershell
node --test .\KimchilyWebApp\tests\*.test.mjs
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyExpo\tools\run.ps1 -Task test
node --test '.\KimchilyUnityRuntime\Tools~\WebChecks\host.test.cjs'
python -B .\tools\test_verify_baseline.py
```

이 명령의 `node`는 위의 지원 버전을 사용합니다. 기존 실행기에 있는 Codex
bundled Node 자동 선택은 이 PC의 오래된 시스템 Node를 우회할 수 있습니다.
게시 서버 전체 테스트는 qrcode/Pillow/cryptography 의존성을 준비한 뒤 별도로 실행합니다.

이주 검증 결과(Node 24.19.0, Python 3.13.1): WebApp 59개, Expo 28개,
WebGL host 25개, 보존 검증 8개로 총 **120개 테스트를 모두 통과**했습니다.
복사 후에도 원본 2,171개 파일의 SHA-256은 모두 처음 값과 일치했습니다.
