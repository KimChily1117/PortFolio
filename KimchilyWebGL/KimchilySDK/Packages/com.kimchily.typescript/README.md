# Kimchily TypeScript

Write normal TypeScript classes with imports, typed fields and editor completion. The official TypeScript compiler checks the source and emits JavaScript. Unity imports a closed module graph into a `TypeScriptAsset`; the installed C# runtime executes it with Jint. TypeScript is not translated to Lua.

## Setup

Version 0.3.0 targets Unity 6 and requires `com.kimchily.creator` 0.1.0 and `com.kimchily.networking` 0.4.0. Install it in both the creator project and the player. Node.js is needed only on the creator's computer. From this package's `Tools~/Compiler` directory, install the pinned compiler with:

```powershell
npm ci --ignore-scripts --no-audit --no-fund
```

Use **Kimchily → TypeScript → Configure Type Completion** to create a project `tsconfig.json` if one does not exist. The supplied `KimchilyCreator` already has a configuration and a VS Code workspace TypeScript SDK path. Open that whole project folder in the editor; its `Typings~/kimchily.d.ts` reference supplies completion and parameter types.

## Authoring

1. Create a script with **Assets → Create → Kimchily → TypeScript Script**.
2. Add **Kimchily → TypeScript Behaviour** to a GameObject and assign the imported `.ts` asset.
3. Public `number`, `string`, `boolean`, `GameObject`, `Transform` and `Vector3` fields appear in the Inspector. Enable an override to replace the class initializer; assign scene objects with Unity's object picker. Private state stays inside the script.
4. Enter Play mode. Save the scene and use **Kimchily → Publish World → Build & Publish**. Compiler errors prevent publication. Relative `.ts` imports are checked and bundled with the entry module.

```ts
import { KimchilyScriptBehaviour } from 'Kimchily.Script';
import { GameObject, Time, WaitForSeconds } from 'UnityEngine';

export default class RotatingObject extends KimchilyScriptBehaviour {
    public lamp!: GameObject;
    public speed: number = 45;

    Start(): void {
        this.StartCoroutine(this.Blink());
    }

    Update(): void {
        this.transform.Rotate(0, this.speed * Time.deltaTime, 0);
    }

    private *Blink(): Generator<WaitForSeconds, void, unknown> {
        while (true) {
            this.lamp.SetActive(false);
            yield new WaitForSeconds(0.5);
            this.lamp.SetActive(true);
            yield new WaitForSeconds(0.5);
        }
    }
}
```

Supported lifecycle methods are `Awake`, `OnEnable`, `Start`, `Update`, `OnDisable` and `OnDestroy`. Constructor/field initializers run before Inspector overrides; use lifecycle methods to read assigned scene references. `Update` can also receive `deltaTime: number`.

The declaration file is the API contract. It covers explicit GameObject/Transform access, vector values, time, diagnostics, local typed events and coroutine handles. It does not declare the whole Unity engine. Transform vector getters return copies: change a vector and assign it back. `Map<string, T>`, classes, arrow functions and synchronous generators are JavaScript language features.

```ts
import { Event } from 'Kimchily.Script';
const joined = new Event<[string]>();
joined.AddListener((userId: string) => { /* local callback */ });
joined.Invoke('sample-player');
```

Events above are local callbacks. Shared game snapshots use the separate `Kimchily.Network` module described below. Character controllers and transport remain native SDK responsibilities; ZEPETO modules are unavailable. Unknown imports fail compilation. Standard TypeScript does not define C# event `+=` subscription; use `AddListener`/`RemoveListener`.

## 게임 규칙과 화면을 TypeScript로 작성하기

버전 0.3.0에서는 고정 포털용 `enableGame/startRound/replay`를 제거했다. `Kimchily.Network`는 등록된 서버 TS 스크립트 ID·SHA-256과 JSON 액션을 전달하고, 서버 TS가 계산한 상태를 읽는 범용 API다. C#은 연결·정체성·게임 방·실행 예산·동기화를 담당하며 게임 이름·발판·시간·승리 조건을 알지 못한다. 규칙 코드는 서버의 등록 목록에 별도로 설치한다. 클라이언트가 소스 코드를 업로드하거나 임의 파일을 실행하지 않는다.

