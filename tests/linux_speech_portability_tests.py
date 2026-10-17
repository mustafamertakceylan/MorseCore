"""No-network Linux speech checks; no model download or system package operation."""
import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("linux_speech_setup", ROOT / "scripts" / "setup-speech.py")
setup = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(setup)


class LinuxSpeechPortabilityTests(unittest.TestCase):
    def test_each_engine_installs_into_private_venv_and_never_uses_system_package_manager(self):
        for voice, package in (("ema-tr", "ema-lightning==1.1.2"),
                               ("antalia-tr", "antalia-mini==1.0.1"),
                               ("joe-en", "piper-tts==1.3.0")):
            with self.subTest(voice=voice), tempfile.TemporaryDirectory(prefix="MorseCore-LinuxSpeech-") as directory:
                home = Path(directory)
                python = home / "venv" / "bin" / "python"
                python.parent.mkdir(parents=True)
                python.touch()

                def run(arguments, **kwargs):
                    return subprocess.CompletedProcess(arguments, 1 if "import importlib.metadata" in str(arguments) else 0)

                with patch.object(setup.sys, "platform", "linux"), \
                     patch.object(setup.sys, "version_info", (3, 12, 0)), \
                     patch.object(setup.subprocess, "run", side_effect=run) as process:
                    setup.install(home, voice=voice, packages_only=True)
                calls = [call.args[0] for call in process.call_args_list]
                installs = [args for args in calls if "install" in args]
                self.assertTrue(any(package in args for args in installs))
                self.assertTrue(all(args[0] == str(python) and "-m" in args and "pip" in args for args in installs))
                self.assertEqual(any("torch==2.10.0" in args for args in installs), voice != "joe-en")
                for arguments in installs:
                    if "torch==2.10.0" in arguments:
                        self.assertIn("https://download.pytorch.org/whl/cpu", arguments)
                self.assertFalse(any(args[0] in ("sudo", "apt", "apt-get", "pacman", "pkexec", "curl") for args in calls))
                self.assertFalse((home / "models").exists())
                self.assertFalse((home / "pack-ready.json").exists())

    def test_rolling_distro_unsupported_python_is_rejected_before_mutating_storage(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-LinuxVersion-") as directory:
            home = Path(directory) / "speech-not-created"
            with patch.object(setup.sys, "version_info", (3, 14, 0)), \
                 patch.object(setup.subprocess, "run") as process:
                with self.assertRaisesRegex(SystemExit, "Python 3.11-3.13"):
                    setup.install(home, voice="joe-en", packages_only=True)
            process.assert_not_called()
            self.assertFalse(home.exists())

    def test_missing_linux_venv_support_does_not_clear_existing_data(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-LinuxVenv-") as directory:
            home = Path(directory)
            model = home / "models" / "saved.bin"
            model.parent.mkdir()
            model.write_bytes(b"preserve-model")

            def run(arguments, **kwargs):
                if "venv" in arguments:
                    raise subprocess.CalledProcessError(1, arguments)
                return subprocess.CompletedProcess(arguments, 1)

            with patch.object(setup.sys, "platform", "linux"), \
                 patch.object(setup.subprocess, "run", side_effect=run) as process:
                with self.assertRaises(subprocess.CalledProcessError):
                    setup.ensure_environment(home)
            self.assertEqual(model.read_bytes(), b"preserve-model")
            self.assertTrue(all("--clear" not in call.args[0] for call in process.call_args_list))


if __name__ == "__main__":
    unittest.main()
