using System.Text.Json;
using System.Text.Json.Serialization;

namespace MorseCore.AppCore.Radio;

public sealed record RadioSource(string Id,string Title,string Url,string Section);
public sealed record ClassPower(string ClassId,decimal Watts,string Basis);
public sealed record RadioBand(string Band,long StartHz,long EndHz,ClassPower[] Powers,string SourceId,string ConditionsKey="bandConditions");
public sealed record RadioCountry(string Id,string NameKey,string[] Classes,string VisitorPrefix,string[] SourceIds,RadioBand[] Bands,string NotesKey);
public sealed record RadioData(int Schema,DateOnly ReviewedOn,RadioSource[] Sources,RadioCountry[] Countries);
public sealed record CallSignInfo(string Normalized,string BaseCall,string? CountryId,string? ClassId,string? Region,bool IsRecognized);
public enum StationMode { Fixed, Portable, Mobile }
public sealed record StationProfile
{
    public string Id { get; init; }=Guid.NewGuid().ToString("N");
    public string Label { get; init; }="";
    public string CallSign { get; init; }="";
    public string CountryId { get; init; }="TR";
    public string ClassId { get; init; }="";
    public string OperatorName { get; init; }="";
    public string Qth { get; init; }="";
}
public sealed record StationDocument
{
    public int Schema { get; init; }=1;
    public string ActiveId { get; init; }="";
    public List<StationProfile> Profiles { get; init; }=[];
}
public sealed record OperatingContext(string CountryId,string ClassId,StationMode Mode,string Region="",string ManualCall="");
public sealed record OperatingCall(string CallSign,bool NeedsConfirmation,string ExplanationKey);
public enum BandStatus { Match, OutsideListedRanges, ClassUnknown, CountryUnknown }
public sealed record BandAssessment(BandStatus Status,RadioBand? Band=null,ClassPower? Power=null);
public enum QsoStage { CheckFrequency, GeneralCall, Reply, Report, Message, Close }
public sealed record QsoRecord
{
    public int Schema { get; init; }=1;
    public string Id { get; init; }=Guid.NewGuid().ToString("N");
    public DateTimeOffset Utc { get; init; }=DateTimeOffset.UtcNow;
    public string OwnCall { get; init; }="";
    public string OtherCall { get; init; }="";
    public string CountryId { get; init; }="";
    public string Band { get; init; }="";
    public long FrequencyHz { get; init; }
    public string SentRst { get; init; }="";
    public string ReceivedRst { get; init; }="";
    public bool Practice { get; init; }=true;
    public string Note { get; init; }="";
    private readonly string? gridSquare;
    public string GridSquare { get=>gridSquare??""; init=>gridSquare=value; }
    // Null is the legacy schema-1 default (seconds were always recorded). False preserves an imported HHMM time.
    public bool? TimeHasSeconds { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented=true,PropertyNamingPolicy=JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RadioData))]
[JsonSerializable(typeof(StationDocument))]
[JsonSerializable(typeof(QsoRecord))]
[JsonSerializable(typeof(WorldOutlineData))]
internal partial class RadioJsonContext : JsonSerializerContext;

public static class RadioCatalog
{
    public static RadioData Data { get; }=Load();
    public static RadioCountry? Country(string id)=>Data.Countries.FirstOrDefault(c=>c.Id==id);
    private static RadioData Load()
    {
        using var stream=typeof(RadioCatalog).Assembly.GetManifestResourceStream("MorseCore.AppCore.Radio.rules.json")!;
        var data=JsonSerializer.Deserialize(stream,RadioJsonContext.Default.RadioData) ?? throw new InvalidDataException("Missing radio catalog.");
        if(data.Schema!=1 || data.Countries.Select(c=>c.Id).Distinct().Count()!=data.Countries.Length || data.Sources.Any(s=>!Uri.TryCreate(s.Url,UriKind.Absolute,out var uri) || uri.Scheme!="https")) throw new InvalidDataException("Invalid radio catalog.");
        foreach(var country in data.Countries)
            foreach(var band in country.Bands)
                if(band.StartHz<=0 || band.EndHz<=band.StartHz || band.Powers.Any(p=>!country.Classes.Contains(p.ClassId) || p.Watts<=0) || !country.SourceIds.Contains(band.SourceId)) throw new InvalidDataException("Invalid band rule.");
        return data;
    }
    public static BandAssessment Assess(string countryId,string classId,long frequencyHz)
    {
        var country=Country(countryId);
        if(country==null) return new(BandStatus.CountryUnknown);
        if(!country.Classes.Contains(classId)) return new(BandStatus.ClassUnknown);
        foreach(var band in country.Bands.Where(b=>frequencyHz>=b.StartHz && frequencyHz<b.EndHz))
            if(band.Powers.FirstOrDefault(p=>p.ClassId==classId) is { } power) return new(BandStatus.Match,band,power);
        // Coverage is explicitly partial: absence is not presented as a legal prohibition.
        return new(BandStatus.OutsideListedRanges);
    }
}
