namespace MorseCore.Desktop;

internal sealed record AudioCaptureDevice(string Id, string Name);

internal static class MorseAudioCapture
{
    public const int MaxSeconds = 30;

#if MORSE_WINUI
    private static readonly SemaphoreSlim CaptureGate = new(1, 1);
    public static async Task<IReadOnlyList<AudioCaptureDevice>> DevicesAsync()
    {
        var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(Windows.Devices.Enumeration.DeviceClass.AudioCapture);
        return devices.Select(d => new AudioCaptureDevice(d.Id, d.Name)).ToArray();
    }

    /// <summary>Records only after the user requests it. Both stop and navigation release the capture device.</summary>
    public static async Task<string> RecordAsync(string deviceId, CancellationToken stop, CancellationToken abandon)
    {
        string path = Path.Combine(Path.GetTempPath(), "morsecore-capture-" + Guid.NewGuid().ToString("N") + ".wav");
        await CaptureGate.WaitAsync(abandon);
        Windows.Media.Capture.MediaCapture? capture = null;
        bool started = false, completed = false;
        try
        {
            abandon.ThrowIfCancellationRequested();
            capture = new Windows.Media.Capture.MediaCapture();
            await capture.InitializeAsync(new Windows.Media.Capture.MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Audio,
                AudioDeviceId = deviceId,
                MediaCategory = Windows.Media.Capture.MediaCategory.Other
            });
            abandon.ThrowIfCancellationRequested();
            await File.WriteAllBytesAsync(path, [], abandon);
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            var profile = Windows.Media.MediaProperties.MediaEncodingProfile.CreateWav(Windows.Media.MediaProperties.AudioEncodingQuality.Low);
            profile.Audio.ChannelCount = 1; profile.Audio.SampleRate = 16000; profile.Audio.BitsPerSample = 16;
            await capture.StartRecordToStorageFileAsync(profile, file); started = true;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop, abandon);
            try { await Task.Delay(TimeSpan.FromSeconds(MaxSeconds), linked.Token); }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
            await capture.StopRecordAsync(); started = false;
            abandon.ThrowIfCancellationRequested();
            completed = true;
            return path;
        }
        finally
        {
            if (started && capture != null)
            {
                try { await capture.StopRecordAsync(); }
                catch (Exception) { /* Dispose releases the device after a failed or disconnected capture. */ }
            }
            try { capture?.Dispose(); }
            finally
            {
                if (!completed) Delete(path);
                CaptureGate.Release();
            }
        }
    }
#else
    private static readonly SemaphoreSlim CaptureGate = new(1, 1);
    public static Task<IReadOnlyList<AudioCaptureDevice>> DevicesAsync() => OperatingSystem.IsLinux()
        ? LinuxAudio.CaptureDevicesAsync() : Task.FromResult<IReadOnlyList<AudioCaptureDevice>>([]);
    public static async Task<string> RecordAsync(string deviceId, CancellationToken stop, CancellationToken abandon)
    {
        if(!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        await CaptureGate.WaitAsync(abandon);
        try { return await LinuxAudio.RecordAsync(deviceId, MaxSeconds, stop, abandon); }
        finally { CaptureGate.Release(); }
    }
#endif

    public static void Delete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if(OperatingSystem.IsLinux() && Path.GetFileName(path)=="capture.wav")
        {
            string? directory=Path.GetDirectoryName(path);
            if(directory!=null && Path.GetFileName(directory).StartsWith("morsecore-audio-",StringComparison.Ordinal))
            {
                // Delete only the now-empty private capture directory, never a caller's unrelated files.
                try { Directory.Delete(directory); }
                catch(IOException) { }
                catch(UnauthorizedAccessException) { }
            }
        }
    }
}
