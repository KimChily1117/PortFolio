# WebGL 월드 제작·게시 가이드

이 가이드는 **`E:\GItHub\PortFolio\KimchilyWebGL` 클론**에서 Unity 6으로 월드를 만들고, QR을 통해 브라우저로 접속하는 절차다. 원본 **`E:\task\Unity_Project`는 Unity 2022.3 Android 기준으로 유지**한다. 두 폴더의 프로젝트·SDK·게시 저장소를 섞지 않는다.

현재 Web 실행기·월드 번들 빌드와 서버 게시, PC Chrome에서 TypeScript·코루틴·월드 입장·이동·점프·재입장을 확인했다. Chrome 터치 에뮬레이션에서도 조이스틱의 걷기·달리기와 점프를 확인했다. **2026-09-20 사용자에게 iPhone 실기기에서도 정상 실행된다는 확인을 받았다.** 상세 결과와 확인 범위는 [전환 검증 보고서](reports/2026-09-20-webgl-clone-validation.md)를 확인한다.

## 1. 어떤 프로젝트를 열어야 하나요?

| 용도 | 경로 |
|---|---|
| 월드·모델·애니메이션 제작 | `E:\GItHub\PortFolio\KimchilyWebGL\KimchilyCreator` |
| 브라우저에서 월드를 실행하는 공통 플레이어 | `E:\GItHub\PortFolio\KimchilyWebGL\KimchilyUnityRuntime` |
| 공통 SDK | `E:\GItHub\PortFolio\KimchilyWebGL\KimchilySDK` |
| 클론 게시 서버 | `E:\GItHub\PortFolio\KimchilyWebGL\KimchilyPublish` |

Unity Hub의 **Projects → Add → Add project from disk**에서 **KimchilyCreator 폴더**를 추가한다. 상위 `KimchilyWebGL` 폴더를 Unity 프로젝트로 추가하는 것은 아니다. 열 때 Editor를 **6000.3.24f1**로 선택한다. 설치된 Editor는 `E:\UnityEngineCore\6000.3.24f1\Editor\Unity.exe`이고 **Web Build Support** 모듈이 필요하다.

평소 제작할 때는 Creator만 열면 된다. Runtime은 공통 웹 실행기를 빌드하거나 실행기 코드를 수정할 때 사용한다. Hub에서 원본과 클론을 구분할 때 프로젝트 이름뿐 아니라 **경로**를 확인한다. `.meta` 파일과 SDK의 상대 폴더 배치는 유지한다.

## 2. 공통 웹 실행기를 준비하기

새 환경에서는 최초 1회, 이후에는 SDK·브라우저 실행기·템플릿 코드를 바꿨을 때 다시 빌드한다. 모델 배치나 기존 TypeScript API를 사용하는 월드 수정만으로 매번 실행기를 다시 빌드할 필요는 없다.

PowerShell에서:

```powershell
Set-Location 'E:\GItHub\PortFolio\KimchilyWebGL'
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyUnityRuntime\tools\export_webgl.ps1
```

이 명령은 클론 Runtime을 Unity 6으로 열어 Web 대상으로 빌드한다. 결과는 **`KimchilyUnityRuntime\Builds\WebGL\index.html`**, 로그는 **`KimchilyUnityRuntime\Artifacts\webgl-build.log`**다. `-PrepareOnly`는 예제 씬만 준비하므로 브라우저 실행기 빌드를 대체하지 않는다.

Runtime을 직접 열었다면 **File → Build Profiles**에서 **Web**으로 전환한 뒤 **Kimchily → Web → Build Browser Player** 메뉴를 사용해도 된다. 해당 Runtime 프로젝트가 이미 Editor에서 열려 있으면 위 배치 명령을 동시에 실행하지 않는다.

결과 `index.html`을 파일 탐색기에서 더블클릭해 `file://`로 여는 대신, 아래 게시 서버가 제공하는 주소로 접속한다. 현재 개발 빌드는 WebGL 2, 네이티브 멀티스레딩 비활성화, 압축 비활성화 설정을 사용한다.

## 3. 게시 서버 켜기

Creator의 **Kimchily → Publish World** 창에서 **서버 켜기 · 연결**을 누르는 방법이 가장 간단하다. **서버 폴더 선택**이 필요하면 반드시 클론의 `E:\GItHub\PortFolio\KimchilyWebGL\KimchilyPublish`를 선택한다. 연결되면 PC 주소와 게시 인증 토큰이 자동으로 입력된다.

PowerShell에서도 같은 서버를 관리할 수 있다. 아래 명령은 클론 루트에서 실행한다.

```powershell
# 새 PC에서 게시 서버의 Python 의존성이 아직 없을 때 한 번 실행
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyPublish\tools\install.ps1

# 시작 / 현재 상태 / 종료
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyPublish\tools\start.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyPublish\tools\status.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyPublish\tools\stop.ps1
```

