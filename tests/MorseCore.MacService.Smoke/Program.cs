using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using MorseCore.Interop;

if(args.Length!=2) throw new ArgumentException("Usage: smoke <service executable or dll> <native library>");
string service=Path.GetFullPath(args[0]),native=Path.GetFullPath(args[1]);
nint handle=NativeLibrary.Load(native);
NativeLibrary.SetDllImportResolver(typeof(MorseEngine).Assembly,(name,_,_)=>name=="MorseCore.Native"?handle:0);
string temporary=Path.Combine(Path.GetTempPath(),"morsecore-service-smoke-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
int checks=0;
try
{
    var catalog=await Call("catalog",new { });
    Check(catalog.GetProperty("radio").GetProperty("countries").GetArrayLength()>=5,"country catalog");
    Check(catalog.GetProperty("outline").GetProperty("rings").GetArrayLength()>0,"offline world outline");
    Check(catalog.GetProperty("voices").GetArrayLength()==8,"voice catalog");
    var call=await Call("callsign",new { value="TA1PDM" });
    Check(call.GetProperty("countryId").GetString()=="TR"&&call.GetProperty("classId").GetString()=="A","Turkish call sign");
    Check(call.GetProperty("areaDetail").GetString()!.Contains("İstanbul"),"Turkish allocation area");
    var band=await Call("assess",new { countryId="TR",classId="A",frequencyHz=7_025_000 });
    Check(band.GetProperty("status").GetInt32()==0,"band assessment");
    var composed=await Call("compose",new { stage=1,ownCall="TA1PDM",otherCall="DL1ABC",sentRst="599",name="Test",qth="Test",message="" });
    Check(composed.GetProperty("text").GetString()!.Contains("TA1PDM")&&composed.GetProperty("morse").GetString()!.Length>0,"QSO composition");
    var prosign=await Call("cw.encode",new { text="TEST <AR>" });
    Check(prosign.GetProperty("morse").GetString()!.Contains(".-.-."),"prosign encoding");
    string profileId=Guid.NewGuid().ToString("N");
    var document=new { schema=1,activeId=profileId,profiles=new[] { new { id=profileId,label="Synthetic",callSign="TA1PDM",countryId="TR",classId="A",operatorName="Test",qth="Test" } } };
    await Call("profiles.save",new { document });
    var profiles=await Call("profiles.load",new { });
    Check(profiles.GetProperty("document").GetProperty("activeId").GetString()==profileId,"profile round trip");
    var operating=await Call("operating",new { profile=document.profiles[0],context=new { countryId="TR",classId="A",mode=1,region="1",manualCall="" } });
    Check(operating.GetProperty("callSign").GetString()!.Contains("TA1PDM"),"portable operating call");
    string id=Guid.NewGuid().ToString("N");
    var entry=new { schema=1,id,utc="2026-01-02T12:34:00+00:00",ownCall="TA1PDM",otherCall="DL1ABC",countryId="TR",band="40m",frequencyHz=7_025_000,sentRst="599",receivedRst="579",practice=true,note="=synthetic",gridSquare="JO62",timeHasSeconds=false };
    await Call("qso.append",new { entry });
    var page=await Call("qso.query",new { query=new { practice=true,limit=25 } });
    Check(page.GetProperty("total").GetInt32()==1,"QSO query");
    await Call("qso.update",new { entry });
    var map=await Call("qso.map",new { query=new { practice=true },maximum=500 });
    Check(map.GetProperty("located").GetInt32()==1,"QSO locator map");
    var mappedEntry=map.GetProperty("entries")[0];
    Check(mappedEntry.GetProperty("latitude").GetDouble()==52.5&&mappedEntry.GetProperty("longitude").GetDouble()==13,"shared JO62 locator centre");
    var unchangedPage=await Call("qso.query",new { query=new { practice=true,limit=25 } });
    Check(unchangedPage.GetProperty("entries")[0].GetProperty("latitude").ValueKind==JsonValueKind.Null&&unchangedPage.GetProperty("entries")[0].GetProperty("longitude").ValueKind==JsonValueKind.Null,"map coordinates do not change stored QSO");
    var exported=await Call("qso.export",new { entries=new[] { entry },format="adif" });
    var preview=await Call("adif.preview",new { text=exported.GetProperty("text").GetString(),ownCall="TA1PDM",countryId="TR",practice=true });
    Check(preview.GetProperty("preview").GetProperty("duplicates").GetInt32()==1,"ADIF duplicate preview");
    var imported=await Call("adif.import",new { entries=preview.GetProperty("document").GetProperty("records") });
    Check(imported.GetProperty("added").GetInt32()==0&&imported.GetProperty("duplicates").GetInt32()==1,"idempotent ADIF import");
    var csv=await Call("qso.export",new { entries=new[] { entry },format="csv" });
    Check(csv.GetProperty("text").GetString()!.Contains("'=synthetic"),"CSV formula neutralization");
    string directCsv=Path.Combine(temporary,"export.csv"),directAdif=Path.Combine(temporary,"export.adi");
    await Call("qso.export.file",new { destination=directCsv,practice=true,format="csv" });
    string originalCsv=await File.ReadAllTextAsync(directCsv);
    Check(originalCsv.Contains("'=synthetic")&&originalCsv.Contains("DL1ABC"),"direct CSV export formula neutralization");
    await Call("qso.export.file",new { destination=directAdif,practice=true,format="adif" });
    Check((await File.ReadAllTextAsync(directAdif)).Contains("<CALL:6>DL1ABC"),"direct ADIF export");
    await Call("qso.export.file",new { destination=directCsv,practice=false,format="csv" },expectSuccess:false);
    Check(await File.ReadAllTextAsync(directCsv)==originalCsv,"direct export preserves existing destination");
    await Call("qso.export.file",new { destination="relative.csv",practice=true,format="csv" },expectSuccess:false);
    await Call("qso.export.file",new { destination=Path.Combine(temporary,"bad.txt"),practice=true,format="invalid" },expectSuccess:false);
    var questions=await Call("learning.questions",new { mode=0,seed=17 });
    Check(questions.GetArrayLength()>0&&questions[0].GetProperty("morse").GetString()!.Length>0,"learning questions");
    var keyed=await Call("keying.analyze",new { wpm=20,alphabet=0,events=new[] { new { down=true,milliseconds=0d },new { down=false,milliseconds=60d } },finishMilliseconds=1000d,expected="E" });
    Check(keyed.GetProperty("snapshot").GetProperty("text").GetString()=="E"&&keyed.GetProperty("accuracy").GetInt32()==100,"straight key analysis");
    string wave=Path.Combine(temporary,"test.wav");
    MorseAudio.WriteWav(wave,MorseEngine.Encode("SOS SOS").Text,new(12,700));
    var decoded=await Call("decoder.file",new { path=wave,frequencyHz=700,wpm=12,alphabet=0 });
    Check(decoded.GetProperty("text").GetString()=="SOS SOS","WAV decoder round trip");
    string input=Path.Combine(temporary,"input.txt"),output=Path.Combine(temporary,"encoded.txt");
    await File.WriteAllTextAsync(input,"SOS");
    await Call("file.convert",new { source=input,destination=output,decode=false,alphabet=0,separator=47 });
    Check((await File.ReadAllTextAsync(output)).Trim()=="... --- ...","file conversion");
    await Call("file.convert",new { source=input,destination=output,decode=false,alphabet=0,separator=47 },expectSuccess:false);
    Check((await File.ReadAllTextAsync(output)).Trim()=="... --- ...","existing file preserved");
    string stations=Path.Combine(temporary,"radio","stations.json");
    await File.WriteAllTextAsync(stations,"{broken");
    await Call("profiles.save",new { document },expectSuccess:false);
    Check(await File.ReadAllTextAsync(stations)=="{broken","corrupt profile preserved");
    await Call("updates.check",new { currentVersion="invalid" },expectSuccess:false);
    await Call("updates.download",new { version="invalid",cacheDirectory=Path.Combine(temporary,"updates") },expectSuccess:false);
    await Call("updates.download",new { version="0.7.5",cacheDirectory="relative" },expectSuccess:false);
    Check(!Directory.Exists(Path.Combine(temporary,"updates")),"invalid update request creates no cache");
    await Call("unknown",new { },expectSuccess:false);
    Console.WriteLine(JsonSerializer.Serialize(new { ok=true,checks,nativeVersion=MorseEngine.NativeVersion }));
}
finally { Directory.Delete(temporary,true); }

void Check(bool passed,string name) { if(!passed) throw new InvalidOperationException("Failed: "+name); checks++; }
async Task<JsonElement> Call(string command,object payload,bool expectSuccess=true)
{
    var start=new ProcessStartInfo(service.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)?"dotnet":service) { UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8 };
    if(service.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(service);
    foreach(string item in new[] { "--data",Path.Combine(temporary,"radio"),"--speech",Path.Combine(temporary,"speech"),"--native",native }) start.ArgumentList.Add(item);
    using var process=Process.Start(start)??throw new IOException("Service did not start.");
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
    Task<string> response=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();
    await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { command,payload })); process.StandardInput.Close();
    try { await process.WaitForExitAsync(timeout.Token); }
    catch { if(!process.HasExited) process.Kill(true); throw; }
    string text=await response,stderr=await error;
    using var json=JsonDocument.Parse(text);
    Check(json.RootElement.GetProperty("ok").GetBoolean()==expectSuccess,command+": "+text+stderr);
    Check(process.ExitCode==(expectSuccess?0:1),command+" exit status");
    return expectSuccess?json.RootElement.GetProperty("data").Clone():json.RootElement.Clone();
}
