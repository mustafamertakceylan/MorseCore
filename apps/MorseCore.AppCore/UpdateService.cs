using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MorseCore.AppCore;

public sealed record ReleaseAsset(string Name, Uri Download, long Size);
public enum UpdatePlatform { WindowsX64, MacArm64, MacX64, LinuxX64 }
public sealed record ReleaseUpdate(Version Version, Uri Page, ReleaseAsset? Installer=null, ReleaseAsset? Checksums=null)
{
    public UpdatePlatform Platform { get; init; }=UpdatePlatform.WindowsX64;
    public string Notes { get; init; }="";
}
public sealed class UpdateService(HttpClient client)
{
    private const string Repository="https://github.com/mustafamertakceylan/MorseCore/releases/";
    public const long MaxInstallerBytes=512L*1024*1024;
    public static readonly Uri Endpoint=new("https://api.github.com/repos/mustafamertakceylan/MorseCore/releases/latest");
    // Redirects are followed explicitly so every hop, including intermediate hops,
    // is constrained to GitHub HTTPS storage on every desktop platform.
    public static HttpClient CreateHttpClient()=>new(new HttpClientHandler { AllowAutoRedirect=false });

    public Task<ReleaseUpdate?> CheckAsync(Version current,CancellationToken token=default)=>CheckAsync(current,UpdatePlatform.WindowsX64,token);

    public async Task<ReleaseUpdate?> CheckAsync(Version current,UpdatePlatform platform,CancellationToken token=default)
    {
        var release=await LatestAsync(platform,token).ConfigureAwait(false);
        return release?.Version>current ? release : null;
    }

    public async Task<ReleaseUpdate?> LatestAsync(UpdatePlatform platform,CancellationToken token=default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response=await GetResponse(Endpoint,timeout.Token,allowNotFound:true).ConfigureAwait(false);
        if(response.StatusCode==HttpStatusCode.NotFound) return null;
        using var json=JsonDocument.Parse(await ReadBounded(response,256*1024,timeout.Token).ConfigureAwait(false));
        var root=json.RootElement;
        if(root.GetProperty("prerelease").GetBoolean() || root.GetProperty("draft").GetBoolean()) return null;
        string tag=root.GetProperty("tag_name").GetString() ?? "";
        if(!Version.TryParse(tag.TrimStart('v','V'),out var version) || version.Build<0 || version.Revision>=0) throw new InvalidDataException("Invalid stable release version.");
        if(tag!="v"+version.ToString(3)) throw new InvalidDataException("Unexpected release tag.");
        var page=ExactUrl(root.GetProperty("html_url").GetString(),Repository+"tag/"+tag);
        ReleaseAsset? installer=null,checksums=null;
        var names=InstallerNames(version,platform);
        var checksumNames=ChecksumNames(platform);
        var installers=new Dictionary<string,ReleaseAsset>(StringComparer.Ordinal);
        var checksumFiles=new Dictionary<string,ReleaseAsset>(StringComparer.Ordinal);
        if(root.TryGetProperty("assets",out var assets))
            foreach(var asset in assets.EnumerateArray())
            {
                string? name=asset.GetProperty("name").GetString();
                if(name==null || (!names.Contains(name,StringComparer.Ordinal) && !checksumNames.Contains(name,StringComparer.Ordinal))) continue;
                bool isChecksum=checksumNames.Contains(name,StringComparer.Ordinal);
                long size=asset.GetProperty("size").GetInt64();
                if(size<=0 || size>(isChecksum ? 64*1024 : MaxInstallerBytes)) throw new InvalidDataException("Invalid release asset size.");
                var download=ExactUrl(asset.GetProperty("browser_download_url").GetString(),Repository+"download/"+tag+"/"+name);
                var selected=new ReleaseAsset(name,download,size);
                if(!isChecksum) { if(!installers.TryAdd(name,selected)) throw new InvalidDataException("Duplicate installer."); }
                else if(!checksumFiles.TryAdd(name,selected)) throw new InvalidDataException("Duplicate checksum file.");
            }
        installer=names.Select(name=>installers.GetValueOrDefault(name)).FirstOrDefault(asset=>asset!=null);
        checksums=checksumNames.Select(name=>checksumFiles.GetValueOrDefault(name)).FirstOrDefault(asset=>asset!=null);
        string notes=root.TryGetProperty("body",out var body)&&body.ValueKind==JsonValueKind.String ? body.GetString()??"" : "";
        return new(version,page,installer,checksums) { Platform=platform,Notes=notes[..Math.Min(notes.Length,32768)] };
    }

