<div align="center">

# MorseCore

**Convert, listen, and practice Morse code.**

Turn text and files into Morse code, decode messages, export audio, and practice reading or listening. Use the console menu or run conversions from scripts.

![C17](https://img.shields.io/badge/C-17-225E85?style=flat-square&logo=c&logoColor=white)
![CMake](https://img.shields.io/badge/CMake-3.28%2B-064F8C?style=flat-square&logo=cmake&logoColor=white)
[![License](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

**English** · [Türkçe](README.tr.md)

</div>

---

## Contents

- [What it does](#what-it-does)
- [Features](#features)
- [Build and run](#build-and-run)
- [Console menu](#console-menu)
- [Command line](#command-line)
- [Audio and WAV](#audio-and-wav)
- [Practice](#practice)
- [Files and settings](#files-and-settings)
- [Supported input](#supported-input)
- [Statistics](#statistics)
- [Portable package](#portable-package)
- [Tests and automation](#tests-and-automation)
- [Project layout](#project-layout)
- [License](#license)

## What it does

MorseCore converts letters, digits, and supported punctuation into dots and dashes, then turns Morse code back into readable text. It runs locally and keeps your interface language and word separator preferences between sessions.

For example, `HELLO WORLD` becomes:

```text
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Decoding returns `HELLO WORLD`. Turkish letters are normalized to their Latin equivalents; their original spelling cannot be recovered from Morse code.

## Features

| Feature | What you can do |
|---|---|
| Text and files | Convert typed messages, standard input, or `.txt` files in either direction |
| Command line | Automate individual conversions or process a folder of text files |
| Audio | Play Morse on Windows or export a PCM WAV file |
| Practice | Answer random letters, digits, or short words by reading or listening |
| Interface | Use English or Turkish menus, a Morse table, and conversion statistics |
| Portable Windows package | Move the installed folder without rebuilding |

## Build and run

Requirements: **CMake 3.28+**, **GCC with C17 support**, and **Ninja** for the included presets. Python 3 is needed for the integration tests, not for running the application. Make sure the build tools are available in your terminal.

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
cmake --preset debug
cmake --build --preset debug
.\run.cmd
```

On Windows, `run.cmd` also opens by double-click after building. From the source checkout, it uses the checkout root for `data/` and `config/`. It looks for a packaged executable first, then a debug build, then a release build.

Local Windows portability checks cover moved installations, spaces, Unicode names, and long paths. Linux build and test automation is configured; its remote execution is described under [Tests and automation](#tests-and-automation).

## Console menu

Run without arguments to open the menu. Type a number and press Enter:

```text
1  Text to Morse
2  Morse to text
3  Text file to Morse
4  Morse file to text
5  Show Morse table
6  About MorseCore
7  Settings
8  Morse audio / WAV
9  Morse practice
0  Exit
```

Choose **1**, enter `HELLO WORLD`, and view the result and statistics. Enter `y` to save the Morse output or `n` to continue. Choose **2** to decode Morse; this option displays the text without a save prompt.

Each Morse letter is a separate group, separated by whitespace. A word separator must stand alone, as in `... --- ... / .-`. Decoding detects `/`, `|`, `#`, or `*` in the input even if your saved preference is different.

## Command line

The following PowerShell examples run from the source checkout:

```powershell
.\run.cmd --help
.\run.cmd --version
.\run.cmd --encode "HELLO WORLD"
.\run.cmd --decode ".... . .-.. .-.. --- / .-- --- .-. .-.. -.."
.\run.cmd --encode "Merhaba Dünya" --separator "|"
.\run.cmd --encode --input "message.txt" --output "message.morse.txt"
.\run.cmd --decode --input "message.morse.txt" --output "decoded.txt"
"SOS" | .\run.cmd --encode --input -
```

A conversion accepts one quoted text argument, `--input FILE`, or standard input until EOF. `--input -` explicitly selects standard input. Without `--output`, only the result is written to standard output; diagnostics go to standard error. CLI paths are ordinary paths, relative to your current working directory or absolute, rather than filenames restricted to `data/`.

The CLI separator defaults to `/`; it does not read the saved menu preference. Use `--separator` to choose another encoding separator. Decoding detects supported separators automatically.

### A folder of files

```powershell
.\run.cmd --batch "input" --encode --output-dir "encoded"
.\run.cmd --batch "encoded" --decode --output-dir "decoded"
```

Batch mode reads regular `.txt` files in filename order, without entering subfolders. `message.txt` produces `message.morse.txt` when encoding or `message.text.txt` when decoding. Input and output directories must differ. The output directory can be created if its parent exists; the source directory must already exist.

Existing text and WAV outputs are protected by default, including batch outputs. Add **`--overwrite`** only when you intend to replace them. Input files cannot be used as their own output. Batch progress and the final success/failure counts go to standard error; a failed file makes the command return a failure status.

Exit codes: `0` success, `1` conversion/file/audio error, `2` invalid arguments.

## Audio and WAV

Open menu **8**, enter plain text, then choose speed and tone. Press Enter to keep a default. Select **1** to play, **2** to save WAV, or **3** to save and play. Menu exports use numbered `morse_audio_*.wav` files in `data/`.

```powershell
.\run.cmd --encode "SOS" --wav "sos.wav"
.\run.cmd --encode "SOS" --play --wpm 15 --frequency 700
```

| Audio setting | Default | Supported range |
|---|---|---|
| Speed | 20 WPM | 5–60 WPM |
| Tone | 600 Hz | 200–2000 Hz |
| Sample rate | 22050 Hz | 8000–48000 Hz in the audio core; menu and CLI use the default |

WAV output is **16-bit mono PCM**. Timing uses a 1-unit dot, 3-unit dash, and gaps of 1/3/7 units within letters, between letters, and between words. Generation uses streaming buffers, with a maximum duration of **10 minutes** and a maximum Morse input size of **1 MiB** for audio.

Live playback is available on Windows and reports an error if the audio device cannot be opened. WAV generation does not require an audio device. `--wav` and `--play` apply to one `--encode` operation, not batch conversion.

## Practice

Open menu **9** and choose reading Morse or listening, then letters, digits, or short English words. The word set contains 32 ASCII words. Choose 1–100 questions; the default is 10. Listening mode also asks for speed and tone and requires Windows audio playback.

Answer in plain text. ASCII letter case and surrounding whitespace are ignored. Each answer consumes the question and reveals the expected answer.

- Press Enter or type `:skip` to skip.
- Type `:replay` to show or play the current question again.
- Type `:quit` to finish early.

The summary shows correct, incorrect, skipped, and completed questions. Accuracy is `correct / completed × 100`; skipped questions count as completed. Practice results are kept only for the session.

## Files and settings

For console file options **3** and **4**, place files in the active `data/` directory and enter a filename such as `message` or `message.txt`. `.txt` is added when needed. Converted files are automatically saved as numbered `morse_output_*.txt` or `decoded_output_*.txt` files. Existing filenames are skipped.

By default, `data/` and `config/` are created beside the executable. Packaged `run.cmd` uses its own folder; source `run.cmd` uses the checkout root. To choose a separate existing writable root, set **`MORSECORE_HOME`** and launch the executable directly, because `run.cmd` sets this variable itself:

```powershell
$env:MORSECORE_HOME = "C:\MorseCore Data"
.\cmake-build-release\MorseCore.exe
```

Menu **7** saves the word separator and interface language in `config/settings.txt`. Defaults:

```ini
word_separator=/
language=EN
```

Languages are `EN` and `TR`. Turkish menu labels use ASCII spelling for terminal compatibility; Turkish input and Unicode file paths are supported. Colors are used when the terminal supports them. Redirected output has no ANSI escape sequences; setting `NO_COLOR` disables colors.

New runtime `.txt` files under the checkout's `data/` are ignored by Git; existing tracked samples remain tracked.

## Supported input

Letters `A–Z` and `a–z`, digits `0–9`, and ASCII whitespace are accepted. Turkish letters in UTF-8 or the supported Windows-1254 subset are normalized:

```text
Ç/ç → C    Ğ/ğ → G    İ/ı → I
Ö/ö → O    Ş/ş → S    Ü/ü → U

Merhaba Dünya → MERHABA DUNYA
```

An initial UTF-8 BOM is accepted and omitted from conversion and character counts. Other alphabets, emoji, malformed input outside the supported legacy subset, and unknown Morse groups are rejected.

Supported ITU punctuation:

```text
. , ? ' / ( ) : = + - " @
```

The application also supports the common extensions **`! & ; _ $`**; these five are not presented as ITU-standard punctuation. A literal slash in plain text is encoded as `-..-.`; a standalone separator in Morse input marks a word boundary.

Encoding combines whitespace between words into one separator and removes leading/trailing whitespace. Decoding produces uppercase letters. Text input and output grow dynamically, limited by available memory and representable sizes. Allocation failures and size overflows return errors; audio has its own limits above.

## Statistics

Console conversions show input/output character counts, word count, converted characters, and literal spaces. Supported Turkish letters count once. Converted characters include supported punctuation. Word, converted-character, and space counts use the plain-text side: input when encoding, output when decoding.

ASCII whitespace separates words, but only actual space characters contribute to the space count. For example, `Merhaba Dünya` has 13 characters, 12 converted letters, 2 words, and 1 space. The exit summary counts conversions, file operations, invalid inputs, and saved outputs for the current session.

## Portable package

Build, test, and package a release locally:

```powershell
cmake --preset release
cmake --build --preset release
ctest --preset release
cpack --preset release
cmake --install cmake-build-release --prefix "C:\Apps\MorseCore"
```

CPack writes a ZIP inside `cmake-build-release/`; for version 0.1.0 on Windows AMD64, the filename is `MorseCore-0.1.0-Windows-AMD64.zip`. Extract it into a writable folder and run `MorseCore.exe` or `run.cmd`. The install command is an alternative to extracting the ZIP. Keep the installed folder together; it includes the executable, launcher, documentation, license, and default configuration.

## Tests and automation

```powershell
ctest --preset debug
ctest --preset release
```

C tests cover conversion, punctuation, dynamic buffers, audio timing/WAV structure, and practice scoring. Python integration tests cover console and CLI flows, batch output protection, settings, and relocated installations. Without Python, integration tests are not registered. Close the application during tests; tests may temporarily change settings before restoring them.

[CI](.github/workflows/ci.yml) is configured for pushes, pull requests, and manual runs. The shared [build workflow](.github/workflows/build.yml) builds and tests Windows with GCC UCRT64 and Ubuntu 24.04 with GCC. Successful Windows jobs are configured to upload a ZIP and `SHA256SUMS` as `MorseCore-Windows-x64`, retained for 14 days.

The [release workflow](.github/workflows/release.yml) requires a `v<version>` tag matching the CMake project version, currently `v0.1.0`, and successful Windows/Linux checks before publishing a GitHub Release. These workflows have not yet been run remotely; no release is claimed here. Automated audio tests do not prove audible playback on physical hardware.

## Project layout

```text
src/                  Conversion, console UI, CLI, audio, practice, files and settings
include/              Public headers and shared configuration
tests/                C tests and Python integration tests
config/settings.txt   Saved language and word separator
data/                 Console input files, samples, and generated output
.github/workflows/    CI, shared build checks, and release automation
CMakeLists.txt        C17 build, test, install, and ZIP package targets
CMakePresets.json     GCC / Ninja debug and release presets
run.cmd               Windows launcher
```

## License

Copyright © 2026 Mustafa Mert Akceylan. Licensed under the [Apache License 2.0](LICENSE). See [NOTICE](NOTICE) for attribution information.
