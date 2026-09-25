# Local TypeScript compiler

Requires Node.js (tested with 18.14.0). Install the exact, lockfile-verified dependency:

```powershell
npm ci --ignore-scripts --no-audit --no-fund
npm test
```

No compiler is downloaded automatically when Unity imports an asset. Set `KIMCHILY_NODE_PATH`
if Node is installed outside the system path and `C:\Program Files\nodejs`.

```powershell
node compile.cjs --project-root E:/work/KimchilyCreator --entry Assets/World/Character.ts --output E:/work/KimchilyCreator/Library/KimchilyTypeScript/result.json
```

The CLI performs actual TypeScript 5.9.3 type checking. It emits ES2018 CommonJS and
source maps, independently of the IDE's `noEmit` tsconfig. The output contains
`apiVersion`, `entryModule`, `className`, `sourceHash`, `compilerVersion`,
`compiledSuccessfully`, `diagnostics`, `warnings`, `modules`, `fields`, and `dependencies`.
Module IDs are project-relative paths without `.ts`, such as `Assets/World/Character`.
Failed compilations contain no JS modules or Inspector field schema. Errors carry source
file/line/column locations. A helper module may succeed with an empty `className`; it
cannot be attached as a behaviour.

Static relative imports remain inside project `Assets`. The only external modules are
`Kimchily.Script` and `UnityEngine`, described by `../../Typings~/kimchily.d.ts`.
There is no Node, browser, network or ZEPETO multiplayer module. The module graph is
limited to 64 source files, 262144 characters per file, and 2 MiB total source text.
Compilation/type checking is not a substitute for the player's execution limits.
After emission, generated JavaScript is also limited to 262144 UTF-16 characters
per module and 1048576 UTF-16 characters across the graph, matching the player.
Exceeding either limit fails compilation and clears all emitted modules; source maps
are not executable code and are not counted in these runtime code budgets.

Supported public Inspector fields are number, string, boolean, GameObject, Transform,
and Vector3. Nullable object references work. Private/protected fields are excluded;
unsupported public fields and readonly fields retain their class defaults and produce
warnings. Runtime API/lifecycle names cannot be shadowed by Inspector fields.

Dependencies are pinned by package-lock.json. TypeScript is Microsoft software under
Apache License 2.0; its unmodified license is in `TYPESCRIPT-LICENSE.txt` and the installed
package includes `ThirdPartyNoticeText.txt`. No TypeScript source has been modified.
