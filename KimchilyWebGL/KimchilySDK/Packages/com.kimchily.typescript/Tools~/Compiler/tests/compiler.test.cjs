'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { compile } = require('../compile.cjs');
const work = path.join(__dirname, '.work');
fs.mkdirSync(work, { recursive: true });
function fixture(files, run) {
  const root = fs.mkdtempSync(path.join(work, 'fixture-'));
  try {
    fs.mkdirSync(path.join(root, 'Assets'));
    for (const [name, source] of Object.entries(files)) {
      const file = path.join(root, name); fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, source);
    }
    return run(root);
  } finally {
    if (!root.startsWith(work + path.sep)) throw new Error('Unsafe fixture cleanup');
    fs.rmSync(root, { recursive: true });
  }
}
const header = `import { KimchilyScriptBehaviour } from 'Kimchily.Script';
import { GameObject, Transform, Vector3, Time } from 'UnityEngine';\n`;
const entry = body => header + `export default class Example extends KimchilyScriptBehaviour { ${body} }`;

test('actual checker emits a closed CommonJS graph and schema for public fields only', () => fixture({
  'Assets/Main.ts': `import { speed } from './Helper';\n` + entry(`
    public amount: number = speed; public label = 'hello'; public active: boolean = true;
    public target: GameObject | null = null; public pivot: Transform | null = null;
    public offset: Vector3 = Vector3.zero; private data = new Map<string, GameObject>();
    protected hidden = 7; private elapsed = 0;
    Update(): void { this.transform.Rotate(0, this.amount * Time.deltaTime, 0); }
  `), 'Assets/Helper.ts': 'export const speed: number = 45;'
}, root => {
  const out = compile(root, 'Assets/Main.ts');
  assert.equal(out.compiledSuccessfully, true, out.diagnostics.join('\n'));
  assert.equal(out.className, 'Example'); assert.equal(out.compilerVersion, '5.9.3');
  assert.deepEqual(out.modules.map(x => x.id), ['Assets/Helper', 'Assets/Main']);
  assert.match(out.modules[1].source, /require\("\.\/Helper"\)/);
  assert.deepEqual(out.fields.map(f => [f.name, f.kind]), [['amount','number'],['label','string'],['active','boolean'],['target','GameObject'],['pivot','Transform'],['offset','Vector3']]);
  assert.ok(out.modules.every(m => JSON.parse(m.sourceMap).sourcesContent.length));
  assert.equal(out.sourceHash.length, 64);
}));

test('type errors include source locations and leave no executable output', () => fixture({
  'Assets/Main.ts': entry('public amount: number = "wrong";')
}, root => {
  const out = compile(root, 'Assets/Main.ts');
  assert.equal(out.compiledSuccessfully, false); assert.deepEqual(out.modules, []); assert.deepEqual(out.fields, []);
  assert.match(out.diagnostics.join('\n'), /Assets\/Main\.ts:.*TS2322/);
}));

test('helper modules do not need a behaviour class', () => fixture({
  'Assets/Helper.ts': 'export function value(): number { return 3; }'
}, root => { const out = compile(root, 'Assets/Helper.ts'); assert.equal(out.compiledSuccessfully, true); assert.equal(out.className, ''); }));

test('an unrelated default class cannot masquerade as a behaviour', () => fixture({
  'Assets/Main.ts': 'class KimchilyScriptBehaviour {} export default class Fake extends KimchilyScriptBehaviour {}'
}, root => { const out = compile(root, 'Assets/Main.ts'); assert.equal(out.compiledSuccessfully, true); assert.equal(out.className, ''); }));

test('unsupported public Map is a warning, private Map is absent', () => fixture({
  'Assets/Main.ts': entry('public collection = new Map<string, number>(); private hidden = new Map<string, number>();')
}, root => {
  const out = compile(root, 'Assets/Main.ts'); assert.equal(out.compiledSuccessfully, true);
  assert.deepEqual(out.fields, []); assert.equal(out.warnings.length, 1); assert.match(out.warnings[0], /collection/);
}));

for (const specifier of ['fs', 'https', 'ZEPETO.Multiplay', '../Outside', './Missing'])
  test('rejects unavailable/outside import ' + specifier, () => fixture({
    'Assets/Main.ts': `import { danger } from '${specifier}';\n` + entry('public speed = danger;'),
    'Outside.ts': 'export const danger = 99;'
  }, root => { const out = compile(root, 'Assets/Main.ts'); assert.equal(out.compiledSuccessfully, false); assert.deepEqual(out.modules, []); }));

test('reserved serialized field names are rejected', () => fixture({
  'Assets/Main.ts': entry('public __proto__: string = "no";')
}, root => { const out = compile(root, 'Assets/Main.ts'); assert.equal(out.compiledSuccessfully, false); assert.match(out.diagnostics.join('\n'), /Reserved behaviour field/); }));

test('changing a dependency changes the source hash and emitted dependency', () => fixture({
  'Assets/Main.ts': `import { speed } from './Helper';\n` + entry('public speed = speed;'),
  'Assets/Helper.ts': 'export const speed = 45;'
}, root => {
  const before = compile(root, 'Assets/Main.ts');
  fs.writeFileSync(path.join(root, 'Assets/Helper.ts'), 'export const speed = 80;');
  const after = compile(root, 'Assets/Main.ts');
  assert.notEqual(before.sourceHash, after.sourceHash); assert.match(after.modules[0].source, /80/);
  fs.writeFileSync(path.join(root, 'Assets/Helper.ts'), 'export const speed: number = "broken";');
  const failed = compile(root, 'Assets/Main.ts'); assert.equal(failed.compiledSuccessfully, false); assert.deepEqual(failed.modules, []);
}));

