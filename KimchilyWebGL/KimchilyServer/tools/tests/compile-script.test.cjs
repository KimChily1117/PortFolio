"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { spawnSync } = require("node:child_process");
const { test } = require("node:test");

const serverRoot = path.resolve(__dirname, "../..");
const evidenceRoot = path.join(serverRoot, "Artifacts/compiler-checks");
fs.mkdirSync(evidenceRoot, { recursive: true });
const sha = value => crypto.createHash("sha256").update(value, "utf8").digest("hex");
const sourceLf = [
    "/** 동일 실행 코드를 타입 수정과 줄바꿈 변경 후에도 재사용하는 회귀 검사. */",
    "interface CounterState { value: number; }",
    "export function create(): CounterState { return { value: 0 }; }",
    "export function reduce(state: CounterState): CounterState { return { value: state.value + 1 }; }",
    ""
].join("\n");

function fixture() {
    // 실행 중인 서버의 games/Creator identity를 건드리지 않고 Artifacts 아래에만 출력한다.
    const directory = fs.mkdtempSync(path.join(evidenceRoot, "immutable-"));
    const source = path.join(directory, "Rules.ts");
    const identity = path.join(directory, "Identity.ts");
    const output = path.join(directory, "games");
    fs.writeFileSync(source, sourceLf);
    function compile() {
        return spawnSync(process.execPath, [path.join(serverRoot, "tools/compile-script.cjs"),
            "--source", source, "--id", "compiler-check", "--world", "test-world", "--output", output,
            "--identity", identity], { encoding: "utf8", timeout: 30000, windowsHide: true });
    }
    function success() {
        const result = compile();
        assert.equal(result.status, 0, result.stderr || String(result.error));
        return JSON.parse(result.stdout);
    }
    const first = success();
    const originalText = fs.readFileSync(first.bundle, "utf8");
    // 내용뿐 아니라 기존 파일 자체를 다시 쓰지 않는다는 불변 계약도 검사한다.
    const oldTime = new Date("2000-01-01T00:00:00Z");
    fs.utimesSync(first.bundle, oldTime, oldTime);
    const originalMtime = fs.statSync(first.bundle).mtimeMs;
    return { source, identity, first, originalText, originalMtime, compile, success };
}

function verifyReuse(current, next, currentSource) {
    assert.equal(next.scriptHash, current.first.scriptHash);
    assert.equal(next.reusedBundle, true);
    assert.equal(next.sourceHash, sha(currentSource), "CLI sourceHash must describe this input");
    assert.equal(next.bundleSourceHash, current.first.sourceHash, "Original bundle provenance must stay intact");
    assert.equal(fs.readFileSync(current.first.bundle, "utf8"), current.originalText);
    assert.equal(fs.statSync(current.first.bundle).mtimeMs, current.originalMtime);
}

test("identical input reuses an immutable bundle without rewriting its bytes or timestamp", () => {
    const current = fixture();
    assert.equal(current.first.reusedBundle, false);
    assert.equal(current.first.sourceHash, current.first.bundleSourceHash);
    verifyReuse(current, current.success(), sourceLf);
});

test("type-only changes may change source provenance while reusing the same JavaScript identity", () => {
    const current = fixture();
    const typeOnly = sourceLf.replace("interface CounterState { value: number; }", "type CounterState = { readonly value: number; };");
    fs.writeFileSync(current.source, typeOnly);
    const next = current.success();
    assert.notEqual(next.sourceHash, current.first.sourceHash);
    verifyReuse(current, next, typeOnly);
});

test("LF to CRLF checkout conversion reuses the same bundle and retains original provenance", () => {
    const current = fixture();
    const sourceCrlf = sourceLf.replaceAll("\n", "\r\n");
    fs.writeFileSync(current.source, sourceCrlf);
    const next = current.success();
    assert.notEqual(next.sourceHash, current.first.sourceHash);
    verifyReuse(current, next, sourceCrlf);
});

for (const [label, corrupt] of [
    ["runtime code", bundle => { bundle.javascript += "\nthrow new Error('changed');"; }],
    ["world approval", bundle => { bundle.worldId = "different-world"; }],
    ["script identity", bundle => { bundle.scriptId = "different-script"; }],
    ["source provenance type", bundle => { bundle.sourceHash = [bundle.sourceHash]; }]
]) {
    test(`a bundle with corrupted ${label} is rejected rather than reused or overwritten`, () => {
        const current = fixture();
        const bundle = JSON.parse(current.originalText);
        corrupt(bundle);
        const corruptText = JSON.stringify(bundle, null, 2) + "\n";
        fs.writeFileSync(current.first.bundle, corruptText);
        const identityBefore = fs.readFileSync(current.identity, "utf8");
        const result = current.compile();
        assert.notEqual(result.status, 0);
        assert.match(result.stderr, /Immutable bundle collision/);
        assert.equal(fs.readFileSync(current.first.bundle, "utf8"), corruptText);
        assert.equal(fs.readFileSync(current.identity, "utf8"), identityBefore);
    });
}
