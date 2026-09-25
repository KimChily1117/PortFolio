from pathlib import Path
import json
import re

sdk = Path(__file__).resolve().parents[1]
workspace = sdk.parent
package = sdk / "Packages" / "com.kimchily.creator"
errors = []
guids = {}
for path in package.rglob("*"):
    if not path.is_file():
        continue
    if path.suffix in (".json", ".asmdef"):
        json.loads(path.read_text(encoding="utf-8-sig"))
    if path.suffix == ".meta":
        match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(), re.M)
        if not match:
            errors.append(f"Invalid GUID: {path}")
        elif match[1] in guids:
            errors.append(f"Duplicate GUID: {path} / {guids[match[1]]}")
        else:
            guids[match[1]] = path
    elif not any(part.endswith("~") for part in path.relative_to(package).parts):
        if not Path(str(path) + ".meta").exists():
            errors.append(f"Missing metadata: {path}")
for name in ("UnityKimchilyCreator", "UnityKimchilyWorld", "UnityToolManager"):
    path = workspace / name / "Packages" / "manifest.json"
    dependency = json.loads(path.read_text(encoding="utf-8-sig"))["dependencies"]["com.kimchily.creator"]
    resolved = (path.parent / dependency.removeprefix("file:")).resolve()
    if resolved != package.resolve():
        errors.append(f"Wrong local UPM target: {name}: {resolved}")
tests = sum(len(re.findall(r"\[(?:UnityTest|Test)\]", p.read_text(encoding="utf-8-sig")))
            for base in (package / "Tests", sdk / "ValidationProject" / "Assets" / "Tests")
            for p in base.rglob("*.cs"))
print(f"Package metadata GUIDs: {len(guids)}; Unity test methods: {tests}")
print("Three project UPM references checked.")
for error in errors:
    print(error)
if errors:
    raise SystemExit(1)
print("STATIC AUDIT PASSED. This does not run the Unity engine.")
