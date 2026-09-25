"""Verify local Expo Go manifests and bundles; does not simulate a native device."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import sys
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT.parent / 'KimchilyPublish' / '.deps'))
import qrcode

origin = 'http://192.168.0.4:8081'
output = ROOT / 'Artifacts'
output.mkdir(exist_ok=True)
result = {'checkedUtc': datetime.now(timezone.utc).isoformat(),
          'nativeDeviceVerified': False, 'expoGoUrl': 'exp://192.168.0.4:8081',
          'platforms': {}}
for platform in ('ios', 'android'):
    request = urllib.request.Request(origin, headers={
        'expo-platform': platform, 'expo-protocol-version': '1',
        'expo-expect-signature': 'keyid="expo-root", alg="rsa-v1_5-sha256"',
        'accept': 'application/expo+json,application/json'})
    with urllib.request.urlopen(request, timeout=20) as response:
        assert response.status == 200
        assert response.headers.get('expo-signature'), 'Expo Go requires a signed development manifest'
        manifest = json.load(response)
    assert manifest['runtimeVersion'] == 'exposdk:57.0.0'
    client = manifest['extra']['expoClient']
    assert client['hostUri'] == '192.168.0.4:8081'
    assert client['name'] == 'Kimchily'
    assert client.get('owner') == 'kimchily'
    assert not manifest['extra']['scopeKey'].startswith('@anonymous/')
    with urllib.request.urlopen(manifest['launchAsset']['url'], timeout=180) as response:
        assert response.status == 200
        bundle = response.read()
    assert len(bundle) > 100000
    result['platforms'][platform] = {
        'runtimeVersion': manifest['runtimeVersion'],
        'owner': client['owner'], 'scopeKey': manifest['extra']['scopeKey'],
        'signedManifest': True,
        'bundleBytes': len(bundle), 'sha256': hashlib.sha256(bundle).hexdigest()}
qrcode.make(result['expoGoUrl']).save(output / 'expo-go-qr.png')
result['passed'] = True
(output / 'dev-server-check.json').write_text(
    json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(result, indent=2))
