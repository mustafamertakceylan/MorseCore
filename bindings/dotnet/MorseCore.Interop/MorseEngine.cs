using System.Runtime.InteropServices;
using System.Text;

namespace MorseCore.Interop;

public sealed class MorseNativeException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed record ConversionStatistics(ulong InputCharacters, ulong OutputCharacters, ulong Words, ulong ConvertedCharacters, ulong Spaces);
public sealed record ConversionResult(string Text, ConversionStatistics Statistics);
public sealed record MorseSymbol(char Character, string Code, bool IsExtension);
public enum MorseAlphabet : uint { Latin, Russian, Arabic }

internal static unsafe class Interop
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    static Interop()
    {
        if (NativeMethods.mc_abi_version() != 1) throw new NotSupportedException("MorseCore native ABI v1 is required.");
    }
    internal static void EnsureCompatible() { }
    internal static byte[] Bytes(string value) { ArgumentNullException.ThrowIfNull(value); return Utf8.GetBytes(value); }
    internal static string String(nint value) => Marshal.PtrToStringUTF8(value) ?? "";
    internal static void Check(int status, bool audio = false)
    {
        if (status == 0) return;
        string message = String(NativeMethods.mc_status_message(status));
        if (audio && status == -103) message += ": " + String(NativeMethods.mc_audio_last_error());
        throw new MorseNativeException(status, message);
    }
    internal static string FixedString(byte* value, int capacity)
    {
        var bytes = new ReadOnlySpan<byte>(value, capacity);
        int end = bytes.IndexOf((byte)0);
        if (end < 0) throw new InvalidOperationException("Native string is not terminated.");
        return Utf8.GetString(bytes[..end]);
    }
    internal static ConversionResult ReadResult(nint pointer)
    {
        using var result = new ResultHandle(pointer);
        if (result.IsInvalid) throw new InvalidOperationException("Native conversion returned no result.");
        int status = NativeMethods.mc_result_copy_text(result, null, 0, out ulong required);
        if (status != -101) Check(status);
        byte[] bytes = new byte[checked((int)required)];
        fixed (byte* buffer = bytes) Check(NativeMethods.mc_result_copy_text(result, buffer, required, out _));
        Check(NativeMethods.mc_result_statistics(result, out NativeStats stats, (uint)sizeof(NativeStats)));
        return new(Utf8.GetString(bytes, 0, bytes.Length - 1),
            new(stats.Input, stats.Output, stats.Words, stats.Converted, stats.Spaces));
    }
}

public static unsafe class MorseEngine
{
    public static string NativeVersion { get { Interop.EnsureCompatible(); return Interop.String(NativeMethods.mc_version()); } }
    public static ConversionResult Encode(string text, char separator = '/') => Convert(text, separator, false);
    public static ConversionResult Decode(string morse, char separator = '\0') => Convert(morse, separator, true);
    public static ConversionResult Encode(string text, MorseAlphabet alphabet, char separator = '/') => ConvertProfile(text, separator, alphabet, false);
    public static ConversionResult Decode(string text, MorseAlphabet alphabet, char separator = '\0') => ConvertProfile(text, separator, alphabet, true);
    private static ConversionResult ConvertProfile(string text, char separator, MorseAlphabet alphabet, bool decode)
    {
        if (!Enum.IsDefined(alphabet)) throw new ArgumentOutOfRangeException(nameof(alphabet));
        byte[] bytes=Interop.Bytes(text);
        nint result;
        fixed(byte* input=bytes)
            Interop.Check(decode ? NativeMethods.mc_decode_alphabet(input,(ulong)bytes.Length,separator,(uint)alphabet,out result)
                : NativeMethods.mc_encode_alphabet(input,(ulong)bytes.Length,separator,(uint)alphabet,out result));
        return Interop.ReadResult(result);
    }
    public static IReadOnlyList<MorseSymbol> Alphabet(MorseAlphabet alphabet)
    {
        if (!Enum.IsDefined(alphabet)) throw new ArgumentOutOfRangeException(nameof(alphabet));
        Interop.EnsureCompatible();
        var symbols=new List<MorseSymbol>();
        for(uint i=0;i<NativeMethods.mc_alphabet_profile_count((uint)alphabet);i++)
        {
            Interop.Check(NativeMethods.mc_alphabet_profile_get((uint)alphabet,i,out NativeSymbol entry,(uint)sizeof(NativeSymbol)));
            symbols.Add(new((char)entry.Character,Interop.FixedString(entry.Code,8),entry.Extension!=0));
        }
        return symbols.AsReadOnly();
    }
    private static ConversionResult Convert(string text, char separator, bool decode)
    {
        byte[] bytes = Interop.Bytes(text);
        nint result;
        fixed (byte* input = bytes)
        {
            int status = decode ? NativeMethods.mc_decode(input, (ulong)bytes.Length, separator, out result)
                                : NativeMethods.mc_encode(input, (ulong)bytes.Length, separator, out result);
            Interop.Check(status);
        }
        return Interop.ReadResult(result);
    }
    public static IReadOnlyList<MorseSymbol> Alphabet()
    {
        Interop.EnsureCompatible();
        var symbols = new List<MorseSymbol>();
        for (uint i = 0; i < NativeMethods.mc_alphabet_count(); i++)
        {
            Interop.Check(NativeMethods.mc_alphabet_get(i, out NativeSymbol entry, (uint)sizeof(NativeSymbol)));
            symbols.Add(new((char)entry.Character, Interop.FixedString(entry.Code, 8), entry.Extension != 0));
        }
        return symbols.AsReadOnly();
    }
}