`Kimchily.UI`의 `Hud`는 Unity TMP 화면에 표시할 텍스트·진행률·색·버튼을 TS에서 정한다. 네이티브 버튼은 VM을 직접 호출하지 않고 문자열 ID를 저장한다. TS는 다음 `Update`에서 ID를 꺼내 필요한 액션을 보낸다.

```ts
import { KimchilyScriptBehaviour } from 'Kimchily.Script';
import { Room } from 'Kimchily.Network';
import { Hud } from 'Kimchily.UI';

interface MyGameState { phase: string; progress: number; }

export default class GamePresentation extends KimchilyScriptBehaviour {
    // Inspector에 서버에 등록한 정확한 ID와 컴파일된 규칙의 SHA-256을 지정한다.
    public serverScriptId: string = '';
    public serverScriptHash: string = '';

    Start(): void {
        Room.useGame(this.serverScriptId, this.serverScriptHash);
    }

    Update(): void {
        // 표시 내용 변경 시 오래된 클릭을 지우므로 입력부터 소비한다.
        const action = Hud.takeAction();
        if (action) Room.sendAction(action, {});

        const room = Room.getState<MyGameState>();
        const game = room.game?.state;
        Hud.showPanel({
            eyebrow: '함께 만드는 월드',
            title: room.connected ? '친구들과 함께 준비해요' : '서버 연결 중',
            body: game?.phase ?? '게임 상태를 기다리는 중입니다.',
            progress: game?.progress ?? 0,
            accent: '#72C6AE',
            action: room.connected ? { id: 'start', label: '시작', enabled: true } : null
        });
    }

    OnDisable(): void { Hud.hide(); }
}
```

`Room.useGame(scriptId, scriptHash)`는 같은 게시 월드·버전·방에서 등록된 규칙에 참여한다. 아직 접속 중이면 요청을 기억한다. ID와 액션 이름은 ASCII 영문·숫자·`_`·`-` 1..80자, 해시는 소문자 16진수 64자다. `Room.sendAction(action, payload?)`의 생략한 payload는 `null`이며 UTF-8 1,024바이트 이내 JSON 값만 허용한다. 반환 boolean은 네이티브 클라이언트의 요청 접수 여부로, 서버 게임의 성공을 뜻하지 않는다.

`Room.getState<T>()`는 `{connected,selfId,players,game}`를 반환한다. `game`은 대기 중 `null`, 수신 후 `{scriptId,scriptHash,version,state:T}`다. 전송용 `stateJson` 문자열은 브리지에서 파싱하여 `state`로 제공한다. 참가자와 게임 상태는 깊게 동결된 복사본이며 동일 JSON은 VM 안에서 재사용한다. 조회는 네트워크 요청이나 UI 생성을 하지 않는다. 제네릭 `T`는 제작자의 타입 선언이므로 게임별 런타임 스키마 검증을 대신하지 않는다.

서버 상태는 JSON 객체, 최대 UTF-8 16KiB·깊이 12·1,024값으로 제한한다. 전체 방 JSON은 65,536문자 이하이며 최대 8명의 참가자를 포함한다. 함수·getter·`toJSON`·순환 참조·Unity 참조를 액션과 HUD에 전달할 수 없다. 브리지는 평범한 JSON 값의 복사본만 호스트에 넘긴다. 브라우저·Node·CLR reflection이나 일반 소켓 API는 제공하지 않는다.

