"""Install the optional offline speech pack. Network access happens only here."""
import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path


def digest(path, algorithm):
    value = hashlib.new(algorithm)
    if algorithm == "sha1":
        value.update(f"blob {path.stat().st_size}\0".encode())
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def install(home, skip_packages=False):
    if not (3, 11) <= sys.version_info[:2] <= (3, 13):
        raise SystemExit("Python 3.11-3.13 is required for this speech pack")
    source = Path(__file__).resolve().parent
    home = home.resolve()
    home.mkdir(parents=True, exist_ok=True)
    python = home / "venv" / ("Scripts/python.exe" if sys.platform == "win32" else "bin/python")
    if not skip_packages:
        subprocess.run([sys.executable, "-m", "venv", str(home / "venv")], check=True)
        subprocess.run([str(python), "-m", "pip", "install", "--index-url", "https://download.pytorch.org/whl/cpu", "torch==2.10.0"], check=True)
        subprocess.run([str(python), "-m", "pip", "install", "ema-lightning==1.1.2", "antalia-mini==1.0.1", "piper-tts==1.3.0"], check=True)
    if not python.is_file():
        raise FileNotFoundError("Speech Python environment is missing")
    for file in json.loads((source / "speech-models.json").read_text(encoding="utf-8")):
        destination = home / file["path"]
        destination.parent.mkdir(parents=True, exist_ok=True)
        algorithm = "sha256" if file["sha256"] else "sha1"
        expected = file["sha256"] or file["git_blob"]
        if destination.exists() and destination.stat().st_size == file["bytes"] and digest(destination, algorithm) == expected:
            continue
        temporary = destination.with_name(destination.name + ".download")
        print("Downloading", file["path"], flush=True)
        try:
            subprocess.run(["curl.exe" if sys.platform == "win32" else "curl", "-sSL", "--fail", "--retry", "3", "--retry-all-errors", "--connect-timeout", "15", "--max-time", "180", "-o", str(temporary), file["url"]], check=True)
            if temporary.stat().st_size != file["bytes"] or digest(temporary, algorithm) != expected:
                raise ValueError("Model integrity check failed: " + file["path"])
            temporary.replace(destination)
        finally:
            temporary.unlink(missing_ok=True)
    shutil.copy2(source / "speech_worker.py", home / "speech_worker.py")
    shutil.copy2(source / "speech-models.json", home / "speech-models.json")
    shutil.copytree(source.parent / "third_party" / "speech", home / "licenses" / "speech", dirs_exist_ok=True)
    with (home / "installed-packages.txt").open("w", encoding="utf-8") as log:
        subprocess.run([str(python), "-m", "pip", "freeze"], stdout=log, check=True)
    (home / "pack-ready.json").write_text(json.dumps({"schema": 1, "voices": 8, "offline": True}) + "\n", encoding="utf-8")
    print("Offline speech pack ready:", home)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--home", type=Path, default=Path(__file__).resolve().parents[1] / "speech-runtime")
    parser.add_argument("--skip-packages", action="store_true", help="Use an existing speech virtual environment")
    arguments = parser.parse_args()
    install(arguments.home, arguments.skip_packages)
