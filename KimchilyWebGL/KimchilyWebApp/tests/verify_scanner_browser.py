"""Decode a real publisher QR PNG in isolated Chrome; use synthetic video, not a physical camera."""
import argparse
import json
from pathlib import Path
import sys
from urllib.parse import urlsplit, parse_qs
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / '.tools' / 'python'))
from playwright.sync_api import sync_playwright


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--launch-url', required=True)
    args = parser.parse_args()
    launch = args.launch_url
    parsed = urlsplit(launch)
    manifest = urlsplit(parse_qs(parsed.query)['manifest'][0])
    _, _, world, revision, _ = manifest.path.split('/')
    origin = f'{parsed.scheme}://{parsed.netloc}'
    png_url = f'{origin}/qr/{world}/{revision}.png'
    with urlopen(png_url, timeout=20) as response:
        png = response.read()
    output = ROOT / 'KimchilyWebApp/tests/artifacts'
    output.mkdir(parents=True, exist_ok=True)
    (output / 'publisher-qr.png').write_bytes(png)
    results = {'passed': False, 'browser': 'isolated Chrome', 'physicalCamera': False, 'iphoneVerified': False, 'qrSource': png_url, 'checks': []}
    with sync_playwright() as p:
        browser = p.chromium.launch(executable_path=r'C:\Program Files\Google\Chrome\Application\chrome.exe', headless=True)
        page = browser.new_page()
        def route(request):
            path = urlsplit(request.request.url).path
            if path == '/qr-test-harness':
                return request.fulfill(content_type='text/html', body='<video id="video" muted playsinline></video><script src="/app/vendor/jsQR-1.4.0.js"></script>')
            if path == '/fixture.png':
                return request.fulfill(content_type='image/png', body=png)
            if path.startswith('/app/'):
                file = ROOT / 'KimchilyWebApp' / path[len('/app/'):]
                return request.fulfill(content_type='application/javascript', body=file.read_bytes())
            return request.abort()
        page.route('**/*', route)
        page.goto(origin + '/qr-test-harness')
        page.evaluate("""async () => {
            window.qr = await import('/app/qr.js'); window.scannerModule = await import('/app/scanner.js');
            window.fixture = await (await fetch('/fixture.png')).blob();
            window.image = new Image(); image.src = URL.createObjectURL(fixture); await image.decode();
        }""")
        image_result = page.evaluate("""async () => {
            let result, errors=[]; const scanner=scannerModule.createQrScanner({video:document.querySelector('video'),onResult:v=>result=v,onError:e=>errors.push(e.message)});
            await scanner.scanFile(new File([fixture], 'publisher.png', {type:'image/png'})); scanner.dispose();
            return {result, errors};
        }""")
        assert image_result['result'] == launch, image_result
        assert not image_result['errors'], image_result
        results['checks'].append('Real publisher PNG decoded by local jsQR through browser image/canvas')
        assert page.evaluate('(value)=>qr.parseQrPayload(value,location.origin).worldId', launch) == world
        results['checks'].append('Decoded publisher link passed strict parser')
        page.evaluate("""async () => {
            window.capture = document.createElement('canvas'); capture.width=image.naturalWidth; capture.height=image.naturalHeight;
            capture.getContext('2d').drawImage(image,0,0); window.media=capture.captureStream(10);
            window.cameraResult=null;window.cameraErrors=[];
            window.scanner=scannerModule.createQrScanner({video:document.querySelector('video'),onResult:v=>cameraResult=v,onError:e=>cameraErrors.push(e.message),
                environment:{isSecureContext:true,mediaDevices:{getUserMedia:async()=>media}}});
            await scanner.start(); media.getVideoTracks()[0].requestFrame();
        }""")
        page.wait_for_function('cameraResult !== null || cameraErrors.length > 0', timeout=15000)
        assert page.evaluate('cameraResult') == launch, page.evaluate('cameraErrors')
        assert page.evaluate('media.getTracks().every(track=>track.readyState==="ended")')
        assert page.evaluate('document.querySelector("video").srcObject === null')
        results['checks'].append('Synthetic video decoded and all MediaStream tracks stopped on success')
        cancelled = page.evaluate("""async () => {
            scanner.dispose(); window.late=capture.captureStream(10); let resolve;
            const pending=new Promise(yes=>resolve=yes);let called=false;
            const item=scannerModule.createQrScanner({video:document.querySelector('video'),onResult:()=>called=true,environment:{isSecureContext:true,mediaDevices:{getUserMedia:()=>pending}}});
            const starting=item.start();item.stop();resolve(late);await starting;item.dispose();
            return !called && late.getTracks().every(track=>track.readyState==='ended') && document.querySelector('video').srcObject===null;
        }""")
        assert cancelled
        results['checks'].append('Late permission grant stopped after cancellation')
        page.screenshot(path=str(output / 'scanner-browser.png'))
        results['passed'] = True
        browser.close()
    (output / 'scanner-browser.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(results, ensure_ascii=False))


if __name__ == '__main__':
    main()