HUD는 각 `KimchilyTypeScriptBehaviour`가 소유한다. `eyebrow` 48자, `title` 96자, `body` 1,200자, 버튼 `label` 48자까지 허용하며 title·버튼 label은 비어 있을 수 없다. 진행률은 0..1, 강조색은 `#RRGGBB`, 표시 DTO는 UTF-8 4KiB 이하이다. 긴 본문은 작은 화면에서 말줄임 처리되므로 게임 안내는 짧게 작성한다. `Hud.hide()`는 화면과 대기 입력을 지운다. TS의 정리 콜백이 예외를 던져도 C# 호스트가 비활성화·오류·재로드·파괴 때 강제로 정리한다.

기존 TypeScript 0.2.0 고정 프리셋 콘텐츠는 새 `Room` API로 다시 작성해야 한다. 최초 이관에는 Networking 0.4.0·TypeScript 0.3.0을 포함한 공통 실행기가 필요하다. 이후 같은 API 안의 클라이언트 연출·모델은 월드 콘텐츠로, 서버 규칙은 서버 등록 자산으로 각각 갱신한다. 규칙을 수정하면 새 컴파일 결과의 해시로 클라이언트 참조도 맞춰야 하며, 임의 서버 파일을 기존 방에 즉시 덮어쓰는 기능은 아니다.

## Runtime and publication

- Interpreter: Jint 4.16.2, with Acornima 1.7.0 and its pinned managed dependency. The vendor folder records licenses, hashes and acquisition steps.
- Compiler: Microsoft TypeScript 5.9.3, pinned by `package-lock.json`.
- Published assets contain generated JavaScript, module IDs, source maps, field metadata and scripting API version 1. They do not contain newly loadable C# code.
- The first switch from Lua to TypeScript requires an updated player/APK. Later TypeScript and model changes within the installed API can be republished without rebuilding the APK.
- The existing Lua package remains available for older content. New authoring uses this TypeScript package.
- Preserve `Kimchily.TypeScript.Runtime`, `Kimchily.Networking`, `Jint`, `Acornima` and `System.Runtime.CompilerServices.Unsafe` in the player's `Assets/link.xml`. `Runtime/TypeScriptPreservation.xml.txt` is a merge template. Networking also supplies its TMP/font build resources.
- A behaviour owns its VM and coroutines. Disable, destruction, reload and script faults cancel owned work. Scene startup faults are reported before the native host receives `WorldReady`.
- Host entry points and generator steps have statement/time/recursion limits. Module/source/array/routine counts are bounded and automatic CLR reflection, arbitrary module IO and string compilation are disabled. These controls do not make in-process execution a complete hostile-code isolation boundary; this development build accepts reviewed content.
- Web players omit Jint's per-thread allocation counter because Unity WebGL IL2CPP does not implement `GC.GetAllocatedBytesForCurrentThread`. Other execution and size limits remain enabled; Web builds do not enforce a per-VM memory quota. Editor and native targets retain the allocation constraint.

Imported JavaScript has no browser DOM, Node APIs or general npm loader. Only the explicit SDK modules and bundled relative imports are available. Promise-based asynchronous host APIs and a JavaScript debugger are not provided. Source maps are stored, while runtime exceptions may still identify generated JavaScript locations.

## Sources

- [TypeScript compiler](https://github.com/microsoft/TypeScript/tree/v5.9.3), Apache-2.0.
- [Jint 4.16.2](https://github.com/sebastienros/jint/releases/tag/v4.16.2), BSD-2-Clause.
- [Acornima](https://github.com/adams85/acornima/tree/v1.7.0), BSD-3-Clause.

The original 0.1.0 package passed Unity 2022.3.16f1 PlayMode 54/54 and EditMode 12/12 tests, built an ARM64 IL2CPP APK, and ran two published TypeScript revisions on a Samsung SM-G955N with Android 9 without reinstalling the APK. Model motion, generator-controlled beacon visibility, return to native home and temporary-download cleanup were observed. Compiler, VM, facade, IDE-completion results and device evidence are recorded in the [2026-09-19 implementation report](../../../docs/reports/2026-09-19-typescript-runtime.md). The QR was decoded with ZXing and its link opened in the app; a physical camera scan was not performed for that revision. This historical result does not establish Android compatibility for the new Unity 6 networking integration.
