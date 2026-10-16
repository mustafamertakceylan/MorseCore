#!/usr/bin/env bash
set -euo pipefail
[[ "$(uname -s)" == Darwin ]] || { echo "DMG packaging requires macOS." >&2; exit 1; }
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(sed -n 's/^project(MorseCore VERSION \([^ ]*\).*/\1/p' "$ROOT/CMakeLists.txt")"
[[ -n "$VERSION" ]] || { echo "Missing version" >&2; exit 1; }
case "$(uname -m)" in arm64) ARCH=arm64;; x86_64) ARCH=x64;; *) echo "Unsupported Mac architecture" >&2; exit 1;; esac
OUTPUT="${MORSECORE_PACKAGE_OUTPUT:-$ROOT/build/release-macos}"
mkdir -p "$OUTPUT"
OUTPUT="$(cd "$OUTPUT" && pwd)"
if [[ "${1:-}" == --help ]]; then echo "usage: $0 [--skip-build] (MORSECORE_BUNDLE_NAME, MORSECORE_PACKAGE_OUTPUT, MORSECORE_SIGN_IDENTITY, MORSECORE_NOTARY_PROFILE)"; exit 0; fi
[[ $# == 0 || ( $# == 1 && "${1:-}" == --skip-build ) ]] || { echo "Unknown argument" >&2; exit 2; }
if [[ "${MORSECORE_SIGN_IDENTITY:--}" != - || -n "${MORSECORE_NOTARY_PROFILE:-}" ]]; then
    echo "This community package supports ad-hoc signing only. Developer ID/notarization requires a validated single-file helper/resource layout first." >&2
    exit 1
fi
if [[ "${1:-}" != --skip-build ]]; then bash "$ROOT/apps/MorseCore.macOS/script/build_and_run.sh" --build; fi
SOURCE="$ROOT/apps/MorseCore.macOS/dist/${MORSECORE_BUNDLE_NAME:-MorseCore.app}"
bash "$ROOT/apps/MorseCore.macOS/packaging/validate-bundle.sh" "$SOURCE"
ACTUAL="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$SOURCE/Contents/Info.plist")"
[[ "$ACTUAL" == "$VERSION" ]] || { echo "Bundle version $ACTUAL does not match $VERSION" >&2; exit 1; }
NAME="MorseCore-$VERSION-macOS-$ARCH.dmg"
[[ ! -e "$OUTPUT/$NAME" ]] || { echo "Refusing to overwrite existing package: $OUTPUT/$NAME" >&2; exit 1; }
WORK="$(mktemp -d "$OUTPUT/.dmg-stage.XXXXXX")"
trap 'rm -rf "$WORK"' EXIT
mkdir "$WORK/payload"
/usr/bin/ditto "$SOURCE" "$WORK/payload/MorseCore.app"
/bin/ln -s /Applications "$WORK/payload/Applications"
/bin/cp "$ROOT/apps/MorseCore.macOS/packaging/INSTALL.txt" "$WORK/payload/INSTALL.txt"
/usr/bin/hdiutil create -quiet -volname "MorseCore $VERSION" -srcfolder "$WORK/payload" -format UDZO -fs HFS+ "$WORK/$NAME"
if [[ -n "${MORSECORE_SIGN_IDENTITY:-}" && "$MORSECORE_SIGN_IDENTITY" != - ]]; then
    /usr/bin/codesign --force --sign "$MORSECORE_SIGN_IDENTITY" --timestamp "$WORK/$NAME"
fi
if [[ -n "${MORSECORE_NOTARY_PROFILE:-}" ]]; then
    [[ -n "${MORSECORE_SIGN_IDENTITY:-}" && "$MORSECORE_SIGN_IDENTITY" != - ]] || { echo "Notarization requires a Developer ID signing identity." >&2; exit 1; }
    /usr/bin/codesign -dv --verbose=4 "$WORK/payload/MorseCore.app" 2>&1 | /usr/bin/grep -q '^Authority=Developer ID Application:' || { echo "Notarization requires a Developer ID signed app; rebuild without --skip-build." >&2; exit 1; }
    /usr/bin/xcrun notarytool submit "$WORK/$NAME" --keychain-profile "$MORSECORE_NOTARY_PROFILE" --wait
    /usr/bin/xcrun stapler staple "$WORK/$NAME"
    /usr/bin/xcrun stapler validate "$WORK/$NAME"
fi
/usr/bin/hdiutil verify "$WORK/$NAME"
/bin/mv "$WORK/$NAME" "$OUTPUT/$NAME"
# Use a platform/architecture-specific manifest; never replace Windows hashes.
(cd "$OUTPUT" && /usr/bin/shasum -a 256 "$NAME") > "$OUTPUT/SHA256SUMS-macOS-$ARCH.txt"
echo "Package: $OUTPUT/$NAME"
echo "Checksums: $OUTPUT/SHA256SUMS-macOS-$ARCH.txt"
