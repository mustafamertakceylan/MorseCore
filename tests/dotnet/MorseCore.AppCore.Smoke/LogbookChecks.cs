using System.Globalization;
using MorseCore.AppCore;
using MorseCore.AppCore.Radio;

internal static class LogbookChecks
{
    public static void Run(Action<bool,string> check)
    {
        void Reject(Action action,string message)
        {
            bool rejected=false; try { action(); } catch(Exception e) when(e is ArgumentException or IOException or InvalidDataException or OperationCanceledException) { rejected=true; }
            check(rejected,message);
        }
        string home=Path.Combine(AppContext.BaseDirectory,"MorseCore-logbook-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(home);
        try
        {
            foreach(string language in Localizer.Languages)
            {
                var localizer=new Localizer(language);
                foreach(string key in new[]{"map","import","duplicateHint","issueMissing","issueMode","issueInvalid","issueRepeated","editInvalid"}) check(localizer["logbook."+key].Length>0,"Logbook translation "+language+key);
            }
            check(Locator.Centre("AA")==new GeoPoint(-85,-170),"Maidenhead two-character cell centre");
            check(Locator.Centre("kn40")==new GeoPoint(40.5,29),"Maidenhead is case-insensitive and returns the cell centre");
            var centre=Locator.Centre("JJ00AA"); check(Math.Abs(centre.Latitude-1d/48)<1e-10 && Math.Abs(centre.Longitude-1d/24)<1e-10,"Maidenhead six-character precision");
            foreach(string grid in new[]{"A","SS","JJ0A","JJ00YY","JJ00AA0X","JJ00AA00YY","JJ00AA00AA0X"}) Reject(()=>Locator.Centre(grid),"Invalid locator rejected: "+grid);
            foreach(string grid in new[]{"AA00AA00AA00","RR99XX99XX99"}) { var position=Locator.Centre(grid); check(position.Latitude is >-90 and <90 && position.Longitude is >-180 and <180,"Twelve-character locator stays within world bounds"); }
            Reject(()=>Locator.Validate("",double.NaN,0),"NaN latitude rejected"); Reject(()=>Locator.Validate("",1,null),"Coordinate pair required");
            check(WorldOutline.Data.Rings.Length>50 && WorldOutline.Data.Rings.Sum(r=>r.Length)<10000 && WorldOutline.Data.License=="Public domain","Small licensed offline coastline loads with generated JSON context");

            var storage=new RadioStore(home);
            var baseRecord=new QsoRecord { Utc=new(2026,10,15,12,00,30,TimeSpan.Zero),OwnCall="TA1PDM",OtherCall="DL1ABC",CountryId="TR",FrequencyHz=14_025_000,Band="20m",SentRst="579",ReceivedRst="589",GridSquare="KN40" };
            var records=new List<QsoRecord>();
            for(int index=0;index<72;index++)
            {
                var entry=baseRecord with { Id=Guid.NewGuid().ToString("N"),Utc=baseRecord.Utc.AddMinutes(index),Practice=index%2==0,OtherCall=index==0?"JA1ABC":"DL1ABC",Band=index%3==0?"40m":"20m",FrequencyHz=index%3==0?7_030_000:14_025_000,GridSquare=index%4==0?"KN40":"" };
                records.Add(entry); storage.Append(entry);
            }
            check(storage.Query(new()).Total==72,"Query sees all records without a Recent limit");
            check(storage.Query(new(Call:"ja1")).Entries.Single().Id==records[0].Id,"Call search reaches records beyond the first fifty");
            check(storage.Query(new(Call:"ta1pdm",Band:"40M")).Total==24,"Own call search and case-insensitive band filter");
            check(storage.Query(new(true)).Total==36 && storage.Query(new(false)).Total==36,"Practice and real contacts stay separate");
            check(storage.Query(new(From:new(2026,10,15),Through:new(2026,10,15))).Total==72 && storage.Query(new(From:new(2026,10,16))).Total==0,"UTC date bounds are inclusive");
            var seen=new HashSet<string>(); string? cursor=null; int pages=0;
            do
            {
                var page=storage.Query(new(Before:cursor,Limit:13));
                check(page.Total==72 && page.Entries.Count<=13,"Cursor page remains bounded and reports all matches");
                foreach(var entry in page.Entries) check(seen.Add(entry.Id),"Cursor pagination never repeats a record");
                cursor=page.NextCursor; pages++;
            } while(cursor!=null);
            check(seen.Count==72 && pages==6,"All pages cover the complete log");
            Reject(()=>storage.Query(new(Before:"../bad")),"Invalid cursor rejected"); Reject(()=>storage.Query(new(Limit:10000)),"Unbounded page rejected"); Reject(()=>storage.Query(new(From:new(2026,10,16),Through:new(2026,10,15))),"Reversed date interval rejected");
            var map=storage.Map(new(),5); check(map.Located==18 && map.Entries.Count==5 && map.Total==72,"Map uses all filtered matches but bounds rendered positions");
            check(Locator.Position(records[1])==null,"Callsign prefixes never create guessed map positions");
            string originalPath=Directory.EnumerateFiles(Path.Combine(home,"logs"),"*-"+records[0].Id+".json").Single();
            var edited=records[0] with { Utc=baseRecord.Utc.AddYears(1),OtherCall="JA2ABC",Note="İstanbul Москва العربية",Latitude=35.5,Longitude=139.5 };
            storage.Update(edited); var latest=storage.Query(new()).Entries[0];
            check(latest.Id==edited.Id && latest.Note==edited.Note && storage.Query(new()).Total==72,"Editing preserves identity and Unicode notes");
            check(File.Exists(originalPath) && latest.Utc==edited.Utc,"Changed UTC sorts correctly while replacing one original file atomically");
            check(Locator.Position(latest)==new GeoPoint(35.5,139.5),"Explicit supplied coordinates take precedence over the cell centre");
            Reject(()=>storage.Update(edited with { Id="..\\stations" }),"Traversal identity rejected before touching disk");
            Reject(()=>storage.Update(edited with { Latitude=100 }),"Invalid edit cannot overwrite a good record");
            check(storage.Query(new()).Entries[0].Latitude==35.5,"Rejected edit preserves original location");
            Reject(()=>storage.Append(edited with { Utc=edited.Utc.AddDays(1) }),"Same identity with changed date cannot create another file");

            string Field(string key,string value,string type="")=>$"<{key}:{value.Length}{(type.Length>0?":"+type:"")}>{value}";
            string Qso(string time="120030",string mode="CW",string? frequency="14.025",string band="20m",string? other="DL1ABC")=>Field("qso_date","20261015","d")+Field("time_on",time,"t")+Field("station_callsign","TA1PDM")+(other==null?"":Field("call",other))+Field("mode",mode)+(frequency==null?"":Field("freq",frequency))+Field("band",band);
            string ascii="Generated by another logger\n<ADIF_VER:5>3.1.8<EOH>"+Qso()+Field("COMMENT","literal <EOR> stays in the comment")+Field("GRIDSQUARE","JO31AA")+"<eor>";
            var parsed=AdifImport.Parse(ascii);
            check(parsed.Records.Count==1 && parsed.Issues.Count==0 && parsed.Total==1,"Optional header, typed lowercase fields and markers parse");
            check(parsed.Records[0].Note.Contains("<EOR>",StringComparison.Ordinal) && parsed.Records[0].GridSquare=="JO31AA","Length framing ignores tags inside values");
            check(parsed.Records[0].Utc.Offset==TimeSpan.Zero && parsed.Records[0].Utc.Hour==12,"Imported timestamps are explicitly UTC");
            var fourDigit=AdifImport.Parse(Qso(time:"1200",frequency:null)+"<EOR>").Records.Single();
            check(fourDigit.FrequencyHz==0 && fourDigit.Band=="20m" && fourDigit.TimeHasSeconds==false,"Band-only and HHMM import preserve unknown frequency and second precision");
            Reject(()=>storage.Append(fourDigit with { Utc=fourDigit.Utc.AddSeconds(1) }),"Minute precision must not hide actual supplied seconds");
            var mixed=AdifImport.Parse(Qso()+"<EOR>"+Qso(mode:"SSB")+"<EOR>"+Qso(other:null)+"<EOR>"+Qso(frequency:"7.03")+"<EOR>");
            check(mixed.Records.Count==1 && mixed.Issues.Count==3 && mixed.Issues.Any(i=>i.Record==2 && i.Key=="logbook.issueMode") && mixed.Issues.Any(i=>i.Record==3 && i.Key=="logbook.issueMissing"),"Bad records are explicitly reported while valid records remain previewable");
            var repeated=AdifImport.Parse(Qso()+Field("CALL","JA1ABC")+"<EOR>"+Qso(time:"130000")+"<EOR>"); check(repeated.Records.Count==1 && repeated.Issues.Single().Key=="logbook.issueRepeated","Duplicate fields reject their record without shifting later record boundaries");
            check(AdifImport.Parse(Qso().Replace("<call:6>","<call:0006>",StringComparison.Ordinal)+"<EOR>").Records.Count==1,"Leading-zero field lengths are accepted on import");
            var fallback=AdifImport.Parse(Qso().Replace(Field("station_callsign","TA1PDM"),Field("OPERATOR","TA2ABC"),StringComparison.Ordinal)+"<EOR>"); check(fallback.Records.Single().OwnCall=="TA2ABC","ADIF OPERATOR supplies station call when STATION_CALLSIGN is absent");
            var manual=AdifImport.Parse(Qso().Replace(Field("station_callsign","TA1PDM"),"",StringComparison.Ordinal)+"<EOR>",new("TA3ABC","TR",false)); check(manual.Records.Single().OwnCall=="TA3ABC" && !manual.Records.Single().Practice,"Explicit defaults fill only missing source fields");
            var marked=AdifImport.Parse(Qso()+Field("APP_MORSECORE_PRACTICE","Y","b")+Field("APP_MORSECORE_COUNTRY_ID","DE")+"<EOR>",new(Practice:false)); check(marked.Records.Single().Practice && marked.Records.Single().CountryId=="DE","Source provenance and operating country override import defaults without inferring rights");
            var location=AdifImport.Parse(Qso()+Field("GRIDSQUARE","FN01MH42")+Field("GRIDSQUARE_EXT","BQ")+Field("LAT","N041 30.000")+Field("LON","W072 30.000")+"<EOR>").Records.Single();
            check(location.GridSquare=="FN01MH42BQ" && location.Latitude==41.5 && location.Longitude==-72.5,"Extended locator and ADIF degree-minute coordinates import");
            check(AdifImport.Parse(Qso()+Field("FOO","bar")+"<EOR>").IgnoredFields==1,"Unsupported extra fields are counted in the import summary");
            foreach(string invalid in new[]{Qso(),Qso()+"<BROKEN:999999999999>x<EOR>",Qso()+"<COMMENT:99>x<EOR>","Generated without EOH",Qso()+"<COMMENT:1>Iş<EOR>",Qso()+"<EOH><EOR>",Qso()+"<EOR:0>"}) Reject(()=>AdifImport.Parse(invalid),"Malformed ADIF never yields a confirmable partial parse");
            foreach(string invalidRecord in new[]{Qso(time:"250000"),Qso(frequency:"14,025"),Qso(frequency:"14.0250001"),Qso()+Field("GRIDSQUARE","ZZ99"),Qso()+Field("APP_MORSECORE_PRACTICE","X")})
                check(AdifImport.Parse(invalidRecord+"<EOR>").Records.Count==0,"Invalid CW values are surfaced as record issues");
            Reject(()=>AdifImport.Parse(new string('x',AdifImport.MaximumBytes+1)),"Oversized import bounded before parsing");

            string imports=Path.Combine(home,"imports-test"); var destination=new RadioStore(imports);
            var sameMinute=parsed.Records[0] with { Utc=parsed.Records[0].Utc.AddSeconds(10),OwnCall=" ta1pdm ",OtherCall="dl1abc" };
            var differentFrequency=parsed.Records[0] with { FrequencyHz=14_030_000 };
            var exactDuplicate=parsed.Records[0] with { OwnCall=" ta1pdm ",OtherCall="dl1abc" };
            var actual=parsed.Records[0] with { Practice=false };
            var preview=destination.PreviewImport([parsed.Records[0],sameMinute,differentFrequency,exactDuplicate,actual]);
            check(preview.Candidates.Count==4 && preview.Duplicates==1,"Only exact duplicates merge; different seconds, frequencies and record types remain separate");
            check(!Directory.Exists(Path.Combine(imports,"logs")),"Import preview never writes contacts");
            var imported=destination.Import(preview.Candidates); check(imported.Added==4 && destination.Query(new(true)).Total==3 && destination.Query(new(false)).Total==1,"Confirmed import publishes all valid records atomically");
            var again=destination.Import(preview.Candidates); check(again.Added==0 && again.Duplicates==4,"Repeated import is idempotent against persisted logs");
            check(destination.PreviewImport([fourDigit]).Duplicates==0,"Missing frequency or time precision never silently merges a possibly different contact");
            Reject(()=>destination.Import([actual with { Utc=actual.Utc.AddDays(1) }],new CancellationToken(true)),"Cancelled import exposes no partial records");
            check(destination.Query(new()).Total==4 && !Directory.EnumerateDirectories(imports,".import-*").Any(),"Cancelled import leaves no staged batch");
            string adi=Path.Combine(home,"roundtrip.adi"); destination.ExportAsync(adi,false,true).GetAwaiter().GetResult();
            var roundtrip=AdifImport.Parse(File.ReadAllText(adi)).Records.Single(); check(!roundtrip.Practice && roundtrip.GridSquare=="JO31AA" && roundtrip.Band=="20m" && roundtrip.FrequencyHz==14_025_000,"ADIF round trip retains coordinates, band, frequency and provenance");
            destination.Append(location with { Id=Guid.NewGuid().ToString("N"),Utc=location.Utc.AddDays(2),Practice=false });
            string coordinates=Path.Combine(home,"coordinates.adi"); destination.ExportAsync(coordinates,false,true).GetAwaiter().GetResult();
            var withCoordinates=AdifImport.Parse(File.ReadAllText(coordinates)).Records.Single(e=>e.GridSquare==location.GridSquare); check(withCoordinates.Latitude==location.Latitude && withCoordinates.Longitude==location.Longitude,"ADIF coordinate and extended grid export round trip");
            string bandOnlyFile=Path.Combine(home,"band-only.adi"); var bandOnly=new RadioStore(Path.Combine(home,"band-only")); bandOnly.Append(fourDigit); bandOnly.ExportAsync(bandOnlyFile,true,true).GetAwaiter().GetResult(); check(!File.ReadAllText(bandOnlyFile).Contains("<FREQ",StringComparison.Ordinal) && AdifImport.Parse(File.ReadAllText(bandOnlyFile)).Records.Single().FrequencyHz==0 && File.ReadAllText(bandOnlyFile).Contains("<TIME_ON:4>1200",StringComparison.Ordinal),"Band-only export omits frequency and retains the original HHMM time precision");
            string legacy=Path.Combine(home,"legacy","logs"); Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(legacy,"old.json"),"{\"schema\":1,\"id\":\""+Guid.NewGuid().ToString("N")+"\",\"utc\":\"2026-10-15T12:00:00Z\",\"ownCall\":\"TA1PDM\",\"otherCall\":\"DL1ABC\",\"countryId\":\"TR\",\"frequencyHz\":14025000,\"band\":\"20m\",\"sentRst\":\"\",\"receivedRst\":\"\",\"practice\":true,\"note\":\"\"}");
            var legacyRecord=new RadioStore(Path.GetDirectoryName(legacy)!).Recent(true).Single(); check(legacyRecord.GridSquare=="" && legacyRecord.TimeHasSeconds!=false,"Existing schema 1 records without new fields retain known second precision and no location");
            File.WriteAllText(Path.Combine(imports,"logs","corrupt.json"),"{");
            int count=Directory.EnumerateFiles(Path.Combine(imports,"logs"),"*.json",SearchOption.AllDirectories).Count();
            Reject(()=>destination.Import([actual with { Utc=actual.Utc.AddDays(8) }]),"Corrupt existing log prevents import rather than hiding data loss");
            check(Directory.EnumerateFiles(Path.Combine(imports,"logs"),"*.json",SearchOption.AllDirectories).Count()==count,"Failed import preserves all source and existing files");
        }
        finally { Directory.Delete(home,true); }
    }
}
