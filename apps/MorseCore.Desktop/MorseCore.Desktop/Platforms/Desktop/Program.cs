using Uno.UI.Hosting;

namespace MorseCore.Desktop;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();
        var registration=LinuxInstanceGate.Acquire().GetAwaiter().GetResult();
        using var instance=registration.Gate;
        if(!registration.Continue) return;

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11(builder =>
            {
                // Keep the shared 2D UI stable on Mesa/VM drivers, including
                // RenderTargetBitmap. Hardware rendering is an explicit opt-in.
                if(Environment.GetEnvironmentVariable("MORSECORE_LINUX_RENDERER")!="auto")
                    builder.RenderingBackend(X11RenderingBackend.Software);
            })
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
