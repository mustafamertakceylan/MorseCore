using MorseCore.AppCore.Keying;

internal static class KeyingChecks
{
    public static void Run(Action<bool,string> check)
    {
        var alphabet=new Dictionary<string,string> { ["."]="E",["-"]="T",["..."]="S",["---"]="O",[".-"]="A",[".."]="I" };
        KeyingSession Session(int wpm=20)=>new(wpm,alphabet);
        static TimeSpan At(double milliseconds)=>TimeSpan.FromMilliseconds(milliseconds);
        void Reject(Action action,string label)
        { try { action(); } catch(ArgumentException) { check(true,label); return; } check(false,label); }
        var sos=Session();
        double clock=0;
        foreach(var (code,index) in new[]{("...",0),("---",1),("...",2)})
        {
            for(int i=0;i<code.Length;i++)
            {
                check(sos.KeyDown(At(clock)),"Straight-key SOS down accepted");
                check(!sos.KeyDown(At(clock+1)),"Keyboard auto-repeat does not create a mark");
                clock+=code[i]=='-' ? 180 : 60;
                check(sos.KeyUp(At(clock)),"Straight-key SOS up accepted");
                if(i<code.Length-1) clock+=60;
            }
            if(index<2) clock+=180;
        }
        sos.Finish(At(clock));
        var snapshot=sos.Snapshot();
        check(snapshot.Text=="SOS" && snapshot.Morse=="... --- ..." && snapshot.Marks==9,"Timed SOS decodes without invented trailing word");
        check(Math.Abs(snapshot.EffectiveWpm-20)<.001 && Math.Abs(snapshot.CharacterWpm-20)<.001,"Actual and character WPM use measured PARIS-equivalent timing");
        check(snapshot.RhythmScore==100 && snapshot.Feedback.SequenceEqual(new[]{"keyingBalanced"}),"Correct marks and gaps receive balanced rhythm feedback");
        check(snapshot.Timings.Single(item=>item.Kind==KeyingTimingKind.Dot).Count==6 && snapshot.Timings.Single(item=>item.Kind==KeyingTimingKind.Dash).AverageMilliseconds==180,"Separate real dot and dash duration metrics");
        check(snapshot.Timings.Single(item=>item.Kind==KeyingTimingKind.LetterGap).Count==2 && snapshot.Timings.Single(item=>item.Kind==KeyingTimingKind.ElementGap).Count==6,"Letter and within-letter gaps measured separately");
        check(!sos.KeyDown(At(clock+1)) && !sos.KeyUp(At(clock+2)),"Finished session ignores later keys");
        var words=Session(); words.KeyDown(At(0)); words.KeyUp(At(60)); words.Advance(At(180));
        check(words.Snapshot().Text=="E","Idle timer finalizes letter at the two-unit classification boundary");
        words.Advance(At(1000));
        check(words.Snapshot().Text=="E" && words.Snapshot().Morse==".","Idle silence is trimmed and does not lower WPM");
        words.KeyDown(At(1040)); words.KeyUp(At(1100)); words.Finish(At(5000));
        check(words.Snapshot().Text=="E E" && words.Snapshot().Morse==". / .","Long measured silence creates one word separator");
        check(words.Snapshot().EffectiveWpm<words.Snapshot().CharacterWpm && words.Snapshot().Feedback.Contains("keyingWordGapLong"),"Long word pauses reduce effective speed and receive concrete feedback");
        var cancel=Session(); cancel.KeyDown(At(0)); cancel.CancelPending(); cancel.KeyUp(At(60)); cancel.Finish(At(70));
        check(cancel.Snapshot() is { Marks:0,Interrupted:1,IsPressed:false } && cancel.Snapshot().Feedback.Contains("keyingInterrupted"),"Focus loss discards missing key-up rather than inventing a dash");
        var timeout=Session(); timeout.KeyDown(At(0)); timeout.Advance(At(10001));
        check(!timeout.IsPressed && timeout.Snapshot().Interrupted==1,"Ten-second stuck press releases itself");
        timeout.KeyUp(At(10002)); timeout.KeyDown(At(10003)); timeout.KeyUp(At(10063)); timeout.Finish(At(10200));
        check(timeout.Snapshot().Text=="E" && timeout.Snapshot().Marks==1,"Training resumes cleanly after missing-key timeout");
        var lateUp=Session(); lateUp.KeyDown(At(0));
        check(!lateUp.KeyUp(At(10001)) && lateUp.Snapshot().Marks==0,"Overlong release cannot sneak past timeout when timer has not fired");
        var noise=Session(); noise.KeyDown(At(0)); noise.KeyUp(At(4)); noise.KeyDown(At(10)); noise.KeyUp(At(70)); noise.Finish(At(90));
        check(noise.Snapshot().Text=="E" && noise.Snapshot().IgnoredTaps==1 && noise.Snapshot().Timings.Sum(item=>item.Count)==1,"Sub-five-ms input noise leaves no extra mark or gap");
        var bad=Session(); bad.KeyDown(At(0)); bad.KeyUp(At(35)); bad.KeyDown(At(135)); bad.KeyUp(At(370)); bad.Finish(At(400));
        check(bad.Snapshot().Text=="A" && bad.Snapshot().Feedback.Contains("keyingDotShort") && bad.Snapshot().Feedback.Contains("keyingDashLong") && bad.Snapshot().Feedback.Contains("keyingElementGapLong"),"Wrong rhythm yields specific short-dot, long-dash and spacing guidance");
        check(bad.Snapshot().RhythmScore<80,"Timing quality score responds to measured deviations");
        var boundary=Session(); boundary.KeyDown(At(0)); boundary.KeyUp(At(120)); boundary.Finish(At(120));
        check(boundary.Snapshot().Text=="T","Exactly two target units are classified as dash");
        var unknown=Session();
        for(int i=0;i<8;i++) { unknown.KeyDown(At(i*120)); unknown.KeyUp(At(i*120+60)); }
        unknown.Finish(At(1000)); check(unknown.Snapshot().Text=="�" && unknown.Snapshot().Feedback.Contains("keyingUnrecognized"),"Unknown code remains visibly uncertain and gets explicit feedback");
        foreach(var (gap,result) in new[]{(119d,"I"),(120d,"EE"),(299d,"EE"),(300d,"E E")})
        {
            var gaps=Session(); gaps.KeyDown(At(0)); gaps.KeyUp(At(60)); gaps.KeyDown(At(60+gap)); gaps.KeyUp(At(120+gap)); gaps.Finish(At(120+gap));
            check(gaps.Snapshot().Text==result,"Deterministic letter/word boundary at gap "+gap);
        }
        var duplicate=Session(); duplicate.KeyDown(At(0)); duplicate.KeyUp(At(60));
        check(!duplicate.KeyUp(At(61)) && duplicate.Snapshot().Marks==1,"Duplicate key-up does not append another mark");
        var copiedAlphabet=new Dictionary<string,string> { ["."]="E" }; var isolated=new KeyingSession(20,copiedAlphabet); copiedAlphabet["."]="X";
        isolated.KeyDown(At(0)); isolated.KeyUp(At(60)); isolated.Finish(At(60));
        check(isolated.Snapshot().Text=="E","Session owns an immutable snapshot of the chosen alphabet");
        var slow=Session(5); slow.KeyDown(At(0)); slow.KeyUp(At(240)); slow.Finish(At(240));
        check(slow.Snapshot().CharacterWpm==5,"Five-WPM lower bound uses a 240-ms dot");
        var fast=Session(60); fast.KeyDown(At(0)); fast.KeyUp(At(20)); fast.Finish(At(20));
        check(fast.Snapshot().CharacterWpm==60,"Sixty-WPM upper bound uses a 20-ms dot");
        Reject(()=>Session(4),"Reject unsupported low speed"); Reject(()=>Session(61),"Reject unsupported high speed");
        Reject(()=>Session().KeyDown(At(-1)),"Reject negative timestamp");
        var monotonic=Session(); monotonic.KeyDown(At(100));
        Reject(()=>monotonic.KeyUp(At(90)),"Reject backwards timestamp instead of corrupting timings");
        var empty=Session(); empty.Finish(At(0));
        check(empty.Snapshot() is { EffectiveWpm:0,CharacterWpm:0,Marks:0 } && empty.Snapshot().Feedback.Contains("keyingNoInput"),"Empty attempt never emits NaN or invented accuracy");
        var maximum=Session();
        for(int i=0;i<KeyingSession.MaximumMarks;i++) { maximum.KeyDown(At(i*240)); maximum.KeyUp(At(i*240+60)); }
        check(!maximum.KeyDown(At(KeyingSession.MaximumMarks*240)) && maximum.Snapshot().LimitReached,"Long session mark count is bounded");
        check(KeyingComparison.Accuracy("SOS","SOS")==100 && KeyingComparison.Accuracy("SOS","SO")==67,"Reference comparison counts missing letters");
        check(KeyingComparison.Accuracy("SOS","SOOS")==75 && KeyingComparison.Accuracy("SOS","XYZ")==0,"Reference comparison counts extra and wrong letters");
        check(KeyingComparison.Accuracy("","E")==0 && KeyingComparison.Accuracy("","")==100,"Empty reference comparison is deterministic");
        Reject(()=>KeyingComparison.Accuracy(new string('S',257),"S"),"Reference comparison input is bounded");
        var arabic=new KeyingSession(20,new Dictionary<string,string> { ["."]="ه" });
        arabic.KeyDown(At(0)); arabic.KeyUp(At(60)); arabic.Finish(At(60));
        check(arabic.Snapshot().Text=="ه","Decoder supports selected non-Latin alphabet without flipping Morse direction");
    }
}