    private static string[] InstallerNames(Version version,UpdatePlatform platform)=>platform switch
    {
        UpdatePlatform.WindowsX64=>[$"MorseCore-{version}-Windows-x64-Setup.exe"],
        UpdatePlatform.LinuxX64=>[$"MorseCore-{version}-Linux-x64.tar.gz"],
        UpdatePlatform.MacArm64 or UpdatePlatform.MacX64=>new[] { platform==UpdatePlatform.MacArm64?"arm64":"x64","universal" }
            .SelectMany(cpu=>new[] { "dmg","pkg","zip" }.Select(ext=>$"MorseCore-{version}-macOS-{cpu}.{ext}")).ToArray(),
        _=>throw new ArgumentOutOfRangeException(nameof(platform))
    };

    // Supplemental Linux assets can be added without replacing an existing release's
    // Windows/macOS checksum list. Legacy all-platform lists remain compatible.
    private static string[] ChecksumNames(UpdatePlatform platform)=>platform==UpdatePlatform.LinuxX64
        ? ["SHA256SUMS-Linux-x64.txt","SHA256SUMS"] : ["SHA256SUMS"];

    public async Task<PreparedUpdate> DownloadAsync(ReleaseUpdate release,string cacheDirectory,IProgress<double>? progress=null,CancellationToken token=default)
    {
        if(release.Version.Build<0 || release.Version.Revision>=0) throw new InvalidDataException("Invalid release version.");
        string tag="v"+release.Version.ToString(3);
        var installer=release.Installer ?? throw new InvalidDataException("Release has no compatible installer.");
        string filename=installer.Name;
        if(!InstallerNames(release.Version,release.Platform).Contains(filename,StringComparer.Ordinal)) throw new InvalidDataException("Installer platform differs from release.");
        var checksums=release.Checksums ?? throw new InvalidDataException("Release has no checksums.");
        ExactUrl(release.Page.AbsoluteUri,Repository+"tag/"+tag);
        ExactUrl(installer.Download.AbsoluteUri,Repository+"download/"+tag+"/"+filename);
        if(!ChecksumNames(release.Platform).Contains(checksums.Name,StringComparer.Ordinal)) throw new InvalidDataException("Checksum platform differs from release.");
        ExactUrl(checksums.Download.AbsoluteUri,Repository+"download/"+tag+"/"+checksums.Name);
        if(installer.Name!=filename || installer.Size<=0 || installer.Size>MaxInstallerBytes || checksums.Size<=0 || checksums.Size>64*1024)
            throw new InvalidDataException("Invalid release assets.");
        if(!Path.IsPathFullyQualified(cacheDirectory)) throw new ArgumentException("An absolute update cache is required.",nameof(cacheDirectory));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        using var sums=await GetAsset(checksums.Download,timeout.Token).ConfigureAwait(false);
        byte[] checksumBytes=await ReadBounded(sums,64*1024,timeout.Token).ConfigureAwait(false);
        if(checksumBytes.LongLength!=checksums.Size) throw new InvalidDataException("Checksum length differs from release metadata.");
        string checksumText=new UTF8Encoding(false,true).GetString(checksumBytes);
        var matches=checksumText.Split('\n').Select(line=>line.TrimEnd('\r')).Where(line=>line.Length>=66 && line[64]==' ' && (line[65]==' ' || line[65]=='*') && line[66..]==filename).ToArray();
        if(matches.Length!=1 || matches[0][..64].Any(c=>!Uri.IsHexDigit(c))) throw new InvalidDataException("Installer checksum is missing or ambiguous.");
        string expected=matches[0][..64];
        string directory=Path.Combine(cacheDirectory,Guid.NewGuid().ToString("N"));
        DesktopPaths.CreateUserDirectory(directory);
        string partial=Path.Combine(directory,filename+".download"),complete=Path.Combine(directory,filename);
        try
        {
            using var response=await GetAsset(installer.Download,timeout.Token).ConfigureAwait(false);
            if(response.Content.Headers.ContentLength is long length && length!=installer.Size) throw new InvalidDataException("Installer length differs from release metadata.");
            using var input=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,64*1024,FileOptions.Asynchronous))
            {
                byte[] bytes=new byte[64*1024]; long received=0; int read;
                while((read=await input.ReadAsync(bytes,timeout.Token).ConfigureAwait(false))!=0)
                {
                    received+=read;
                    if(received>installer.Size) throw new InvalidDataException("Installer exceeds its declared size.");
                    hash.AppendData(bytes,0,read);
                    await output.WriteAsync(bytes.AsMemory(0,read),timeout.Token).ConfigureAwait(false);
                    progress?.Report(100.0*received/installer.Size);
                }
                if(received!=installer.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(expected,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installer integrity check failed.");
            }
            timeout.Token.ThrowIfCancellationRequested();
            File.Move(partial,complete);
            return new PreparedUpdate(release.Version,complete,expected);
        }
        catch { try { Directory.Delete(directory,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } throw; }
    }

    private async Task<HttpResponseMessage> GetAsset(Uri uri,CancellationToken token)
    {
        return await GetResponse(uri,token).ConfigureAwait(false);
    }
    public static bool IsTrustedDownload(Uri uri)=>uri.IsAbsoluteUri && uri.Scheme=="https" && string.IsNullOrEmpty(uri.UserInfo) && uri.IsDefaultPort &&
        (uri.Host is "github.com" or "api.github.com" or "githubusercontent.com" || uri.Host.EndsWith(".githubusercontent.com",StringComparison.OrdinalIgnoreCase));

    private async Task<HttpResponseMessage> GetResponse(Uri uri,CancellationToken token,bool allowNotFound=false)
    {
        for(int hop=0;hop<=5;hop++)
        {
            if(!IsTrustedDownload(uri)) throw new InvalidDataException("Untrusted update URL.");
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);
            request.Headers.UserAgent.ParseAdd("MorseCore-Updater/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
            var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
        try
        {
            if(response.RequestMessage?.RequestUri is { } final && !IsTrustedDownload(final)) throw new InvalidDataException("Untrusted asset redirect.");
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location=response.Headers.Location ?? throw new InvalidDataException("Update redirect has no destination.");
                uri=location.IsAbsoluteUri?location:new Uri(uri,location);
                response.Dispose(); continue;
            }
            if(response.StatusCode==HttpStatusCode.NotFound && allowNotFound) return response;
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch { response.Dispose(); throw; }
        }
        throw new InvalidDataException("Too many update redirects.");
    }
    private static Uri ExactUrl(string? value,string expected)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri) || !string.Equals(uri.AbsoluteUri,expected,StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected GitHub release URL.");
        return uri;
    }
    private static async Task<byte[]> ReadBounded(HttpResponseMessage response,int maximum,CancellationToken token)
    {
        using var stream=await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var buffer=new MemoryStream(); byte[] chunk=new byte[4096]; int read;
        while((read=await stream.ReadAsync(chunk,token).ConfigureAwait(false))!=0)
        {
            if(buffer.Length+read>maximum) throw new InvalidDataException("Release response is too large.");
            buffer.Write(chunk,0,read);
        }
        return buffer.ToArray();
    }
}

