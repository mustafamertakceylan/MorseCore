using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using MorseCore.AppCore;
using MorseCore.AppCore.SpeechPackages;

internal static class VoicePackageChecks
{
    public static void Run(Action<bool,string> check)=>RunAsync(check).GetAwaiter().GetResult();

    private static async Task RunAsync(Action<bool,string> check)
    {
        var catalog=VoicePackageCatalog.Packages;
        check(catalog.Count==8 && catalog.All(p=>p.Files.Count>=3),"Eight pinned voice packs include model cards/configs");
        check(catalog.Select(p=>p.Voice.Language).Distinct().Count()==5,"Voice pack language coverage");
        check(catalog.First(p=>p.Voice.Id=="ema-tr").DownloadBytes==34407274,"EMA download size is exact and excludes dependency estimates");
        foreach(var package in catalog)
        {
            check(package.Files.All(file=>file.Url.StartsWith("https://huggingface.co/",StringComparison.Ordinal)),"Pinned HTTPS voice origin: "+package.Voice.Id);
            check(package.Files.Select(file=>file.Path).Distinct().Count()==package.Files.Count,"No duplicate owned voice files: "+package.Voice.Id);
        }
        string directory=Path.Combine(AppContext.BaseDirectory,"MorseCore-VoiceChecks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var fixture=new HttpClient(new FixtureHandler());
            var manager=new VoicePackageManager(directory,fixture);
            manager.ExtractTools();
            check(File.Exists(Path.Combine(directory,".voice-tools","setup-speech.py")) && File.Exists(Path.Combine(directory,"speech_worker.py")),"Desktop extracts its embedded voice installer without external scripts");
            check(File.ReadAllText(Path.Combine(directory,"licenses","speech","antalia-mini","NOTICE")).Length>0 && File.Exists(Path.Combine(directory,"licenses","speech","ema-lightning","LICENSE")),"Embedded speech licenses and NOTICE survive package installation");
            bool rejected=false;
            try { _=new VoicePackageManager(Path.GetPathRoot(directory)!,fixture); } catch(ArgumentException) { rejected=true; }
            check(rejected,"Voice manager rejects filesystem roots");
            string unknown=Path.Combine(directory,"my-recording.wav"); await File.WriteAllTextAsync(unknown,"keep");
            await manager.RemoveAsync("ema-tr");
            check(File.Exists(unknown),"Removing absent voice preserves user recordings and folders");
            string model=Path.Combine(directory,"models","ema"); Directory.CreateDirectory(model);
            string changed=Path.Combine(model,"config.json"); await File.WriteAllTextAsync(changed,"custom user configuration");
            rejected=false;
            try { await manager.RemoveAsync("ema-tr"); } catch(IOException) { rejected=true; }
            check(rejected && File.Exists(changed) && File.Exists(unknown),"Changed model file blocks removal without deleting user data");
            await File.WriteAllTextAsync(changed,"existing model must survive a failed replacement");
            var pinned=catalog.First(p=>p.Voice.Id=="ema-tr").Files.First(file=>file.Path.EndsWith("config.json",StringComparison.Ordinal));
            rejected=false;
            try { await manager.EnsureModelAsync(pinned); } catch(IOException) { rejected=true; }
            check(rejected && await File.ReadAllTextAsync(changed)=="existing model must survive a failed replacement","Incorrect download length leaves previous model intact");
            check(!Directory.GetFiles(directory,"*.download",SearchOption.AllDirectories).Any(),"Failed voice download removes temporary data");
            using var badHashClient=new HttpClient(new FixtureHandler(pinned.Bytes));
            var badHashManager=new VoicePackageManager(directory,badHashClient);
            rejected=false;
            try { await badHashManager.EnsureModelAsync(pinned); } catch(IOException) { rejected=true; }
            check(rejected && await File.ReadAllTextAsync(changed)=="existing model must survive a failed replacement","Incorrect pinned digest cannot replace installed model");
            check(!Directory.GetFiles(directory,"*.download",SearchOption.AllDirectories).Any(),"Digest failure removes temporary download");
            using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
            rejected=false;
            try { await manager.EnsureModelAsync(pinned,token:cancelled.Token); } catch(OperationCanceledException) { rejected=true; }
            check(rejected && File.Exists(unknown),"Cancelled model download preserves existing files");
            // Pinned public EMA config, not model weights. Explicit LF makes the fixture portable.
            byte[] config=Encoding.UTF8.GetBytes("""
{
  "model_type": "ema-lightning",
  "package": "ema-lightning",
  "language": "tr",
  "license": "apache-2.0",
  "sample_rate": 48000,
  "latent_rate": 25,
  "sampling_steps": 4,
  "parameters": {
    "acoustic": "5.6M",
    "vocoder": "3.0M",
    "total": "8.6M"
  },
  "files": {
    "acoustic": "ema.pt",
    "vocoder": "decoder.pt"
  }
}
""".Replace("\r\n","\n"));
            var successHandler=new FixtureHandler(data:config);
            using var successClient=new HttpClient(successHandler);
            var successManager=new VoicePackageManager(directory,successClient);
            await successManager.EnsureModelAsync(pinned);
            check(await VoicePackageManager.MatchesAsync(changed,pinned),"Verified catalog fixture atomically replaces an invalid model");
            await successManager.EnsureModelAsync(pinned);
            check(successHandler.Requests==1,"Retry reuses existing verified models without network requests");
            await successManager.RemoveAsync("ema-tr");
            check(!File.Exists(changed) && File.Exists(unknown),"Removal deletes only verified files owned by the selected pack");
            rejected=false;
            try { await manager.EnsureModelAsync(pinned with { Url="https://example.com/model",Path="../outside" }); } catch(ArgumentException) { rejected=true; }
            check(rejected,"Untrusted catalog entries and destinations cannot enter downloader");
            var validText=new VoiceModelFile { Url="https://example.com",Path="test",Bytes=4,GitBlob="",Sha256=Convert.ToHexString(SHA256.HashData("keep"u8)) };
            check(await VoicePackageManager.MatchesAsync(unknown,validText),"SHA-256 content verification");
            var blob=SHA1.HashData(Encoding.UTF8.GetBytes("blob 4\0keep"));
            check(await VoicePackageManager.MatchesAsync(unknown,validText with { Sha256=null,GitBlob=Convert.ToHexString(blob) }),"Git blob verification includes header and pinned length");
            check(!await VoicePackageManager.IsCompatiblePythonAsync("python"),"Runtime detector never executes a bare PATH alias");
            // Every copy of a language file has the same set, and each key has end-user text.
            var english=new Localizer("en");
            foreach(string language in Localizer.Languages)
            {
                var text=new Localizer(language);
                check(english.Keys.Where(key=>key.StartsWith("voices",StringComparison.Ordinal)).All(key=>!string.IsNullOrWhiteSpace(text[key])),"Voice management translated: "+language);
            }
        }
        finally { Directory.Delete(directory,true); }
    }

    private sealed class FixtureHandler(long length=1,byte[]? data=null):HttpMessageHandler
    {
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage=request,Content=new ByteArrayContent(data ?? new byte[checked((int)length)]) });
        }
    }
}
