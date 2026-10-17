#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
if [[ "${1:-}" == --help ]]; then
    echo 'usage: package-linux.sh [--skip-build] [--deb]'
    echo 'MORSECORE_PACKAGE_OUTPUT, MORSECORE_LINUX_PUBLISH_DIR, MORSECORE_LINUX_BUILD_DIR, NUGET_PACKAGES, CC'
    exit 0
fi
[[ "$(uname -s)" == Linux && "$(uname -m)" == x86_64 ]] || { echo 'Linux x86_64 packaging host required.' >&2; exit 1; }
SKIP=0
DEB=0
for argument in "$@"; do
    case "$argument" in --skip-build) SKIP=1;; --deb) DEB=1;; *) echo "Unknown argument: $argument" >&2; exit 2;; esac
done
command -v python3 >/dev/null || { echo 'Python 3 is required for packaging (not for running MorseCore).' >&2; exit 1; }
command -v readelf >/dev/null || { echo 'binutils/readelf is required for ELF compatibility validation.' >&2; exit 1; }
OUTPUT="${MORSECORE_PACKAGE_OUTPUT:-$ROOT/build/release-linux}"
BUILD="${MORSECORE_LINUX_BUILD_DIR:-$ROOT/build/linux/native}"
PUBLISH="${MORSECORE_LINUX_PUBLISH_DIR:-$ROOT/build/linux/publish}"
VERSION="$(sed -n 's/^project(MorseCore VERSION \([0-9][0-9.]*\).*/\1/p' "$ROOT/CMakeLists.txt")"
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Missing CMake project version.' >&2; exit 1; }
export NUGET_PACKAGES="${NUGET_PACKAGES:-$ROOT/build/linux/nuget}"
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$ROOT/build/linux/dotnet-home}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_GENERATE_ASPNET_CERTIFICATE=false DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=false
if [[ "$SKIP" == 0 ]]; then
    [[ ! -e "$PUBLISH" ]] || { echo "Publish directory already exists: $PUBLISH (use a new MORSECORE_LINUX_PUBLISH_DIR)." >&2; exit 1; }
    cmake -S "$ROOT" -B "$BUILD" -G Ninja -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF
    cmake --build "$BUILD" --target MorseCoreNative
    NATIVE="$(cd -- "$BUILD" && pwd -P)/libMorseCore.Native.so"
    dotnet publish "$ROOT/apps/MorseCore.Desktop/MorseCore.Desktop/MorseCore.Desktop.csproj" \
        --configuration Release --framework net10.0-desktop --runtime linux-x64 --self-contained true \
        --artifacts-path "$BUILD/dotnet" -m:1 -p:UseSharedCompilation=false --disable-build-servers \
        -p:PublishTrimmed=false -p:PublishSingleFile=false "-p:NativeLibraryPath=$NATIVE" \
        "-p:Version=$VERSION" "-p:ApplicationDisplayVersion=$VERSION" --output "$PUBLISH"
fi
ARGS=(--publish "$PUBLISH" --output "$OUTPUT" --packages "$NUGET_PACKAGES")
[[ "$DEB" == 0 ]] || ARGS+=(--deb)
python3 "$ROOT/packaging/linux/build-package.py" "${ARGS[@]}"
