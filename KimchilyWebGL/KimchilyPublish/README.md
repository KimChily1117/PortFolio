# Kimchily local publisher

Android 및 WebGL 월드 빌드 폴더를 검증해 불변 revision URL과 실제 QR을 제공하는 개발용 서비스입니다. ZIP 업로드에만 로컬 Bearer 인증을 요구합니다. 배포된 월드·QR·랜딩 페이지는 해당 서버에 접속할 수 있는 사람에게 공개됩니다. 운영 서비스용 사용자 계정·HTTPS 종료·검수·과금·CDN은 포함하지 않습니다.

이 폴더는 `E:\GItHub\PortFolio\KimchilyWebGL` 클론용입니다. 기본 포트는 **8788**이며 상태·토큰·월드 저장소는 이 폴더의 `.local`에 새로 생성됩니다. 원본 `E:\task\Unity_Project`의 8787 서비스 및 `.local`을 공유하지 않습니다.

## 설치와 실행

Python 3.10 이상이 필요합니다. 저장소 루트에서:

```powershell
KimchilyPublish\tools\install.ps1
KimchilyPublish\tools\start.ps1
KimchilyPublish\tools\status.ps1
# 종료할 때
KimchilyPublish\tools\stop.ps1
```

기본 실행은 **활성 Wi-Fi/Ethernet의 LAN IPv4를 찾아 `0.0.0.0:8788`에 바인딩**합니다. PC/Unity용 `Local URL`과 휴대전화/QR용 `Phone/public URL`을 구분해 출력합니다. 활성 물리 인터페이스에서 기본 경로가 있는 주소를 우선하고, route metric + interface metric이 가장 낮은 경로를 선택합니다. VPN·터널·loopback·link-local·가상 어댑터는 자동 선택에서 제외합니다. 우선순위가 같은 주소가 여러 개이거나 기본 경로 없이 후보가 여러 개면 후보를 표시하고 `-LanAddress` 선택을 요구합니다. 자동 선택할 주소가 없으면 오류를 알립니다.

```powershell
# 여러 LAN 후보 중 하나를 선택
KimchilyPublish\tools\start.ps1 -LanAddress 192.168.0.20
# 기존 수동 옵션도 유지: 프록시·가상 인터페이스 등은 명시적인 public origin 사용
KimchilyPublish\tools\start.ps1 -BindAddress 0.0.0.0 -Port 8788 -PublicBaseUrl http://192.168.0.20:8788
# PC 전용 또는 별도로 설정한 adb reverse 경로
KimchilyPublish\tools\start.ps1 -BindAddress 127.0.0.1 -PublicBaseUrl http://127.0.0.1:8788
```

휴대전화와 PC는 서로 접속 가능한 네트워크에 있어야 합니다. 도구는 방화벽·라우터 설정을 자동 변경하지 않습니다. USB loopback 방식은 `adb reverse tcp:8788 tcp:8788`을 별도로 설정해야 합니다. LAN 자동 선택과 서버 `/health` 확인은 휴대전화 연결 성공 자체를 보장하지 않으므로 실제 QR 입장을 확인하세요. QR origin은 **게시 시점** 설정으로 고정되며, PC 주소가 바뀌면 새 설정으로 서버를 다시 시작하고 새 revision을 게시해야 합니다.

`start.ps1`은 숨김 백그라운드 프로세스를 시작한 뒤 최대 10초 동안 프로세스 생존과 로컬 `/health`의 Kimchily 서비스 응답을 확인합니다. 준비 확인 뒤에만 성공을 알립니다. 시작 직후부터 `.local/server.pid`와 `.local/connection.json`에 관리 정보를 기록하므로 준비가 늦거나 실패한 프로세스도 `status`/`stop`으로 조사할 수 있습니다. 토큰은 상태 JSON에 포함하지 않습니다. 로그는 `.local/server.log`·`.local/server-error.log`입니다.

같은 설정으로 다시 시작하면 등록 PID의 Python·`server.py` 경로·데이터 디렉터리·시작 시각·바인딩/public URL과 `/health`를 확인하고 재사용합니다. 이전 `server.pid`만 있는 실행도 실제 명령줄을 내부 확인해 인계합니다. `BindAddress`·`Port`·`PublicBaseUrl`이 다르면 새 설정을 조용히 무시하지 않고 오류로 알립니다. 변경하려면 기존 서버를 명시적으로 종료한 뒤 시작하세요. 같은 상태 폴더의 시작/종료는 잠금으로 중복 실행을 막습니다.

