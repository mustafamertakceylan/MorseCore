#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd -P)"
source "$ROOT/packaging/linux/native-dependencies.sh"
BASE="$ROOT/cmake-build-agent-work/linux-packaging"
mkdir -p "$BASE"
WORK="$(mktemp -d "$BASE/native-paths.XXXXXX")"
WORK="$(cd -- "$WORK" && pwd -P)"
trap 'case "$WORK" in /*/native-paths.*) rm -rf -- "$WORK";; esac' EXIT
mkdir -p "$WORK/bin" "$WORK/app" "$WORK/system" "$WORK/local with spaces"
cat > "$WORK/bin/ldd" <<'SH'
#!/usr/bin/env bash
case "$(cat -- "$1")" in
    broken) echo 'lib-synthetic-missing.so => not found';;
    invalid) echo 'not a dynamic executable'; exit 1;;
    *) echo 'libc.so.6 => /synthetic/libc.so.6';;
esac
SH
chmod +x "$WORK/bin/ldd"
export PATH="$WORK/bin:$PATH"
checks=0
expect_success() {
    if ! morsecore_check_native_library "$@"; then echo "FAIL: expected resolved library: $1" >&2; exit 1; fi
    checks=$((checks + 1))
}
expect_failure() {
    if morsecore_check_native_library "$@"; then echo "FAIL: expected missing/unloadable library: $1" >&2; exit 1; fi
    checks=$((checks + 1))
}
printf '%s' valid > "$WORK/local with spaces/libSDL2-2.0.so.0"
expect_success libSDL2-2.0.so.0 "$WORK/app:$WORK/local with spaces" ''
expect_failure libssl.so.3 "$WORK/app:$WORK/local with spaces" ''
printf '%s' valid > "$WORK/system/libssl.so.3"
expect_success libssl.so.3 "$WORK/app" "    libssl.so.3 (libc6,x86-64) => $WORK/system/libssl.so.3"
expect_failure libssl.so.3 "$WORK/app" "    libssl.so.30 (libc6,x86-64) => $WORK/system/libssl.so.3"
printf '%s' valid > "$WORK/local with spaces/libicuuc.so.78.2"
expect_success libicuuc.so "$WORK/local with spaces" ''
expect_success libicuuc.so "$WORK/app" "    libicuuc.so.78 (libc6,x86-64) => $WORK/local with spaces/libicuuc.so.78.2"
printf '%s' valid > "$WORK/app/libicuuc.so.invalid"
expect_failure libicuuc.so "$WORK/app" ''
printf '%s' broken > "$WORK/app/libssl.so.3"
expect_failure libssl.so.3 "$WORK/app" "    libssl.so.3 (libc6,x86-64) => $WORK/system/libssl.so.3"
printf '%s' invalid > "$WORK/app/libX11.so.6"
expect_failure libX11.so.6 "$WORK/app" ''
expect_failure libssl.so.3 '.' ''
# Exercise the complete launcher: only the named CLR LTTng plugin is optional.
mkdir -p "$WORK/checker"
cp "$ROOT/packaging/linux/morsecore" "$ROOT/packaging/linux/native-dependencies.sh" "$WORK/checker/"
for library in MorseCore.Desktop libMorseCore.Native.so libcoreclr.so libSkiaSharp.so libSDL2-2.0.so.0 libicuuc.so.78 libssl.so.3 libfontconfig.so.1 libX11.so.6 libXfixes.so.3; do
    printf '%s' valid > "$WORK/checker/$library"
done
printf '%s' broken > "$WORK/checker/libcoreclrtraceptprovider.so"
for utility in xdg-open xdg-settings; do
    printf '%s\n' '#!/usr/bin/env bash' 'exit 0' > "$WORK/bin/$utility"
    chmod +x "$WORK/bin/$utility"
done
if ! bash "$WORK/checker/morsecore" --check-dependencies > "$WORK/optional.log" 2>&1; then
    cat "$WORK/optional.log" >&2
    echo 'FAIL: optional LTTng dependency blocked normal application checks.' >&2
    exit 1
fi
grep -q 'optional .NET LTTng tracing provider' "$WORK/optional.log"
checks=$((checks + 1))
printf '%s' broken > "$WORK/checker/libcritical.so"
if bash "$WORK/checker/morsecore" --check-dependencies > "$WORK/critical.log" 2>&1; then
    echo 'FAIL: a missing critical dependency passed the launcher checks.' >&2
    exit 1
fi
grep -q 'libcritical.so' "$WORK/critical.log"
checks=$((checks + 1))
rm -- "$WORK/checker/libcritical.so"
printf '%s' broken > "$WORK/checker/libSDL2-2.0.so.0"
if bash "$WORK/checker/morsecore" --check-dependencies > "$WORK/sdl.log" 2>&1; then
    echo 'FAIL: missing SDL dependencies passed the launcher checks.' >&2
    exit 1
fi
grep -q 'libSDL2-2.0.so.0' "$WORK/sdl.log"
checks=$((checks + 1))
echo "PASS: $checks native dependency tests (search paths, optional LTTng, required native/SDL failures)."