public sealed class PreparedUpdate : IDisposable
{
    public Version Version { get; }
    public string InstallerPath { get; }
    private readonly string checksum;
    private bool started;
    internal PreparedUpdate(Version version,string path,string checksum) { Version=version; InstallerPath=path; this.checksum=checksum; }
    // macOS opens a verified package only after a separate user click. Ownership
    // transfer keeps that package after this short-lived helper exits.
    public string RetainVerifiedFile()
    {
        VerifyIntegrity(); started=true; return InstallerPath;
    }
    public void VerifyIntegrity()
    {
        using var stream=File.OpenRead(InstallerPath);
        if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(checksum,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded installer was modified.");
    }
    public Process StartInstaller(string applicationDirectory,string language)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if(!Path.IsPathFullyQualified(applicationDirectory)) throw new ArgumentException("An absolute installation path is required.",nameof(applicationDirectory));
        VerifyIntegrity();
        string? product=FileVersionInfo.GetVersionInfo(InstallerPath).ProductVersion;
        if(!System.Version.TryParse(product,out var installed) || installed.Major!=Version.Major || installed.Minor!=Version.Minor || installed.Build!=Version.Build)
            throw new InvalidDataException("Installer product version differs from the release.");
        var start=new ProcessStartInfo(InstallerPath) { UseShellExecute=true,WindowStyle=ProcessWindowStyle.Normal,WorkingDirectory=Path.GetDirectoryName(InstallerPath)! };
        start.ArgumentList.Add("/NORESTART"); start.ArgumentList.Add("/SP-"); start.ArgumentList.Add("/DIR="+applicationDirectory);
        start.ArgumentList.Add("/LANG="+(language switch { "tr"=>"turkish","de"=>"german","ru"=>"russian","ar"=>"arabic",_=>"english" }));
        var process=Process.Start(start) ?? throw new IOException("Installer could not be started.");
        started=true; return process;
    }
    public void Dispose()
    {
        if(!started && Directory.Exists(Path.GetDirectoryName(InstallerPath))) Directory.Delete(Path.GetDirectoryName(InstallerPath)!,true);
    }
}
