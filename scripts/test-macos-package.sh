#!/usr/bin/env bash
set -euo pipefail
[[ "$(uname -s)" == Darwin ]] || { echo "Package tests require macOS." >&2; exit 1; }
[[ $# == 1 && -f "$1" ]] || { echo "usage: $0 MorseCore-VERSION-macOS-ARCH.dmg" >&2; exit 2; }
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DMG="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/morsecore-package-test.XXXXXX")"
WORK="$(cd "$WORK" && pwd -P)"
MOUNTED=0
GUI_PID=""
cleanup() {
    # Only the process launched from this test's unique temporary bundle.
    if [[ -n "$GUI_PID" ]]; then /bin/kill -TERM "$GUI_PID" 2>/dev/null || true; fi
    if [[ "$MOUNTED" == 1 ]]; then /usr/bin/hdiutil detach "$WORK/mount" -quiet || return; fi
    rm -rf "$WORK"
}
trap cleanup EXIT
mkdir -p "$WORK/mount" "$WORK/Applications" "$WORK/data" "$WORK/speech"
/usr/bin/hdiutil verify "$DMG"
/usr/bin/hdiutil attach -readonly -nobrowse -mountpoint "$WORK/mount" "$DMG" >/dev/null
MOUNTED=1
[[ -L "$WORK/mount/Applications" && "$(/usr/bin/readlink "$WORK/mount/Applications")" == /Applications ]] || { echo "Applications shortcut missing or incorrect" >&2; exit 1; }
[[ -s "$WORK/mount/INSTALL.txt" ]] || { echo "Installation instructions missing" >&2; exit 1; }
bash "$ROOT/apps/MorseCore.macOS/packaging/validate-bundle.sh" "$WORK/mount/MorseCore.app"
/usr/bin/ditto "$WORK/mount/MorseCore.app" "$WORK/Applications/MorseCore.app"
/usr/bin/hdiutil detach "$WORK/mount" -quiet
MOUNTED=0
APP="$WORK/Applications/MorseCore.app"
bash "$ROOT/apps/MorseCore.macOS/packaging/validate-bundle.sh" "$APP"
# Exercise the actual Release executable's CFBundle lookup without a GUI.
# Run from a relocated app, so the builder's absolute SwiftPM path cannot help.
python3 - "$APP" "$WORK" <<'PY'
import pathlib, subprocess, sys
app, work = map(pathlib.Path, sys.argv[1:])
result = subprocess.run([str(app / 'Contents/MacOS/MorseCoreMac'), '--verify-packaged-resources'],
    env={'PATH': '/usr/bin:/bin', 'HOME': str(work), 'TMPDIR': str(work)},
    capture_output=True, timeout=20, check=True)
(work / 'resources.json').write_bytes(result.stdout)
PY
[[ "$(/usr/bin/plutil -extract ok raw -o - "$WORK/resources.json")" == true ]]
[[ "$(/usr/bin/plutil -extract resourceBundle raw -o - "$WORK/resources.json")" == "$APP/Contents/Resources/MorseCoreMac_MorseCoreMac.bundle" ]]
cat "$WORK/resources.json"
echo 'PASS: relocated Release executable decodes five translations, logo and two font files from its own resource bundle.'
# Execute the relocated self-contained .NET helper with no dotnet/Homebrew PATH.
printf '%s\n' '{"command":"cw.encode","payload":{"text":"SOS"}}' | \
    /usr/bin/env -i PATH=/usr/bin:/bin HOME="$WORK" TMPDIR="$WORK" \
    "$APP/Contents/Helpers/MorseCore.MacService" --data "$WORK/data" --speech "$WORK/speech" \
    --native "$APP/Contents/Frameworks/libMorseCore.Native.dylib" > "$WORK/response.json"
[[ "$(/usr/bin/plutil -extract ok raw -o - "$WORK/response.json")" == true ]] || { cat "$WORK/response.json" >&2; exit 1; }
[[ "$(/usr/bin/plutil -extract data.morse raw -o - "$WORK/response.json")" == '... --- ...' ]] || { cat "$WORK/response.json" >&2; exit 1; }
if [[ "${MORSECORE_PACKAGE_GUI_SMOKE:-0}" == 1 ]]; then
    /usr/bin/open -n --env "MORSECORE_DATA_ROOT=$WORK/data" \
        --env "MORSECORE_PACKAGE_READY_FILE=$WORK/data/package-ready.json" \
        --env "MORSECORE_SETTINGS_SUITE=com.morsecore.mac.package-test.$(basename "$WORK")" "$APP"
    for attempt in {1..20}; do
        sleep 1
        GUI_PID="$(/usr/bin/pgrep -f "^$APP/Contents/MacOS/MorseCoreMac$" || true)"
        [[ -z "$GUI_PID" || ! -s "$WORK/data/package-ready.json" ]] || break
    done
    [[ "$GUI_PID" =~ ^[0-9]+$ ]] || { echo "Installed GUI process did not launch" >&2; exit 1; }
    [[ -s "$WORK/data/package-ready.json" ]] || { cat "$WORK/data/package-readiness-diagnostic.json" 2>/dev/null || true; echo "Installed GUI did not decode its resources and present a window" >&2; exit 1; }
    [[ "$(/usr/bin/plutil -extract pid raw -o - "$WORK/data/package-ready.json")" == "$GUI_PID" ]]
    [[ "$(/usr/bin/plutil -extract visibleWindow raw -o - "$WORK/data/package-ready.json")" == true ]]
    cat "$WORK/data/package-ready.json"
    /bin/kill -0 "$GUI_PID"
    /bin/kill -TERM "$GUI_PID"
    GUI_PID=""
    /usr/bin/defaults delete "com.morsecore.mac.package-test.$(basename "$WORK")" 2>/dev/null || true
    echo 'PASS: relocated Release GUI decodes packaged resources and presents a real window with isolated settings/data.'
fi
echo 'PASS: DMG integrity, read-only mount, Applications shortcut, relocation, signatures, architecture, bundled runtime and native SOS conversion.'
echo 'Scope: isolated drag-and-drop installation; Gatekeeper approval and physical audio need separate device checks.'
