#!/usr/bin/env node
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const ts = require('typescript');
if (ts.version !== '5.9.3') throw new Error('Kimchily requires exactly TypeScript 5.9.3; run npm ci --ignore-scripts.');

const API_MODULES = new Set(['Kimchily.Script', 'UnityEngine']);
// Match TypeScriptVm's generated-code budgets. JavaScript string.length and C#
// string.Length both count UTF-16 code units, rather than UTF-8 bytes/code points.
const MAXIMUM_MODULE_CHARACTERS = 262144;
const MAXIMUM_TOTAL_CHARACTERS = 1048576;
const RESERVED = new Set(['__proto__', 'prototype', 'constructor', 'gameObject', 'transform', 'enabled',
  'Awake', 'Start', 'Update', 'OnEnable', 'OnDisable', 'OnDestroy', 'StartCoroutine', 'StopCoroutine', 'StopAllCoroutines']);
const slash = p => p.replace(/\\/g, '/');
const inside = (root, file) => { const r = path.relative(root, file); return r !== '..' && !r.startsWith('..' + path.sep) && !path.isAbsolute(r); };

function compile(projectRoot, entry) {
  const result = { apiVersion: 1, entryModule: '', className: '', sourceHash: '', compilerVersion: ts.version,
    compiledSuccessfully: false, diagnostics: [], warnings: [], modules: [], fields: [], dependencies: [] };
  const root = fs.realpathSync(projectRoot);
  const assets = fs.realpathSync(path.join(root, 'Assets'));
  if (!inside(root, assets)) throw new Error('Assets must be inside the project, not an external symbolic link.');
  const typings = fs.realpathSync(path.resolve(__dirname, '../../Typings~/kimchily.d.ts'));
  const libRoot = fs.realpathSync(path.dirname(require.resolve('typescript/lib/lib.d.ts')));
  const toolFiles = [__filename, path.join(__dirname, 'package.json'), path.join(__dirname, 'package-lock.json'), typings];
  const read = new Map();
  let total = 0;
  function assetFile(file) {
    const absolute = path.resolve(file);
    if (!inside(assets, absolute) || slash(absolute).split('/').some(s => s === 'node_modules' || s === '.git')) return false;
    if (!/\.tsx?$/i.test(absolute) || /\.d\.ts$/i.test(absolute) || /\.tsx$/i.test(absolute)) return false;
    try { return inside(assets, fs.realpathSync(absolute)); } catch { return false; }
  }
  function safeRead(file) {
    const absolute = path.resolve(file);
    if (read.has(absolute)) return read.get(absolute);
    const allowed = absolute === typings || (inside(libRoot, absolute) && /^lib(?:\.[a-z0-9]+)*\.d\.ts$/i.test(path.basename(absolute))) || assetFile(absolute);
    if (!allowed) return undefined;
    try {
      const text = fs.readFileSync(absolute, 'utf8');
      if (assetFile(absolute)) {
        if (text.length > 262144) throw new Error('TypeScript source exceeds 262144 characters: ' + slash(path.relative(root, absolute)));
        total += text.length;
        if (total > 2097152 || [...read.keys()].filter(assetFile).length >= 64) throw new Error('TypeScript module graph exceeds 64 modules or 2 MiB source.');
      }
      read.set(absolute, text); return text;
    } catch (error) { if (error.code === 'ENOENT') return undefined; throw error; }
  }
  const entryPath = path.resolve(root, entry);
  if (!assetFile(entryPath)) throw new Error('Entry must be an existing .ts file inside project Assets (no symlinks outside Assets).');
  result.entryModule = slash(path.relative(root, entryPath)).replace(/\.ts$/i, '');
  const options = { target: ts.ScriptTarget.ES2018, module: ts.ModuleKind.CommonJS, moduleResolution: ts.ModuleResolutionKind.Node10,
    strict: true, noEmitOnError: true, sourceMap: true, inlineSources: true, noEmit: false,
    esModuleInterop: false, skipLibCheck: false, types: [], lib: ['lib.es2018.d.ts'],
    rootDir: assets, outDir: path.join(root, 'Library', 'KimchilyTypeScript', 'virtual-emit') };
  const host = ts.createCompilerHost(options);
  host.readFile = safeRead;
  host.fileExists = file => assetFile(file) || path.resolve(file) === typings || (inside(libRoot, path.resolve(file)) && fs.existsSync(file));
  host.getSourceFile = (file, languageVersion) => { const text = safeRead(file); return text === undefined ? undefined : ts.createSourceFile(file, text, languageVersion, true); };
  host.resolveModuleNames = (names, containingFile) => names.map(name => {
    if (API_MODULES.has(name)) return { resolvedFileName: typings, extension: ts.Extension.Dts, isExternalLibraryImport: true };
    if (!name.startsWith('./') && !name.startsWith('../')) return undefined;
    const base = path.resolve(path.dirname(containingFile), name);
    const candidates = name.endsWith('.js') ? [base.slice(0, -3) + '.ts'] : [base + '.ts', path.join(base, 'index.ts')];
    const found = candidates.find(assetFile);
    return found ? { resolvedFileName: found, extension: ts.Extension.Ts, isExternalLibraryImport: false } : undefined;
  });
  const outputs = new Map();
  host.writeFile = (file, text, bom, onError, sourceFiles) => {
    const source = sourceFiles && sourceFiles.find(s => assetFile(s.fileName));
    if (!source) return;
    const id = slash(path.relative(root, source.fileName)).replace(/\.ts$/i, '');
    const module = outputs.get(id) || { id, source: '', sourceMap: '' };
    if (file.endsWith('.js.map')) module.sourceMap = text;
    else if (file.endsWith('.js')) module.source = text.replace(/^\/\/# sourceMappingURL=.*$/gm, '').trimEnd() + '\n';
    outputs.set(id, module);
  };
  const program = ts.createProgram([entryPath, typings], options, host);
  const checker = program.getTypeChecker();
  const diagnostics = ts.getPreEmitDiagnostics(program);
  function formatDiagnostic(d) {
    const message = ts.flattenDiagnosticMessageText(d.messageText, '\n');
    if (!d.file) return `TS${d.code}: ${message}`;
    const loc = d.file.getLineAndCharacterOfPosition(d.start || 0);
    return `${slash(path.relative(root, d.file.fileName))}:${loc.line + 1}:${loc.character + 1} TS${d.code}: ${message}`;
  }
  result.diagnostics.push(...diagnostics.filter(d => d.category === ts.DiagnosticCategory.Error).map(formatDiagnostic));
  result.warnings.push(...diagnostics.filter(d => d.category !== ts.DiagnosticCategory.Error).map(formatDiagnostic));
  function customError(node, message) {
    const file = node.getSourceFile(); const loc = file.getLineAndCharacterOfPosition(node.getStart());
    result.diagnostics.push(`${slash(path.relative(root, file.fileName))}:${loc.line + 1}:${loc.character + 1} ${message}`);
  }
  for (const file of program.getSourceFiles().filter(s => assetFile(s.fileName))) {
    function visit(node) {
      // CommonJS runtime resolution is deliberately limited to statically declared imports.
      if ((ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) && node.moduleSpecifier && ts.isStringLiteral(node.moduleSpecifier)) {
        const name = node.moduleSpecifier.text;
        if (!API_MODULES.has(name) && !name.startsWith('./') && !name.startsWith('../'))
          customError(node, 'Import is not a supported runtime module: ' + name);
      }
      if (ts.isCallExpression(node) && (node.expression.kind === ts.SyntaxKind.ImportKeyword ||
          (ts.isIdentifier(node.expression) && ['require', 'eval', 'Function'].includes(node.expression.text))))
        customError(node, 'Dynamic loading/evaluation is not supported. Use a static import.');
      if (ts.isNewExpression(node) && ts.isIdentifier(node.expression) && node.expression.text === 'Function')
        customError(node, 'Dynamic evaluation is not supported.');
      if (ts.isImportEqualsDeclaration(node)) customError(node, 'Use ES import syntax, not import = require.');
      if (ts.isReferenceFileDirective && ts.isReferenceFileDirective(node)) customError(node, 'Reference directives are not supported.');
      ts.forEachChild(node, visit);
    }
    visit(file);
    if (file.referencedFiles.length || file.typeReferenceDirectives.length) customError(file, 'Triple-slash reference directives are not supported.');
  }
  const source = program.getSourceFile(entryPath);
  const moduleSymbol = checker.getSymbolAtLocation(source);
  let exported = moduleSymbol && checker.getExportsOfModule(moduleSymbol).find(s => s.name === 'default');
  if (exported && (exported.flags & ts.SymbolFlags.Alias)) exported = checker.getAliasedSymbol(exported);
  const cls = exported && (exported.declarations || []).find(ts.isClassDeclaration);
  function isBehaviour(type, seen = new Set()) {
    if (!type || seen.has(type)) return false; seen.add(type);
    const sym = type.getSymbol();
    if (sym && sym.name === 'KimchilyScriptBehaviour' && (sym.declarations || []).some(d => path.resolve(d.getSourceFile().fileName) === typings)) return true;
    return (type.getBaseTypes ? type.getBaseTypes() || [] : []).some(t => isBehaviour(t, seen));
  }
  const classType = cls && checker.getTypeAtLocation(cls);
  if (classType && isBehaviour(classType)) {
    result.className = cls.name ? cls.name.text : 'default';
    for (const property of checker.getPropertiesOfType(classType)) {
      const declaration = (property.declarations || []).find(d => assetFile(d.getSourceFile().fileName) &&
        (ts.isPropertyDeclaration(d) || (ts.isParameter(d) && ts.isParameterPropertyDeclaration(d, d.parent))));
      if (!declaration) continue;
      const modifiers = ts.getCombinedModifierFlags(declaration);
      if (RESERVED.has(property.name)) { customError(declaration, 'Reserved behaviour field name: ' + property.name); continue; }
      if (modifiers & (ts.ModifierFlags.Private | ts.ModifierFlags.Protected | ts.ModifierFlags.Static)) continue;
      if (ts.isPrivateIdentifier(declaration.name)) continue;
      if (!/^[A-Za-z_][A-Za-z0-9_]{0,79}$/.test(property.name)) { customError(declaration, 'Inspector field names must be ASCII identifiers of at most 80 characters.'); continue; }
      let type = checker.getTypeOfSymbolAtLocation(property, declaration);
      if (type.isUnion()) {
        const members = type.types.filter(t => !(t.flags & (ts.TypeFlags.Null | ts.TypeFlags.Undefined)));
        if (members.length === 1) type = members[0];
      }
      let kind = '';
      if (type.flags & ts.TypeFlags.NumberLike) kind = 'number';
      else if (type.flags & ts.TypeFlags.StringLike) kind = 'string';
      else if (type.flags & ts.TypeFlags.BooleanLike || type.flags === ts.TypeFlags.Boolean ||
               (type.isUnion() && type.types.every(t => t.flags & ts.TypeFlags.BooleanLiteral))) kind = 'boolean';
      else {
        const symbol = type.getSymbol();
        if (symbol && ['GameObject', 'Transform', 'Vector3'].includes(symbol.name) &&
            (symbol.declarations || []).some(d => path.resolve(d.getSourceFile().fileName) === typings)) kind = symbol.name;
      }
      if (kind && !(modifiers & ts.ModifierFlags.Readonly)) result.fields.push({ name: property.name, kind });
      else result.warnings.push(`Inspector does not support public field '${property.name}': ${checker.typeToString(type)}${modifiers & ts.ModifierFlags.Readonly ? ' (readonly)' : ''}. Its class initializer is used.`);
    }
    if (result.fields.length > 128) customError(cls, 'A behaviour can expose at most 128 Inspector fields.');
  }
  result.dependencies = [...read.keys()].filter(p => assetFile(p) || p === typings).sort().map(p => slash(p));
  result.dependencies.push(...toolFiles.filter(p => p !== typings).map(slash));
  const hash = crypto.createHash('sha256');
  hash.update('kimchily-typescript-api-1\0' + ts.version + '\0');
  for (const file of [...new Set(result.dependencies)].sort()) {
    const label = inside(root, file) ? slash(path.relative(root, file)) : inside(__dirname, file) ? 'compiler/' + slash(path.relative(__dirname, file)) : 'typings/kimchily.d.ts';
    hash.update(label + '\0'); hash.update(fs.readFileSync(file)); hash.update('\0');
  }
  result.sourceHash = hash.digest('hex');
  if (result.diagnostics.length === 0) {
    const emit = program.emit();
    result.diagnostics.push(...emit.diagnostics.filter(d => d.category === ts.DiagnosticCategory.Error).map(formatDiagnostic));
    if (!emit.emitSkipped && !result.diagnostics.length) {
      result.modules = [...outputs.values()].filter(m => m.source).sort((a, b) => a.id.localeCompare(b.id));
      let generatedCharacters = 0;
      for (const module of result.modules) {
        generatedCharacters += module.source.length;
        if (module.source.length > MAXIMUM_MODULE_CHARACTERS)
          result.diagnostics.push(`${module.id}.ts: Generated JavaScript is ${module.source.length} UTF-16 characters; the player limit is ${MAXIMUM_MODULE_CHARACTERS} per module. Split this module before publishing.`);
      }
      if (generatedCharacters > MAXIMUM_TOTAL_CHARACTERS)
        result.diagnostics.push(`Generated JavaScript module graph is ${generatedCharacters} UTF-16 characters; the player limit is ${MAXIMUM_TOTAL_CHARACTERS} total. Reduce the script graph before publishing.`);
      result.compiledSuccessfully = result.diagnostics.length === 0 && result.modules.some(m => m.id === result.entryModule);
    }
  }
  if (!result.compiledSuccessfully) { result.modules = []; result.fields = []; }
  return result;
}

if (require.main === module) {
  const args = {};
  for (let i = 2; i < process.argv.length; i += 2) args[process.argv[i]] = process.argv[i + 1];
  let result, safeOutput;
  try {
    if (!args['--project-root'] || !args['--entry'] || !args['--output']) throw new Error('Usage: node compile.cjs --project-root <Unity project> --entry Assets/Example.ts --output <project>/Library/KimchilyTypeScript/result.json');
    const root = fs.realpathSync(args['--project-root']);
    const output = path.resolve(args['--output']);
    if (!inside(path.join(root, 'Library', 'KimchilyTypeScript'), output)) throw new Error('Output must be inside project Library/KimchilyTypeScript.');
    safeOutput = output;
    // The same output name may be reused by a command-line build. Clear the old
    // success before any compiler operation that can fail or be interrupted.
    if (fs.existsSync(output)) fs.unlinkSync(output);
    result = compile(root, args['--entry']);
    fs.mkdirSync(path.dirname(output), { recursive: true });
    fs.writeFileSync(output, JSON.stringify(result, null, 2));
    process.exitCode = result.compiledSuccessfully ? 0 : 1;
  } catch (error) {
    if (safeOutput) {
      fs.mkdirSync(path.dirname(safeOutput), { recursive: true });
      fs.writeFileSync(safeOutput, JSON.stringify({ apiVersion: 1, entryModule: '', className: '', sourceHash: '', compilerVersion: ts.version,
        compiledSuccessfully: false, diagnostics: [String(error.message || error)], warnings: [], modules: [], fields: [], dependencies: [] }, null, 2));
    }
    process.stderr.write(String(error.message || error) + '\n'); process.exitCode = 1;
  }
}
module.exports = { compile };
