using System.Globalization;

namespace MorseCore.AppCore.Radio;

public sealed record LogQuery(bool? Practice=null,string Call="",string Band="",DateOnly? From=null,DateOnly? Through=null,string? Before=null,int Limit=25)
{
    internal bool Matches(QsoRecord entry)=>
        (!Practice.HasValue || entry.Practice==Practice) &&
        (Call.Length==0 || entry.OwnCall.Contains(Call,StringComparison.OrdinalIgnoreCase) || entry.OtherCall.Contains(Call,StringComparison.OrdinalIgnoreCase)) &&
        (Band.Length==0 || string.Equals(Band,entry.Band,StringComparison.OrdinalIgnoreCase) || string.Equals(Band,AdifBands.FromFrequency(entry.FrequencyHz),StringComparison.OrdinalIgnoreCase)) &&
        (!From.HasValue || DateOnly.FromDateTime(entry.Utc.UtcDateTime)>=From) &&
        (!Through.HasValue || DateOnly.FromDateTime(entry.Utc.UtcDateTime)<=Through);
    internal void Validate()
    {
        if(Call.Length>32 || Band.Length>30 || Limit is <1 or >100 || From>Through) throw new ArgumentException("Invalid log filter.");
        if(Before!=null) _=ReadCursor(Before);
    }
    internal static string Cursor(QsoRecord entry)=>entry.Utc.UtcTicks.ToString(CultureInfo.InvariantCulture)+":"+entry.Id;
    internal static (long Ticks,string Id) ReadCursor(string value)
    {
        var parts=value.Split(':');
        if(parts.Length!=2 || !long.TryParse(parts[0],NumberStyles.None,CultureInfo.InvariantCulture,out long ticks) || ticks<0 || ticks>DateTimeOffset.MaxValue.UtcTicks || !Guid.TryParseExact(parts[1],"N",out _)) throw new ArgumentException("Invalid log cursor.");
        return(ticks,parts[1]);
    }
    internal static int Compare(QsoRecord left,QsoRecord right)
    { int order=right.Utc.CompareTo(left.Utc); return order!=0?order:string.CompareOrdinal(right.Id,left.Id); }
}
public sealed record LogPage(IReadOnlyList<QsoRecord> Entries,int Total,string? NextCursor);
public sealed record LogMap(IReadOnlyList<QsoRecord> Entries,int Located,int Total);
public sealed record ImportPreview(IReadOnlyList<QsoRecord> Candidates,int Duplicates);
public sealed record ImportResult(int Added,int Duplicates);

/// <summary>ADIF 3.1.8 band names/ranges; these are interchange labels, never operating permissions.</summary>
public static class AdifBands
{
    // https://adif.org.uk/318/ADIF_318.htm, III.B.4; reviewed 2026-10-15.
    private static readonly (string Name,long Low,long High)[] Values=
    [
        ("2190m",135700,137800),("630m",472000,479000),("560m",501000,504000),
        ("160m",1800000,2000000),("80m",3500000,4000000),("60m",5060000,5450000),
        ("40m",7000000,7300000),("30m",10100000,10150000),("20m",14000000,14350000),
        ("17m",18068000,18168000),("15m",21000000,21450000),("12m",24890000,24990000),
        ("10m",28000000,29700000),("8m",40000000,45000000),("6m",50000000,54000000),
        ("5m",54000001,69900000),("4m",70000000,71000000),("2m",144000000,148000000),
        ("1.25m",222000000,225000000),("70cm",420000000,450000000),("33cm",902000000,928000000),
        ("23cm",1240000000,1300000000),("13cm",2300000000,2450000000),("9cm",3300000000,3500000000),
        ("6cm",5650000000,5925000000),("3cm",10000000000,10500000000),("1.25cm",24000000000,24250000000),
        ("6mm",47000000000,47200000000),("4mm",75500000000,81000000000),("2.5mm",119980000000,123000000000),
        ("2mm",134000000000,149000000000),("1mm",241000000000,250000000000),("submm",300000000000,7500000000000)
    ];
    public static string? Normalize(string band)=>Values.FirstOrDefault(b=>b.Name.Equals(band,StringComparison.OrdinalIgnoreCase)).Name;
    public static string? FromFrequency(long hz)=>Values.FirstOrDefault(b=>hz>=b.Low && hz<=b.High).Name;
    public static bool Contains(string band,long hz)=>Values.Any(b=>b.Name.Equals(band,StringComparison.OrdinalIgnoreCase) && hz>=b.Low && hz<=b.High);
}
