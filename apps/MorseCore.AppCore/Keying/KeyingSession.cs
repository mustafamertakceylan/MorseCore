using System.Text;

namespace MorseCore.AppCore.Keying;

public enum KeyingTimingKind { Dot, Dash, ElementGap, LetterGap, WordGap }

public sealed record KeyingTiming(KeyingTimingKind Kind, int Count, double AverageMilliseconds, double ExpectedMilliseconds);
public sealed record KeyingSnapshot(string Morse, string Text, int Marks, double EffectiveWpm, double CharacterWpm,
    int RhythmScore, IReadOnlyList<KeyingTiming> Timings, IReadOnlyList<string> Feedback,
    bool IsPressed, bool IsFinished, bool LimitReached, int Interrupted, int IgnoredTaps);

/// <summary>
/// Measures a straight key against an explicit target speed. All timestamps must come from one
/// monotonic clock. Classification boundaries are two units for marks/letters and five for words;
/// the actual measured durations are retained for feedback rather than rounded to those boundaries.
/// </summary>
public sealed class KeyingSession
{
    public const int MaximumMarks=4096;
    public const double HoldTimeoutMilliseconds=10_000;
    private readonly IReadOnlyDictionary<string,string> alphabet;
    private readonly double unit;
    private readonly StringBuilder morse=new(), decoded=new(), current=new();
    private readonly List<(KeyingTimingKind Kind,double Duration)> measurements=[];
    private double lastTimestamp=-1, pressStart, lastRelease, pendingGap, firstPress;
    private bool pressed, finished, hasRelease, wordSeparated;
    private int marks, interrupted, ignoredTaps;
    private double lastValidRelease;

    public KeyingSession(int targetWpm,IReadOnlyDictionary<string,string> alphabet)
    {
        if(targetWpm is <5 or >60) throw new ArgumentOutOfRangeException(nameof(targetWpm));
        ArgumentNullException.ThrowIfNull(alphabet);
        this.alphabet=new Dictionary<string,string>(alphabet,StringComparer.Ordinal);
        unit=1200d/targetWpm;
    }
    public bool IsPressed=>pressed;
    public bool IsFinished=>finished;

