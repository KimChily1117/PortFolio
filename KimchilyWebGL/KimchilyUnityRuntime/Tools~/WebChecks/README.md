# Browser host checks

Run `node --test host.test.cjs` in this directory. These checks cover URL validation,
Unity startup ordering, event/request matching, cancellation, failed initialization
cleanup and retry. They do not replace a real Unity Web build or mobile Safari run.
`dotnet build WebCompile.csproj` checks the runtime and builder against the pinned
Unity installation without starting Unity. It includes the Web player adapter path
and therefore needs the installed WebGL module. Before that module is available,
`-p:DefineConstants=UNITY_EDITOR` checks only the Editor-visible path.

Build with `Kimchily.World.Editor.WebRuntimeBuilder.Build` and `-buildTarget WebGL`.
The result goes to `Builds/WebGL` and uses the `KimchilyWeb` template. The sample
passes actual TypeScript through the installed pinned compiler; a missing compiler
is a build failure rather than a silent C# substitute. The FBX sample and mobile
player are reused from the existing generated demo. Humanoid clip playback remains
part of the authored world integration test; the small built-in fixture has no
locomotion profile.

Serve the build at `/player/` on the publication server, with `application/wasm`
for `.wasm` files. This development build disables compression and browser data
caching, uses WebGL 2 (OpenGLES3), and disables native threads. There are no CDN
scripts. A remote entry URL is `/player/?manifest=<absolute-URL>&sha256=<64-hex>`;
the manifest must be on the same origin and at `/worlds/<world>/<revision>/world.json`.
Loading over `file://` is intentionally rejected. The browser does not bypass the
Unity manifest, checksum, version, platform or required-type checks.

The `.jslib` forwards the existing protocol as JSON to `window.KimchilyWebReceive`.
The template also emits a DOM `kimchily-world-event` CustomEvent for observation.
Commands use `SendMessage("KimchilyHostBridge", "Receive", json)`. Start is a user
action; duplicate startup events cannot open twice. Failed scene initialization
requests and awaits `CloseWorld` before allowing another open. Visibility, focus,
rotation and touch cancellation clear movement input. Fullscreen is optional;
portrait and landscape both use the available viewport without orientation locks.
