#if !MORSE_WINUI
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace MorseCore.Desktop;

// A per-user, per-display lock lives for the process lifetime. The pipe carries
// only activation requests; preferences and radio records never cross IPC.
internal sealed class LinuxInstanceGate : IDisposable
{
    private readonly FileStream instanceLock;
    private readonly CancellationTokenSource lifetime=new();
    private readonly NamedPipeServerStream server;
    private readonly Task listener;

    private LinuxInstanceGate(FileStream instanceLock,string pipeName)
    {
        this.instanceLock=instanceLock;
        server=new NamedPipeServerStream(pipeName,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        listener=Listen();
    }

    internal static async Task<(LinuxInstanceGate? Gate,bool Continue)> Acquire()
    {
        if(!OperatingSystem.IsLinux()) return (null,true);
        string? isolatedHome=Environment.GetEnvironmentVariable("MORSECORE_DESKTOP_HOME");
        string home=!string.IsNullOrEmpty(isolatedHome) && Path.IsPathFullyQualified(isolatedHome)
            ? isolatedHome : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string identity=home+"\n"+Environment.UserName+"\n"+Environment.GetEnvironmentVariable("DISPLAY");
        string suffix=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        string pipeName="MorseCore-"+suffix;
        string directory=Path.Combine(home,"MorseCore","Instance");
        Directory.CreateDirectory(directory,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        FileStream stream;
        try
        {
            stream=new FileStream(Path.Combine(directory,suffix+".lock"),new FileStreamOptions
            {
                Mode=FileMode.OpenOrCreate,Access=FileAccess.ReadWrite,Share=FileShare.None,
                UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite
            });
        }
        catch(IOException lockError) when((lockError.HResult&0xffff) is 11 or 32 or 33)
        {
            using var client=new NamedPipeClientStream(".",pipeName,PipeDirection.Out,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await client.ConnectAsync(timeout.Token);
                await client.WriteAsync(new byte[] { 1 },timeout.Token);
                await client.FlushAsync(timeout.Token);
            }
            catch(Exception ex) when(ex is IOException or OperationCanceledException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("MorseCore is already running, but its window could not be activated.");
            }
            return (null,false);
        }
        try { return (new LinuxInstanceGate(stream,pipeName),true); }
        catch { stream.Dispose(); throw; }
    }

    private async Task Listen()
    {
        var message=new byte[1];
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                await server.WaitForConnectionAsync(lifetime.Token);
                try
                {
                    using var requestTimeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    requestTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                    if(await server.ReadAsync(message,requestTimeout.Token)==1 && message[0]==1)
                        App.ActivateLinuxInstance();
                }
                catch(OperationCanceledException) when(!lifetime.IsCancellationRequested) { }
                catch(IOException) { }
                finally { if(server.IsConnected) server.Disconnect(); }
            }
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { }
        catch(ObjectDisposedException) when(lifetime.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        lifetime.Cancel();
        server.Dispose();
        try { listener.GetAwaiter().GetResult(); }
        catch(IOException) { }
        finally { instanceLock.Dispose(); lifetime.Dispose(); }
    }
}
#endif
