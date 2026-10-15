using System.Text;
using MorseCore.Interop;

namespace MorseCore.AppCore.Radio;

public static class CwMessages
{
    public static IReadOnlyDictionary<string,string> Prosigns { get; }=new Dictionary<string,string>
    { ["<AR>"]=".-.-.",["<SK>"]="...-.-",["<BT>"]="-...-",["<AS>"]=".-...",["<KN>"]="-.--.",["<HH>"]="........" };
    public static string Encode(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if(message.Length>4000) throw new ArgumentException("CW message exceeds 4000 characters.");
        var words=message.ToUpperInvariant().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        var result=new StringBuilder();
        foreach(string word in words)
        {
            if(result.Length>0) result.Append(" / ");
            if(Prosigns.TryGetValue(word,out string? joined)) result.Append(joined);
            else
            {
                if(word.Contains('<') || word.Contains('>')) throw new ArgumentException("Unsupported or incomplete prosign.");
                result.Append(MorseEngine.Encode(word,MorseAlphabet.Latin,'/').Text.TrimEnd());
            }
        }
        return result.ToString();
    }
    public static string Compose(QsoStage stage,string ownCall,string otherCall,string sentRst,string name,string qth,string message)
    {
        string own=CallSigns.Parse(ownCall).Normalized;
        if(stage==QsoStage.CheckFrequency) return $"QRL? DE {own} K";
        if(stage==QsoStage.GeneralCall) return $"CQ CQ CQ DE {own} {own} {own} K";
        string other=CallSigns.Parse(otherCall).Normalized;
        string header=$"{other} DE {own}";
        string Clean(string input)
        {
            string clean=string.Join(' ',input.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
            if(clean.Length>2000) throw new ArgumentException("QSO field is too long.");
            return clean;
        }
        return stage switch
        {
            QsoStage.Reply=>$"{header} {own} K",
            QsoStage.Report when IsRst(sentRst)=>$"{header} UR RST {sentRst}"+(string.IsNullOrWhiteSpace(name)?"":$" NAME {Clean(name)}")+(string.IsNullOrWhiteSpace(qth)?"":$" QTH {Clean(qth)}")+$" DE {own} <AR> <KN>",
            QsoStage.Report=>throw new ArgumentException("Enter the actual three-digit CW RST report."),
            QsoStage.Message when !string.IsNullOrWhiteSpace(message)=>$"{header} {Clean(message)} DE {own} <AR> <KN>",
            QsoStage.Close=>$"{header} TNX QSO 73 DE {own} <SK>",
            _=>throw new ArgumentException("Enter a QSO message.")
        };
    }
    public static bool IsRst(string text)=>text.Length==3 && text[0] is >= '1' and <= '5' && text[1] is >= '1' and <= '9' && text[2] is >= '1' and <= '9';
}
