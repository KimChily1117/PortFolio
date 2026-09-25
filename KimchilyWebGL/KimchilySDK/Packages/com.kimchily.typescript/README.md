# Kimchily TypeScript

Write normal TypeScript classes with imports, typed fields and editor completion. The official TypeScript compiler checks the source and emits JavaScript. Unity imports a closed module graph into a `TypeScriptAsset`; the installed C# runtime executes it with Jint. TypeScript is not translated to Lua.

## Setup

This package targets Unity 2022.3.16f1 and requires `com.kimchily.creator`. Install it in both the creator project and the player. Node.js is needed only on the creator's computer. From this package's `Tools~/Compiler` directory, install the pinned compiler with:

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

Events above are local callbacks. Network rooms, replicated players, character controllers and ZEPETO modules are not implemented by this package. Unknown imports fail compilation. Standard TypeScript does not define C# event `+=` subscription; use `AddListener`/`RemoveListener`.

## Runtime and publication

- Interpreter: Jint 4.16.2, with Acornima 1.7.0 and its pinned managed dependency. The vendor folder records licenses, hashes and acquisition steps.
- Compiler: Microsoft TypeScript 5.9.3, pinned by `package-lock.json`.
- Published assets contain generated JavaScript, module IDs, source maps, field metadata and scripting API version 1. They do not contain newly loadable C# code.
- The first switch from Lua to TypeScript requires an updated player/APK. Later TypeScript and model changes within the installed API can be republished without rebuilding the APK.
- The existing Lua package remains available for older content. New authoring uses this TypeScript package.
- Preserve `Kimchily.TypeScript.Runtime`, `Jint`, `Acornima` and `System.Runtime.CompilerServices.Unsafe` in the player's `Assets/link.xml`. `Runtime/TypeScriptPreservation.xml.txt` is a merge template.
- A behaviour owns its VM and coroutines. Disable, destruction, reload and script faults cancel owned work. Scene startup faults are reported before the native host receives `WorldReady`.
- Host entry points and generator steps have statement/time/recursion limits. Module/source/array/routine counts are bounded and automatic CLR reflection, arbitrary module IO and string compilation are disabled. These controls do not make in-process execution a complete hostile-code isolation boundary; this development build accepts reviewed content.
- Web players omit Jint's per-thread allocation counter because Unity WebGL IL2CPP does not implement `GC.GetAllocatedBytesForCurrentThread`. Other execution and size limits remain enabled; Web builds do not enforce a per-VM memory quota. Editor and native targets retain the allocation constraint.

Imported JavaScript has no browser DOM, Node APIs or general npm loader. Only the explicit SDK modules and bundled relative imports are available. Promise-based asynchronous host APIs and a JavaScript debugger are not provided. Source maps are stored, while runtime exceptions may still identify generated JavaScript locations.

## Sources

- [TypeScript compiler](https://github.com/microsoft/TypeScript/tree/v5.9.3), Apache-2.0.
- [Jint 4.16.2](https://github.com/sebastienros/jint/releases/tag/v4.16.2), BSD-2-Clause.
- [Acornima](https://github.com/adams85/acornima/tree/v1.7.0), BSD-3-Clause.

The current workspace has passed Unity 2022.3.16f1 PlayMode 54/54 and EditMode 12/12 tests, built an ARM64 IL2CPP APK, and run two published TypeScript revisions on a Samsung SM-G955N with Android 9 without reinstalling the APK. Model motion, generator-controlled beacon visibility, return to native home and temporary-download cleanup were observed. Compiler, VM, facade, IDE-completion results and device evidence are recorded in the [2026-09-19 implementation report](../../../docs/reports/2026-09-19-typescript-runtime.md). The new QR was decoded with ZXing and its link opened in the app; a physical camera scan was not performed for this TypeScript revision. These results do not establish compatibility with every device or Unity version.
