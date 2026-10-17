"""Build Linux archives from an existing, untrimmed self-contained publish."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[2]
PACKAGING = ROOT / "packaging/linux"
APP_ID = "io.github.mustafamertakceylan.morsecore"


def version():
    match = re.search(r"^project\(MorseCore VERSION (\d+\.\d+\.\d+)\b", (ROOT / "CMakeLists.txt").read_text(), re.M)
    if not match:
        raise ValueError("CMake project version is missing")
    return match[1]


def sha256(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def validate_publish(publish, expected_version):
    required = ("MorseCore.Desktop", "MorseCore.Desktop.dll", "MorseCore.Desktop.deps.json",
                "MorseCore.Desktop.runtimeconfig.json", "libMorseCore.Native.so", "libhostfxr.so",
                "libhostpolicy.so", "libcoreclr.so", "libSkiaSharp.so")
    for name in required:
        if not (publish / name).is_file():
            raise ValueError(f"Incomplete self-contained publish: {name}")
    config = json.loads((publish / "MorseCore.Desktop.runtimeconfig.json").read_text())["runtimeOptions"]
    if config.get("framework") or config.get("frameworks") or not config.get("includedFrameworks"):
        raise ValueError("Publish is framework-dependent; a self-contained runtime is required")
    if not any(entry["name"] == "Microsoft.NETCore.App" and entry["version"].startswith("10.")
               for entry in config["includedFrameworks"]):
        raise ValueError("The package requires the .NET 10 runtime")
    deps = json.loads((publish / "MorseCore.Desktop.deps.json").read_text())
    if not deps.get("runtimeTarget", {}).get("name", "").endswith("/linux-x64"):
        raise ValueError("Unexpected publish runtime: linux-x64 is required")
    app_versions = [name.split("/", 1)[1] for name in deps["libraries"] if name.startswith("MorseCore.Desktop/")]
    if app_versions != [expected_version]:
        raise ValueError(f"Published desktop version differs from CMake version {expected_version}")
    for entry in publish.rglob("*"):
        if entry.is_symlink():
            raise ValueError(f"Publish symlinks are not allowed: {entry.relative_to(publish)}")
        if entry.is_file() and (entry.name.startswith(".env") or entry.suffix.lower() in (".pfx", ".p12", ".key")):
            raise ValueError(f"Unexpected private configuration/key in publish: {entry.name}")


def validate_elf(publish, maximum, maximum_glibcxx="3.4.30", maximum_cxxabi="1.3.13"):
    highest = (0, 0)
    highest_glibcxx = (0, 0, 0)
    highest_cxxabi = (0, 0, 0)
    total = 0
    for entry in sorted(publish.rglob("*")):
        if not entry.is_file():
            continue
        with entry.open("rb") as source:
            header = source.read(20)
        if not header.startswith(b"\x7fELF"):
            continue
        # ELFCLASS64, little endian, EM_X86_64. Do not execute inspected files.
        if len(header) < 20 or header[4:6] != b"\x02\x01" or int.from_bytes(header[18:20], "little") != 62:
            raise ValueError(f"Unexpected ELF architecture: {entry.relative_to(publish)}")
        result = subprocess.run(["readelf", "--version-info", "--wide", str(entry)], check=True,
                                capture_output=True, text=True)
        versions = [tuple(map(int, match)) for match in re.findall(r"\bGLIBC_(\d+)\.(\d+)\b", result.stdout)]
        if versions:
            highest = max(highest, *versions)
        for symbol, current in (("GLIBCXX", highest_glibcxx), ("CXXABI", highest_cxxabi)):
            required = [tuple(map(int, match)) for match in re.findall(r"\b" + symbol + r"_(\d+)\.(\d+)\.(\d+)\b", result.stdout)]
            if required:
                if symbol == "GLIBCXX":
                    highest_glibcxx = max(current, *required)
                else:
                    highest_cxxabi = max(current, *required)
        total += 1
    if not total:
        raise ValueError("No Linux ELF binaries found")
    if highest > tuple(map(int, maximum.split("."))):
        raise ValueError(f"GLIBC {highest[0]}.{highest[1]} exceeds compatibility baseline {maximum}; rebuild on an older base")
    for label, required, baseline in (("GLIBCXX", highest_glibcxx, maximum_glibcxx), ("CXXABI", highest_cxxabi, maximum_cxxabi)):
        if required > tuple(map(int, baseline.split("."))):
            raise ValueError(f"{label} {'.'.join(map(str, required))} exceeds compatibility baseline {baseline}; rebuild on an older base")
    return {"elf_files": total, "highest_glibc_symbol": ".".join(map(str, highest)), "baseline_glibc": maximum,
            "highest_glibcxx_symbol": ".".join(map(str, highest_glibcxx)), "baseline_glibcxx": maximum_glibcxx,
            "highest_cxxabi_symbol": ".".join(map(str, highest_cxxabi)), "baseline_cxxabi": maximum_cxxabi}


def copy_payload(publish, stage):
    shutil.copytree(publish, stage, ignore=shutil.ignore_patterns("*.pdb", "*.dbg"))
    for name in ("LICENSE", "NOTICE"):
        shutil.copy2(ROOT / name, stage / name)
    shutil.copy2(PACKAGING / "morsecore", stage / "morsecore")
    shutil.copy2(PACKAGING / "native-dependencies.sh", stage / "native-dependencies.sh")
    shutil.copy2(PACKAGING / "morsecore.desktop", stage / "morsecore.desktop")
    # Keep the exact shared brand asset; never redraw a platform-specific logo.
    shutil.copy2(ROOT / "apps/MorseCore.Desktop/MorseCore.Desktop/Assets/Icons/icon.svg", stage / "morsecore.svg")
    for name in ("linux.md", "linux.tr.md", "offline-speech.md", "offline-speech.tr.md"):
        (stage / "docs").mkdir(exist_ok=True)
        shutil.copy2(ROOT / "docs" / name, stage / "docs" / name)
    for font in (ROOT / "apps/MorseCore.Desktop/MorseCore.Desktop/Assets/Fonts").glob("*OFL.txt"):
        (stage / "licenses/fonts").mkdir(parents=True, exist_ok=True)
        shutil.copy2(font, stage / "licenses/fonts" / font.name)
    shutil.copytree(ROOT / "third_party/speech", stage / "third_party/speech", dirs_exist_ok=True)
    normalize_permissions(stage)


def require_posix_staging(work):
    """DrvFs may report chmod success without applying Unix mode bits."""
    probe = work / ".permissions-probe"
    try:
        work.chmod(0o700)
        probe.write_bytes(b"permission probe")
        probe.chmod(0o600)
        if work.stat().st_mode & 0o777 != 0o700 or probe.stat().st_mode & 0o777 != 0o600:
            raise ValueError("Linux staging needs POSIX permissions; set TMPDIR=/tmp (not /mnt/c or another DrvFs path)")
    except OSError as error:
        raise ValueError("Linux staging needs POSIX permissions; set TMPDIR=/tmp (not /mnt/c or another DrvFs path)") from error
    finally:
        probe.unlink(missing_ok=True)


def normalize_permissions(root):
    """Ignore source checkout permissions, including DrvFs's all-0777 modes."""
    root.chmod(0o755)
    for file in root.rglob("*"):
        if file.is_symlink():
            continue
        if file.is_dir():
            file.chmod(0o755)
        elif file.is_file():
            with file.open("rb") as source:
                elf = source.read(4) == b"\x7fELF"
            executable = elf or file.name in ("MorseCore.Desktop", "morsecore", "createdump") or file.suffix == ".sh"
            file.chmod(0o755 if executable else 0o644)


