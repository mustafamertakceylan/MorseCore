"""No-network regression checks for macOS speech dependency installation."""
import importlib.util
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("mac_speech_setup", ROOT / "scripts" / "setup-speech.py")
setup = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(setup)


class MacSpeechPortabilityTests(unittest.TestCase):
    def test_torch_index_matches_platform_without_changing_version(self):
        pip = ["/speech/venv/bin/python", "-m", "pip", "install"]
        for platform in ("darwin", "win32", "linux"):
            with self.subTest(platform=platform), patch.object(setup.sys, "platform", platform):
                arguments = setup.torch_install_arguments(pip)
                self.assertEqual(arguments[-1], "torch==2.10.0")
                self.assertEqual(arguments[:len(pip)], pip)
                self.assertEqual("--index-url" in arguments, platform != "darwin")
                self.assertEqual("https://download.pytorch.org/whl/cpu" in arguments, platform != "darwin")

    def test_each_mac_engine_installs_only_its_dependencies_without_models(self):
        for voice, package in (("ema-tr", "ema-lightning==1.1.2"),
                               ("antalia-tr", "antalia-mini==1.0.1"),
                               ("kareem-ar", "piper-tts==1.3.0")):
            with self.subTest(voice=voice), tempfile.TemporaryDirectory(prefix="MorseCore-MacSpeech-") as directory:
                home = Path(directory)
                python = home / "venv" / "bin" / "python"
                python.parent.mkdir(parents=True)
                python.touch()
                def run(arguments, **kwargs):
                    return subprocess.CompletedProcess(arguments, 1 if "import importlib.metadata" in str(arguments) else 0)
                with patch.object(setup.sys, "platform", "darwin"), \
                     patch.object(setup.sys, "version_info", (3, 12, 0)), \
                     patch.object(setup.subprocess, "run", side_effect=run) as process:
                    setup.install(home, voice=voice, packages_only=True)
                calls = [call.args[0] for call in process.call_args_list]
                installs = [args for args in calls if "install" in args]
                self.assertTrue(any(package in args for args in installs))
                self.assertFalse(any("curl" in str(args) for args in calls))
                self.assertFalse(any("--index-url" in args for args in installs))
                self.assertEqual(any("torch==2.10.0" in args for args in installs), voice != "kareem-ar")
                self.assertFalse((home / "models").exists())
                self.assertFalse((home / "pack-ready.json").exists())
                self.assertTrue((home / "speech_worker.py").is_file())

    def test_incompatible_existing_venv_is_repaired_without_clear(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-MacSpeechRepair-") as directory:
            home = Path(directory)
            python = home / "venv" / "bin" / "python"
            python.parent.mkdir(parents=True)
            python.touch()
            marker = home / "existing-recording.wav"
            marker.write_bytes(b"preserve")
            repaired = False
            def run(arguments, **kwargs):
                nonlocal repaired
                if "venv" in arguments:
                    repaired = True
                return subprocess.CompletedProcess(arguments, 0 if repaired else 1)
            with patch.object(setup.sys, "platform", "darwin"), \
                 patch.object(setup.subprocess, "run", side_effect=run) as process:
                self.assertEqual(setup.ensure_environment(home), python)
            probes = [call.args[0] for call in process.call_args_list if "-c" in call.args[0]]
            self.assertTrue(any("sys.version_info" in str(args) and "Py_GIL_DISABLED" in str(args) for args in probes))
            repairs = [call.args[0] for call in process.call_args_list if "venv" in call.args[0]]
            self.assertEqual(len(repairs), 1)
            self.assertIn("--without-pip", repairs[0])
            self.assertNotIn("--clear", repairs[0])
            self.assertEqual(marker.read_bytes(), b"preserve")


if __name__ == "__main__":
    unittest.main()
