#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
[[ "$(uname -s)" == Linux && -n "${DISPLAY:-}" ]] || { echo 'Linux X11/XWayland display required.' >&2; exit 1; }
WORK="$(mktemp -d "$ROOT/cmake-build-linux-ui.XXXXXX")"
export MORSECORE_DESKTOP_SMOKE="$WORK"
export MORSECORE_DESKTOP_HOME="$WORK/profile"
export MORSECORE_DIAGNOSTICS_DIR="$WORK"
export XDG_CONFIG_HOME="$WORK/config" XDG_DATA_HOME="$WORK/data" XDG_CACHE_HOME="$WORK/cache"
export SDL_AUDIODRIVER=dummy
mkdir -p "$WORK/profile" "$WORK/config" "$WORK/data" "$WORK/cache"
NATIVE="${MORSECORE_NATIVE_LIBRARY:-$ROOT/build/linux/native/libMorseCore.Native.so}"
[[ -f "$NATIVE" ]] || { echo 'Build the native library first; set MORSECORE_NATIVE_LIBRARY if needed.' >&2; exit 1; }
dotnet publish "$ROOT/apps/MorseCore.Desktop/MorseCore.Desktop/MorseCore.Desktop.csproj" \
    -c Debug -f net10.0-desktop -r linux-x64 --self-contained true \
    --artifacts-path "$WORK/build" -m:1 -p:UseSharedCompilation=false --disable-build-servers \
    -p:PublishTrimmed=false "-p:NativeLibraryPath=$NATIVE" -o "$WORK/app"
TIMEOUT="${MORSECORE_LINUX_UI_TIMEOUT_SECONDS:-360}"
[[ "$TIMEOUT" =~ ^[0-9]+$ && "$TIMEOUT" -ge 60 && "$TIMEOUT" -le 600 ]] || { echo 'UI timeout must be 60–600 seconds.' >&2; exit 2; }
timeout "$TIMEOUT" "$WORK/app/MorseCore.Desktop" >"$WORK/gui.log" 2>&1 || {
    cat "$WORK/gui.log" >&2
    echo "UI process failed. Evidence: $WORK" >&2
    exit 1
}
python3 - "$WORK/ui-smoke.json" <<'PY'
import json, sys
from pathlib import Path
report=json.loads(Path(sys.argv[1]).read_text())
if not report['Passed']:
    raise SystemExit(report['Failure'])
print(f"PASS: {len(report['Checks'])} shared UI checks. Evidence: {Path(sys.argv[1]).parent}")
PY
