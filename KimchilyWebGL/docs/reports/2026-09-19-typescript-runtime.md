# TypeScript 제작·실행기 구현 기록 — 2026-09-19

새 월드는 표준 TypeScript로 작성한다. `import`, `class`, 공개 타입 필드, private `Map`, 동기 generator를 사용하며, 공식 TypeScript 5.9.3이 타입 검사와 ES2018 CommonJS 출력을 수행한다. Unity가 결과를 `TypeScriptAsset`으로 임포트하고 앱에 설치된 Jint 4.16.2가 JavaScript를 실행한다. 기존 Lua 패키지는 이전 콘텐츠를 위해 유지한다.

Unity 2022.3.16f1 **PlayMode 54/54·EditMode 12/12**, FBX 샘플의 TypeScript 전환·Android 콘텐츠 게시·ARM64 IL2CPP 앱 빌드를 확인했다. **SM-G955N / Android 9에서 같은 APK·같은 프로세스로 TypeScript revision 1→2를 실행**했고 FBX 회전·이동·generator beacon 전환·퇴장 후 다운로드 정리·새 링크 재입장을 확인했다. [통합 검증 JSON](../../KimchilyAndroid/Artifacts/typescript-verification.json)에 APK·게시·manifest 해시와 테스트 증거를 기록했다. 아래는 새 TypeScript의 증거이며 2026-09-18 Lua/FBX 실기기 결과와 구분한다.

이 보고서는 2026-09-19 새벽의 최초 TypeScript 검증 기록이다. 같은 날 이후의 게시 연결 복구와 초기화 시간 제한 수정·재검증은 [게시 복구 보고서](2026-09-19-publish-recovery.md)에 별도로 기록한다. 테스트 XML·현재 빌드 경로는 재실행 시 갱신될 수 있으므로 당시 시각·결과와 보존한 APK 해시를 함께 확인한다.

## 구성과 전달 흐름

| 단계 | 실제 파일·역할 |
|---|---|
| 작성 | [Character.ts](../../KimchilyCreator/Assets/World/Character.ts), [Motion.ts](../../KimchilyCreator/Assets/World/Motion.ts) |
| 타입 제안 | [kimchily.d.ts](../../KimchilySDK/Packages/com.kimchily.typescript/Typings~/kimchily.d.ts), 제작 프로젝트 tsconfig와 VS Code workspace SDK |
| 임포트 | `.ts` importer → TypeScript 5.9.3 strict 검사 → JS·source map·모듈 ID·공개 필드 메타데이터를 TypeScriptAsset에 저장 |
| 씬 연결 | Kimchily TypeScript Behaviour의 Script Asset과 Inspector override |
| 게시 | Creator의 Validate / Build & Publish → Android 씬·자산 번들 → 서버 revision·manifest·QR |
| 앱 | Kotlin QR/링크 수신 → 기존 OpenWorld 계약 → Unity 다운로드·검증·씬 로드 |
| 스크립트 실행 | Behaviour별 Jint VM → 제한된 JavaScript façade → 명시적인 C# Unity 호출 |

TypeScript 원본을 Lua로 바꾸지 않는다. 제작용 Node.js는 Android에 필요하지 않다. C# Runtime과 Jint는 앱에 미리 포함하며, 콘텐츠 번들이 새 C# DLL을 동적으로 추가하지 않는다. 따라서 최초 TypeScript 전환에는 새 APK가 필요하고 이후 설치된 API 범위의 모델·스크립트 변경을 새 revision으로 게시한다.

## 제작 시작과 기존 샘플 전환

Unity 2022.3.16f1, Android Build Support, Built-in Render Pipeline을 기준으로 한다. `KimchilyCreator`와 `KimchilyUnityRuntime` 양쪽에 `com.kimchily.creator` 및 `com.kimchily.typescript`가 필요하다. 컴파일러는 lockfile로 고정한다.

```powershell
# 워크스페이스 루트에서 실행
npm.cmd ci --prefix KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler --ignore-scripts --no-audit --no-fund
```

