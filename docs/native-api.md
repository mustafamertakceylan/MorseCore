# MorseCore native API

[Türkçe](native-api.tr.md) · [Public header](../include/morsecore_native.h)

MorseCore 0.2.0 provides ABI v1 through `MorseCore.Native.dll` on Windows and
`libMorseCore.Native.so` on Linux. The shared engine contains no console renderer,
settings loader or runtime-home initialization. Loading it has no filesystem side effects.

## Build

Native requirements: CMake 3.28+, Ninja and GCC 15+ or Clang 18+ with the C23
features checked at configure time. C23 mode is required, with compiler extensions off.

```powershell
cmake --preset debug
cmake --build --preset debug
ctest --preset debug
```

The `clang-debug` preset uses `clang`. On Windows, invoke it from a Visual Studio
x64 developer prompt so Clang can find Windows SDK headers, libraries and the linker.
Microsoft-compatible builds use the static CRT. MinGW builds link compiler runtimes
statically. Windows system DLLs are still required. The CLI links the common C23 engine
statically and can run without the native DLL.

## ABI and ownership

- Include only `morsecore_native.h` in external native consumers. C++ callers are supported
  through `extern "C"`; the C++ compiler does not need C23.
- Check `mc_abi_version() == MC_ABI_VERSION` before calling other functions. ABI v1
  uses cdecl, fixed-width integers and natural struct alignment. Struct layouts are frozen;
  changing them requires another ABI. The process architecture must match the library.
- Status `MC_OK` is zero. Other results are named constants in the header. Status messages
  are borrowed static strings. They do not require freeing.
- Inputs are explicit byte spans: strict UTF-8, no embedded NUL, no trailing NUL required.
  A leading BOM is accepted. Unlike the console, the DLL does not accept Windows-1254.
  Byte lengths and character statistics are different quantities.
- `mc_encode`, `mc_decode` and `mc_practice_next` return opaque result handles.
  Query `mc_result_copy_text` with NULL/0 to obtain the required capacity **including NUL**;
  then pass your own buffer. A short buffer is not changed. Encoded text retains the
  engine's trailing space. Call `mc_result_destroy` even if copying or later host work fails.
- Create/destroy practice and cancellation handles with their matching DLL functions.
  Never pass a handle to `free`, `delete`, `Marshal.FreeHGlobal` or another DLL version.
  Destroying NULL is allowed. Double destruction and use after destruction are invalid.
- Struct getters take caller capacity in bytes. Pass `sizeof(your_struct)`; short
  structures are rejected before any write. The header describes all fields and sizes.
- Keep the library loaded until every handle and call has finished. No handle destruction
  may race another use. Independent sessions and stateless conversions can run concurrently;
  serialize operations on the same practice session and writes to the same output path.

## Services

| Service | Entry points |
|---|---|
| Conversion and statistics | `mc_encode`, `mc_decode`, `mc_result_copy_text`, `mc_result_statistics` |
| Reference table | `mc_alphabet_count`, `mc_alphabet_get` |
| Practice | `mc_practice_create`, `mc_practice_next`, `mc_practice_submit`, `mc_practice_stats` |
| Audio | `mc_audio_defaults`, `mc_audio_analyze`, `mc_write_wav`, `mc_play` |
| Playback cancellation | `mc_cancel_create`, `mc_cancel_request`, `mc_cancel_destroy` |

Separator zero means `/` for encoding/audio and automatic detection for decoding.
Practice returns Morse questions; the expected answer is returned only after submission.
An empty answer skips. A failed submission or allocation does not consume the question.

Calls are synchronous. Schedule conversion, file work and playback away from a GUI's UI
thread. Playback supports a one-shot cancellation handle, which can be requested from
another thread. A pre-cancelled call does not open a sound device. Active Windows playback
polls cancellation and resets its queued buffers. Keep the token alive until playback and
all cancellation requests finish. Live playback is Windows-only; WAV export is portable.

WAV export explicitly replaces its output path and is not cancellable. Hosts must handle
overwrite confirmation and partial-output failures. WAV paths are NUL-terminated UTF-8.
`mc_audio_last_error` is local to the calling thread: copy it immediately after
`MC_AUDIO_ERROR`, before switching threads or making another audio call.

## .NET consumer

`bindings/dotnet/MorseCore.Interop` targets .NET 10 without WinUI dependencies.
It uses source-generated `LibraryImport`, cdecl signatures, strict UTF-8 and `SafeHandle`.
Results are copied into managed records before native handles are disposed. Practice
calls/disposal are serialized. `MorseAudio.PlayAsync` runs on a worker and maps
`CancellationToken` to the native cancellation handle. No XAML or UI design is included.

```powershell
cmake --preset interop
cmake --build --preset interop
ctest --preset interop
```

The smoke consumer loads the specified real library and checks conversion, statistics,
errors, concurrency, practice, WAV and pre-cancellation without audio hardware. In a future
desktop deployment, put the matching native DLL beside the app. The binding uses safe
native-library search paths. .NET is optional for the C build.
