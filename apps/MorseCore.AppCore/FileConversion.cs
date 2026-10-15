using System.Text;
using MorseCore.Interop;

namespace MorseCore.AppCore;

public sealed record ConvertedFile(string Source,string Destination);
public static class FileConversion
{
    public const int MaxInputBytes=8*1024*1024;
    private static readonly UTF8Encoding Utf8=new(false,true);
    public static async Task<ConvertedFile> ConvertAsync(string source,string destination,bool decode,MorseAlphabet alphabet,char separator,CancellationToken token=default)
    {
        await using var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read,4096,true);
        if(input.Length>MaxInputBytes) throw new IOException("File exceeds 8 MiB.");
        using var memory=new MemoryStream();
        byte[] block=new byte[8192]; int read;
        while((read=await input.ReadAsync(block,token).ConfigureAwait(false))>0)
        {
            if(memory.Length+read>MaxInputBytes) throw new IOException("File exceeds 8 MiB.");
            memory.Write(block,0,read);
        }
        string text=Utf8.GetString(memory.GetBuffer(),0,checked((int)memory.Length));
        token.ThrowIfCancellationRequested();
        var result=await Task.Run(()=>decode ? MorseEngine.Decode(text,alphabet,separator) : MorseEngine.Encode(text,alphabet,separator),token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        string folder=Path.GetDirectoryName(Path.GetFullPath(destination))!;
        Directory.CreateDirectory(folder);
        string temporary=Path.Combine(folder,$".morsecore-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary,result.Text,Utf8,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            // No replacement: existing user files are preserved even in a publication race.
            File.Move(temporary,destination,false);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
        return new(source,destination);
    }
}
