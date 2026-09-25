"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const ts = require("../Compiler/node_modules/typescript");

const probe = path.join(__dirname, "autocomplete-probe.ts");
const typings = path.resolve(__dirname, "../../Typings~/kimchily.d.ts");
const source = `import { KimchilyScriptBehaviour } from "Kimchily.Script";
import { GameObject, Space } from "UnityEngine";
export default class AutocompleteScript extends KimchilyScriptBehaviour {
    public speed: number = 45;
    public beacon: GameObject | null = null;
    Update(deltaTime: number): void {
        this.transform.Rotate(0, this.speed * deltaTime, 0, Space.World);
        this.speed = this.speed;
        this.beacon?.SetActive(true);
    }
}
`;
const options = {
    target: ts.ScriptTarget.ES2018, module: ts.ModuleKind.CommonJS,
    strict: true, noEmit: true, types: [], lib: ["lib.es2018.d.ts"]
};
const host = {
    getScriptFileNames: () => [probe, typings],
    getScriptVersion: () => "1",
    getScriptSnapshot(file) {
        const text = path.resolve(file) === probe ? source : ts.sys.readFile(file);
        return text === undefined ? undefined : ts.ScriptSnapshot.fromString(text);
    },
    getCurrentDirectory: () => __dirname,
    getCompilationSettings: () => options,
    getDefaultLibFileName: settings => ts.getDefaultLibFilePath(settings),
    fileExists: file => path.resolve(file) === probe || ts.sys.fileExists(file),
    readFile: file => path.resolve(file) === probe ? source : ts.sys.readFile(file),
    readDirectory: ts.sys.readDirectory,
    directoryExists: ts.sys.directoryExists,
    getDirectories: ts.sys.getDirectories,
    useCaseSensitiveFileNames: () => ts.sys.useCaseSensitiveFileNames,
    getNewLine: () => ts.sys.newLine
};
const service = ts.createLanguageService(host, ts.createDocumentRegistry());
try {
    const diagnostics = [...service.getSyntacticDiagnostics(probe), ...service.getSemanticDiagnostics(probe)];
    assert.deepEqual(diagnostics.map(d => ts.flattenDiagnosticMessageText(d.messageText, "\n")), []);
    const transformPosition = source.indexOf("this.transform.") + "this.transform.".length;
    const transform = service.getCompletionsAtPosition(probe, transformPosition, {});
    assert.ok(transform, "this.transform. must provide completions");
    const transformNames = transform.entries.map(entry => entry.name);
    for (const expected of ["Rotate", "Translate", "position", "localPosition", "localScale", "eulerAngles", "gameObject"])
        assert.ok(transformNames.includes(expected), `Missing Transform completion: ${expected}`);
    assert.ok(!transformNames.includes("GetComponent"), "Unsupported Unity APIs must not be suggested");

    const memberPosition = source.indexOf("this.speed =") + "this.".length;
    const members = service.getCompletionsAtPosition(probe, memberPosition, {});
    assert.ok(members, "this. must provide script field completions");
    const memberNames = members.entries.map(entry => entry.name);
    for (const expected of ["speed", "beacon", "gameObject", "transform", "StartCoroutine", "StopCoroutine"])
        assert.ok(memberNames.includes(expected), `Missing script completion: ${expected}`);

    function quickInfo(position) {
        const info = service.getQuickInfoAtPosition(probe, position);
        assert.ok(info, "QuickInfo must exist");
        return { signature: ts.displayPartsToString(info.displayParts), documentation: ts.displayPartsToString(info.documentation) };
    }
    const rotateInfo = quickInfo(transformPosition + 1);
    assert.match(rotateInfo.signature, /Rotate\(x: number, y: number, z: number, relativeTo\?: Space\)/);
    assert.match(rotateInfo.documentation, /Euler rotation in degrees/);
    const speedInfo = quickInfo(memberPosition + 1);
    assert.match(speedInfo.signature, /speed: number/);
    const beaconInfo = quickInfo(source.indexOf("this.beacon?") + "this.".length + 1);
    assert.match(beaconInfo.signature, /beacon: GameObject \| null/);
    const parameterInfo = quickInfo(source.indexOf("deltaTime: number") + 1);
    assert.match(parameterInfo.signature, /deltaTime: number/);
    const details = service.getCompletionEntryDetails(probe, transformPosition, "Rotate", {}, undefined, {}, undefined);
    assert.ok(details, "Completion detail must exist");
    assert.match(ts.displayPartsToString(details.displayParts), /relativeTo\?: Space/);

    const report = {
        typescriptVersion: ts.version,
        strictSemanticDiagnostics: diagnostics.length,
        transformCompletions: transformNames,
        scriptCompletions: memberNames,
        rotateQuickInfo: rotateInfo,
        speedQuickInfo: speedInfo,
        beaconQuickInfo: beaconInfo,
        parameterQuickInfo: parameterInfo,
        completionDetail: ts.displayPartsToString(details.displayParts),
        passed: true
    };
    const output = path.join(__dirname, "Artifacts/ide-check.json");
    fs.mkdirSync(path.dirname(output), { recursive: true });
    fs.writeFileSync(output, JSON.stringify(report, null, 2) + "\n");
    console.log(`PASS TypeScript ${ts.version}: actual LanguageService completions, parameter types, nullable public fields and JSDoc`);
    console.log(`Report: ${output}`);
} finally { service.dispose(); }
