using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using MorseCore.AppCore;
using MorseCore.AppCore.Decoding;
using MorseCore.AppCore.Keying;
using MorseCore.AppCore.Radio;
using MorseCore.AppCore.SpeechPackages;
using MorseCore.Interop;

namespace MorseCore.MacService;

internal static class Program
{
    // ADIF's own parser still enforces its 8 MiB text limit. Escaped JSON may be six times larger.
    private const int MaximumInputBytes=64*1024*1024;
    private static readonly JsonSerializerOptions Json=new() { PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true };
    private static nint nativeHandle;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding=new UTF8Encoding(false);
        using var cancellation=new CancellationTokenSource();
        Console.CancelKeyPress+=(_,e)=> { e.Cancel=true; cancellation.Cancel(); };
        using var terminate=OperatingSystem.IsWindows()?null:PosixSignalRegistration.Create(PosixSignal.SIGTERM,context=> { context.Cancel=true; cancellation.Cancel(); });
        try
        {
            var options=Arguments(args);
            string directory=RequiredPath(options,"--data"),speech=RequiredPath(options,"--speech"),native=RequiredPath(options,"--native");
            nativeHandle=NativeLibrary.Load(native);
            NativeLibrary.SetDllImportResolver(typeof(MorseEngine).Assembly,(name,_,_)=>name=="MorseCore.Native"?nativeHandle:0);
            // ABI compatibility is checked before executing commands, including data writes.
            _=MorseEngine.NativeVersion;
            byte[] input=await ReadInput(cancellation.Token);
            using var request=JsonDocument.Parse(input,new JsonDocumentOptions { MaxDepth=64 });
            string command=request.RootElement.GetProperty("command").GetString()??throw new ArgumentException("Missing command.");
            JsonElement payload=request.RootElement.TryGetProperty("payload",out var value)?value:JsonSerializer.SerializeToElement(new { });
            object result=await Execute(command,payload,directory,speech,cancellation.Token);
            Console.WriteLine(JsonSerializer.Serialize(new { ok=true,data=result },Json));
            return 0;
        }
        catch(Exception error)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok=false,error=error is OperationCanceledException?"Operation cancelled.":error.Message },Json));
            return 1;
        }
        // The OS releases the loaded library after all managed/native finalizers at process exit.
    }

    private static async Task<object> Execute(string command,JsonElement p,string directory,string speech,CancellationToken token)
    {
        var store=new RadioStore(directory);
        switch(command)
        {
            case "updates.check":
                if(!Version.TryParse(Text(p,"currentVersion"),out var currentVersion)) throw new ArgumentException("Invalid current version.");
                using(var http=UpdateService.CreateHttpClient())
                {
                    var release=await new UpdateService(http).LatestAsync(UpdatePlatformForProcess(),token);
                    if(release==null) return new { found=false };
                    return new { found=true,version=release.Version.ToString(3),page=release.Page.AbsoluteUri,notes=release.Notes,newer=release.Version>currentVersion,
                        installer=Asset(release.Installer),checksums=Asset(release.Checksums) };
                }
            case "updates.download":
                string updateVersion=Text(p,"version"),cache=Text(p,"cacheDirectory");
                if(!Version.TryParse(updateVersion,out _)||!Path.IsPathFullyQualified(cache)) throw new ArgumentException("Invalid update version or cache directory.");
                using(var http=UpdateService.CreateHttpClient())
                {
                    var updater=new UpdateService(http);
                    var release=await updater.LatestAsync(UpdatePlatformForProcess(),token);
                    if(release==null||release.Version.ToString(3)!=updateVersion||release.Installer==null||release.Checksums==null) throw new InvalidDataException("Release has changed or has no compatible update package.");
                    using var prepared=await updater.DownloadAsync(release,Path.GetFullPath(cache),token:token);
                    string digest;
                    await using(var file=File.OpenRead(prepared.InstallerPath)) digest=Convert.ToHexString(await SHA256.HashDataAsync(file,token)).ToLowerInvariant();
                    token.ThrowIfCancellationRequested();
                    string path=prepared.RetainVerifiedFile();
                    return new { path,sha256=digest,version=release.Version.ToString(3) };
                }
            case "catalog": return new { radio=RadioCatalog.Data,terms=CwLearning.Terms,phonetics=CwLearning.Phonetics,prosigns=CwMessages.Prosigns,outline=WorldOutline.Data,voices=OfflineSpeech.Voices };
            case "callsign":
                var call=CallSigns.Parse(Text(p,"value"));
                return new { call.Normalized,call.BaseCall,call.CountryId,call.ClassId,call.Region,call.IsRecognized,areaDetail=call.CountryId=="TR"&&call.Region!=null?CallSigns.TurkishArea(call.Region):null };
            case "cw.encode": return new { morse=CwMessages.Encode(Text(p,"text")) };
            case "operating": return CallSigns.Operating(Read<StationProfile>(p,"profile"),Read<OperatingContext>(p,"context"));
            case "assess": return RadioCatalog.Assess(Text(p,"countryId"),Text(p,"classId"),p.GetProperty("frequencyHz").GetInt64());
            case "compose":
                string message=CwMessages.Compose((QsoStage)Integer(p,"stage"),Text(p,"ownCall"),Text(p,"otherCall"),Text(p,"sentRst"),Text(p,"name"),Text(p,"qth"),Text(p,"message"));
                return new { text=message,morse=CwMessages.Encode(message) };
            case "profiles.load": return new { document=store.LoadProfiles(),warning=store.Warning };
            case "profiles.save":
                _=store.LoadProfiles(); // Preserve unreadable existing data just like Windows.
                store.SaveProfiles(Read<StationDocument>(p,"document")); return new { };
            case "qso.query": return store.Query(Read<LogQuery>(p,"query"),token);
            case "qso.map":
                var map=store.Map(Read<LogQuery>(p,"query"),Integer(p,"maximum",500),token);
                // Presentation coordinates come from the shared locator; stored records stay unchanged.
                return map with { Entries=map.Entries.Select(entry=>Locator.Position(entry) is { } position
                    ? entry with { Latitude=position.Latitude,Longitude=position.Longitude } : entry).ToArray() };
            case "qso.append": store.Append(Read<QsoRecord>(p,"entry")); return new { };
            case "qso.update": store.Update(Read<QsoRecord>(p,"entry")); return new { };
            case "qso.export": return new { text=await Export(Read<QsoRecord[]>(p,"entries"),Text(p,"format"),token) };
            case "qso.export.file":
                string destination=Text(p,"destination"),format=Text(p,"format");
                if(!Path.IsPathFullyQualified(destination)) throw new ArgumentException("Export destination must be an absolute path.");
                if(format is not ("adif" or "csv")) throw new ArgumentException("Unknown export format.");
                await store.ExportAsync(destination,Boolean(p,"practice"),format=="adif",token);
                return new { };
            case "adif.preview":
                var document=AdifImport.Parse(Text(p,"text"),new(Text(p,"ownCall"),Text(p,"countryId","OTHER"),Boolean(p,"practice",true)),token);
                return new { document,preview=store.PreviewImport(document.Records,token) };
            case "adif.import": return store.Import(Read<QsoRecord[]>(p,"entries"),token);
            case "learning.questions": return CwLearning.Questions(Integer(p,"mode"),Integer(p,"seed"));
            case "keying.analyze":
                var alphabet=MorseEngine.Alphabet((MorseAlphabet)Integer(p,"alphabet")).GroupBy(symbol=>symbol.Code).ToDictionary(group=>group.Key,group=>group.First().Character.ToString());
                var session=new KeyingSession(Integer(p,"wpm",20),alphabet);
                var events=Read<KeyEvent[]>(p,"events");
                if(events.Length>2*KeyingSession.MaximumMarks+100) throw new ArgumentException("Too many key events.");
                foreach(var item in events) { token.ThrowIfCancellationRequested(); if(item.Down) session.KeyDown(TimeSpan.FromMilliseconds(item.Milliseconds)); else session.KeyUp(TimeSpan.FromMilliseconds(item.Milliseconds)); }
                session.Finish(TimeSpan.FromMilliseconds(p.GetProperty("finishMilliseconds").GetDouble()));
                var snapshot=session.Snapshot();
                return new { snapshot,accuracy=KeyingComparison.Accuracy(Text(p,"expected"),snapshot.Text) };
            case "decoder.file": return await MorseDecoder.DecodeFileAsync(Text(p,"path"),new(Number(p,"frequencyHz"),Number(p,"wpm"),Number(p,"threshold")??.35,(MorseAlphabet)Integer(p,"alphabet")),token);
            case "speech.status":
                using(var http=new HttpClient())
                {
                    var manager=new VoicePackageManager(speech,http);
                    PortablePython.AddToProcessPath(speech);
                    string? python=await manager.FindPythonAsync(token);
                    return new { voices=OfflineSpeech.Voices.Select(voice=>new { voice.Id,voice.Language,voice.Name,voice.Engine,voice.Model,installed=OfflineSpeech.IsInstalled(speech,voice),downloadBytes=VoicePackageCatalog.Get(voice.Id).DownloadBytes,source=VoicePackageCatalog.Get(voice.Id).Source.AbsoluteUri,sourceUrl=VoicePackageCatalog.Get(voice.Id).Source.AbsoluteUri }).ToArray(),python,pythonMissing=python==null,runtimeIncludedWithInstall=OperatingSystem.IsMacOS(),runtimeDownloadBytes=25_268_458 };
                }
            case "speech.install":
                using(var http=new HttpClient { Timeout=TimeSpan.FromMinutes(20) })
                {
                    _=VoicePackageCatalog.Get(Text(p,"voiceId"));
                    var manager=new VoicePackageManager(speech,http);
                    PortablePython.AddToProcessPath(speech);
                    if(OperatingSystem.IsMacOS()&&await manager.FindPythonAsync(token)==null) await PortablePython.EnsureAsync(speech,http,token);
                    await manager.InstallAsync(Text(p,"voiceId"),token:token);
                }
                return new { };
            case "speech.remove":
                using(var http=new HttpClient()) await new VoicePackageManager(speech,http).RemoveAsync(Text(p,"voiceId"),token);
                return new { };
            case "speech.synthesize": await OfflineSpeech.SynthesizeAsync(Text(p,"text"),Text(p,"voiceId"),speech,Text(p,"output"),token); return new { };
            case "file.convert": return await FileConversion.ConvertAsync(Text(p,"source"),Text(p,"destination"),Boolean(p,"decode"),(MorseAlphabet)Integer(p,"alphabet"),(char)Integer(p,"separator",'/'),token);
            default: throw new ArgumentException("Unknown command: "+command);
        }
    }

    private static UpdatePlatform UpdatePlatformForProcess()=>RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.Arm64=>UpdatePlatform.MacArm64,
        Architecture.X64=>UpdatePlatform.MacX64,
        _=>throw new PlatformNotSupportedException("Unsupported macOS update architecture.")
    };
    private static object? Asset(ReleaseAsset? asset)=>asset==null?null:new { name=asset.Name,url=asset.Download.AbsoluteUri,size=asset.Size };

    private static async Task<string> Export(QsoRecord[] entries,string format,CancellationToken token)
    {
        if(format is not ("adif" or "csv")) throw new ArgumentException("Unknown export format.");
        string temporary=Path.Combine(Path.GetTempPath(),"morsecore-export-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var export=new RadioStore(temporary);
            foreach(var entry in entries) { token.ThrowIfCancellationRequested(); export.Append(entry); }
            // Keep selected real and practice rows while retaining the Windows export writer.
            string path=Path.Combine(temporary,"selection."+format);
            await export.ExportAsync(path,false,format=="adif",token);
            string real=await File.ReadAllTextAsync(path,token);
            string practicePath=Path.Combine(temporary,"practice."+format);
            await export.ExportAsync(practicePath,true,format=="adif",token);
            string practice=await File.ReadAllTextAsync(practicePath,token);
            int firstLine=practice.IndexOf('\n');
            return real+(firstLine<0?"":practice[(firstLine+1)..]);
        }
        finally { Directory.Delete(temporary,true); }
    }

    private static Dictionary<string,string> Arguments(string[] args)
    {
        if(args.Length!=6) throw new ArgumentException("Expected --data, --speech and --native absolute paths.");
        var options=new Dictionary<string,string>(StringComparer.Ordinal);
        for(int index=0;index<args.Length;index+=2)
            if(args[index] is not ("--data" or "--speech" or "--native") || !options.TryAdd(args[index],args[index+1])) throw new ArgumentException("Invalid arguments.");
        return options;
    }
    private static string RequiredPath(Dictionary<string,string> options,string name)=>options.TryGetValue(name,out string? value)&&Path.IsPathFullyQualified(value)?Path.GetFullPath(value):throw new ArgumentException(name+" requires an absolute path.");
    private static async Task<byte[]> ReadInput(CancellationToken token)
    {
        using var memory=new MemoryStream(); var input=Console.OpenStandardInput(); byte[] buffer=new byte[8192]; int count;
        while((count=await input.ReadAsync(buffer,token))>0) { if(memory.Length+count>MaximumInputBytes) throw new InvalidDataException("Request exceeds 64 MiB."); await memory.WriteAsync(buffer.AsMemory(0,count),token); }
        return memory.ToArray();
    }
    private static T Read<T>(JsonElement p,string name)=>p.GetProperty(name).Deserialize<T>(Json)??throw new ArgumentException("Missing "+name+".");
    private static string Text(JsonElement p,string name,string fallback="")=>p.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetString()??fallback:fallback;
    private static int Integer(JsonElement p,string name,int fallback=0)=>p.TryGetProperty(name,out var value)?value.GetInt32():fallback;
    private static bool Boolean(JsonElement p,string name,bool fallback=false)=>p.TryGetProperty(name,out var value)?value.GetBoolean():fallback;
    private static double? Number(JsonElement p,string name)=>p.TryGetProperty(name,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetDouble():null;
    private sealed record KeyEvent(bool Down,double Milliseconds);
}
