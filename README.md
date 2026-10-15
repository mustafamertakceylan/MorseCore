<div align="center">

# MorseCore

<img src="apps/MorseCore.Desktop/MorseCore.Desktop/Assets/Brand/morsecore.png" width="92" alt="MorseCore logo">

**Convert, listen, and practice Morse code.**

![C23](https://img.shields.io/badge/C-23-225E85?style=flat-square&logo=c&logoColor=white)
[![Version](https://img.shields.io/badge/Version-0.3.0-5e6a42?style=flat-square)](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.3.0)
[![License](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

**English** · [Türkçe](README.tr.md)

</div>

MorseCore converts text and files to Morse code and back. Use the Windows desktop app, the console menu, or the command line.

## Download

**[Download for Windows x64 — v0.3.0](https://github.com/mustafamertakceylan/MorseCore/releases/download/v0.3.0/MorseCore-0.3.0-Windows-x64-Desktop.zip)**

Extract the whole ZIP and open **MorseCore.Desktop.exe**. Keep its DLLs and subfolders together. This is an unsigned desktop preview; macOS and Linux desktop editions are planned.

For optional speech, run **setup-speech.cmd** with Python 3.11–3.13. Setup downloads models once; speech then works offline. Models are not included in the ZIP. [Speech setup →](docs/offline-speech.md)

The console app and native DLL are available on the [release page](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.3.0), alongside checksums and release notes.

## Features

- Convert messages and `.txt` files, individually or in batches.
- Listen to Morse, export WAV audio, and practice reading or listening.
- Convert Latin/Turkish, Russian, and Arabic text in the desktop app.
- Choose light/dark themes and Turkish, English, German, Russian, or Arabic menus.
- Read text aloud offline: EMA Lightning and Antalia-2 Mini for Turkish; Piper for the other four languages.

## Build and run

The C23 core and console need **CMake 3.28+**, **GCC 15+**, and **Ninja**. Clang 18+ is supported through the `clang-debug` preset.

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
cmake --preset debug
cmake --build --preset debug
.\run.cmd
```

For command-line use, try `.\run.cmd --encode "HELLO WORLD"`; see `.\run.cmd --help` for all options. The console supports Latin/Turkish input.

## Guides

- [Desktop app and building the GUI](docs/desktop.md)
- [Offline speech and voice models](docs/offline-speech.md)
- [C API and .NET integration](docs/native-api.md)
- [Changelog](CHANGELOG.md)

## Thanks and license

Thank you to **Canberk Aslan** for [EMA Lightning](https://github.com/canberk7/ema-lightning) and **PatientDesk AI** for [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini), which provide MorseCore's Turkish speech voices. More credits are available in **Settings → Thanks to open source**.

MorseCore is licensed under [Apache 2.0](LICENSE). See [NOTICE](NOTICE) for attribution. Third-party components and models retain their own licenses; original speech-engine licenses and Antalia's notice are preserved in [third_party/speech](third_party/speech/README.md).
