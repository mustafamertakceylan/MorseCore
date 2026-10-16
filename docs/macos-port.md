# macOS port

The SwiftUI frontend lives in `apps/MorseCore.macOS`. C23 is linked through
`MorseCoreBridge`; `MorseCore.MacService` reuses the Windows AppCore for data,
QSO/radio rules, ADIF, audio decoding and offline speech. A single-request
helper exits after each operation; the app serializes helper jobs and caches
the immutable catalog. Audio playback, capture and sidetone use AVFoundation.

| Area | macOS implementation | Verification boundary |
| --- | --- | --- |
| Text/files | Three alphabets, Unicode statistics, 200,000 UTF-16 unit editor limit, 8 MiB file limit, no overwrite for batch output | Native, Swift bridge and shared-service tests |
| Sound | Morse playback, WAV export, speed/frequency, WAV decoder, selected microphone and 120-second capture | WAV/decoder tested; physical microphone remains device QA |
| Speech | EMA, Antalia, eight offline voices across five languages, install/remove, private runtime bootstrap | Eight voices produced non-silent PCM WAV with OS-level network denial on Apple Silicon |
| Education | Six practice modes, missed answers, three alphabets, CW glossary/phonetics/prosigns, focused straight key and timing | Native practice and shared keying tests; physical keyboard interaction remains QA |
| Radio | Profiles, call recognition, operation suffixes, QSO stages, band guide, log queries/editing, ADIF/CSV, offline map | Same AppCore as Windows; synthetic data only for tests |
| UI | RAW palette/fonts/background paths, 240/72 sidebar, grouped navigation, matching card spacing, five languages/RTL | Render snapshots; screenshots do not prove button interactions |
| Updates | Shared AppCore release/CPU selection, bounded download and SHA-256; native installer opening | Synthetic rejection tests and opt-in real-release download check |

The band guide preserves the Windows rules snapshot, review date and sources.
Porting does not constitute a new legal review or authorize RF transmission.
Neither frontend connects to radio hardware in this stage.

The DMG is ad hoc signed. Developer ID signing and notarization are not included;
the current managed helper layout explicitly rejects those signing modes.
Installation is drag-and-drop, with .NET bundled; upgrades require the user's
replacement action. See [installation](macos.md) and [architecture](architecture.md).
Nothing in this port changes the Windows release number.
