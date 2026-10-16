using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using MorseCore.AppCore;

static class UpdateChecks
{
    private const string Root="https://github.com/mustafamertakceylan/MorseCore/releases/";
    private const string Filename="MorseCore-2.0.0-Windows-x64-Setup.exe";
    internal static async Task<int> Run(string directory)
    {
        int count=0;
        void Check(bool value,string description) { if(!value) throw new Exception(description); count++; }
        byte[] payload=new byte[150000]; Random.Shared.NextBytes(payload);
        string hash=Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string Metadata(string? page=null,string? url=null,long? size=null,bool duplicate=false)=>JsonSerializer.Serialize(new {
            tag_name="v2.0.0",draft=false,prerelease=false,html_url=page ?? Root+"tag/v2.0.0",
            assets=duplicate ? new[]{Asset(Filename,url,size),Asset(Filename,url,size),Asset("SHA256SUMS",null,null)} : new[]{Asset(Filename,url,size),Asset("SHA256SUMS",null,null)}
        });
        object Asset(string name,string? url,long? size)=>new { name,browser_download_url=url ?? Root+"download/v2.0.0/"+name,size=size ?? (name==Filename ? payload.Length : 64+2+Filename.Length+1) };
        HttpClient Client(string? metadata=null,string? sums=null,byte[]? content=null)
        {
            string checksumText=sums ?? hash+"  "+Filename+"\n";
            if(metadata==null)
            {
                var root=System.Text.Json.Nodes.JsonNode.Parse(Metadata())!;
                foreach(var item in root["assets"]!.AsArray())
                    if(item!["name"]!.GetValue<string>()=="SHA256SUMS") item["size"]=Math.Min(65536,System.Text.Encoding.UTF8.GetByteCount(checksumText));
                metadata=root.ToJsonString();
            }
            return new(new Handler(metadata,checksumText,content ?? payload));
        }
        string cache=Path.Combine(directory,"updates"); Directory.CreateDirectory(cache);
        using(var client=Client())
        {
            var service=new UpdateService(client);
            var release=(await service.CheckAsync(new Version(0,4,0)))!;
            Check(release.Installer?.Name==Filename && release.Checksums?.Name=="SHA256SUMS","Matching Windows installer assets selected");
            string downloaded;
            using(var prepared=await service.DownloadAsync(release,cache))
            {
                downloaded=prepared.InstallerPath;
                Check(File.ReadAllBytes(downloaded).SequenceEqual(payload),"Installer downloaded and verified without modification");
                Check(!Directory.GetFiles(cache,"*.download",SearchOption.AllDirectories).Any(),"Only a verified download becomes executable");
                File.AppendAllText(downloaded,"tampered");
                if(OperatingSystem.IsWindows())
                {
                    try { using var process=prepared.StartInstaller(directory,"tr"); throw new Exception("Tampered executable started"); }
                    catch(InvalidDataException) { count++; }
                }
            }
            Check(!File.Exists(downloaded),"Declining installation removes the cached executable");
        }
        foreach(string metadata in new[]{Metadata(page:Root+"tag/v1.0.0"),Metadata(page:Root+"tag/v2.0.0?bad=1"),Metadata(url:"https://example.com/installer.exe"),Metadata(url:Root+"download/v1.0.0/"+Filename),Metadata(size:UpdateService.MaxInstallerBytes+1),Metadata(duplicate:true)})
        {
            using var client=Client(metadata);
            try { await new UpdateService(client).CheckAsync(new Version(0,4,0)); throw new Exception("Invalid release accepted"); }
            catch(InvalidDataException) { count++; }
        }
        foreach(string sums in new[]{new string('0',64)+"  "+Filename,hash+"  wrong.exe",hash+"  "+Filename+"\n"+hash+"  "+Filename,new string('x',64)+"  "+Filename,new string(' ',65537)})
        {
            using var client=Client(sums:sums);
            var service=new UpdateService(client); var release=(await service.CheckAsync(new Version(0,4,0)))!;
            try { using var prepared=await service.DownloadAsync(release,cache); throw new Exception("Invalid checksum accepted"); }
            catch(InvalidDataException) { count++; }
            Check(!Directory.GetFiles(cache,"*",SearchOption.AllDirectories).Any(),"Failed verification leaves no executable");
        }
        foreach(int length in new[]{payload.Length-1,payload.Length+1})
        {
            using var client=Client(content:new byte[length]);
            var service=new UpdateService(client); var release=(await service.CheckAsync(new Version(0,4,0)))!;
            try { using var prepared=await service.DownloadAsync(release,cache); throw new Exception("Invalid size accepted"); }
            catch(InvalidDataException) { count++; }
        }
        using(var cancellation=new CancellationTokenSource())
        using(var client=Client())
        {
            var service=new UpdateService(client); var release=(await service.CheckAsync(new Version(0,4,0)))!;
            try { using var prepared=await service.DownloadAsync(release,cache,new CallbackProgress(_=>cancellation.Cancel()),cancellation.Token); throw new Exception("Cancellation ignored"); }
            catch(OperationCanceledException) { count++; }
            Check(!Directory.GetFiles(cache,"*",SearchOption.AllDirectories).Any(),"Cancelled download removes partial and completed files");
        }
        using(var client=Client())
        {
            var service=new UpdateService(client); var release=(await service.CheckAsync(new Version(0,4,0)))!;
            try { using var prepared=await service.DownloadAsync(release with { Installer=release.Installer! with { Download=new Uri("https://example.com/other.exe") } },cache); throw new Exception("Forged release accepted"); }
            catch(InvalidDataException) { count++; }
        }
        foreach(var platform in new[] { UpdatePlatform.MacArm64,UpdatePlatform.MacX64,UpdatePlatform.WindowsX64 })
        {
            string cpu=platform==UpdatePlatform.MacArm64?"arm64":"x64";
            string expected=platform==UpdatePlatform.WindowsX64?Filename:$"MorseCore-2.0.0-macOS-{cpu}.dmg";
            string[] filenames=["MorseCore-2.0.0-macOS-x64.pkg","MorseCore-2.0.0-macOS-arm64.zip",Filename,
                "MorseCore-2.0.0-macOS-universal.dmg","MorseCore-2.0.0-macOS-arm64.dmg","MorseCore-2.0.0-macOS-x64.dmg"];
            string sums=hash+"  "+expected+"\n";
            string metadata=JsonSerializer.Serialize(new { tag_name="v2.0.0",draft=false,prerelease=false,html_url=Root+"tag/v2.0.0",body="Notes",
                assets=filenames.Select(name=>new { name,browser_download_url=Root+"download/v2.0.0/"+name,size=payload.Length })
                    .Cast<object>().Append(new { name="SHA256SUMS",browser_download_url=Root+"download/v2.0.0/SHA256SUMS",size=sums.Length }) });
            using var client=Client(metadata,sums);
            var service=new UpdateService(client);
            var release=(await service.LatestAsync(platform))!;
            Check(release.Installer?.Name==expected && release.Platform==platform,"CPU-specific DMG takes priority over universal and archive; Windows stays EXE");
            Check(release.Notes=="Notes","Shared release notes preserve text");
            using var prepared=await service.DownloadAsync(release,cache);
            prepared.VerifyIntegrity(); count++;
            Check(await service.CheckAsync(new Version(2,0,0))==null,"Same Windows version is not a new update");
            Check((await service.LatestAsync(platform))?.Version==new Version(2,0,0),"Latest metadata remains available for same-version release validation");
            string retained=prepared.RetainVerifiedFile(); prepared.Dispose();
            Check(File.Exists(retained),"macOS verified package survives helper exit after ownership transfer");
            File.AppendAllText(retained,"changed");
            try { prepared.VerifyIntegrity(); throw new Exception("Changed retained package accepted"); } catch(InvalidDataException) { count++; }
            Directory.Delete(Path.GetDirectoryName(retained)!,true);
        }
        foreach(string cpu in new[] { "x64","universal" })
        {
            string name=$"MorseCore-2.0.0-macOS-{cpu}.dmg";
            string metadata=JsonSerializer.Serialize(new { tag_name="v2.0.0",draft=false,prerelease=false,html_url=Root+"tag/v2.0.0",assets=new[] { Asset(name,null,100) } });
            using var client=Client(metadata);
            var release=(await new UpdateService(client).LatestAsync(UpdatePlatform.MacArm64))!;
            Check(cpu=="universal"?release.Installer?.Name==name:release.Installer==null,"Wrong CPU never offered; universal can be selected");
        }
        foreach(string invalid in new[] { "http://github.com/a","https://github.com.evil.example/a","https://user@github.com/a","https://github.com:444/a","https://example.org/a" })
            Check(!UpdateService.IsTrustedDownload(new Uri(invalid)),"Unsafe update URL rejected: "+invalid);
        foreach(string valid in new[] { "https://github.com/a","https://api.github.com/a","https://release-assets.githubusercontent.com/a" })
            Check(UpdateService.IsTrustedDownload(new Uri(valid)),"Trusted HTTPS source allowed");
        foreach(string destination in new[] { "http://github.com/a","https://evil.example/a" })
        {
            int requests=0;
            using var client=new HttpClient(new ResponseHandler(request=> { requests++; var response=new HttpResponseMessage(HttpStatusCode.Redirect) { RequestMessage=request }; response.Headers.Location=new Uri(destination); return response; }));
            try { await new UpdateService(client).LatestAsync(UpdatePlatform.MacArm64); throw new Exception("Unsafe intermediate redirect followed"); } catch(InvalidDataException) { count++; }
            Check(requests==1,"Rejected redirect is never requested");
        }
        using(var client=new HttpClient(new ResponseHandler(request=>new HttpResponseMessage(HttpStatusCode.TooManyRequests) { RequestMessage=request })))
        {
            try { await new UpdateService(client).LatestAsync(UpdatePlatform.MacArm64); throw new Exception("Rate limit reported as up to date"); } catch(HttpRequestException error) { Check(error.StatusCode==HttpStatusCode.TooManyRequests,"Rate limits remain errors rather than no updates"); }
        }
        using(var client=new HttpClient(new ResponseHandler(request=>new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage=request })))
            Check(await new UpdateService(client).LatestAsync(UpdatePlatform.MacArm64)==null,"No releases is distinct from current version");
        return count;
    }
    private sealed class ResponseHandler(Func<HttpRequestMessage,HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(response(request)); }
    }
    private sealed class CallbackProgress(Action<double> callback) : IProgress<double> { public void Report(double value)=>callback(value); }
    private sealed class Handler(string metadata,string sums,byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(request.Headers.UserAgent.Count==0) throw new Exception("Update request has no User-Agent");
            HttpContent content=request.RequestUri==UpdateService.Endpoint ? new StringContent(metadata) :
                request.RequestUri!.AbsolutePath.EndsWith("/SHA256SUMS",StringComparison.Ordinal) ? new StringContent(sums) : new ByteArrayContent(payload);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=content,RequestMessage=request });
        }
    }
}
