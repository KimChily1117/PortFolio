"""Refresh runtime JSON from preserved sources and validate every gallery model."""
import concurrent.futures
import hashlib
import importlib.util
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location('spine_adapter', ROOT/'tools/import-epicseven-spine.py')
ADAPTER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ADAPTER)

def validate(item):
    try:
        source = pathlib.Path(item['source_json'])
        raw = source.read_bytes()
        adapted = ADAPTER.adapt(json.loads(raw))
        target = ROOT/'Resources'/item['path']
        target.write_text(json.dumps(adapted, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
        check = subprocess.run([str(ROOT/'build-win/Release/aster_spine_tests.exe'),str(target),
                                str(ROOT/'Resources'/item['atlas'])],capture_output=True,text=True,timeout=60)
        if check.returncode:
            raise ValueError((check.stderr or check.stdout).strip())
        item.update(source_sha256=hashlib.sha256(raw).hexdigest(), runtime_validation=check.stdout.strip(),
                    compatibility='Spine 3.8 skin shape, Bezier curves, and identity scale defaults adapted to bundled 3.6 runtime; source retained')
        return {'item':item,'passed':True}
    except (OSError, ValueError, KeyError, IndexError, subprocess.TimeoutExpired) as error:
        return {'item':item,'passed':False,'error':str(error)}

def main():
    path = ROOT/'Resources/reference/epicseven/catalog.json'
    catalog = json.loads(path.read_text(encoding='utf-8'))
    with concurrent.futures.ProcessPoolExecutor(max_workers=4) as pool:
        results = list(pool.map(validate, catalog['items']))
    catalog['items'] = [r['item'] for r in results if r['passed']]
    catalog['failures'] += [{'source':r['item']['source_json'],'error':r['error']} for r in results if not r['passed']]
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
    temporary.replace(path)
    report = {'total':len(results),'passed':len(catalog['items']),
              'failures':[{'name':r['item']['name'],'error':r['error']} for r in results if not r['passed']]}
    (ROOT/'.deps/epicseven-assets/animated/cocos-runtime-validation.json').write_text(
        json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False))

if __name__ == '__main__':
    main()
