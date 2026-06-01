"""
Package the FastAPI service into a single sidecar binary with PyInstaller.

The output is named with the Rust target triple so Tauri's `externalBin` picks
it up (e.g. `pogo-service-x86_64-pc-windows-msvc.exe`). Run from this dir:

    pip install -r requirements.txt pyinstaller
    python build_sidecar.py
"""

import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
OUT = HERE.parent / "src-tauri" / "binaries"


def target_triple() -> str:
    out = subprocess.check_output(["rustc", "-vV"], text=True)
    for line in out.splitlines():
        if line.startswith("host:"):
            return line.split(":", 1)[1].strip()
    raise SystemExit("could not determine rust host triple (is rustc installed?)")


def main() -> None:
    triple = target_triple()
    name = "pogo-service"
    subprocess.check_call(
        [
            sys.executable, "-m", "PyInstaller",
            "--onefile", "--name", name,
            # pymobiledevice3 pulls in plenty of optional submodules.
            "--collect-all", "pymobiledevice3",
            "--hidden-import", "uvicorn.logging",
            str(HERE / "__main__.py"),
        ]
    )

    OUT.mkdir(parents=True, exist_ok=True)
    ext = ".exe" if sys.platform == "win32" else ""
    src = HERE / "dist" / f"{name}{ext}"
    dst = OUT / f"{name}-{triple}{ext}"
    shutil.copy2(src, dst)
    print(f"-> {dst}")


if __name__ == "__main__":
    main()
