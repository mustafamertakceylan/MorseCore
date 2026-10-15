using System.Globalization;
using System.Text.Json;
using MorseCore.AppCore;
using MorseCore.AppCore.Radio;
using MorseCore.Interop;

internal static class RadioChecks
{
    public static async Task<int> Run(string directory)
    {
        int checks=0;
        void Check(bool value,string message) { if(!value) throw new Exception(message); checks++; }
        void Reject(Action action,string message)
        { try { action(); } catch(Exception e) when(e is ArgumentException or IOException or InvalidDataException) { checks++; return; } throw new Exception(message); }
        var data=RadioCatalog.Data;
        Check(data.Countries.Length==8 && data.ReviewedOn==new DateOnly(2026,10,15),"Dated seven-country catalog plus manual profile");
        foreach(var language in Localizer.Languages)
        {
            var localizer=new Localizer(language);
            foreach(var country in data.Countries)
            { Check(localizer[country.NameKey].Length>0 && localizer[country.NotesKey].Length>0,"Country rules translated: "+language+country.Id); }
            foreach(var term in CwLearning.Terms) Check(localizer["term_"+term.Key].Length>0,"Glossary translation: "+language+term.Key);
        }
        foreach(var sample in new[]{("TA1PDM","TR","A","1"),("TB2CAA","TR","C","2"),("TB2ABC","TR","B","2"),("TC100ABC","TR", "", ""),("DL1ABC","DE","A",""),("DO1ABC","DE","E",""),("DN9ABC","DE","N",""),("R1ABC","RU","",""),("UA3ABC","RU","",""),("M8ABC","GB","",""),("2E0ABC","GB","",""),("K1ABC","US","",""),("AA1ABC","US","",""),("HZ1AA","SA","",""),("A61AA","AE","A",""),("A65ABC","AE","",""),("A67ABC","AE","B",""),("JA1ABC","","","")})
        {
            var call=CallSigns.Parse(sample.Item1);
            Check((call.CountryId??"")==sample.Item2 && (call.ClassId??"")==sample.Item3 && (call.Region??"")==sample.Item4,"Call classification: "+sample.Item1);
        }
        Check(CallSigns.Parse(" dl/ta1pdm/p ").BaseCall=="TA1PDM","Compound call retains one base call");
        Check(CallSigns.Parse("ME0ABC").CountryId=="GB" && CallSigns.Parse("GB0ABC").CountryId=="GB" && CallSigns.Parse("DE1ABC").CountryId==null,"Current British RSL and excluded German prefixes");
        foreach(string invalid in new[]{"","TA1","TA1PDM//P","TA1PDM/DL1ABC","TA1PDM<SK>","TAİPDM",new string('A',33)}) Reject(()=>CallSigns.Parse(invalid),"Malformed call accepted");
        var station=new StationProfile { Label="Test",CallSign="TA1PDM",CountryId="TR",ClassId="A" };
        Check(CallSigns.Operating(station,new("TR","A",StationMode.Fixed,"2")).CallSign=="TA1PDM/2","Turkish region-change suffix proposal");
        Check(CallSigns.TurkishArea("1")!.Contains("Tekirdağ",StringComparison.Ordinal) && CallSigns.TurkishArea("2")!.Contains("Ankara",StringComparison.Ordinal),"Official Turkish call area geography");
        Reject(()=>CallSigns.Operating(station,new("TR","A",StationMode.Fixed,"X")),"Invalid operating region silently ignored");
        Check(CallSigns.Operating(station,new("DE","A",StationMode.Portable)).NeedsConfirmation,"Cross-border rights are never inferred");
        Reject(()=>CallSigns.Operating(station,new("DE","A",StationMode.Portable,ManualCall:"DL1ABC")),"Foreign base call accepted as own call");
        Check(CallSigns.Operating(station,new("DE","A",StationMode.Portable,ManualCall:"DL/TA1PDM/P")).CallSign=="DL/TA1PDM/P","Manually confirmed visitor call");
        var sa=station with { Id=Guid.NewGuid().ToString("N"),CallSign="HZ1AA",CountryId="SA",ClassId="1" };
        Check(CallSigns.Operating(sa,new("SA","1",StationMode.Mobile)).CallSign=="HZ1AA/M","CST mobile suffix");
        var n=RadioCatalog.Assess("DE","N",28_100_000);
        Check(n.Status==BandStatus.Match && n.Power is { Watts:10,Basis:"ERP" },"German N uses ERP, not PEP");
        Check(RadioCatalog.Assess("DE","N",14_025_000).Status==BandStatus.OutsideListedRanges,"German N has no listed 20m privilege");
        Check(RadioCatalog.Assess("AE","B",144_100_000).Power?.Watts==50 && RadioCatalog.Assess("AE","B",14_025_000).Status!=BandStatus.Match,"TDRA novice VHF limits");
        Check(RadioCatalog.Assess("SA","2",14_025_000).Status!=BandStatus.Match && RadioCatalog.Assess("SA","2",47_050_000_000).Power?.Watts==50,"CST classes differ");
        Check(RadioCatalog.Assess("RU","1",14_025_000).Status!=BandStatus.Match,"Inaccessible Russian annex never grants permissions");
        Check(RadioCatalog.Assess("US","General",14_020_000).Status!=BandStatus.Match && RadioCatalog.Assess("US","Extra",14_020_000).Power?.Watts==1500,"FCC class-specific CW lower edge");
        Check(RadioCatalog.Assess("US","Technician",7_030_000).Power?.Watts==200,"FCC technician HF power");
        Check(RadioCatalog.Assess("GB","Foundation",7_030_000).Power?.Watts==25,"Ofcom foundation 2024+ power");
        Check(RadioCatalog.Assess("GB","Full",10_120_000).Power?.Watts==400,"Ofcom secondary 30m power");
        Check(RadioCatalog.Assess("TR","B",7_030_000).Band?.ConditionsKey=="trHomebuilt","Conditional home-built exception retains condition");
        foreach(var country in data.Countries)
            foreach(var band in country.Bands)
                foreach(var power in band.Powers)
                {
                    var match=RadioCatalog.Assess(country.Id,power.ClassId,band.StartHz);
                    Check(match.Status==BandStatus.Match && match.Power==power,"Country/class lower edge: "+country.Id+band.Band+power.ClassId);
                }
        Check(RadioCatalog.Assess("AE","B",146_000_000).Status!=BandStatus.Match,"Upper edge excluded");
        Check(RadioCatalog.Assess("TR","?",14_025_000).Status==BandStatus.ClassUnknown && RadioCatalog.Assess("XX","A",14_025_000).Status==BandStatus.CountryUnknown,"Unknown country/class cannot match");
        Check(CwMessages.Encode("<AR>")==".-.-." && CwMessages.Encode("<SK>")=="...-.-","Real joined prosign encoding");
        Check(MorseAudio.Analyze(CwMessages.Encode("AR")).Units==MorseAudio.Analyze(CwMessages.Encode("<AR>")).Units+2,"Prosign has one-unit internal gaps in actual audio engine");
        string wave=Path.Combine(directory,"joined-prosign.wav"); MorseAudio.WriteWav(wave,CwMessages.Encode("<AR>"));
        Check(File.ReadAllBytes(wave).AsSpan(0,4).SequenceEqual("RIFF"u8),"Joined prosign WAV renders through native engine");
        Check(CwMessages.Encode("A R").Contains(" / ") && CwMessages.Encode("AR")==".- .-.","Prosign differs from separated letters and words");
        Check(CwMessages.Encode("CQ <AR>").EndsWith(" / .-.-.",StringComparison.Ordinal),"Message word gap before prosign");
        Reject(()=>CwMessages.Encode("<UNKNOWN>"),"Unknown prosign accepted");
        Reject(()=>CwMessages.Compose(QsoStage.Report,"TA1PDM","DL1ABC","","","",""),"Invented default RST report");
        foreach(QsoStage stage in Enum.GetValues<QsoStage>())
        {
            string message=CwMessages.Compose(stage,"TA1PDM","DL1ABC","579","Ali","Ankara","TNX FER CALL");
            Check(message.Contains("TA1PDM",StringComparison.Ordinal) && CwMessages.Encode(message).Length>0,"CW stage: "+stage);
        }
        Check(!CwMessages.IsRst("699") && !CwMessages.IsRst("590") && CwMessages.IsRst("579"),"Actual CW RST digit constraints");
        for(int mode=0;mode<6;mode++)
        {
            var questions=CwLearning.Questions(mode,17); var lesson=new CwLesson(questions);
            Check(questions.Count is >0 and <=10,"Bounded learning session");
            var first=lesson.Next()!; Check(lesson.Submit(first.Answer.ToLowerInvariant()) && lesson.Correct==1,"Case-insensitive practice");
            lesson.Next(); lesson.Submit("wrong"); Check(lesson.Missed.Count==1 && lesson.Incorrect==1,"Mistakes retained");
            var retry=new CwLesson(lesson.Missed); Check(retry.Next()!=null,"Wrong-answer review session");
        }
        var prosignLesson=new CwLesson([new("<AR>",".-.-.")]); prosignLesson.Next(); Check(prosignLesson.Submit("ar"),"Prosign exercises accept familiar unbracketed answers");
        string home=Path.Combine(directory,"Radio"); var storage=new RadioStore(home);
        var document=new StationDocument { ActiveId=station.Id,Profiles=[station,sa] }; storage.SaveProfiles(document);
        Check(storage.LoadProfiles().Profiles.Count==2 && storage.LoadProfiles().ActiveId==station.Id,"Multiple profiles persist and restore selection");
        var invalidDoc=document with { Profiles=[station with { ClassId="?" }] };
        Reject(()=>storage.SaveProfiles(invalidDoc),"Invalid class saved");
        Check(storage.LoadProfiles().Profiles.Count==2,"Rejected save preserves existing profiles");
        var practice=new QsoRecord { Utc=new DateTimeOffset(2026,10,15,23,59,58,TimeSpan.FromHours(3)),OwnCall="TA1PDM",OtherCall="DL1ABC",CountryId="TR",Band="20m",FrequencyHz=14_025_000,SentRst="579",ReceivedRst="589",Note="İstanbul, \"test\"\nМосква العربية",Practice=true };
        storage.Append(practice); storage.Append(practice with { Id=Guid.NewGuid().ToString("N"),Practice=false,Note="=1+1" });
        Check(storage.Recent(true).Count==1 && storage.Recent(false).Count==1,"Training and actual contacts are separate");
        Check(storage.Recent(true)[0].Utc.Offset==TimeSpan.Zero && storage.Recent(true)[0].Utc.Hour==20,"Stored QSO times are UTC");
        Reject(()=>storage.Append(practice),"Duplicate QSO overwrote a log file");
        string csv=Path.Combine(home,"practice.csv"),adi=Path.Combine(home,"contacts.adi");
        var originalCulture=CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE"); await storage.ExportAsync(csv,true,false); await storage.ExportAsync(adi,false,true); }
        finally { CultureInfo.CurrentCulture=originalCulture; }
        string csvText=await File.ReadAllTextAsync(csv),adiText=await File.ReadAllTextAsync(adi);
        Check(csvText.Contains("14.025",StringComparison.Ordinal) && csvText.Contains("Москва العربية",StringComparison.Ordinal) && csvText.Contains("\"\"test\"\"",StringComparison.Ordinal),"Invariant CSV frequency, quoted fields and Unicode notes");
        Check(adiText.Contains("<TIME_ON:6>205958",StringComparison.Ordinal) && adiText.Contains("<CALL:6>DL1ABC",StringComparison.Ordinal) && adiText.Contains("<EOR>",StringComparison.Ordinal),"ADIF length framing and UTC");
        Check(adiText.Contains("<APP_MORSECORE_PRACTICE:1>N",StringComparison.Ordinal),"ADIF retains actual/practice provenance");
        string realCsv=Path.Combine(home,"real.csv"); await storage.ExportAsync(realCsv,false,false); Check((await File.ReadAllTextAsync(realCsv)).Contains("'\u003d1+1",StringComparison.Ordinal),"CSV formulas neutralised");
        string before=await File.ReadAllTextAsync(csv);
        try { await storage.ExportAsync(csv,false,false); throw new Exception("Existing export replaced"); } catch(IOException) { checks++; }
        Check(await File.ReadAllTextAsync(csv)==before,"Existing output remains intact");
        string reserved=Path.Combine(home,"picker.csv"); File.WriteAllText(reserved,""); await storage.ExportAsync(reserved,true,false,replaceExisting:true);
        Check((await File.ReadAllTextAsync(reserved)).Contains("DL1ABC",StringComparison.Ordinal),"Explicit picker reservation can be filled atomically");
        string cancelled=Path.Combine(home,"cancelled.csv");
        try { await storage.ExportAsync(cancelled,true,false,new CancellationToken(true)); throw new Exception("Cancellation ignored"); } catch(OperationCanceledException) { Check(!File.Exists(cancelled),"Cancelled export leaves no output"); }
        File.WriteAllText(Path.Combine(home,"stations.json"),"{\"schema\":1,\"profiles\":null}");
        var corrupt=new RadioStore(home); Check(corrupt.LoadProfiles().Profiles.Count==0 && corrupt.Warning!=null,"Malformed/null station document handled");
        Reject(()=>corrupt.SaveProfiles(document),"Corrupted station file overwritten");
        Check(File.ReadAllText(Path.Combine(home,"stations.json")).Contains("null",StringComparison.Ordinal),"Corrupt data preserved for recovery");
        Check(!Directory.EnumerateFiles(home,"*.tmp",SearchOption.AllDirectories).Any(),"No export or profile temporary file leaks");
        File.WriteAllText(Path.Combine(home,"logs","corrupt.json"),"{");
        Reject(()=>storage.Recent(true),"Corrupt log was silently ignored");
        Console.WriteLine($"Radio and education: {checks} checks passed."); return checks;
    }
}
