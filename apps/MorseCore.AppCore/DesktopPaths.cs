namespace MorseCore.AppCore;

public sealed record DesktopDirectories(string ConfigDirectory,string DataDirectory,string CacheDirectory);

/// <summary>One storage policy for desktop adapters; resolving a path never creates it.</summary>
public static class DesktopPaths
{
    public static DesktopDirectories Current => OperatingSystem.IsLinux()
        ? ResolveLinuxDirectories(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"),Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
            Environment.GetEnvironmentVariable("XDG_CACHE_HOME"))
        : new(Path.Combine(LocalData,"MorseCore","Desktop"),Path.Combine(LocalData,"MorseCore","Desktop"),
            Path.Combine(LocalData,"MorseCore","Desktop","Cache"));

    private static string LocalData=>Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string ConfigDirectory=>Current.ConfigDirectory;
    public static string DataDirectory=>Current.DataDirectory;
    public static string CacheDirectory=>Current.CacheDirectory;
    public static string PreferencesFile=>Path.Combine(ConfigDirectory,"settings.json");
    public static string SpeechDirectory=>OperatingSystem.IsLinux()
        ? Path.Combine(DataDirectory,"Speech") : Path.Combine(LocalData,"MorseCore","Speech");

    /// <summary>New Linux user storage is private; existing permissions are preserved.</summary>
    public static void CreateUserDirectory(string path)
    {
        if(!Path.IsPathFullyQualified(path)) throw new ArgumentException("An absolute storage directory is required.",nameof(path));
        if(OperatingSystem.IsLinux()) Directory.CreateDirectory(path,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        else Directory.CreateDirectory(path);
    }
    public static string OutputDirectory
    {
        get
        {
            string documents=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if(!Path.IsPathFullyQualified(documents)) documents=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if(!Path.IsPathFullyQualified(documents)) throw new IOException("An absolute user directory is required.");
            return Path.Combine(documents,"MorseCore");
        }
    }

    /// <summary>XDG requires absolute overrides; relative values are ignored, never resolved against cwd.</summary>
    public static DesktopDirectories ResolveLinuxDirectories(string userHome,string? configHome=null,string? dataHome=null,string? cacheHome=null)
    {
        if(!Path.IsPathFullyQualified(userHome)) throw new ArgumentException("An absolute user home is required.",nameof(userHome));
        string Root(string? value,params string[] fallback)=>!string.IsNullOrWhiteSpace(value) && Path.IsPathFullyQualified(value)
            ? Path.GetFullPath(value) : Path.Combine(new[]{Path.GetFullPath(userHome)}.Concat(fallback).ToArray());
        return new(Path.Combine(Root(configHome,".config"),"morsecore"),
            Path.Combine(Root(dataHome,".local","share"),"morsecore"),Path.Combine(Root(cacheHome,".cache"),"morsecore"));
    }
}
