using System.Diagnostics;
using System.Text;

namespace MorseCore.AppCore;

public sealed record OfflineVoice(string Id,string Language,string Name,string Engine,string Model);

/// <summary>Runs an optional, separately installed speech pack. Synthesis never uses a network client.</summary>
public static class OfflineSpeech
{
    public const int MaxCharacters=4000;
    public static IReadOnlyList<OfflineVoice> Voices { get; }=Array.AsReadOnly<OfflineVoice>([
        new("ema-tr","tr-TR","EMA Lightning","ema","ema"),
        new("antalia-tr","tr-TR","Antalia-2 Mini","antalia","antalia"),
        new("kareem-ar","ar-SA","Kareem · Piper","piper","ar_JO-kareem-medium"),
        new("thorsten-de","de-DE","Thorsten · Piper","piper","de_DE-thorsten-high"),
        new("kerstin-de","de-DE","Kerstin · Piper","piper","de_DE-kerstin-low"),
        new("dmitri-ru","ru-RU","Dmitri · Piper","piper","ru_RU-dmitri-medium"),
        new("denis-ru","ru-RU","Denis · Piper","piper","ru_RU-denis-medium"),
        new("joe-en","en-US","Joe · Piper","piper","en_US-joe-medium")
    ]);
    public static IReadOnlyList<OfflineVoice> ForLanguage(string language)=>Voices.Where(v=>v.Language==language).ToArray();
    public static string PythonPath(string home)=>Path.Combine(home,"venv",OperatingSystem.IsWindows()?"Scripts":"bin",OperatingSystem.IsWindows()?"python.exe":"python");
    public static string ResolveHome(string? configured=null)
    {
        if(!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured)) return Path.GetFullPath(configured);
        string? environment=Environment.GetEnvironmentVariable("MORSECORE_SPEECH_HOME");
        if(!string.IsNullOrWhiteSpace(environment) && Path.IsPathFullyQualified(environment)) return Path.GetFullPath(environment);
        for(var directory=new DirectoryInfo(AppContext.BaseDirectory);directory!=null;directory=directory.Parent)
        {
            string candidate=Path.Combine(directory.FullName,"speech-runtime");
            if(File.Exists(Path.Combine(candidate,"pack-ready.json"))) return candidate;
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MorseCore","Speech");
    }
    public static bool IsInstalled(string home,OfflineVoice voice)
    {
        if(!File.Exists(PythonPath(home))) return false;
        string model=Path.Combine(home,"models",voice.Model);
        return voice.Engine switch
        {
            "ema"=>File.Exists(Path.Combine(home,"speech_worker.py")) && File.Exists(Path.Combine(model,"ema.pt")) && File.Exists(Path.Combine(model,"decoder.pt")) && File.Exists(Path.Combine(model,"config.json")),
            "antalia"=>File.Exists(Path.Combine(home,"speech_worker.py")) && File.Exists(Path.Combine(model,"acoustic.safetensors")) && File.Exists(Path.Combine(model,"vocoder.safetensors")) && File.Exists(Path.Combine(model,"config.json")),
            "piper"=>File.Exists(model+".onnx") && File.Exists(model+".onnx.json"),
            _=>false
        };
    }
    public static async Task SynthesizeAsync(string text,string voiceId,string home,string output,CancellationToken token=default)
    {
        if(string.IsNullOrWhiteSpace(text) || text.Length>MaxCharacters) throw new ArgumentException("Speech requires 1–4000 characters.",nameof(text));
        var voice=Voices.FirstOrDefault(v=>v.Id==voiceId) ?? throw new ArgumentException("Unknown offline voice.",nameof(voiceId));
        if(!IsInstalled(home,voice)) throw new FileNotFoundException("Offline speech pack is missing.");
        if(File.Exists(output)) throw new IOException("Speech output already exists.");
        token.ThrowIfCancellationRequested();
        var start=new ProcessStartInfo(PythonPath(home)) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=home };
        start.ArgumentList.Add("-X"); start.ArgumentList.Add("utf8"); start.ArgumentList.Add("-I");
        if(voice.Engine=="piper")
        {
            foreach(string argument in new[]{"-m","piper","--model",Path.Combine(home,"models",voice.Model+".onnx"),"--output_file",output}) start.ArgumentList.Add(argument);
        }
        else
        {
            foreach(string argument in new[]{Path.Combine(home,"speech_worker.py"),voice.Engine,Path.Combine(home,"models",voice.Model),output}) start.ArgumentList.Add(argument);
        }
        start.Environment["HF_HUB_OFFLINE"]="1";
        start.Environment["TRANSFORMERS_OFFLINE"]="1";
        start.Environment["HF_HUB_DISABLE_TELEMETRY"]="1";
        start.Environment["OMP_NUM_THREADS"]="2";
        start.Environment["MKL_NUM_THREADS"]="2";
        using var process=new Process { StartInfo=start };
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(2));
        bool success=false,started=false;
        try
        {
            if(!process.Start()) throw new IOException("Speech process did not start.");
            started=true;
            // Drain both pipes while running; a third-party diagnostic must not deadlock synthesis.
            Task drainOut=Drain(process.StandardOutput),drainError=Drain(process.StandardError);
            using var registration=timeout.Token.Register(()=> { try { if(!process.HasExited) process.Kill(true); } catch(InvalidOperationException) { } });
            await process.StandardInput.WriteAsync(text.AsMemory(),timeout.Token); process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token); await Task.WhenAll(drainOut,drainError);
            timeout.Token.ThrowIfCancellationRequested();
            if(process.ExitCode!=0 || !File.Exists(output)) throw new IOException("Offline speech synthesis failed.");
            var size=new FileInfo(output).Length;
            if(size<44 || size>64*1024*1024) throw new IOException("Invalid speech output size.");
            using(var wave=File.OpenRead(output))
            {
                byte[] header=new byte[12]; wave.ReadExactly(header);
                if(!header.AsSpan(0,4).SequenceEqual("RIFF"u8) || !header.AsSpan(8,4).SequenceEqual("WAVE"u8)) throw new IOException("Invalid speech output format.");
            }
            success=true;
        }
        catch(Exception e) when(timeout.IsCancellationRequested && e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { throw new OperationCanceledException("Speech synthesis cancelled.",e,timeout.Token); }
        finally
        {
            if(started) { try { if(!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } } catch(InvalidOperationException) { } }
            if(!success && File.Exists(output)) File.Delete(output);
        }
    }
    private static async Task Drain(StreamReader reader)
    {
        char[] buffer=new char[2048];
        while(await reader.ReadAsync(buffer)>0) { }
    }
}
