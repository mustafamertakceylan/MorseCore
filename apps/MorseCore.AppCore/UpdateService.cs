using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MorseCore.AppCore;

public sealed record ReleaseAsset(string Name, Uri Download, long Size);
public sealed record ReleaseUpdate(Version Version, Uri Page, ReleaseAsset? Installer=null, ReleaseAsset? Checksums=null);
public sealed class UpdateService(HttpClient client)
{
    private const string Repository="https://github.com/mustafamertakceylan/MorseCore/releases/";
    public const long MaxInstallerBytes=512L*1024*1024;
    public static readonly Uri Endpoint=new("https://api.github.com/repos/mustafamertakceylan/MorseCore/releases/latest");
    public async Task<ReleaseUpdate?> CheckAsync(Version current, CancellationToken token=default)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint);
        request.Headers.UserAgent.ParseAdd("MorseCore/0.5.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(4));
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
        if(response.StatusCode==HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await ReadBounded(response,256*1024,timeout.Token).ConfigureAwait(false));
        var root=json.RootElement;
        if(root.GetProperty("prerelease").GetBoolean() || root.GetProperty("draft").GetBoolean()) return null;
        string tag=root.GetProperty("tag_name").GetString() ?? "";
        if(!Version.TryParse(tag.TrimStart('v','V'),out var version) || version.Build<0 || version.Revision>=0 || version<=current) return null;
        if(tag!="v"+version.ToString(3)) throw new InvalidDataException("Unexpected release tag.");
        var page=ExactUrl(root.GetProperty("html_url").GetString(),Repository+"tag/"+tag);
        ReleaseAsset? installer=null,checksums=null;
        string filename=$"MorseCore-{version}-Windows-x64-Setup.exe";
        if(root.TryGetProperty("assets",out var assets))
            foreach(var asset in assets.EnumerateArray())
            {
                string? name=asset.GetProperty("name").GetString();
                if(name!=filename && name!="SHA256SUMS") continue;
                long size=asset.GetProperty("size").GetInt64();
                if(size<=0 || size>(name==filename ? MaxInstallerBytes : 64*1024)) throw new InvalidDataException("Invalid release asset size.");
                var download=ExactUrl(asset.GetProperty("browser_download_url").GetString(),Repository+"download/"+tag+"/"+name);
                var selected=new ReleaseAsset(name,download,size);
                if(name==filename) { if(installer!=null) throw new InvalidDataException("Duplicate installer."); installer=selected; }
                else { if(checksums!=null) throw new InvalidDataException("Duplicate checksum file."); checksums=selected; }
            }
        return new(version,page,installer,checksums);
    }

    public async Task<PreparedUpdate> DownloadAsync(ReleaseUpdate release,string cacheDirectory,IProgress<double>? progress=null,CancellationToken token=default)
    {
        string tag="v"+release.Version.ToString(3),filename=$"MorseCore-{release.Version}-Windows-x64-Setup.exe";
        var installer=release.Installer ?? throw new InvalidDataException("Release has no Windows installer.");
        var checksums=release.Checksums ?? throw new InvalidDataException("Release has no checksums.");
        ExactUrl(release.Page.AbsoluteUri,Repository+"tag/"+tag);
        ExactUrl(installer.Download.AbsoluteUri,Repository+"download/"+tag+"/"+filename);
        ExactUrl(checksums.Download.AbsoluteUri,Repository+"download/"+tag+"/SHA256SUMS");
        if(installer.Name!=filename || installer.Size<=0 || installer.Size>MaxInstallerBytes || checksums.Name!="SHA256SUMS" || checksums.Size<=0 || checksums.Size>64*1024)
            throw new InvalidDataException("Invalid release assets.");
        if(!Path.IsPathFullyQualified(cacheDirectory)) throw new ArgumentException("An absolute update cache is required.",nameof(cacheDirectory));
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        using var sums=await GetAsset(checksums.Download,timeout.Token).ConfigureAwait(false);
        string checksumText=Encoding.UTF8.GetString(await ReadBounded(sums,64*1024,timeout.Token).ConfigureAwait(false));
        var matches=checksumText.Split('\n').Select(line=>line.TrimEnd('\r')).Where(line=>line.Length>=66 && line[64]==' ' && (line[65]==' ' || line[65]=='*') && line[66..]==filename).ToArray();
        if(matches.Length!=1 || matches[0][..64].Any(c=>!Uri.IsHexDigit(c))) throw new InvalidDataException("Installer checksum is missing or ambiguous.");
        string expected=matches[0][..64];
        string directory=Path.Combine(cacheDirectory,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
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
        catch { Directory.Delete(directory,true); throw; }
    }

    private async Task<HttpResponseMessage> GetAsset(Uri uri,CancellationToken token)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);
        request.Headers.UserAgent.ParseAdd("MorseCore-Updater/1.0");
        var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            // GitHub redirects release assets to its HTTPS storage CDN.
            if(response.RequestMessage?.RequestUri is { } final && final.Scheme!="https") throw new InvalidDataException("Insecure asset redirect.");
            return response;
        }
        catch { response.Dispose(); throw; }
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
    public Process StartInstaller(string applicationDirectory,string language)
    {
        if(!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if(!Path.IsPathFullyQualified(applicationDirectory)) throw new ArgumentException("An absolute installation path is required.",nameof(applicationDirectory));
        using(var stream=File.OpenRead(InstallerPath))
            if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(checksum,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Downloaded installer was modified.");
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
