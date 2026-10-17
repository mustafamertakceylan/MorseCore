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
            with patch.object(setup.sys, "version_info", (3, 12, 0)), \
                 patch.object(setup.subprocess, "run") as run:
                run.return_value.returncode = 0
                setup.install(home, skip_packages=True, voice="joe-en", packages_only=True)
            self.assertFalse((home / "models").exists())
            self.assertFalse((home / "pack-ready.json").exists())
            self.assertTrue((home / "speech_worker.py").is_file())
            self.assertTrue((home / "licenses" / "speech" / "antalia-mini" / "NOTICE").is_file())
            self.assertTrue(run.called)  # Installed-package report remains available.
            self.assertTrue(all("curl" not in str(call) for call in run.call_args_list))
            self.assertFalse(any("install" in call.args[0] or "version('pip')" in str(call.args[0])
                                 for call in run.call_args_list))

    def test_piper_install_does_not_request_pytorch_or_other_engines(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-SpeechSetup-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            with patch.object(setup.sys, "version_info", (3, 12, 0)), \
                 patch.object(setup.subprocess, "run") as run:
                # The venv and pip are healthy, but the selected engine is absent.
                run.side_effect = lambda arguments, **kwargs: subprocess.CompletedProcess(arguments, 1 if "import importlib.metadata" in str(arguments) else 0)
                setup.install(home, voice="joe-en", packages_only=True)
            calls = "\n".join(str(call) for call in run.call_args_list)
            self.assertIn("piper-tts==1.3.0", calls)
            self.assertNotIn("torch==", calls)
            self.assertNotIn("ema-lightning==", calls)
            self.assertNotIn("antalia-mini==", calls)
            self.assertIn("--no-input", calls)

    def test_old_private_pip_is_repaired_before_torch_without_touching_system_python(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-OldPip-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            modern = False

            def run(arguments, **kwargs):
                nonlocal modern
                if setup.PIP_BOOTSTRAP in arguments:
                    modern = True
                code = 1 if "import importlib.metadata" in str(arguments) or ("version('pip')" in str(arguments) and not modern) else 0
                return subprocess.CompletedProcess(arguments, code)

            with patch.object(setup.sys, "version_info", (3, 12, 0)), \
                 patch.object(setup.subprocess, "run", side_effect=run) as process:
                setup.install(home, voice="ema-tr", packages_only=True)
            calls = [call.args[0] for call in process.call_args_list]
            repairs = [args for args in calls if setup.PIP_BOOTSTRAP in args]
            self.assertEqual(len(repairs), 1)
            repair = repairs[0]
            self.assertEqual(repair[0], str(python))
            self.assertIn("--require-virtualenv", repair)
            self.assertIn("--isolated", repair)
            self.assertIn("--only-binary=:all:", repair)
            self.assertIn("https://pypi.org/simple", repair)
            self.assertIn("--upgrade", repair)
            self.assertNotIn("--user", repair)
            self.assertLess(calls.index(repair), next(i for i, args in enumerate(calls) if "torch==2.10.0" in args and "install" in args))
            self.assertEqual(sum("version('pip')" in str(args) for args in calls), 2)
            self.assertTrue(all(args[0] == str(python) for args in calls if "install" in args))

    def test_modern_private_pip_is_not_reinstalled_for_new_voice_dependencies(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-ModernPip-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            with patch.object(setup.sys, "version_info", (3, 12, 0)), \
                 patch.object(setup.subprocess, "run") as process:
                process.side_effect = lambda args, **kw: subprocess.CompletedProcess(args, 1 if "import importlib.metadata" in str(args) else 0)
                setup.install(home, voice="joe-en", packages_only=True)
            calls = [call.args[0] for call in process.call_args_list]
            self.assertFalse(any(setup.PIP_BOOTSTRAP in args for args in calls))
            self.assertTrue(any("piper-tts==1.3.0" in args and "install" in args for args in calls))

    def test_pip_floor_probe_compares_versions_and_requires_a_virtual_environment(self):
        # Run the actual child probe with synthetic metadata, without installing pip.
        with patch.object(setup.subprocess, "run") as process:
            process.return_value.returncode = 0
            setup.ensure_online_pip(Path(setup.sys.executable))
        probe = process.call_args.args[0][3]
        inject = "import importlib.metadata as m,sys; m.version=lambda name:sys.argv[1]; sys.prefix='synthetic-venv' if sys.argv[2]=='venv' else sys.base_prefix; exec(sys.argv[3])"
        for version, environment, expected in (("23.0.1", "venv", 1), ("25.3", "venv", 1),
                                               ("26.2", "venv", 1), ("26.2.1", "venv", 0),
                                               ("26.3", "venv", 0), ("27.0.0", "venv", 0),
                                               ("26.2.1", "system", 1)):
            with self.subTest(version=version, environment=environment):
                result = subprocess.run([setup.sys.executable, "-I", "-c", inject, version, environment, probe],
                                        stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
                self.assertEqual(result.returncode, expected, result.stderr.decode(errors="replace"))

    def test_complete_voice_dependencies_do_not_probe_or_upgrade_pip(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-ExistingVoice-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()
            with patch.object(setup.sys, "version_info", (3, 12, 0)), \
                 patch.object(setup.subprocess, "run") as process:
                process.return_value.returncode = 0
                setup.install(home, voice="ema-tr", packages_only=True)
            calls = [call.args[0] for call in process.call_args_list]
            self.assertFalse(any("version('pip')" in str(args) or "install" in args for args in calls))

    def test_failed_pip_repair_does_not_start_engine_installation(self):
        with tempfile.TemporaryDirectory(prefix="MorseCore-PipFailure-") as directory:
            home = Path(directory)
            python = home / "venv" / ("Scripts/python.exe" if setup.sys.platform == "win32" else "bin/python")
            python.parent.mkdir(parents=True)
            python.touch()

            def run(arguments, **kwargs):
                if setup.PIP_BOOTSTRAP in arguments:
                    raise subprocess.CalledProcessError(1, arguments)
                code = 1 if "import importlib.metadata" in str(arguments) or "version('pip')" in str(arguments) else 0
                return subprocess.CompletedProcess(arguments, code)

            with patch.object(setup.sys, "version_info", (3, 12, 0)), \
                 patch.object(setup.subprocess, "run", side_effect=run) as process:
                with self.assertRaises(subprocess.CalledProcessError):
                    setup.install(home, voice="ema-tr", packages_only=True)
            calls = [call.args[0] for call in process.call_args_list]
            self.assertFalse(any("torch==2.10.0" in args and "install" in args for args in calls))
            self.assertFalse((home / "pack-ready.json").exists())

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

    @unittest.skipUnless((3, 11) <= setup.sys.version_info[:2] < (3, 14),
                         "Real speech venv recovery requires supported Python 3.11–3.13")
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