기본 포트는 **8788**이다. 기본 시작은 `0.0.0.0`에 바인딩하고 활성 LAN 주소를 찾아 휴대전화용 URL을 정한다. 원본 Android 개발 서버의 **8787**과는 별도다.

| 표시되는 주소 | 사용처 |
|---|---|
| Local URL, 예: `http://127.0.0.1:8788` | 같은 PC의 Unity 게시 창 **Server URL** |
| Phone/public URL, 예: `http://192.168.0.4:8788` | 휴대전화와 QR에서 접속할 주소. 실제 PC의 현재 LAN 주소를 사용 |

LAN 후보가 여러 개면 다음과 같이 **실제 PC 주소**를 선택한다. 예시의 숫자를 그대로 사용하는 설정은 아니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyPublish\tools\start.ps1 -LanAddress 192.168.0.4
```

직접 공개 주소를 지정할 때 지원하는 옵션은 다음과 같다. `-PublicBaseUrl`과 `-LanAddress`는 동시에 사용하지 않는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyPublish\tools\start.ps1 -BindAddress 0.0.0.0 -Port 8788 -PublicBaseUrl http://192.168.0.4:8788
```

주소나 포트를 바꾸려면 기존 클론 서버를 먼저 `stop.ps1`로 종료한 뒤 새 설정으로 시작한다. 다른 설정으로 실행 중인 서버가 있으면 도구는 설정을 조용히 덮어쓰지 않고 오류를 표시한다. 세 관리 명령 모두 `-Json`을 지원한다. 별도 상태 폴더를 쓰는 고급 설정에서는 `-StateDirectory`를 start/status/stop에 동일하게 전달한다.

토큰·게시 월드는 클론의 `KimchilyPublish\.local`에 저장된다. 토큰 값을 QR이나 메시지에 넣지 않는다. 서버를 종료해도 게시 파일은 남지만, 꺼져 있는 동안 새로운 QR 입장과 콘텐츠 다운로드는 되지 않는다.

## 4. Creator에서 월드 빌드·게시하기

1. 클론 **KimchilyCreator**를 Unity 6000.3.24f1로 연다.
2. **File → Build Profiles**에서 **Web**을 선택해 활성 플랫폼으로 전환한다. 처음에는 플랫폼 목록에서 Web을 추가해야 할 수 있다. 이 프로젝트는 **Built-in** 렌더 파이프라인을 유지한다.
3. 제작할 씬을 열고 모델·플레이어·애니메이션을 연결한 뒤 저장한다. 시작 씬은 `Assets/World/MyWorld.unity`다. 모델과 클립 설정은 [모델 애니메이션 연결 가이드](animation-import-guide.md)를 따른다. 그 문서의 Android 게시 절차 대신 이 문서의 **WebGL** 게시 절차를 사용한다.
4. **Kimchily → Publish World**를 연다. **World ID**를 정하고 **Entry Scene**에 해당 씬을 넣는다. 이 창의 **Build Target은 WebGL**로 선택한다. Unity Build Profiles의 **Web**과 이 필드의 **WebGL**은 같은 대상을 뜻한다.
5. **서버 켜기 · 연결**을 누른다. 이미 PowerShell로 실행했다면 **상태 확인 → 이 서버로 연결**을 사용한다. **Server URL**은 클론 서버의 `http://127.0.0.1:8788`인지 확인한다.
6. **Validate**로 누락 자산·미지원 C# 등을 확인한다. 모델의 외부 스크립트 때문에 막혔다면 **플레이어 모델 게시용 복사본 만들기**로 복사본을 연결한 후 씬을 다시 저장한다. 제거된 외부 스크립트의 물리·표정·IK 기능이 자동 복원되는 것은 아니다.
7. **Build & Publish**를 누르고 완료될 때까지 기다린다. 새 WebGL 번들을 만든 뒤 서버에 업로드하며, 성공하면 **Published URL**, **Launch Link**, QR이 표시된다.
8. **Open Published Page**로 QR 페이지를 열거나 **Copy Launch Link**로 실행 링크를 복사한다.

버튼이 비활성화돼 있다면 Play 모드인지, 작업이 진행 중인지, 서버·토큰이 연결됐는지, **활성 플랫폼과 Build Target이 둘 다 Web/WebGL인지** 확인한다.

**Publish Last Build**는 이미 완성된 마지막 빌드를 다시 업로드하는 기능이다. 씬이나 애니메이션·TypeScript를 수정했다면 **Build & Publish**로 새 빌드를 만들어야 한다. 같은 revision은 덮어쓰지 않는다.

