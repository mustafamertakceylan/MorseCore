using System.Text.Json;

namespace MorseCore.AppCore;

public sealed class Localizer
{
    public static readonly string[] Languages = ["tr", "en", "de", "ru", "ar"];
    public static readonly string[] LanguageNames = ["Türkçe", "English", "Deutsch", "Русский", "العربية"];
    private readonly Dictionary<string,string> strings;
    public bool IsRtl { get; }
    public string Language { get; }
    public Localizer(string language)
    {
        if(!Languages.Contains(language)) language="en";
        Language=language;
        IsRtl=language=="ar";
        var assembly=typeof(Localizer).Assembly;
        strings=new(StringComparer.Ordinal);
        foreach(string resource in assembly.GetManifestResourceNames()
            .Where(name=>name.StartsWith("MorseCore.AppCore.Strings.",StringComparison.Ordinal) && name.EndsWith($".{language}.json",StringComparison.Ordinal))
            .Order(StringComparer.Ordinal))
        {
            using var stream=assembly.GetManifestResourceStream(resource)!;
            var section=JsonSerializer.Deserialize(stream,AppJsonContext.Default.DictionaryStringString)
                ?? throw new InvalidDataException("Invalid language resource: "+resource);
            foreach(var entry in section)
                if(!strings.TryAdd(entry.Key,entry.Value)) throw new InvalidDataException("Duplicate translation key: "+entry.Key);
        }
    }
    public string this[string key] => strings.TryGetValue(key,out string? value) ? value : throw new KeyNotFoundException(key);
    public IReadOnlyCollection<string> Keys => strings.Keys;
}
