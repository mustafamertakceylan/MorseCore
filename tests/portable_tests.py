"""Test installs and ZIP relocation without reading or changing personal runtime state."""
from pathlib import Path
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

EXE = Path(sys.argv.pop(1)).resolve()
SOURCE = Path(sys.argv.pop(1)).resolve()
CMAKE = Path(sys.argv.pop(1)).resolve()
BUILD = Path(sys.argv.pop(1)).resolve()
CPACK = CMAKE.with_name("cpack.exe" if os.name == "nt" else "cpack")


def source_state():
    return {str(path): hashlib.sha256(path.read_bytes()).hexdigest()
            for directory in (SOURCE / "data", SOURCE / "config")
            for path in directory.rglob("*") if path.is_file()}


class PortableTests(unittest.TestCase):
    def setUp(self):
        self.original = source_state()
        self.temporary = tempfile.TemporaryDirectory(dir=BUILD, prefix="portable-")
        self.base = Path(self.temporary.name)
        self.cwd = self.base / "unrelated-working-directory"
        self.cwd.mkdir()
        self.environment = dict(os.environ)
        self.environment.pop("MORSECORE_HOME", None)
        # A release must start without GCC, MSYS2, Ninja or their runtime DLLs on PATH.
        if os.name == "nt":
            windows = Path(os.environ.get("SystemRoot", r"C:\Windows"))
            self.environment["PATH"] = os.pathsep.join((str(windows / "System32"), str(windows)))

    def tearDown(self):
        self.assertEqual(source_state(), self.original, "Repository data/settings changed")
        self.temporary.cleanup()

    def execute(self, executable, text="0\n", override=None, launcher=False):
        environment = dict(self.environment)
        if override is not None:
            environment["MORSECORE_HOME"] = str(override)
        command = [str(executable)]
        if launcher:
            command = [os.environ.get("COMSPEC", r"C:\Windows\System32\cmd.exe"), "/d", "/c", str(executable)]
        process = subprocess.run(command, input=text.encode("utf-8"), cwd=self.cwd,
                                 capture_output=True, env=environment, timeout=30)
        self.assertEqual(process.returncode, 0, process.stderr.decode(errors="replace"))
        self.assertEqual(process.stderr, b"")
        return process.stdout.decode("utf-8").replace("\r\n", "\n")

    def install(self, destination):
        result = subprocess.run([str(CMAKE), "--install", str(BUILD), "--prefix", str(destination)],
                                capture_output=True, timeout=30)
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))

    def assert_fresh_layout(self, home):
        self.assertEqual((home / "config/settings.txt").read_text(), "word_separator=/\nlanguage=EN\n")
        self.assertEqual(list((home / "data").iterdir()), [])
        for name in (EXE.name, "run.cmd", "README.md", "README.tr.md", "LICENSE", "NOTICE"):
            self.assertTrue((home / name).is_file(), name)

    def test_install_relocate_unicode_path_and_keep_settings(self):
        installed = self.base / "Morse Core Türkçe Çalışma"
        self.install(installed)
        self.assert_fresh_layout(installed)
        (installed / "data/message.txt").write_text("HELLO WORLD", encoding="utf-8")
        output = self.execute(installed / EXE.name, "3\nmessage\n\n7\n2\nTR\n\n0\n0\n")
        self.assertIn("File converted successfully", output)
        self.assertEqual((installed / "data/morse_output_1.txt").read_text().strip(),
                         ".... . .-.. .-.. --- / .-- --- .-. .-.. -..")
        prefix = "Taşınan İkinci Konum "
        padding = max(1, 215 - len(str(self.base)) - len(prefix))
        relocated = self.base / (prefix + "a" * padding)
        installed.rename(relocated)
        self.assertGreater(len(str(relocated / "data/message.txt").encode("utf-8")), 200)
        output = self.execute(relocated / EXE.name, "4\nmorse_output_1\n\n0\n")
        self.assertIn("Dosya basariyla donusturuldu", output)
        self.assertIn("language=TR", (relocated / "config/settings.txt").read_text())
        self.assertEqual((relocated / "data/decoded_output_1.txt").read_text(), "HELLO WORLD")
        self.assertEqual(list(self.cwd.iterdir()), [])

    def test_bare_executable_creates_default_directories(self):
        home = self.base / "Yalnız Uygulama Çığ"
        home.mkdir()
        shutil.copy2(EXE, home / EXE.name)
        self.assertIn("Text to Morse", self.execute(home / EXE.name))
        self.assertTrue((home / "data").is_dir())
        self.assertEqual((home / "config/settings.txt").read_text(), "word_separator=/\nlanguage=EN\n")

    def test_explicit_unicode_home_is_isolated_and_persistent(self):
        home = self.base / "Özel Veri Klasörü"
        home.mkdir()
        self.execute(EXE, "7\n1\n|\n\n0\n0\n", override=home)
        output = self.execute(EXE, "1\nA B\ny\n\n0\n", override=home)
        self.assertIn(".- | -...", output)
        self.assertIn("word_separator=|", (home / "config/settings.txt").read_text())
        self.assertEqual(len(list((home / "data").glob("*.txt"))), 1)
        self.assertEqual(list(self.cwd.iterdir()), [])

    @unittest.skipIf(os.name == "nt", "Backslash is a path separator on Windows")
    def test_posix_home_ending_in_literal_backslash(self):
        home = self.base / "literal-backslash\\"
        home.mkdir()
        self.assertIn("Text to Morse", self.execute(EXE, override=home))
        self.assertTrue((home / "data").is_dir())
        self.assertEqual((home / "config/settings.txt").read_text(),
                         "word_separator=/\nlanguage=EN\n")
        self.assertFalse((self.base / "literal-backslash\\data").exists())
        self.assertFalse((self.base / "literal-backslash\\config").exists())
        self.assertEqual(list(self.cwd.iterdir()), [])

    def test_oversized_home_fails_without_fallback_or_writes(self):
        environment = {**self.environment, "MORSECORE_HOME": str(self.base) + "/" + "a" * 5000}
        result = subprocess.run([str(EXE)], input=b"0\n", cwd=self.cwd, capture_output=True,
                                env=environment, timeout=10)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b"cannot prepare data/config", result.stderr)
        self.assertEqual(list(self.cwd.iterdir()), [])

    def test_zip_has_clean_defaults_and_runs_after_extraction(self):
        package_dir = self.base / "packages"
        result = subprocess.run([str(CPACK), "--config", str(BUILD / "CPackConfig.cmake"),
                                 "-B", str(package_dir)], cwd=BUILD, capture_output=True, timeout=60)
        self.assertEqual(result.returncode, 0, result.stderr.decode(errors="replace"))
        packages = list(package_dir.glob("*.zip"))
        self.assertEqual(len(packages), 1)
        extracted = self.base / "ZIP Çıkarılan Klasör"
        with zipfile.ZipFile(packages[0]) as archive:
            archive.extractall(extracted)
            executable = next(extracted.rglob(EXE.name))
            if os.name != "nt":
                member = archive.getinfo(executable.relative_to(extracted).as_posix())
                mode = (member.external_attr >> 16) & 0o777
                self.assertTrue(mode & 0o111, "Packaged executable has no Unix execute permission")
                # zipfile does not restore Unix permissions during extraction.
                executable.chmod(mode)
        self.assert_fresh_layout(executable.parent)
        self.assertIn("Text to Morse", self.execute(executable))
        if os.name == "nt":
            self.assertIn("Text to Morse", self.execute(executable.parent / "run.cmd", launcher=True))


if __name__ == "__main__":
    unittest.main(verbosity=2)
