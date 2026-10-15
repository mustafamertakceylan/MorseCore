<div align="center">

# MorseCore

<img src="apps/MorseCore.Desktop/MorseCore.Desktop/Assets/Brand/morsecore.png" width="92" alt="MorseCore logo">

**Convert, listen, and practice Morse code.**

![C23](https://img.shields.io/badge/C-23-225E85?style=flat-square&logo=c&logoColor=white)
[![Version](https://img.shields.io/badge/Version-0.5.0-5e6a42?style=flat-square)](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.5.0)
[![License](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

**English** · [Türkçe](README.tr.md)

</div>

MorseCore converts text and files to Morse code and back. A Windows desktop app powered by a C23 engine.

## Download

**[Download Windows Setup — v0.5.0](https://github.com/mustafamertakceylan/MorseCore/releases/download/v0.5.0/MorseCore-0.5.0-Windows-x64-Setup.exe)** · [Portable ZIP](https://github.com/mustafamertakceylan/MorseCore/releases/download/v0.5.0/MorseCore-0.5.0-Windows-x64-Desktop.zip)

Run Setup and open MorseCore from the Start menu. For the portable ZIP, extract all files and open **MorseCore.Desktop.exe**. This is an unsigned preview; macOS and Linux desktop editions are planned.

For optional speech, use **Set up offline voices** in the Start menu or **setup-speech.cmd** in the ZIP. Python 3.11–3.13 and internet are needed once; speech then works offline. Models download separately. [Speech setup →](docs/offline-speech.md)

Checksums and release notes are on the [release page](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.5.0).

## Features

- Convert messages and `.txt` files, individually or in batches.
- Listen to Morse, export WAV audio, and practice reading or listening.
- Convert Latin/Turkish, Russian, and Arabic text in the desktop app.
- Choose light/dark themes and Turkish, English, German, Russian, or Arabic menus.
- Read text aloud offline: EMA Lightning and Antalia-2 Mini for Turkish; Piper for the other four languages.

Version **0.5.0** adds station profiles, CW preparation, separate practice/QSO logs and a radio glossary. Country guides use dated sources; Russian power matching is unavailable. [Radio guide →](docs/amateur-radio.md)

## Build and run

Use **PowerShell 7**, **.NET 10**, Windows SDK/WinUI tools, **CMake 3.28+**, **GCC 15+** and **Ninja**. See the [desktop guide](docs/desktop.md#run-from-source) for details.

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
pwsh -File scripts/run-desktop.ps1
```

## Guides

- [Desktop app and building the GUI](docs/desktop.md)
- [Windows Setup: installation and building](docs/windows-setup.md)
- [Offline speech and voice models](docs/offline-speech.md)
- [C API and .NET integration](docs/native-api.md)
- [Changelog](CHANGELOG.md)

## Thanks and license

Thank you to **Canberk Aslan** for [EMA Lightning](https://github.com/canberk7/ema-lightning) and **PatientDesk AI** for [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini), which provide MorseCore's Turkish speech voices. More credits are available in **Settings → Thanks to open source**.

MorseCore is licensed under [Apache 2.0](LICENSE). See [NOTICE](NOTICE) for attribution. Third-party components and models retain their own licenses; original speech-engine licenses and Antalia's notice are preserved in [third_party/speech](third_party/speech/README.md).
