#!/usr/bin/env bash
set -euo pipefail
[[ "$(uname -s)" == Darwin ]] || { echo "Bundle validation requires macOS." >&2; exit 1; }
[[ $# == 1 && -d "$1/Contents" ]] || { echo "usage: $0 /absolute/path/MorseCore.app" >&2; exit 2; }
BUNDLE="$(cd "$1" && pwd)"
CONTENTS="$BUNDLE/Contents"
for required in Info.plist MacOS/MorseCoreMac Frameworks/libMorseCore.Native.dylib Helpers/MorseCore.MacService Helpers/libcoreclr.dylib Helpers/LICENSE.txt Helpers/ThirdPartyNotices.txt Resources/LICENSE Resources/NOTICE Resources/MorseCore.icns Resources/MorseCoreMac_MorseCoreMac.bundle; do
    [[ -e "$CONTENTS/$required" ]] || { echo "Missing bundled component: $required" >&2; exit 1; }
done
/usr/bin/plutil -lint "$CONTENTS/Info.plist"
/usr/bin/codesign --verify --deep --strict "$BUNDLE"
ARCH="${MORSECORE_EXPECT_ARCH:-$(uname -m)}"
count=0
while IFS= read -r -d '' binary; do
    /usr/bin/file -b "$binary" | /usr/bin/grep -q 'Mach-O' || continue
    count=$((count + 1))
    /usr/bin/lipo -verify_arch "$ARCH" "$binary"
    while IFS= read -r dependency; do
        [[ -n "$dependency" ]] || continue
        case "$dependency" in
            /usr/lib/*|/System/Library/*) ;;
            @loader_path/*)
                [[ -e "$(dirname "$binary")/${dependency#@loader_path/}" ]] || { echo "Missing loader-relative dependency: $binary -> $dependency" >&2; exit 1; }
                ;;
            @executable_path/*)
                [[ -e "$CONTENTS/MacOS/${dependency#@executable_path/}" || -e "$CONTENTS/Helpers/${dependency#@executable_path/}" ]] || { echo "Missing executable-relative dependency: $binary -> $dependency" >&2; exit 1; }
                ;;
            @rpath/*)
                name="${dependency#@rpath/}"
                # Apple ships these in the dyld shared cache; recent macOS may
                # not expose a separate /usr/lib/swift file for each library.
                case "$name" in libswift*.dylib) continue;; esac
                [[ -e "$CONTENTS/Frameworks/$name" || -e "$CONTENTS/Helpers/$name" || -e "/usr/lib/swift/$name" ]] || { echo "Missing bundled rpath dependency: $binary -> $dependency" >&2; exit 1; }
                ;;
            *) echo "External build-machine dependency: $binary -> $dependency" >&2; exit 1 ;;
        esac
    done < <(/usr/bin/otool -L "$binary" | /usr/bin/tail -n +2 | /usr/bin/sed -E 's/^[[:space:]]+//; s/ \(compatibility version.*$//')
    while IFS= read -r rpath; do
        case "$rpath" in @loader_path*|@executable_path*|/usr/lib/swift|/System/Library/*) ;;
            *) echo "External build-machine rpath: $binary -> $rpath" >&2; exit 1;;
        esac
    done < <(/usr/bin/otool -l "$binary" | /usr/bin/awk '/cmd LC_RPATH/ { wanted=1; next } wanted && $1=="path" { print $2; wanted=0 }')
done < <(/usr/bin/find "$CONTENTS" -type f -print0)
[[ $count -gt 2 ]] || { echo "Incomplete Mach-O payload" >&2; exit 1; }
echo "Bundle validated: $count Mach-O files, architecture $ARCH, self-contained runtime and relative dependencies."
