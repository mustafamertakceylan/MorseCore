using System.Diagnostics;
using System.Text;
using Microsoft.Win32;

namespace MorseCore.AppCore.SpeechPackages;

public sealed partial class VoicePackageManager
{
    public const string PythonInstallerName="python-3.13.16-amd64.exe";
    public const long PythonInstallerBytes=29935584;
    private const string PythonSha256="fb4f9f5d438b2396da0086dc70b935c530cb578e37adc6d354f7ad2037fee83b";

    public async Task<string?> FindPythonAsync(CancellationToken token=default)
    {
        var candidates=new List<string> { OfflineSpeech.PythonPath(Home) };
        if(OperatingSystem.IsWindows())
        {
            for(int minor=13;minor>=11;minor--)
                foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
                {
                    using var registry=RegistryKey.OpenBaseKey(hive,RegistryView.Registry64);
                    foreach(string tag in new[]{"3."+minor,"3."+minor+"-64"})
                    {
                        using var key=registry.OpenSubKey("Software\\Python\\PythonCore\\"+tag+"\\InstallPath");
                        if(key?.GetValue("ExecutablePath") is string executable) candidates.Add(executable);
                        else if(key?.GetValue("") is string directory) candidates.Add(Path.Combine(directory,"python.exe"));
                    }
                }
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Python","Python313","python.exe"));
        }
        foreach(string path in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator,StringSplitOptions.RemoveEmptyEntries))
            if(Path.IsPathFullyQualified(path) && !path.Contains("WindowsApps",StringComparison.OrdinalIgnoreCase))
                foreach(string name in OperatingSystem.IsWindows()?new[]{"python.exe"}:new[]{"python3","python"}) candidates.Add(Path.Combine(path,name));
        foreach(string candidate in candidates.Distinct(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            if(await IsCompatiblePythonAsync(candidate,token).ConfigureAwait(false)) return candidate;
        }
        return null;
    }

