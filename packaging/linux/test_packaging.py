"""Package contract/safety tests; no installation, user data or model downloads."""
import importlib.util
import io
import json
import os
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch


def load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


package = load("linux_package", "build-package.py")
verify = load("linux_verify", "verify-package.py")


class PackageContract(unittest.TestCase):
    def setUp(self):
        base = package.ROOT / "cmake-build-agent-work/linux-packaging"
        base.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix="contract-", dir=base)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.publish = self.root / "publish"
        self.publish.mkdir()
        for name in ("MorseCore.Desktop", "MorseCore.Desktop.dll", "libMorseCore.Native.so", "libhostfxr.so",
                     "libhostpolicy.so", "libcoreclr.so", "libSkiaSharp.so"):
            (self.publish / name).write_bytes(b"fixture")
        self.config = {"runtimeOptions": {"includedFrameworks": [{"name": "Microsoft.NETCore.App", "version": "10.0.0"}]}}
        self.deps = {"runtimeTarget": {"name": ".NETCoreApp,Version=v10.0/linux-x64"},
                     "libraries": {"MorseCore.Desktop/" + package.version(): {"type": "project"}}}
        self.write_metadata()

    def write_metadata(self):
        (self.publish / "MorseCore.Desktop.runtimeconfig.json").write_text(json.dumps(self.config))
        (self.publish / "MorseCore.Desktop.deps.json").write_text(json.dumps(self.deps))

    def test_valid_self_contained_contract(self):
        package.validate_publish(self.publish, package.version())

    def test_native_missing_rejected(self):
        (self.publish / "libMorseCore.Native.so").unlink()
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            package.validate_publish(self.publish, package.version())

    def test_runtime_missing_rejected(self):
        (self.publish / "libcoreclr.so").unlink()
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            package.validate_publish(self.publish, package.version())

    def test_wrong_version_rejected(self):
        with self.assertRaisesRegex(ValueError, "version differs"):
            package.validate_publish(self.publish, "9.9.9")

    def test_output_inside_publish_rejected(self):
        for output in (self.publish, self.publish / "output"):
            with self.assertRaisesRegex(ValueError, "outside the publish"):
                package.build(self.publish, output, self.root)

    @unittest.skipIf(os.name == "nt", "Unix mode bits require a real POSIX filesystem")
    def test_permissive_source_modes_are_normalized(self):
        # Use the POSIX temp fs even when the checkout/test root is WSL DrvFs.
        with tempfile.TemporaryDirectory(prefix="morsecore-permission-test-") as temporary:
            root = Path(temporary)
            (root / "nested").mkdir()
            for name, content in (("MorseCore.Desktop", b"app"), ("createdump", b"dump"),
                                  ("morsecore", b"launcher"), ("helper.sh", b"script"),
                                  ("nested/libnative.so", b"\x7fELFfixture"),
                                  ("nested/config.json", b"{}"), ("LICENSE", b"license")):
                (root / name).write_bytes(content)
            for file in [root, *root.rglob("*")]:
                file.chmod(0o777)
            package.normalize_permissions(root)
            for file in [root, *root.rglob("*")]:
                expected = 0o644 if file.name in ("config.json", "LICENSE") else 0o755
                self.assertEqual(file.stat().st_mode & 0o777, expected, str(file))

    @unittest.skipIf(os.name == "nt", "Unix mode bits require a real POSIX filesystem")
    def test_posix_temp_staging_is_verified(self):
        with tempfile.TemporaryDirectory(prefix="morsecore-staging-test-") as temporary:
            root = Path(temporary)
            package.require_posix_staging(root)
            self.assertEqual(root.stat().st_mode & 0o777, 0o700)
            self.assertFalse((root / ".permissions-probe").exists())

    @unittest.skipIf(os.name == "nt", "Unix modes and package symlinks require a POSIX filesystem")
    def test_deb_control_and_payload_modes_are_normalized(self):
        with tempfile.TemporaryDirectory(prefix="morsecore-deb-mode-test-") as temporary:
            root = Path(temporary)
            stage = root / "payload"
            stage.mkdir()
            for name in ("LICENSE", "NOTICE", "morsecore.desktop", "morsecore.svg", "config.json"):
                (stage / name).write_text("synthetic package fixture")
                (stage / name).chmod(0o777)
            (stage / "libnative.so").write_bytes(b"\x7fELFfixture")
            (stage / "libnative.so").chmod(0o777)
            stage.chmod(0o777)
            dependencies = json.loads((package.PACKAGING / "dependencies.json").read_text())
            with patch.object(package.subprocess, "run"):
                package.create_deb(stage, root, root / "fixture.deb", package.version(), dependencies)
            self.assertEqual((root / "deb/DEBIAN").stat().st_mode & 0o777, 0o755)
            self.assertEqual((root / "deb/DEBIAN/control").stat().st_mode & 0o777, 0o644)
            self.assertEqual((root / "deb/usr/lib/morsecore/config.json").stat().st_mode & 0o777, 0o644)
            self.assertEqual((root / "deb/usr/lib/morsecore/libnative.so").stat().st_mode & 0o777, 0o755)
            self.assertTrue((root / "deb/usr/bin/morsecore").is_symlink())

    def test_framework_dependent_rejected(self):
        self.config["runtimeOptions"]["framework"] = {"name": "Microsoft.NETCore.App", "version": "10.0.0"}
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, "framework-dependent"):
            package.validate_publish(self.publish, package.version())

    def test_wrong_dotnet_rejected(self):
        self.config["runtimeOptions"]["includedFrameworks"][0]["version"] = "9.0.0"
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, "NET 10"):
            package.validate_publish(self.publish, package.version())

    def test_wrong_runtime_rejected(self):
        self.deps["runtimeTarget"]["name"] = ".NETCoreApp,Version=v10.0/win-x64"
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, "linux-x64"):
            package.validate_publish(self.publish, package.version())

    def test_private_key_rejected(self):
        (self.publish / "private.pfx").write_bytes(b"synthetic-not-a-key")
        with self.assertRaisesRegex(ValueError, "private configuration/key"):
            package.validate_publish(self.publish, package.version())

    def test_environment_file_rejected(self):
        (self.publish / ".env.local").write_text("SYNTHETIC=true")
        with self.assertRaisesRegex(ValueError, "private configuration/key"):
            package.validate_publish(self.publish, package.version())

    @unittest.skipIf(os.name == "nt", "Windows symlink creation may require privileges; Linux CI exercises this")
    def test_symlink_rejected(self):
        (self.publish / "outside").symlink_to(self.root)
        with self.assertRaisesRegex(ValueError, "symlinks"):
            package.validate_publish(self.publish, package.version())

    def elf(self, machine=62):
        header = bytearray(64)
        header[:6] = b"\x7fELF\x02\x01"
        header[18:20] = machine.to_bytes(2, "little")
        (self.publish / "libMorseCore.Native.so").write_bytes(header)

    def test_wrong_elf_arch_rejected(self):
        self.elf(183)  # EM_AARCH64
        with self.assertRaisesRegex(ValueError, "architecture"):
            package.validate_elf(self.publish, "2.36")

    def test_no_elf_rejected(self):
        with self.assertRaisesRegex(ValueError, "No Linux ELF"):
            package.validate_elf(self.publish, "2.36")

    def test_glibc_baseline_passes(self):
        self.elf()
        with patch.object(package.subprocess, "run", return_value=type("Result", (), {"stdout": "GLIBC_2.2.5 GLIBC_2.36"})()):
            self.assertEqual(package.validate_elf(self.publish, "2.36")["highest_glibc_symbol"], "2.36")

    def test_newer_glibc_rejected(self):
        self.elf()
        with patch.object(package.subprocess, "run", return_value=type("Result", (), {"stdout": "GLIBC_2.38"})()):
            with self.assertRaisesRegex(ValueError, "exceeds compatibility"):
                package.validate_elf(self.publish, "2.36")

    def test_newer_glibcxx_rejected(self):
        self.elf()
        with patch.object(package.subprocess, "run", return_value=type("Result", (), {"stdout": "GLIBC_2.34 GLIBCXX_3.4.31"})()):
            with self.assertRaisesRegex(ValueError, "GLIBCXX.*exceeds compatibility"):
                package.validate_elf(self.publish, "2.36")

    def test_newer_cxxabi_rejected(self):
        self.elf()
        with patch.object(package.subprocess, "run", return_value=type("Result", (), {"stdout": "GLIBC_2.34 CXXABI_1.3.14"})()):
            with self.assertRaisesRegex(ValueError, "CXXABI.*exceeds compatibility"):
                package.validate_elf(self.publish, "2.36")

    def test_checksum_passes_and_tamper_rejected(self):
        archive = self.root / "MorseCore-0.7.5-Linux-x64.tar.gz"
        archive.write_bytes(b"synthetic archive content")
        (self.root / "SHA256SUMS-Linux-x64.txt").write_text(package.sha256(archive) + "  " + archive.name + "\n")
        verify.check_checksum(archive)
        archive.write_bytes(b"tampered content")
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            verify.check_checksum(archive)

    def test_checksum_duplicate_rejected(self):
        archive = self.root / "MorseCore-0.7.5-Linux-x64.tar.gz"
        archive.write_bytes(b"synthetic archive content")
        (self.root / "SHA256SUMS-Linux-x64.txt").write_text((package.sha256(archive) + "  " + archive.name + "\n") * 2)
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            verify.check_checksum(archive)

    def unsafe_archive(self, name, member_type=tarfile.REGTYPE):
        archive = self.root / "MorseCore-0.7.5-Linux-x64.tar.gz"
        with tarfile.open(archive, "w:gz") as handle:
            info = tarfile.TarInfo(name)
            info.type = member_type
            info.linkname = "/tmp/synthetic-target" if member_type == tarfile.SYMTYPE else ""
            handle.addfile(info, io.BytesIO())
        with self.assertRaises(ValueError):
            verify.extract(archive, self.root / "extracted")

    def test_archive_traversal_rejected(self):
        self.unsafe_archive("MorseCore-0.7.5-Linux-x64/../../escape")
        self.assertFalse((self.root / "escape").exists())

    def test_archive_absolute_path_rejected(self):
        self.unsafe_archive("/tmp/escape")

    def test_archive_symlink_rejected(self):
        self.unsafe_archive("MorseCore-0.7.5-Linux-x64/link", tarfile.SYMTYPE)

    def test_archive_hardlink_rejected(self):
        self.unsafe_archive("MorseCore-0.7.5-Linux-x64/link", tarfile.LNKTYPE)

    def test_archive_wrong_root_rejected(self):
        self.unsafe_archive("SomethingElse/file")

    def test_archive_device_rejected(self):
        self.unsafe_archive("MorseCore-0.7.5-Linux-x64/device", tarfile.CHRTYPE)

    def test_archive_duplicate_rejected(self):
        archive = self.root / "MorseCore-0.7.5-Linux-x64.tar.gz"
        with tarfile.open(archive, "w:gz") as handle:
            for _ in range(2):
                handle.addfile(tarfile.TarInfo("MorseCore-0.7.5-Linux-x64/duplicate"), io.BytesIO())
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            verify.extract(archive, self.root / "extracted")

    def test_archive_unknown_filename_rejected(self):
        with self.assertRaisesRegex(ValueError, "filename"):
            verify.extract(self.root / "unexpected.tar.gz", self.root / "extracted")


if __name__ == "__main__":
    unittest.main(verbosity=2)
