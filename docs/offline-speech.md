# Offline speech

[Türkçe](offline-speech.tr.md) · [Desktop](desktop.md)

Speech uses local neural models instead of Windows speech packs. Text-to-Morse speaks the original text; Morse-to-text decodes the selected alphabet before speaking. Choose the speech language and voice directly below the converter. No manual voice scan or copying to another screen is required. **Stop** cancels synthesis and playback.

| Language | Voices | Runtime |
|---|---|---|
| Turkish | EMA Lightning; Antalia-2 Mini | CPU PyTorch |
| Arabic | Kareem, Jordanian Arabic; one speaker | Piper / ONNX Runtime CPU |
| German | Thorsten, high quality; Kerstin, low quality | Piper / ONNX Runtime CPU |
| Russian | Dmitri; Denis | Piper / ONNX Runtime CPU |
| English | Joe | Piper / ONNX Runtime CPU |

[EMA Lightning](https://huggingface.co/canberkkkkkk/ema-lightning) has 8.6M parameters, about 34 MB of weights. [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) has 7.62M parameters, about 30.5 MB of fp32 weights. These are publisher specifications, not RAM measurements or a guarantee of perceived voice quality. Alania's hosted API and the local Antalia Mini model are separate products.

## Install once

The development pack requires Python 3.11–3.13 x64, `curl`, and internet during setup. Windows Setup 0.4.1 checks Python and optionally installs the bundled, unmodified [Python 3.13.16 installer](https://www.python.org/downloads/release/python-31316/). It reuses a compatible interpreter without changing PATH. Voice models still download separately with consent. On Windows, double-click `setup-speech.cmd` in the repository, or run:

```powershell
python -X utf8 scripts/setup-speech.py
```

It installs an isolated CPU runtime and eight voices in the ignored `speech-runtime/` folder. Models total approximately 472 MiB; Python dependencies require additional disk space. Existing verified files are reused when setup is retried. Model revisions, file sizes and SHA-256/LFS or Git blob digests are pinned in `scripts/speech-models.json`. Model cards and package licenses remain in the pack. Source files and Git do not contain the model weights.

The application locates `speech-runtime` beside its directory or in a parent directory. Another location can be selected in **Settings → Speech pack**, or with the absolute `MORSECORE_SPEECH_HOME` environment variable. This preview uses the setup machine's Python installation; moving the virtual environment to a different computer is not a supported installer. MSIX does not embed the optional pack.

## What stays offline

Synthesis launches a separate process with local model paths. The Turkish adapter disables Hub access and rejects socket connections. Piper is invoked through its own CLI with an existing local model. No API key, service or background server is used. Missing models produce a message; synthesis never starts a download. The independent GitHub update setting still controls application update checks.

Models load on demand and the worker exits after each synthesis. Temporary WAV files are removed after playback or cancellation. Speech is limited to 4000 characters and two minutes of generation time. CPU inference uses no CUDA build; the Turkish adapter uses two CPU threads and skips batch-size tuning.

Small weights do not mean small runtime memory. On this Windows machine, one cold CPU sample generated 4.68 seconds of EMA speech in 2.92 seconds and 5.40 seconds of Antalia speech in 3.02 seconds, including Python/model startup. The worker process tree peaked at 335.3/349.5 MiB working set and 1185.7/1139.1 MiB private committed memory respectively. The process exited afterward; this excludes the desktop host and is not a performance guarantee. The publisher's GPU speed claims are not CPU measurements.

Antalia's published browser ONNX example is version 1.0.0; the installed Python model is 1.0.1. A lower-memory ONNX adapter needs output/normalization parity checks before replacing this tested path. It is not enabled in this preview.

## Licenses and checks

MorseCore and its Turkish adapter remain Apache-2.0. EMA and Antalia code/weights are Apache-2.0. The separately installed [Piper engine](https://github.com/OHF-Voice/piper1-gpl/tree/v1.3.0) is GPL-3.0; it is invoked as a separate CLI rather than linked into the MorseCore library. Runtime dependencies retain their own licenses.

Piper model cards identify CC0 datasets for Thorsten, Kerstin, Dmitri, Denis and Joe. Kareem's card points to an upstream dataset repository without an explicit license in the card. Its distribution permission needs clarification before bundling it in a public release. Installing this development pack does not relicense third-party engines, models or datasets as Apache-2.0. See the pinned model cards under `speech-runtime/licenses/` and the [upstream voice repository](https://huggingface.co/rhasspy/piper-voices).

```powershell
pwsh -File scripts/test-speech.ps1
pwsh -File scripts/test-desktop.ps1
```

The speech check runs actual synthesis for every installed catalog voice and checks output, input limits, missing packs and cancellation. The GUI check tests layout in five languages and, when EMA is installed, real synthesis through muted MediaPlayer playback. Listening quality and physical speaker output need a separate listening check.
