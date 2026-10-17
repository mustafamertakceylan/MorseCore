using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MorseCore.Interop;

namespace MorseCore.Desktop;

/// <summary>Linux device I/O only. C23 remains responsible for Morse timing and WAV generation.</summary>
internal static class LinuxAudio
{
    private const string Library = "libSDL2-2.0.so.0";
    private const uint AudioSubsystem = 0x10;
    private const ushort Signed16LittleEndian = 0x8010;
    private const long MaxWaveBytes = 64 * 1024 * 1024;
    private const int CaptureRate = 16000;
    private static readonly object Lifecycle = new();

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioSpec
    {
        public int Frequency;
        public ushort Format;
        public byte Channels, Silence;
        public ushort Samples, Padding;
        public uint Size;
        public nint Callback, UserData;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void AudioCallback(nint userData, nint stream, int length);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_InitSubSystem(uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern nint SDL_GetError();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_QuitSubSystem(uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_GetNumAudioDevices(int capture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern nint SDL_GetAudioDeviceName(int index, int capture);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern uint SDL_OpenAudioDevice([MarshalAs(UnmanagedType.LPUTF8Str)] string? device, int capture, in AudioSpec desired, out AudioSpec obtained, int changes);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_CloseAudioDevice(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_PauseAudioDevice(uint device, int pause);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_QueueAudio(uint device, nint data, uint length);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern uint SDL_DequeueAudio(uint device, [Out] byte[] data, uint length);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern uint SDL_GetQueuedAudioSize(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int SDL_GetAudioDeviceStatus(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_ClearQueuedAudio(uint device);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern nint SDL_RWFromFile([MarshalAs(UnmanagedType.LPUTF8Str)] string path, [MarshalAs(UnmanagedType.LPUTF8Str)] string mode);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern nint SDL_LoadWAV_RW(nint source, int freeSource, out AudioSpec spec, out nint buffer, out uint length);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void SDL_FreeWAV(nint buffer);

    private static void Initialize()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("SDL audio is available in the Linux target.");
        if (SDL_InitSubSystem(AudioSubsystem) != 0) throw AudioError("The Linux audio service could not be initialized.");
    }

    private static IOException AudioError(string message) => new(message + " " + Marshal.PtrToStringUTF8(SDL_GetError()));

    private sealed class AudioDevice : IDisposable
    {
        internal readonly uint Id;
        internal readonly AudioSpec Spec;
        private bool disposed;
        internal AudioDevice(string? name, bool capture, AudioSpec desired)
        {
            lock (Lifecycle)
            {
                Initialize();
                try
                {
                    // SDL2-compat can retain stale SDL3 instance IDs across audio
                    // subsystem restarts. Redetect before resolving a selected name.
                    if (name != null && SDL_GetNumAudioDevices(capture ? 1 : 0) < 0)
                        throw AudioError("Linux audio device enumeration failed.");
                    Id = SDL_OpenAudioDevice(name, capture ? 1 : 0, in desired, out Spec, 0);
                    if (Id == 0) throw AudioError("The selected Linux audio device could not be opened.");
                }
                catch { SDL_QuitSubSystem(AudioSubsystem); throw; }
            }
        }
        public void Dispose()
        {
            lock (Lifecycle)
            {
                if (disposed) return;
                disposed = true;
                SDL_PauseAudioDevice(Id, 1);
                SDL_CloseAudioDevice(Id); // SDL waits until the callback has finished before returning.
                SDL_QuitSubSystem(AudioSubsystem);
            }
        }
    }

    internal static string CreateTemporaryDirectory()
    {
        string path = Directory.CreateTempSubdirectory("morsecore-audio-").FullName;
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return path;
    }

    internal static void DeleteTemporaryDirectory(string directory)
    {
        try { Directory.Delete(directory, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    internal static async Task PlayMorseAsync(string morse, AudioOptions options, char separator, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        string directory = CreateTemporaryDirectory();
        try
        {
            string path = Path.Combine(directory, "morse.wav");
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                // Native validation includes the existing duration/size limits.
                MorseAudio.WriteWav(path, morse, options, separator);
                token.ThrowIfCancellationRequested();
            }, token);
            await PlayWaveAsync(path, token);
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    internal static async Task PlayWaveAsync(string path, CancellationToken token, bool muted = false)
    {
        token.ThrowIfCancellationRequested();
        if (new FileInfo(path).Length is < 44 or > MaxWaveBytes) throw new InvalidDataException("Invalid audio size.");
        nint buffer = 0;
        try
        {
            nint input = SDL_RWFromFile(path, "rb");
            if (input == 0) throw new IOException("The audio file could not be opened.");
            if (SDL_LoadWAV_RW(input, 1, out AudioSpec spec, out buffer, out uint length) == 0)
                throw new InvalidDataException("Unsupported WAV audio.");
            int bytesPerSample = spec.Format & 0xFF;
            if (spec.Frequency is < 8000 or > 192000 || spec.Channels is < 1 or > 2 || bytesPerSample is not (8 or 16 or 24 or 32 or 64) || length == 0 || length > MaxWaveBytes)
                throw new InvalidDataException("Unsupported WAV audio.");
            int alignment = spec.Channels * (bytesPerSample / 8);
            if (length % alignment != 0) throw new InvalidDataException("Incomplete WAV frame.");
            double seconds = (double)length / (spec.Frequency * alignment);
            if (seconds > 600) throw new InvalidDataException("Audio duration exceeds ten minutes.");
            token.ThrowIfCancellationRequested();
            if (muted) { await Task.Delay(TimeSpan.FromSeconds(seconds), token); return; }
            spec.Callback = 0; spec.UserData = 0;
            using var device = new AudioDevice(null, false, spec);
            SDL_PauseAudioDevice(device.Id, 0);
            var clock = Stopwatch.StartNew();
            uint offset = 0;
            uint maximumQueue = checked((uint)(spec.Frequency * alignment / 4));
            while (offset < length || SDL_GetQueuedAudioSize(device.Id) > 0)
            {
                token.ThrowIfCancellationRequested();
                if (SDL_GetAudioDeviceStatus(device.Id) == 0) throw new IOException("The Linux audio output was disconnected.");
                if (clock.Elapsed.TotalSeconds > seconds + 10) throw new IOException("The Linux audio device stopped responding.");
                if (offset < length && SDL_GetQueuedAudioSize(device.Id) < maximumQueue)
                {
                    uint chunk = Math.Min(32768u - 32768u % (uint)alignment, length - offset);
                    if (SDL_QueueAudio(device.Id, buffer + checked((int)offset), chunk) != 0)
                        throw new IOException("The Linux audio buffer could not be queued.");
                    offset += chunk;
                }
                else await Task.Delay(15, token);
            }
            // An empty SDL queue may still have one device buffer awaiting playback.
            await Task.Delay(TimeSpan.FromSeconds((double)device.Spec.Samples / spec.Frequency), token);
        }
        finally { if (buffer != 0) SDL_FreeWAV(buffer); }
    }

    internal static Task<IReadOnlyList<AudioCaptureDevice>> CaptureDevicesAsync() => Task.Run<IReadOnlyList<AudioCaptureDevice>>(() =>
    {
        lock (Lifecycle)
        {
            Initialize();
            try
            {
                int count = SDL_GetNumAudioDevices(1);
                if (count < 0) throw new IOException("Linux audio input enumeration failed.");
                var devices = new List<AudioCaptureDevice>();
                for (int i = 0; i < Math.Min(count, 128); i++)
                {
                    string? name = Marshal.PtrToStringUTF8(SDL_GetAudioDeviceName(i, 1));
                    if (!string.IsNullOrWhiteSpace(name))
                        devices.Add(new("sdl:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(name)), name));
                }
                return devices;
            }
            finally { SDL_QuitSubSystem(AudioSubsystem); }
        }
    });

    internal static async Task<string> RecordAsync(string deviceId, int maxSeconds, CancellationToken stop, CancellationToken abandon)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSeconds, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxSeconds, 120);
        if (!deviceId.StartsWith("sdl:", StringComparison.Ordinal) || deviceId.Length > 2048) throw new ArgumentException("Invalid audio device.", nameof(deviceId));
        string name;
        try { name = Encoding.UTF8.GetString(Convert.FromBase64String(deviceId[4..])); }
        catch (FormatException ex) { throw new ArgumentException("Invalid audio device.", nameof(deviceId), ex); }
        if (string.IsNullOrWhiteSpace(name) || name.Contains('\0')) throw new ArgumentException("Invalid audio device.", nameof(deviceId));
        abandon.ThrowIfCancellationRequested();
        string directory = CreateTemporaryDirectory();
        bool completed = false;
        try
        {
            string path = Path.Combine(directory, "capture.wav");
            var desired = new AudioSpec { Frequency = CaptureRate, Format = Signed16LittleEndian, Channels = 1, Samples = 512 };
            using var device = new AudioDevice(name, true, desired);
            using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using var writer = new BinaryWriter(output, Encoding.ASCII, true);
            WriteCaptureHeader(writer, 0);
            SDL_PauseAudioDevice(device.Id, 0);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop, abandon);
            var clock = Stopwatch.StartNew();
            byte[] chunk = new byte[4096];
            uint written = 0, maximum = checked((uint)(CaptureRate * 2 * maxSeconds));
            void Drain()
            {
                uint queued = SDL_GetQueuedAudioSize(device.Id);
                if (queued > 1024 * 1024) throw new IOException("Audio capture buffer exceeded its limit.");
                while (queued > 0 && written < maximum)
                {
                    uint requested = Math.Min((uint)chunk.Length, Math.Min(queued, maximum - written));
                    requested &= ~1u;
                    if (requested == 0) break;
                    uint received = SDL_DequeueAudio(device.Id, chunk, requested);
                    if (received == 0) break;
                    if (received > requested || (received & 1) != 0) throw new IOException("Invalid capture buffer.");
                    writer.Write(chunk, 0, (int)received); written += received;
                    queued = SDL_GetQueuedAudioSize(device.Id);
                }
            }
            while (clock.Elapsed.TotalSeconds < maxSeconds && written < maximum && !stop.IsCancellationRequested)
            {
                abandon.ThrowIfCancellationRequested();
                if (SDL_GetAudioDeviceStatus(device.Id) == 0) throw new IOException("The Linux audio input was disconnected.");
                Drain();
                try { await Task.Delay(20, linked.Token); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested && !abandon.IsCancellationRequested) { break; }
            }
            SDL_PauseAudioDevice(device.Id, 1);
            abandon.ThrowIfCancellationRequested();
            Drain();
            SDL_ClearQueuedAudio(device.Id);
            if (written == 0) throw new IOException("The Linux audio input produced no samples.");
            output.Position = 0; WriteCaptureHeader(writer, written); writer.Flush();
            completed = true;
            return path;
        }
        finally { if (!completed) DeleteTemporaryDirectory(directory); }
    }

    private static void WriteCaptureHeader(BinaryWriter writer, uint bytes)
    {
        writer.Write("RIFF"u8); writer.Write(36u + bytes); writer.Write("WAVEfmt "u8);
        writer.Write(16u); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write(CaptureRate); writer.Write(CaptureRate * 2);
        writer.Write((ushort)2); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(bytes);
    }

    internal static Sidetone? CreateSidetone(uint frequency)
    {
        if (!OperatingSystem.IsLinux()) return null;
        try { return new Sidetone(frequency); }
        catch (Exception ex) when (ex is IOException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { return null; }
    }

    /// <summary>One reusable SDL callback, with no task/process allocation for a key press.</summary>
    internal sealed class Sidetone : IDisposable
    {
        private readonly AudioCallback callback;
        private readonly AudioDevice device;
        private readonly object sync = new();
        private readonly double increment;
        private double phase, amplitude;
        private volatile bool pressed;
        private bool disposed;
        internal Sidetone(uint frequency)
        {
            increment = 2 * Math.PI * Math.Clamp(frequency, 200u, 2000u) / 22050;
            callback = Fill;
            var desired = new AudioSpec { Frequency = 22050, Format = Signed16LittleEndian, Channels = 1, Samples = 128, Callback = Marshal.GetFunctionPointerForDelegate(callback) };
            device = new AudioDevice(null, false, desired);
            SDL_PauseAudioDevice(device.Id, 0);
        }
        public bool Start() { lock (sync) { if (disposed) return false; pressed = true; return true; } }
        public void Stop() { lock (sync) { pressed = false; } }
        private unsafe void Fill(nint _, nint stream, int length)
        {
            if (length <= 0) return;
            var bytes = new Span<byte>((void*)stream, length);
            bytes.Clear();
            var samples = MemoryMarshal.Cast<byte, short>(bytes[..(length & ~1)]);
            for (int i = 0; i < samples.Length; i++)
            {
                amplitude += Math.Clamp((pressed ? 5000 : 0) - amplitude, -75, 75);
                samples[i] = (short)(amplitude * Math.Sin(phase));
                phase += increment; if (phase >= 2 * Math.PI) phase -= 2 * Math.PI;
            }
        }
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true; pressed = false; device.Dispose();
                GC.KeepAlive(callback);
            }
        }
    }
}
