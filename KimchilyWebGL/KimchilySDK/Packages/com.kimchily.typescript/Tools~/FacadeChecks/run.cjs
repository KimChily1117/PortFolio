"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname, "../../Runtime/Resources/Kimchily/TypeScript/Bootstrap.js.txt"), "utf8");
const plain = value => JSON.parse(JSON.stringify(value));
let passed = 0;
let failed = 0;

function setup() {
    const calls = [], routines = new Map();
    const objects = new Map([0, 1, 2].map(id => [id, {
        name: `Object${id}`, activeSelf: true,
        position: [id, 2, 3], localPosition: [0, 0, 0], localScale: [1, 1, 1], eulerAngles: [0, 0, 0]
    }]));
    let nextRoutine = 1;
    let deltaTime = 0.016;
    const host = { call(op, id, args) {
        assert.equal(arguments.length, 3);
        assert.equal(typeof op, "string");
        assert.equal(typeof id, "number");
        assert.ok(Array.isArray(args), "Host arguments are a JavaScript array");
        calls.push({ op, id, args });
        if (op === "time.deltaTime") return deltaTime;
        if (op.startsWith("debug.")) return;
        if (op === "coroutine.start") { const handle = nextRoutine++; routines.set(handle, args[0]); return handle; }
        if (op === "coroutine.stop") { routines.delete(args[0]); return; }
        if (op === "coroutine.stopAll") { routines.clear(); return; }
        const object = objects.get(id);
        if (!object) throw new Error("Missing scene reference");
        switch (op) {
            case "gameObject.getName": return object.name;
            case "gameObject.setName": object.name = args[0]; return;
            case "gameObject.getActiveSelf": return object.activeSelf;
            case "gameObject.setActive": object.activeSelf = args[0]; return;
            case "transform.translate": case "transform.rotate": return;
            default: {
                const match = /^transform\.(get|set)(Position|LocalPosition|LocalScale|EulerAngles)$/.exec(op);
                if (!match) throw new Error("Unknown host operation: " + op);
                const key = match[2][0].toLowerCase() + match[2].slice(1);
                if (match[1] === "get") return object[key].slice();
                assert.equal(args.length, 3, "Vector setter is a flat [x,y,z] argument list");
                object[key] = Array.from(args);
            }
        }
    }};
    const factory = vm.runInNewContext(source, {}, { timeout: 1000 });
    const api = factory(host);
    const script = api.modules["Kimchily.Script"], unity = api.modules.UnityEngine;
    class Behaviour extends script.KimchilyScriptBehaviour {}
    return { ...script, ...unity, api, calls, routines, objects, Behaviour,
        owner: api.create(Behaviour), setDelta: value => { deltaTime = value; } };
}
function test(name, body) {
    try { body(); passed++; console.log("PASS " + name); }
    catch (error) { failed++; console.error("FAIL " + name); console.error(error); }
}

