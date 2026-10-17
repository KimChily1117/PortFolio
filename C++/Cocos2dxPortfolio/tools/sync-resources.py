"""Incremental local build copy; converter scratch files are never runtime assets."""
from pathlib import Path
import argparse
import shutil
p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('destination',type=Path);a=p.parse_args()
copied=0
for src in a.source.rglob('*'):
    if not src.is_file() or any(mark in src.name for mark in ('.tmp','.part','.pending')):
        continue
    dst=a.destination/src.relative_to(a.source)
    st=src.stat()
    if dst.exists() and dst.stat().st_size==st.st_size and dst.stat().st_mtime_ns==st.st_mtime_ns:
        continue
    dst.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(src,dst);copied+=1
print(f'Resources updated: {copied} files')
