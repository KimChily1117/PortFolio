# MoonSharp dependency provenance

- Package ID/version: `MoonSharp` `2.0.0` (interpreter assembly 2.0.0.0).
- Official package URL: https://api.nuget.org/v3-flatcontainer/moonsharp/2.0.0/moonsharp.2.0.0.nupkg
- Package page: https://www.nuget.org/packages/MoonSharp/2.0.0
- Retrieved: 2026-09-18.
- Original baseline: extracted `lib/net35-client/MoonSharp.Interpreter.dll` and companion XML documentation without modification. The original DLL is retained under `Tools~/Vendor/reference` for API regression checks.
- NuGet archive SHA-256: `6BE871A1C30B36503900737D0B2C5C769942286A6E865D96A2A6912CA45AE060`.
- Original NuGet DLL SHA-256: `97F3207A579D19235FC68F742A9E5F2431C5035F309B7B3A4371B48886442A87`.
- Deployed source-built DLL SHA-256: `FC1F287351AA4CB9E7A1BC004F9603B047E1FAC34A2B6DF3FD2283F9AFDBA823`.
- Official source archive: https://codeload.github.com/moonsharp-devs/moonsharp/zip/refs/tags/v2.0.0.0
- Source archive SHA-256: `8FE4C239E650CE576CC50C475E3F437829BC217158B41DE8637B59215FD32398`.
- Source change: only `PlatformAutoDetector.GetDefaultScriptLoader()` now creates an empty dictionary-backed `UnityAssetsScriptLoader`, avoiding the legacy Unity Resources reflection path during `Script` static initialization. Patch, exact original Compile list, source, license and rebuild script are preserved in `Tools~/Vendor`.
- Build target: `netstandard2.1`. Assembly name/version/public key and 1744 public API declarations match the original NuGet DLL. The companion XML documentation remains upstream's unmodified API documentation.
- Upstream license URL: https://raw.githubusercontent.com/moonsharp-devs/moonsharp/v2.0.0.0/LICENSE
- License SHA-256: `0D42AEDD4C41975A7C4E616703DB9423C4BEA0E5108E57E045809A2CB9FB386C`.
- License: BSD 3-Clause-style upstream license, preserved verbatim in `LICENSE-MoonSharp.txt`. MoonSharp 2.0.0 is not labeled MIT here.

Downloaded archives are not shipped in the UPM package. The managed interpreter is built from the preserved source with the one default-loader patch above. Copy the `Runtime/link.xml` preservation template into the consuming project's `Assets` directory before IL2CPP builds; package-local link.xml is not sufficient in Unity 2022.3.
