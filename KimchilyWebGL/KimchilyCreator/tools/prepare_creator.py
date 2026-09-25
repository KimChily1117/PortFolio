"""Prepare the independent 2022.3 creator; preserve all existing user assets."""
from pathlib import Path
import hashlib
import shutil

project = Path(__file__).resolve().parents[1]
workspace = project.parent
source = workspace / "UnityKimchilyWorld/Assets/KimchilyCreatorTool/CharactorResources/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Meshes_Humans/HumanMale_Character.fbx"
model = project / "Assets/World/Model.fbx"
if not model.exists():
    model.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, model)
for item in (project / "Assets").rglob("*"):
    if item.suffix == ".meta" or item.suffix in (".fbx", ".lua", ".ts"):
        continue
    meta = Path(str(item) + ".meta")
    if meta.exists():
        continue
    guid = hashlib.sha256(("kimchily.creator.project/" + item.relative_to(project).as_posix()).encode()).hexdigest()[:32]
    value = f"fileFormatVersion: 2\nguid: {guid}\n"
    if item.is_dir():
        value += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif item.suffix == ".cs":
        value += "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
    elif item.suffix == ".asmdef":
        value += "AssemblyDefinitionImporter:\n  externalObjects: {}\n"
    else:
        value += "DefaultImporter:\n  externalObjects: {}\n"
    meta.write_text(value, encoding="utf-8")
(project / "Artifacts").mkdir(exist_ok=True)
print("Creator project prepared; existing model/scene/scripts preserved.")
