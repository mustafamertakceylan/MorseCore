"""Exercise console audio/practice flows without requiring an audio device."""
from array import array
import hashlib
import os
from pathlib import Path
import queue
import re
import subprocess
import sys
import tempfile
import threading
import time
import unittest
import wave

EXE = Path(sys.argv.pop(1)).resolve()
SOURCE = Path(sys.argv.pop(1)).resolve()
ANSI = re.compile(r"\x1b\[[0-9;]*m")
MORSE = dict(zip(
    [".-", "-...", "-.-.", "-..", ".", "..-.", "--.", "....", "..", ".---", "-.-", ".-..",
     "--", "-.", "---", ".--.", "--.-", ".-.", "...", "-", "..-", "...-", ".--", "-..-", "-.--", "--.."],
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"))


def clean(text):
    return ANSI.sub("", text.replace("\r\n", "\n"))


def source_state():
    return {str(path): hashlib.sha256(path.read_bytes()).hexdigest()
            for directory in (SOURCE / "data", SOURCE / "config")
            for path in directory.rglob("*") if path.is_file()}


class ConsoleFeatureTests(unittest.TestCase):
    def setUp(self):
        self.original = source_state()
        self.temporary = tempfile.TemporaryDirectory(dir=EXE.parent, prefix="console-runtime-")
        self.home = Path(self.temporary.name)
        self.data = self.home / "data"
        self.data.mkdir()
        self.settings = self.home / "config/settings.txt"
        self.settings.parent.mkdir()
        self.environment = {**os.environ, "MORSECORE_HOME": str(self.home)}
        self.set_language("EN")

    def tearDown(self):
        self.temporary.cleanup()
        self.assertEqual(source_state(), self.original, "Repository data/settings changed")

    def set_language(self, language):
        self.settings.write_text(f"word_separator=/\nlanguage={language}\n", encoding="utf-8")

    def run_cli(self, script):
        process = subprocess.run([str(EXE)], input=script.encode("utf-8"),
                                 capture_output=True, cwd=self.home, env=self.environment,
                                 timeout=15)
        self.assertEqual(process.returncode, 0, process.stderr.decode(errors="replace"))
        self.assertEqual(process.stderr, b"")
        return clean(process.stdout.decode("utf-8"))

    def read_wav(self, path):
        raw = path.read_bytes()
        self.assertEqual(raw[:4], b"RIFF")
        self.assertEqual(raw[8:12], b"WAVE")
        self.assertEqual(int.from_bytes(raw[4:8], "little"), len(raw) - 8)
        with wave.open(str(path), "rb") as audio:
            self.assertEqual(audio.getnchannels(), 1)
            self.assertEqual(audio.getsampwidth(), 2)
            self.assertEqual(audio.getframerate(), 22050)
            self.assertEqual(audio.getcomptype(), "NONE")
            count = audio.getnframes()
            samples = array("h", audio.readframes(count))
        if sys.byteorder != "little":
            samples.byteswap()
        self.assertEqual(len(samples), count)
        self.assertTrue(any(samples), "WAV contains only silence")
        return count, samples

    def assert_tone(self, samples, expected):
        # Measure positive zero crossings away from the short attack/release.
        crossings = [index for index in range(1, len(samples))
                     if samples[index - 1] <= 0 < samples[index]]
        self.assertGreater(len(crossings), 3)
        measured = (len(crossings) - 1) * 22050 / (crossings[-1] - crossings[0])
        self.assertAlmostEqual(measured, expected, delta=5)

    def assert_accuracy(self, output, expected, label="Accuracy"):
        # printf follows the host's numeric locale, independently of UI language.
        match = re.search(rf"{re.escape(label)}: (\d+[.,]\d+)%", output)
        self.assertIsNotNone(match, output)
        self.assertAlmostEqual(float(match.group(1).replace(",", ".")), expected, places=1)

    def test_english_and_turkish_menu_entries(self):
        for language, audio, practice in (("EN", "Morse Audio / WAV", "Practice"),
                                          ("TR", "Sesli Morse / WAV", "Alistirma")):
            with self.subTest(language=language):
                self.set_language(language)
                output = self.run_cli("0\n")
                self.assertRegex(output, rf"\[8\]\s+{re.escape(audio)}")
                self.assertRegex(output, rf"\[9\]\s+{re.escape(practice)}")

    def test_wav_defaults_speed_and_tone_are_real_pcm(self):
        output = self.run_cli("8\nE\n\n\n2\n\n8\nE\n40\n1000\n2\n\n0\n")
        self.assertEqual(output.count("WAV saved:"), 2)
        self.assertIn("Saved outputs: 2", output)
        files = sorted(self.data.glob("morse_audio_*.wav"))
        self.assertEqual(len(files), 2)
        default_count, default_samples = self.read_wav(files[0])
        fast_count, fast_samples = self.read_wav(files[1])
        self.assertEqual(default_count, 1323)  # E: 1.2 / 20 seconds at 22050 Hz.
        self.assertEqual(fast_count, 662)  # Nearest integer frame at 40 WPM.
        self.assert_tone(default_samples, 600)
        self.assert_tone(fast_samples, 1000)
        self.assertNotIn("Playing Morse", output)

    def test_turkish_wav_and_invalid_audio_text(self):
        self.set_language("TR")
        output = self.run_cli("8\nHELLO%\n\n8\nE\n60\n2000\n2\n\n0\n")
        self.assertIn("Desteklenmeyen karakter", output)
        self.assertIn("WAV kaydedildi:", output)
        files = list(self.data.glob("*.wav"))
        self.assertEqual(len(files), 1)
        count, samples = self.read_wav(files[0])
        self.assertEqual(count, 441)
        self.assert_tone(samples, 2000)

    def test_audio_numbers_retry_and_cancel(self):
        output = self.run_cli("8\nE\nabc\n0\n61\n20tail\n" + "9" * 80 +
                              "\n40\n199\n2001\n600junk\n1200\n4\n2\n\n0\n")
        self.assertEqual(output.count("[!] Invalid input"), 9)
        files = list(self.data.glob("*.wav"))
        self.assertEqual(len(files), 1)
        count, samples = self.read_wav(files[0])
        self.assertEqual(count, 662)
        self.assert_tone(samples, 1200)
        output = self.run_cli("8\nE\n\n\n0\n\n0\n")
        self.assertNotIn("WAV saved", output)
        self.assertNotIn("Playing Morse", output)
        self.assertEqual(len(list(self.data.glob("*.wav"))), 1)

    def test_audio_duration_failure_is_reported_without_file(self):
        output = self.run_cli("8\n" + "T" * 2000 + "\n5\n600\n2\n\n0\n")
        self.assertIn("Audio operation failed", output)
        self.assertIn("10 minute duration limit", output)
        self.assertEqual(list(self.data.glob("*.wav")), [])
        self.assertIn("Invalid inputs: 1", output)

    def test_practice_wrong_skip_quit_in_both_languages(self):
        for language in ("EN", "TR"):
            with self.subTest(language=language):
                self.set_language(language)
                output = self.run_cli("9\n1\n1\n4\nwrong-answer\n:skip\n\n:quit\n\n0\n")
                if language == "EN":
                    self.assertIn("Completed: 3 / 4", output)
                    self.assertIn("Correct: 0   Incorrect: 1   Skipped: 2", output)
                    self.assert_accuracy(output, 0.0)
                else:
                    self.assertIn("Tamamlanan: 3 / 4", output)
                    self.assertIn("Dogru: 0   Yanlis: 1   Atlanan: 2", output)
                    self.assert_accuracy(output, 0.0, "Dogruluk")

    def test_practice_default_session_and_replay(self):
        output = self.run_cli("9\n\n\n\n" + ":skip\n" * 10 + "\n0\n")
        self.assertIn("Completed: 10 / 10", output)
        self.assertIn("Correct: 0   Incorrect: 0   Skipped: 10", output)
        output = self.run_cli("9\n1\n2\n1\n:replay\n:skip\n\n0\n")
        displayed = re.findall(r"^(?:Answer: )?([.\-]{5}) *$", output, flags=re.MULTILINE)
        self.assertEqual(len(displayed), 2)
        self.assertEqual(displayed[0], displayed[1])
        self.assertEqual(output.count("Question 1 / 1"), 1)
        self.assertIn("Completed: 1 / 1", output)
        self.assertIn("Correct: 0   Incorrect: 0   Skipped: 1", output)

    def test_practice_numeric_validation(self):
        output = self.run_cli("9\n0\n3\none\n1\n0\n4\n3\n0\n101\n2x\n" +
                              "9" * 80 + "\n2\n:skip\n:quit\n\n0\n")
        self.assertEqual(output.count("[!] Invalid input"), 9)
        self.assertIn("Completed: 1 / 2", output)
        self.assertIn("Correct: 0   Incorrect: 0   Skipped: 1", output)

    def test_visual_practice_scores_an_actual_decoded_question(self):
        # Decode the real prompt instead of changing the product's random seed.
        process = subprocess.Popen([str(EXE)], cwd=self.home, env=self.environment,
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=subprocess.PIPE, text=True, encoding="utf-8", bufsize=1)
        received = []
        pending = queue.Queue()

        def reader():
            for character in iter(lambda: process.stdout.read(1), ""):
                received.append(character)
                pending.put(character)
            pending.put(None)

        worker = threading.Thread(target=reader, daemon=True)
        worker.start()

        def send(text):
            process.stdin.write(text)
            process.stdin.flush()

        def until_prompt():
            text = ""
            deadline = time.monotonic() + 10
            while not text.endswith("\nAnswer: "):
                remaining = deadline - time.monotonic()
                self.assertGreater(remaining, 0, clean("".join(received)))
                try:
                    character = pending.get(timeout=remaining)
                except queue.Empty:
                    self.fail("Timed out waiting for practice answer prompt")
                self.assertIsNotNone(character, clean("".join(received)))
                text += character
            return clean(text)

        try:
            send("9\n1\n3\n3\n")
            question = until_prompt()
            match = re.search(r"Question 1 / 3\n([.\- ]+)\nAnswer: $", question)
            self.assertIsNotNone(match, question)
            answer = "".join(MORSE[token] for token in match.group(1).split())
            self.assertNotRegex(question, r"(?:Correct|Incorrect|Skipped) \| Answer:")
            send("  " + answer.lower() + " \t\n")
            until_prompt()
            send("wrong-answer\n")
            until_prompt()
            send(":skip\n\n0\n")
            process.stdin.close()
            process.wait(timeout=10)
            worker.join(timeout=5)
            self.assertFalse(worker.is_alive())
            self.assertEqual(process.returncode, 0)
            self.assertEqual(process.stderr.read(), "")
            output = clean("".join(received))
            self.assertIn("Completed: 3 / 3", output)
            self.assertIn("Correct: 1   Incorrect: 1   Skipped: 1", output)
            self.assert_accuracy(output, 33.3)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            worker.join(timeout=5)
            process.stdout.close()
            process.stderr.close()
            if not process.stdin.closed:
                process.stdin.close()

    def test_eof_at_feature_prompts_does_not_open_audio(self):
        scripts = ["8\n", "8\nE\n", "8\nE\n20\n", "8\nE\n20\n600\n",
                   "9\n", "9\n1\n", "9\n1\n1\n", "9\n1\n1\n1\n",
                   "9\n2\n1\n1\n", "9\n2\n1\n1\n60\n"]
        for script in scripts:
            with self.subTest(script=script):
                output = self.run_cli(script)
                self.assertEqual(len(re.findall(r"\[8\]\s+Morse Audio / WAV", output)), 1)
                self.assertEqual(len(re.findall(r"\[9\]\s+Practice", output)), 1)
                self.assertNotIn("Playing Morse", output)
                self.assertNotIn("Audio operation failed", output)
        self.assertEqual(list(self.data.glob("*.wav")), [])

    def test_listening_source_guard_hides_answer_and_handles_device_failure(self):
        # This is a source-level guard only; actual hardware playback is not
        # exercised or claimed by this device-independent suite.
        source = (SOURCE / "src/console_features.c").read_text(encoding="utf-8")
        start = source.index("while (practiceNext(")
        prompt = source.index('printf("%s", localized(language, "Cevap: ", "Answer: "))', start)
        before_prompt = source[start:prompt]
        self.assertRegex(before_prompt, r"if\s*\(presentation == 1\)\s*printf")
        self.assertNotRegex(before_prompt, r"printf\([^;]*question\.answer")
        self.assertRegex(before_prompt, r"else if\s*\(!playMorseAudio\([^;]+\)\)\s*\{"
                                      r"\s*printAudioError\(language\);[^}]+break;")


if __name__ == "__main__":
    unittest.main(verbosity=2)
