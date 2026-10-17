using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MorseCore.AppCore.Decoding;
using MorseCore.Desktop;

// Unix Environment.SetEnvironmentVariable changes the managed environment only.
// Start a child with the dummy driver in its initial native environment so this
// test never opens a real speaker/microphone when invoked directly via dotnet.
if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("SDL_AUDIODRIVER") != "dummy")
{
    string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process executable");
    var start = new ProcessStartInfo(executable) { UseShellExecute = false };
    if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach (string argument in args) start.ArgumentList.Add(argument);
    start.Environment["SDL_AUDIODRIVER"] = "dummy";
    using var child = Process.Start(start) ?? throw new InvalidOperationException("Audio smoke child did not start");
    await child.WaitForExitAsync();
    return child.ExitCode;
}

int checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    checks++;
}
async Task Throws<T>(Func<Task> action, string description) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException(description);
}
var flags = BindingFlags.NonPublic | BindingFlags.Static;
var header = typeof(LinuxAudio).GetMethod("WriteCaptureHeader", flags)!;
byte[] Capture(uint bytes)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    header.Invoke(null, [writer, bytes]);
    writer.Write(new byte[bytes]); writer.Flush();
    return stream.ToArray();
}
foreach (int seconds in new[] { 1, 30, 120 })
{
    byte[] bytes = Capture((uint)(seconds * 32000));
    Check(bytes.Length == seconds * 32000 + 44, "PCM header length");
    using var stream = new MemoryStream(bytes);
    var wave = WaveAudio.Read(stream);
    Check(wave.SampleRate == 8000, "Capture is compatible with shared decoder");
    Check(wave.Duration == TimeSpan.FromSeconds(seconds), "Capture duration roundtrip");
}
await Throws<ArgumentOutOfRangeException>(() => LinuxAudio.RecordAsync("invalid", 0, default, default), "Zero capture duration must be rejected");
await Throws<ArgumentOutOfRangeException>(() => LinuxAudio.RecordAsync("invalid", 121, default, default), "Overlong capture must be rejected");
await Throws<ArgumentException>(() => LinuxAudio.RecordAsync("sdl:%%%", 30, default, default), "Malformed device ID must be rejected");
await Throws<ArgumentException>(() => LinuxAudio.RecordAsync("sdl:AA==", 30, default, default), "NUL device name must be rejected");
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    await Throws<OperationCanceledException>(() => LinuxAudio.PlayWaveAsync("missing.wav", cancelled.Token), "Playback must observe precancellation before touching files or SDL");
    await Throws<OperationCanceledException>(() => LinuxAudio.PlayMorseAsync("...", new(20, 600), '/', cancelled.Token), "Morse must observe precancellation");
}
// Exercise the native callback's PCM envelope without opening any user audio device.
var sidetoneType = typeof(LinuxAudio.Sidetone);
var tone = (LinuxAudio.Sidetone)RuntimeHelpers.GetUninitializedObject(sidetoneType);
sidetoneType.GetField("increment", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tone, 2 * Math.PI * 600 / 22050);
sidetoneType.GetField("pressed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tone, true);
var fill = sidetoneType.GetMethod("Fill", BindingFlags.Instance | BindingFlags.NonPublic)!;
nint memory = Marshal.AllocHGlobal(1025);
try
{
    fill.Invoke(tone, [(nint)0, memory, 1025]);
    byte[] pcm = new byte[1025]; Marshal.Copy(memory, pcm, 0, pcm.Length);
    Check(pcm[^1] == 0, "Odd trailing byte must be cleared");
    short[] samples = new short[512]; Buffer.BlockCopy(pcm, 0, samples, 0, 1024);
    Check(samples.Any(s => s != 0), "Held paddle generates audio");
    Check(samples.All(s => Math.Abs((int)s) <= 5000), "Sidetone amplitude remains bounded");
    sidetoneType.GetField("pressed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(tone, false);
    fill.Invoke(tone, [(nint)0, memory, 1024]);
    Marshal.Copy(memory, pcm, 0, 1024);
    Check(pcm.AsSpan(1020, 4).SequenceEqual(new byte[4]), "Released paddle fades to silence");
}
finally { Marshal.FreeHGlobal(memory); }

string directory = LinuxAudio.CreateTemporaryDirectory();
try
{
    string invalid = Path.Combine(directory, "invalid.wav");
    await File.WriteAllBytesAsync(invalid, new byte[12]);
    await Throws<InvalidDataException>(() => LinuxAudio.PlayWaveAsync(invalid, default), "Undersized WAV must fail before SDL");
    Check(File.Exists(invalid), "Playback does not delete caller files");
    if (OperatingSystem.IsLinux())
    {
        Check((File.GetUnixFileMode(directory) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) == 0, "Temporary audio is private");
        // The process starts with SDL's native dummy driver; no user device opens.
        string valid = Path.Combine(directory, "valid.wav");
        await File.WriteAllBytesAsync(valid, Capture(3200));
        await LinuxAudio.PlayWaveAsync(valid, default); checks++;
        await LinuxAudio.PlayWaveAsync(valid, default, muted: true); checks++;
        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25)))
            await Throws<OperationCanceledException>(() => LinuxAudio.PlayWaveAsync(valid, cancellation.Token), "Playback cancellation closes device");
        var devices = await MorseAudioCapture.DevicesAsync();
        Check(devices.Count > 0, "SDL dummy capture enumeration");
        // The stop timer starts before async SDL initialization, thread startup and
        // first-use JIT. Allow those costs plus several 32 ms capture buffers;
        // this checks explicit stop, rather than stopping before capture begins.
        using (var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            string recorded = await MorseAudioCapture.RecordAsync(devices[0].Id, stop.Token, default);
            try
            {
                var wave = WaveAudio.Read(recorded);
                Check(wave.Duration.TotalSeconds is > 0 and <= 30, "Stop returns bounded decoder-compatible PCM");
            }
            finally { MorseAudioCapture.Delete(recorded); }
            Check(!Directory.Exists(Path.GetDirectoryName(recorded)), "Capture file and private directory cleaned up");
        }
        using (var abandon = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
            await Throws<OperationCanceledException>(() => MorseAudioCapture.RecordAsync(devices[0].Id, default, abandon.Token), "Abandoned capture releases input");
        using var actualTone = LinuxAudio.CreateSidetone(700);
        Check(actualTone != null && actualTone.Start(), "SDL sidetone starts");
        await Task.Delay(25); actualTone!.Stop(); actualTone.Dispose();
        Check(!actualTone.Start(), "Disposed sidetone cannot restart");
    }
    else Console.WriteLine("SDL device checks skipped: host is not Linux.");
}
finally { LinuxAudio.DeleteTemporaryDirectory(directory); }
Check(!Directory.Exists(directory), "Private temporary directory cleaned up");
Console.WriteLine($"Linux audio smoke passed: {checks} checks.");
return 0;