`stop.ps1`은 **등록되고 동일성이 확인된 서버만 종료**한 뒤 실제 종료를 기다립니다. 포트 소유자·ADB·다른 Python 프로세스는 종료하지 않습니다. 이미 꺼진 서버는 성공으로 처리하며, 다른 프로세스로 재사용된 PID나 변조/불일치 상태는 오류로 거절합니다. 종료해도 게시된 월드·토큰·로그를 삭제하지 않아 다시 시작하면 기존 콘텐츠를 제공할 수 있습니다. `Listen`/`Bound` 포트 충돌 시에는 표시된 소유 PID·프로세스명과 로그를 확인하세요.

### Unity 연동용 JSON 계약

세 명령 모두 `-Json`으로 JSON 한 개만 표준 출력에 보냅니다. 정상 시작/종료와 정상 상태 조회(`running`/`stopped`)는 exit 0, 상태 이상이나 동작 실패는 exit 1입니다. GUI는 종료 코드와 JSON의 `status`·`message`를 함께 확인해야 합니다.

```powershell
KimchilyPublish\tools\start.ps1 -Json
KimchilyPublish\tools\status.ps1 -Json
KimchilyPublish\tools\stop.ps1 -Json
```

| 필드 | 의미 |
|---|---|
| `schemaVersion` | `1` |
| `status` | `running`, `stopped`, `unhealthy`(검증된 프로세스의 health 실패), `conflict`(PID/설정/포트 충돌), `error`(잘못된 상태·동작 실패) |
| `localUrl`, `publicUrl`, `bindAddress`, `port` | PC용 주소, QR용 origin, 서버 바인딩 정보 |
| `pid`, `processStartTimeUtc`, `processVerified`, `healthy` | 등록 프로세스와 해당 프로세스 검증·health 상태; 종료 시 PID는 null |
| `stateDirectory`, `serverPath`, `stdoutLog`, `stderrLog` | 상태·로그 위치; token 값은 없음 |
| `message`, `updatedAtUtc` | 현재 조회/동작 결과와 UTC 시각 |

`connection.json`은 마지막 저장 상태이며 서버가 외부에서 종료되면 낡을 수 있습니다. 실제 상태는 항상 `status.ps1 -Json`으로 다시 확인합니다. `-StateDirectory <폴더>`를 세 명령에 동일하게 지정하면 서버의 `--data-dir`와 PID·연결 정보·토큰·로그를 그 폴더로 분리합니다. 다른 서버와 동시에 실행하려면 `-Port`도 다르게 지정하세요.

Unity 게시 창의 **서버 켜기 · 연결**을 사용하면 주소와 `.local/token`이 자동으로 연결됩니다. **상태 확인**과 **서버 끄기**도 같은 창에서 사용할 수 있습니다. 다른 서버를 수동으로 연결할 때는 Server URL과 그 서버의 Publisher Token을 입력합니다. 토큰은 로그·manifest·QR에 포함되지 않습니다. 토큰을 변경하려면 서버를 중지한 뒤 token 파일을 교체/삭제하고 다시 실행합니다. `.local` 및 `.deps`는 버전 관리 대상에서 제외되어 있습니다. 전체 순서는 [모바일 월드 사용 안내](../docs/mobile-world-guide.md)를 참고하세요.

Unity 메뉴 **Kimchily → Creator SDK** 또는 **Kimchily → Publish World**를 열고 Android 또는 WebGL 타깃을 선택합니다. **Build & Publish**는 새 빌드를 만든 뒤 ZIP을 비동기로 전송하고, **Publish Last Build**는 마지막 빌드 폴더를 검증해 게시합니다. 빌드 폴더의 Unity 보조 `.manifest` 파일과 루트 번들은 업로드하지 않고 `world.json` 및 명시된 번들만 보냅니다. 성공하면 랜딩 URL·실행 링크·실제 QR 이미지가 표시됩니다. 두 플랫폼은 각각 새 revision을 게시하며, 같은 world/revision을 다른 플랫폼으로 덮어쓸 수 없습니다.

## API

Expo 앱과 브라우저 홈은 같은 게시 데이터를 사용합니다. 앱 실행·설치는 [Expo 안내](../KimchilyExpo/README.md), 브라우저 카메라용 HTTPS와 인증서 설치·서버 종료는 [LAN 웹앱 안내](../docs/lan-webapp-guide.md)를 참고하세요.

