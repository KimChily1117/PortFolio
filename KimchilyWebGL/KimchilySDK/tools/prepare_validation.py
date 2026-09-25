"""Prepare local-only Unity verification fixtures; does not launch Unity."""
from pathlib import Path
import os
import json
import shutil
import hashlib
import re

sdk = Path(__file__).resolve().parents[1]
workspace = sdk.parent
project = sdk / "ValidationProject"
cache = Path(os.environ["LOCALAPPDATA"]) / "Unity/cache/packages/packages.unity.com"
manifest = project / "Packages/manifest.json"
data = json.loads(manifest.read_text(encoding="utf-8-sig"))
for name, version in (("com.unity.test-framework", "1.1.33"), ("com.unity.ext.nunit", "1.0.6")):
    package = cache / f"{name}@{version}"
    if not (package / "package.json").is_file():
        raise SystemExit(f"Required local test package is absent: {package}")
    data["dependencies"][name] = "file:" + package.as_posix()
data["dependencies"]["com.unity.modules.particlesystem"] = "1.0.0"
data["dependencies"]["com.unity.ugui"] = "1.0.0"
manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
model = workspace / "UnityKimchilyWorld/Assets/KimchilyCreatorTool/CharactorResources/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Meshes_Humans/HumanMale_Character.fbx"
copies = {
    model: project / "Assets/Fixtures/Model.fbx",
    workspace / "UnityKimchilyWorld/Assets/KimchilyCreatorTool/Runtime/KimchilyBaseFramework.dll": project / "Assets/Plugins/KimchilyBaseFramework.dll",
    workspace / "UnityKimchilyWorld/Assets/Plugins/x86_64/xlua.dll": project / "Assets/Plugins/x86_64/xlua.dll",
}
for source, target in copies.items():
    if not source.is_file():
        raise SystemExit(f"Missing existing fixture: {source}")
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    if target.suffix == ".dll":
        # Minimal GUID-only metadata does not configure a usable PluginImporter.
        # Keep fixture GUIDs stable and explicitly enable the Windows test host.
        metadata = target.with_suffix(".dll.meta")
        previous = metadata.read_text(encoding="utf-8") if metadata.exists() else ""
        match = re.search(r"^guid: ([0-9a-f]{32})$", previous, re.MULTILINE)
        guid = match.group(1) if match else hashlib.sha256(
            target.relative_to(project).as_posix().encode()).hexdigest()[:32]
        cpu = "x86_64" if target.name == "xlua.dll" else "AnyCPU"
        metadata.write_text(f"""fileFormatVersion: 2
guid: {guid}
PluginImporter:
  externalObjects: {{}}
  serializedVersion: 2
  iconMap: {{}}
  executionOrder: {{}}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {{}}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: {cpu}
        OS: Windows
        DefaultValueInitialized: true
  - first:
      Standalone: Win64
    second:
      enabled: 1
      settings:
        CPU: {cpu}
  userData:
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")
(sdk / "Artifacts").mkdir(exist_ok=True)
print("Local verification fixtures prepared. Unity was not launched.")
