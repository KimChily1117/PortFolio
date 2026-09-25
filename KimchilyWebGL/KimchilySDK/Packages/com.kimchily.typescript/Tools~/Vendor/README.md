# Pinned JavaScript interpreter dependencies

`fetch.ps1` downloads official NuGet packages and extracts the existing signed DLLs without modification. Run with `powershell -NoProfile -ExecutionPolicy Bypass -File ./fetch.ps1`. `provenance.json` records package URLs and SHA-256 hashes of both package archives and deployed DLLs. NuGet nuspec files retain dependency and upstream commit metadata.

| Dependency | Version | Deployed framework | License |
|---|---|---|---|
| Jint | 4.16.2 | .NET Standard 2.1 | BSD-2-Clause |
| Acornima | 1.7.0 | .NET Standard 2.1 | BSD-3-Clause; included NOTICE retains upstream notices |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | .NET Standard 2.0 | MIT; included third-party notices |

Jint 4.16 uses Acornima, not the older Esprima parser. Acornima's .NET Standard 2.1 build requires Unsafe 6.0.0. Licenses and notices are also included beside the deployed DLLs under Runtime/Plugins.

These assemblies interpret compiled JavaScript as data. No runtime C# compilation, native engine, `AllowClr`, object reflection projection, CLR event conversion, filesystem/network module resolver or Node APIs are installed. A per-behaviour VM exposes a fixed operation facade through explicit JsValue values and ClrFunction callbacks. The Editor TypeScript compiler is a separate dependency and is not shipped as a Player runtime.

Unity WebGL players do not register Jint's `MemoryLimitConstraint`: its reset and check call `GC.GetAllocatedBytesForCurrentThread`, an unimplemented WebGL IL2CPP icall. Other statement/time/recursion/array/source limits remain enabled. There is no replacement per-VM allocation quota on Web. Native and Editor paths retain the existing allocation constraint; vendor DLLs are unchanged.

The host enforces finite statement/time/recursion/allocation checks, closed module graphs, source limits, frame-based generator limits and bounded cleanup. Jint remains an in-process interpreter: allocation and timeout checks are cooperative, including managed built-ins; they do not provide operating-system isolation or a hard heap ceiling. This SDK does not claim hostile-content isolation. CPU/allocation limits must be tested on the actual IL2CPP target. Automatic CLR interop is deliberately avoided, and managed tests alone do not prove Android compatibility.

Preserve Jint, Acornima, Unsafe and Kimchily.TypeScript.Runtime from stripping using the consuming project's Assets/link.xml, based on Runtime/TypeScriptPreservation.xml.txt. Unity package-local link.xml files are not the supported preservation location.

Verification commands:

```
dotnet build ../../Tools~/Compile/Kimchily.TypeScript.Runtime.csproj
dotnet run --project ../../Tools~/VmChecks/VmChecks.csproj
```

Unity PlayMode tests are in Tests/Runtime. The VM checks exercise the same TypeScriptVm source and same vendored .NET Standard binaries as Unity; their host is a bounded fake scene facade. Android ARM64 IL2CPP and a same-APK republish test remain required integration gates.
