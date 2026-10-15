using MorseCore.Interop;

namespace MorseCore.AppCore.Radio;

public sealed record CwTerm(string Code,string Key,string Category,string Example);
public sealed record CwQuestion(string Answer,string Morse);

public static class CwLearning
{
    public static IReadOnlyList<CwTerm> Terms { get; }=Array.AsReadOnly(new CwTerm[] {
        new("QRL?","qrl","qcodes","QRL? DE TA1PDM K"), new("QRM","qrm","qcodes","QRM 5"),
        new("QRN","qrn","qcodes","QRN 3"), new("QRS","qrs","qcodes","PSE QRS"),
        new("QRQ","qrq","qcodes","PSE QRQ"), new("QRT","qrt","qcodes","NW QRT"),
        new("QRV","qrv","qcodes","QRV?"), new("QRZ?","qrz","qcodes","QRZ? DE DL1ABC K"),
        new("QSB","qsb","qcodes","QSB"), new("QSL","qsl","qcodes","QSL TNX"),
        new("QSO","qso","qcodes","TNX QSO"), new("QSY","qsy","qcodes","QSY 14030"),
        new("QTH","qth","qcodes","QTH ANKARA"), new("CQ","cq","abbreviations","CQ CQ DE TA1PDM K"),
        new("DE","de","abbreviations","DL1ABC DE TA1PDM"), new("K","k","abbreviations","DE TA1PDM K"),
        new("KN","kn","abbreviations","DL1ABC DE TA1PDM KN"), new("RST","rst","abbreviations","UR RST 579"),
        new("PSE","pse","abbreviations","PSE QRS"), new("TNX","tnx","abbreviations","TNX QSO"),
        new("73","73","abbreviations","73"), new("<AR>","ar","prosigns","TNX <AR>"),
        new("<SK>","sk","prosigns","73 <SK>"), new("<BT>","bt","prosigns","RST 579 <BT> NAME ALI"),
        new("<AS>","as","prosigns","<AS>"), new("<KN>","knProsign","prosigns","<KN>"),
        new("<HH>","hh","prosigns","<HH>") });
    public static IReadOnlyList<string> Phonetics { get; }=Array.AsReadOnly(new[] {
        "Alfa","Bravo","Charlie","Delta","Echo","Foxtrot","Golf","Hotel","India","Juliett","Kilo","Lima","Mike",
        "November","Oscar","Papa","Quebec","Romeo","Sierra","Tango","Uniform","Victor","Whiskey","X-ray","Yankee","Zulu" });
    public static IReadOnlyList<CwQuestion> Questions(int mode,int seed)
    {
        IEnumerable<string> answers=mode switch {
            0=>Enumerable.Range('A',26).Select(c=>((char)c).ToString()),
            1=>Enumerable.Range(0,10).Select(n=>n.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            2=>new[]{"MORSE","RADIO","HELLO","SIGNAL","ANTENNA","CALL","LISTEN","SHORT","LONG","CODE"},
            3=>Terms.Where(t=>t.Category=="qcodes").Select(t=>t.Code),
            4=>new[]{"TA1PDM","DL1ABC","R1ABC","G4ABC","K1ABC","HZ1AA","A61AA","TB2ABC","DN9ABC","M7ABC"},
            5=>CwMessages.Prosigns.Keys,
            _=>throw new ArgumentOutOfRangeException(nameof(mode)) };
        var shuffled=answers.ToArray(); new Random(seed).Shuffle(shuffled);
        return shuffled.Take(10).Select(a=>new CwQuestion(a,CwMessages.Encode(a))).ToArray();
    }
}

public sealed class CwLesson(IEnumerable<CwQuestion> questions)
{
    private readonly Queue<CwQuestion> pending=new(questions);
    private readonly Dictionary<string,CwQuestion> missed=new(StringComparer.Ordinal);
    public CwQuestion? Current { get; private set; }
    public int Correct { get; private set; }
    public int Incorrect { get; private set; }
    public int Skipped { get; private set; }
    public IReadOnlyCollection<CwQuestion> Missed=>missed.Values;
    public CwQuestion? Next()=>Current=pending.TryDequeue(out var value)?value:null;
    public bool Submit(string answer)
    {
        var question=Current ?? throw new InvalidOperationException("No active question.");
        string value=answer.Trim().ToUpperInvariant();
        bool correct=value==question.Answer || question.Answer.StartsWith('<') && question.Answer.EndsWith('>') && value==question.Answer[1..^1];
        if(correct) Correct++; else { if(value.Length==0) Skipped++; else Incorrect++; missed[question.Answer]=question; }
        Current=null; return correct;
    }
}