| 요청 | 동작 |
| --- | --- |
| `GET /health` | 서비스 상태, 인증 불필요 |
| `GET /` | 브라우저 월드 홈/PWA |
| `GET /api/worlds` | 게시된 WebGL 월드의 최신 revision 목록과 허용된 QR 출처 |
| `GET /api/resolve?url=…` | 등록된 출처의 월드 QR·링크를 검증하고 현재 서버의 실행 링크 반환 |
| `GET /manifest.webmanifest`, `GET /sw.js` | PWA 설치 정보와 앱 화면 캐시 |
| `GET /dev/setup` | 같은 Wi-Fi 테스트용 인증서 설치 안내 |
| `GET /dev/connection`, `GET /dev/ca.cer` | 공개 접속 정보·CA 인증서. 개인키·토큰은 제공하지 않음 |
| `POST /api/publish` | `Authorization: Bearer …`, `Content-Type: application/zip`, 고정 `Content-Length` 필요 |
| `GET /worlds/{worldId}/{revisionId}/world.json` | 업로드한 manifest bytes 그대로 제공 |
| `GET /worlds/{worldId}/{revisionId}/{fileName}` | manifest에 선언된 번들만 제공 |
| `GET /w/{worldId}/{revisionId}` | 플랫폼에 맞는 QR과 실행 링크가 있는 페이지 |
| `GET /qr/{worldId}/{revisionId}.png` | Android 앱 링크 또는 WebGL 브라우저 링크를 인코딩한 PNG |
| `GET /player/?manifest=…&sha256=…` | 같은 출처의 WebGL revision과 manifest 해시를 확인한 뒤 브라우저 플레이어 제공 |
| `GET /player/{path}` | 지정한 WebGL 빌드 디렉터리의 허용된 정적 파일 제공 |

QR은 오류정정 M, box size 8, border 4, mask 0으로 생성한다. 자동 마스크가 일부 링크에서 ZXing 3.5.3의 잘못된 finder 후보 선택을 유발해, 실제 게시 링크의 크기·회전 검출을 통과한 설정을 사용한다. 변경 배경과 검증 범위는 [Kimchily 전환 기록](../docs/reports/kimchily-rebrand-2026-09-18.md)에 있다.

게시 성공은 HTTP `201`이며 JSON 필드는 `worldId`, `revisionId`, `manifestUrl`, `manifestSha256`, `publishUrl`, `qrUrl`, `launchUrl`입니다. Android의 `launchUrl`은 `kimchily://world?manifest=<URL encoded absolute manifest URL>&sha256=<64 hex>`, WebGL은 `<publisher origin>/player/?manifest=<URL encoded absolute manifest URL>&sha256=<64 hex>` 형식입니다. 오류는 `{ "code": "…", "message": "…" }`; 인증 실패 `401`, 잘못된 ZIP/manifest `400`, 기존 revision `409`, 용량 초과 `413`, 잘못된 미디어 타입 `415`입니다.

세계·revision ID는 `[A-Za-z0-9_-]` 1–80자이고 Windows 예약 이름은 제외합니다. 업로드와 확장 결과 각각 최대 256 MiB, manifest 최대 1 MiB, 번들 최대 64개이며 이 버전은 정확히 하나의 진입 씬을 지원합니다. ZIP은 flat regular file만 허용하며 경로·중복·대소문자 충돌·심볼릭 링크·암호화·선언되지 않은 파일을 거부합니다. 모든 번들의 크기/SHA-256과 의존성 구조를 확인한 후 디렉터리 rename으로 원자 게시합니다. 동일 revision은 덮어쓸 수 없습니다. manifest bytes를 재작성하지 않아 QR의 SHA와 다운로드한 manifest의 SHA가 동일합니다.

이 개발 경로는 Android 및 WebGL 빌드를 게시합니다. 서버는 Unity 번들 내부의 C# 타입 존재 여부나 런타임 호환성을 실행 검증하지 않습니다. SDK의 portable type 검사는 Editor에서, Unity/SDK/platform/render pipeline 및 번들 무결성 검사는 해당 플레이어에서 수행합니다.

## WebGL 플레이어 제공

기본 정적 파일 위치는 `../KimchilyUnityRuntime/Builds/WebGL`이며 `index.html`, `player.js`, `host.js`, `player.css`, `Build/*` 등의 빌드 결과를 `/player/` 아래 제공합니다. 별도 위치는 Python 서버의 `--webplayer-dir <빌드 디렉터리>`로 지정할 수 있습니다. 빌드가 없으면 게시와 health는 동작하지만 `/player/`는 `503 WEB_PLAYER_MISSING`을 반환합니다. Unity 소스·DLL 업로드 API는 추가하지 않습니다.

