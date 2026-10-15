using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
namespace MorseCore.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeStats { public ulong Input, Output, Words, Converted, Spaces; }
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeSymbol { public uint Character, Extension; public fixed byte Code[8]; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativePracticeStats { public uint Total, Answered, Correct, Incorrect, Skipped, Mode; public double Accuracy; }
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeFeedback { public int Verdict; public uint Number; public fixed byte Expected[64]; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativeAudioOptions { public uint Wpm, Frequency, Rate, Reserved; }
[StructLayout(LayoutKind.Sequential)]
internal struct NativeAudioAnalysis { public ulong Units, Samples, Tones; }

internal sealed class ResultHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal ResultHandle(nint value) : base(true) { SetHandle(value); }
    protected override bool ReleaseHandle() { NativeMethods.mc_result_destroy(handle); return true; }
}
internal sealed class PracticeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal PracticeHandle(nint value) : base(true) { SetHandle(value); }
    protected override bool ReleaseHandle() { NativeMethods.mc_practice_destroy(handle); return true; }
}
internal sealed class CancellationHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal CancellationHandle(nint value) : base(true) { SetHandle(value); }
    protected override bool ReleaseHandle() { NativeMethods.mc_cancel_destroy(handle); return true; }
}

internal static unsafe partial class NativeMethods
{
    internal const string Library = "MorseCore.Native";
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint mc_abi_version();
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint mc_version();
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint mc_status_message(int status);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_encode(byte* input, ulong length, uint separator, out nint result);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_decode(byte* input, ulong length, uint separator, out nint result);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void mc_result_destroy(nint result);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_result_copy_text(ResultHandle result, byte* buffer, ulong capacity, out ulong required);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_result_statistics(ResultHandle result, out NativeStats stats, uint size);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial uint mc_alphabet_count();
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_alphabet_get(uint index, out NativeSymbol symbol, uint size);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_practice_create(uint mode, uint questions, uint seed, out nint practice);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void mc_practice_destroy(nint practice);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_practice_next(PracticeHandle practice, out nint result);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_practice_submit(PracticeHandle practice, byte* input, ulong length, out NativeFeedback feedback, uint size);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_practice_stats(PracticeHandle practice, out NativePracticeStats stats, uint size);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_audio_defaults(out NativeAudioOptions options, uint size);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_audio_analyze(byte* input, ulong length, uint separator, in NativeAudioOptions options, uint optionsSize, out NativeAudioAnalysis analysis, uint size);
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_write_wav(string path, byte* input, ulong length, uint separator, in NativeAudioOptions options, uint size);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint mc_audio_last_error();
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_cancel_create(out nint cancellation);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void mc_cancel_request(CancellationHandle cancellation);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void mc_cancel_destroy(nint cancellation);
    [LibraryImport(Library), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int mc_play(byte* input, ulong length, uint separator, in NativeAudioOptions options, uint size, CancellationHandle cancellation);
}
