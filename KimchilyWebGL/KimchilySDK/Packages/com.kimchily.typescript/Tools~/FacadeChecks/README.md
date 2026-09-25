# Facade and IDE contract checks

Run from the workspace root after installing the pinned TypeScript dependency in `Tools~/Compiler`:

```powershell
node 'KimchilySDK/Packages/com.kimchily.typescript/Tools~/FacadeChecks/run.cjs'
node 'KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler/node_modules/typescript/bin/tsc' -p 'KimchilySDK/Packages/com.kimchily.typescript/Tools~/FacadeChecks/tsconfig.json'
node 'KimchilySDK/Packages/com.kimchily.typescript/Tools~/FacadeChecks/ide-check.cjs'
```

The 17 Node checks evaluate the actual bundled bootstrap with a scene-host stub. They verify reference identity, constructor owner access, invalid constructors, value-copy vectors, actual host operation names/arguments, Inspector bindings, handle ownership, coroutine generators, local event limits/snapshots, and logging. They do not replace Jint, Unity lifecycle, IL2CPP, or device tests.

The strict TypeScript check includes the shipped sample, valid API usage, and negative assertions using `@ts-expect-error`. Missing expected errors fail compilation. The IDE check invokes TypeScript 5.9.3's real LanguageService and verifies `this.transform.` completions, script public-field completions, nullable references, typed parameters, and JSDoc. It saves `Artifacts/ide-check.json`; it does not require a running code editor.

## Runtime boundary

`Bootstrap.js.txt` evaluates to a factory, called with one plain JavaScript host object containing `call(operation, objectId, argsArray)`. It returns `modules`, `create(ctor, ownerId = 0)`, and `applyFields(instance, bindings)`.

The host returns only JavaScript primitives/objects. Scene wrappers use opaque IDs; GameObject and its Transform share the same ID. The constructor context binds the owner during `super()`, so field initializers can read the owner. Inspector values are assigned after construction and before lifecycle callbacks.

| Operation | Arguments | Result |
|---|---|---|
| `gameObject.getName` / `getActiveSelf` | `[]` | string / boolean |
| `gameObject.setName` / `setActive` | `[value]` | unused |
| `transform.getPosition` / `getLocalPosition` / `getLocalScale` / `getEulerAngles` | `[]` | `[x,y,z]` |
| Corresponding `transform.set*` | `[x,y,z]` | unused |
| `transform.translate` / `rotate` | `[x,y,z,space]`, World=0, Self=1 | unused |
| `time.deltaTime` | `[]`, ID 0 | number |
| `debug.log` / `logWarning` / `logError` | `[string]`, ID 0 | unused |
| `coroutine.start` | `[iterator]`, owner ID | positive integer handle |
| `coroutine.stop` | `[handle]`, owner ID | unused |
| `coroutine.stopAll` | `[]`, owner ID | unused |

Inspector bindings are an object keyed by field name, each value `{type, value}`. Types are `GameObject`, `Transform`, `Vector3`, `number`, `string`, and `boolean`; scene references accept an ID or null, and Vector3 accepts `[x,y,z]`. Names are ASCII identifiers up to 80 characters. Up to 128 bindings are accepted. Prototype names, owner accessors, coroutine APIs, and lifecycle names are reserved. Conversion is validated before values are assigned.

`WaitForSeconds` is a frozen JavaScript token `{__kimchilyWait: 'seconds', seconds: number}`. Null/undefined coroutine yields mean the next frame. Event is a local synchronous listener collection; it has no network or engine event subscription semantics. Standard JavaScript `Map` and generator syntax come from the interpreter.