Creator를 닫고 명령으로 작업할 경우에는 다음 실행기를 사용할 수 있다. 이미 같은 Creator 프로젝트가 열려 있으면 동시에 실행하지 않는다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyCreator\tools\build_webgl.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\KimchilyCreator\tools\publish_last.ps1
```

CLI 게시 응답은 `KimchilyCreator\Artifacts\publish-result.json`에 저장된다. GUI 게시 결과는 게시 창에서 확인한다.

## 5. iPhone에서 QR로 열기

아래 접속 경로를 안내한 뒤 사용자가 iPhone에서 정상 실행됨을 확인했다. 기능별 상세 검사와 성능 측정은 별도로 진행한다.

1. iPhone과 서버 PC를 서로 통신할 수 있는 **같은 Wi-Fi/LAN**에 연결한다. PC는 같은 공유기의 유선 LAN을 사용해도 된다.
2. PC에 게시 창 또는 **Published URL**의 QR을 표시한다.
3. iPhone의 **기본 카메라**로 QR을 비추고 링크를 누른다. Safari에서 연다. 메신저 안의 내장 브라우저로 열렸다면 Safari로 다시 연다.
4. Kimchily 시작 화면의 **월드 시작**을 누르고 실행기·월드 로딩을 기다린다. 시작 버튼은 사용자 입력이 필요한 브라우저 실행 흐름에 포함된다.
5. 월드가 준비되면 왼쪽 스틱으로 이동하고 오른쪽 영역을 드래그해 시점을 바꾼다. 점프 버튼과 상단 **나가기**로 동작을 확인한다.

화면은 휴대전화의 가로·세로 크기에 맞춰 배치된다. 가로로 돌려 플레이하는 것을 권장한다. Safari에서는 브라우저와 기기의 회전 잠금 설정에 따라 동작하며 앱처럼 가로 방향 강제를 보장하지 않는다.

별도 Android APK나 iPhone 앱을 설치하는 흐름이 아니다. PC에서는 같은 Launch Link를 브라우저에서 열어 **월드 시작**을 누른 뒤 WASD/방향키와 Space를 사용한다.

휴대전화에서 `127.0.0.1`은 PC 주소가 아니다. QR에는 **PC의 LAN 주소**가 들어 있어야 한다. PC에서 `/health`가 정상이어도 Wi-Fi 기기 격리·VPN·방화벽 때문에 휴대전화 연결은 막힐 수 있다. 서버 도구는 방화벽이나 공유기 설정을 자동 변경하지 않는다.

## 6. 버전과 외부 접속

- 원본 Unity 2022.3 Android 번들과 클론 Unity 6 WebGL 번들은 **서로 호환되지 않는다**. 원본 번들 파일이나 manifest의 플랫폼·버전 문자열만 바꾸지 않는다. 클론 Creator에서 다시 빌드해 새 revision으로 게시한다.
- Web 실행기와 월드 번들은 같은 Unity/SDK 버전으로 빌드해야 한다. SDK나 실행기를 바꿨다면 공통 Web 실행기와 영향받는 콘텐츠를 함께 갱신한다.
- 이전 QR은 이전 revision을 가리킨다. PC의 LAN 주소가 바뀌면 서버 공개 주소를 새로 설정하고 새 revision을 게시한다.
- 현재 `http://192.168...:8788` 구성은 **로컬 개발용**이다. 다른 장소·모바일 데이터에서 접속하려면 서버를 계속 접근 가능한 환경에 배치하고 **HTTPS와 안정적인 공개 주소**를 구성해야 한다. 설정의 `PublicBaseUrl`만 외부 주소로 바꾸는 것으로 HTTPS나 외부 접속이 생기지는 않는다.
- 현재 플레이어와 월드 manifest·번들은 **같은 origin**에서 제공하는 구조다. 원격 배포 때도 이 경로를 유지하고, 운영용 접근 제어·서버 구성을 별도로 준비한다.

## 7. 막혔을 때

| 증상 | 먼저 확인할 것 |
|---|---|
| Web 플랫폼을 선택할 수 없음 | Unity 6000.3.24f1의 Web Build Support 설치 여부 |
| 게시 버튼이 비활성화 | Play 종료, 서버 연결, Build Profiles의 Web와 게시 창의 WebGL 일치 |
| 게시됐는데 `WEB_PLAYER_MISSING` | 공통 실행기 `Builds/WebGL/index.html`이 있는지, Runtime 빌드가 완료됐는지 |
| PC에서는 열리고 iPhone에서는 접속 불가 | QR의 LAN 주소, 같은 네트워크, 서버 상태, 방화벽·기기 격리 |
| 월드 시작 후 오류 | 페이지 오류 문구와 브라우저 콘솔, Web 실행기/번들의 Unity·SDK 버전, 최신 공통 실행기를 빌드했는지 확인 |
| 모델·애니메이션이 예전 상태 | 최신 씬 저장 후 Build & Publish했는지, 새 revision QR인지 |
| 원본 프로젝트나 서버가 선택됨 | 경로가 `E:\GItHub\PortFolio\KimchilyWebGL`인지, 게시 포트가 8788인지 |

서버 세부 옵션과 로그 위치는 [게시 서버 안내](../KimchilyPublish/README.md), 원본 보존 검사는 [공통 개발 도구 안내](../tools/README.md)를 참고한다.
