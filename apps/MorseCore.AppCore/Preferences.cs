using System.Text.Json;
using MorseCore.Interop;

namespace MorseCore.AppCore;

public sealed record Preferences
{
    public string Language { get; init; } = "tr";
    public string Theme { get; init; } = "system";
    public MorseAlphabet Alphabet { get; init; }
    public char Separator { get; init; } = '/';
    public bool ReduceMotion { get; init; }
    public bool Collapsed { get; init; }
    public bool CheckUpdates { get; init; } = true;
    public DateTimeOffset LastUpdateCheck { get; init; }
    public uint Wpm { get; init; } = 20;
    public uint Frequency { get; init; } = 600;
    public string SpeechLanguage { get; init; } = "tr-TR";
    public string VoiceId { get; init; } = "";
    public string SpeechHome { get; init; } = "";
    public string OutputDirectory { get; init; } = DesktopPaths.OutputDirectory;

    public Preferences Validated() => this with
    {
        Language = Localizer.Languages.Contains(Language) ? Language : "en",
        Theme = Theme is "light" or "dark" or "system" ? Theme : "system",
        Alphabet = Enum.IsDefined(Alphabet) ? Alphabet : MorseAlphabet.Latin,
        Separator = "/|#*".Contains(Separator) ? Separator : '/',
        LastUpdateCheck = LastUpdateCheck>DateTimeOffset.UtcNow ? DateTimeOffset.MinValue : LastUpdateCheck,
        Wpm = Math.Clamp(Wpm, 5, 60), Frequency = Math.Clamp(Frequency, 200, 2000),
        SpeechLanguage = SpeechLanguage is "tr-TR" or "en-US" or "de-DE" or "ru-RU" or "ar-SA" ? SpeechLanguage : "en-US",
        SpeechHome = !string.IsNullOrEmpty(SpeechHome) && Path.IsPathFullyQualified(SpeechHome) ? SpeechHome : "",
        OutputDirectory = !string.IsNullOrWhiteSpace(OutputDirectory) && Path.IsPathFullyQualified(OutputDirectory)
            ? OutputDirectory : DesktopPaths.OutputDirectory
    };
}

public sealed class PreferencesStore(string path)
{
    public static string DefaultPath => DesktopPaths.PreferencesFile;
    public string? LoadWarning { get; private set; }
    public Preferences Load()
    {
        try { return File.Exists(path) ? (JsonSerializer.Deserialize(File.ReadAllText(path),AppJsonContext.Default.Preferences) ?? new()).Validated() : new(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { LoadWarning = e.Message; return new(); }
    }
    public void Save(Preferences preferences)
    {
        string directory=Path.GetDirectoryName(Path.GetFullPath(path))!;
        DesktopPaths.CreateUserDirectory(directory);
        string temporary=Path.Combine(directory,$".settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary,JsonSerializer.Serialize(preferences.Validated(),AppJsonContext.Default.Preferences));
            File.Move(temporary,path,true);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