test("exports only supported modules and keeps owner identity stable", () => {
    const c = setup();
    assert.deepEqual(Object.keys(c.api.modules), ["Kimchily.Script", "UnityEngine"]);
    assert.equal(c.owner.gameObject.transform, c.owner.transform);
    assert.equal(c.owner.transform.gameObject, c.owner.gameObject);
    assert.equal(c.owner.gameObject.name, "Object0");
    assert.deepEqual(Object.keys(c.owner.gameObject), [], "Native IDs are not public object properties");
});
test("constructors can use owner and context recovers after errors", () => {
    const c = setup();
    class Good extends c.KimchilyScriptBehaviour { constructor() { super(); this.initialName = this.gameObject.name; } }
    assert.equal(c.api.create(Good, 1).initialName, "Object1");
    class Bad extends c.KimchilyScriptBehaviour { constructor() { super(); throw new Error("constructor failure"); } }
    assert.throws(() => c.api.create(Bad), /constructor failure/);
    assert.equal(c.api.create(Good).initialName, "Object0");
    assert.throws(() => new Good(), /runtime must create/);
    assert.throws(() => c.api.create(class {}), /must extend/);
    assert.throws(() => c.api.create(c.Behaviour, -1), /reference ID/);
});
test("constructor cannot replace the bound instance", () => {
    const c = setup();
    class Wrong extends c.KimchilyScriptBehaviour { constructor() { super(); return {}; } }
    assert.throws(() => c.api.create(Wrong), /different object/);
});
test("scene objects cannot be directly constructed or forged", () => {
    const c = setup();
    assert.throws(() => new c.GameObject(), /assigned from the scene/);
    assert.throws(() => new c.Transform(), /assigned from the scene/);
    assert.throws(() => c.GameObject.prototype.SetActive.call({}, true), /scene reference/);
});
test("scene name, active state, transform setters and defaults reach the host", () => {
    const c = setup();
    c.owner.gameObject.name = "Renamed";
    c.owner.gameObject.SetActive(false);
    assert.equal(c.owner.gameObject.name, "Renamed");
    assert.equal(c.owner.gameObject.activeSelf, false);
    for (const key of ["position", "localPosition", "localScale", "eulerAngles"]) {
        c.owner.transform[key] = new c.Vector3(4, 5, 6);
        assert.deepEqual(plain(c.owner.transform[key]), { x: 4, y: 5, z: 6 });
    }
    c.owner.transform.Translate(1, 2, 3);
    c.owner.transform.Rotate(0, 90, 0, c.Space.World);
    assert.deepEqual(plain(c.calls.slice(-2)), [
        { op: "transform.translate", id: 0, args: [1, 2, 3, 1] },
        { op: "transform.rotate", id: 0, args: [0, 90, 0, 0] }
    ]);
    assert.equal(c.Space[c.Space.Self], "Self");
});
test("reading or mutating a vector does not move its source object", () => {
    const c = setup();
    const value = c.owner.transform.position;
    value.x = 99;
    assert.equal(c.owner.transform.position.x, 0);
    c.owner.transform.position = value;
    value.y = 88;
    assert.equal(c.owner.transform.position.x, 99);
    assert.equal(c.owner.transform.position.y, 2);
});
test("vector math and static constants are independent JavaScript values", () => {
    const c = setup(), V = c.Vector3;
    const zero = V.zero; zero.x = 9;
    assert.equal(V.zero.x, 0);
    const value = new V(3, 4, 0);
    assert.equal(value.magnitude, 5);
    assert.equal(value.sqrMagnitude, 25);
    assert.ok(Math.abs(value.normalized.magnitude - 1) < 1e-12);
    assert.equal(value.x, 3);
    assert.equal(V.Distance(V.zero, value), 5);
    assert.equal(V.Dot(V.up, V.right), 0);
    assert.deepEqual(plain(V.Cross(V.right, V.up)), plain(V.forward));
    assert.deepEqual(plain(V.Lerp(V.zero, V.one, 2)), plain(V.one));
    assert.deepEqual(plain(V.Lerp(V.zero, V.one, -2)), plain(V.zero));
    assert.deepEqual(plain(V.zero.normalized), { x: 0, y: 0, z: 0 });
    assert.deepEqual(plain(value.subtract(new V(1, 1, 0)).multiply(2)), { x: 4, y: 6, z: 0 });
});
test("invalid numeric and scene arguments fail before host mutation", () => {
    const c = setup();
    const count = c.calls.length;
    assert.throws(() => new c.Vector3(NaN), /finite/);
    assert.throws(() => c.owner.transform.Translate(Infinity, 0, 0), /finite/);
    assert.throws(() => c.owner.transform.Rotate(0, 0, 0, 2), /Space/);
    assert.throws(() => { c.owner.transform.position = [1, 2, 3]; }, /Vector3/);
    assert.throws(() => c.owner.gameObject.SetActive(1), /boolean/);
    assert.throws(() => { c.owner.gameObject.name = 1; }, /string/);
    assert.equal(c.calls.length, count);
});
test("Inspector bindings share reference IDs and preserve null references", () => {
    const c = setup();
    c.api.applyFields(c.owner, {
        beacon: { type: "GameObject", value: 1 }, target: { type: "Transform", value: 1 },
        speed: { type: "number", value: 45 }, label: { type: "string", value: "hello" },
        animate: { type: "boolean", value: false }, offset: { type: "Vector3", value: [1, 2, 3] },
        empty: { type: "GameObject", value: null }, emptyTransform: { type: "Transform", value: null }
    });
    assert.equal(c.owner.beacon.transform, c.owner.target);
    assert.equal(c.owner.target.gameObject.name, "Object1");
    assert.equal(c.owner.speed, 45); assert.equal(c.owner.label, "hello"); assert.equal(c.owner.animate, false);
    assert.ok(c.owner.offset instanceof c.Vector3);
    assert.equal(c.owner.empty, null); assert.equal(c.owner.emptyTransform, null);
    c.owner.beacon.SetActive(false);
    assert.equal(c.objects.get(1).activeSelf, false);
    assert.equal(c.objects.get(0).activeSelf, true);
});
test("bad Inspector bindings do not apply earlier fields or replace lifecycle APIs", () => {
    const c = setup();
    c.owner.speed = 10;
    assert.throws(() => c.api.applyFields(c.owner, { speed: { type: "number", value: 20 }, bad: { type: "number", value: "wrong" } }), /finite/);
    assert.equal(c.owner.speed, 10);
    for (const name of ["__proto__", "prototype", "constructor", "gameObject", "transform", "StartCoroutine", "Awake", "OnEnable", "Update", "bad-name"]) {
        const bindings = Object.create(null); bindings[name] = { type: "number", value: 1 };
        assert.throws(() => c.api.applyFields(c.owner, bindings), /Invalid Inspector field/);
    }
    assert.throws(() => c.api.applyFields(c.owner, { camera: { type: "Camera", value: 0 } }), /Unsupported Inspector type/);
    assert.throws(() => c.api.applyFields(c.owner, { bad: { type: "Vector3", value: [1, 2] } }), /three-number/);
    assert.throws(() => c.api.applyFields({}, {}), /runtime-created/);
});
test("Map remains native JavaScript and private script state is untouched", () => {
    const c = setup();
    class State extends c.KimchilyScriptBehaviour { constructor() { super(); this.index = new Map([["owner", this.gameObject]]); } }
    const instance = c.api.create(State);
    c.api.applyFields(instance, { speed: { type: "number", value: 3 } });
    assert.equal(instance.index.get("owner"), instance.gameObject);
    assert.equal(instance.index.size, 1);
});
test("coroutine generator, wait tokens and stop handles follow the host contract", () => {
    const c = setup(); let progressed = 0;
    function* routine() { progressed++; yield new c.WaitForSeconds(0.5); progressed++; yield null; progressed++; }
    const iterator = routine(), handle = c.owner.StartCoroutine(iterator);
    assert.ok(handle instanceof c.Coroutine); assert.equal(c.routines.get(1), iterator); assert.equal(progressed, 0);
    const wait = iterator.next().value;
    assert.deepEqual(plain(wait), { __kimchilyWait: "seconds", seconds: 0.5 });
    assert.ok(Object.isFrozen(wait)); assert.equal(iterator.next().value, null);
    assert.equal(iterator.next().done, true); assert.equal(progressed, 3);
    c.owner.StopCoroutine(handle); assert.equal(c.routines.size, 0);
    c.owner.StopCoroutine(handle); // Completed/cancelled handles may be stopped safely.
    assert.deepEqual(plain(c.calls.at(-1)), { op: "coroutine.stop", id: 0, args: [1] });
    c.owner.StartCoroutine(routine()); c.owner.StopAllCoroutines(); assert.equal(c.routines.size, 0);
});
test("coroutine handles cannot be fabricated or stopped by another behaviour", () => {
    const c = setup();
    const other = c.api.create(c.Behaviour, 1);
    const handle = c.owner.StartCoroutine((function* () { yield null; })());
    assert.throws(() => other.StopCoroutine(handle), /another behaviour/);
    assert.throws(() => c.owner.StopCoroutine({}), /another behaviour/);
    assert.throws(() => new c.Coroutine(), /StartCoroutine/);
    assert.throws(() => c.owner.StartCoroutine(() => {}), /iterator/);
    assert.throws(() => new c.WaitForSeconds(-1), /nonnegative/);
    assert.throws(() => new c.WaitForSeconds(Infinity), /finite/);
});
test("events are local, deduplicate listeners and enforce capacity", () => {
    const c = setup(), event = new c.Event(1); let sum = 0;
    const first = value => { sum += value; }, second = () => {};
    event.AddListener(first); event.AddListener(first);
    assert.equal(event.ListenerCount, 1);
    assert.throws(() => event.AddListener(second), /capacity/);
    event.Invoke(3); assert.equal(sum, 3);
    event.RemoveListener(first); event.RemoveListener(first);
    event.Invoke(3); assert.equal(sum, 3); assert.equal(event.ListenerCount, 0);
    assert.equal(c.calls.length, 0, "Local events do not reach the Unity host");
    assert.throws(() => new c.Event(257), /capacity/);
    assert.throws(() => new c.Event(0), /capacity/);
    assert.throws(() => event.AddListener(null), /function/);
});
test("event listener changes affect the next invocation, not the current snapshot", () => {
    const c = setup(), event = new c.Event(), seen = [];
    const third = () => seen.push("third"), second = () => seen.push("second");
    const first = () => { seen.push("first"); event.RemoveListener(second); event.AddListener(third); };
    event.AddListener(first); event.AddListener(second);
    event.Invoke(); assert.deepEqual(seen, ["first", "second"]);
    seen.length = 0; event.Invoke(); assert.deepEqual(seen, ["first", "third"]);
});
test("event exceptions propagate to the runtime boundary", () => {
    const c = setup(), event = new c.Event();
    event.AddListener(() => { throw new Error("listener failed"); });
    assert.throws(() => event.Invoke(), /listener failed/);
});
test("Time reads current frame values and Debug forwards only strings", () => {
    const c = setup(); assert.equal(c.Time.deltaTime, 0.016);
    c.setDelta(0.25); assert.equal(c.Time.deltaTime, 0.25);
    c.Debug.Log("hello"); c.Debug.LogWarning(12); c.Debug.LogError(null);
    assert.deepEqual(plain(c.calls.slice(-3)), [
        { op: "debug.log", id: 0, args: ["hello"] },
        { op: "debug.logWarning", id: 0, args: ["12"] },
        { op: "debug.logError", id: 0, args: ["null"] }
    ]);
});

console.log(`Facade checks: ${passed} passed, ${failed} failed`);
if (failed) process.exitCode = 1;
