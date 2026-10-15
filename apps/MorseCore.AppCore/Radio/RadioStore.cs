using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MorseCore.AppCore.Radio;

public sealed class RadioStore(string directory)
{
    public string? Warning { get; private set; }
    public StationDocument LoadProfiles()
    {
        string path=Path.Combine(directory,"stations.json");
        try
        {
            if(!File.Exists(path)) return new();
            if(new FileInfo(path).Length>128*1024) throw new InvalidDataException("Station file is too large.");
            var document=JsonSerializer.Deserialize(File.ReadAllText(path),RadioJsonContext.Default.StationDocument) ?? throw new InvalidDataException("Invalid station file.");
            ValidateProfiles(document);
            return document;
        }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Warning=e.Message; return new(); }
    }
    public void SaveProfiles(StationDocument document)
    {
        if(Warning!=null) throw new InvalidDataException("Preserving unreadable station data. Back up and repair the station file first.");
        ValidateProfiles(document);
        AtomicWrite(Path.Combine(directory,"stations.json"),JsonSerializer.Serialize(document,RadioJsonContext.Default.StationDocument));
    }
    public void Append(QsoRecord entry)
    {
        Validate(entry);
        using var lease=WriteLease();
        if(ReadEntries().Any(e=>e.Id==entry.Id)) throw new IOException("QSO identity already exists.");
        AtomicWrite(EntryPath(entry),Serialize(entry),false);
    }
    public IReadOnlyList<QsoRecord> Recent(bool practice,int maximum=50)=>Query(new(practice,Limit:Math.Clamp(maximum,1,100))).Entries;
    public void Update(QsoRecord entry)
    {
        Validate(entry);
        using var lease=WriteLease();
        string? target=null;
        foreach(var item in ReadFiles())
        {
            if(item.Record.Id==entry.Id)
            { if(target!=null) throw new InvalidDataException("Multiple files have the same QSO identity."); target=item.Path; }
        }
        if(target==null) throw new FileNotFoundException("QSO no longer exists.");
        // Keep the original file path even when UTC changes: one atomic replacement, no duplicate or delete gap.
        AtomicWrite(target,Serialize(entry));
    }
    public LogPage Query(LogQuery query,CancellationToken token=default)
    {
        query.Validate();
        var rows=new List<QsoRecord>(query.Limit+1); int total=0;
        (long Ticks,string Id)? cursor=query.Before==null?null:LogQuery.ReadCursor(query.Before);
        foreach(var entry in ReadEntries())
        {
            token.ThrowIfCancellationRequested(); if(!query.Matches(entry)) continue; total++;
            if(cursor is { } before && (entry.Utc.UtcTicks>before.Ticks || entry.Utc.UtcTicks==before.Ticks && string.CompareOrdinal(entry.Id,before.Id)>=0)) continue;
            int index=rows.BinarySearch(entry,Comparer<QsoRecord>.Create(LogQuery.Compare)); if(index<0) index=~index;
            if(index>query.Limit) continue; rows.Insert(index,entry); if(rows.Count>query.Limit+1) rows.RemoveAt(rows.Count-1);
        }
        bool more=rows.Count>query.Limit; if(more) rows.RemoveAt(rows.Count-1);
        return new(rows,total,more?LogQuery.Cursor(rows[^1]):null);
    }
    public LogMap Map(LogQuery query,int maximum=500,CancellationToken token=default)
    {
        query.Validate(); maximum=Math.Clamp(maximum,1,1000); int total=0,located=0; var rows=new List<QsoRecord>(maximum);
        foreach(var entry in ReadEntries())
        {
            token.ThrowIfCancellationRequested(); if(!query.Matches(entry)) continue; total++;
            if(Locator.Position(entry)==null) continue; located++;
            int index=rows.BinarySearch(entry,Comparer<QsoRecord>.Create(LogQuery.Compare)); if(index<0) index=~index;
            if(index>=maximum) continue; rows.Insert(index,entry); if(rows.Count>maximum) rows.RemoveAt(rows.Count-1);
        }
        return new(rows,located,total);
    }
    public ImportPreview PreviewImport(IReadOnlyList<QsoRecord> entries,CancellationToken token=default)
    {
        if(entries.Count>AdifImport.MaximumRecords) throw new ArgumentException("Too many import records.");
        var candidates=new Dictionary<string,QsoRecord>(StringComparer.Ordinal); int duplicates=0;
        foreach(var entry in entries) { token.ThrowIfCancellationRequested(); Validate(entry); if(!candidates.TryAdd(DuplicateKey(entry),entry)) duplicates++; }
        foreach(var existing in ReadEntries()) { token.ThrowIfCancellationRequested(); if(candidates.Remove(DuplicateKey(existing))) duplicates++; }
        return new(candidates.Values.ToArray(),duplicates);
    }
    public ImportResult Import(IReadOnlyList<QsoRecord> confirmed,CancellationToken token=default)
    {
        using var lease=WriteLease();
        // Recheck after confirmation while holding the writer lease; imports are idempotent across instances.
        var preview=PreviewImport(confirmed,token);
        if(preview.Candidates.Count==0) return new(0,preview.Duplicates);
        string batch="import-"+Guid.NewGuid().ToString("N"),temporary=Path.Combine(directory,"."+batch),logs=Path.Combine(directory,"logs");
        Directory.CreateDirectory(temporary); var created=new List<string>();
        try
        {
            foreach(var candidate in preview.Candidates)
            {
                token.ThrowIfCancellationRequested(); var entry=candidate with { Id=Guid.NewGuid().ToString("N") };
                string path=Path.Combine(temporary,Path.GetFileName(EntryPath(entry))); created.Add(path);
                File.WriteAllText(path,Serialize(entry),new UTF8Encoding(false));
            }
            token.ThrowIfCancellationRequested(); Directory.CreateDirectory(logs);
            // Same-volume directory rename exposes the complete batch at once; failed writes expose no partial batch.
            Directory.Move(temporary,Path.Combine(logs,batch));
            return new(preview.Candidates.Count,preview.Duplicates);
        }
        finally
        { if(Directory.Exists(temporary)) { foreach(string path in created) if(File.Exists(path)) File.Delete(path); Directory.Delete(temporary); } }
    }
    public static string DuplicateKey(QsoRecord entry)
    {
        string channel=entry.FrequencyHz>0?"hz:"+entry.FrequencyHz.ToString(CultureInfo.InvariantCulture):"band:"+(AdifBands.Normalize(entry.Band)??entry.Band);
        // Conservative exact matching prevents real contacts within the same minute/band from being discarded.
        bool seconds=entry.TimeHasSeconds!=false;
        return string.Join('|',entry.Utc.UtcDateTime.ToString(seconds?"yyyyMMddHHmmss":"yyyyMMddHHmm",CultureInfo.InvariantCulture),seconds?"seconds":"minutes",CallSigns.Parse(entry.OwnCall).Normalized,CallSigns.Parse(entry.OtherCall).Normalized,channel,entry.Practice?"P":"R");
    }
    private FileStream WriteLease()
    { Directory.CreateDirectory(directory); return new FileStream(Path.Combine(directory,".log-write.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
    private string EntryPath(QsoRecord entry)=>Path.Combine(directory,"logs",entry.Utc.ToUniversalTime().ToString("yyyyMMddTHHmmssfff",CultureInfo.InvariantCulture)+"-"+entry.Id+".json");
    private static string Serialize(QsoRecord entry)=>JsonSerializer.Serialize(entry with { Utc=entry.Utc.ToUniversalTime(),OwnCall=CallSigns.Parse(entry.OwnCall).Normalized,OtherCall=CallSigns.Parse(entry.OtherCall).Normalized,GridSquare=entry.GridSquare.Trim().ToUpperInvariant() },RadioJsonContext.Default.QsoRecord);
    private IEnumerable<QsoRecord> ReadEntries()
        =>ReadFiles().Select(item=>item.Record);
    private IEnumerable<(string Path,QsoRecord Record)> ReadFiles()
    {
        string logs=Path.Combine(directory,"logs");
        if(!Directory.Exists(logs)) yield break;
        foreach(string path in Directory.EnumerateFiles(logs,"*.json",SearchOption.AllDirectories))
        {
            if(new FileInfo(path).Length>32*1024) throw new InvalidDataException("QSO record is too large.");
            QsoRecord record;
            try { record=JsonSerializer.Deserialize(File.ReadAllText(path),RadioJsonContext.Default.QsoRecord) ?? throw new InvalidDataException("Invalid QSO record."); }
            catch(JsonException e) { throw new InvalidDataException("Invalid QSO record.",e); }
            Validate(record); yield return(path,record);
        }
    }
    public async Task ExportAsync(string path,bool practice,bool adif,CancellationToken token=default,bool replaceExisting=false)
    {
        string target=Path.GetFullPath(path),parent=Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(parent);
        string temporary=Path.Combine(parent,".qso-"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            await using(var writer=new StreamWriter(new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None),new UTF8Encoding(false)))
            {
                await writer.WriteLineAsync(adif ? "MorseCore <ADIF_VER:5>3.1.8 <PROGRAMID:9>MorseCore <EOH>" : "UTC,OwnCall,OtherCall,FrequencyMHz,Band,Mode,SentRST,ReceivedRST,Practice,Note,GridSquare,Latitude,Longitude,TimeHasSeconds");
                foreach(var entry in ReadEntries().Where(e=>e.Practice==practice))
                {
                    token.ThrowIfCancellationRequested();
                    string frequency=entry.FrequencyHz==0?"":(entry.FrequencyHz/1_000_000m).ToString("0.######",CultureInfo.InvariantCulture);
                    string Field(string key,string value)=>$"<{key}:{value.Length}>{value} ";
                    string Csv(string value)
                    {
                        // Quoting alone does not prevent spreadsheet formula execution.
                        string trimmed=value.TrimStart();
                        if(trimmed.Length>0 && "=+-@".Contains(trimmed[0])) value="'"+value;
                        return "\""+value.Replace("\"","\"\"")+"\"";
                    }
                    // ADI's basic String fields use ASCII. Preserve Unicode notes in CSV/local JSON.
                    string line=adif ? Field("QSO_DATE",entry.Utc.UtcDateTime.ToString("yyyyMMdd",CultureInfo.InvariantCulture))+Field("TIME_ON",entry.Utc.UtcDateTime.ToString(entry.TimeHasSeconds==false?"HHmm":"HHmmss",CultureInfo.InvariantCulture))+Field("STATION_CALLSIGN",entry.OwnCall)+Field("CALL",entry.OtherCall)+(frequency.Length>0?Field("FREQ",frequency):"")+(AdifBands.Normalize(entry.Band) is { } band?Field("BAND",band):"")+Field("MODE","CW")+Field("RST_SENT",entry.SentRst)+Field("RST_RCVD",entry.ReceivedRst)+Field("APP_MORSECORE_PRACTICE",entry.Practice?"Y":"N")+Field("APP_MORSECORE_COUNTRY_ID",entry.CountryId)+(entry.GridSquare.Length>0?Field("GRIDSQUARE",entry.GridSquare[..Math.Min(8,entry.GridSquare.Length)]):"")+(entry.GridSquare.Length>8?Field("GRIDSQUARE_EXT",entry.GridSquare[8..]):"")+(entry.Latitude is { } lat && entry.Longitude is { } lon?Field("LAT",Locator.ToAdif(lat,true))+Field("LON",Locator.ToAdif(lon,false)):"")+"<EOR>" :
                        string.Join(',',new[]{entry.Utc.ToString("O",CultureInfo.InvariantCulture),entry.OwnCall,entry.OtherCall,frequency,entry.Band,"CW",entry.SentRst,entry.ReceivedRst,entry.Practice.ToString(),entry.Note,entry.GridSquare,entry.Latitude?.ToString("R",CultureInfo.InvariantCulture)??"",entry.Longitude?.ToString("R",CultureInfo.InvariantCulture)??"",(entry.TimeHasSeconds!=false).ToString()}.Select(Csv));
                    await writer.WriteLineAsync(line);
                }
                await writer.FlushAsync(token);
            }
            token.ThrowIfCancellationRequested(); File.Move(temporary,target,replaceExisting);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
    private static bool IsId(string id)=>Guid.TryParseExact(id,"N",out _);
    private static void ValidateProfiles(StationDocument document)
    {
        if(document.Schema!=1 || document.Profiles==null || document.ActiveId==null || document.Profiles.Count>25 || document.Profiles.Any(p=>p==null || !IsId(p.Id)) || document.Profiles.Select(p=>p.Id).Distinct().Count()!=document.Profiles.Count || document.ActiveId.Length>0 && !document.Profiles.Any(p=>p.Id==document.ActiveId)) throw new InvalidDataException("Invalid station collection.");
        foreach(var profile in document.Profiles)
        {
            if(profile.Label==null || profile.Label.Length>80 || profile.OperatorName==null || profile.OperatorName.Length>80 || profile.Qth==null || profile.Qth.Length>80 || RadioCatalog.Country(profile.CountryId) is not { } country || !country.Classes.Contains(profile.ClassId)) throw new InvalidDataException("Invalid station profile.");
            var call=CallSigns.Parse(profile.CallSign);
            if(call.CountryId is { } id && id!=profile.CountryId) throw new InvalidDataException("Station country does not match its call sign.");
        }
    }
    private static void Validate(QsoRecord entry)
    {
        if(entry.Schema!=1 || !IsId(entry.Id) || entry.FrequencyHz is <0 or >300_000_000_000 || entry.FrequencyHz==0 && AdifBands.Normalize(entry.Band??"")==null || entry.Note==null || entry.Note.Length>4000 || entry.Band==null || entry.Band.Length>30 || entry.Utc==default || RadioCatalog.Country(entry.CountryId)==null || entry.SentRst==null || entry.ReceivedRst==null || entry.GridSquare==null) throw new InvalidDataException("Invalid QSO record.");
        _=CallSigns.Parse(entry.OwnCall); _=CallSigns.Parse(entry.OtherCall);
        if(entry.TimeHasSeconds==false && entry.Utc.UtcTicks%TimeSpan.TicksPerMinute!=0) throw new InvalidDataException("Minute-precision QSO time contains seconds.");
        Locator.Validate(entry.GridSquare,entry.Latitude,entry.Longitude);
        if(entry.SentRst.Length>0 && !CwMessages.IsRst(entry.SentRst) || entry.ReceivedRst.Length>0 && !CwMessages.IsRst(entry.ReceivedRst)) throw new InvalidDataException("Invalid RST report.");
    }
    private static void AtomicWrite(string path,string content,bool replace=true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try { File.WriteAllText(temporary,content,new UTF8Encoding(false)); File.Move(temporary,path,replace); }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
