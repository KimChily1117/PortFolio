"""Detached publisher entry: logs go to the selected data directory, never caller pipes."""
from pathlib import Path
import argparse
import runpy
import sys


def main():
    server = Path(__file__).resolve().parent.parent / "server.py"
    if len(sys.argv) < 2 or Path(sys.argv[1]).resolve() != server:
        raise SystemExit("Expected this project's server.py path.")
    arguments = sys.argv[2:]
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--data-dir", type=Path, default=server.parent / ".local")
    settings, _ = parser.parse_known_args(arguments)
    settings.data_dir.mkdir(parents=True, exist_ok=True)
    with (settings.data_dir / "server.log").open("a", encoding="utf-8", buffering=1) as stdout, \
         (settings.data_dir / "server-error.log").open("a", encoding="utf-8", buffering=1) as stderr:
        sys.stdout, sys.stderr = stdout, stderr
        sys.argv = [str(server), *arguments]
        try:
            runpy.run_path(str(server), run_name="__main__")
        except BaseException:
            # Preserve startup/bind diagnostics even though this process has no caller pipes.
            import traceback
            traceback.print_exc(file=stderr)
            raise SystemExit(1)


if __name__ == "__main__":
    main()
