# MorseCore

<p align="center">
  <b>A console-based Morse encoder and decoder written in C</b>
</p>

<p align="center">
  <code>C</code> · <code>CMake</code> · <code>File I/O</code> · <code>2D Arrays</code> · <code>Modular Programming</code>
</p>

---

## Overview

**MorseCore** is a console-based C project designed to convert plain text into Morse code and convert Morse code back into readable text.

The project was developed as a programming assignment to demonstrate fundamental C programming concepts, including arrays, strings, functions, file operations, modular source structure, input validation, and menu-based program control.

This project is intentionally kept simple and educational. Its main purpose is not to compete with existing Morse tools, but to show clean usage of core C programming structures in a practical application.

---

## Features

- Text to Morse conversion
- Morse to Text conversion
- File-based Text to Morse conversion
- File-based Morse to Text conversion
- 2D array-based Morse code table
- Turkish character normalization
- Automatic output file naming
- File preview before saving output
- Input validation
- Safe menu input handling
- Colored console interface
- Morse table display
- Project information screen
- Modular file structure

---

## Supported Characters

MorseCore supports the following input characters:

| Character Type | Support |
|---|---|
| English letters | `A-Z` |
| Numbers | `0-9` |
| Spaces | Supported |
| Turkish letters | Converted to English equivalents |
| Special symbols | Not supported |

### Turkish Character Normalization

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

---

## Morse Format

MorseCore uses the following Morse formatting rules:

| Element | Format |
|---|---|
| Letter separation | One space |
| Word separation | `/` |
| Supported symbols | `.` and `-` |

Example:

```txt
HELLO WORLD
```

Output:

```txt
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

---

## Menu Options

When the program starts, the following menu is displayed:

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
| [0] Exit                                          |
+---------------------------------------------------+
```

---

## Project Structure

```txt
MorseCore/
├── CMakeLists.txt
├── README.md
├── LICENSE
├── include/
│   ├── file_utils.h
│   ├── morse.h
│   └── ui.h
├── src/
│   ├── file_utils.c
│   ├── main.c
│   ├── morse.c
│   └── ui.c
└── data/
    └── .gitkeep
```

---

## Source File Responsibilities

| File | Responsibility |
|---|---|
| `main.c` | Program entry point and console encoding setup |
| `ui.c` | Console interface, menu system, previews, and user interaction |
| `morse.c` | Morse encoding, Morse decoding, Turkish normalization, and Morse table |
| `file_utils.c` | File reading, file writing, file existence checking, and automatic file naming |
| `ui.h` | UI function declarations |
| `morse.h` | Morse function declarations |
| `file_utils.h` | File utility function declarations |

---

## Used C Concepts

This project demonstrates the following C programming concepts:

| Concept | Usage in Project |
|---|---|
| 1D arrays | Character table |
| 2D arrays | Morse code table |
| Character arrays | Text and Morse buffers |
| Strings | Input parsing and conversion |
| Functions | Separate conversion, UI, and file operations |
| Header files | Function declarations |
| Source file separation | Modular project structure |
| `switch-case` | Menu control |
| Loops | Character scanning and table search |
| Conditional statements | Validation and conversion decisions |
| File operations | Reading from and writing to files |
| `FILE` pointer | File handling |
| Input validation | Empty input and invalid character checks |
| CMake | Build configuration |

---

## 2D Array Usage

The Morse codes are stored in a 2D character array:

```c
char morseCodes[36][6];
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

This allows the program to search a character and retrieve the corresponding Morse code using the same array index.

---

## File Operations

The program uses the `data` folder for runtime file operations.

For file-based conversion, the user enters the file name manually.

Example:

```txt
Enter input file name from data folder: input.txt
```

The program then reads:

```txt
data/input.txt
```

Generated output files are automatically numbered to prevent overwriting previous results.

Example output files:

```txt
morse_output_1.txt
morse_output_2.txt
morse_output_3.txt

decoded_output_1.txt
decoded_output_2.txt
decoded_output_3.txt
```

Runtime `.txt` files inside the `data` folder are ignored by Git.

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
Çağrı Şifre Çözümü
```

Processed as:

```txt
CAGRI SIFRE COZUMU
```

Morse output:

```txt
-.-. .- --. .-. .. / ... .. ..-. .-. . / -.-. --- --.. ..- -- ..-
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

After building the project, run the generated executable.

### Windows

```bash
cmake-build-debug\MorseCore.exe
```

---

## Notes

- The program is designed for console usage.
- Turkish characters are normalized before Morse conversion.
- Special symbols are intentionally not supported in the current version.
- File outputs are automatically numbered.
- Runtime `.txt` files are excluded from Git tracking.

---

## License

This project is licensed under the MIT License.
