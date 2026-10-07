"""Exercise the real executable; restore settings and remove only test-created files."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys
import tempfile
import unittest

EXE = Path(sys.argv.pop(1)).resolve()
ROOT = Path(sys.argv.pop(1)).resolve()
DATA = ROOT / "data"
SETTINGS = ROOT / "config/settings.txt"
HELLO = ".... . .-.. .-.. --- / .-- --- .-. .-.. -.."


class CliTests(unittest.TestCase):
    def setUp(self):
        self.settings_backup = SETTINGS.read_bytes()
        self.original_files = {p: hashlib.sha256(p.read_bytes()).digest() for p in DATA.glob("*.txt")}
        self.created = set()
        SETTINGS.write_text("word_separator=/\nlanguage=EN\n", encoding="utf-8")

    def tearDown(self):
        SETTINGS.write_bytes(self.settings_backup)
        for path in self.created:
            path.unlink(missing_ok=True)
        for path, digest in self.original_files.items():
            self.assertEqual(hashlib.sha256(path.read_bytes()).digest(), digest, str(path))

    def run_cli(self, text, cwd=ROOT):
        before = set(DATA.glob("*.txt"))
        try:
            process = subprocess.run([str(EXE)], input=text.encode("utf-8"),
                                     stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                     cwd=cwd, timeout=10)
        finally:
            self.created.update(set(DATA.glob("*.txt")) - before)
        self.assertEqual(process.returncode, 0, process.stderr.decode(errors="replace"))
        self.assertEqual(process.stderr, b"")
        return re.sub(r"\x1b\[[0-9;]*m", "", process.stdout.decode("utf-8").replace("\r\n", "\n"))

    def fixture(self, content):
        with tempfile.NamedTemporaryFile(dir=DATA, prefix="qa_", suffix=".txt", delete=False) as file:
            file.write(content.encode("utf-8"))
            path = Path(file.name)
        self.created.add(path)
        return path.stem

    def test_encode_decode_and_save(self):
        output = self.run_cli(f"1\nHELLO WORLD\ny\n\n2\n{HELLO}\n\n0\n")
        self.assertIn(HELLO, output)
        self.assertIn("Text output:\nHELLO WORLD", output)
        self.assertIn("Saved outputs: 1", output)
        saved = [p for p in self.created if p.name.startswith("morse_output_")]
        self.assertEqual(len(saved), 1)
        self.assertEqual(saved[0].read_text().strip(), HELLO)

    def test_turkish_input_and_auto_separator(self):
        output = self.run_cli("1\nMerhaba Dünya\nn\n\n2\n.... . .-.. .-.. --- # .-- --- .-. .-.. -..\n\n0\n")
        self.assertIn("-- . .-. .... .- -... .- / -.. ..- -. -.-- .-", output)
        self.assertIn("Text output:\nHELLO WORLD", output)
        self.assertIn("Detected/active separator: #", output)

    def test_file_roundtrip_from_other_directory(self):
        name = self.fixture("HELLO\nWORLD")
        with tempfile.TemporaryDirectory(dir=EXE.parent) as cwd:
            output = self.run_cli(f"3\n{name}\n\n0\n", cwd)
            self.assertIn("File converted successfully", output)
            encoded = [p for p in self.created if p.name.startswith("morse_output_")]
            self.assertEqual(len(encoded), 1)
            self.assertEqual(encoded[0].read_text().strip(), HELLO)
            output = self.run_cli(f"4\n{encoded[0].name}\n\n0\n", cwd)
            self.assertIn("File converted successfully", output)
            decoded = [p for p in self.created if p.name.startswith("decoded_output_")]
            self.assertEqual(len(decoded), 1)
            self.assertEqual(decoded[0].read_text(), "HELLO WORLD")

    def test_persistent_settings(self):
        output = self.run_cli("7\n1\n|\n\n2\ntr\n\n0\n0\n")
        self.assertIn("Dil basariyla kaydedildi", output)
        self.assertIn("word_separator=|", SETTINGS.read_text())
        self.assertIn("language=TR", SETTINGS.read_text())
        output = self.run_cli("1\nA B\nn\n\n0\n", EXE.parent)
        self.assertIn("Metni Morse'a Cevir", output)
        self.assertIn(".- | -...", output)

    def test_invalid_input_and_missing_file(self):
        output = self.run_cli("1\nHELLO!\n\n2\n......\n\n3\n../config/settings\n\n4\nqa_missing_file\n\n0\n")
        self.assertIn("Unsupported character detected", output)
        self.assertIn("Invalid or empty Morse input", output)
        self.assertIn("Invalid input", output)
        self.assertIn("File could not be opened", output)
        self.assertIn("Invalid inputs: 4", output)

    def test_long_input_and_menu_recovery(self):
        output = self.run_cli("1\n" + "9" * 999 + "\nn\n\n1\n" + "9" * 1000 + "\n\n0\n")
        self.assertIn("Output length: 5994", output)
        self.assertIn("Text to Morse operations: 1", output)
        self.assertIn("Invalid inputs: 1", output)

    def test_large_file_is_rejected(self):
        name = self.fixture("9" * 1000)
        output = self.run_cli(f"3\n{name}\n\n0\n")
        self.assertIn("File could not be opened", output)
        self.assertNotIn("File converted successfully", output)
        self.assertFalse(any(p.name.startswith("morse_output_") for p in self.created))

    def test_menu_validation_table_and_about(self):
        output = self.run_cli("1junk\n\n5\n\n6\n\n0\n")
        self.assertIn("Invalid inputs: 1", output)
        self.assertIn("Z  ->  --..", output)
        self.assertIn("About MorseCore", output)

    def test_invalid_settings_are_rejected(self):
        output = self.run_cli("7\n1\nxx\n\n2\nENGLISH\n\n0\n0\n")
        self.assertIn("Invalid inputs: 2", output)
        self.assertEqual(SETTINGS.read_text(), "word_separator=/\nlanguage=EN\n")

    def test_eof(self):
        self.assertIn("Session Summary", self.run_cli(""))
        self.assertIn("Enter text:", self.run_cli("1\n"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
