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
        using var stream=typeof(Localizer).Assembly.GetManifestResourceStream($"MorseCore.AppCore.Strings.{language}.json")!;
        strings=JsonSerializer.Deserialize(stream,AppJsonContext.Default.DictionaryStringString)!;
    }
    public string this[string key] => strings.TryGetValue(key,out string? value) ? value : throw new KeyNotFoundException(key);
    public IReadOnlyCollection<string> Keys => strings.Keys;
}
