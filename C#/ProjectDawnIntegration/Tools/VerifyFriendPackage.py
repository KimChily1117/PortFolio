"""Check the distributable against its manifest and the actual Unity build."""
from pathlib import Path
import hashlib, json, sys, zipfile

root = Path(__file__).resolve().parents[1]
folder = Path(sys.argv[1]).resolve()
rows = json.loads((folder/'files.json').read_text(encoding='utf-8-sig'))
manifest = json.loads((folder/'manifest.json').read_text(encoding='utf-8-sig'))
archive = folder/'ProjectDawn-FriendTest.zip'
assert hashlib.sha256(archive.read_bytes()).hexdigest() == manifest['sha256']
forbidden = {'.invite', '.pem', '.pfx', '.key', '.sql', '.bak', '.env'}
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None, 'Archive CRC failed'
    files = {n for n in z.namelist() if not n.endswith('/')}
    expected = {'ProjectDawn/'+r['path'] for r in rows}
    assert files == expected, 'Manifest/file-set mismatch'
    for row in rows:
        p = Path(row['path'])
        assert p.suffix.lower() not in forbidden
        data = z.read('ProjectDawn/'+row['path'])
        assert len(data) == row['bytes']
        assert hashlib.sha256(data).hexdigest() == row['sha256']
        original = root/'Build'/p
        if p.parts[0] in {'ProjectDawnRaid.exe','UnityPlayer.dll','UnityCrashHandler64.exe',
                          'MonoBleedingEdge','ProjectDawnRaid_Data'}:
            assert original.is_file(), str(original)
            assert hashlib.sha256(original.read_bytes()).hexdigest() == row['sha256'], str(p)
    endpoint = json.loads(z.read('ProjectDawn/playtest.server.json').decode('utf-8-sig'))
    assert endpoint['server'] == 'tls://203.0.113.10' and endpoint['port'] == 28443
report = dict(passed=True, files=len(files), bytes=archive.stat().st_size,
              sha256=manifest['sha256'], crc=True, hashes=True, actualBuildMatch=True,
              forbiddenFileExtensionsAbsent=True, endpoint=endpoint['server']+':'+str(endpoint['port']))
(folder/'package-verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
