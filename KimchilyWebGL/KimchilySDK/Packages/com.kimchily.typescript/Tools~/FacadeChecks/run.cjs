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
    let roomStateJson = JSON.stringify({ connected: false, selfId: null, players: [], game: null });
    let panel = null, action = null;
    const host = { call(op, id, args) {
        assert.equal(arguments.length, 3);
        assert.equal(typeof op, "string");
        assert.equal(typeof id, "number");
        assert.ok(Array.isArray(args), "Host arguments are a JavaScript array");
        calls.push({ op, id, args });
        if (op === "time.deltaTime") return deltaTime;
        if (op === "network.getState") return roomStateJson;
        if (["network.useGame", "network.sendAction"].includes(op)) return true;
        if (op === "hud.showPanel") { panel = JSON.parse(args[0]); return; }
        if (op === "hud.takeAction") { const next = action; action = null; return next; }
        if (op === "hud.hide") { panel = null; action = null; return; }
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
    const context = vm.createContext({});
    const factory = vm.runInContext(source, context, { timeout: 1000 });
    const api = factory(host);
    const script = api.modules["Kimchily.Script"], unity = api.modules.UnityEngine;
    class Behaviour extends script.KimchilyScriptBehaviour {}
    return { ...script, ...unity, ...api.modules["Kimchily.Network"], ...api.modules["Kimchily.UI"], api, calls, routines, objects, Behaviour,
        value: value => vm.runInContext("JSON.parse(" + JSON.stringify(JSON.stringify(value)) + ")", context),
        evaluate: code => vm.runInContext(code, context), getPanel: () => panel, queueAction: value => { action = value; },
        setRoom: value => { roomStateJson = JSON.stringify(value); }, setRoomJson: value => { roomStateJson = value; },
        owner: api.create(Behaviour), setDelta: value => { deltaTime = value; } };
}
function test(name, body) {
    try { body(); passed++; console.log("PASS " + name); }
    catch (error) { failed++; console.error("FAIL " + name); console.error(error); }
}

test("exports only supported modules and keeps owner identity stable", () => {
    const c = setup();
    assert.deepEqual(Object.keys(c.api.modules), ["Kimchily.Script", "UnityEngine", "Kimchily.Network", "Kimchily.UI"]);
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

const hash = "a".repeat(64);
test("network commands select registered script identity and JSON actions only", () => {
    const c = setup();
    assert.throws(() => c.Room.useGame("../arbitrary-code", hash), /identifier/);
    assert.throws(() => c.Room.useGame("portal", "wrong"), /SHA-256/);
    assert.throws(() => c.Room.sendAction("bad action"), /identifier/);
    assert.equal(c.calls.length, 0);
    assert.equal(c.Room.useGame("portal", hash), true);
    assert.equal(c.Room.sendAction("start", c.value({ mode: "cooperative" })), true);
    assert.equal(c.Room.sendAction("reset"), true);
    assert.deepEqual(plain(c.calls), [
        { op: "network.useGame", id: 0, args: ["portal", hash] },
        { op: "network.sendAction", id: 0, args: ["start", '{"mode":"cooperative"}'] },
        { op: "network.sendAction", id: 0, args: ["reset", "null"] }
    ]);
    for (const old of ["send", "connect", "enableGame", "startRound", "replay"]) assert.equal(c.Room[old], undefined);
});

test("actions reject accessors, scene references, cycles and excessive UTF-8 before reaching the host", () => {
    const c = setup(), calls = c.calls.length;
    assert.throws(() => c.Room.sendAction("start", c.owner.gameObject), /plain JSON objects/);
    assert.throws(() => c.Room.sendAction("start", c.evaluate("({get value(){throw new Error('getter executed')}})")), /accessors/);
    assert.throws(() => c.Room.sendAction("start", c.evaluate("({toJSON(){throw new Error('toJSON executed')}})")), /plain JSON values/);
    assert.throws(() => c.Room.sendAction("start", c.evaluate("(()=>{const x={};x.x=x;return x;})()")), /Cyclic/);
    assert.throws(() => c.Room.sendAction("start", "한".repeat(342)), /SDK limit/);
    assert.throws(() => c.Room.sendAction("start", "🌶".repeat(256)), /SDK limit/);
    assert.throws(() => c.Room.sendAction("start", c.evaluate("({value:Infinity})")), /finite/);
    assert.equal(c.calls.length, calls);
    c.Room.sendAction("start", "a".repeat(1022));
    assert.equal(Buffer.byteLength(c.calls.at(-1).args[1]), 1024);
});

test("room stateJson becomes typed detached deeply frozen data and identical snapshots are cached", () => {
    const c = setup();
    const state = { phase: "playing", pads: [{ id: "star", active: true, playerId: null }] };
    const server = { connected: true, selfId: "p1", players: [{ playerId: "p1", name: "고추", state: { sequence: 1, x: 3 } }],
        game: { scriptId: "portal", scriptHash: hash, version: 2, stateJson: JSON.stringify(state) } };
    c.setRoom(server);
    const first = c.Room.getState();
    assert.equal(first.players[0].name, "고추");
    assert.equal(first.game.state.phase, "playing");
    assert.equal(first.game.stateJson, undefined);
    assert.ok(Object.isFrozen(first) && Object.isFrozen(first.players[0].state) && Object.isFrozen(first.game.state.pads));
    assert.throws(() => { first.game.state.phase = "complete"; }, TypeError);
    assert.throws(() => { first.players[0].state.x = 99; }, TypeError);
    assert.equal(c.Room.getState(), first);
    state.phase = "complete";
    assert.equal(first.game.state.phase, "playing");
    server.game.stateJson = JSON.stringify(state);
    server.game.version++;
    c.setRoom(server);
    assert.equal(c.Room.getState().game.state.phase, "complete");
    assert.notEqual(c.Room.getState(), first);
});

test("room snapshot rejects oversize graph and invalid game envelopes", () => {
    const c = setup();
    c.setRoomJson(" ".repeat(65537));
    assert.throws(() => c.Room.getState(), /SDK limit/);
    c.setRoom({ connected: false, players: Array(9).fill(null), game: null });
    assert.throws(() => c.Room.getState(), /Invalid room snapshot/);
    const room = { connected: true, players: [], selfId: "", game: { scriptId: "portal", scriptHash: hash, version: 1, stateJson: "[]" } };
    c.setRoom(room);
    assert.throws(() => c.Room.getState(), /JSON object/);
    room.game.stateJson = JSON.stringify({ x: "한".repeat(6000) }); c.setRoom(room);
    assert.throws(() => c.Room.getState(), /Invalid game snapshot/);
    room.game.stateJson = JSON.stringify({ x: Array(1024).fill(0) }); c.setRoom(room);
    assert.throws(() => c.Room.getState(), /structure/);
    room.game.stateJson = "{}"; room.game.version = -1; c.setRoom(room);
    assert.throws(() => c.Room.getState(), /Invalid game snapshot/);
    c.setRoom({ connected: false, selfId: null, players: [], game: null });
    assert.equal(c.Room.getState().game, null);
});

test("HUD marshals a plain card and receives queued IDs without callbacks", () => {
    const c = setup();
    c.Hud.showPanel(c.evaluate("({eyebrow:'',title:'Connecting',body:'',action:undefined,progress:undefined,accent:undefined})"));
    assert.equal(c.getPanel().action, null);
    assert.equal(c.getPanel().progress, 0);
    c.Hud.showPanel(c.value({ eyebrow: "칠리 섬", title: "함께 시작해요", body: "발판을 나눠 밟아요.\n준비되면 시작!", action: { id: "start", label: "시작" } }));
    assert.deepEqual(c.getPanel(), { eyebrow: "칠리 섬", title: "함께 시작해요", body: "발판을 나눠 밟아요.\n준비되면 시작!", progress: 0, accent: "#72C6AE", action: { id: "start", label: "시작", enabled: true } });
    assert.equal(c.Hud.takeAction(), null);
    c.queueAction("start");
    assert.equal(c.Hud.takeAction(), "start");
    assert.equal(c.Hud.takeAction(), null);
    c.queueAction("start"); c.Hud.hide();
    assert.equal(c.getPanel(), null);
    assert.equal(c.Hud.takeAction(), null);
});

test("maximum legal server state remains valid with eight room players", () => {
    const c = setup();
    c.setRoom({ connected: true, selfId: "p0", players: Array.from({ length: 8 }, (_, i) => ({ playerId: "p" + i, name: "Player", state: { sequence: 1, x: 0, y: 0, z: 0, yaw: 0, speed: 0, verticalVelocity: 0, grounded: true } })),
        game: { scriptId: "game", scriptHash: hash, version: 1, stateJson: JSON.stringify({ values: Array(1022).fill(0) }) } });
    const value = c.Room.getState();
    assert.equal(value.players.length, 8);
    assert.equal(value.game.state.values.length, 1022);
    assert.ok(Object.isFrozen(value.game.state.values));
});

test("HUD rejects callbacks, getters, unknown properties and malformed display values", () => {
    const c = setup();
    const card = { eyebrow: "", title: "Ready", body: "" };
    assert.throws(() => c.Hud.showPanel(c.evaluate("({eyebrow:'',title:'Hi',body:'',action:{id:'start',label:'Go',callback(){}}})")), /plain JSON values/);
    assert.throws(() => c.Hud.showPanel(c.evaluate("({eyebrow:'',get title(){throw new Error('getter executed')},body:''})")), /accessors/);
    assert.throws(() => c.Hud.showPanel(c.evaluate("({eyebrow:'',title:'Ready',body:'',unknown:undefined})")), /plain JSON values/);
    for (const extra of [{ progress: Infinity }, { progress: -1 }, { accent: "red" }, { title: "" }, { body: "x".repeat(1201) }, { arbitrary: true }, { action: { id: "start", label: "" } }, { action: { id: "start", label: "Go", enabled: 1 } }]) {
        assert.throws(() => c.Hud.showPanel(c.value({ ...card, ...extra })));
    }
    assert.equal(c.calls.length, 0, "Malformed DTOs never mutate Unity UI");
});

console.log("Facade checks: " + passed + " passed, " + failed + " failed");
if (failed) process.exitCode = 1;
