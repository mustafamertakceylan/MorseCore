using MorseCore.AppCore;

namespace MorseCore.Desktop;

internal sealed class SpeechService
{
#if MORSE_WINUI
    public static bool IsSupported=>true;
    private CancellationTokenSource? current;
    private readonly SemaphoreSlim gate=new(1,1);
    public void Stop()=>current?.Cancel();
#else
    public static bool IsSupported=>false;
    public void Stop() { }
#endif
    public async Task SpeakAsync(string text,string id,string home,CancellationToken token,bool muted=false)
    {
#if MORSE_WINUI
        await gate.WaitAsync(token);
        string? path=null;
        try
        {
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(token); current=cancellation;
            string directory=Path.Combine(Path.GetTempPath(),"MorseCore-Speech");
            Directory.CreateDirectory(directory);
            path=Path.Combine(directory,Guid.NewGuid().ToString("N")+".wav");
            await OfflineSpeech.SynthesizeAsync(text,id,home,path,cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            using var stream=File.OpenRead(path);
            using var player=new Windows.Media.Playback.MediaPlayer { IsMuted=muted };
            var complete=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            player.MediaEnded+=(_,_)=>complete.TrySetResult();
            player.MediaFailed+=(_,_)=>complete.TrySetException(new IOException("Speech playback failed."));
            player.Source=Windows.Media.Core.MediaSource.CreateFromStream(stream.AsRandomAccessStream(),"audio/wav");
            using var registration=cancellation.Token.Register(()=>complete.TrySetCanceled(cancellation.Token));
            cancellation.Token.ThrowIfCancellationRequested(); player.Play(); await complete.Task;
        }
        finally { try { if(path!=null && File.Exists(path)) File.Delete(path); } finally { current=null; gate.Release(); } }
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Speech playback is currently available in the Windows target.");
#endif
    }
}
