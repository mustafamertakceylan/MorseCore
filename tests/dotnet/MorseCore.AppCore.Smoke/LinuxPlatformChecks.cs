using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MorseCore.AppCore;

static class LinuxPlatformChecks
{
    internal static async Task<int> Run(string directory)
    {
        int checks=0;
        void Check(bool condition,string label) { if(!condition) throw new Exception(label); checks++; }
        string home=Path.Combine(directory,"synthetic-home");
        var defaults=DesktopPaths.ResolveLinuxDirectories(home);
        Check(defaults.ConfigDirectory==Path.Combine(home,".config","morsecore"),"XDG default configuration root");
        Check(defaults.DataDirectory==Path.Combine(home,".local","share","morsecore"),"XDG persistent data root");
        Check(defaults.CacheDirectory==Path.Combine(home,".cache","morsecore"),"XDG disposable cache root");
        var invalid=DesktopPaths.ResolveLinuxDirectories(home,"relative"," ","../cache");
        Check(invalid==defaults,"Relative and empty XDG overrides are ignored");
        string custom=Path.Combine(directory,"custom-storage");
        var customPaths=DesktopPaths.ResolveLinuxDirectories(home,custom,custom,custom);
        Check(customPaths.ConfigDirectory==Path.Combine(custom,"morsecore") && customPaths.DataDirectory==customPaths.CacheDirectory,"Absolute XDG roots are honored");
        Check(!Directory.Exists(home) && !Directory.Exists(custom),"Path discovery creates no directories");
        string privateStorage=Path.Combine(directory,"private-storage");
        DesktopPaths.CreateUserDirectory(privateStorage);
        Check(Directory.Exists(privateStorage),"Explicit storage creation succeeds");
        if(OperatingSystem.IsLinux())
        {
            Check(File.GetUnixFileMode(privateStorage)==(UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute),"New Linux storage is owner-only");
            File.SetUnixFileMode(privateStorage,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute|UnixFileMode.GroupRead);
            DesktopPaths.CreateUserDirectory(privateStorage);
            Check((File.GetUnixFileMode(privateStorage)&UnixFileMode.GroupRead)!=0,"Existing storage permissions are preserved");
        }
        try { DesktopPaths.ResolveLinuxDirectories("relative-home"); throw new Exception("Relative home accepted"); }
        catch(ArgumentException) { checks++; }
        Check(Path.IsPathFullyQualified(new Preferences { OutputDirectory="relative" }.Validated().OutputDirectory),"Missing Documents folder cannot create relative output paths");
        if(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            string oldRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MorseCore");
            Check(PreferencesStore.DefaultPath==Path.Combine(oldRoot,"Desktop","settings.json"),"Windows/macOS preferences location remains compatible");
            Check(DesktopPaths.SpeechDirectory==Path.Combine(oldRoot,"Speech"),"Windows/macOS speech location remains compatible");
        }
        if(OperatingSystem.IsLinux())
        {
            Check(PreferencesStore.DefaultPath==Path.Combine(DesktopPaths.ConfigDirectory,"settings.json"),"Linux preferences use XDG config");
            Check(DesktopPaths.SpeechDirectory==Path.Combine(DesktopPaths.DataDirectory,"Speech"),"Linux voices use persistent XDG data");
            Check(OfflineSpeech.PythonPath(home)==Path.Combine(home,"venv","bin","python"),"Linux speech uses isolated virtual environment");
        }

        const string root="https://github.com/mustafamertakceylan/MorseCore/releases/";
        const string filename="MorseCore-2.0.0-Linux-x64.tar.gz";
        const string checksum="SHA256SUMS-Linux-x64.txt";
        byte[] payload=Encoding.UTF8.GetBytes("synthetic-archive-fixture");
        string hash=Convert.ToHexString(SHA256.HashData(payload));
        string sums=hash+"  "+filename+"\n";
        string Metadata(IEnumerable<string> names)=>JsonSerializer.Serialize(new {
            tag_name="v2.0.0",draft=false,prerelease=false,html_url=root+"tag/v2.0.0",
            assets=names.Select(name=>new { name,browser_download_url=root+"download/v2.0.0/"+name,
                size=name is checksum or "SHA256SUMS"?Encoding.UTF8.GetByteCount(sums):payload.Length })
        });
        string[] mixed=["MorseCore-2.0.0-Windows-x64-Setup.exe","MorseCore-2.0.0-Linux-arm64.tar.gz",
            "MorseCore-2.0.0-Linux-x64.deb","MorseCore-2.0.0-macOS-arm64.dmg",filename,checksum];
        string cache=Path.Combine(directory,"linux-updates");
        using(var client=new HttpClient(new Handler(Metadata(mixed),sums,payload)))
        {
            var updater=new UpdateService(client);
            var release=(await updater.LatestAsync(UpdatePlatform.LinuxX64))!;
            Check(release.Platform==UpdatePlatform.LinuxX64 && release.Installer?.Name==filename,"Linux selects only its x64 portable release amid mixed platform assets");
            Check(release.Checksums?.Name==checksum,"Linux supplemental release selects platform checksum asset");
            Check((await updater.CheckAsync(new Version(1,0,0),UpdatePlatform.LinuxX64))?.Installer?.Name==filename,"Shared version comparison honors Linux platform");
            Check(await updater.CheckAsync(new Version(2,0,0),UpdatePlatform.LinuxX64) is null,"Same Linux version is not an available update");
            using(var prepared=await updater.DownloadAsync(release,cache))
            {
                Check(File.ReadAllBytes(prepared.InstallerPath).SequenceEqual(payload),"Linux archive uses shared exact size and SHA256 validation");
                string retained=prepared.RetainVerifiedFile();
                prepared.Dispose();
                Check(File.Exists(retained),"User can retain verified Linux archive for explicit manual installation");
                File.AppendAllText(retained,"tampered");
                try { prepared.VerifyIntegrity(); throw new Exception("Tampered Linux archive accepted"); }
                catch(InvalidDataException) { checks++; }
                Directory.Delete(Path.GetDirectoryName(retained)!,true);
            }
            try { using var result=await updater.DownloadAsync(release with { Platform=UpdatePlatform.WindowsX64 },cache); throw new Exception("Cross-platform update accepted"); }
            catch(InvalidDataException) { checks++; }
            foreach(var forged in new[] {
                release.Checksums! with { Name="SHA256SUMS-macOS-arm64.txt" },
                release.Checksums! with { Download=new Uri(root+"download/v2.0.0/SHA256SUMS") } })
            {
                try { using var result=await updater.DownloadAsync(release with { Checksums=forged },cache); throw new Exception("Wrong Linux checksum name or URL accepted"); }
                catch(InvalidDataException) { checks++; }
            }
        }
        foreach(var checksumOrder in new[] { new[]{"SHA256SUMS",checksum},new[]{checksum,"SHA256SUMS"},new[]{"SHA256SUMS"} })
        {
            bool supplemental=checksumOrder.Contains(checksum);
            using var client=new HttpClient(new Handler(Metadata(new[]{filename}.Concat(checksumOrder)),sums,payload,
                supplemental?new string('0',64)+sums[64..]:sums));
            var updater=new UpdateService(client);
            var release=(await updater.LatestAsync(UpdatePlatform.LinuxX64))!;
            Check(release.Checksums?.Name==(supplemental?checksum:"SHA256SUMS"),"Platform checksum wins independent of order; legacy shared list remains fallback");
            using var prepared=await updater.DownloadAsync(release,cache);
            Check(File.ReadAllBytes(prepared.InstallerPath).SequenceEqual(payload),"Selected platform or legacy checksum validates Linux archive");
        }
        foreach(string duplicate in new[]{checksum,"SHA256SUMS"})
        {
            using var client=new HttpClient(new Handler(Metadata([filename,checksum,"SHA256SUMS",duplicate]),sums,payload));
            try { await new UpdateService(client).LatestAsync(UpdatePlatform.LinuxX64); throw new Exception("Duplicate Linux checksum accepted"); }
            catch(InvalidDataException) { checks++; }
        }
        foreach(var platform in new[]{UpdatePlatform.WindowsX64,UpdatePlatform.MacArm64,UpdatePlatform.MacX64})
        {
            using var client=new HttpClient(new Handler(Metadata(mixed),sums,payload));
            Check((await new UpdateService(client).LatestAsync(platform))!.Checksums is null,"Other platforms never consume Linux-specific checksums");
        }
        foreach(var names in new[] { mixed.Where(name=>name!=filename).ToArray(),new[]{"MorseCore-2.0.0-Linux-x64.deb","SHA256SUMS"} })
        {
            using var client=new HttpClient(new Handler(Metadata(names),sums,payload));
            Check((await new UpdateService(client).LatestAsync(UpdatePlatform.LinuxX64))!.Installer is null,"Wrong CPU or package manager asset is never silently substituted");
        }
        using(var client=new HttpClient(new Handler(Metadata([filename,filename,"SHA256SUMS"]),sums,payload)))
        {
            try { await new UpdateService(client).LatestAsync(UpdatePlatform.LinuxX64); throw new Exception("Duplicate Linux archive accepted"); }
            catch(InvalidDataException) { checks++; }
        }
        using(var client=new HttpClient(new Handler(Metadata(mixed),new string('0',64)+sums[64..],payload)))
        {
            var updater=new UpdateService(client);
            var release=(await updater.LatestAsync(UpdatePlatform.LinuxX64))!;
            try { using var update=await updater.DownloadAsync(release,cache); throw new Exception("Bad Linux archive hash accepted"); }
            catch(InvalidDataException) { checks++; }
            Check(!Directory.EnumerateFiles(cache,"*",SearchOption.AllDirectories).Any(),"Failed Linux validation removes download artifacts");
        }
        return checks;
    }

    private sealed class Handler(string metadata,string sums,byte[] payload,string? legacySums=null):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            HttpContent content=request.RequestUri==UpdateService.Endpoint ? new StringContent(metadata) :
                request.RequestUri!.AbsolutePath.EndsWith("/SHA256SUMS-Linux-x64.txt",StringComparison.Ordinal) ? new StringContent(sums) :
                request.RequestUri.AbsolutePath.EndsWith("/SHA256SUMS",StringComparison.Ordinal) ? new StringContent(legacySums??sums):new ByteArrayContent(payload);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage=request,Content=content });
        }
    }
}
