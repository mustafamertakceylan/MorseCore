using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MorseCore.AppCore.Keying;
using MorseCore.Interop;
using Windows.System;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private KeyingSession? keyingSession;
    private Stopwatch? keyingClock;
    private DispatcherTimer? keyingTimer;
    private Action? keyingCancelInput;
    private Button? keyingSurface;
    private KeyingSidetone? keyingTone;
    private int keyingWpm=20;
    private bool keyingSound=true;
    private string keyingExpected="SOS";
    private string keyingNormalizedExpected="";
#if DEBUG
    private Action<bool>? keyingSmokePress;
    private Func<TimeSpan>? keyingSmokeClock;
#endif

    private TimeSpan KeyingNow()
    {
#if DEBUG
        if(SmokeDirectory is not null && keyingSmokeClock is { } clock) return clock();
#endif
        return keyingClock?.Elapsed ?? TimeSpan.Zero;
    }

    private void BuildKeying()
    {
        Heading(T("keyingTitle"),T("keyingIntroduction"));
        if(!nativeReady) { PageContent.Children.Add(Label(T("nativeUnavailable"))); return; }
        var alphabet=MorseEngine.Alphabet(preferences.Alphabet).GroupBy(symbol=>symbol.Code)
            .ToDictionary(group=>group.Key,group=>group.First().Character.ToString(),StringComparer.Ordinal);
        var stack=new StackPanel { Spacing=16 };
        stack.Children.Add(AlphabetChoice());
        var speed=new NumberBox { Header=T("keyingTargetSpeed"),Minimum=5,Maximum=60,Value=keyingWpm,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        var sound=new ToggleSwitch { Header=T("keyingSidetone"),IsOn=keyingSound,OnContent=T("keyingOn"),OffContent=T("keyingOff") };
        stack.Children.Add(Columns(260,2,speed,sound));
        var expected=new TextBox { Header=T("keyingReference"),Text=keyingExpected,MaxLength=64,FlowDirection=PlainDirection,PlaceholderText="SOS" };
        expected.TextChanged+=(_,_)=>keyingExpected=expected.Text;
        stack.Children.Add(expected);
        var keyLabel=Label(T("keyingPress"),22);
        var key=new Button { Content=keyLabel,Height=100,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Center,CornerRadius=new CornerRadius(14),Background=Brush("CanvasBrush"),BorderBrush=Brush("AccentInkBrush"),BorderThickness=new Thickness(2) };
        keyingSurface=key;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(key,"KeyingPaddle");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(key,T("keyingPress"));
        var guide=Label(T("keyingKeyboardHelp"),13,true); guide.TextWrapping=TextWrapping.Wrap;
        stack.Children.Add(key); stack.Children.Add(guide);
        var transcript=new TextBox { Header=T("keyingDecoded"),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MinHeight=70,FlowDirection=PlainDirection };
        var symbols=new TextBox { Header=T("keyingMorse"),IsReadOnly=true,FontFamily=MonoFont,TextWrapping=TextWrapping.Wrap,FlowDirection=FlowDirection.LeftToRight,MinHeight=60 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(transcript,"KeyingTranscript");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(symbols,"KeyingMorse");
        var resultLabel=Label("",14); resultLabel.TextWrapping=TextWrapping.Wrap;
        var timings=Label("",13,true); timings.TextWrapping=TextWrapping.Wrap;
        var feedback=Label("",14); feedback.TextWrapping=TextWrapping.Wrap;
        string? inputKind=null;
        string? referenceError=null;
        string? sessionReference=null;
        uint pointerId=0;
        bool toneUnavailable=false;
        void Visual(bool down)
        {
            key.Background=Brush(down ? "AccentBrush" : "CanvasBrush");
            keyLabel.Foreground=down ? new SolidColorBrush(Microsoft.UI.Colors.White) : Brush("InkBrush");
            keyLabel.Text=T(down ? "keyingHeld" : "keyingPress");
        }
        void Refresh()
        {
            if(keyingSession==null) { feedback.Text=referenceError ?? ""; return; }
            var snapshot=keyingSession.Snapshot();
            if(transcript.Text!=snapshot.Text) transcript.Text=snapshot.Text;
            if(symbols.Text!=snapshot.Morse) symbols.Text=snapshot.Morse;
            resultLabel.Text=$"{T("keyingEffectiveSpeed")}: {snapshot.EffectiveWpm:0.0} WPM  ·  {T("keyingCharacterSpeed")}: {snapshot.CharacterWpm:0.0} WPM  ·  {T("keyingRhythm")}: {snapshot.RhythmScore}%";
            if(snapshot.IsFinished && keyingNormalizedExpected.Length>0)
                resultLabel.Text+=$"  ·  {T("keyingAccuracy")}: {KeyingComparison.Accuracy(keyingNormalizedExpected,snapshot.Text)}%";
            timings.Text=string.Join("\n",snapshot.Timings.Select(item=>$"{T("keyingTiming"+item.Kind)}: {(item.Count==0 ? "—" : item.AverageMilliseconds.ToString("0"))} ms  /  {T("keyingTarget")}: {item.ExpectedMilliseconds:0} ms"));
            feedback.Text=string.Join("\n",snapshot.Feedback.Select(T));
            if(toneUnavailable) feedback.Text+="\n"+T("keyingSoundUnavailable");
            if(referenceError!=null) feedback.Text+="\n"+referenceError;
        }
        void CancelInput()
        {
            if(!ReferenceEquals(keyingSurface,key)) return;
            inputKind=null; keyingSession?.CancelPending(); keyingTone?.Stop(); Visual(false); key.ReleasePointerCaptures(); Refresh();
        }
        keyingCancelInput=CancelInput;
        bool TryReference(out string normalized)
        {
            normalized="";
            try
            {
                normalized=string.IsNullOrWhiteSpace(expected.Text) ? "" : MorseEngine.Decode(MorseEngine.Encode(expected.Text,preferences.Alphabet).Text,preferences.Alphabet).Text;
                referenceError=null;
                return true;
            }
            catch(MorseNativeException) { referenceError=T("unsupported"); }
            catch(ArgumentException) { referenceError=T("unsupported"); }
            SetStatus(referenceError!);
            return false;
        }
        bool TryStart()
        {
            CancelInput();
            if(!TryReference(out string normalized))
            {
                if(keyingClock!=null && keyingSession is { IsFinished:false }) keyingSession.Finish(KeyingNow());
                keyingClock?.Stop(); keyingTimer?.Stop();
                keyingTone?.Dispose(); keyingTone=null;
                Refresh(); return false;
            }
            keyingSession=new(keyingWpm,alphabet); keyingClock=Stopwatch.StartNew();
            keyingNormalizedExpected=normalized;
            sessionReference=expected.Text;
            toneUnavailable=false;
            keyingTone?.Dispose(); keyingTone=keyingSound ? KeyingSidetone.TryCreate(preferences.Frequency) : null;
            toneUnavailable=keyingSound && keyingTone==null;
            keyingTimer?.Start(); SetStatus(T("ready")); Refresh(); return true;
        }
        void Down(string kind,uint id=0)
        {
            if(!ReferenceEquals(keyingSurface,key) || inputKind!=null) return;
            if((keyingSession==null || keyingSession.IsFinished || sessionReference!=expected.Text) && !TryStart()) return;
            if(keyingClock==null || !keyingSession!.KeyDown(KeyingNow())) return;
            inputKind=kind; pointerId=id; Visual(true);
            if(keyingSound && keyingTone!=null && !keyingTone.Start()) toneUnavailable=true;
            Refresh();
        }
        void Up(string kind,uint id=0)
        {
            if(!ReferenceEquals(keyingSurface,key) || inputKind!=kind || kind=="pointer" && pointerId!=id) return;
            inputKind=null;
            if(keyingClock!=null) keyingSession?.KeyUp(KeyingNow());
            keyingTone?.Stop(); Visual(false); Refresh();
        }
#if DEBUG
        keyingSmokePress=down=> { if(down) Down("keyboard"); else Up("keyboard"); };
#endif
        key.PreviewKeyDown+=(_,e)=> { if(e.Key!=VirtualKey.Space) return; e.Handled=true; if(!e.KeyStatus.WasKeyDown) Down("keyboard"); };
        key.PreviewKeyUp+=(_,e)=> { if(e.Key!=VirtualKey.Space) return; e.Handled=true; Up("keyboard"); };
        // Controls handle pointer presses themselves; handled-events-too keeps the key usable with mouse and touch.
        key.AddHandler(UIElement.PointerPressedEvent,new PointerEventHandler((_,e)=>
        {
            if(!ReferenceEquals(keyingSurface,key) || inputKind!=null || !e.GetCurrentPoint(key).Properties.IsLeftButtonPressed) return;
            key.Focus(FocusState.Pointer);
            // Starting clears old input and captures. Do that before capturing this first press,
            // otherwise TryStart -> CancelInput would discard the capture just acquired here.
            if((keyingSession==null || keyingSession.IsFinished || sessionReference!=expected.Text) && !TryStart()) { e.Handled=true; return; }
            if(!ReferenceEquals(keyingSurface,key) || inputKind!=null) return;
            if(key.CapturePointer(e.Pointer))
            {
                Down("pointer",e.Pointer.PointerId);
                if(inputKind!="pointer" || pointerId!=e.Pointer.PointerId) key.ReleasePointerCapture(e.Pointer);
                e.Handled=true;
            }
        }),true);
        key.AddHandler(UIElement.PointerReleasedEvent,new PointerEventHandler((_,e)=>
        { Up("pointer",e.Pointer.PointerId); key.ReleasePointerCapture(e.Pointer); e.Handled=true; }),true);
        key.PointerCanceled+=(_,e)=> { if(inputKind=="pointer" && pointerId==e.Pointer.PointerId) CancelInput(); };
        key.PointerCaptureLost+=(_,e)=> { if(inputKind=="pointer" && pointerId==e.Pointer.PointerId) CancelInput(); };
        key.LostFocus+=(_,_)=>CancelInput(); key.Unloaded+=(_,_)=> { if(ReferenceEquals(keyingSurface,key)) CleanupKeying(); };
        speed.ValueChanged+=(_,_)=>
        {
            if(double.IsNaN(speed.Value)) return;
            int value=(int)Math.Clamp(speed.Value,5,60);
            if(keyingWpm==value) return;
            keyingWpm=value; _=TryStart();
        };
        sound.Toggled+=(_,_)=>
        {
            keyingSound=sound.IsOn; keyingTone?.Stop(); keyingTone?.Dispose(); keyingTone=null;
            toneUnavailable=false;
            if(keyingSound && keyingSession is { IsFinished:false } && referenceError==null)
            { keyingTone=KeyingSidetone.TryCreate(preferences.Frequency); toneUnavailable=keyingTone==null; }
            Refresh();
        };
        keyingTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
        keyingTimer.Tick+=(_,_)=>
        {
            if(!ReferenceEquals(keyingSurface,key) || keyingSession==null || keyingClock==null) return;
            keyingSession.Advance(KeyingNow());
            if(inputKind!=null && !keyingSession.IsPressed) { inputKind=null; keyingTone?.Stop(); Visual(false); }
            Refresh();
        };
        App.MainWindow.Activated+=KeyingWindowActivated;
        var controls=Row();
        controls.Children.Add(Action("keyingStart",()=> { if(TryStart()) key.Focus(FocusState.Programmatic); return Task.CompletedTask; },"\uE768",true));
        controls.Children.Add(Action("keyingFinish",()=>
        {
            CancelInput();
            keyingNormalizedExpected=TryReference(out string normalized) ? normalized : "";
            if(keyingClock!=null) { keyingSession?.Finish(KeyingNow()); keyingClock.Stop(); }
            keyingTimer.Stop(); Refresh(); return Task.CompletedTask;
        },"\uE71A"));
        stack.Children.Add(controls); WrapActions(controls,stack);
        symbols.Background=Brush("ResultBrush"); symbols.MinHeight=transcript.MinHeight=90;
        stack.Children.Add(Columns(300,2,transcript,symbols)); stack.Children.Add(resultLabel); stack.Children.Add(timings); stack.Children.Add(feedback);
        var note=Label(T("keyingMeasurementNote"),13,true); note.TextWrapping=TextWrapping.Wrap; stack.Children.Add(note);
        PageContent.Children.Add(Card(stack));
        // Retain completed results when navigating back, but each new press starts a fresh session.
        if(keyingSession!=null) Refresh();
    }
    private void KeyingWindowActivated(object sender,WindowActivatedEventArgs e)
    {
#if MORSE_WINUI
        if(e.WindowActivationState==WindowActivationState.Deactivated) keyingCancelInput?.Invoke();
#else
        if(e.WindowActivationState==Windows.UI.Core.CoreWindowActivationState.Deactivated) keyingCancelInput?.Invoke();
#endif
    }
    private void CleanupKeying()
    {
        keyingCancelInput?.Invoke(); keyingCancelInput=null;
        keyingSurface=null;
        if(keyingClock!=null && keyingSession is { IsFinished:false }) keyingSession.Finish(KeyingNow());
        keyingClock?.Stop();
        keyingTimer?.Stop(); keyingTimer=null;
        keyingTone?.Dispose(); keyingTone=null;
#if DEBUG
        keyingSmokePress=null;
#endif
        App.MainWindow.Activated-=KeyingWindowActivated;
    }
}

/// <summary>A small reusable PCM sidetone; one prepared buffer per session, no worker/task per key press.</summary>
internal sealed class KeyingSidetone : IDisposable
{
    [StructLayout(LayoutKind.Sequential,Pack=2)]
    private struct Format { public ushort Tag,Channels; public uint SampleRate,BytesPerSecond; public ushort BlockAlign,Bits,Extra; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Header { public nint Data; public uint Length,Recorded; public nuint User; public uint Flags,Loops; public nint Next; public nuint Reserved; }
    [DllImport("winmm.dll",ExactSpelling=true)] private static extern uint waveOutOpen(out nint handle,uint device,ref Format format,nint callback,nint instance,uint flags);
    [DllImport("winmm.dll",ExactSpelling=true)] private static extern uint waveOutPrepareHeader(nint handle,nint header,uint size);
    [DllImport("winmm.dll",ExactSpelling=true)] private static extern uint waveOutWrite(nint handle,nint header,uint size);
    [DllImport("winmm.dll",ExactSpelling=true)] private static extern uint waveOutReset(nint handle);
    [DllImport("winmm.dll",ExactSpelling=true)] private static extern uint waveOutUnprepareHeader(nint handle,nint header,uint size);
    [DllImport("winmm.dll",ExactSpelling=true)] private static extern uint waveOutClose(nint handle);
    private nint handle,header,buffer;
    private readonly uint headerSize=(uint)Marshal.SizeOf<Header>();
    private bool playing;
    private LinuxAudio.Sidetone? linux;
    public static KeyingSidetone? TryCreate(uint frequency)
    {
        if(OperatingSystem.IsLinux())
        {
            var tone=LinuxAudio.CreateSidetone(frequency);
            return tone is null ? null : new KeyingSidetone { linux=tone };
        }
        if(!OperatingSystem.IsWindows()) return null;
        var result=new KeyingSidetone();
        try
        {
            var format=new Format { Tag=1,Channels=1,SampleRate=22050,BytesPerSecond=44100,BlockAlign=2,Bits=16 };
            if(waveOutOpen(out result.handle,uint.MaxValue,ref format,0,0,0)!=0) { result.Dispose(); return null; }
            var samples=new short[2205];
            // A 100 ms integral cycle buffer loops without a discontinuity.
            double hz=Math.Round(Math.Clamp(frequency,200u,2000u)/10d)*10;
            for(int i=0;i<samples.Length;i++) samples[i]=(short)(5000*Math.Sin(2*Math.PI*hz*i/22050));
            result.buffer=Marshal.AllocHGlobal(samples.Length*2); Marshal.Copy(samples,0,result.buffer,samples.Length);
            result.header=Marshal.AllocHGlobal((int)result.headerSize);
            Marshal.StructureToPtr(new Header { Data=result.buffer,Length=(uint)(samples.Length*2) },result.header,false);
            if(waveOutPrepareHeader(result.handle,result.header,result.headerSize)!=0) { result.Dispose(); return null; }
            return result;
        }
        catch(Exception e) when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        { result.Dispose(); return null; }
    }
    public bool Start()
    {
        if(linux is { } tone) return tone.Start();
        if(handle==0 || header==0) return false;
        if(playing) return true;
        var value=Marshal.PtrToStructure<Header>(header); value.Flags=(value.Flags&~1u)|12u; value.Loops=uint.MaxValue;
        Marshal.StructureToPtr(value,header,false);
        playing=waveOutWrite(handle,header,headerSize)==0; return playing;
    }
    public void Stop() { linux?.Stop(); if(handle!=0 && playing) { waveOutReset(handle); playing=false; } }
    public void Dispose()
    {
        Stop();
        linux?.Dispose(); linux=null;
        if(handle!=0)
        {
            if(header!=0) waveOutUnprepareHeader(handle,header,headerSize);
            waveOutClose(handle); handle=0;
        }
        if(header!=0) { Marshal.FreeHGlobal(header); header=0; }
        if(buffer!=0) { Marshal.FreeHGlobal(buffer); buffer=0; }
    }
}
