namespace MorseCore.Interop;

public readonly record struct AudioOptions(uint WordsPerMinute, uint FrequencyHz, uint SampleRate = 22050);
public sealed record AudioAnalysis(ulong Units, ulong Samples, ulong Tones);

public static class MorseAudio
{
    public static unsafe AudioOptions Defaults
    {
        get
        {
            Interop.EnsureCompatible();
            Interop.Check(NativeMethods.mc_audio_defaults(out NativeAudioOptions options, (uint)sizeof(NativeAudioOptions)));
            return new(options.Wpm, options.Frequency, options.Rate);
        }
    }
    private static NativeAudioOptions Options(AudioOptions? options)
    {
        AudioOptions value = options ?? Defaults;
        return new() { Wpm = value.WordsPerMinute, Frequency = value.FrequencyHz, Rate = value.SampleRate };
    }
    public static unsafe AudioAnalysis Analyze(string morse, AudioOptions? options = null, char separator = '/')
    {
        byte[] bytes = Interop.Bytes(morse);
        NativeAudioOptions actual = Options(options);
        NativeAudioAnalysis analysis;
        fixed (byte* input = bytes)
            Interop.Check(NativeMethods.mc_audio_analyze(input, (ulong)bytes.Length, separator, in actual,
                (uint)sizeof(NativeAudioOptions), out analysis, (uint)sizeof(NativeAudioAnalysis)), audio: true);
        return new(analysis.Units, analysis.Samples, analysis.Tones);
    }
    /// <summary>Writes synchronously and replaces an existing output. Run on a worker for a GUI.</summary>
    public static unsafe void WriteWav(string path, string morse, AudioOptions? options = null, char separator = '/')
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (path.Contains('\0')) throw new ArgumentException("Path contains NUL.", nameof(path));
        _ = Interop.Bytes(path); // Reject invalid UTF-16 before the UTF-8 path marshaller runs.
        byte[] bytes = Interop.Bytes(morse);
        NativeAudioOptions actual = Options(options);
        fixed (byte* input = bytes)
            Interop.Check(NativeMethods.mc_write_wav(path, input, (ulong)bytes.Length, separator,
                in actual, (uint)sizeof(NativeAudioOptions)), audio: true);
    }
    public static Task PlayAsync(string morse, AudioOptions? options = null, char separator = '/', CancellationToken cancellationToken = default)
    {
        byte[] bytes = Interop.Bytes(morse);
        NativeAudioOptions actual = Options(options);
        return Task.Run(() => Play(bytes, actual, separator, cancellationToken));
    }
    private static unsafe void Play(byte[] bytes, NativeAudioOptions options, char separator, CancellationToken token)
    {
        Interop.Check(NativeMethods.mc_cancel_create(out nint pointer));
        using var cancellation = new CancellationHandle(pointer);
        using var registration = token.Register(() => NativeMethods.mc_cancel_request(cancellation));
        int status;
        fixed (byte* input = bytes)
            status = NativeMethods.mc_play(input, (ulong)bytes.Length, separator,
                in options, (uint)sizeof(NativeAudioOptions), cancellation);
        if (status == -104) throw new OperationCanceledException(token);
        Interop.Check(status, audio: true); // Copy thread-local error on this worker, before returning to the caller.
    }
}