test('dynamic import and reference directives are rejected', () => fixture({
  'Assets/Main.ts': '/// <reference path="../Outside.ts" />\n' + entry('Start(): void { import("UnityEngine"); }'),
  'Outside.ts': 'declare const outside: number;'
}, root => { const out = compile(root, 'Assets/Main.ts'); assert.equal(out.compiledSuccessfully, false); assert.match(out.diagnostics.join('\n'), /reference|Dynamic/); }));

test('no Node or browser global APIs appear in type completion', () => fixture({
  'Assets/Main.ts': entry('Start(): void { fetch("https://example.com"); process.exit(0); }')
}, root => { const out = compile(root, 'Assets/Main.ts'); assert.equal(out.compiledSuccessfully, false); assert.match(out.diagnostics.join('\n'), /fetch/); assert.match(out.diagnostics.join('\n'), /process/); }));

test('shipped facade sample compiles and exposes exactly its six public fields', () => fixture({
  'Assets/WorldBehaviour.ts': fs.readFileSync(path.resolve(__dirname, '../../../Samples~/RotatingObject/WorldBehaviour.ts'), 'utf8')
}, root => {
  const out = compile(root, 'Assets/WorldBehaviour.ts');
  assert.equal(out.compiledSuccessfully, true, out.diagnostics.join('\n'));
  assert.deepEqual(out.fields.map(f => [f.name, f.kind]), [['beacon','GameObject'],['speed','number'],['amplitude','number'],['label','string'],['animate','boolean'],['offset','Vector3']]);
  assert.deepEqual(out.warnings, []);
}));

test('ambient declarations cannot enable an unlisted runtime import', () => fixture({
  'Assets/Ambient.ts': 'declare module "fs" { export const danger: number; }',
  'Assets/Main.ts': 'import "./Ambient"; import { danger } from "fs";\n' + entry('public speed = danger;')
}, root => {
  const out = compile(root, 'Assets/Main.ts'); assert.equal(out.compiledSuccessfully, false);
  assert.match(out.diagnostics.join('\n'), /not a supported runtime module: fs/);
}));

test('CLI exception replaces a previous success file with empty failed output', () => fixture({
  'Assets/Main.ts': entry('public speed = 45;')
}, root => {
  const output = path.join(root, 'Library/KimchilyTypeScript/result.json');
  const args = [path.join(__dirname, '../compile.cjs'), '--project-root', root, '--entry', 'Assets/Main.ts', '--output', output];
  assert.equal(spawnSync(process.execPath, args).status, 0);
  assert.equal(JSON.parse(fs.readFileSync(output)).compiledSuccessfully, true);
  fs.writeFileSync(path.join(root, 'Assets/Main.ts'), '// oversized\n' + ' '.repeat(262145));
  assert.equal(spawnSync(process.execPath, args).status, 1);
  const failed = JSON.parse(fs.readFileSync(output)); assert.equal(failed.compiledSuccessfully, false); assert.deepEqual(failed.modules, []);
}));

test('generated export expansion cannot exceed the per-module player budget', () => {
  const source = Array.from({ length: 10500 }, (_, i) => `export const v${i} = 1;`).join('\n');
  assert.ok(source.length < 262144, 'fixture must fit the source input budget');
  return fixture({ 'Assets/Expanded.ts': source }, root => {
    const out = compile(root, 'Assets/Expanded.ts');
    assert.equal(out.compiledSuccessfully, false);
    assert.match(out.diagnostics.join('\n'), /Generated JavaScript.*262144 per module/);
    assert.deepEqual(out.modules, []); assert.deepEqual(out.fields, []);
  });
});

test('generated graph uses the total player budget in UTF-16 characters, not UTF-8 bytes', () => {
  const files = {};
  const literal = '🦀'.repeat(105000); // 210000 UTF-16 chars, 420000 UTF-8 bytes.
  for (let i = 0; i < 5; i++) files[`Assets/Chunk${i}.ts`] = `export const text = '${literal}';`;
  const imports = count => Array.from({ length: count }, (_, i) => `import './Chunk${i}';`).join('\n') + '\n' + entry('public speed = 45;');
  files['Assets/Main.ts'] = imports(4);
  return fixture(files, root => {
    const valid = compile(root, 'Assets/Main.ts');
    assert.equal(valid.compiledSuccessfully, true, valid.diagnostics.join('\n'));
    assert.ok(valid.modules.every(m => m.source.length <= 262144));
    assert.ok(valid.modules.reduce((sum, m) => sum + Buffer.byteLength(m.source, 'utf8'), 0) > 1048576,
      'the valid graph deliberately exceeds one MiB of UTF-8 bytes');
    fs.writeFileSync(path.join(root, 'Assets/Main.ts'), imports(5));
    const oversized = compile(root, 'Assets/Main.ts');
    assert.equal(oversized.compiledSuccessfully, false);
    assert.match(oversized.diagnostics.join('\n'), /module graph.*1048576 total/);
    assert.deepEqual(oversized.modules, []); assert.deepEqual(oversized.fields, []);
  });
});
