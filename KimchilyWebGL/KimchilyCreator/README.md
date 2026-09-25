# Kimchily Creator — 모델·TypeScript·월드 게시

Unity에서 모델과 TypeScript 동작을 제작하고 Android 콘텐츠로 게시하는 프로젝트다. `.ts`는 공식 TypeScript 컴파일러로 검사한 뒤 JavaScript 모듈을 담은 Unity 자산으로 임포트하며, 앱에 포함된 Jint 실행기가 동작을 실행한다. 제작 코드는 [Character.ts](Assets/World/Character.ts)와 상대 경로로 가져오는 [Motion.ts](Assets/World/Motion.ts)를 기준으로 한다. 기존 Lua 패키지와 콘텐츠는 호환성 유지를 위해 남겨둔다.

현재 WebGL 작업본은 Unity **6000.3.24f1**, **WebGL Build Support**, **Built-in Render Pipeline**을 사용한다. 아래 Android 실기기 기록은 이전 기준본의 검증 이력이다. 이 폴더는 기존 `UnityKimchilyCreator`와 별도로 만든 제작 프로젝트이며, 실행 앱은 [KimchilyAndroid](../KimchilyAndroid/README.md), 게시 서버는 [KimchilyPublish](../KimchilyPublish/README.md)다.

TypeScript 컴파일·API 계약·자동완성과 Unity 2022.3.16f1의 **PlayMode 54개·EditMode 12개** 검사가 통과했다. ARM64 IL2CPP 앱을 **SM-G955N / Android 9**에 설치하고 TypeScript 게시 월드를 실행했다. `speed` 100→140, `blinkSeconds` 0.3→0.5로 바꾼 [revision 2](http://192.168.0.4:8787/w/my-first-world/20260918T182013596Z-1533bb5e)도 **APK 재설치 없이 같은 프로세스**에서 실행했다. FBX 회전·이동, generator의 beacon 활성/비활성 전환, 두 월드의 퇴장 후 홈 복귀·임시 다운로드 정리와 새 링크 재입장을 확인했다. 이번 QR은 실제 ZXing 해독과 같은 링크의 앱 입장으로 검사했으며 물리 카메라 촬영은 포함하지 않았다. APK·로그·화면 증거와 검사 범위는 [TypeScript 구현 기록](../docs/reports/2026-09-19-typescript-runtime.md)에 있다. 기존 Lua의 결과는 [이름 변경 기록](../docs/reports/kimchily-rebrand-2026-09-18.md)으로 보존한다. TypeScript 최초 도입에는 새 실행기를 포함한 APK가 필요하다.

## 빠르게 시작하기

1. 아래 명령으로 고정된 컴파일러를 준비한 뒤 Unity Hub에서 이 폴더를 연다. `Assets/World/MyWorld.unity`를 열고 Build Settings의 **Android → Switch Platform**을 적용한다. 처음 만드는 샘플은 **Kimchily → Create Starter World**, 기존 Lua 샘플 전환은 **Kimchily → Migrate Starter World to TypeScript**를 사용한다. 전환 도구는 원본 씬을 `Artifacts/typescript-migration`에 보관한다.
2. `My TypeScript Character`의 **Kimchily TypeScript Behaviour**에 `Assets/World/Character.ts`를 연결한다. `beacon`의 Inspector override를 켜고 씬의 beacon 오브젝트를 지정한다. 현재 revision 2의 `speed`와 `blinkSeconds`는 override를 끄면 클래스 기본값 140·0.5를 사용한다. VS Code에서 이 프로젝트 폴더를 열어 스크립트를 편집하고, Unity의 컴파일 오류와 Play 동작을 확인한 뒤 저장한다.
3. PC와 휴대폰을 같은 Wi-Fi에 연결하고 `Kimchily → Publish World`의 **서버 켜기 · 연결**을 누른다. LAN 주소와 게시 토큰이 자동으로 연결된다. 같은 창의 **상태 확인**, **서버 끄기**로 관리한다. [서버·모바일 월드 사용 안내](../docs/mobile-world-guide.md)에 명령줄 방식도 정리되어 있다.
4. World ID와 Entry Scene을 지정하고 Build Target을 **Android**로 설정한다. 다른 서버를 수동 연결할 때만 Server URL과 해당 서버의 Publisher Token을 입력한다. URL을 바꾸면 기존 토큰은 비워진다. 토큰은 Editor 세션 메모리에만 보관된다.
5. **Build & Publish**를 누른다. 검증과 Android 번들 빌드 후 ZIP 업로드가 진행된다. **Publish Last Build**는 마지막 빌드를 다시 검증해 게시한다. 성공하면 Published URL, App Link, QR이 표시된다. 같은 revision은 덮어쓸 수 없으므로 게시한 내용을 바꿀 때는 새로 빌드한다.
6. Unity가 포함된 Kimchily Android 앱을 설치하고, Creator 창의 **Copy App Link**로 복사한 링크를 앱의 **만든 월드로 입장 → 링크로 입장**에서 사용한다. 네이티브 단독 APK는 링크를 확인할 수 있지만 Unity 월드를 실행하지 못한다. 게시 페이지의 **Kimchily 앱에서 열기** 버튼도 같은 앱 링크를 사용한다.
7. 월드의 **나가기** 또는 Android 뒤로가기로 네이티브 홈에 돌아온다. 모델이나 TypeScript를 수정하고 다시 게시하면 새 revision과 링크가 만들어진다. 해당 TypeScript 실행기와 SDK가 포함된 APK에서는 지원 API 안의 후속 콘텐츠 변경을 새 게시본으로 전달할 수 있다.

```powershell
# 워크스페이스 루트에서 최초 한 번; lockfile의 TypeScript 5.9.3을 설치한다.
npm.cmd ci --prefix KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler --ignore-scripts --no-audit --no-fund
```

**Server URL과 public-base-url은 역할이 다르다.** 전자는 Editor의 업로드 주소이고, 후자는 QR·앱 링크에 기록되어 휴대전화가 사용하는 주소다. 일반 LAN 연결에서 후자를 `127.0.0.1`로 설정하면 휴대전화 자신을 가리키게 된다. USB 연결은 게시 서버 문서의 `adb reverse` 구성이 별도로 필요하다.

앱의 **월드 QR 스캔**으로 게시 페이지 또는 Creator 창의 QR을 촬영해도 같은 월드로 입장한다. 최초 카메라 권한 요청을 허용한다. 이름 변경으로 앱 ID가 `com.kimchily.app`, 링크가 `kimchily://world`로 바뀌었으므로 새 APK와 새로 게시한 QR을 사용한다. 기존 이름으로 설치된 앱과는 별도 앱이다.

## 모델과 TypeScript 편집

FBX 등 Unity가 임포트한 모델·프리팹을 Assets에 넣고 씬에 배치한다. 연결된 머티리얼·텍스처·애니메이션·중첩 프리팹은 의존성 수집 대상이다. Android와 Built-in 파이프라인에 맞는 셰이더를 사용하고, 문자열로 찾는 등 씬 참조에서 발견할 수 없는 리소스는 Creator 창의 Additional Assets에 명시한다. 임의 모델·셰이더의 기기 호환성은 해당 콘텐츠로 별도 확인해야 한다.

**Assets → Create → Kimchily → TypeScript Script**로 파일을 만들고, GameObject에 **Kimchily → TypeScript Behaviour**를 추가해 임포트된 `.ts` 자산을 연결한다. `.ts` 파일 자체가 Script Asset이며 컴파일된 JS를 수동 복사하지 않는다. 공개 `number`, `string`, `boolean`, `GameObject`, `Transform`, `Vector3` 필드를 Inspector에서 override할 수 있다. private 필드는 스크립트 내부 상태로 유지된다.

[현재 Character.ts](Assets/World/Character.ts)의 핵심 구조는 다음과 같다. 전체 파일에는 `Map<string, number>` 상태, 로그와 `./Motion`을 통한 상하 이동도 들어 있다.

```ts
import { KimchilyScriptBehaviour } from 'Kimchily.Script';
import { GameObject, Time, WaitForSeconds } from 'UnityEngine';

export default class Character extends KimchilyScriptBehaviour {
    public beacon!: GameObject;
    public speed: number = 140;
    public blinkSeconds: number = 0.5;

    Start(): void { this.StartCoroutine(this.Blink()); }
    Update(): void { this.transform.Rotate(0, this.speed * Time.deltaTime, 0); }

    private *Blink(): Generator<WaitForSeconds, void, unknown> {
        while (true) {
            this.beacon.SetActive(false);
            yield new WaitForSeconds(this.blinkSeconds);
            this.beacon.SetActive(true);
            yield new WaitForSeconds(this.blinkSeconds);
        }
    }
}
```

`beacon!`의 `!`는 TypeScript의 초기화 검사에 관한 표기이며 객체를 생성하거나 자동 연결하지 않는다. 이 샘플은 Inspector에서 beacon 참조를 지정해야 한다. 생성자·필드 초기화 다음에 Inspector override가 적용되므로 지정된 참조는 `Awake`·`OnEnable`·`Start` 등 lifecycle에서 사용한다.

타입 제안은 프로젝트의 `tsconfig.json`과 SDK [kimchily.d.ts](../KimchilySDK/Packages/com.kimchily.typescript/Typings~/kimchily.d.ts)가 제공한다. VS Code에서 `.ts`를 열고 **TypeScript: Select TypeScript Version → Use Workspace Version**을 선택하면 설치한 5.9.3을 사용한다. `this.transform.`에서 `Rotate`·`position` 등이, `this.`에서 `beacon`·`speed`가 제안된다. 상세 설정과 실제 LanguageService 검증은 [TypeScript 구현 기록](../docs/reports/2026-09-19-typescript-runtime.md)을 참고한다.

제공 API는 명시적인 GameObject/Transform 참조, Vector3, Time, Debug, 로컬 Event와 generator 코루틴이다. Vector3 getter는 복사본이므로 수정한 값을 Transform에 다시 대입한다. `yield null`은 다음 프레임, `yield new WaitForSeconds(...)`는 Unity의 시간 배율을 적용한 대기다. 비활성화·파괴·재로드·스크립트 오류 시 해당 Behaviour의 코루틴을 취소한다. 자세한 동작은 [TypeScript 패키지](../KimchilySDK/Packages/com.kimchily.typescript/README.md)를 참고한다.

Animator·임의 `GetComponent`·오브젝트 검색·파일/네트워크·DOM/Node API·일반 npm 로더·네트워크 Room/Player·캐릭터 컨트롤러는 제공하지 않는다. `Character`는 이 샘플의 클래스 이름이며 별도 아바타 SDK를 뜻하지 않는다. C# DLL이나 새로운 Unity API를 콘텐츠만으로 추가할 수 없고, 실행기 API를 늘릴 때는 APK도 업데이트해야 한다. 현재 실행 제한은 검토된 개발 콘텐츠를 위한 것이며 완전한 악성 코드 격리가 아니다.

기존 `.lua` 자산과 **Kimchily → Lua Behaviour**는 유지된다. Lua 월드를 계속 만들 때는 [Lua 패키지의 기존 API](../KimchilySDK/Packages/com.kimchily.scripting/README.md)를 사용한다. TypeScript와 Lua의 모듈·객체 API는 서로 다르다.

## 명령줄 빌드와 게시

아래 명령은 워크스페이스 루트에서 실행한다. `prepare_creator.py`와 `Kimchily → Create Starter World`는 처음 준비할 때 사용한다. 이미 있는 씬·모델·스크립트는 준비 도구가 덮어쓰지 않는다.

```powershell
python KimchilyCreator/tools/prepare_creator.py
# 기존 Lua 시작 씬을 전환할 때만 사용; 씬 백업을 남긴다.
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyCreator/tools/migrate_typescript.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyCreator/tools/build_world.ps1 -Target Android
powershell -NoProfile -ExecutionPolicy Bypass -File KimchilyCreator/tools/publish_last.ps1 -ServerUrl http://127.0.0.1:8787
```

`publish_last.ps1`은 UI와 같은 C# 게시 클라이언트를 사용하며, 토큰을 파일에서 읽어 ZIP 업로드와 QR 다운로드까지 완료한다. 게시 서버가 먼저 실행 중이어야 한다. 별도의 Python 게시 CLI는 `KimchilyPublish/tools/publish.py`다.

| 파일/폴더 | 내용 |
| --- | --- |
| `Artifacts/last-build.txt` | 마지막으로 성공한 콘텐츠 빌드 경로 |
| `WorldBuilds/<revision>/world.json` | 플랫폼·씬·번들·타입 요구사항 및 무결성 정보 |
| `Artifacts/publish-result.json` | 마지막 Editor 게시의 URL·앱 링크·manifest SHA |
| `Artifacts/editor-publish.log` | 실제 C# 업로드와 QR 이미지 처리 로그 |

콘텐츠는 Android·단일 진입 씬·Built-in·Unity 2022.3.16f1을 기준으로 한다. 게시 ZIP과 확장 결과는 각각 최대 256 MiB, 번들은 최대 64개다. 앱은 manifest SHA-256, 번들 크기·SHA-256·CRC, Unity/SDK/플랫폼 호환성을 검사한다. 로컬 HTTP는 개발 APK에서 허용하며 release 경로는 HTTPS를 요구한다. 현재 서버와 검증은 **로컬 LAN 개발 환경**이고, 인터넷 공개 HTTPS 운영 배포는 포함하지 않는다.

## 이름 변경 전의 실제 검증 기록 — 2026-09-18

아래 두 revision과 카메라 촬영 증거는 이름 변경 전에 수집한 원본 기록이다. 과거 APK·QR·manifest·로그는 재작성하지 않았다. 현재 이름으로 다시 검증한 결과는 [Kimchily 전환 기록](../docs/reports/kimchily-rebrand-2026-09-18.md)을 참고한다. 이전 revision의 SDK 타입명과 QR 규약은 현재 앱과 다르므로 현재 앱 테스트에는 새 게시 결과 `Artifacts/publish-result.json`을 사용한다.

Samsung **SM-G955N**에서 설치한 Unity 포함 APK를 유지한 채 다음 두 콘텐츠 revision을 실행했다. 최종 앱 프로세스 `19568`의 [실행 로그](../KimchilyAndroid/Artifacts/device-final-revision-switch.log)에 revision 1과 revision 2의 Lua 시작 메시지가 모두 기록되어 있어, 콘텐츠 변경을 위해 APK를 다시 설치하지 않은 흐름을 확인할 수 있다. [최종 기기 화면](../KimchilyAndroid/Artifacts/device-final-published-world.png)도 보존했다.

| 순서 | 콘텐츠 revision | 증거 |
| --- | --- | --- |
| 1 | `20260918T133601438Z-8930c1d0` | [Editor 게시 결과](Artifacts/publish-editor-v1.json), [QR PNG](Artifacts/world-qr-editor-v1.png), [실기기 화면](../KimchilyAndroid/Artifacts/device-published-v1.png) |
| 2 | `20260918T134207063Z-489f333e` | [Editor 게시 결과](Artifacts/publish-editor-v2.json), [QR PNG](Artifacts/world-qr-editor-v2.png), [실기기 화면](../KimchilyAndroid/Artifacts/device-published-v2.png) |

확인한 범위:

- Creator에서 Android 번들 빌드 → C# Editor 클라이언트 게시 → 실제 QR PNG 다운로드/표시.
- 게시 URL에서 받은 manifest 및 번들의 크기·SHA 일치.
- 실기기에서 FBX 모델 표시와 Lua 캐릭터 동작·beacon 코루틴 실행. [revision 1→2 로그](../KimchilyAndroid/Artifacts/device-published-v2.log)에 두 시작 메시지가 남아 있다.
- 퇴장 후 네이티브 홈에 **월드에서 나왔어요.** 표시. [홈 UI 기록](../KimchilyAndroid/Artifacts/device-home-after-publish.xml)과 퇴장 후 외부 캐시 내 월드 다운로드 디렉터리가 비어 있는 상태를 확인했다.
- 자동 검증: Unity 42개, 네이티브 25개, 게시 서버 13개 통과. [Unity 결과](../KimchilyUnityRuntime/Artifacts/runtime-playmode.xml), [네이티브 결과](../KimchilyAndroid/app/build/reports/tests/testDebugUnitTest/index.html), [서버 테스트](../KimchilyPublish/tests/test_server.py).

두 게시본의 QR은 앱과 동일한 ZXing 3.5.3으로 기본·축소·회전 검출을 통과했고, [두 번째 QR 검증 결과](../KimchilyAndroid/Artifacts/qr-decode-editor-v2.json)에서 링크 전체와 필드 일치를 확인했다. **카메라 촬영도 실제 SM-G955N에서 확인했다.** 23:03:53 QR 인식 후 23:03:54 revision 2 Lua 실행이 기록됐으며, 사용자가 인식 성공을 확인했다. [카메라 흐름 로그](../KimchilyAndroid/Artifacts/device-camera-investigation.log), [QR 입장 후 화면](../KimchilyAndroid/Artifacts/device-after-camera-scan.png)을 보존했다.

이 결과는 게시된 샘플과 해당 기기를 기준으로 한다. 모든 Lua API의 Android 경계 조건, 다른 기기·OS, 임의 셰이더/모델, 대용량 월드·장시간 사용·네트워크 장애·인터넷 HTTPS 배포까지 검증했다는 의미는 아니다.
