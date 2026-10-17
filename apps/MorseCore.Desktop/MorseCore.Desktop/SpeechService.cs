using MorseCore.AppCore;

namespace MorseCore.Desktop;

internal sealed class SpeechService
{
#if MORSE_WINUI
    public static bool IsSupported=>true;
#else
    public static bool IsSupported=>OperatingSystem.IsLinux();
#endif
    private CancellationTokenSource? current;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object sync=new();
    public void Stop() { lock(sync) current?.Cancel(); }
    public async Task SpeakAsync(string text,string id,string home,CancellationToken token,bool muted=false)
    {
        if(!IsSupported) throw new PlatformNotSupportedException("Speech playback requires Windows or Linux.");
        await gate.WaitAsync(token);
        string? directory=null;
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            lock(sync) current=cancellation;
            directory=LinuxAudio.CreateTemporaryDirectory();
            string path=Path.Combine(directory,"speech.wav");
            await OfflineSpeech.SynthesizeAsync(text,id,home,path,cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
#if MORSE_WINUI
            using var stream=File.OpenRead(path);
            using var player=new Windows.Media.Playback.MediaPlayer { IsMuted=muted };
            var complete=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.MediaEnded+=(_,_)=>complete.TrySetResult();
            player.MediaFailed+=(_,_)=>complete.TrySetException(new IOException("Speech playback failed."));
            player.Source=Windows.Media.Core.MediaSource.CreateFromStream(stream.AsRandomAccessStream(),"audio/wav");
            using var registration=cancellation.Token.Register(()=>complete.TrySetCanceled(cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested(); player.Play(); await complete.Task;
#else
            await LinuxAudio.PlayWaveAsync(path,cancellation.Token,muted);
#endif
        }
        finally
        {
            lock(sync) current=null;
            if(directory!=null) LinuxAudio.DeleteTemporaryDirectory(directory);
            gate.Release();
        }
    }
}
