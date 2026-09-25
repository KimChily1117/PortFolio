# Kimchily Lua Scripting 0.1

This UPM package adds a Lua 5.2-style world scripting surface using the pure C# MoonSharp 2.0.0 interpreter. Install alongside `com.kimchily.creator`. The same package and DLL must be built into both Creator and the World player. Published AssetBundles carry `.lua` TextAssets and serialized `KimchilyLuaBehaviour` references; they do not download new C# assemblies.

## Authoring

1. Use **Assets > Create > Kimchily > Lua Script**.
2. Add **Kimchily > Lua Behaviour** to a scene GameObject and assign the script asset.
3. Assign optional named GameObject references in `References`.
4. Enter Play mode, then publish the scene through the Creator publisher.

`KimchilyLuaBehaviour` creates its own Lua globals. State is retained while disabled; `on_start` runs once. Disabling cancels its pending/active coroutines. Re-enabling invokes `on_enable`; it does not resume cancelled coroutines. `Reload()` explicitly discards state and restarts the assigned asset. Destroying the component cancels its work and calls `on_destroy` when the script is healthy.

```lua
function on_start()
    log("Hello from Lua")
    start(function()
        wait_seconds(1)
        if refs.lamp then refs.lamp.set_active(true) end
        next_frame()
        self.set_scale(1.2, 1.2, 1.2)
    end)
end

function on_update(dt)
    self.rotate(0, 45 * dt, 0)
end
```

## Version 0.1 API

| Surface | Functions |
| --- | --- |
| Lifecycle | `on_enable()`, `on_start()`, `on_update(deltaSeconds)`, `on_disable()`, `on_destroy()` |
| Object facade (`self` or `refs.name`) | `position()` → `{x,y,z}`, `set_position(x,y,z)`, `translate(x,y,z)` in world space, `rotate(x,y,z)` in local space/degrees, `set_scale(x,y,z)`, `set_active(bool)`, `is_active()` |
| Diagnostics | `log(value)` (maximum 2048 output characters) |
| Coroutines | `start(function)` (maximum 32 pending/active per behaviour), `wait_seconds(0..86400)` (Unity scaled time), `next_frame()` |

Both `self.rotate(...)` and `self:rotate(...)` are supported. `position()` returns a value copy: assign with `set_position` to change Unity state. A missing named reference is `nil`; accessing a destroyed assigned object produces a script error. Coroutine functions may yield, lifecycle functions may not. `start()` queues work; new routines created by a running routine begin during a later Update. Routines started in `on_start` begin after that callback returns.

No animator, arbitrary `GetComponent`, object search, asset/network/file loading or arbitrary Unity/C# namespace is exposed in 0.1. Add future API operations as explicit checked callbacks.

## Limits and IL2CPP

- Every Lua chunk, lifecycle and coroutine resume has an instruction budget (default 20000, enforced range 100–100000 even for deserialized values). A forced yield becomes a script fault instead of continuing an infinite loop indefinitely.
- Faults populate `IsFaulted`/`LastError`, emit one Unity Console error, and cancel the behaviour's routines. Other behaviours keep running.
- `.lua` source is limited to 262144 characters; script references and loaded content must still be validated by the publisher/player.
- No CLR userdata or automatic interop registration is used. `io`, `os`, `load`, `require`, `debug`, Lua coroutine creation/resumption, protected calls and metatables are unavailable. Reentrant `string.gsub`, `table.sort` and several allocation/pattern helpers are removed.
- This is a restricted MVP execution surface, **not a complete hostile-code sandbox**: instruction counting does not enforce total memory, native callback wall-clock time or resource allocation limits. Lua string/table growth can still exhaust memory. Accept reviewed content until resource isolation and publishing controls are implemented.
- `Runtime/link.xml` is a preservation template. Unity 2022.3 does not support `link.xml` inside packages: copy/merge it into the consuming player's `Assets` directory and also preserve `Kimchily.Creator.Runtime`. See [Unity's linker documentation](https://docs.unity3d.com/kr/2022.3/Manual/ManagedCodeStripping.html). The managed interpreter is source-built from official MoonSharp 2.0.0.0 for `netstandard2.1`, preserving its public API and assembly identity. Its default loader uses an empty in-memory dictionary instead of legacy Unity Resources reflection; see [source, patch and rebuild instructions](Tools~/Vendor/README.md). It requires no platform-specific xLua native binary. Actual Android/iOS IL2CPP validation remains required; passing Editor/.NET checks alone does not establish device compatibility.

## Validation

`Tests/Runtime/LuaBehaviourTests.cs` covers object/lifecycle callbacks, coroutine frame/time yields, disable/destroy cancellation, deferred self-disable, syntax/runtime errors, missing privileged libraries, and instruction budget faults at top level, Update and coroutine entry. Add `com.kimchily.scripting` to the consuming project's `testables`, then run Unity PlayMode tests. `Tools~/Compile` also provides a managed compilation check against the installed Unity editor and built Creator DLL.

## Upstream references

- [MoonSharp getting started and IL2CPP preservation](https://www.moonsharp.org/getting_started.html)
- [MoonSharp sandbox configuration](https://www.moonsharp.org/sandbox.html)
- [MoonSharp coroutine and preemption caveats](https://www.moonsharp.org/coroutines.html)

Dependency origin, exact SHA-256 hashes and the original BSD license are in `Runtime/Plugins`.