def create_deb(stage, work, output, package_version, dependencies):
    deb = work / "deb"
    target = deb / "usr/lib/morsecore"
    target.parent.mkdir(parents=True)
    shutil.copytree(stage, target)
    (deb / "usr/bin").mkdir(parents=True)
    (deb / "usr/bin/morsecore").symlink_to("/usr/lib/morsecore/morsecore")
    for source, relative in (("morsecore.desktop", f"usr/share/applications/{APP_ID}.desktop"),
                             ("morsecore.svg", f"usr/share/icons/hicolor/scalable/apps/{APP_ID}.svg"),
                             ("LICENSE", "usr/share/doc/morsecore/LICENSE"), ("NOTICE", "usr/share/doc/morsecore/NOTICE")):
        destination = deb / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(stage / source, destination)
    control = deb / "DEBIAN"
    control.mkdir()
    control.chmod(0o755)
    size = sum(path.stat().st_size for path in deb.rglob("*") if path.is_file() and not path.is_symlink())
    (control / "control").write_text(
        f"Package: morsecore\nVersion: {package_version}\nArchitecture: amd64\nSection: hamradio\nPriority: optional\n"
        "Maintainer: MorseCore contributors <noreply@github.com>\n"
        f"Installed-Size: {(size + 1023) // 1024}\nDepends: {', '.join(dependencies['debian'])}\n"
        f"Suggests: {', '.join(dependencies['debian_optional'])}\n"
        "Homepage: https://github.com/mustafamertakceylan/MorseCore\n"
        "Description: Desktop Morse toolkit with offline speech\n"
        " Shared C23 conversion engine, audio tools, learning and amateur radio logs.\n"
        " The .NET runtime is included. Speech models are optional separate downloads.\n", encoding="utf-8")
    (control / "control").chmod(0o644)
    normalize_permissions(deb)
    subprocess.run(["dpkg-deb", "--build", "--root-owner-group", str(deb), str(output)], check=True)


