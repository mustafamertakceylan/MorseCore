using System.Globalization;

namespace MorseCore.AppCore.Radio;

public sealed record AdifImportOptions(string OwnCall="",string CountryId="OTHER",bool Practice=true);
public sealed record AdifIssue(int Record,string Key,string Field="");
public sealed record AdifDocument(IReadOnlyList<QsoRecord> Records,IReadOnlyList<AdifIssue> Issues,int Total,int IgnoredFields);

/// <summary>Bounded ADI reader. Length framing is consumed before looking for tags, including literal EOR text in values.</summary>
public static class AdifImport
{
    public const int MaximumBytes=8*1024*1024,MaximumRecords=10000;
    // ADIF 3.1.8 (2026-10-12), IV.A, III.B.4 and III.C.1: https://adif.org.uk/318/ADIF_318.htm
    private static readonly HashSet<string> Supported=new(StringComparer.OrdinalIgnoreCase)
    { "QSO_DATE","TIME_ON","STATION_CALLSIGN","OPERATOR","CALL","FREQ","BAND","MODE","SUBMODE","RST_SENT","RST_RCVD","COMMENT","NOTES","GRIDSQUARE","GRIDSQUARE_EXT","LAT","LON","APP_MORSECORE_PRACTICE","APP_MORSECORE_COUNTRY_ID" };
    public static AdifDocument Parse(string source,AdifImportOptions? options=null,CancellationToken token=default)
    {
        options??=new();
        if(source.Length>MaximumBytes) throw new InvalidDataException("ADIF file exceeds 8 MiB.");
        source=source.TrimStart('\uFEFF');
        if(source.Any(c=>c>127)) throw new InvalidDataException("ADI must use ASCII; international ADX fields are not supported.");
        var records=new List<QsoRecord>(); var issues=new List<AdifIssue>(); var fields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        bool header=source.AsSpan().TrimStart().Length>0 && source.AsSpan().TrimStart()[0]!='<',hadHeader=false,recordError=false;
        int position=0,total=0,ignored=0;
        while(position<source.Length)
        {
            token.ThrowIfCancellationRequested(); int start=source.IndexOf('<',position); if(start<0) break;
            int end=source.IndexOf('>',start+1); if(end<0 || end-start>120) throw new InvalidDataException("Incomplete or oversized ADIF field tag.");
            var tag=source[(start+1)..end].Split(':'); string name=tag[0].ToUpperInvariant(); position=end+1;
            if(name.Length==0 || name.Length>64 || name.Any(c=>!(char.IsAsciiLetterOrDigit(c) || c=='_'))) throw new InvalidDataException("Invalid ADIF field name.");
            if(name is "EOH" or "EOR")
            {
                if(tag.Length!=1) throw new InvalidDataException("ADIF record markers must not have a length.");
                if(name=="EOH")
                {
                    if(hadHeader || total>0 || fields.Keys.Any(k=>k is "CALL" or "QSO_DATE" or "TIME_ON")) throw new InvalidDataException("Unexpected ADIF header marker.");
                    fields.Clear(); header=false; hadHeader=true; recordError=false; continue;
                }
                if(header) throw new InvalidDataException("ADIF header has no EOH marker.");
                if(++total>MaximumRecords) throw new InvalidDataException("ADIF exceeds 10000 records.");
                if(!recordError)
                {
                    try { records.Add(Convert(fields,options)); }
                    catch(AdifRecordException e) { issues.Add(new(total,e.Key,e.Field)); }
                }
                fields.Clear(); recordError=false; continue;
            }
            if(tag.Length is <2 or >3 || tag[1].Length==0 || tag[1].Any(c=>!char.IsAsciiDigit(c)) || !int.TryParse(tag[1],NumberStyles.None,CultureInfo.InvariantCulture,out int length) || length>65536 || length>source.Length-position)
                throw new InvalidDataException("Invalid, oversized or truncated ADIF field length.");
            if(tag.Length==3 && (tag[2].Length!=1 || !"BNDTSMELIGC".Contains(char.ToUpperInvariant(tag[2][0])))) throw new InvalidDataException("Invalid ADIF type indicator.");
            if(tag.Length==3 && "IGC".Contains(char.ToUpperInvariant(tag[2][0]))) throw new InvalidDataException("International ADIF fields require ADX.");
            string value=source.Substring(position,length); position+=length;
            if(fields.Count>=200) throw new InvalidDataException("Too many ADIF fields in one record.");
            if(!fields.TryAdd(name,value))
            { if(header) throw new InvalidDataException("Duplicate ADIF header field."); issues.Add(new(total+1,"logbook.issueRepeated",name)); recordError=true; }
            if(!header && !Supported.Contains(name) && name is not("ADIF_VER" or "PROGRAMID" or "PROGRAMVERSION" or "CREATED_TIMESTAMP")) ignored++;
        }
        if(header || fields.Count>0) throw new InvalidDataException("ADIF ends before EOH/EOR; nothing was imported.");
        if(total==0) throw new InvalidDataException("ADIF contains no QSO records.");
        return new(records,issues,total,ignored);
    }
    private static QsoRecord Convert(Dictionary<string,string> fields,AdifImportOptions options)
    {
        string Value(string key)=>fields.GetValueOrDefault(key,"").Trim();
        string Required(string key) { string value=Value(key); if(value.Length==0) throw new AdifRecordException("logbook.issueMissing",key); return value; }
        string mode=Required("MODE");
        if(!mode.Equals("CW",StringComparison.OrdinalIgnoreCase) || Value("SUBMODE").Length>0) throw new AdifRecordException("logbook.issueMode","MODE/SUBMODE");
        string date=Required("QSO_DATE"),time=Required("TIME_ON");
        if(date.Length!=8 || date.Any(c=>!char.IsAsciiDigit(c)) || time.Length is not(4 or 6) || time.Any(c=>!char.IsAsciiDigit(c)) || !DateTime.TryParseExact(date+time,time.Length==4?"yyyyMMddHHmm":"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var instant) || instant.Year<1930) throw new AdifRecordException("logbook.issueInvalid","QSO_DATE/TIME_ON");
        string own=Value("STATION_CALLSIGN"); if(own.Length==0) own=Value("OPERATOR"); if(own.Length==0) own=options.OwnCall;
        string other=Required("CALL");
        try { own=CallSigns.Parse(own).Normalized; other=CallSigns.Parse(other).Normalized; }
        catch(ArgumentException) { throw new AdifRecordException("logbook.issueInvalid","STATION_CALLSIGN/CALL"); }
        string freq=Value("FREQ"),band=Value("BAND"); long hz=0;
        if(band.Length>0) band=AdifBands.Normalize(band) ?? throw new AdifRecordException("logbook.issueInvalid","BAND");
        if(freq.Length>0)
        {
            if(!decimal.TryParse(freq,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal mhz) || mhz is <=0 or >300000 || decimal.Round(mhz*1_000_000,0)!=mhz*1_000_000) throw new AdifRecordException("logbook.issueInvalid","FREQ");
            hz=(long)(mhz*1_000_000);
            if(band.Length>0 && !AdifBands.Contains(band,hz)) throw new AdifRecordException("logbook.issueInvalid","FREQ/BAND");
            if(band.Length==0) band=AdifBands.FromFrequency(hz)??"";
        }
        if(hz==0 && band.Length==0) throw new AdifRecordException("logbook.issueMissing","FREQ/BAND");
        bool practice=options.Practice; string provenance=Value("APP_MORSECORE_PRACTICE").ToUpperInvariant();
        if(provenance.Length>0) practice=provenance=="Y"?true:provenance=="N"?false:throw new AdifRecordException("logbook.issueInvalid","APP_MORSECORE_PRACTICE");
        string country=Value("APP_MORSECORE_COUNTRY_ID"); if(country.Length==0) country=options.CountryId;
        if(RadioCatalog.Country(country)==null) throw new AdifRecordException("logbook.issueInvalid","APP_MORSECORE_COUNTRY_ID");
        string sent=Value("RST_SENT"),received=Value("RST_RCVD");
        if(sent.Length>0 && !CwMessages.IsRst(sent) || received.Length>0 && !CwMessages.IsRst(received)) throw new AdifRecordException("logbook.issueInvalid","RST_SENT/RST_RCVD");
        string grid=Value("GRIDSQUARE").ToUpperInvariant(),extension=Value("GRIDSQUARE_EXT").ToUpperInvariant();
        if(extension.Length>0)
        { if(grid.Length!=8 || extension.Length is not(2 or 4)) throw new AdifRecordException("logbook.issueInvalid","GRIDSQUARE_EXT"); grid+=extension; }
        double? latitude=null,longitude=null;
        try
        {
            if(Value("LAT").Length>0 || Value("LON").Length>0) { latitude=Locator.ParseAdif(Value("LAT").ToUpperInvariant(),true); longitude=Locator.ParseAdif(Value("LON").ToUpperInvariant(),false); }
            Locator.Validate(grid,latitude,longitude);
        }
        catch(ArgumentException) { throw new AdifRecordException("logbook.issueInvalid","GRIDSQUARE/LAT/LON"); }
        string note=string.Join("\n",new[]{Value("COMMENT"),Value("NOTES")}.Where(v=>v.Length>0));
        if(note.Length>4000) throw new AdifRecordException("logbook.issueInvalid","COMMENT/NOTES");
        return new() { Utc=new DateTimeOffset(DateTime.SpecifyKind(instant,DateTimeKind.Utc)),TimeHasSeconds=time.Length==6,OwnCall=own,OtherCall=other,CountryId=country,FrequencyHz=hz,Band=band,Practice=practice,SentRst=sent,ReceivedRst=received,GridSquare=grid,Latitude=latitude,Longitude=longitude,Note=note };
    }
    private sealed class AdifRecordException(string key,string field):Exception
    { public string Key { get; }=key; public string Field { get; }=field; }
}
