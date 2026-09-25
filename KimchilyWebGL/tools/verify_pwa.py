"""Local home -> QR image -> Unity -> home integration in isolated Chrome."""
import base64
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'.tools'/'python'))
sys.path.insert(0, str(ROOT/'KimchilyPublish'/'.deps'))
from cryptography import x509
from cryptography.hazmat.primitives import serialization
from playwright.sync_api import sync_playwright

output = ROOT/'Artifacts'/'pwa-browser'
output.mkdir(parents=True, exist_ok=True)
cert = x509.load_pem_x509_certificate((ROOT/'KimchilyPublish'/'.local'/'tls'/'server.pem').read_bytes())
spki = cert.public_key().public_bytes(serialization.Encoding.DER, serialization.PublicFormat.SubjectPublicKeyInfo)
pin = base64.b64encode(hashlib.sha256(spki).digest()).decode()
publication = json.loads((ROOT/'KimchilyCreator'/'Artifacts'/'publish-result.json').read_text(encoding='utf-8-sig'))
result = {'passed': False, 'device': 'isolated desktop Chrome; development certificate pinned for this process', 'physicalCameraVerified': False}
with sync_playwright() as pw:
    browser = pw.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe',
        headless=True, args=['--enable-webgl', '--enable-unsafe-swiftshader', '--ignore-certificate-errors-spki-list='+pin])
    context = browser.new_context(viewport={'width': 390, 'height': 844}, has_touch=True)
    page = context.new_page()
    errors=[]
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.add_init_script("window.__worldEvents=[];addEventListener('kimchily-world-event',event=>window.__worldEvents.push(event.detail));")
    try:
        page.goto('http://192.168.0.4:8788/', wait_until='networkidle')
        page.locator('.world-card').first.wait_for()
        page.screenshot(path=str(output/'home-http.png'))
        page.locator('#open-scan').click()
        assert page.locator('#start-camera').is_disabled()
        page.locator('#entry-dialog').evaluate('(element)=>element.close()')
        page.goto('https://192.168.0.4:8789/', wait_until='networkidle')
        page.locator('.world-card').first.wait_for()
        assert page.evaluate('isSecureContext')
        page.wait_for_function("navigator.serviceWorker.controller !== null", timeout=20000)
        page.screenshot(path=str(output/'home-https.png'))
        page.locator('#open-scan').click()
        assert page.locator('#start-camera').is_enabled()
        # Read the existing HTTP QR inside the HTTPS application; alias rebasing
        # must pass server revision/hash checks before loading the world.
        response = context.request.get(publication['qrUrl'])
        assert response.status == 200
        page.locator('input[type=file]').set_input_files({'name':'published-world.png','mimeType':'image/png','buffer':response.body()})
        page.wait_for_url('https://192.168.0.4:8789/player/**', timeout=20000)
        page.locator('#start').click()
        page.wait_for_function("window.__worldEvents.some(event=>event.type==='WorldReady')", timeout=180000)
        page.screenshot(path=str(output/'world.png'))
        page.locator('#close').click()
        page.wait_for_url('https://192.168.0.4:8789/', timeout=20000)
        page.locator('.recent-card').first.wait_for()
        page.screenshot(path=str(output/'home-return.png'))
        context.set_offline(True)
        page.reload(wait_until='domcontentloaded')
        page.locator('#offline-banner').wait_for(state='visible')
        page.screenshot(path=str(output/'offline-home.png'))
        assert not errors, errors
        result.update(passed=True, httpsSecureContext=True, qrImageToWorld=True,
                      httpQrRebasedToHttps=True, unityWorldReady=True, exitToHome=True, offlineHome=True)
    except Exception as error:
        result['failure']=str(error)
        page.screenshot(path=str(output/'failure.png'))
    finally:
        result['pageErrors']=errors
        (output/'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        browser.close()
print(json.dumps(result, ensure_ascii=False))
raise SystemExit(0 if result['passed'] else 1)
