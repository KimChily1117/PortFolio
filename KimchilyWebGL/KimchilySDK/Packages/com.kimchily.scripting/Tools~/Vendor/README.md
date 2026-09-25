# MoonSharp 2.0.0.0 source build

This directory preserves the official `v2.0.0.0` interpreter source files used by the original .NET 3.5 client build, with one reviewed source patch. `Tools~` is ignored by Unity's asset compilation.

## Why the patch is needed

The original `Script` static constructor calls `PlatformAutoDetector.GetDefaultScriptLoader()` before a caller can set `Script.DefaultOptions`. On Unity, the NuGet DLL creates `UnityAssetsScriptLoader()` and reflects the legacy `UnityEngine.Resources, UnityEngine` and `UnityEngine.TextAsset, UnityEngine` names. In the Android IL2CPP player that path emitted `Error initializing UnityScriptLoader : NullReferenceException` even though Kimchily's explicit `LoadString` scripts continued running.

`memory-only-loader.patch` changes only `GetDefaultScriptLoader()` to construct `UnityAssetsScriptLoader(new Dictionary<string,string>())`. This supported constructor performs no Resources scan, reflection, or filesystem access. Scripts remain supplied explicitly as validated TextAsset contents. No Lua API has been added and no runtime error messages are suppressed.

## Build and verify

From the workspace root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'KimchilySDK/Packages/com.kimchily.scripting/Tools~/Vendor/build.ps1'
```

The script builds the interpreter for `netstandard2.1`, copies it to `Runtime/Plugins`, and checks its assembly identity and 1744 public declarations against the official NuGet DLL. It also exercises first-initialization loader selection, instruction preemption, callback yields and blocked libraries. The API baseline in `reference` is retained only for this regression check; Unity does not import it.

The exact 247 Compile items come from the preserved upstream `MoonSharp.Interpreter.net35-client.csproj` and are listed in `SourceItems.props`. Unused sibling projects and resources are omitted. The source's original assembly version and public repository signing key preserve `MoonSharp.Interpreter, Version=2.0.0.0, PublicKeyToken=921e73ce94aa17f8`; the .NET target changes from the original `net35-client` to `netstandard2.1` for Unity 2022.3.

## Restore the upstream baseline

- Official source archive: https://codeload.github.com/moonsharp-devs/moonsharp/zip/refs/tags/v2.0.0.0
- Archive SHA-256: `8FE4C239E650CE576CC50C475E3F437829BC217158B41DE8637B59215FD32398`.
- Extract the Compile entries listed in the original `src/MoonSharp.Interpreter/MoonSharp.Interpreter.net35-client.csproj`, the project itself, `src/keypair.snk`, and the root `LICENSE`.
- Place the interpreter files under `source/MoonSharp.Interpreter` and the signing key at `source/keypair.snk`.
- Apply `memory-only-loader.patch` relative to this directory, then run `build.ps1`.
- The downloaded archive is kept outside the UPM package at `KimchilySDK/Artifacts/vendor/moonsharp-v2.0.0.0.zip` when available. It is not required for normal rebuilds.

Original NuGet package, original and rebuilt DLL hashes, license, and source references are documented in `Runtime/Plugins/PROVENANCE.md`. Android IL2CPP runtime validation remains separate from these managed checks.
