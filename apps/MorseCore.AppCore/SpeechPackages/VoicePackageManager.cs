using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace MorseCore.AppCore.SpeechPackages;

/// <summary>User-initiated package management. No constructor or synthesis path accesses the network.</summary>
public sealed partial class VoicePackageManager(string home,HttpClient http)
{
    public string Home { get; }=ValidateHome(home);
    private const string ToolDirectory=".voice-tools";

    public async Task InstallAsync(string voiceId,IProgress<VoicePackageProgress>? progress=null,CancellationToken token=default)
    {
        var package=VoicePackageCatalog.Get(voiceId);
        using var lease=AcquireLease();
        string? python=await FindPythonAsync(token).ConfigureAwait(false);
        if(python is null) throw new FileNotFoundException(OperatingSystem.IsLinux()
            ? "Offline voices require 64-bit Python 3.11–3.13 with venv and ensurepip. Install the distribution's matching python3-venv package (Debian/Ubuntu/Pardus); no system packages are installed automatically."
            : "A compatible Python runtime is required.");
        ExtractTools();
        foreach(string path in new[]{"speech-models.json","installed-packages.txt",ToolDirectory+"/setup.stdout.log",ToolDirectory+"/setup.stderr.log"}) OwnedPath(path);
        progress?.Report(new("dependencies"));
        await RunPythonAsync(python,["-X","utf8","-I",OwnedPath(ToolDirectory+"/setup-speech.py"),"--home",Home,"--voice",voiceId,"--packages-only"],OwnedPath(ToolDirectory+"/setup"),token).ConfigureAwait(false);
        long completed=0;
        foreach(var file in package.Files)
        {
            token.ThrowIfCancellationRequested();
            progress?.Report(new("download",completed,package.DownloadBytes,file.Path));
            await EnsureModelAsync(file,bytes=>progress?.Report(new("download",completed+bytes,package.DownloadBytes,file.Path)),token).ConfigureAwait(false);
            completed+=file.Bytes;
        }
        // Old all-in-one packs and new per-voice packs use the same discovery marker.
        WriteAtomic("pack-ready.json","{\"schema\":1,\"offline\":true}\n");
        if(!OfflineSpeech.IsInstalled(Home,package.Voice)) throw new IOException("Voice package verification failed.");
        progress?.Report(new("complete",completed,package.DownloadBytes));
    }

    internal async Task EnsureModelAsync(VoiceModelFile file,Action<long>? progress=null,CancellationToken token=default)
    {
        if(!VoicePackageCatalog.Packages.Any(package=>package.Files.Any(known=>ReferenceEquals(known,file)))) throw new ArgumentException("Only bundled voice catalog files may be downloaded.",nameof(file));
        string destination=OwnedPath(file.Path);
        if(!await MatchesAsync(destination,file,token).ConfigureAwait(false))
            await DownloadAsync(new Uri(file.Url),destination,file.Bytes,file.Sha256,file.GitBlob,progress,token).ConfigureAwait(false);
    }

    /// <summary>Deletes only files in the pinned package manifest, after verifying every existing file.</summary>
    public async Task RemoveAsync(string voiceId,CancellationToken token=default)
    {
        var package=VoicePackageCatalog.Get(voiceId);
        using var lease=AcquireLease();
        foreach(var file in package.Files)
        {
            string path=OwnedPath(file.Path);
            if(File.Exists(path) && !await MatchesAsync(path,file,token).ConfigureAwait(false)) throw new IOException("A model file was changed outside the package manager; it was preserved.");
        }
        token.ThrowIfCancellationRequested();
        // No recursive deletion: Python, other voices, recordings, settings and unknown files remain.
        foreach(var file in package.Files) { string path=OwnedPath(file.Path); if(File.Exists(path)) File.Delete(path); }
    }

