"""Detached HTTPS publisher with logs separate from the HTTP authoring process."""
from pathlib import Path
import runpy
import sys

root = Path(__file__).resolve().parents[1]
logs = root/'.local'/'tls-process'
logs.mkdir(parents=True, exist_ok=True)
with (logs/'server.log').open('a', encoding='utf-8', buffering=1) as output:
    # Detached WMI processes may have invalid inherited standard descriptors.
    # Replace the Python streams directly, without depending on those handles.
    sys.stdout = output
    sys.stderr = output
    sys.argv[0] = str(root/'server.py')
    runpy.run_path(str(root/'server.py'), run_name='__main__')
