"""No-network checks for per-voice installation and GUI installer arguments."""
import importlib.util
import json
import tempfile
import unittest
import subprocess
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("speech_setup", ROOT / "scripts" / "setup-speech.py")
setup = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(setup)


class SpeechSetupTests(unittest.TestCase):
    def test_catalog_partition_and_exact_sizes(self):
        catalog = json.loads((ROOT / "scripts" / "speech-models.json").read_text(encoding="utf-8"))
        selected = [f for voice in setup.VOICES for f in setup.model_files(catalog, voice)]
        self.assertEqual(len(selected), len(catalog))
        self.assertEqual({f["path"] for f in selected}, {f["path"] for f in catalog})
        self.assertEqual(sum(f["bytes"] for f in setup.model_files(catalog, "ema-tr")), 34407274)
        self.assertEqual(len(setup.model_files(catalog, "joe-en")), 3)

    def test_dependencies_only_never_downloads_model(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-SpeechSetup-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            with patch.object(setup.subprocess, "run") as run:
                run.return_value.returncode = 0
                setup.install(home, skip_packages=True, voice="joe-en", packages_only=True)
            self.assertFalse((home / "models").exists())
            self.assertFalse((home / "pack-ready.json").exists())
            self.assertTrue((home / "speech_worker.py").is_file())
            self.assertTrue((home / "licenses" / "speech" / "antalia-mini" / "NOTICE").is_file())
            self.assertTrue(run.called)  # Installed-package report remains available.
            self.assertTrue(all("curl" not in str(call) for call in run.call_args_list))

    def test_piper_install_does_not_request_pytorch_or_other_engines(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-SpeechSetup-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            with patch.object(setup.subprocess, "run") as run:
                # The venv and pip are healthy, but the selected engine is absent.
                run.side_effect = lambda arguments, **kwargs: subprocess.CompletedProcess(arguments, 1 if "import importlib.metadata" in str(arguments) else 0)
                setup.install(home, voice="joe-en", packages_only=True)
            calls = "\n".join(str(call) for call in run.call_args_list)
            self.assertIn("piper-tts==1.3.0", calls)
            self.assertNotIn("torch==", calls)
            self.assertNotIn("ema-lightning==", calls)
            self.assertNotIn("antalia-mini==", calls)
            self.assertIn("--no-input", calls)

    def test_interrupted_ensurepip_is_repaired_without_deleting_models(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-SpeechSetup-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            model = home / "models" / "ema" / "ema.pt"
            model.parent.mkdir(parents=True)
            model.write_bytes(b"existing model")
            recording = home / "recording.wav"
            recording.write_bytes(b"user recording")
            state = {"pip": False}

            def run(arguments, **kwargs):
                if "ensurepip" in arguments:
                    state["pip"] = True
                code = 1 if "--version" in arguments and not state["pip"] else 0
                return subprocess.CompletedProcess(arguments, code)

            with patch.object(setup.subprocess, "run", side_effect=run) as process:
                self.assertEqual(setup.ensure_environment(home), python)
                self.assertTrue(state["pip"])
                self.assertTrue(any("ensurepip" in call.args[0] for call in process.call_args_list))
                self.assertFalse(any("venv" in call.args[0] for call in process.call_args_list))
                process.reset_mock()
                setup.ensure_environment(home)
                self.assertFalse(any("ensurepip" in call.args[0] for call in process.call_args_list))
            self.assertEqual(model.read_bytes(), b"existing model")
            self.assertEqual(recording.read_bytes(), b"user recording")

    def test_invalid_venv_launcher_repairs_from_base_without_clear(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-SpeechSetup-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            state = {"venv": False}

            def run(arguments, **kwargs):
                if "venv" in arguments:
                    state["venv"] = True
                code = 1 if "-c" in arguments and not state["venv"] else 0
                return subprocess.CompletedProcess(arguments, code)

            with patch.object(setup.subprocess, "run", side_effect=run) as process:
                setup.ensure_environment(home)
            recreations = [call.args[0] for call in process.call_args_list if "venv" in call.args[0]]
            self.assertEqual(len(recreations), 1)
            self.assertEqual(recreations[0][0], getattr(setup.sys, "_base_executable", None) or setup.sys.executable)
            self.assertIn("--without-pip", recreations[0])
            self.assertNotIn("--clear", recreations[0])

    def test_real_venv_without_pip_recovers_offline(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-SpeechSetup-") as directory:
            home = Path(directory)
            environment = home / "venv"
            subprocess.run([setup.sys.executable, "-m", "venv", "--without-pip", str(environment)], check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
            python = environment / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            probe = [str(python), "-I", "-m", "pip", "--version"]
            self.assertNotEqual(subprocess.run(probe, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode, 0)
            model = home / "models" / "saved-model.pt"
            model.parent.mkdir()
            model.write_bytes(b"preserve installed model")
            self.assertEqual(setup.ensure_environment(home), python)
            self.assertEqual(subprocess.run(probe, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode, 0)
            self.assertEqual(model.read_bytes(), b"preserve installed model")


if __name__ == "__main__":
    unittest.main()
