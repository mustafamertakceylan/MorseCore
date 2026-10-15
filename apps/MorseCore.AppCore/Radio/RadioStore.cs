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
        string path=Path.Combine(directory,"logs",entry.Utc.ToUniversalTime().ToString("yyyyMMddTHHmmssfff",CultureInfo.InvariantCulture)+"-"+entry.Id+".json");
        AtomicWrite(path,JsonSerializer.Serialize(entry with { Utc=entry.Utc.ToUniversalTime() },RadioJsonContext.Default.QsoRecord),false);
    }
    public IReadOnlyList<QsoRecord> Recent(bool practice,int maximum=50)=>ReadEntries().Where(e=>e.Practice==practice).Take(Math.Clamp(maximum,1,100)).ToArray();
    private IEnumerable<QsoRecord> ReadEntries()
    {
        string logs=Path.Combine(directory,"logs");
        if(!Directory.Exists(logs)) yield break;
        foreach(string path in Directory.EnumerateFiles(logs,"*.json").OrderByDescending(Path.GetFileName,StringComparer.Ordinal))
        {
            if(new FileInfo(path).Length>32*1024) throw new InvalidDataException("QSO record is too large.");
            QsoRecord record;
            try { record=JsonSerializer.Deserialize(File.ReadAllText(path),RadioJsonContext.Default.QsoRecord) ?? throw new InvalidDataException("Invalid QSO record."); }
            catch(JsonException e) { throw new InvalidDataException("Invalid QSO record.",e); }
            Validate(record); yield return record;
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
                await writer.WriteLineAsync(adif ? "MorseCore <ADIF_VER:5>3.1.4 <PROGRAMID:9>MorseCore <EOH>" : "UTC,OwnCall,OtherCall,FrequencyMHz,Band,Mode,SentRST,ReceivedRST,Practice,Note");
                foreach(var entry in ReadEntries().Where(e=>e.Practice==practice))
                {
                    token.ThrowIfCancellationRequested();
                    string frequency=(entry.FrequencyHz/1_000_000m).ToString("0.######",CultureInfo.InvariantCulture);
                    string Field(string key,string value)=>$"<{key}:{value.Length}>{value} ";
                    string Csv(string value)
                    {
                        // Quoting alone does not prevent spreadsheet formula execution.
                        string trimmed=value.TrimStart();
                        if(trimmed.Length>0 && "=+-@".Contains(trimmed[0])) value="'"+value;
                        return "\""+value.Replace("\"","\"\"")+"\"";
                    }
                    // ADI's basic String fields use ASCII. Preserve Unicode notes in CSV/local JSON.
                    string line=adif ? Field("QSO_DATE",entry.Utc.UtcDateTime.ToString("yyyyMMdd",CultureInfo.InvariantCulture))+Field("TIME_ON",entry.Utc.UtcDateTime.ToString("HHmmss",CultureInfo.InvariantCulture))+Field("STATION_CALLSIGN",entry.OwnCall)+Field("CALL",entry.OtherCall)+Field("FREQ",frequency)+Field("MODE","CW")+Field("RST_SENT",entry.SentRst)+Field("RST_RCVD",entry.ReceivedRst)+Field("APP_MORSECORE_PRACTICE",entry.Practice?"Y":"N")+"<EOR>" :
                        string.Join(',',new[]{entry.Utc.ToString("O",CultureInfo.InvariantCulture),entry.OwnCall,entry.OtherCall,frequency,entry.Band,"CW",entry.SentRst,entry.ReceivedRst,entry.Practice.ToString(),entry.Note}.Select(Csv));
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
        if(entry.Schema!=1 || !IsId(entry.Id) || entry.FrequencyHz is <=0 or >300_000_000_000 || entry.Note==null || entry.Note.Length>4000 || entry.Band==null || entry.Band.Length>30 || entry.Utc==default || RadioCatalog.Country(entry.CountryId)==null || entry.SentRst==null || entry.ReceivedRst==null) throw new InvalidDataException("Invalid QSO record.");
        _=CallSigns.Parse(entry.OwnCall); _=CallSigns.Parse(entry.OtherCall);
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
