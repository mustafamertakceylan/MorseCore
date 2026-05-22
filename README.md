# MorseCore

<p align="center">
  <b>A console-based Morse encoder and decoder written in C</b>
</p>

<p align="center">
  <code>C</code> · <code>CMake</code> · <code>File I/O</code> · <code>2D Arrays</code> · <code>Structs</code> · <code>Pointers</code> · <code>Modular Design</code>
</p>

---

## Overview

**MorseCore** is a console-based Morse encoder and decoder developed in C.

The project converts plain text into Morse code and converts Morse code back into readable text. It also supports file-based conversion, Turkish character normalization, configurable word separators, persistent settings, bilingual interface messages, statistics, input validation, and centralized error handling.

This project is designed as an educational C programming project. Its main goal is to demonstrate fundamental and intermediate C concepts in a clean, modular structure.

---

## Features

- Text to Morse conversion
- Morse to Text conversion
- File-based Text to Morse conversion
- File-based Morse to Text conversion
- Turkish character normalization
- Configurable Morse word separator
- Persistent settings through `config/settings.txt`
- Automatic separator detection while decoding Morse input
- English / Turkish interface language support
- Input validation
- Centralized error handling
- Conversion statistics
- Session summary
- Automatic output file naming
- File preview before saving output
- Colored console interface
- Morse table display
- Modular source/header separation

---

## Supported Characters

MorseCore supports the following character groups:

| Character Type | Support |
|---|---|
| English letters | `A-Z` |
| Numbers | `0-9` |
| Spaces | Supported |
| Turkish letters | Converted to English equivalents |
| Unsupported symbols | Rejected |

---

## Turkish Character Normalization

Turkish characters are converted into their closest English equivalents before Morse conversion.

| Turkish Character | Converted Character |
|---|---|
| `Ç / ç` | `C` |
| `Ğ / ğ` | `G` |
| `İ / ı` | `I` |
| `Ö / ö` | `O` |
| `Ş / ş` | `S` |
| `Ü / ü` | `U` |

Example:

```txt
Merhaba Dünya
```

is processed as:

```txt
MERHABA DUNYA
```

Morse output:

```txt
-- . .-. .... .- -... .- / -.. ..- -. -.-- .-
```

---

## Morse Format

MorseCore uses the following format rules:

| Element | Format |
|---|---|
| Letter separation | One space |
| Word separation | Configurable separator |
| Default word separator | `/` |
| Supported separators | `/`, `|`, `#`, `*` |
| Morse symbols | `.` and `-` |

Example with default separator:

```txt
HELLO WORLD
```

Output:

```txt
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Example with custom separator `|`:

```txt
.... . .-.. .-.. --- | .-- --- .-. .-.. -..
```

---

## Persistent Settings

MorseCore stores user settings in:

```txt
config/settings.txt
```

Default settings:

```txt
word_separator=/
language=EN
```

The program remembers the selected word separator and interface language between runs.

---

## Separator Detection

When decoding Morse input, MorseCore can automatically detect the word separator from the input.

Example:

```txt
.... . .-.. .-.. --- # .-- --- .-. .-.. -..
```

Even if the saved separator is `/`, the program detects `#` and decodes the input as:

```txt
HELLO WORLD
```

If no separator is detected, the saved separator from `config/settings.txt` is used.

---

## Interface Language Support

MorseCore supports two interface languages:

| Language | Code |
|---|---|
| English | `EN` |
| Turkish | `TR` |

The Turkish interface uses ASCII Turkish text for console compatibility.

Example:

```txt
Metni Morse'a Cevir
Morse'u Metne Cevir
Ayarlar
Cikis
```

This avoids terminal encoding problems while preserving Turkish input support for Morse conversion.

---

## Menu Options

Default English menu:

```txt
+---------------------------------------------------+
|                     MorseCore                     |
+---------------------------------------------------+
| [1] Text to Morse                                 |
| [2] Morse to Text                                 |
| [3] Read text from file and convert to Morse      |
| [4] Read Morse from file and convert to Text      |
| [5] Show Morse Table                              |
| [6] About Project                                 |
| [7] Settings                                      |
| [0] Exit                                          |
+---------------------------------------------------+
```

Settings menu:

```txt
+---------------------------------------------------+
|                      Settings                     |
+---------------------------------------------------+
| Current word separator: /                         |
| Current language      : EN                        |
+---------------------------------------------------+
| [1] Change word separator                         |
| [2] Change language                               |
| [0] Back                                          |
+---------------------------------------------------+
```

---

## Project Structure

```txt
MorseCore/
├── CMakeLists.txt
├── README.md
├── LICENSE
├── .gitignore
├── config/
│   └── settings.txt
├── data/
│   └── .gitkeep
├── include/
│   ├── app.h
│   ├── app_config.h
│   ├── constants.h
│   ├── error.h
│   ├── error_codes.h
│   ├── error_messages.h
│   ├── file_utils.h
│   ├── language.h
│   ├── morse.h
│   ├── separator.h
│   ├── settings.h
│   ├── stats.h
│   ├── ui.h
│   └── validation.h
└── src/
    ├── app.c
    ├── error.c
    ├── file_utils.c
    ├── language.c
    ├── main.c
    ├── morse.c
    ├── separator.c
    ├── settings.c
    ├── stats.c
    ├── ui.c
    └── validation.c
```

