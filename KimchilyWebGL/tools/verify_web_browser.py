"""Exercise our local Web build in an isolated desktop Chromium process."""
import argparse
import json
import re
from pathlib import Path
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools" / "python"))
from playwright.sync_api import sync_playwright


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", default="http://127.0.0.1:8788/player/")
    parser.add_argument("--name", default="demo")
    parser.add_argument("--typescript-marker", default="KIMCHILY_WEB_TYPESCRIPT_READY")
    parser.add_argument("--require-animation", action="store_true")
    parser.add_argument("--touch", action="store_true", help="Exercise Chromium touch input at a phone-sized viewport; this is not Safari validation.")
    args = parser.parse_args()
    output = ROOT / "Artifacts" / "web-browser" / args.name
    output.mkdir(parents=True, exist_ok=True)
    (output / "console.jsonl").write_text("", encoding="utf-8")
    messages, failures, requests = [], [], []
    result = {"url": args.url, "browser": "isolated Chrome with touch emulation" if args.touch else "isolated desktop Chrome", "iphoneVerified": False, "passed": False}
    started = time.monotonic()
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            executable_path=r"C:\Program Files\Google\Chrome\Application\chrome.exe",
            headless=True, args=["--enable-webgl", "--enable-unsafe-swiftshader"],
        )
        context = browser.new_context(viewport={"width": 844, "height": 390} if args.touch else {"width": 1280, "height": 720}, has_touch=args.touch, device_scale_factor=1)
        page = context.new_page()
        def record_console(message):
            record = {"type": message.type, "text": message.text}
            messages.append(record)
            with (output / "console.jsonl").open("a", encoding="utf-8") as stream:
                stream.write(json.dumps(record, ensure_ascii=False) + "\n")
        page.on("console", record_console)
        page.on("pageerror", lambda error: failures.append(str(error)))
        page.on("requestfailed", lambda request: requests.append({"url": request.url, "error": request.failure}))
        page.add_init_script("window.__kimchilyEvents=[]; addEventListener('kimchily-world-event',event=>window.__kimchilyEvents.push(event.detail));")
        try:
            response = page.goto(args.url, wait_until="networkidle", timeout=60000)
            if response.status != 200:
                raise AssertionError(f"Player HTTP {response.status}")
            page.locator("#start").click()
            page.wait_for_function("window.__kimchilyEvents.some(event=>['WorldReady','WorldFailed'].includes(event.type)) || document.querySelector('#reload').hidden===false", timeout=240000)
            if not page.evaluate("window.__kimchilyEvents.some(event=>event.type==='WorldReady')"):
                raise AssertionError(page.locator("#status").inner_text())
            page.wait_for_timeout(1500)
            if args.typescript_marker and not any(args.typescript_marker in item["text"] for item in messages):
                raise AssertionError("TypeScript execution marker was not observed.")
            result["firstEntrySeconds"] = round(time.monotonic() - started, 2)
            page.screenshot(path=str(output / "world-ready.png"))
            page.locator("#unity-canvas").focus()
            if args.touch:
                session = context.new_cdp_session(page)
                size = 390 * .26
                center_x = size * .68
                center_y = 390 - size * .68
                def touch(kind, points):
                    session.send("Input.dispatchTouchEvent", {"type": kind, "touchPoints": points})
                point = {"id": 1, "x": center_x, "y": center_y}
                touch("touchStart", [point])
                page.wait_for_timeout(100)
                point["x"] += size * .34 * .3
                touch("touchMove", [point])
                page.wait_for_timeout(650)
                page.screenshot(path=str(output / "world-walking.png"))
                point["x"] = center_x + size * .34
                touch("touchMove", [point])
                page.wait_for_timeout(400)
                page.screenshot(path=str(output / "world-moving.png"))
                touch("touchEnd", [])
                page.wait_for_timeout(150)
                jump = {"id": 2, "x": 844 - size * .51, "y": 390 - size * .51}
                touch("touchStart", [jump])
                page.wait_for_timeout(150)
                touch("touchEnd", [])
                page.wait_for_timeout(250)
            else:
                page.keyboard.down("a")
                page.wait_for_timeout(500)
                page.screenshot(path=str(output / "world-moving.png"))
                page.keyboard.up("a")
                page.wait_for_timeout(150)
                page.keyboard.down("Space")
                page.wait_for_timeout(150)
                page.keyboard.up("Space")
                page.wait_for_timeout(250)
            page.screenshot(path=str(output / "world-jump.png"))
            page.wait_for_timeout(1000)
            console_text = "\n".join(item["text"] for item in messages)
            distances = [float(value) for value in re.findall(r"MoveStop distance=([0-9.]+)", console_text)]
            if not distances or max(distances) < 0.1:
                raise AssertionError("Keyboard input did not produce measurable character movement.")
            if "[Kimchily Player] Jump position=" not in console_text or "independentRig=True" not in console_text:
                raise AssertionError("Character jump or independent third-person camera was not observed.")
            if args.require_animation and ("Animation=Run" not in console_text or "Animation=Jump" not in console_text):
                raise AssertionError("Mapped running and jumping animation states were not observed.")
            if args.touch and args.require_animation and "Animation=Walk" not in console_text:
                raise AssertionError("Partial joystick input did not produce the mapped walk animation.")
            result["movementDistances"] = distances
            if args.touch:
                stops_before_resize = len(distances)
                touch("touchStart", [{"id": 3, "x": center_x + size * .34, "y": center_y}])
                page.wait_for_timeout(150)
                page.set_viewport_size({"width": 390, "height": 844})
                page.wait_for_timeout(350)
                touch("touchEnd", [])
                page.wait_for_timeout(250)
                if sum("MoveStop distance=" in item["text"] for item in messages) <= stops_before_resize:
                    raise AssertionError("Viewport resize did not clear held joystick movement.")
                page.screenshot(path=str(output / "world-portrait.png"))
                result["portraitResizeClearedInput"] = True
            page.locator("#close").click()
            page.wait_for_function("window.__kimchilyEvents.some(event=>event.type==='WorldClosed')", timeout=45000)
            page.locator("#start").click()
            page.wait_for_function("window.__kimchilyEvents.filter(event=>event.type==='WorldReady').length>=2", timeout=90000)
            page.wait_for_timeout(800)
            page.screenshot(path=str(output / "world-reentered.png"))
            events = page.evaluate("window.__kimchilyEvents")
            errors = [item for item in events if item.get("type") == "WorldFailed"]
            runtime_errors = [item["text"].splitlines()[0] for item in messages
                              if re.search(r"Not implemented icall:|SCRIPT_INITIALIZATION_FAILED|MissingManifestResourceException|RuntimeError:|Unhandled exception", item["text"])]
            if errors or failures or requests or runtime_errors:
                raise AssertionError(f"Browser failures: host={errors}; page={failures}; requests={requests}; runtime={runtime_errors}")
            result.update(passed=True, events=events)
        except Exception as error:
            result["failure"] = str(error)
            result["events"] = page.evaluate("window.__kimchilyEvents || []")
            result["status"] = page.locator("#status").inner_text() if page.locator("#status").count() else page.title()
            page.screenshot(path=str(output / "failure.png"))
        finally:
            result.update(console=messages, pageErrors=failures, failedRequests=requests)
            (output / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
            browser.close()
    print(json.dumps({key: value for key, value in result.items() if key not in {"console", "events"}}, ensure_ascii=False))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
