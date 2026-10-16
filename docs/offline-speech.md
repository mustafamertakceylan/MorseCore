# Offline speech

The in-app voice manager is available from **v0.6.0**. Users of **v0.5.0** can upgrade or continue using **Set up offline voices** from the Start menu / **setup-speech.cmd** in the portable ZIP.

[Türkçe](offline-speech.tr.md) · [Desktop](desktop.md)

Open **Audio → Voice packs**, choose a language, then **Download and install** next to a voice. Listen to the sample and select **Use this voice**. In **Audio → Create audio**, enter text or use the last conversion and press **Speak**. The converter itself only converts.

No terminal commands are needed. If the speech runtime is missing, the app offers to install the official Python runtime for your Windows user, with confirmation. It reuses compatible Python 3.11–3.13 x64 installations, does not change PATH and requests no administrator access. Setup also offers this optional prerequisite. Once the official Python installer starts, wait for it to finish; the app does not kill it midway.

| Language | Available voices |
|---|---|
| Turkish | EMA Lightning · Antalia-2 Mini |
| English | Joe · Piper |
| German | Thorsten · Piper; Kerstin · Piper |
| Russian | Dmitri · Piper; Denis · Piper |
| Arabic | Kareem · Piper, Jordanian Arabic |

## Download once, use offline

Downloads start only after your confirmation. The app shows each model's exact download size, progress, cancellation and errors. Shared runtime dependencies take extra space; their download size is not included in the model size. A Piper-only installation does not install PyTorch or the Turkish engines. Leaving the voice-pack page cancels model/dependency installation; verified files are reused when you retry.

Speech then uses local files with no API key or background server. Missing voices show a message and never trigger an automatic download. Stop cancels generation and playback. Application update checks are controlled separately in Settings.

**Remove voice** deletes only that voice's unmodified, verified model files and model card. Other voices, Python, shared dependencies, settings, contacts and recordings remain. A file modified outside the manager is preserved. The selected speech-folder path is shown on the page; change it in **Settings → Speech pack** if needed.

## Limits and credits

Speech playback is available on Windows x64 and Apple Silicon macOS. Linux has no desktop interface yet. On Mac, use **Audio → Voice packs**; if needed, the app prepares its own verified Python runtime without changing the system PATH. Models load in a separate worker process when needed and exit after playback preparation. The request limit is 4000 characters and two minutes of generation. Small model files do not imply small peak RAM; Turkish voices use CPU PyTorch and can consume several hundred MiB while generating.

Thanks to the creators of [EMA Lightning](https://huggingface.co/canberkkkkkk/ema-lightning), [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) and [Piper](https://github.com/OHF-Voice/piper1-gpl/tree/v1.3.0). EMA and Antalia code/weights are Apache-2.0. Piper is GPL-3.0 and runs as a separate executable process. Individual models and dependencies retain their own terms. License and NOTICE files stay with the installed pack.

Kareem's source model card does not explicitly identify the dataset license. Review its [upstream model card](https://huggingface.co/rhasspy/piper-voices/blob/c10ece1aade47bb51c153c893d14e5bf8e5b7117/ar/ar_JO/kareem/medium/MODEL_CARD) before redistribution; the installation confirmation repeats this limitation. Other catalog model cards identify CC0 datasets. Installing the pack does not relicense third-party components as Apache-2.0.

<details>
<summary>Development and checks</summary>

The optional command-line setup still works: `python -X utf8 scripts/setup-speech.py --voice ema-tr`. Omit `--voice` to install the full catalog. Desktop installation uses the same version-pinned engine packages and individually verifies files from `scripts/speech-models.json`; downloads use temporary files, exact sizes and SHA-256 or Git blob digests before replacement.

The app discovers the ignored `speech-runtime/` development folder, a configured absolute folder or `%LOCALAPPDATA%/MorseCore/Speech`. `MORSECORE_SPEECH_HOME` overrides discovery when no folder is selected. Setup details are in `.voice-tools/setup.stdout.log` and `setup.stderr.log` inside the selected folder. This virtual environment is tied to the Python installation on the same computer; copying it to another machine is unsupported.

Run `python -m unittest discover -s tests -p test_speech_setup.py`, the AppCore smoke checks, and `pwsh -File scripts/test-speech.ps1` for installed voices. Actual sound quality and speaker output require listening; automated tests do not prove them.

</details>