1. Unity에서 `KimchilyCreator`를 연다. 초기 샘플 생성은 **Kimchily → Create Starter World**다. 이미 Lua 샘플 씬이 있으면 **Kimchily → Migrate Starter World to TypeScript**로 전환한다. 전환 전에 원본 `MyWorld.unity`를 `Artifacts/typescript-migration`에 보관한다.
2. `Assets/World/MyWorld.unity`의 `My TypeScript Character`를 선택한다. **Kimchily TypeScript Behaviour**의 Script Asset은 `Character.ts`다.
3. `beacon : GameObject — Inspector override`를 켜고 beacon 오브젝트를 지정한다. 이 예제의 `beacon!`는 TypeScript 초기화 검사만 생략하므로 실제 객체 연결이 필요하다.
4. 현재 revision 2의 `speed`, `blinkSeconds`는 override를 끄면 클래스 초기값 140, 0.5를 사용한다. 켜면 Inspector 값으로 교체한다.
5. 스크립트를 저장하고 임포트 오류를 해결한다. Play에서 검증한 뒤 씬을 저장하고 **Kimchily → Publish World → Build & Publish**를 실행한다. 각 기기의 실행 결과는 아래 결과 칸에 별도로 기록한다.

새 파일은 **Assets → Create → Kimchily → TypeScript Script**로 만든다. GameObject에 **Kimchily → TypeScript Behaviour**를 추가하고 임포트된 `.ts` 자산을 연결한다. 자동 생성 JS를 수동 복사하거나 C# 컴포넌트로 변환할 필요가 없다. Node 경로를 찾지 못하면 제작 PC의 `KIMCHILY_NODE_PATH`를 지정한다.

상대 import는 프로젝트 `Assets` 안의 `.ts`로 제한한다. `import { sampleHeight } from './Motion'`처럼 사용하며 두 모듈이 함께 검사·게시된다. 외부 모듈은 현재 `Kimchily.Script`와 `UnityEngine`만 제공한다. 기존 tsconfig는 구성 메뉴가 덮어쓰지 않으므로 이미 파일이 있으면 include 경로를 확인한다.

## VS Code 타입 제안

VS Code에서 **KimchilyCreator 폴더 전체**를 연다. 단일 `.ts`만 열면 프로젝트 설정과 SDK 선언 파일을 놓칠 수 있다. 현재 [tsconfig.json](../../KimchilyCreator/tsconfig.json)은 `strict: true`, `noEmit: true`, ES2018/CommonJS, `types: []`를 사용하고 다음을 include한다.

```json
[
  "Assets/**/*.ts",
  "../KimchilySDK/Packages/com.kimchily.typescript/Typings~/kimchily.d.ts"
]
```

`noEmit`은 IDE 타입 검사 설정이다. Unity importer는 별도 컴파일 작업으로 게시할 JavaScript를 생성한다.

설치된 **VS Code 1.96.2**의 TypeScript 확장 설정 schema에서 다음 키의 지원을 확인했다. 프로젝트 [.vscode/settings.json](../../KimchilyCreator/.vscode/settings.json)에 이미 기록돼 있다.

```json
{
  "typescript.tsdk": "../KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler/node_modules/typescript/lib",
  "typescript.enablePromptUseWorkspaceTsdk": true
}
```

