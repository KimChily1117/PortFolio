"""Prepare local verification content without modifying the original projects."""
from pathlib import Path
import hashlib
import os
import json
import shutil

project = Path(__file__).resolve().parents[1]
workspace = project.parent
source = workspace / "UnityKimchilyWorld/Assets/KimchilyCreatorTool/CharactorResources/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Meshes_Humans/HumanMale_Character.fbx"
target = project / "Assets/Fixtures/Model.fbx"
target.parent.mkdir(parents=True, exist_ok=True)
if not source.is_file():
    raise SystemExit(f"Missing local FBX fixture: {source}")
shutil.copy2(source, target)

cache = Path(os.environ["LOCALAPPDATA"]) / "Unity/cache/packages/packages.unity.com"
manifest = project / "Packages/manifest.json"
data = json.loads(manifest.read_text(encoding="utf-8"))
for name, version in (("com.unity.test-framework", "1.1.33"), ("com.unity.ext.nunit", "1.0.6")):
    package = cache / f"{name}@{version}"
    if not (package / "package.json").is_file():
        raise SystemExit(f"Missing local validation dependency: {package}")
    data["dependencies"][name] = "file:" + package.as_posix()
manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

for path in sorted((project / "Assets").rglob("*")):
    if path.suffix == ".meta" or "Generated" in path.parts or "Fixtures" in path.parts:
        continue
    meta = Path(str(path) + ".meta")
    if meta.exists():
        continue
    guid = hashlib.sha256(("kimchily.world/" + path.relative_to(project).as_posix()).encode()).hexdigest()[:32]
    text = f"fileFormatVersion: 2\nguid: {guid}\n"
    if path.is_dir():
        text += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif path.suffix == ".cs":
        text += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
    elif path.suffix == ".asmdef":
        text += "AssemblyDefinitionImporter:\n  externalObjects: {}\n"
    else:
        text += "DefaultImporter:\n  externalObjects: {}\n"
    meta.write_text(text, encoding="utf-8")
(project / "Artifacts").mkdir(exist_ok=True)
print("Prepared Android Unity runtime project and local FBX fixture.")
