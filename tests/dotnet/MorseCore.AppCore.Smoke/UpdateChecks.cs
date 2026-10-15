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
        object Asset(string name,string? url,long? size)=>new { name,browser_download_url=url ?? Root+"download/v2.0.0/"+name,size=size ?? (name==Filename ? payload.Length : 200) };
        HttpClient Client(string? metadata=null,string? sums=null,byte[]? content=null)=>new(new Handler(metadata ?? Metadata(),sums ?? hash+"  "+Filename+"\n",content ?? payload));
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
        return count;
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
