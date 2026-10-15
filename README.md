<div align="center">

# MorseCore

**Text and Morse code, in both directions.**

Convert a message in your terminal or read it from a text file. View the Morse alphabet, save results, and keep your language and word separator preferences between runs.

![C17](https://img.shields.io/badge/C-17-225E85?style=flat-square&logo=c&logoColor=white)
![CMake](https://img.shields.io/badge/CMake-3.28%2B-064F8C?style=flat-square&logo=cmake&logoColor=white)
[![License](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

**English** · [Türkçe](README.tr.md)

</div>

---

## Contents

- [What it does](#what-it-does)
- [Features](#features)
- [Getting started](#getting-started)
- [Using MorseCore](#using-morsecore)
- [Files](#files)
- [Settings](#settings)
- [Supported input](#supported-input)
- [Statistics](#statistics)
- [Project layout](#project-layout)
- [Tests](#tests)
- [License](#license)

## What it does

MorseCore converts letters and numbers into dots and dashes, and turns Morse code back into readable uppercase text. It runs locally through a numbered console menu.

For example, `HELLO WORLD` becomes:

```text
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Choose the reverse conversion to get `HELLO WORLD` back.

## Features

- Convert typed text or `.txt` files in either direction.
- Accept Turkish letters by converting them to their Latin equivalents.
- Choose `/`, `|`, `#`, or `*` between Morse words; detect the separator when decoding.
- Switch between English and Turkish menus and save your preferences.
- See conversion statistics and a summary of the current session.
- Browse the 36-entry Morse table for letters and digits.
- Use a colored console interface with an ASCII `MORSE CORE` banner.

## Getting started

### Requirements

| Tool | Needed for |
|---|---|
| CMake 3.28 or newer | Configuring and building the project |
| GCC with C17 support | The included `debug` preset |
| Ninja | The included `debug` preset |
| Python 3 | CLI integration tests; not needed to run the application |

The build and tests have been checked on Windows with MSYS2 UCRT64 GCC. Other platforms have not been verified.

### Build and run

Clone the repository, then run these commands from its root directory. Make sure `cmake`, `gcc`, and `ninja` are available in your terminal.

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
cmake --preset debug
cmake --build --preset debug
.\run.cmd
```

After building, you can also double-click `run.cmd` on Windows or start the executable directly:

```powershell
.\cmake-build-debug\MorseCore.exe
```

The build stores this checkout's absolute path for `data/` and `config/`, so launching from another working directory still uses the same files. If you move the checkout, configure and build it again. The executable alone is not a standalone distribution.

## Using MorseCore

Type a menu number and press Enter:

```text
1  Text to Morse
2  Morse to text
3  Text file to Morse
4  Morse file to text
5  Show Morse table
6  About MorseCore
7  Settings
0  Exit
```

### Convert a message

Choose **1**, enter `HELLO WORLD`, and press Enter. The application shows the result and its statistics, then asks whether to save the Morse output. Enter `y` to save or `n` to continue without saving.

Choose **2** to decode this message:

```text
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Each letter is a separate Morse group, separated by whitespace. A word separator must be a group of its own, as in `... --- ... / .-`. Decoding detects the supported separator in the message, even if your saved preference is different. Interactive decoding displays the text without a save prompt; use option **4** to save decoded file output.

## Files

Place input files in `data/`. For a file named `message.txt`, choose **3** or **4** and enter `message` or `message.txt`. The application adds `.txt` when needed; enter a filename rather than a full path.

File conversions display the input and output, then automatically save a numbered result in the same directory:

```text
data/
  message.txt
  morse_output_1.txt
  morse_output_2.txt
  decoded_output_1.txt
```

Numbering skips existing filenames to avoid overwriting them. Saving an interactively encoded message also uses a numbered `morse_output_*.txt` file.

New runtime `.txt` files in `data/` are ignored by Git. Sample files already tracked in the repository remain tracked.

## Settings

Open **7 → Settings** to change the word separator or interface language. Changes are saved in `config/settings.txt` and reused on the next launch.

The defaults are:

```ini
word_separator=/
language=EN
```

Supported languages are `EN` and `TR`. Turkish menu labels use ASCII spelling for terminal compatibility; Turkish input letters are still accepted.

Colors are enabled when the terminal supports them. Redirected output contains no ANSI escape sequences. To disable colors in PowerShell:

```powershell
$env:NO_COLOR = "1"
.\run.cmd
```

## Supported input

Plain text accepts `A–Z`, `a–z`, digits `0–9`, spaces, tabs, and line breaks. Turkish letters are accepted in UTF-8 or Windows-1254:

```text
Ç/ç → C    Ğ/ğ → G    İ/ı → I
Ö/ö → O    Ş/ş → S    Ü/ü → U

Merhaba Dünya → MERHABA DUNYA
```

Decoding produces uppercase Latin letters and cannot restore the original Turkish spelling. Encoding combines whitespace between words into one separator and discards leading and trailing whitespace.

Input is limited to **999 bytes**, including file content; a UTF-8 Turkish letter takes two bytes. Longer input is rejected rather than partially converted. Punctuation and other unsupported characters are rejected. Morse input uses `.` and `-` for letters and one of `/`, `|`, `#`, or `*` between words.

The application currently provides text conversion only; audio playback, microphone decoding, a graphical interface, and command-line conversion arguments are not implemented.

## Statistics

After a successful conversion, MorseCore shows the input and output character counts, word count, converted letters and digits, and literal spaces. A supported Turkish letter counts as one character rather than two UTF-8 bytes.

Words, converted characters, and spaces are counted on the plain-text side: the input when encoding, the output when decoding. Tabs and line breaks separate words but do not count as literal spaces. For example, `Merhaba Dünya` has 13 characters, 12 letters, 2 words, and 1 space.

The exit summary reports conversions, file operations, invalid inputs, and saved outputs for the current session. Session statistics are not saved between runs.

## Project layout

```text
src/                 Application flow, conversion, console UI, files, and settings
include/             Public headers and shared configuration
tests/               Core tests and CLI integration scenarios
config/settings.txt  Saved language and separator
data/                Input files, samples, and generated output
CMakeLists.txt       Build and test targets
CMakePresets.json    GCC / Ninja debug preset
run.cmd              Windows launcher for the built application
```

## Tests

After configuring and building, run:

```powershell
ctest --preset debug
```

CTest runs the C core tests and, when Python 3 is found during configuration, a CLI suite with **12 scenarios** against the real executable. Without Python, the CLI suite is omitted; the core tests still run.

Coverage includes conversions, Turkish encodings, all four separators, statistics, input and output limits, file operations, persistent settings, invalid input, and end-of-input handling. The CLI suite restores settings, removes its generated files, and checks that existing sample files remain unchanged. Close the application before running tests because the CLI suite temporarily changes settings.

## License

Copyright © 2026 Mustafa Mert Akceylan. Licensed under the [Apache License 2.0](LICENSE). See [NOTICE](NOTICE) for attribution information.
