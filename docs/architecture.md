# Shared platform architecture

MorseCore develops application rules once and shares them across desktop platforms.

| Layer | Single source | Platform adapters |
| --- | --- | --- |
| Conversion, statistics, practice and WAV synthesis | C23 engine and `include/morsecore_native.h` | .NET interop and Swift bridge |
| Stations, QSO, ADIF/CSV, radio guide, audio decoding and speech packages | `apps/MorseCore.AppCore` | Windows/Linux direct calls; macOS single-request self-contained `MorseCore.MacService` |
| Release selection and verified downloads | AppCore `UpdateService` | Native installer/Finder opening on macOS; Windows installer handoff; verified Linux archive and manual package update |
| Translations, ADIF band data and brand resources | AppCore `Strings` and `Radio/LogQuery.cs`; desktop `Assets` | macOS generated bundle copies |
| Interface and OS integration | Shared WinUI/Uno XAML on Windows/Linux; SwiftUI on macOS | Windows, file pickers, audio devices, microphone permissions and accessibility |

Keep business logic out of platform views. A fix to shared rules must reach all
frontends through the same service contract, with regression checks for each.
Do not introduce another platform-specific parser or storage schema when a shared
implementation already exists. OS-specific differences should be narrow and explicit.

Run `python scripts/sync-macos-resources.py` after changing shared strings, fonts or
the logo or ADIF ranges. CI runs the same command with `--check` to detect stale copies.
Map coordinates are resolved by AppCore `Locator.Position` in the service response;
the native map views only project and draw those coordinates.

`CMakeLists.txt` is the version source for every package. A platform added to an
existing release keeps that version and records its source commit; published tags
and existing packages are never rewritten. Native screens can differ while sharing
the same data and rules.

For development and distribution commands, see [macOS](../apps/MorseCore.macOS/README.md)
and [Windows](desktop.md). The [Linux preview](linux.md) reuses the Windows XAML,
styles, fonts and assets through Uno Skia Desktop/X11. SDL2 device I/O, X11 window
input and package handoff are narrow Linux adapters. Settings use XDG config,
radio/voice data use XDG data and downloaded updates use XDG cache. Portable,
DEB and Arch packages share one self-contained x64 payload. Distribution targets
are separate from environments actually tested; the Linux guide records that boundary.