def build(publish, output, packages, deb=False):
    if output == publish or publish in output.parents:
        raise ValueError("Output must be outside the publish directory")
    if not publish.is_dir() or not packages.is_dir():
        raise ValueError("Existing publish directory and NuGet cache are required")
    package_version = version()
    dependencies = json.loads((PACKAGING / "dependencies.json").read_text())
    validate_publish(publish, package_version)
    elf = validate_elf(publish, dependencies["glibc_maximum_required"], dependencies["glibcxx_maximum_required"],
                       dependencies["cxxabi_maximum_required"])
    subprocess.run([sys.executable, str(PACKAGING / "verify-package.py"), "--probe-publish",
                    str(publish), package_version], check=True)
    if deb and shutil.which("dpkg-deb") is None:
        raise ValueError("dpkg-deb is required for --deb")
    basename = f"MorseCore-{package_version}-Linux-x64"
    names = [basename + ".tar.gz", "PKGBUILD", "SHA256SUMS-Linux-x64.txt"]
    if deb:
        names.append(basename + ".deb")
    output.mkdir(parents=True, exist_ok=True)
    if any((output / name).exists() for name in names):
        raise ValueError("Refusing to overwrite an existing archive, recipe or checksum; choose a new output directory")
    # Output may live on WSL DrvFs; staging must use /tmp or another POSIX fs.
    with tempfile.TemporaryDirectory(prefix="morsecore-linux-stage-") as temporary:
        work = Path(temporary)
        require_posix_staging(work)
        stage = work / basename
        copy_payload(publish, stage)
        subprocess.run([sys.executable, str(ROOT / "scripts/collect-desktop-licenses.py"), str(stage), str(packages)], check=True)
        commit = subprocess.run(["git", "-C", str(ROOT), "rev-parse", "HEAD"], check=True, capture_output=True, text=True).stdout.strip()
        dirty = bool(subprocess.run(["git", "-C", str(ROOT), "status", "--porcelain"], check=True, capture_output=True, text=True).stdout)
        (stage / "BUILD-INFO.json").write_text(json.dumps({"version": package_version, "runtime": "linux-x64",
            "source_commit": commit, "source_tree_dirty": dirty, "compatibility": elf,
            "desktop_smoke_tested": False}, indent=2) + "\n", encoding="utf-8")
        # Include files subsequently added by license collection and metadata.
        normalize_permissions(stage)
        archive = work / names[0]
        with tarfile.open(archive, "w:gz", compresslevel=9) as handle:
            def normalize(info):
                info.uid = info.gid = 0
                info.uname = info.gname = "root"
                return info
            handle.add(stage, arcname=basename, filter=normalize)
        recipe = (PACKAGING / "PKGBUILD.in").read_text().replace("@VERSION@", package_version)
        recipe = recipe.replace("@DEPENDS@", " ".join(f"'{name}'" for name in dependencies["arch"]))
        recipe = recipe.replace("@OPTIONAL@", " ".join(f"'{name}'" for name in dependencies["arch_optional"]))
        recipe = recipe.replace("@SHA256@", sha256(archive))
        (work / "PKGBUILD").write_text(recipe, encoding="utf-8")
        archives = [archive]
        if deb:
            deb_path = work / (basename + ".deb")
            create_deb(stage, work, deb_path, package_version, dependencies)
            archives.append(deb_path)
        (work / "SHA256SUMS-Linux-x64.txt").write_text("".join(f"{sha256(path)}  {path.name}\n" for path in archives), encoding="utf-8")
        for name in names:
            # Refuse races with another packaging operation; never replace its files.
            with (output / name).open("xb") as destination, (work / name).open("rb") as source:
                shutil.copyfileobj(source, destination)
            print(output / name)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--packages", type=Path, required=True)
    parser.add_argument("--deb", action="store_true")
    args = parser.parse_args()
    try:
        build(args.publish.resolve(), args.output.resolve(), args.packages.resolve(), args.deb)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        parser.exit(1, f"Linux packaging failed: {error}\n")
