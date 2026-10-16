#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-run}"
case "$MODE" in run|--build|--test|--smoke|--debug|--logs|--telemetry|--verify) ;; *) echo "usage: $0 [--build|--test|--smoke|--debug|--logs|--telemetry|--verify]" >&2; exit 2;; esac
[[ "$(uname -s)" == Darwin ]] || { echo "This script requires macOS." >&2; exit 1; }
PACKAGE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT_DIR="$(cd "$PACKAGE_DIR/../.." && pwd)"
NATIVE_DIR="$ROOT_DIR/build/mac-native"
APP_NAME="MorseCoreMac"
BUNDLE_ID="com.morsecore.mac"
DIST_DIR="$PACKAGE_DIR/dist"
BUNDLE_NAME="${MORSECORE_BUNDLE_NAME:-MorseCore.app}"
[[ "$BUNDLE_NAME" == *.app && "$BUNDLE_NAME" != */* && "$BUNDLE_NAME" != .* ]] || { echo "Bundle name must be a plain .app filename." >&2; exit 1; }
APP_BUNDLE="$DIST_DIR/$BUNDLE_NAME"
FINAL_BINARY="$APP_BUNDLE/Contents/MacOS/$APP_NAME"
VERSION="$(sed -n 's/^project(MorseCore VERSION \([^ ]*\).*/\1/p' "$ROOT_DIR/CMakeLists.txt")"
[[ -n "$VERSION" ]] || { echo "Missing project version" >&2; exit 1; }
python3 "$ROOT_DIR/scripts/sync-macos-resources.py" --check

CONFIGURATION=release
NATIVE_CONFIGURATION=Release
case "$MODE" in --test|--debug|--smoke) CONFIGURATION=debug; NATIVE_CONFIGURATION=Debug;; esac
cmake -S "$ROOT_DIR" -B "$NATIVE_DIR" -DCMAKE_BUILD_TYPE="$NATIVE_CONFIGURATION" -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0
cmake --build "$NATIVE_DIR" -j 4
cd "$PACKAGE_DIR"
if [[ "$MODE" == --test ]]; then
    ctest --test-dir "$NATIVE_DIR" --output-on-failure
    swift test
    exit 0
fi
swift build -c "$CONFIGURATION"
BIN_DIR="$(swift build -c "$CONFIGURATION" --show-bin-path)"

# Preserve old bundles and never terminate an existing user session.
if /usr/bin/pgrep -f "$FINAL_BINARY" >/dev/null; then
    echo "MorseCore is already running from this bundle. Close it before rebuilding." >&2
    exit 1
fi
case "$(uname -m)" in arm64) RUNTIME_ID=osx-arm64;; x86_64) RUNTIME_ID=osx-x64;; *) echo "Unsupported Mac architecture" >&2; exit 1;; esac
HELPER_DIR="$ROOT_DIR/cmake-build-agent-work/mac-service/$RUNTIME_ID"
# A packaged app includes .NET; only the developer machine needs its SDK.
if [[ "${MORSECORE_SKIP_HELPER_PUBLISH:-0}" != 1 ]]; then
    DOTNET="$(command -v dotnet || true)"
    if [[ -z "$DOTNET" ]]; then
        for candidate in "$HOME/.dotnet/dotnet" /usr/local/share/dotnet/dotnet /opt/homebrew/bin/dotnet; do
            if [[ -x "$candidate" ]]; then DOTNET="$candidate"; break; fi
        done
    fi
    [[ -n "$DOTNET" ]] || { echo "Building requires the .NET 10 SDK; the installed application does not. Install the SDK or explicitly reuse a current helper with MORSECORE_SKIP_HELPER_PUBLISH=1." >&2; exit 1; }
    DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
        "$DOTNET" publish "$ROOT_DIR/apps/MorseCore.MacService/MorseCore.MacService.csproj" \
        -c Release -r "$RUNTIME_ID" --self-contained true -p:UseSharedCompilation=false \
        -o "$HELPER_DIR" --nologo
