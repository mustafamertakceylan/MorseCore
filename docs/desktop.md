# MorseCore desktop

[Türkçe](desktop.tr.md) · [Home](../README.md)

The Windows interface brings text and file conversion, Morse audio, speech and practice together. This is a development preview for 0.4.0. No signed installer has been released.

## Interface

- RAW Akademi's exact light palette: olive `#5E6A42`, cream `#FAF8F2`, white surfaces `#FFFFFF`, ink `#2C2A22` and borders `#E7E4DF`. Dark mode uses the same olive for the sidebar/title bar, lighter green surfaces and cream action buttons.
- An application-drawn title bar and close button; a native Windows caption region moves while the mouse button is held and stops on release. The logo and app name are centered in the window. Window dimensions are locked; resize, maximize and minimize are blocked. Startup size is clamped to the display work area.
- The MorseCore logo appears in the title bar, sidebar, loading screen and application icon. **System / Light / Dark** theme selection lives in Settings.
- Atkinson Hyperlegible body text, Crimson Pro headings and monospace Morse editors.
- A collapsible sidebar for conversion, files, audio, practice and the alphabet. Settings stay at the bottom. Collapsed icons and the menu toggle share one center line, including the Arabic layout.
- Short page and menu transitions. Windows animation preferences and **Reduce motion** are respected; no continuously running idle effects.
- Turkish, English, German, Russian and Arabic. Arabic mirrors the sidebar to the right while Morse fields remain left to right.
- A compact converter with clipboard/export actions, Morse playback and offline speech on the same screen; `Ctrl+Enter` converts.

Settings include an expandable **Thanks to open source** section with authors, license labels and source links for EMA Lightning, Antalia Mini, Piper, normalizer-tr, PyTorch, ONNX Runtime and the voice models. Engine and model licenses remain separate.

Interface language, conversion alphabet and speech language are independent. Choose an alphabet when decoding: dots and dashes do not carry an alphabet identifier.

## Run from source

