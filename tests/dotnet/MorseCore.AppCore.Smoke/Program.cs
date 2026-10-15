using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using MorseCore.AppCore;
using MorseCore.Interop;

string library=Path.GetFullPath(args[0]);
NativeLibrary.SetDllImportResolver(typeof(MorseEngine).Assembly,(_,_,_)=>NativeLibrary.Load(library));
int checks=0;
void Check(bool condition,string label) { if(!condition) throw new Exception(label); checks++; }
string directory=Path.Combine(Path.GetDirectoryName(library)!,"morsecore-app-test-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var keys=new Localizer("en").Keys.Order().ToArray();
    foreach(string language in Localizer.Languages)
    {
        var localizer=new Localizer(language);
        Check(localizer.Keys.Order().SequenceEqual(keys),"Complete translation keys: "+language);
        Check(localizer.Keys.All(key=>!string.IsNullOrWhiteSpace(localizer[key])),"Nonempty translations: "+language);
        Check(localizer.IsRtl==(language=="ar"),"RTL locale: "+language);
    }
    var store=new PreferencesStore(Path.Combine(directory,"settings.json"));
    var settings=new Preferences { Language="ar",Theme="dark",Alphabet=MorseAlphabet.Arabic,ReduceMotion=true,Separator='#' };
    store.Save(settings); Check(store.Load()==settings,"Preferences roundtrip");
    File.WriteAllText(Path.Combine(directory,"settings.json"),"{bad json");
    Check(store.Load().Language=="tr" && store.LoadWarning!=null,"Corrupt preferences recovery");
    var validated=(new Preferences { Language="xx",Theme="xx",Alphabet=(MorseAlphabet)99,Wpm=0,Frequency=99999,Separator='x' }).Validated();
    Check(validated.Language=="en" && validated.Theme=="system" && validated.Alphabet==MorseAlphabet.Latin && validated.Wpm==5 && validated.Frequency==2000 && validated.Separator=='/',"Invalid preferences validation");
    foreach(var pair in new[]{("Привет мир",MorseAlphabet.Russian,"ПРИВЕТ МИР"),("سَلَامٌ عالم",MorseAlphabet.Arabic,"سلام عالم")})
    {
        var encoded=MorseEngine.Encode(pair.Item1,pair.Item2);
        Check(MorseEngine.Decode(encoded.Text,pair.Item2).Text==pair.Item3,"Native multilingual roundtrip");
        Check(encoded.Statistics.Words==2 && encoded.Statistics.InputCharacters==(ulong)pair.Item1.EnumerateRunes().Count(),"Unicode statistics");
        foreach(var symbol in MorseEngine.Alphabet(pair.Item2))
            Check(MorseEngine.Decode(MorseEngine.Encode(symbol.Character.ToString(),pair.Item2).Text,pair.Item2).Text==symbol.Character.ToString(),"Native alphabet consumer");
    }
    string input=Path.Combine(directory,"вход.txt"),output=Path.Combine(directory,"نتيجة.txt");
    await File.WriteAllTextAsync(input,"Привет мир",new UTF8Encoding(false,true));
    await FileConversion.ConvertAsync(input,output,false,MorseAlphabet.Russian,'/');
    Check(MorseEngine.Decode(await File.ReadAllTextAsync(output),MorseAlphabet.Russian).Text=="ПРИВЕТ МИР","UTF-8 file conversion and paths");
    string original=await File.ReadAllTextAsync(output);
    try { await FileConversion.ConvertAsync(input,output,false,MorseAlphabet.Russian,'/'); throw new Exception("Existing file replaced"); }
    catch(IOException) { Check(await File.ReadAllTextAsync(output)==original,"Existing output preserved"); }
    Check(!Directory.GetFiles(directory,"*.tmp").Any(),"Atomic output cleanup");
    using(var cancelled=new CancellationTokenSource())
    {
        cancelled.Cancel();
        try { await FileConversion.ConvertAsync(input,Path.Combine(directory,"cancelled.txt"),false,MorseAlphabet.Russian,'/',cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch(OperationCanceledException) { Check(!File.Exists(Path.Combine(directory,"cancelled.txt")),"Cancelled file creates no output"); }
    }
    await File.WriteAllBytesAsync(input,[0xff,0xfe]);
    try { await FileConversion.ConvertAsync(input,Path.Combine(directory,"invalid.txt"),false,MorseAlphabet.Russian,'/'); throw new Exception("Invalid UTF8 accepted"); }
    catch(DecoderFallbackException) { Check(!File.Exists(Path.Combine(directory,"invalid.txt")),"Invalid UTF8 rejected"); }
    await File.WriteAllBytesAsync(input,new byte[FileConversion.MaxInputBytes+1]);
    try { await FileConversion.ConvertAsync(input,Path.Combine(directory,"large.txt"),false,MorseAlphabet.Russian,'/'); throw new Exception("Large file accepted"); }
    catch(IOException) { Check(!File.Exists(Path.Combine(directory,"large.txt")),"File size bound"); }
    foreach(var release in new[]{("v0.4.0",false,false,true),("v0.1.0",false,false,false),("v9.0.0",true,false,false),("v9.0.0",false,true,false),("weird",false,false,false)})
    {
        string body=JsonSerializer.Serialize(new { tag_name=release.Item1,prerelease=release.Item2,draft=release.Item3,html_url="https://github.com/mustafamertakceylan/MorseCore/releases/tag/"+release.Item1 });
        using var client=new HttpClient(new ResponseHandler(HttpStatusCode.OK,body));
        Check((await new UpdateService(client).CheckAsync(new Version(0,2,0))!=null)==release.Item4,"Stable release version policy");
    }
    using(var client=new HttpClient(new ResponseHandler(HttpStatusCode.NotFound,"")))
        Check(await new UpdateService(client).CheckAsync(new Version(0,2,0))==null,"No published release");
    using(var client=new HttpClient(new ResponseHandler(HttpStatusCode.OK,"{\"tag_name\":\"v1.0.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://example.com/fake\"}")))
    {
        try { await new UpdateService(client).CheckAsync(new Version(0,2,0)); throw new Exception("Foreign release URL accepted"); }
        catch(InvalidDataException) { checks++; }
    }
    using(var client=new HttpClient(new ResponseHandler(HttpStatusCode.OK,new string(' ',256*1024+1))))
    {
        try { await new UpdateService(client).CheckAsync(new Version(0,2,0)); throw new Exception("Unbounded metadata accepted"); }
        catch(InvalidDataException) { checks++; }
    }
    foreach(string language in new[]{"tr-TR","ar-SA","de-DE","ru-RU","en-US"})
        Check(OfflineSpeech.ForLanguage(language).Count>0,"Offline voice coverage: "+language);
    Check(OfflineSpeech.ForLanguage("tr-TR").Select(v=>v.Engine).ToHashSet().SetEquals(["ema","antalia"]),"Two Turkish neural engines");
    Check(!OfflineSpeech.IsInstalled(directory,OfflineSpeech.Voices[0]),"Missing speech pack detected");
    try { await OfflineSpeech.SynthesizeAsync("test","unknown",directory,Path.Combine(directory,"bad.wav")); throw new Exception("Unknown voice accepted"); }
    catch(ArgumentException) { checks++; }
    try { await OfflineSpeech.SynthesizeAsync(new string('x',4001),"ema-tr",directory,Path.Combine(directory,"bad.wav")); throw new Exception("Speech limit ignored"); }
    catch(ArgumentException) { checks++; }
    if(Environment.GetEnvironmentVariable("MORSECORE_SPEECH_HOME") is { Length:>0 } speechHome)
    {
        var examples=new Dictionary<string,string> { ["tr-TR"]="Bugün saat on dört otuzda toplantımız var. Morse Core ses denemesi.",["ar-SA"]="مرحبا بكم في تطبيق مورس. هذا اختبار للصوت باللغة العربية.",["de-DE"]="Willkommen bei Morse Core. Dies ist ein deutscher Sprachtest.",["ru-RU"]="Добро пожаловать в Морзе. Это проверка русского голоса.",["en-US"]="Welcome to Morse Core. This is an offline speech test." };
        foreach(var voice in OfflineSpeech.Voices)
        {
            string wave=Path.Combine(directory,voice.Id+".wav");
            Check(OfflineSpeech.IsInstalled(speechHome,voice),"Speech pack installed: "+voice.Id);
            await OfflineSpeech.SynthesizeAsync(examples[voice.Language],voice.Id,speechHome,wave);
            byte[] data=await File.ReadAllBytesAsync(wave);
            Check(data.Length>1000 && data.Skip(44).Any(b=>b!=0),"Real offline synthesis: "+voice.Id);
        }
        string cancelledOutput=Path.Combine(directory,"speech-cancelled.wav");
        using(var cancellation=new CancellationTokenSource(100))
        {
            try { await OfflineSpeech.SynthesizeAsync(examples["tr-TR"],"ema-tr",speechHome,cancelledOutput,cancellation.Token); throw new Exception("Speech cancellation ignored"); }
            catch(OperationCanceledException) { Check(!File.Exists(cancelledOutput),"Cancelled speech worker exits and removes output"); }
        }
        Check(!Directory.GetFiles(directory,"*.download").Any(),"Synthesis never downloads model files");
    }
    Console.WriteLine($"App core: {checks} checks passed.");
}
finally { Directory.Delete(directory,true); }

sealed class ResponseHandler(HttpStatusCode status,string body) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
    {
        if(request.Headers.UserAgent.Count==0) throw new Exception("Missing GitHub API user agent");
        return Task.FromResult(new HttpResponseMessage(status) { Content=new StringContent(body) });
    }
}
