"""Copy the existing workspace sources without touching the Android baseline."""
import hashlib
import json
import os
from pathlib import Path
import shutil
from datetime import datetime, timezone

SOURCE = Path(r"E:\task\Unity_Project")
DESTINATION = Path(__file__).resolve().parents[1]
PROJECTS = ["KimchilyCreator", "KimchilySDK", "KimchilyUnityRuntime", "KimchilyAndroid", "KimchilyPublish", "docs"]
EXCLUDED = {"library", "temp", "logs", "usersettings", "obj", "bin", "node_modules", ".git", ".vs", ".idea", "__pycache__", ".deps", ".local", ".gradle", ".gradle-user-home", ".android-user-home", ".emulator", "artifacts", "worldbuilds", "builds"}

def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

def copy():
    if DESTINATION == SOURCE or SOURCE in DESTINATION.parents:
        raise RuntimeError("The clone must be outside the original workspace.")
    for project in PROJECTS:
        if (DESTINATION / project).exists():
            raise RuntimeError(f"Refusing to overwrite existing clone project: {project}")
    records = []
    for project in PROJECTS:
        for directory, folders, filenames in os.walk(SOURCE / project):
            current = Path(directory)
            folders[:] = [name for name in folders if name.lower() not in EXCLUDED and not (project == "KimchilyAndroid" and name.lower() == "build") and not (current / name).is_symlink() and not (current / name).is_junction()]
            for name in filenames:
                if name.lower() in {"local.properties", ".ds_store"} or name.endswith((".pyc", ".pidb")):
                    continue
                original = current / name
                if original.is_symlink():
                    raise RuntimeError(f"Unexpected file symlink: {original}")
                relative = original.relative_to(SOURCE)
                target = DESTINATION / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(original, target)
                sha = digest(original)
                if digest(target) != sha:
                    raise RuntimeError(f"Copy changed during cloning: {relative}")
                records.append({"path": relative.as_posix(), "size": target.stat().st_size, "sha256": sha})
    shutil.copy2(SOURCE / "README.md", DESTINATION / "README.md")
    records.append({"path": "README.md", "size": (SOURCE / "README.md").stat().st_size, "sha256": digest(SOURCE / "README.md")})
    evidence = [
        "KimchilyAndroid/Artifacts/kimchily-unity-debug.apk",
        "KimchilySDK/Artifacts/editmode.xml",
        "KimchilyUnityRuntime/Artifacts/runtime-playmode.xml",
        "KimchilyCreator/Artifacts/third-person-animation-20260920/verification.json",
        "KimchilyCreator/Artifacts/third-person-animation-20260920/device-walk.png",
        "KimchilyCreator/Artifacts/third-person-animation-20260920/device-run.png",
        "KimchilyCreator/Artifacts/third-person-animation-20260920/device-jump.png",
        "KimchilyCreator/Artifacts/third-person-animation-20260920/device-camera-orbit.png",
    ]
    evidence_records = []
    for relative in evidence:
        original = SOURCE / relative
        if not original.is_file():
            raise RuntimeError(f"Missing baseline evidence: {relative}")
        target = DESTINATION / "preservation" / "android-2022.3" / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(original, target)
        sha = digest(original)
        if digest(target) != sha:
            raise RuntimeError(f"Evidence copy mismatch: {relative}")
        evidence_records.append({"path": relative, "sha256": sha, "size": original.stat().st_size})
    output = DESTINATION / "preservation" / "source-manifest.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({"createdUtc": datetime.now(timezone.utc).isoformat(), "source": str(SOURCE), "clone": str(DESTINATION), "unityVersion": "2022.3.16f1", "excludedDirectoryNames": sorted(EXCLUDED), "files": records, "evidence": evidence_records}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"source": str(SOURCE), "clone": str(DESTINATION), "files": len(records), "sourceBytes": sum(item["size"] for item in records), "evidenceBytes": sum(item["size"] for item in evidence_records)}, ensure_ascii=False))

if __name__ == "__main__":
    copy()
