using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MorseCore.AppCore.SpeechPackages;

namespace MorseCore.MacService;

/// <summary>A pinned, private runtime installed only as part of a user-requested voice installation.</summary>
internal static class PortablePython
{
    private const string DownloadUrl="https://github.com/astral-sh/python-build-standalone/releases/download/20261013/cpython-3.13.16%2B20261013-aarch64-apple-darwin-install_only_stripped.tar.gz";
    private const long DownloadBytes=25_268_458;
    private const string Sha256="16a5332fb0dcb77f163fc8f8cbccd95be1644cc53e68e5d2a79f50afd6680a0e";
    private const string Notice="""
        MorseCore optional speech runtime: CPython 3.13.16, python-build-standalone 20261013.
        Built and distributed by the python-build-standalone contributors / Astral.
        Source: https://github.com/astral-sh/python-build-standalone
        Build source and release: https://github.com/astral-sh/python-build-standalone/releases/tag/20261013
        CPython: https://www.python.org/; https://github.com/python/cpython/tree/v3.13.16
        The original runtime LICENSE and bundled dependency license files are preserved under python/.
        This runtime is private to MorseCore's speech folder; no administrator access or global PATH changes.
        """;

    private static string Root(string home)=>Path.Combine(home,".voice-tools","runtime");
    private static string Executable(string home)=>Path.Combine(Root(home),"python","bin","python3.13");

    public static void AddToProcessPath(string home)
    {
        if(!OperatingSystem.IsMacOS()) return;
        string executable=Executable(home);
        CheckLinks(Root(home));
        if(File.Exists(executable))
            Environment.SetEnvironmentVariable("PATH",Path.GetDirectoryName(executable)+Path.PathSeparator+(Environment.GetEnvironmentVariable("PATH")??""));
    }

    public static async Task EnsureAsync(string home,HttpClient http,CancellationToken token)
    {
        if(!OperatingSystem.IsMacOS()||RuntimeInformation.ProcessArchitecture!=Architecture.Arm64)
            throw new PlatformNotSupportedException("The private Python runtime currently supports Apple Silicon Macs.");
        string tools=Path.Combine(home,".voice-tools");
        CheckLinks(tools); Directory.CreateDirectory(tools); CheckLinks(tools);
        string lockPath=Path.Combine(tools,".runtime-install.lock"); CheckLinks(lockPath);
        using var lease=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        CheckLinks(Root(home));
        if(await VoicePackageManager.IsCompatiblePythonAsync(Executable(home),token)) { AddToProcessPath(home); return; }
        string temporary=Path.Combine(tools,".runtime-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string archive=Path.Combine(temporary,"runtime.tar.gz"),tar=Path.Combine(temporary,"runtime.tar"),stage=Path.Combine(temporary,"stage");
            using(var response=await http.GetAsync(DownloadUrl,HttpCompletionOption.ResponseHeadersRead,token))
            {
                response.EnsureSuccessStatusCode();
                if(response.RequestMessage?.RequestUri is not { Scheme:"https" }) throw new IOException("Insecure Python runtime redirect.");
                if(response.Content.Headers.ContentLength is { } length&&length!=DownloadBytes) throw new IOException("Unexpected Python runtime size.");
                await using var source=await response.Content.ReadAsStreamAsync(token);
                await using var destination=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);
                using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long total=0; byte[] block=new byte[65536]; int count;
                while((count=await source.ReadAsync(block,token))>0)
                {
                    total+=count; if(total>DownloadBytes) throw new IOException("Python runtime exceeds its pinned size.");
                    digest.AppendData(block,0,count); await destination.WriteAsync(block.AsMemory(0,count),token);
                }
                if(total!=DownloadBytes||!Convert.ToHexString(digest.GetHashAndReset()).Equals(Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Python runtime integrity verification failed.");
            }
            // Bound decompression independently of compressed size before parsing any archive paths.
            await using(var source=File.OpenRead(archive))
            await using(var gzip=new GZipStream(source,CompressionMode.Decompress))
            await using(var destination=new FileStream(tar,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            {
                byte[] block=new byte[65536]; long total=0; int count;
                while((count=await gzip.ReadAsync(block,token))>0)
                { total+=count; if(total>512L*1024*1024) throw new IOException("Expanded Python runtime is too large."); await destination.WriteAsync(block.AsMemory(0,count),token); }
            }
            Directory.CreateDirectory(stage);
            using(var source=File.OpenRead(tar))
            using(var reader=new TarReader(source))
            {
                int count=0; TarEntry? entry;
                while((entry=reader.GetNextEntry())!=null)
                {
                    token.ThrowIfCancellationRequested();
                    if(++count>50000) throw new IOException("Too many Python runtime archive entries.");
                    _=ArchivePath(stage,entry.Name);
                    if(entry.EntryType is TarEntryType.SymbolicLink or TarEntryType.HardLink)
                    {
                        string parent=entry.EntryType==TarEntryType.SymbolicLink?Path.GetDirectoryName(entry.Name)!:"";
                        _=ArchivePath(stage,Path.Combine(parent,entry.LinkName));
                    }
                    else if(entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                        throw new IOException("Unsupported Python runtime archive entry.");
                }
            }
            token.ThrowIfCancellationRequested();
            TarFile.ExtractToDirectory(tar,stage,false);
            token.ThrowIfCancellationRequested();
            string python=Path.Combine(stage,"python","bin","python3.13");
            if(!await VoicePackageManager.IsCompatiblePythonAsync(python,token)) throw new IOException("Downloaded Python runtime could not be verified.");
            if(!File.Exists(Path.Combine(stage,"python","LICENSE"))&&!File.Exists(Path.Combine(stage,"python","lib","python3.13","LICENSE.txt"))) throw new IOException("Python runtime license is missing.");
            await File.WriteAllTextAsync(Path.Combine(stage,"MORSECORE-RUNTIME-NOTICE.txt"),Notice,new UTF8Encoding(false),token);
            token.ThrowIfCancellationRequested(); CheckLinks(tools); CheckLinks(Root(home));
            // Preserve a previous incomplete runtime before publishing the verified replacement.
            if(Directory.Exists(Root(home))) Directory.Move(Root(home),Root(home)+".incomplete-"+Guid.NewGuid().ToString("N"));
            Directory.Move(stage,Root(home));
            AddToProcessPath(home);
        }
        finally { Directory.Delete(temporary,true); }
    }

    private static string ArchivePath(string root,string relative)
    {
        if(string.IsNullOrEmpty(relative)||Path.IsPathFullyQualified(relative)||relative.Contains('\\')) throw new IOException("Unsafe Python archive path.");
        string full=Path.GetFullPath(Path.Combine(root,relative));
        if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)) throw new IOException("Python archive path escapes its folder.");
        string python=Path.Combine(root,"python");
        if(full!=python&&!full.StartsWith(python+Path.DirectorySeparatorChar,StringComparison.Ordinal)) throw new IOException("Unexpected Python archive root.");
        return full;
    }
    private static void CheckLinks(string path)
    {
        for(string? current=path;current!=null;current=Path.GetDirectoryName(current))
        {
            try { if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("Python runtime folders cannot contain symbolic links."); }
            catch(FileNotFoundException) { } catch(DirectoryNotFoundException) { }
        }
    }
}
