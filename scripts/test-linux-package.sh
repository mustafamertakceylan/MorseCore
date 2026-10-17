#!/usr/bin/env bash
set -euo pipefail
[[ "$(uname -s)" == Linux ]] || { echo 'Linux package tests require Linux.' >&2; exit 1; }
[[ $# -ge 1 && $# -le 2 && -f "$1" ]] || { echo 'usage: test-linux-package.sh PACKAGE.tar.gz [PACKAGE.deb]' >&2; exit 2; }
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
ARCHIVE="$(realpath -- "$1")"
DEB=""
[[ $# == 1 ]] || DEB="$(realpath -- "$2")"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/morsecore-linux-package.XXXXXX")"
WORK="$(cd -- "$WORK" && pwd -P)"
GUI_PID=""
cleanup() {
    if [[ -n "$GUI_PID" ]]; then kill -TERM "$GUI_PID" 2>/dev/null || true; wait "$GUI_PID" 2>/dev/null || true; fi
    # mktemp's resolved, unique absolute directory is the only deletion target.
    case "$WORK" in /*/morsecore-linux-package.*) rm -rf -- "$WORK";; *) echo "Unsafe cleanup path: $WORK" >&2;; esac
}
trap cleanup EXIT
PACKAGE="$(python3 "$ROOT/packaging/linux/verify-package.py" "$ARCHIVE" "$WORK/extracted")"
[[ -d "$PACKAGE" ]] || { echo 'Package inspection did not produce a root directory.' >&2; exit 1; }
"$PACKAGE/morsecore" --check-dependencies
if [[ -n "$DEB" ]]; then
    [[ -f "$DEB" ]] || { echo "Missing .deb: $DEB" >&2; exit 1; }
    python3 "$ROOT/packaging/linux/verify-package.py" --check-file "$DEB"
    dpkg-deb --extract "$DEB" "$WORK/deb"
    [[ "$(dpkg-deb --field "$DEB" Package)" == morsecore ]]
    [[ "$(dpkg-deb --field "$DEB" Architecture)" == amd64 ]]
    [[ "$(dpkg-deb --field "$DEB" Version)" == "$(basename "$ARCHIVE" | cut -d- -f2)" ]]
    [[ "$(readlink -- "$WORK/deb/usr/bin/morsecore")" == /usr/lib/morsecore/morsecore ]]
    cmp "$PACKAGE/libMorseCore.Native.so" "$WORK/deb/usr/lib/morsecore/libMorseCore.Native.so"
    [[ -f "$WORK/deb/usr/share/applications/io.github.mustafamertakceylan.morsecore.desktop" ]]
    [[ -f "$WORK/deb/usr/share/icons/hicolor/scalable/apps/io.github.mustafamertakceylan.morsecore.svg" ]]
    echo 'PASS: .deb metadata, extraction, launcher link, menu entry, icon and matching native payload.'
fi
if [[ "${MORSECORE_LINUX_GUI_SMOKE:-0}" == 1 ]]; then
    [[ -n "${DISPLAY:-}" ]] || { echo 'GUI smoke requires an X11/XWayland DISPLAY.' >&2; exit 1; }
    mkdir -p "$WORK/home" "$WORK/data" "$WORK/config" "$WORK/cache" "$WORK/state"
    HOME="$WORK/home" XDG_DATA_HOME="$WORK/data" XDG_CONFIG_HOME="$WORK/config" \
        XDG_CACHE_HOME="$WORK/cache" XDG_STATE_HOME="$WORK/state" MORSECORE_DESKTOP_HOME="$WORK/data/morsecore" \
        "$PACKAGE/morsecore" >"$WORK/gui.log" 2>&1 &
    GUI_PID=$!
    sleep 6
    if ! kill -0 "$GUI_PID" 2>/dev/null; then cat "$WORK/gui.log" >&2; echo 'Relocated GUI exited during startup.' >&2; exit 1; fi
    kill -TERM "$GUI_PID"
    wait "$GUI_PID" 2>/dev/null || true
    GUI_PID=""
    echo 'PASS: relocated self-contained GUI stays running with isolated user directories.'
fi
echo 'PASS: archive path/link safety, complete runtime/notices, native ABI/version, relocated SOS and native dependencies.'
echo 'Scope: package extraction only; apt/pacman installation, upgrades, GUI layout, microphone and real devices require VM checks.'