    private FileStream AcquireLease()
    {
        EnsureDirectory(Home);
        return new FileStream(OwnedPath(".voice-pack.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }

    internal void ExtractTools()
    {
        foreach(string file in new[]{"setup-speech.py","speech_worker.py","speech-models.json"})
        {
            using var source=typeof(VoicePackageManager).Assembly.GetManifestResourceStream("MorseCore.SpeechAssets."+file) ?? throw new InvalidDataException("Voice installer resource is missing.");
            using var reader=new StreamReader(source,Encoding.UTF8);
            WriteAtomic(ToolDirectory+"/"+file,reader.ReadToEnd());
            if(file=="speech_worker.py") WriteAtomic(file,ReadResource("MorseCore.SpeechAssets."+file));
        }
        // Preserve original license and NOTICE files. Resource dots retain directory separators
        // through an explicit map, avoiding filename/path guesses for model ownership.
        foreach(var (resource,destination) in new[]{
            ("antalia-mini.LICENSE","antalia-mini/LICENSE"),("antalia-mini.NOTICE","antalia-mini/NOTICE"),
            ("ema-lightning.LICENSE","ema-lightning/LICENSE"),("README.md","README.md")})
            WriteAtomic("licenses/speech/"+destination,ReadResource("MorseCore.SpeechLicenses."+resource));
    }

    private static string ReadResource(string name)
    {
        var assembly=typeof(VoicePackageManager).Assembly;
        string resource=assembly.GetManifestResourceNames().FirstOrDefault(n=>n.Replace('\\','.').Replace('/','.')==name) ?? name;
        using var stream=assembly.GetManifestResourceStream(resource) ?? throw new InvalidDataException("Voice license resource is missing: "+name);
        using var reader=new StreamReader(stream,Encoding.UTF8); return reader.ReadToEnd();
    }

    private void WriteAtomic(string relative,string text)
    {
        string destination=OwnedPath(relative); EnsureDirectory(Path.GetDirectoryName(destination)!);
        string temporary=OwnedPath(relative+"."+Guid.NewGuid().ToString("N")+".tmp");
        try { File.WriteAllText(temporary,text,new UTF8Encoding(false)); OwnedPath(relative); File.Move(temporary,destination,true); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }

    private async Task DownloadAsync(Uri uri,string destination,long bytes,string? sha256,string gitBlob,Action<long>? progress,CancellationToken token)
    {
        EnsureDirectory(Path.GetDirectoryName(destination)!);
        string relative=Path.GetRelativePath(Home,destination).Replace('\\','/');
        string temporary=OwnedPath(relative+"."+Guid.NewGuid().ToString("N")+".download");
        try
        {
            using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            // The default client follows HTTPS redirects to the publisher's CDN. Refuse a downgrade.
            if(response.RequestMessage?.RequestUri is not { Scheme:"https" }) throw new IOException("Insecure model redirect.");
            if(response.Content.Headers.ContentLength is { } length && length!=bytes) throw new IOException("Unexpected voice download size.");
            await using(var source=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            await using(var target=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,64*1024,true))
            {
                byte[] buffer=new byte[64*1024]; long total=0; int read;
                var progressClock=System.Diagnostics.Stopwatch.StartNew();
                while((read=await source.ReadAsync(buffer,token).ConfigureAwait(false))>0)
                {
                    total+=read; if(total>bytes) throw new IOException("Voice download exceeds its pinned size.");
                    await target.WriteAsync(buffer.AsMemory(0,read),token).ConfigureAwait(false);
                    if(progressClock.ElapsedMilliseconds>=120 || total==bytes) { progress?.Invoke(total); progressClock.Restart(); }
                }
                if(total!=bytes) throw new IOException("Incomplete voice download.");
            }
            var expected=new VoiceModelFile { Url=uri.ToString(),Path=relative,Bytes=bytes,Sha256=sha256,GitBlob=gitBlob };
            if(!await MatchesAsync(temporary,expected,token).ConfigureAwait(false)) throw new IOException("Voice integrity check failed.");
            token.ThrowIfCancellationRequested(); OwnedPath(relative); File.Move(temporary,destination,true);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }

    public static async Task<bool> MatchesAsync(string path,VoiceModelFile file,CancellationToken token=default)
    {
        if(!File.Exists(path) || new FileInfo(path).Length!=file.Bytes) return false;
        using var hash=IncrementalHash.CreateHash(file.Sha256 is null ? HashAlgorithmName.SHA1:HashAlgorithmName.SHA256);
        if(file.Sha256 is null) hash.AppendData(Encoding.UTF8.GetBytes($"blob {file.Bytes}\0"));
        await using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,true);
        byte[] buffer=new byte[64*1024]; int count;
        while((count=await stream.ReadAsync(buffer,token).ConfigureAwait(false))>0) hash.AppendData(buffer,0,count);
        return Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Sha256 ?? file.GitBlob,StringComparison.OrdinalIgnoreCase);
    }

    private static string ValidateHome(string path)
    {
        if(!Path.IsPathFullyQualified(path)) throw new ArgumentException("Speech home must be an absolute path.",nameof(path));
        string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        if(full.Length<5 || Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)==full) throw new ArgumentException("A filesystem root cannot be a speech home.",nameof(path));
        CheckLinks(full); return full;
    }

    private string OwnedPath(string relative)
    {
        VoicePackageCatalog.ValidateRelative(relative);
        string full=Path.GetFullPath(Path.Combine(Home,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!full.StartsWith(Home+Path.DirectorySeparatorChar,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)) throw new IOException("Voice path is outside the speech folder.");
        CheckLinks(full); return full;
    }

    private static void CheckLinks(string path)
    {
        for(string? current=path;current is not null;current=Path.GetDirectoryName(current))
        {
            try { if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("Voice package folders cannot contain symbolic links or junctions."); }
            catch(FileNotFoundException) { }
            catch(DirectoryNotFoundException) { }
        }
    }
    private static void EnsureDirectory(string path) { CheckLinks(path); DesktopPaths.CreateUserDirectory(path); CheckLinks(path); }
}