`/player/`와 `/player/index.html`에서만 실행 쿼리를 허용합니다. `manifest`와 `sha256`은 각각 하나여야 하며 manifest는 같은 publisher origin의 `/worlds/{world}/{revision}/world.json`이어야 합니다. 서버는 해당 manifest가 WebGL이고 원본 바이트의 SHA-256이 링크와 일치하는지 검사합니다. 플레이어도 다운로드한 manifest 및 bundle 해시를 별도로 검사해야 합니다. 경로 이탈, 인코딩된 경로, 심볼릭 링크·junction, 디렉터리 목록, `.cs`·`.dll`·`.meta` 등의 소스 파일은 제공하지 않습니다.

`.wasm`은 `application/wasm`, `.js`는 `application/javascript`, `.data`는 `application/octet-stream`으로 제공합니다. `.gz`와 `.br`는 원래 파일 MIME에 각각 `Content-Encoding: gzip`/`br`을 붙이며 파일을 다시 압축하지 않습니다. `.unityweb`은 Unity의 JavaScript 압축 해제 경로를 위해 `application/octet-stream`과 Content-Encoding 없음으로 제공합니다. LAN HTTP 개발 빌드는 압축 Disabled 또는 gzip을 권장하며, Brotli의 브라우저 기본 해제는 HTTPS가 필요할 수 있습니다. [Unity 배포 문서](https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html)

교체 가능한 개발 플레이어 `/player/*`는 `Cache-Control: no-store`입니다. 불변 revision의 manifest·bundle·QR·랜딩 URL은 기존 1년 `immutable`을 유지합니다. 서로 다른 origin을 위한 CORS 허용은 추가하지 않습니다. 게시된 월드와 플레이어는 같은 Unity/SDK 버전으로 빌드해야 하며, 이전 엔진 버전의 플레이어를 보관하는 런타임 버전 관리 기능은 별도 작업입니다.

## 테스트

기존 빌드 폴더를 CLI에서 게시하고 응답 JSON을 파일로 저장할 수도 있습니다. 토큰은 파일에서 읽으며 출력하지 않습니다.

```powershell
python KimchilyPublish/tools/publish.py KimchilyCreator/WorldBuilds/<revision> --server-url http://127.0.0.1:8788 --output KimchilyPublish/.local/publish-result.json
```

```powershell
python -m unittest discover -s KimchilyPublish/tests -v
# 별도 임시 포트·상태 폴더에서 서버 관리 명령 검증
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyPublish/tools/test-server-management.ps1
```

테스트는 임시 저장소와 임시 loopback 포트에서 실제 HTTP 업로드·다운로드·hash·QR PNG·동시 게시·불변 revision·인증·ZIP 경로/중복/링크·용량·오류 시 원자성을 검증합니다. 상시 서버를 실행하지 않습니다.

서버 관리 회귀 검증은 별도의 서버를 시작해 준비 확인·재사용·설정 불일치·포트 충돌·시작 시각 변조·다른 프로세스 PID 보호·실제 종료·중복 종료·재시작을 확인합니다. 기본 `.local` 서버는 건드리지 않습니다. 결과와 임시 서버 로그는 `Artifacts/server-management/<실행 ID>`에 남기며 토큰을 출력하지 않습니다. Windows 프로세스 동일성 확인에 필요한 CIM 조회 권한이 없으면 관리 명령은 실패를 알리고 프로세스를 변경하지 않습니다.

실제 Unity Editor의 ZIP/UnityWebRequest/QR 경로는 다음 batch 진입점으로 검증할 수 있습니다. `-quit`을 넣으면 비동기 작업이 끝나기 전에 종료되므로 넣지 않습니다. 성공 시 응답 JSON을 저장하고 QR 디코딩 크기를 기록한 뒤 Editor를 종료합니다. 토큰 값은 command line에 넣지 않습니다.

```text
Unity.exe -batchmode -nographics -projectPath <KimchilyCreator absolute path>
  -executeMethod Kimchily.Creator.Editor.WorldPublisherBatch.Publish
  -publishBuildDirectory <completed Android or WebGL revision directory>
  -publishServerUrl http://127.0.0.1:8788
  -publishTokenFile <KimchilyPublish/.local/token absolute path>
  -publishResultFile <response JSON absolute path>
```