---

## Source File Responsibilities

| File | Responsibility |
|---|---|
| `main.c` | Program entry point and console encoding setup |
| `app.c` | Main application flow, menu case handling, settings flow |
| `ui.c` | Console output, menus, previews, input prompts, waiting functions |
| `morse.c` | Text/Morse conversion and Turkish character normalization |
| `file_utils.c` | File reading, writing, path building, automatic output file naming |
| `settings.c` | Loading and saving persistent settings |
| `separator.c` | Separator validation and automatic separator detection |
| `language.c` | English/Turkish interface text system |
| `stats.c` | Conversion statistics and session summary |
| `validation.c` | Empty input, file name, and `.txt` extension validation |
| `error.c` | Error code to localized error message conversion |

---

## Used C Concepts

| Concept | Usage in Project |
|---|---|
| 1D arrays | Character table |
| 2D arrays | Morse code table |
| Character arrays | Text, Morse, file paths, and settings |
| Strings | Input processing and conversion |
| Functions | Modular program structure |
| Header files | Function declarations and shared types |
| Source file separation | Clear module responsibility |
| `switch-case` | Main menu and settings menu control |
| Loops | Character scanning and Morse table search |
| Conditional statements | Validation and conversion decisions |
| File operations | Reading/writing text and settings files |
| `FILE *` | File handling |
| `struct` | Conversion statistics, session statistics, app settings |
| Pointers | Updating structs through function parameters |
| `enum` | Language text keys and result/error codes |
| CMake | Build configuration |

---

## Struct Usage

MorseCore uses structs to group related data.

### `ConversionStats`

Used for storing statistics about a single conversion.

```c
typedef struct {
    int inputLength;
    int outputLength;
    int wordCount;
    int convertedCharacterCount;
    int spaceCount;
} ConversionStats;
```

### `SessionStats`

Used for storing program-wide operation counters during a session.

```c
typedef struct {
    int textToMorseCount;
    int morseToTextCount;
    int fileOperationCount;
    int invalidInputCount;
    int savedOutputCount;
} SessionStats;
```

### `AppSettings`

Used for storing persistent application settings.

```c
typedef struct {
    char wordSeparator;
    char language[3];
} AppSettings;
```

---

## Pointer Usage

Pointers are used to update structs through function parameters.

Example:

```c
initializeSessionStats(&sessionStats);
calculateConversionStats(text, output, &conversionStats);
printSessionSummary(&sessionStats, settings.language);
```

This avoids unnecessary copying and allows functions to directly update the original structs.

---

## 2D Array Usage

The Morse codes are stored in a 2D character array.

```c
char morseCodes[MORSE_COUNT][MAX_MORSE_LENGTH];
```

This structure is used because each Morse code is stored as a string.

```txt
36 entries  ->  26 letters + 10 numbers
6 columns   ->  maximum 5 Morse symbols + '\0'
```

The character table and Morse table share the same index.

Example:

```txt
characters[0] = 'A'
morseCodes[0] = ".-"

characters[1] = 'B'
morseCodes[1] = "-..."
```

---

## File Operations

The program uses the `data` folder for runtime file operations.

For file-based conversion, the user enters the file name manually.

Example:

```txt
input
```

The program automatically converts it to:

```txt
input.txt
```

and reads:

```txt
data/input.txt
```

Generated output files are automatically numbered to prevent overwriting existing files.

Example:

```txt
morse_output_1.txt
morse_output_2.txt
decoded_output_1.txt
decoded_output_2.txt
```

Runtime `.txt` files inside `data/` are ignored by Git.

---

## Example Usage

### Text to Morse

Input:

```txt
HELLO WORLD
```

Output:

```txt
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

---

### Morse to Text

Input:

```txt
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Output:

```txt
HELLO WORLD
```

---

### Turkish Input

Input:

```txt
Merhaba Dünya
```

Processed as:

```txt
MERHABA DUNYA
```

Output:

```txt
-- . .-. .... .- -... .- / -.. ..- -. -.-- .-
```

---

## Build Instructions

This project uses CMake.

### Configure

```bash
cmake -S . -B cmake-build-debug
```

### Build

```bash
cmake --build cmake-build-debug
```

---

## Run Instructions

After building, run the generated executable.

### Windows

```bash
cmake-build-debug\MorseCore.exe
```

---

## Notes

- The project is designed for console usage.
- Turkish input characters are normalized before Morse conversion.
- Turkish interface text uses ASCII Turkish for terminal compatibility.
- The default word separator is `/`.
- Word separator and language settings are persistent.
- File outputs are automatically numbered.
- Runtime `.txt` files inside `data/` are excluded from Git tracking.
- The project is intentionally modular for readability and future expansion.

---

## Future Work

Possible future improvements:

- Punctuation support
- Batch file conversion
- More detailed test mode
- Exporting session logs
- WAV-based Morse audio analysis
- Real-time microphone Morse decoding
- GUI frontend

---

## License

This project is licensed under the MIT License.