fi
python3 "$PACKAGE_DIR/packaging/copy-runtime-notices.py" "$HELPER_DIR" "$RUNTIME_ID" "$ROOT_DIR/apps/MorseCore.MacService/obj/project.assets.json"
for helper_file in MorseCore.MacService libcoreclr.dylib LICENSE.txt ThirdPartyNotices.txt; do
    [[ -f "$HELPER_DIR/$helper_file" ]] || { echo "Self-contained helper component missing at $HELPER_DIR/$helper_file" >&2; exit 1; }
done
mkdir -p "$DIST_DIR"
STAGE_DIR="$(mktemp -d "$DIST_DIR/.bundle-stage.XXXXXX")"
trap 'rm -rf "$STAGE_DIR"' EXIT
STAGED_BUNDLE="$STAGE_DIR/$BUNDLE_NAME"
APP_CONTENTS="$STAGED_BUNDLE/Contents"
APP_BINARY="$APP_CONTENTS/MacOS/$APP_NAME"
mkdir -p "$APP_CONTENTS/MacOS" "$APP_CONTENTS/Frameworks" "$APP_CONTENTS/Resources"
cp "$BIN_DIR/$APP_NAME" "$APP_BINARY"
cp "$NATIVE_DIR/libMorseCore.Native.dylib" "$APP_CONTENTS/Frameworks/"
cp -R "$BIN_DIR/MorseCoreMac_MorseCoreMac.bundle" "$APP_CONTENTS/Resources/"
cp "$ROOT_DIR/LICENSE" "$ROOT_DIR/NOTICE" "$APP_CONTENTS/Resources/"
mkdir -p "$APP_CONTENTS/Helpers"
cp -R "$HELPER_DIR/." "$APP_CONTENTS/Helpers/"
chmod +x "$APP_CONTENTS/Helpers/MorseCore.MacService"
chmod +x "$APP_BINARY"
install_name_tool -id '@rpath/libMorseCore.Native.dylib' "$APP_CONTENTS/Frameworks/libMorseCore.Native.dylib"
DEPENDENCY="$(otool -L "$APP_BINARY" | awk '/libMorseCore.Native.dylib/ {print $1; exit}')"
[[ -n "$DEPENDENCY" ]] || { echo "Native dependency missing from app" >&2; exit 1; }
install_name_tool -change "$DEPENDENCY" '@rpath/libMorseCore.Native.dylib' "$APP_BINARY"
install_name_tool -add_rpath '@executable_path/../Frameworks' "$APP_BINARY"
install_name_tool -delete_rpath "$NATIVE_DIR" "$APP_BINARY"
# SwiftPM/Xcode may add an absolute PackageFrameworks search path even when
# this package has no dynamic package frameworks. It must not escape the build.
while IFS= read -r build_rpath; do
    case "$build_rpath" in
        "$PACKAGE_DIR"/.build/*) install_name_tool -delete_rpath "$build_rpath" "$APP_BINARY" ;;
        /*.xctoolchain/usr/lib/swift*) install_name_tool -delete_rpath "$build_rpath" "$APP_BINARY" ;;
    esac
done < <(/usr/bin/otool -l "$APP_BINARY" | /usr/bin/awk '/cmd LC_RPATH/ { wanted=1; next } wanted && $1=="path" { print $2; wanted=0 }')

ICONSET="$STAGE_DIR/MorseCore.iconset"
mkdir -p "$ICONSET"
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$PACKAGE_DIR/Sources/MorseCoreMac/Resources/morsecore.png" --out "$ICONSET/icon_${size}x${size}.png" >/dev/null
    doubled=$((size * 2))
    sips -z "$doubled" "$doubled" "$PACKAGE_DIR/Sources/MorseCoreMac/Resources/morsecore.png" --out "$ICONSET/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP_CONTENTS/Resources/MorseCore.icns"
cat > "$APP_CONTENTS/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>$APP_NAME</string>
<key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
<key>CFBundleName</key><string>MorseCore</string>
<key>CFBundleDisplayName</key><string>MorseCore</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>$VERSION</string>
<key>CFBundleVersion</key><string>$VERSION</string>
<key>CFBundleIconFile</key><string>MorseCore.icns</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSPrincipalClass</key><string>NSApplication</string>
<key>NSHighResolutionCapable</key><true/>
<key>NSMicrophoneUsageDescription</key><string>MorseCore records your selected microphone locally to decode Morse code. Audio stays on this Mac.</string>
</dict></plist>
PLIST
plutil -lint "$APP_CONTENTS/Info.plist"
SIGN_IDENTITY="${MORSECORE_SIGN_IDENTITY:--}"
if [[ "$SIGN_IDENTITY" != - || -n "${MORSECORE_NOTARY_PROFILE:-}" ]]; then
    echo "Developer ID packaging is not enabled for the current loose managed helper layout. Use the ad-hoc community build; a single-file helper/resource layout must be validated before notarization." >&2
    exit 1
fi
SIGN_OPTIONS=(--force --sign "$SIGN_IDENTITY")
if [[ "$SIGN_IDENTITY" != - ]]; then SIGN_OPTIONS+=(--options runtime --timestamp); fi
# Sign nested code individually before sealing the outer bundle. The .NET
# apphost needs JIT entitlements only for a Developer ID distribution.
while IFS= read -r -d '' binary; do
    if /usr/bin/file -b "$binary" | /usr/bin/grep -q 'Mach-O'; then
        if [[ "$binary" == "$APP_CONTENTS/Helpers/MorseCore.MacService" && "$SIGN_IDENTITY" != - ]]; then
            codesign "${SIGN_OPTIONS[@]}" --entitlements "$PACKAGE_DIR/packaging/helper-entitlements.plist" "$binary"
        else
            codesign "${SIGN_OPTIONS[@]}" "$binary"
        fi
    fi
done < <(find "$APP_CONTENTS/Helpers" "$APP_CONTENTS/Frameworks" -type f -print0)
# Helpers currently also contains the managed .NET payload. Deep ad-hoc sealing
# preserves its generic code signatures across this community bundle. Do not
# extend this temporary layout to Developer ID/notarized distribution: Apple
# requires non-Mach-O resources outside nested-code-only directories.
codesign --deep "${SIGN_OPTIONS[@]}" "$STAGED_BUNDLE"
bash "$PACKAGE_DIR/packaging/validate-bundle.sh" "$STAGED_BUNDLE"
# Recheck immediately before replacement in case a user opened it during build.
if /usr/bin/pgrep -f "$FINAL_BINARY" >/dev/null; then echo "Bundle was opened during build; leaving it untouched." >&2; exit 1; fi
if [[ -e "$APP_BUNDLE" ]]; then mv "$APP_BUNDLE" "$STAGE_DIR/previous.app"; fi
if ! mv "$STAGED_BUNDLE" "$APP_BUNDLE"; then
    if [[ -d "$STAGE_DIR/previous.app" ]]; then mv "$STAGE_DIR/previous.app" "$APP_BUNDLE"; fi
    exit 1
fi
APP_BINARY="$FINAL_BINARY"

case "$MODE" in
    --build) echo "$APP_BUNDLE" ;;
    --debug) lldb -- "$APP_BINARY" ;;
    --logs) /usr/bin/open "$APP_BUNDLE"; /usr/bin/log stream --info --style compact --predicate 'process == "MorseCoreMac"' ;;
    --telemetry) /usr/bin/open "$APP_BUNDLE"; /usr/bin/log stream --info --style compact --predicate 'subsystem == "com.morsecore.mac"' ;;
    --verify) /usr/bin/open "$APP_BUNDLE"; sleep 2; /usr/bin/pgrep -f "$APP_BINARY" ;;
    --smoke)
        SMOKE_DIR="$ROOT_DIR/build/mac-ui-smoke-$(date +%Y%m%d-%H%M%S)"
        /usr/bin/open -n -W --env "MORSECORE_UI_SMOKE_DIR=$SMOKE_DIR" --env "MORSECORE_DATA_ROOT=$SMOKE_DIR/data" --env MORSECORE_SETTINGS_SUITE=com.morsecore.mac.launch-smoke "$APP_BUNDLE"
        test -f "$SMOKE_DIR/render-smoke.json"
        cat "$SMOKE_DIR/render-smoke.json"
        ;;
    run) /usr/bin/open "$APP_BUNDLE" ;;
esac
