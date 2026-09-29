"use strict";
// 빌드 시에만 Node/TypeScript를 사용한다. 운영 서버는 생성된 JS를 Jint에서 실행한다.
const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const serverRoot = path.resolve(__dirname, "..");
const ts = require(path.join(serverRoot, "../KimchilySDK/Packages/com.kimchily.typescript/Tools~/Compiler/node_modules/typescript"));
const args = {};
for (let i = 2; i < process.argv.length; i += 2) args[process.argv[i]] = process.argv[i + 1];
const sourcePath = path.resolve(args["--source"]);
const id = args["--id"];
const world = args["--world"];
const output = path.resolve(args["--output"]);
if (!/^[A-Za-z0-9_-]{1,80}$/.test(id) || !/^[A-Za-z0-9_-]{1,80}$/.test(world)) throw new Error("Invalid script/world id");
if (ts.version !== "5.9.3") throw new Error("Expected pinned TypeScript 5.9.3");
const source = fs.readFileSync(sourcePath, "utf8");
const sha = value => crypto.createHash("sha256").update(value, "utf8").digest("hex");
let javascript;
const options = { strict: true, target: ts.ScriptTarget.ES2020, module: ts.ModuleKind.CommonJS,
    lib: ["lib.es2020.d.ts"], noEmitOnError: true, newLine: ts.NewLineKind.LineFeed,
    skipLibCheck: true, declaration: false, sourceMap: false, types: [] };
const host = ts.createCompilerHost(options);
host.writeFile = (file, text) => { if (file.endsWith(".js")) javascript = text; };
const program = ts.createProgram([sourcePath], options, host);
// 이번 실행기는 단일 CJS 모듈만 지원한다. 서버에서 require가 생기지 않도록 imports/re-exports를 명확히 거부한다.
const entry = program.getSourceFile(sourcePath);
if (!entry) throw new Error("Missing TypeScript source");
for (const statement of entry.statements) {
    if (ts.isImportDeclaration(statement) || ts.isImportEqualsDeclaration(statement)
        || (ts.isExportDeclaration(statement) && statement.moduleSpecifier)) throw new Error("Server rules must be a closed single module");
}
const diagnostics = ts.getPreEmitDiagnostics(program);
if (diagnostics.length) throw new Error(ts.formatDiagnosticsWithColorAndContext(diagnostics, {
    getCurrentDirectory: () => serverRoot, getCanonicalFileName: file => file, getNewLine: () => "\n"
}));
const emitted = program.emit();
if (emitted.emitSkipped || !javascript) throw new Error("TypeScript emitted no JavaScript");
if (Buffer.byteLength(javascript, "utf8") > 262144) throw new Error("Compiled rules are too large");
const scriptHash = sha(javascript);
const sourceHash = sha(source);
const bundle = { schemaVersion: 1, scriptId: id, worldId: world, scriptHash,
    sourceHash, compilerVersion: ts.version, javascript };
const destination = path.join(output, id, scriptHash + ".json");
const json = JSON.stringify(bundle, null, 2) + "\n";
let bundleSourceHash = sourceHash;
let reusedBundle = false;
fs.mkdirSync(path.dirname(destination), { recursive: true });
if (fs.existsSync(destination)) {
    // 실행 정체성은 JS의 해시다. 타입 전용 수정이나 CRLF/LF 변환은 sourceHash만 바꿀 수 있다.
    // 같은 JS·ID·월드이면 기존 승인 번들을 그대로 재사용하고 최초 등록의 출처 기록은 보존한다.
    const existingText = fs.readFileSync(destination, "utf8");
    let existing;
    try { existing = JSON.parse(existingText); }
    catch { throw new Error("Immutable bundle collision: existing bundle is not JSON"); }
    const expectedKeys = Object.keys(bundle).sort().join(",");
    if (!existing || typeof existing !== "object" || Array.isArray(existing)
        || Object.keys(existing).sort().join(",") !== expectedKeys
        || existing.schemaVersion !== 1 || existing.scriptId !== id || existing.worldId !== world
        || existing.scriptHash !== scriptHash || existing.javascript !== javascript
        || sha(existing.javascript) !== scriptHash
        || typeof existing.sourceHash !== "string" || !/^[a-f0-9]{64}$/.test(existing.sourceHash) || typeof existing.compilerVersion !== "string"
        || !existing.compilerVersion.length)
        throw new Error("Immutable bundle collision: approved identity, runtime code or provenance is invalid");
    bundleSourceHash = existing.sourceHash;
    reusedBundle = true;
} else {
    const temporary = destination + ".tmp-" + process.pid;
    fs.writeFileSync(temporary, json, { flag: "wx" });
    fs.renameSync(temporary, destination);
}
if (args["--identity"]) {
    // 월드의 클라이언트도 동일한 코드 해시를 요청한다. 서로 다른 규칙 버전이 같은 방에 섞이지 않는다.
    const identity = path.resolve(args["--identity"]);
    fs.mkdirSync(path.dirname(identity), { recursive: true });
    fs.writeFileSync(identity, "// compile-script.ps1이 생성합니다. 규칙 변경 후 다시 빌드해 주세요.\n"
        + `export const SCRIPT_ID = ${JSON.stringify(id)};\nexport const SCRIPT_HASH = ${JSON.stringify(scriptHash)};\n`);
}
// sourceHash는 이번 입력, bundleSourceHash는 불변 번들을 최초 생성한 입력이다. 재사용 시 둘은 다를 수 있다.
process.stdout.write(JSON.stringify({ scriptId: id, scriptHash, bundle: destination, sourceHash, bundleSourceHash, reusedBundle }) + "\n");
