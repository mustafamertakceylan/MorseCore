"""Install the optional offline speech pack. Network access happens only here."""
import argparse
import hashlib
import json
import shutil
import subprocess
import sys
from pathlib import Path

VOICES = {
    "ema-tr": ("ema", "ema"), "antalia-tr": ("antalia", "antalia"),
    "kareem-ar": ("piper", "ar_JO-kareem-medium"),
    "thorsten-de": ("piper", "de_DE-thorsten-high"),
    "kerstin-de": ("piper", "de_DE-kerstin-low"),
    "dmitri-ru": ("piper", "ru_RU-dmitri-medium"),
    "denis-ru": ("piper", "ru_RU-denis-medium"),
    "joe-en": ("piper", "en_US-joe-medium"),
}
PACKAGES = {"ema": "ema-lightning==1.1.2", "antalia": "antalia-mini==1.0.1", "piper": "piper-tts==1.3.0"}


def model_files(catalog, voice):
    if voice is None:
        return catalog
    engine, model = VOICES[voice]
    if engine == "piper":
        return [f for f in catalog if f["path"] in (
            f"models/{model}.onnx", f"models/{model}.onnx.json", f"licenses/{model}.MODEL_CARD")]
    return [f for f in catalog if f["path"].startswith(f"models/{model}/")]


def digest(path, algorithm):
    value = hashlib.new(algorithm)
    if algorithm == "sha1":
        value.update(f"blob {path.stat().st_size}\0".encode())
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            value.update(chunk)
    return value.hexdigest()


def ensure_environment(home):
    """Repair an interrupted venv bootstrap without clearing it or any models."""
    environment = home / "venv"
    python = environment / ("Scripts/python.exe" if sys.platform == "win32" else "bin/python")

    def succeeds(arguments):
        try:
            return subprocess.run(arguments, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0
        except OSError:
            return False

    # A leftover launcher is not proof of a working venv: ensurepip may have
    # been cancelled after python.exe was copied but before pip was installed.
    probe = "import sys; from pathlib import Path; sys.exit(0 if sys.prefix != sys.base_prefix and Path(sys.prefix).resolve() == Path(sys.argv[1]).resolve() else 1)"
    if not python.is_file() or not succeeds([str(python), "-I", "-c", probe, str(environment)]):
        base = getattr(sys, "_base_executable", None) or sys.executable
        if not Path(base).is_file():
            raise FileNotFoundError("A valid base Python interpreter is required to repair the speech environment")
        # venv without --clear preserves installed packages and unrelated files.
        # Bootstrap pip separately so a retry can identify and repair that step.
        subprocess.run([base, "-m", "venv", "--without-pip", str(environment)], check=True)
        if not python.is_file() or not succeeds([str(python), "-I", "-c", probe, str(environment)]):
            raise RuntimeError("Speech Python environment could not be repaired")
    pip_probe = [str(python), "-I", "-m", "pip", "--version"]
    if not succeeds(pip_probe):
        # ensurepip uses Python's bundled wheels, with no network access.
        subprocess.run([str(python), "-I", "-m", "ensurepip", "--upgrade", "--default-pip"], check=True)
        if not succeeds(pip_probe):
            raise RuntimeError("Speech pip bootstrap could not be repaired; retry installation")
    return python


def install(home, skip_packages=False, voice=None, packages_only=False):
    if not (3, 11) <= sys.version_info[:2] <= (3, 13):
        raise SystemExit("Python 3.11-3.13 is required for this speech pack")
    source = Path(__file__).resolve().parent
    home = home.resolve()
    home.mkdir(parents=True, exist_ok=True)
    python = ensure_environment(home)
    engines = [VOICES[voice][0]] if voice else list(PACKAGES)
    if not skip_packages:
        specifications = [PACKAGES[engine] for engine in engines]
        if any(engine in ("ema", "antalia") for engine in engines):
            specifications.insert(0, "torch==2.10.0")
        # Check inside the selected venv, not the interpreter that launched setup.
        probe = "import importlib.metadata as m,sys; specs=sys.argv[1:]; sys.exit(0 if all(m.version(s.split('==')[0]).split('+')[0]==s.split('==')[1] for s in specs) else 1)"
        present = subprocess.run([str(python), "-I", "-c", probe, *specifications], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0
        if not present:
            print(json.dumps({"phase": "dependencies"}), flush=True)
            pip = [str(python), "-m", "pip", "--disable-pip-version-check", "--no-input", "install"]
            if "torch==2.10.0" in specifications:
                subprocess.run([*pip, "--index-url", "https://download.pytorch.org/whl/cpu", "torch==2.10.0"], check=True)
            subprocess.run([*pip, *[s for s in specifications if not s.startswith("torch==")]], check=True)
    if not python.is_file():
        raise FileNotFoundError("Speech Python environment is missing")
    catalog = json.loads((source / "speech-models.json").read_text(encoding="utf-8"))
    for file in [] if packages_only else model_files(catalog, voice):
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
    license_source = source.parent / "third_party" / "speech"
    if license_source.is_dir():
        shutil.copytree(license_source, home / "licenses" / "speech", dirs_exist_ok=True)
    with (home / "installed-packages.txt").open("w", encoding="utf-8") as log:
        subprocess.run([str(python), "-m", "pip", "freeze"], stdout=log, check=True)
    if not packages_only:
        (home / "pack-ready.json").write_text(json.dumps({"schema": 1, "voices": 8 if voice is None else 1, "offline": True}) + "\n", encoding="utf-8")
    print("Offline speech pack ready:", home)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--home", type=Path, default=Path(__file__).resolve().parents[1] / "speech-runtime")
    parser.add_argument("--skip-packages", action="store_true", help="Use an existing speech virtual environment")
    parser.add_argument("--voice", choices=tuple(VOICES), help="Install one voice instead of the whole catalog")
    parser.add_argument("--packages-only", action="store_true", help="Install engine dependencies; the desktop manages verified model downloads")
    arguments = parser.parse_args()
    install(arguments.home, arguments.skip_packages, arguments.voice, arguments.packages_only)