    public bool KeyDown(TimeSpan timestamp)
    {
        double now=Validate(timestamp);
        if(finished || pressed || marks>=MaximumMarks) return false;
        AdvanceCore(now);
        pressed=true; pressStart=now;
        pendingGap=hasRelease ? now-lastRelease : 0;
        return true;
    }
    public bool KeyUp(TimeSpan timestamp)
    {
        double now=Validate(timestamp);
        if(finished || !pressed) return false;
        pressed=false;
        double duration=now-pressStart;
        if(duration>HoldTimeoutMilliseconds) { interrupted++; return false; }
        // A zero-length or sub-5 ms transition is input noise, not a deliberate Morse element.
        if(duration<5) { ignoredTaps++; return false; }
        if(marks==0) firstPress=pressStart;
        else if(hasRelease)
        {
            KeyingTimingKind gap=pendingGap>=5*unit ? KeyingTimingKind.WordGap : pendingGap>=2*unit ? KeyingTimingKind.LetterGap : KeyingTimingKind.ElementGap;
            measurements.Add((gap,pendingGap));
        }
        bool dash=duration>=2*unit;
        current.Append(dash ? '-' : '.');
        measurements.Add((dash ? KeyingTimingKind.Dash : KeyingTimingKind.Dot,duration));
        marks++; hasRelease=true; lastRelease=lastValidRelease=now; wordSeparated=false;
        return true;
    }
    public void Advance(TimeSpan timestamp)
    {
        double now=Validate(timestamp);
        if(!finished) AdvanceCore(now);
    }
    private void AdvanceCore(double now)
    {
        if(pressed)
        {
            if(now-pressStart>HoldTimeoutMilliseconds) { pressed=false; interrupted++; }
            return;
        }
        if(!hasRelease) return;
        double gap=now-lastRelease;
        if(gap>=2*unit) FlushLetter();
        if(gap>=5*unit && !wordSeparated && decoded.Length>0)
        {
            morse.Append(" / "); decoded.Append(' '); wordSeparated=true;
        }
    }
    /// <summary>Discards an unfinished press on focus/window loss. Never invents a missing key-up.</summary>
    public void CancelPending()
    {
        if(pressed) { pressed=false; interrupted++; }
    }
    public void Finish(TimeSpan timestamp)
    {
        _=Validate(timestamp);
        if(finished) return;
        CancelPending(); FlushLetter(); finished=true;
    }
    public KeyingSnapshot Snapshot()
    {
        string code=morse.ToString();
        if(current.Length>0) code+=(code.Length>0 && !code.EndsWith(' ') ? " " : "")+current;
        var timings=Enum.GetValues<KeyingTimingKind>().Select(kind=>
        {
            var values=measurements.Where(item=>item.Kind==kind).Select(item=>item.Duration).ToArray();
            return new KeyingTiming(kind,values.Length,values.Length==0 ? 0 : values.Average(),Expected(kind));
        }).ToArray();
        double units=measurements.Sum(item=>Units(item.Kind));
        double elapsed=marks>0 ? lastValidRelease-firstPress : 0;
        double characterUnit=measurements.Where(item=>item.Kind is KeyingTimingKind.Dot or KeyingTimingKind.Dash)
            .Select(item=>item.Duration/Units(item.Kind)).DefaultIfEmpty(0).Average();
        double error=measurements.Select(item=>Math.Abs(item.Duration/Expected(item.Kind)-1)).DefaultIfEmpty(1).Average();
        var feedback=new List<string>();
        if(interrupted>0) feedback.Add("keyingInterrupted");
        if(ignoredTaps>0) feedback.Add("keyingIgnoredTaps");
        if(marks>=MaximumMarks) feedback.Add("keyingLimit");
        if(decoded.ToString().Contains('�')) feedback.Add("keyingUnrecognized");
        foreach(var timing in timings.Where(item=>item.Count>0))
        {
            double ratio=timing.AverageMilliseconds/timing.ExpectedMilliseconds;
            if(ratio<.75) feedback.Add("keying"+timing.Kind+"Short");
            else if(ratio>1.25) feedback.Add("keying"+timing.Kind+"Long");
        }
        if(marks==0) feedback.Add("keyingNoInput");
        else if(feedback.Count==0) feedback.Add(error<=.25 ? "keyingBalanced" : "keyingUneven");
        return new(code.Trim(' ','/'),decoded.ToString().Trim(),marks,elapsed>0 ? 1200*units/elapsed : 0,
            characterUnit>0 ? 1200/characterUnit : 0,(int)Math.Round(Math.Clamp(100*(1-error),0,100)),
            timings,feedback,pressed,finished,marks>=MaximumMarks,interrupted,ignoredTaps);
    }
    private void FlushLetter()
    {
        if(current.Length==0) return;
        string code=current.ToString();
        if(morse.Length>0 && morse[^1]!=' ') morse.Append(' ');
        morse.Append(code);
        decoded.Append(alphabet.TryGetValue(code,out string? character) ? character : "�");
        current.Clear();
    }
    private double Validate(TimeSpan timestamp)
    {
        double value=timestamp.TotalMilliseconds;
        if(value<0 || value<lastTimestamp) throw new ArgumentOutOfRangeException(nameof(timestamp),"A monotonic non-negative timestamp is required.");
        lastTimestamp=value; return value;
    }
    private double Expected(KeyingTimingKind kind)=>Units(kind)*unit;
    private static int Units(KeyingTimingKind kind)=>kind switch { KeyingTimingKind.Dash or KeyingTimingKind.LetterGap=>3,KeyingTimingKind.WordGap=>7,_=>1 };
}
