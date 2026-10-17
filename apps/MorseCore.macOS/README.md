# MorseCore for macOS

Native SwiftUI frontend with the same C23 engine and AppCore business logic
as Windows and Linux. Requires macOS 14 or later. Apple Silicon is the currently tested
target. Development builds require Xcode/Swift 6 and CMake 3.28 or later with
a final C23 compiler. The app bundle includes its .NET helper runtime;
end users do not need a separate .NET installation.

With a .NET 10 SDK on the Mac, the build script publishes the self-contained
helper automatically. For cross-publishing on another development computer:

```sh
# Run from the repository root. For an Intel build, use osx-x64 consistently.
dotnet publish apps/MorseCore.MacService -c Release -r osx-arm64 \
  --self-contained true -o cmake-build-agent-work/mac-service/osx-arm64
python3 apps/MorseCore.macOS/packaging/copy-runtime-notices.py \
  cmake-build-agent-work/mac-service/osx-arm64 osx-arm64 \
  apps/MorseCore.MacService/obj/project.assets.json
```

From this directory on a Mac:

```sh
chmod +x script/build_and_run.sh
./script/build_and_run.sh --test   # native and Swift bridge tests
./script/build_and_run.sh          # build a local .app and open it
./script/build_and_run.sh --smoke  # isolated development render snapshots
```

`--build` creates `dist/MorseCore.app` without launching. `--verify` also
checks the launched process. `--debug`, `--logs` and `--telemetry` are available
for diagnostics. Builds never close an existing app session automatically.
Normal runs build with Release optimizations; debug and render-smoke modes
use a Debug build. Render snapshots do not test interactive button behavior.

The workspaces cover conversion and files, Morse WAV playback/export, WAV and
microphone decoding, offline speech and voice packages, practice/alphabet/CW
glossary, straight-key timing, station profiles, QSO preparation, logbook,
ADIF/CSV and an offline contact map. The interface includes five languages,
Arabic RTL with LTR Morse/callsigns, RAW colors/fonts and reduced motion.

Speech synthesis stays offline. Voice installation downloads the selected
engine/models with consent. If needed, Apple Silicon can install a verified
private Python 3.13 runtime in the speech folder, without sudo or system PATH
changes. EMA Lightning, Antalia-2 Mini and Piper retain their original notices.

Update checks use the same AppCore policy as Windows: matching CPU assets,
bounded HTTPS downloads and SHA-256. Installation requires a separate user
action to open a verified DMG/PKG; there is no silent app replacement.
Physical microphone capture still needs device QA.

From the repository root, create and validate a drag-and-drop installer:

```sh
bash scripts/package-macos.sh
MORSECORE_PACKAGE_GUI_SMOKE=1 bash scripts/test-macos-package.sh \
  build/release-macos/MorseCore-1.0.1-macOS-arm64.dmg
```

For a freshly cross-published helper, set `MORSECORE_SKIP_HELPER_PUBLISH=1` on
the Mac. Copy all helper output, including runtime licenses. This explicit
reuse option is for developers; CI normally publishes from source.
`python3 scripts/sync-macos-resources.py --check` detects stale strings,
brand resources and generated ADIF ranges.

Community bundles use ad hoc signing, not Developer ID signing
or notarization. Native library and SwiftPM resources are bundled together.
Brand artwork and OFL fonts are reused from the desktop app; font licenses,
Apache-2.0 LICENSE and NOTICE travel with the app bundle.

Developer ID and notarization are currently rejected by the scripts: the
managed helper layout needs a separate signing-compatible packaging change.
Buying a membership or selecting the existing development certificate does
not by itself make this package notarized. See [installation](../../docs/macos.md)
and [shared architecture](../../docs/architecture.md).
