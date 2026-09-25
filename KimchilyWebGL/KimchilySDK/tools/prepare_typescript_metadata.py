"""Add stable metadata for new authored files; preserve all existing Unity GUIDs."""
from pathlib import Path
import hashlib

workspace = Path(__file__).resolve().parents[2]
targets = [
    (workspace / 'KimchilySDK/Packages/com.kimchily.typescript', 'kimchily.typescript.v1/'),
    (workspace / 'KimchilyCreator/Assets', 'kimchily.creator.project/'),
    (workspace / 'KimchilyUnityRuntime/Assets', 'kimchily.world/'),
]
created = 0
for root, prefix in targets:
    for path in sorted(root.rglob('*')):
        relative = path.relative_to(root)
        if path.name.startswith('.') or path.suffix == '.meta' or any(part.endswith('~') or part.startswith('.') for part in relative.parts):
            continue
        if path.suffix in ('.fbx', '.lua', '.ts', '.unity', '.mat', '.prefab'):
            continue  # These require their actual Unity importer/serialization metadata.
        meta = Path(str(path) + '.meta')
        if meta.exists():
            continue
        guid = hashlib.sha256((prefix + relative.as_posix()).encode()).hexdigest()[:32]
        value = f'fileFormatVersion: 2\nguid: {guid}\n'
        if path.is_dir():
            value += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
        elif path.suffix == '.cs':
            value += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
        elif path.suffix == '.asmdef':
            value += 'AssemblyDefinitionImporter:\n  externalObjects: {}\n'
        elif path.suffix == '.dll':
            value += 'PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 1\n  validateReferences: 1\n  platformData:\n  - first:\n      Any: \n    second:\n      enabled: 1\n      settings: {}\n'
        elif path.suffix in ('.txt', '.json'):
            value += 'TextScriptImporter:\n  externalObjects: {}\n'
        else:
            value += 'DefaultImporter:\n  externalObjects: {}\n'
        meta.write_text(value, encoding='utf-8')
        created += 1
print(f'Created {created} metadata files; existing GUIDs preserved.')
