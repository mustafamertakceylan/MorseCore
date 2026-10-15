"""Run command and batch conversions against the real executable in isolation."""

from pathlib import Path
import hashlib
import os
import re
import subprocess
import sys
import tempfile
import unittest
import wave


EXE = Path(sys.argv.pop(1)).resolve()
SOS = "... --- ..."


class CommandTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="command-", dir=EXE.parent)
        self.root = Path(self.temporary.name)
        self.home = self.root / "runtime home"
        self.home.mkdir()
        self.env = {**os.environ, "MORSECORE_HOME": str(self.home), "NO_COLOR": "1"}

    def tearDown(self):
        self.temporary.cleanup()

    def command(self, *arguments, data=None, code=0, timeout=120):
        if isinstance(data, str):
            data = data.encode("utf-8")
        result = subprocess.run([str(EXE), *map(str, arguments)], input=data,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                env=self.env, cwd=self.root, timeout=timeout)
        self.assertEqual(result.returncode, code,
                         f"{arguments!r}: stdout={result.stdout[:300]!r}, stderr={result.stderr[:1000]!r}")
        self.assertNotIn(b"\x1b", result.stdout)
        self.assertNotIn(b"MORSE CORE", result.stdout)
        self.assertNotIn(b"Session Summary", result.stdout)
        return result

    def result_text(self, result):
        return result.stdout.decode("utf-8").replace("\r\n", "\n").rstrip("\n")

    def fixture(self, name, content):
        path = self.root / name
        if isinstance(content, str):
            content = content.encode("utf-8")
        path.write_bytes(content)
        return path

    def test_help_and_version(self):
        help_result = self.command("--help")
        self.assertEqual(help_result.stderr, b"")
        for option in ("--encode", "--decode", "--batch", "--wav", "--play", "--overwrite"):
            self.assertIn(option, help_result.stdout.decode())
        version = self.command("--version")
        self.assertRegex(self.result_text(version), r"^MorseCore \d+\.\d+\.\d+$")
        self.assertEqual(version.stderr, b"")

    def test_inline_encode_decode_and_turkish_unicode_arguments(self):
        encoded = self.command("--encode", "SOS")
        self.assertEqual(self.result_text(encoded), SOS)
        self.assertEqual(encoded.stderr, b"")
        self.assertEqual(self.result_text(self.command("--decode", SOS)), "SOS")
        encoded = self.result_text(self.command("--encode", "Çığ ÖŞÜ 2026!"))
        self.assertEqual(self.result_text(self.command("--decode", encoded)), "CIG OSU 2026!")
        self.assertEqual(self.result_text(self.command("--encode", "--", "--")), "-....- -....-")

    def test_stdin_whitespace_and_separators(self):
        for separator in "/|#*":
            encoded = self.command("--encode", "--separator", separator, data=" \tSOS\r\nTEST\n")
            self.assertEqual(self.result_text(encoded), f"{SOS} {separator} - . ... -")
            decoded = self.command("--decode", "--input", "-", "--separator", separator,
                                   data=encoded.stdout)
            self.assertEqual(self.result_text(decoded), "SOS TEST")
            self.assertEqual(decoded.stderr, b"")

    def test_decode_auto_separator_and_leading_dashes(self):
        self.assertEqual(self.result_text(self.command("--decode", "-- --- .-. ... .")), "MORSE")
        self.assertEqual(self.result_text(self.command("--decode", "--")), "M")
        self.assertEqual(self.result_text(self.command("--decode", "--", "--output", self.root / "m.txt")), "")
        self.assertEqual((self.root / "m.txt").read_text(), "M")
        self.assertEqual(self.result_text(self.command("--decode", "--", "-- --- .-. ... .")), "MORSE")
        for separator in "/|#*":
            self.assertEqual(self.result_text(self.command("--decode", f"{SOS} {separator} - . ... -")), "SOS TEST")
        self.command("--decode", "--does-not-exist", code=2)

    def test_punctuation_roundtrip(self):
        text = 'AZ 09 .,?\'/():=+-"@!&;_$'
        encoded = self.result_text(self.command("--encode", text))
        self.assertEqual(self.result_text(self.command("--decode", encoded)), text)

    def test_unicode_and_spaced_file_paths(self):
        source = self.fixture("çığ kaynak metni.txt", "İstanbul, Dünya!\r\nSOS")
        original = source.read_bytes()
        encoded = self.root / "şifreli çıktı.txt"
        decoded = self.root / "çözülmüş çıktı.txt"
        result = self.command("--encode", "--input", source, "--output", encoded)
        self.assertEqual((result.stdout, result.stderr), (b"", b""))
        result = self.command("--decode", "--input", encoded, "--output", decoded)
        self.assertEqual((result.stdout, result.stderr), (b"", b""))
        self.assertEqual(decoded.read_text(encoding="utf-8"), "ISTANBUL, DUNYA! SOS")
        self.assertEqual(source.read_bytes(), original)
        self.assertEqual(list(self.root.glob("*.tmp-*")), [])

    def test_existing_output_is_protected_and_overwrite_is_explicit(self):
        output = self.fixture("existing.txt", "KEEP THIS")
        failed = self.command("--encode", "SOS", "--output", output, code=1)
        self.assertEqual(failed.stdout, b"")
        self.assertIn(b"--overwrite", failed.stderr)
        self.assertEqual(output.read_text(), "KEEP THIS")
        succeeded = self.command("--encode", "SOS", "--output", output, "--overwrite")
        self.assertEqual(succeeded.stdout, b"")
        self.assertEqual(output.read_text(), SOS)

    def test_source_is_protected_even_with_overwrite(self):
        source = self.fixture("source.txt", "SOS")
        for destination in (source, self.root / "." / source.name):
            self.command("--encode", "--input", source, "--output", destination, "--overwrite", code=1)
            self.assertEqual(source.read_bytes(), b"SOS")
        alias = self.root / "hardlink.txt"
        try:
            os.link(source, alias)
        except OSError:
            return
        self.command("--encode", "--input", source, "--output", alias, "--overwrite", code=1)
        self.assertEqual(source.read_bytes(), b"SOS")

    def test_invalid_conversion_and_file_diagnostics(self):
        for arguments, data in [(("--encode", "💡"), None),
                                (("--decode", "......"), None),
                                (("--encode",), b""),
                                (("--encode",), b"A\0B"),
                                (("--encode", "--input", "missing.txt"), None)]:
            result = self.command(*arguments, data=data, code=1)
            self.assertEqual(result.stdout, b"")
            self.assertIn(b"MorseCore:", result.stderr)
            self.assertRegex(result.stderr.decode(), r"code -\d+")
        invalid_file = self.fixture("nul.txt", b"SOS\0TAIL")
        self.command("--encode", "--input", invalid_file, code=1)

    def test_invalid_option_combinations(self):
        arguments = [
            ("--unknown",), ("--encode", "--decode"), ("--encode", "--encode", "SOS"),
            ("--encode", "A", "B"), ("--encode", "--input"),
            ("--encode", "SOS", "--input", "x.txt"),
            ("--encode", "SOS", "--separator", "!"),
            ("--encode", "SOS", "--separator", "//"),
            ("--encode", "SOS", "--wpm", "30"),
            ("--encode", "SOS", "--wav", "x.wav", "--wpm", "0"),
            ("--encode", "SOS", "--wav", "x.wav", "--frequency", "100"),
            ("--encode", "SOS", "--wav", "x.wav", "--wpm", "3.5"),
            ("--decode", SOS, "--wav", "x.wav"),
            ("--batch", "input", "--encode"),
            ("--encode", "--output-dir", "output"),
            ("--batch", "input", "--encode", "--output-dir", "output", "--play"),
            ("--encode", "SOS", "--", "extra"),
        ]
        for options in arguments:
            with self.subTest(options=options):
                result = self.command(*options, code=2)
                self.assertEqual(result.stdout, b"")
                self.assertIn(b"--help", result.stderr)

    def test_wav_output_and_collision(self):
        destination = self.root / "çığ sesi.wav"
        result = self.command("--encode", "SOS", "--wav", destination,
                              "--wpm", "60", "--frequency", "750")
        self.assertEqual(self.result_text(result), SOS)
        self.assertEqual(result.stderr, b"")
        with wave.open(str(destination), "rb") as wav:
            self.assertEqual((wav.getnchannels(), wav.getsampwidth(), wav.getframerate()), (1, 2, 22050))
            self.assertGreater(wav.getnframes(), 0)
        original = hashlib.sha256(destination.read_bytes()).digest()
        self.command("--encode", "TEST", "--wav", destination, code=1)
        self.assertEqual(hashlib.sha256(destination.read_bytes()).digest(), original)
        self.command("--encode", "TEST", "--wav", destination, "--overwrite")
        self.assertNotEqual(hashlib.sha256(destination.read_bytes()).digest(), original)

    def test_output_failures_and_identical_audio_text_destinations(self):
        missing = self.root / "missing" / "output.txt"
        result = self.command("--encode", "SOS", "--output", missing, code=1)
        self.assertEqual(result.stdout, b"")
        self.assertFalse(missing.exists())
        destination = self.root / "both.txt"
        child = self.root / "child"
        child.mkdir()
        alias = child / ".." / destination.name
        result = self.command("--encode", "SOS", "--output", destination,
                              "--wav", alias, "--overwrite", code=1)
        self.assertIn(b"different", result.stderr)
        self.assertFalse(destination.exists())
        self.command("--encode", "SOS", "--output", child, "--overwrite", code=1)
        self.assertEqual(list(self.root.glob("*.tmp-*")), [])

    def test_batch_mixed_success_filters_and_unicode_names(self):
        source = self.root / "toplu kaynak"
        output = self.root / "toplu sonuç"
        source.mkdir()
        for name, text in {"a.txt": "SOS", "b.TXT": "TEST!", "çığ örnek.txt": "Çığ", "z.txt": "💡"}.items():
            (source / name).write_text(text, encoding="utf-8")
        (source / "skip.md").write_text("SOS")
        child = source / "child.txt"
        child.mkdir()
        (child / "nested.txt").write_text("SOS")
        originals = {p.name: p.read_bytes() for p in source.iterdir() if p.is_file()}
        result = self.command("--batch", source, "--encode", "--output-dir", output, code=1)
        self.assertEqual(result.stdout, b"")
        self.assertIn(b"3 succeeded, 1 failed", result.stderr)
        self.assertIn(b"z.txt", result.stderr)
        self.assertEqual(sorted(p.name for p in output.iterdir()),
                         sorted(["a.morse.txt", "b.morse.txt", "çığ örnek.morse.txt"]))
        for name, content in originals.items():
            self.assertEqual((source / name).read_bytes(), content)
        decoded = self.root / "decoded"
        self.command("--batch", output, "--decode", "--output-dir", decoded)
        self.assertEqual((decoded / "b.morse.text.txt").read_text(), "TEST!")

    def test_batch_collision_continues_and_overwrite(self):
        source, output = self.root / "input", self.root / "output"
        source.mkdir()
        output.mkdir()
        (source / "a.txt").write_text("SOS")
        (source / "b.txt").write_text("TEST")
        existing = output / "a.morse.txt"
        existing.write_text("PRESERVE")
        result = self.command("--batch", source, "--encode", "--output-dir", output, code=1)
        self.assertIn(b"1 succeeded, 1 failed", result.stderr)
        self.assertEqual(existing.read_text(), "PRESERVE")
        self.assertTrue((output / "b.morse.txt").is_file())
        result = self.command("--batch", source, "--encode", "--output-dir", output, "--overwrite")
        self.assertIn(b"2 succeeded, 0 failed", result.stderr)
        self.assertEqual(existing.read_text(), SOS)

    def test_batch_same_directory_rejected_and_nested_output_not_reprocessed(self):
        source = self.root / "input"
        source.mkdir()
        (source / "a.txt").write_text("SOS")
        result = self.command("--batch", source, "--encode", "--output-dir", source / ".", code=1)
        self.assertIn(b"different", result.stderr)
        nested = source / "output"
        for _ in range(2):
            result = self.command("--batch", source, "--encode", "--output-dir", nested, "--overwrite")
            self.assertIn(b"1 succeeded, 0 failed", result.stderr)
            self.assertEqual([p.name for p in nested.iterdir()], ["a.morse.txt"])

    def test_batch_empty_and_missing_directory(self):
        source = self.root / "empty"
        source.mkdir()
        result = self.command("--batch", source, "--decode", "--output-dir", self.root / "output")
        self.assertEqual(result.stdout, b"")
        self.assertIn(b"0 succeeded, 0 failed", result.stderr)
        self.command("--batch", self.root / "missing", "--encode", "--output-dir", source, code=1)

    def test_batch_file_symlinks_are_not_followed(self):
        source = self.root / "input"
        source.mkdir()
        real = self.fixture("outside.txt", "SOS")
        try:
            (source / "linked.txt").symlink_to(real)
        except OSError:
            self.skipTest("Creating symbolic links is unavailable on this host")
        result = self.command("--batch", source, "--encode", "--output-dir", self.root / "output")
        self.assertIn(b"0 succeeded, 0 failed", result.stderr)
        self.assertEqual(real.read_bytes(), b"SOS")

    def test_large_file_roundtrip_over_two_megabytes(self):
        text = ("SOS, TEST! 42? " * 160000).strip()
        self.assertGreater(len(text.encode()), 2 * 1024 * 1024)
        source = self.fixture("large source.txt", text)
        encoded, decoded = self.root / "large morse.txt", self.root / "large decoded.txt"
        original = hashlib.sha256(source.read_bytes()).digest()
        result = self.command("--encode", "--input", source, "--output", encoded)
        self.assertEqual((result.stdout, result.stderr), (b"", b""))
        self.assertGreater(encoded.stat().st_size, source.stat().st_size)
        result = self.command("--decode", "--input", encoded, "--output", decoded)
        self.assertEqual((result.stdout, result.stderr), (b"", b""))
        self.assertEqual(decoded.read_bytes(), text.encode())
        self.assertEqual(hashlib.sha256(source.read_bytes()).digest(), original)


if __name__ == "__main__":
    unittest.main(verbosity=2)
