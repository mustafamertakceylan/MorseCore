"""Inspect/extract only MorseCore archives, then probe the relocated native ABI."""
import argparse
import ctypes
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import tarfile


def extract(archive, destination):
    match = re.fullmatch(r"MorseCore-(\d+\.\d+\.\d+)-Linux-x64\.tar\.gz", archive.name)
    if not match:
        raise ValueError("Unexpected Linux archive filename")
    root_name = archive.name.removesuffix(".tar.gz")
    destination.mkdir(parents=True, exist_ok=False)
    with tarfile.open(archive, "r:gz") as handle:
        members = handle.getmembers()
        names = set()
        total = 0
        for member in members:
            path = PurePosixPath(member.name)
            if (path.is_absolute() or ".." in path.parts or "\\" in member.name or
                    not path.parts or path.parts[0] != root_name or not (member.isdir() or member.isfile())):
                raise ValueError(f"Unsafe archive entry: {member.name}")
            normalized = str(path)
            if normalized in names:
                raise ValueError(f"Duplicate archive entry: {member.name}")
            names.add(normalized)
            total += member.size
            if total > 4 * 1024**3:
                raise ValueError("Uncompressed archive exceeds 4 GiB test limit")
        # Reject links/special files before writing; do not use tar's path handling.
        for member in members:
            target = destination.joinpath(*PurePosixPath(member.name).parts)
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with handle.extractfile(member) as source, target.open("xb") as sink:
                    while chunk := source.read(1024 * 1024):
                        sink.write(chunk)
                target.chmod(member.mode & 0o777)
    root = destination / root_name
    info = json.loads((root / "BUILD-INFO.json").read_text())
    if info["version"] != match[1] or info["runtime"] != "linux-x64":
        raise ValueError("Package version/runtime metadata mismatch")
    for name in ("MorseCore.Desktop", "libMorseCore.Native.so", "libcoreclr.so", "libhostfxr.so",
                 "morsecore", "native-dependencies.sh", "morsecore.desktop", "morsecore.svg", "LICENSE", "NOTICE", "licenses/dependencies.json"):
        if not (root / name).is_file() or not (root / name).stat().st_size:
            raise ValueError(f"Missing package component: {name}")
    return root, match[1]


def probe_native(root, expected_version):
    native = ctypes.CDLL(str(root / "libMorseCore.Native.so"))
    native.mc_abi_version.restype = ctypes.c_uint32
    native.mc_version.restype = ctypes.c_char_p
    if native.mc_abi_version() != 1 or native.mc_version().decode() != expected_version:
        raise ValueError("Relocated native ABI/version mismatch")
    native.mc_encode.argtypes = (ctypes.c_char_p, ctypes.c_uint64, ctypes.c_uint32, ctypes.POINTER(ctypes.c_void_p))
    native.mc_encode.restype = ctypes.c_int32
    native.mc_result_copy_text.argtypes = (ctypes.c_void_p, ctypes.c_char_p, ctypes.c_uint64, ctypes.POINTER(ctypes.c_uint64))
    native.mc_result_copy_text.restype = ctypes.c_int32
    native.mc_result_destroy.argtypes = (ctypes.c_void_p,)
    native.mc_result_destroy.restype = None
    result = ctypes.c_void_p()
    if native.mc_encode(b"SOS", 3, 0, ctypes.byref(result)) != 0:
        raise ValueError("Relocated native encode failed")
    try:
        buffer = ctypes.create_string_buffer(64)
        required = ctypes.c_uint64()
        if native.mc_result_copy_text(result, buffer, len(buffer), ctypes.byref(required)) != 0 or buffer.value.strip() != b"... --- ...":
            raise ValueError("Relocated native SOS conversion mismatch")
    finally:
        native.mc_result_destroy(result)


def check_checksum(archive):
    manifest = archive.parent / "SHA256SUMS-Linux-x64.txt"
    lines = manifest.read_text().splitlines()
    expected = [line.split()[0] for line in lines if len(line.split()) == 2 and line.split()[1] == archive.name]
    with archive.open("rb") as source:
        actual = hashlib.file_digest(source, "sha256").hexdigest()
    if len(expected) != 1 or not re.fullmatch(r"[0-9a-f]{64}", expected[0]) or expected[0] != actual:
        raise ValueError("Package SHA-256 does not match Linux checksum manifest")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, nargs="?")
    parser.add_argument("destination", type=Path, nargs="?")
    parser.add_argument("--probe-publish", nargs=2, metavar=("DIRECTORY", "VERSION"))
    parser.add_argument("--check-file", type=Path)
    args = parser.parse_args()
    if args.check_file:
        check_checksum(args.check_file.resolve())
    elif args.probe_publish:
        probe_native(Path(args.probe_publish[0]).resolve(), args.probe_publish[1])
    else:
        if not args.archive or not args.destination:
            parser.error("archive and destination are required")
        check_checksum(args.archive.resolve())
        root, package_version = extract(args.archive.resolve(), args.destination.resolve())
        probe_native(root, package_version)
        print(root)
