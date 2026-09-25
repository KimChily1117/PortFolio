"""Create stable Unity metadata only when absent; never overwrite existing GUIDs."""
from pathlib import Path
import hashlib

root = Path(__file__).resolve().parents[1] / "Packages" / "com.kimchily.creator"
created = 0
for path in sorted(root.rglob("*")):
    if path.suffix == ".meta" or any(part.endswith("~") for part in path.relative_to(root).parts):
        continue
    meta = Path(str(path) + ".meta")
    if meta.exists():
        continue
    rel = path.relative_to(root).as_posix()
    guid = hashlib.sha256(("kimchily.creator.v1/" + rel).encode()).hexdigest()[:32]
    header = f"fileFormatVersion: 2\nguid: {guid}\n"
    if path.is_dir():
        text = header + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif path.suffix == ".cs":
        text = header + "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    elif path.suffix == ".asmdef":
        text = header + "AssemblyDefinitionImporter:\n  externalObjects: {}\n"
    elif path.suffix in (".json", ".txt"):
        text = header + "TextScriptImporter:\n  externalObjects: {}\n"
    else:
        text = header + "DefaultImporter:\n  externalObjects: {}\n"
    meta.write_text(text, encoding="utf-8")
    created += 1
print(f"Created {created} missing package metadata files.")

