# Unity 6 clone development tools

`unity-version.json` pins the Editor and changeset for this clone. Its optional
`editorRoot` records this machine's installation at `E:/UnityEngineCore/6000.3.24f1/Editor`.
PowerShell runners prefer explicit `-UnityEditor`, then `KIMCHILY_UNITY_EDITOR`, then
`KIMCHILY_UNITY_EDITOR_ROOT`, then this local configuration. Without a configured path,
they use the standard Unity Hub installation directory. The helper checks the executable's
product version and rejects projects outside this clone. It does not change Hub
installation settings or launch an Editor by itself.

```powershell
# Read-only preservation and local-package checks; optional report stays in this clone.
python tools/verify_baseline.py --output Artifacts/baseline-verification.json
python -m unittest discover -s tools -p test_verify_baseline.py

# Run sequentially; do not launch a second Editor on an already-open project.
./KimchilyUnityRuntime/tools/export_webgl.ps1 -PrepareOnly
./KimchilyUnityRuntime/tools/test_runtime.ps1 -Platform EditMode
./KimchilyUnityRuntime/tools/test_runtime.ps1 -Platform PlayMode
./KimchilyUnityRuntime/tools/export_webgl.ps1
./KimchilyCreator/tools/build_webgl.ps1
./KimchilyCreator/tools/publish_last.ps1
```

The local publisher uses port **8788** in this clone; its token is generated locally
and was not copied from the preserved Android workspace. Start the clone's publisher
before publishing. Refer to `KimchilyPublish` tools for its start/status commands.

Managed C# projects import `Unity.Build.props`, which reads the same JSON version/path.
An explicit `-p:UnityEditorRoot=".../Editor"` or the `KIMCHILY_UNITY_EDITOR_ROOT` environment
variable overrides the configured path, in that order. Test projects
use `Unity.TestReferences.props`: first import their clone Unity project to generate
`Library/ScriptAssemblies` and resolve the Editor's Test Framework/NUnit packages.
There are no personal AppData cache paths or legacy 3D-template DLL paths.

`verify_baseline.py` hashes all recorded original source files, the recorded original
Android evidence, and their preserved evidence copies. Upgraded clone sources are
allowed to change. It also rejects missing local `file:` package references and any
that resolve outside this clone, including references through directory links.
The original source record is `preservation/source-manifest.json`; verification never
rewrites it. New original files not recorded when cloning are outside its hash scope.

Android native Gradle tooling in this clone has **not** been migrated to Unity 6.
Use the original 2022.3 workspace and preserved APK for the existing Android baseline;
the new Web tools do not rebuild or reinstall it.
