# MorseCore desktop

[Türkçe](desktop.tr.md) · [Home](../README.md)

The Windows interface brings text and file conversion, Morse audio, speech, learning and radio contact preparation together. [Download the Windows preview](https://github.com/mustafamertakceylan/MorseCore/releases/latest); Setup is currently unsigned.

## Interface

- RAW-inspired olive `#5E6A42` and cream `#FAF8F2`, warm cards, white inputs and lightly tinted results. Dark mode uses green surfaces and cream action buttons. Four slow-moving corners reuse RAW's wave shapes and gold lines; cards keep their solid background for readability.
- An application-drawn title bar with centered branding, minimize and close controls. The window opens at 1440×900 pixels, clamped to the display work area; resizing and maximizing remain disabled. Native Windows caption regions provide dragging.
- The MorseCore logo appears in the title bar, loading screen and application icon. **System / Light / Dark** theme selection lives in Settings. Navigation stays hidden until the welcome screen finishes.
- Atkinson Hyperlegible body text, Crimson Pro headings and monospace Morse editors.
- A collapsible sidebar with its menu button at the top left of the window. Learning and Amateur radio have subpages; Settings stays at the bottom. Form columns adapt to the available width.
- Short page/menu transitions and subtle background motion. Windows animation preferences and **Reduce motion** are respected. Background animation stops while the window is inactive.
- Turkish, English, German, Russian and Arabic. Arabic mirrors the sidebar to the right while Morse fields remain left to right.
- A compact converter with clipboard/export actions; `Ctrl+Enter` converts. Morse playback and offline speech live on Audio.

Settings include an expandable **Thanks to open source** section with authors, license labels and source links for EMA Lightning, Antalia Mini, Piper, normalizer-tr, PyTorch, ONNX Runtime and the voice models. Engine and model licenses remain separate.

Interface language, conversion alphabet and speech language are independent. Choose an alphabet when decoding: dots and dashes do not carry an alphabet identifier.

## New in 0.7.0

Amateur radio now opens **My stations**, **Prepare a contact** and **Contact log** from the sidebar. The map has a direct button in the log. Country guides use an infographic that preserves exact frequency limits, power units, licence-class requirements and special conditions. Source coverage and review dates are unchanged; the guide does not grant operating permission.

The [English](../README.md), [Turkish](../README.tr.md), [German](../README.de.md), [Russian](../README.ru.md) and [Arabic](../README.ar.md) READMEs include screenshots in their own language.

## Added in 0.6.0

These features are included in the v0.6.0 Windows installer and portable ZIP.

- **Learning → Sending practice:** focus the large key, then hold Space, click or touch to send. Finish to see timing, rhythm, speed and optional reference-text accuracy. Letters use the selected alphabet; the speed target sets the dot/dash thresholds. Leaving the page releases the key and sound.
- **Audio → Decode audio:** select a WAV and decode it locally. Automatic tone/speed detection can be overridden under Advanced. Windows can record up to 30 seconds from a selected audio input and decode when stopped. Files are limited to 64 MiB and two minutes; PCM and IEEE float mono/stereo are supported. This targets a single clean CW tone, not speech or a mixture of stations. `□` marks uncertain characters; real microphone and noisy-radio performance remain unverified.
- **Amateur radio → Log:** search all contacts by call, UTC date or band, edit existing records, or preview a CW ADIF import before confirming it. Exact duplicates are excluded; differing seconds/frequencies remain separate. Imported time precision is preserved. The offline map shows only manually supplied coordinates or recorded Maidenhead cells; a call prefix does not locate an operator.
- **Audio → Voice packs**, also reachable from Settings: choose a language, review model size and install, select, sample or remove individual voices. Downloads require confirmation; playback stays offline. Shared Python dependencies take extra space. Missing Python can be installed with the official verified Windows installer after confirmation.

ADIF imports support ASCII `.adi`/`.adif`, up to 8 MiB and 10,000 records. Unsupported fields and rejected records are summarized before importing; the log is not a lossless archive of every ADIF field. Keep the original file. Practice and real contacts remain separate.

## Run from source

Development requires Windows x64, .NET 10 SDK, Windows SDK 10.0.26100, WinUI build tools, PowerShell 7, CMake 3.28+, GCC 15+ and Ninja. NuGet dependencies download on the first build. `global.json` pins the Uno SDK.

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

The loading screen prepares preferences and the native engine. Network checks do not hold the main interface open. Automatic checks query the latest stable GitHub Release at most once every six hours, excluding drafts, prereleases and older versions. Settings provide manual checks and an opt-out. From Settings, Download and install fetches the matching Windows x64 Setup and SHA256SUMS from that release. Downloads are streamed to disk with size limits and cancellation. The app verifies SHA-256 and the installer version, then asks before opening Setup and closing itself. Checking alone never installs anything. Releases must contain Setup.exe and SHA256SUMS; a commit alone is not an application update.

```powershell
# Verification only; no installation or certificate creation.
pwsh -File scripts/package-desktop.ps1 -Unsigned

# Requires a matching certificate with a private key.
pwsh -File scripts/package-desktop.ps1 -CertificateThumbprint <40-character-thumbprint>
```

The script writes `MorseCore.msix`, `MorseCore.appinstaller` and `SHA256SUMS` into a unique ignored folder under `cmake-build-desktop-package/`. Unsigned packages cannot be distributed as normal trusted installers. The script does not install anything or add certificate trust.

The current package publisher is `O=Mustafa Mert Akceylan`, which must exactly match the signing certificate. A different production certificate requires updating the publisher and its validation together. Trusted signing and release publishing are still pending.

The future MSIX update channel requires publishing signed MSIX and App Installer assets under their fixed GitHub Release names, and installing via `.appinstaller`. Commits alone do not create an update. Test a real installation and an upgrade between two signed versions before release. [App Installer documentation](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview).

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
