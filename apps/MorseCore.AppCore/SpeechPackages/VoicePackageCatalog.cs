using System.Text.Json;
using System.Text.Json.Serialization;

namespace MorseCore.AppCore.SpeechPackages;

public sealed record VoiceModelFile
{
    [JsonPropertyName("url")] public required string Url { get; init; }
    [JsonPropertyName("path")] public required string Path { get; init; }
    [JsonPropertyName("sha256")] public string? Sha256 { get; init; }
    [JsonPropertyName("git_blob")] public required string GitBlob { get; init; }
    [JsonPropertyName("bytes")] public long Bytes { get; init; }
}

public sealed record VoicePackage(OfflineVoice Voice,IReadOnlyList<VoiceModelFile> Files)
{
    /// <summary>Exact pinned model/card download bytes; shared Python dependencies are additional.</summary>
    public long DownloadBytes=>Files.Sum(file=>file.Bytes);
    public Uri Source=>new(Voice.Engine switch {
        "ema"=>"https://huggingface.co/canberkkkkkk/ema-lightning",
        "antalia"=>"https://huggingface.co/cloud0day3/antalia-mini",
        _=>"https://huggingface.co/rhasspy/piper-voices"
    });
}

public sealed record VoicePackageProgress(string Phase,long CompletedBytes=0,long TotalBytes=0,string? File=null);

public static class VoicePackageCatalog
{
    public static IReadOnlyList<VoicePackage> Packages { get; }=Load();
    public static VoicePackage Get(string id)=>Packages.FirstOrDefault(p=>p.Voice.Id==id) ?? throw new ArgumentException("Unknown voice package.",nameof(id));
    private static IReadOnlyList<VoicePackage> Load()
    {
        using var stream=typeof(VoicePackageCatalog).Assembly.GetManifestResourceStream("MorseCore.SpeechAssets.speech-models.json") ?? throw new InvalidDataException("Voice catalog resource is missing.");
        var files=JsonSerializer.Deserialize(stream,VoicePackageJsonContext.Default.VoiceModelFileArray) ?? throw new InvalidDataException("Voice catalog is empty.");
        foreach(var file in files)
        {
            if(file.Bytes is <=0 or >256*1024*1024 || !Uri.TryCreate(file.Url,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="huggingface.co" || !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort || uri.Query.Length>0 || uri.Fragment.Length>0 || !uri.AbsolutePath.Contains("/resolve/",StringComparison.Ordinal))
                throw new InvalidDataException("Invalid pinned voice URL or length.");
            ValidateRelative(file.Path);
            string hash=file.Sha256 ?? file.GitBlob;
            if(hash.Length!=(file.Sha256 is null ? 40:64) || hash.Any(c=>!Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid voice digest.");
        }
        if(files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=files.Length) throw new InvalidDataException("Duplicate model destination.");
        return Array.AsReadOnly(OfflineSpeech.Voices.Select(voice=>new VoicePackage(voice,Array.AsReadOnly(files.Where(f=>voice.Engine=="piper"
            ? f.Path is var path && (path=="models/"+voice.Model+".onnx" || path=="models/"+voice.Model+".onnx.json" || path=="licenses/"+voice.Model+".MODEL_CARD")
            : f.Path.StartsWith("models/"+voice.Model+"/",StringComparison.Ordinal)).ToArray()))).ToArray());
    }
    internal static void ValidateRelative(string relative)
    {
        if(string.IsNullOrEmpty(relative) || relative.Contains('\\') || relative.Contains(':') || Path.IsPathFullyQualified(relative) || relative.Split('/').Any(part=>part is "" or "." or "..")) throw new InvalidDataException("Unsafe voice package path.");
    }
}

[JsonSourceGenerationOptions(WriteIndented=false)]
[JsonSerializable(typeof(VoiceModelFile[]))]
internal partial class VoicePackageJsonContext:JsonSerializerContext;