`.ts`를 연 상태에서 **TypeScript: Select TypeScript Version → Use Workspace Version**을 선택한다. 설정 경로만 지정하는 것과 해당 버전을 실제로 선택하는 것은 별도 단계다. 최신 VS Code 공식 문서는 같은 목적의 `js/ts.tsdk.path` 키를 안내하므로 VS Code를 바꿀 때는 설치 버전이 지원하는 키를 확인한다. [VS Code 공식 TypeScript 설정](https://code.visualstudio.com/docs/typescript/typescript-transpiling#_using-the-workspace-version-of-typescript)

확인할 제안은 `this.transform.`의 `Rotate`, `position`, `Translate`와 `this.`의 공개 필드다. Hover/매개변수 정보에서 `Rotate(x: number, y: number, z: number, relativeTo?: Space)`와 `speed: number`를 볼 수 있어야 한다. SDK 선언 파일의 JSDoc도 함께 전달된다. 이러한 편집 기능은 VS Code의 TypeScript 언어 서비스가 제공한다. [VS Code 공식 편집 안내](https://code.visualstudio.com/docs/typescript/typescript-editing)

실제 TypeScript 5.9.3 LanguageService를 호출한 [검증 결과](../../KimchilySDK/Packages/com.kimchily.typescript/Tools~/FacadeChecks/Artifacts/ide-check.json)에서 자동완성 목록·QuickInfo·JSDoc·nullable `GameObject | null`을 확인했다. VS Code 창을 자동 조작한 결과는 아니며 같은 TypeScript 언어 서비스 API를 통한 검증이다.

## 실제 Character.ts와 Inspector 필드

현재 [Character.ts](../../KimchilyCreator/Assets/World/Character.ts)의 핵심 부분이다. 전체 파일은 `./Motion`에서 높이 함수를 가져오고 private `Map<string, number>`로 내부 상태를 관리한다.

```ts
import { KimchilyScriptBehaviour } from 'Kimchily.Script';
import { GameObject, Time, WaitForSeconds } from 'UnityEngine';

export default class Character extends KimchilyScriptBehaviour {
    public beacon!: GameObject;
    public speed: number = 140;
    public blinkSeconds: number = 0.5;

    Start(): void { this.StartCoroutine(this.Blink()); }

    Update(): void {
        this.transform.Rotate(0, this.speed * Time.deltaTime, 0);
    }

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

이는 표준 TypeScript 클래스·접근 제한자·generator 구문이며 `KimchilyScriptBehaviour` 상속을 요구한다. 언어 문법은 [TypeScript 클래스](https://www.typescriptlang.org/docs/handbook/2/classes.html), [iterator와 generator](https://www.typescriptlang.org/docs/handbook/iterators-and-generators.html)를 따른다.

| 공개 필드 타입 | Inspector·실행 동작 |
|---|---|
| `number`, `string`, `boolean` | override를 켜면 Inspector 값, 끄면 클래스 초기값 |
| `GameObject`, `Transform` | 씬 참조를 지정; `GameObject | null`·`Transform | null`도 허용 |
| `Vector3` | 세 숫자를 지정하며 실행 시 독립적인 Vector3 값으로 연결 |
| private / protected 필드 | Inspector 메타데이터에서 제외, JS 내부 상태 유지 |
| 지원하지 않는 public 타입 / readonly 필드 | Inspector override에서 제외하고 컴파일러 경고; 클래스의 기본 동작 유지 |

필드 이름은 ASCII identifier 최대 80자이며 최대 128개다. `constructor`, `__proto__`, `gameObject`, `transform`, lifecycle 및 코루틴 API 이름은 바인딩 대상이 될 수 없다. 생성자·클래스 필드 초기화가 끝난 뒤 override를 적용하므로 지정된 다른 오브젝트는 lifecycle에서 사용한다. `this.gameObject`와 `this.transform`은 생성 중 `super()` 시점부터 소유 객체에 연결된다.

## 지원 API

[kimchily.d.ts](../../KimchilySDK/Packages/com.kimchily.typescript/Typings~/kimchily.d.ts)가 실제 제공 API의 기준이다. `UnityEngine` 이름은 아래 구현된 부분을 노출하는 모듈이며 Unity 전체 API를 자동 공개하는 의미가 아니다.

| 모듈·타입 | 현재 API |
|---|---|
| `Kimchily.Script.KimchilyScriptBehaviour` | 읽기 전용 `gameObject`, `transform`; `Awake`, `OnEnable`, `Start`, `Update(dt)`, `OnDisable`, `OnDestroy`; `StartCoroutine`, `StopCoroutine`, `StopAllCoroutines` |
| `Kimchily.Script.Coroutine` | 시작한 Behaviour에 속하는 불투명 취소 핸들 |
| `Kimchily.Script.Event<[...]>` | 로컬 동기 `AddListener`, `RemoveListener`, `Invoke`, `ListenerCount` |
| `UnityEngine.GameObject` | `name`, `activeSelf`, `SetActive(bool)`, `transform` |
| `UnityEngine.Transform` | `gameObject`; `position`, `localPosition`, `localScale`, `eulerAngles`; `Translate(x,y,z,Space)`, `Rotate(x,y,z,Space)` |
| `UnityEngine.Vector3` | `x/y/z`, zero/one/up/right/forward, magnitude/sqrMagnitude/normalized, clone/add/subtract/multiply/Normalize, Distance/Dot/Cross/Lerp |
| `UnityEngine.Time`, `Debug` | `Time.deltaTime`, `Debug.Log` / `LogWarning` / `LogError` |
| `UnityEngine.WaitForSeconds`, `Space` | 시간 배율을 적용하는 코루틴 대기, World=0·Self=1(Translate/Rotate 기본값) |

Transform의 Vector3 getter는 복사본이다. `this.transform.position.x = 1`만으로 객체가 이동하지 않는다.

```ts
const position = this.transform.position;
position.x = 1;
this.transform.position = position;
```

동기 generator에서 `yield null` 또는 `yield undefined`는 다음 프레임으로 진행하고, `yield new WaitForSeconds(seconds)`는 대기 후 재개한다. 현재 대기 값은 유한한 0~3600초다. `StartCoroutine(this.Blink())`의 반환 핸들을 `StopCoroutine(handle)`에 전달한다. 다른 Behaviour의 핸들을 중지할 수 없고 비활성화·파괴·재로드·오류 시 소유 코루틴이 취소된다. C# 코어가 처리할 수 있는 다른 Unity yield 타입이 TypeScript에도 모두 제공되는 것은 아니다.

Event는 네트워크 이벤트가 아닌 스크립트 내부 콜백이다. 기본 64개, 지정할 수 있는 최대 256개의 고유 listener를 받는다. 호출 시 listener 목록을 복사하므로 콜백 도중 추가·제거한 항목은 다음 호출부터 반영된다. listener 오류는 런타임 오류 경계로 전달한다.

```ts
import { Event } from 'Kimchily.Script';
const changed = new Event<[number]>();
const onChanged = (value: number): void => { /* 로컬 처리 */ };
changed.AddListener(onChanged);
changed.Invoke(1);
changed.RemoveListener(onChanged);
```

C#의 이벤트 `+=` 구문을 TypeScript 구독 문법으로 제공하지 않는다. `Map`, 클래스, arrow function, generator는 JS 언어 기능이다. 샘플의 `Character` 클래스는 아바타/캐릭터 제어 SDK나 ZEPETO Character API 구현을 뜻하지 않는다.

## 실행과 게시 경계

스크립트는 owner ID와 직렬화된 참조 ID를 통해 명시적인 host 호출만 한다. Unity CLR 객체나 reflection을 JS로 노출하지 않는다. 임의 `GetComponent`, 오브젝트 검색, Animator 제어, 파일·네트워크·DOM·Node·일반 npm loader, 네트워크 Room/Player, 복제·멀티플레이·캐릭터 컨트롤러는 구현 범위 밖이다. 알 수 없는 외부 import는 컴파일 오류가 된다.

VM에는 명령 수·시간·재귀·배열·메모리 등의 실행 제한을 설정하고 `eval`/문자열 컴파일·CLR interop·외부 모듈 IO를 차단한다. Behaviour당 코루틴은 최대 32개다. 컴파일러는 입력 그래프 최대 64개 파일을 검사하고, 생성 JS도 모듈당 262,144자·전체 1,048,576 UTF-16자 제한을 적용해 실행기와 맞춘다. 초과하면 생성 모듈을 성공 산출물로 내보내지 않는다. 이 제한을 완전한 악성 코드 프로세스 격리로 취급하지 않는다. 현재 배포 대상은 검토한 개발 콘텐츠다.

source map은 자산에 보관하지만 VS Code JavaScript 디버거 연결이나 TypeScript 원본 위치로의 완전한 런타임 예외 매핑은 제공하지 않는다. Promise 기반 비동기 Unity API는 없다. JS 기능의 범위와 이 SDK가 노출하는 host API를 구분한다.

Jint는 .NET용 JavaScript 인터프리터이며 클래스·Map/Set·generator를 지원한다. 이 SDK는 그 위에 제한된 Unity API를 구현한다. Jint 자체의 지원 목록이 Kimchily의 외부 IO 또는 Unity 전체 API 지원을 뜻하지 않는다. [고정 버전 Jint 설명](https://raw.githubusercontent.com/sebastienros/jint/v4.16.2/README.md)

플레이어의 `Assets/link.xml`에 `Kimchily.TypeScript.Runtime`, `Jint`, `Acornima`, `System.Runtime.CompilerServices.Unsafe`를 보존한다. 패키지의 `Runtime/TypeScriptPreservation.xml.txt`를 기존 SDK/Lua 보존 설정에 병합한다. 네이티브 앱은 Unity as a Library로 전체 화면 월드를 연다. [Unity 2022.3 Android 라이브러리 통합](https://docs.unity3d.com/2022.3/Documentation/Manual/UnityasaLibrary-Android.html)

## 검증 결과와 재현

| 검사 | 현재 기록 |
|---|---|
| 공식 TypeScript strict 타입 검사 | 샘플·정상 API·`@ts-expect-error` 오류 사례 통과 |
| Compiler CLI 회귀 | 19/19 통과; 실제 SDK 샘플 public 6개 추출, private Map/Event/Coroutine 제외, 생성 JS 한도 초과 거절 확인 |
| JS façade 계약 | Node 17/17 통과; 객체 ID·Vector3 복사·필드 바인딩·코루틴 핸들·Event snapshot/상한 확인 |
| 타입 제안 | TypeScript 5.9.3 LanguageService의 completion·QuickInfo·JSDoc 실검증 통과 |
| 실제 Creator 예제 | Character.ts와 Motion.ts 컴파일 성공, 공개 필드 beacon/speed/blinkSeconds 3개 |
| Jint 관리 코드 VM 테스트 | 22/22 통과 |
| Unity PlayMode | 54/54 통과, 실패·skip 0. [XML](../../KimchilyUnityRuntime/Artifacts/runtime-playmode.xml) |
| Unity EditMode importer·필드 검사 | 12/12 통과, 실패·skip 0. [XML](../../KimchilyUnityRuntime/Artifacts/runtime-editmode.xml) |
| Creator 씬·Android 콘텐츠 | FBX 씬 TypeScript 전환, Android 번들 빌드 및 새 revision 게시 성공 |
| 실제 게시 QR PNG | revision 1·2 모두 ZXing 3.5.3 기본 검출로 launchUrl 전체·Android WorldLink 필드 일치. [최신 검증 JSON](../../KimchilyAndroid/Artifacts/qr-decode-typescript-revision2.json), [최신 PNG](../../KimchilyAndroid/Artifacts/qr-typescript-revision2.png). 카메라 촬영 검증과는 별도 |
| Android ARM64 IL2CPP·통합 APK | 빌드 성공, IL2CPP 207개 노드·실패 0. 아래 크기·SHA-256 기록 |
| 실제 TypeScript 월드 입장·동작·퇴장·재입장 | SM-G955N / Android 9(API 28), 동일 APK·프로세스에서 revision 1→2. FBX 회전·이동·두 번째 퇴장까지 홈 복귀·캐시 0개·새 링크 재입장 확인 |
| Beacon 활성/비활성 화면 | revision 2 화면 0(on)·3(off) 직접 확인 및 5프레임 색상 영역 검사. 정확한 대기 주기 측정은 아님 |

```powershell
# 워크스페이스 루트. 아래 명령은 Unity/기기 실행 없이 검사한다.
node KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler/compile.cjs --project-root KimchilyCreator --entry Assets/World/Character.ts --output KimchilyCreator/Library/KimchilyTypeScript/typescript-character-check.json
npm.cmd test --prefix KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler
node KimchilySDK/Packages/com.kimchily.typescript/Tools~/FacadeChecks/run.cjs
node KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler/node_modules/typescript/bin/tsc -p KimchilySDK/Packages/com.kimchily.typescript/Tools~/FacadeChecks/tsconfig.json
node KimchilySDK/Packages/com.kimchily.typescript/Tools~/FacadeChecks/ide-check.cjs
```

컴파일 CLI의 `--output`은 제작 프로젝트의 `Library/KimchilyTypeScript` 내부만 허용한다. 위 명령을 실제 실행해 종료 코드 0, `compiledSuccessfully=true`, Character·Motion 모듈 2개, 공개 필드 3개, 오류·경고 0을 확인했다. 이 중간 컴파일 결과를 `Artifacts`나 원본 `Assets`에 직접 출력하지 않는다.

현재 실행 결과와 후속 기록:

- Unity 버전: 2022.3.16f1. Windows Editor 검사 결과 파일 시각은 2026-09-19 02:59:23 KST(PlayMode), 03:00:26 KST(EditMode)다.
- 테스트 XML: 위 두 파일에서 총 54/12개, 모두 Passed, 실패·skip 0을 확인했다.
- 씬 전환: [typescript-migration.log](../../KimchilyCreator/Artifacts/typescript-migration.log), [백업 폴더](../../KimchilyCreator/Artifacts/typescript-migration). 씬 변환·저장 뒤 Mono 종료 정리에서 대기해 해당 작업 프로세스를 종료했다. 전환 도구의 정상 종료 코드 0을 주장하지 않으며 실제 전환된 씬과 후속 콘텐츠 빌드 성공을 별도로 확인했다.
- Android 콘텐츠 빌드: [world-build-Android.log](../../KimchilyCreator/Artifacts/world-build-Android.log).
- 첫 TypeScript 게시 revision: `20260918T175908689Z-9a9a3b63`, manifest SHA-256 `caae8f69cd55642538b756768cbbb2a3d9a092abacd832cfc1583f1a63e795bb`. [보존한 revision 1 응답](../../KimchilyCreator/Artifacts/publish-result-typescript-revision1.json).
- 최신 revision 2: `20260918T182013596Z-1533bb5e`, manifest SHA-256 `93ab522af68bc05c7b11bd642eaff72aa7f01510ab9be7ee39e91f14db0b5435`. [현재 게시 응답](../../KimchilyCreator/Artifacts/publish-result.json), [게시 페이지와 QR](http://192.168.0.4:8787/w/my-first-world/20260918T182013596Z-1533bb5e). 주소는 로컬 LAN 개발 서버다. [기기에서 다운로드한 manifest](../../KimchilyAndroid/Artifacts/device-typescript-revision2-world.json)의 실제 SHA-256도 이 값과 일치한다.
- 당시 APK(별도 보존): [kimchily-unity-typescript-initial.apk](../../KimchilyAndroid/Artifacts/kimchily-unity-typescript-initial.apk), **38,284,262바이트**, SHA-256 **`B957A39C117B2B2FAB7C8033C3014CF6334029667F4FE2BA74D725FC2FD3C70B`**.
- 기기: **Samsung SM-G955N / Android 9 / API 28**. 설치 갱신 시각은 **2026-09-19 03:10:04 KST**, 두 revision의 앱 프로세스는 **1902**로 같다.

| 실기기 실행 | 스크립트 값·변경 | 기록 |
|---|---|---|
| 03:10:23 revision 1 Start | speed=100, blinkSeconds=0.3 | [로그](../../KimchilyAndroid/Artifacts/device-typescript-revision1.log) |
| 03:21:01 revision 2 Start | speed=140, blinkSeconds=0.5, 시작 메시지 revision 2 | [두 시작 메시지가 포함된 로그](../../KimchilyAndroid/Artifacts/device-typescript-revision2.log) |

revision 2는 TypeScript 공개 필드 기본값과 시작 메시지를 수정하고 다시 게시한 결과다. 두 실행 사이에 APK를 재설치하지 않았으며 동일 프로세스의 로그로 새 스크립트가 로드된 것을 확인했다. 첫 월드에서 나간 뒤 다운로드 임시 디렉터리가 비어 있는 것을 확인하고 새 링크로 들어갔다. 두 번째 퇴장도 [홈 UI](../../KimchilyAndroid/Artifacts/device-typescript-exit-ui.xml)와 `WorldDownloads` 항목 0개를 확인했고 설치 갱신 시각은 계속 03:10:04였다.

FBX 회전·이동은 revision 1의 [화면 0](../../KimchilyAndroid/Artifacts/device-typescript-revision1-0.png), [화면 1](../../KimchilyAndroid/Artifacts/device-typescript-revision1-1.png), [화면 2](../../KimchilyAndroid/Artifacts/device-typescript-revision1-2.png)를 직접 비교해 확인했다. revision 2는 [화면 0의 beacon on](../../KimchilyAndroid/Artifacts/device-typescript-revision2-0.png)과 [화면 3의 off](../../KimchilyAndroid/Artifacts/device-typescript-revision2-3.png)를 직접 확인했다. 5프레임의 cyan 영역 픽셀 수는 10,586·10,586·10,586·0·0이며 [관찰 기록](../../KimchilyAndroid/Artifacts/device-typescript-beacon-check.json)에 남겼다. generator의 깜빡임은 확인했지만 0.5초 주기를 정밀 측정한 결과는 아니다.

이번 TypeScript QR은 **실제 ZXing 기본 해독 + 동일 링크의 앱 입장**으로 검사했다. 물리 카메라 QR 촬영은 수행하지 않았다. 다른 기기·OS, 장시간 반복·메모리/프레임 성능의 정량 검증도 이 결과에 포함하지 않는다.

## 상류 구현과 라이선스

| 의존성 | 사용 목적·공식 자료 |
|---|---|
| TypeScript 5.9.3 | 실제 컴파일·타입 검사·LanguageService, Apache-2.0. [공식 소스](https://github.com/microsoft/TypeScript/tree/v5.9.3) |
| Jint 4.16.2 | JavaScript 인터프리터, BSD-2-Clause. [공식 버전](https://github.com/sebastienros/jint/releases/tag/v4.16.2) |
| Acornima 1.7.0 | Jint의 JS parser 의존성, BSD-3-Clause. [공식 소스](https://github.com/adams85/acornima/tree/v1.7.0) |

다운로드·해시·원본 라이선스는 TypeScript 패키지의 `Tools~/Vendor`와 `Tools~/Compiler`에 기록한다. SDK 코드, upstream 엔진, 실제 실행 검증 범위를 구분해 관리한다.
