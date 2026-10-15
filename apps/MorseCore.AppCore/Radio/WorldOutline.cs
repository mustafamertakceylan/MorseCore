using System.Text.Json;

namespace MorseCore.AppCore.Radio;

public sealed record WorldOutlineData(string Source,string License,string Url,string LicenseUrl,double[][][] Rings);
public static class WorldOutline
{
    private static readonly Lazy<WorldOutlineData> Cached=new(Load);
    public static WorldOutlineData Data=>Cached.Value;
    private static WorldOutlineData Load()
    {
        using var stream=typeof(WorldOutline).Assembly.GetManifestResourceStream("MorseCore.AppCore.Radio.world-outline.json")!;
        var data=JsonSerializer.Deserialize(stream,RadioJsonContext.Default.WorldOutlineData)??throw new InvalidDataException("Missing world outline.");
        if(data.Rings.Length>256 || data.Rings.Sum(r=>r.Length)>10000 || data.Rings.Any(r=>r.Length<4 || r.Any(p=>p.Length!=2 || !double.IsFinite(p[0]) || !double.IsFinite(p[1]) || p[0] is <-180 or >180 || p[1] is <-90 or >90))) throw new InvalidDataException("Invalid world outline.");
        return data;
    }
}