Development requires Windows x64, .NET 10 SDK, Windows SDK 10.0.26100, WinUI build tools, PowerShell 7 and the [C23 toolchain](../README.md#build-and-run). NuGet dependencies download on the first build. `global.json` pins the Uno SDK.

```powershell
pwsh -File scripts/run-desktop.ps1
# Or double-click run-gui.cmd.
pwsh -File scripts/run-desktop.ps1 -Configuration Release
```

The UI calls the native C23 library. Desktop preferences live in `%LOCALAPPDATA%/MorseCore/Desktop/settings.json`. `MORSECORE_DESKTOP_HOME` can select another absolute directory. Messages are not saved in preferences.

## Alphabets and counters

| Profile | Behavior |
|---|---|
| Latin / Turkish | Existing Latin table; Turkish letters normalize to Latin equivalents |
| Russian | Cyrillic letters; lowercase becomes uppercase and `Ё/ё` normalizes to `Е` |
| Arabic | 28 basic letters plus standalone hamza; vowel marks and tatweel are omitted |

Arabic normalization is `آ أ إ ٱ → ا`, `ؤ → و`, `ئ ى → ي`, `ة → ه`. Arabic-Indic digits become `0…9`; Arabic comma, semicolon and question mark become their Latin equivalents. Normalization is lossy: original spelling cannot be reconstructed. Mixed letters from other alphabets, emoji, unsupported symbols and decomposed Unicode sequences are rejected rather than silently dropped.

Russian/Arabic base mappings use [Signal Corps TM 11-459, Appendix VII, printed pp. 71–72](https://www.govinfo.gov/content/pkg/GOVPUB-W-824b48e17943859ed2af2b45952379df/pdf/GOVPUB-W-824b48e17943859ed2af2b45952379df.pdf). They are separate profiles from the [ITU Latin table](https://www.itu.int/rec/R-REC-M.1677-1-200910-I), with shared digits and punctuation. The alphabet page exposes the mappings.

Character lengths count Unicode scalar values, not bytes or UTF-16 code units; a leading BOM is excluded. Arabic marks count toward input length but not converted symbols. Words are separated by whitespace. This is not a general Unicode grapheme counter.

## Files and sound

File conversion handles UTF-8 `.txt` files sequentially, up to 8 MiB per file. A temporary output is published atomically without replacing an existing file. Folder, alphabet and direction are captured when the batch starts. Native conversion itself is synchronous; cancellation is checked between files/before publishing.

Morse audio supports Windows playback and WAV export, 5–60 WPM and 200–2000 Hz. Practice uses ten-question sessions of Latin letters, digits or short words, retaining the session across navigation and theme changes.

Speech uses [offline neural models](offline-speech.md): EMA Lightning and Antalia Mini for Turkish, Piper for Arabic, German, Russian and English. Install the optional pack once with `setup-speech.cmd`; choose voices directly in the converter. Windows speech packs are no longer used. Synthesis starts on demand in a separate process that exits when finished. Speech is limited to 4000 characters. Small weights do not imply small active runtime memory; measured figures and separate model licenses are documented in the speech guide.

## Windows Setup

An unsigned per-user **Setup.exe** can be built with `pwsh -File scripts/package-desktop-installer.ps1`. It includes Start menu shortcuts and Windows uninstall registration. Optional speech models remain outside the application folder. See the [installation, build and verification guide](windows-setup.md).

## Updates and MSIX

The loading screen prepares preferences and the native engine. Network checks do not hold the main interface open. Automatic checks query the latest stable GitHub Release at most once every six hours, excluding drafts, prereleases and older versions. Settings provide manual checks and an opt-out. The application check does not download or execute binaries.

```powershell
# Verification only; no installation or certificate creation.
pwsh -File scripts/package-desktop.ps1 -Unsigned

# Requires a matching certificate with a private key.
pwsh -File scripts/package-desktop.ps1 -CertificateThumbprint <40-character-thumbprint>
```

The script writes `MorseCore.msix`, `MorseCore.appinstaller` and `SHA256SUMS` into a unique ignored folder under `cmake-build-desktop-package/`. Unsigned packages cannot be distributed as normal trusted installers. The script does not install anything or add certificate trust.

The current package publisher is `O=Mustafa Mert Akceylan`, which must exactly match the signing certificate. A different production certificate requires updating the publisher and its validation together. Trusted signing and release publishing are still pending.

Automatic installation/update requires publishing signed MSIX and App Installer assets under their fixed GitHub Release names, and installing via `.appinstaller`. Commits alone do not create an update. Test a real installation and an upgrade between two signed versions before release. [App Installer documentation](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview).

## Validation and performance

```powershell
cmake --preset release -DMORSECORE_BUILD_DOTNET_BINDINGS=ON
cmake --build --preset release
ctest --preset release
pwsh -File scripts/test-desktop.ps1 -MouseInput
```

The GUI check uses isolated preferences and saves screenshots/JSON under `cmake-build-desktop-tests/`. It exercises native conversion, counters, themes, RTL, menu transitions, practice buttons, the offline voice catalog and speech action visibility in five languages. When EMA is installed, it synthesizes speech and completes muted MediaPlayer playback. It does not emit audible sound. Operating system picker dialogs and physical audio output require separate QA.

`-MouseInput` sends mouse input to the test window to check press–drag–release, a single click and a second drag. Input stops if another application's window takes the foreground. The JSON report records whether mouse tests actually ran.

Only the active page's control tree is retained. Conversion, file and audio work stays off the UI thread; files are not processed concurrently in memory. Windows publishing currently disables trimming because the WinRT dependencies failed at startup in a trimmed package. AOT/trimming optimizations require a separately measured and tested deployment profile.

## Platform roadmap

Windows uses native WinUI; Uno's desktop target shares the same XAML screens with the future macOS/Linux editions. [Uno platform support](https://platform.uno/docs/articles/getting-started/requirements.html).

1. Windows design feedback, large-file/memory measurements, audio and picker QA.
2. Trusted MSIX signing and actual installation/update verification.
3. macOS: C23 `.dylib`, native audio/speech/pickers, device testing and signed distribution.
4. Linux: C23 `.so`, native audio/speech/pickers, desktop accessibility and distribution packages.

A successful shared-desktop build on Windows does not establish that the GUI works on a Mac or Linux machine. GUI execution and packaging on those platforms remain future work.
