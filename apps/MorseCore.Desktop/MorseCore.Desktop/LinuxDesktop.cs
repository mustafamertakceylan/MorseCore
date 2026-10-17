using System.Diagnostics;

namespace MorseCore.Desktop;

/// <summary>OS handoff only; release selection, downloading and hashes stay in AppCore.</summary>
internal static class LinuxDesktop
{
    internal static void OpenDirectory(string path)
    {
        if(!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        if(!Path.IsPathFullyQualified(path) || !Directory.Exists(path)) throw new IOException("Invalid update directory.");
        var start=new ProcessStartInfo("xdg-open") { UseShellExecute=false };
        start.ArgumentList.Add(path);
        using var process=Process.Start(start) ?? throw new IOException("File manager could not be opened.");
    }
}