    public static async Task<bool> IsCompatiblePythonAsync(string candidate,CancellationToken token=default)
    {
        if(!Path.IsPathFullyQualified(candidate) || !File.Exists(candidate) || candidate.Contains("WindowsApps",StringComparison.OrdinalIgnoreCase)) return false;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            string probe="import sys,struct,venv,ensurepip,sysconfig; sys.exit(0 if (3,11)<=sys.version_info[:2]<(3,14) and struct.calcsize('P')==8 and not sysconfig.get_config_var('Py_GIL_DISABLED') else 1)";
            return await RunProcessAsync(candidate,["-I","-S","-c",probe],null,deadline.Token).ConfigureAwait(false)==0;
        }
        catch(OperationCanceledException) when(!token.IsCancellationRequested) { return false; }
        catch(Exception e) when(e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    /// <summary>Call only after explicit GUI consent. Never requests elevation or changes PATH.</summary>
    public async Task<string> InstallPythonAsync(IProgress<VoicePackageProgress>? progress=null,CancellationToken token=default)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Automatic runtime installation currently targets Windows.");
        using var lease=AcquireLease();
        if(await FindPythonAsync(token).ConfigureAwait(false) is { } existing) return existing;
        string installer=OwnedPath(ToolDirectory+"/"+PythonInstallerName);
        var spec=new VoiceModelFile { Url="https://www.python.org/ftp/python/3.13.16/"+PythonInstallerName,Path=ToolDirectory+"/"+PythonInstallerName,Bytes=PythonInstallerBytes,Sha256=PythonSha256,GitBlob="" };
        progress?.Report(new("runtimeDownload",0,PythonInstallerBytes));
        if(!await MatchesAsync(installer,spec,token).ConfigureAwait(false))
        {
            string? cached=await FindCachedInstallerAsync(spec,token).ConfigureAwait(false);
            if(cached!=null)
            {
                EnsureDirectory(Path.GetDirectoryName(installer)!);
                string temporary=OwnedPath(ToolDirectory+"/"+Guid.NewGuid().ToString("N")+".tmp");
                try { File.Copy(cached,temporary); if(!await MatchesAsync(temporary,spec,token).ConfigureAwait(false)) throw new IOException("Python installer integrity check failed."); File.Move(temporary,installer,true); }
                finally { if(File.Exists(temporary)) File.Delete(temporary); }
            }
            else await DownloadAsync(new Uri(spec.Url),installer,PythonInstallerBytes,PythonSha256,"",bytes=>progress?.Report(new("runtimeDownload",bytes,PythonInstallerBytes)),token).ConfigureAwait(false);
        }
        token.ThrowIfCancellationRequested();
        progress?.Report(new("runtimeInstall"));
        // Once MSI installation starts, let the official installer finish or cancel itself.
        // Killing the tree midway can leave a partially registered system component.
        var start=new ProcessStartInfo(installer) { UseShellExecute=false,WindowStyle=ProcessWindowStyle.Normal };
        foreach(string argument in new[]{"/passive","/norestart","InstallAllUsers=0","Include_launcher=0","InstallLauncherAllUsers=0","Include_test=0","Include_doc=0","Include_tcltk=0","Include_pip=1","Include_dev=1","Include_exe=1","Include_lib=1","PrependPath=0","AppendPath=0","AssociateFiles=0","Shortcuts=0","/log",OwnedPath(ToolDirectory+"/python-install.log")}) start.ArgumentList.Add(argument);
        using var process=Process.Start(start) ?? throw new IOException("Python installer did not start.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        if(process.ExitCode is not (0 or 3010)) throw new IOException("Python installation was cancelled or failed.");
        return await FindPythonAsync(CancellationToken.None).ConfigureAwait(false) ?? throw new IOException("Installed Python could not be verified.");
    }

    private static async Task<string?> FindCachedInstallerAsync(VoiceModelFile spec,CancellationToken token)
    {
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
            foreach(string candidate in new[]{Path.Combine(directory.FullName,"prerequisites",PythonInstallerName),Path.Combine(directory.FullName,"cmake-build-installer-tools","prerequisites",PythonInstallerName)})
                if(await MatchesAsync(candidate,spec,token).ConfigureAwait(false)) return candidate;
        return null;
    }

    private static async Task RunPythonAsync(string python,IEnumerable<string> arguments,string log,CancellationToken token)
    {
        int code=await RunProcessAsync(python,arguments,log,token).ConfigureAwait(false);
        if(code!=0) throw new IOException("Speech engine installation failed. See .voice-tools/setup.stdout.log and setup.stderr.log in the speech folder.");
    }

    private static async Task<int> RunProcessAsync(string executable,IEnumerable<string> arguments,string? log,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var start=new ProcessStartInfo(executable) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
        foreach(string argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["PYTHONUTF8"]="1"; start.Environment["PIP_DISABLE_PIP_VERSION_CHECK"]="1";
        using var process=new Process { StartInfo=start };
        if(!process.Start()) throw new IOException("Speech package process did not start.");
        using var registration=token.Register(()=> { try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) { } });
        Task output=DrainAsync(process.StandardOutput,log is null ? null:log+".stdout.log");
        Task error=DrainAsync(process.StandardError,log is null ? null:log+".stderr.log");
        try { await process.WaitForExitAsync(token).ConfigureAwait(false); await Task.WhenAll(output,error).ConfigureAwait(false); token.ThrowIfCancellationRequested(); return process.ExitCode; }
        finally
        {
            try { if(!process.HasExited) { process.Kill(true); await process.WaitForExitAsync().ConfigureAwait(false); } } catch(InvalidOperationException) { }
            await Task.WhenAll(output,error).ConfigureAwait(false);
        }
    }

    private static async Task DrainAsync(StreamReader reader,string? path)
    {
        await using var writer=path is null ? null:new StreamWriter(new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));
        char[] buffer=new char[4096]; int read; long count=0;
        while((read=await reader.ReadAsync(buffer).ConfigureAwait(false))>0)
        {
            if(writer!=null && count<2*1024*1024) await writer.WriteAsync(buffer.AsMemory(0,(int)Math.Min(read,2*1024*1024-count))).ConfigureAwait(false);
            count+=read;
        }
    }
}
