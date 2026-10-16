using Microsoft.UI.Xaml.Media;
using MorseCore.AppCore;
using MorseCore.AppCore.Radio;
using MorseCore.Interop;
using Windows.Storage.Pickers;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private readonly List<string> files=[];
    private bool batchWorking;
    private void BuildFiles()
    {
        Heading(T("files"),T("fileLimit"));
        var stack=new StackPanel { Spacing=18 };
        PageContent.Children.Add(Card(Columns(280,2,AlphabetChoice(),Choice(T("conversionDirection"),[T("textToMorse"),T("morseToText")],decode?1:0,i=> { decode=i==1; result=null; revision++; }))));
        stack.Children.Add(Action("chooseFiles",async()=>
        {
            if(batchWorking) return;
            var picker=new FileOpenPicker(); picker.FileTypeFilter.Add(".txt"); InitializePicker(picker);
            var selected=await picker.PickMultipleFilesAsync();
            if(selected.Count>0) { files.Clear(); files.AddRange(selected.Select(f=>f.Path)); BuildPage(); }
        },"\uE8B7"));
        var list=new ListView { ItemsSource=files.Select(Path.GetFileName).ToArray(),MaxHeight=240,SelectionMode=ListViewSelectionMode.None };
        stack.Children.Add(list);
        var destination=new StackPanel { Spacing=14 };
        destination.Children.Add(Label(T("outputFolder"),18));
        destination.Children.Add(Label(preferences.OutputDirectory,14,true)); destination.Children.Add(Action("chooseFolder",ChooseDirectory));
        var convertFiles=Action("convert",async()=>
        {
            if(batchWorking || files.Count==0) return;
            batchWorking=true; int done=0;
            var snapshot=preferences; bool direction=decode; var selected=files.ToArray();
            try
            {
                foreach(string file in selected)
                {
                    SetStatus($"{T("busy")} {done+1}/{selected.Length}");
                    string output=Path.Combine(snapshot.OutputDirectory,Path.GetFileNameWithoutExtension(file)+(direction?".text.txt":".morse.txt"));
                    await FileConversion.ConvertAsync(file,output,direction,snapshot.Alphabet,snapshot.Separator,lifetime.Token); done++;
                }
                SetStatus($"{done} {T("filesDone")}");
            }
            catch(IOException) { SetStatus($"{done}/{selected.Length} · {T("fileError")}"); }
            finally { batchWorking=false; }
        },"\uE8D4",true);
        convertFiles.IsEnabled=files.Count>0;
        stack.Children.Add(convertFiles);
        stack.Children.Add(WorkspaceFeedback());
        PageContent.Children.Add(Columns(320,2,Card(stack),Card(destination)));
    }
    private string audioSource="";
    private bool audioIsMorse;
    private int audioWorkspace;
    private Button? audioPlayButton,audioStopButton,audioExportButton;
    private void UpdateAudioActions()
    {
        bool active=playback!=null;
        if(audioPlayButton!=null) audioPlayButton.IsEnabled=!active && !string.IsNullOrWhiteSpace(audioSource);
        if(audioStopButton!=null) audioStopButton.Visibility=active?Visibility.Visible:Visibility.Collapsed;
        if(audioExportButton!=null) audioExportButton.IsEnabled=!active && !string.IsNullOrWhiteSpace(audioSource);
    }
    private string AudioMorse()=>audioIsMorse ? audioSource : MorseEngine.Encode(audioSource,preferences.Alphabet,preferences.Separator).Text;
    private void BuildAudio()
    {
        Heading(T("audio"));
        int[] sections=[0,3,1,2];
        var workspace=WorkspaceTabs("AudioWorkspace",["uxMorseAudio","speech","workspaceDecode","workspaceVoices"],Array.IndexOf(sections,audioWorkspace),selected=>
        {
            int index=sections[selected];
            if(audioWorkspace==index) return;
            StopPlayback(); audioWorkspace=index; BuildPage();
        });
        PageContent.Children.Add(workspace);
        if(audioWorkspace==1) { BuildAudioDecoderSection(); return; }
        if(audioWorkspace==2) { BuildVoicePacksPage(); return; }
        var stack=new StackPanel { Spacing=18 };
        stack.Children.Add(Label(T(audioWorkspace==3?"uxSpeechIntro":"uxMorseIntro"),14,true));
        stack.Children.Add(Columns(260,2,AlphabetChoice(),Choice(T("audioInputType"),[T("plainText"),T("morseInput")],audioIsMorse?1:0,i=> { if(audioIsMorse==(i==1)) return; audioIsMorse=i==1; BuildPage(); })));
        if(result!=null || !string.IsNullOrWhiteSpace(source))
            stack.Children.Add(Action("useConversion",()=> { audioSource=result?.Text ?? source; audioIsMorse=result!=null ? !decode : decode; BuildPage(); return Task.CompletedTask; }));
        var morse=new TextBox { Header=T(audioIsMorse?"morseInput":"plainText"),Text=audioSource,PlaceholderText=audioIsMorse?"... --- ...":T("inputPlaceholder"),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=160,MaxLength=200_000,FlowDirection=audioIsMorse?FlowDirection.LeftToRight:PlainDirection,FontFamily=new FontFamily(audioIsMorse?"Consolas":"Segoe UI"),FontSize=18 };
        morse.TextChanged+=(_,_)=> { audioSource=morse.Text; UpdateAudioActions(); UpdateSpeechActions(); }; stack.Children.Add(morse);
        if(audioWorkspace==3)
        {
            PageContent.Children.Add(Card(stack));
            BuildSpeech(async()=>
            {
                string value=audioSource; var options=preferences; bool fromMorse=audioIsMorse;
                return fromMorse ? (await Task.Run(()=>MorseEngine.Decode(value,options.Alphabet,options.Separator))).Text : value;
            });
            PageContent.Children.Add(WorkspaceFeedback());
            return;
        }
        var controls=Row();
        audioPlayButton=Action("play",()=>PlayMorse(AudioMorse()),"\uE768",true);
        audioStopButton=Action("stop",()=> { StopPlayback(); return Task.CompletedTask; },"\uE71A");
        audioExportButton=Action("exportWav",async()=>
        {
            string snapshot=AudioMorse(); var options=preferences;
            string? path=await SavePath("wav","morse");
            if(path!=null) { await Task.Run(()=>MorseAudio.WriteWav(path,snapshot,new(options.Wpm,options.Frequency),options.Separator)); SetStatus(T("saved")); }
        },"\uE74E");
        controls.Children.Add(audioPlayButton); controls.Children.Add(audioStopButton); controls.Children.Add(audioExportButton);
        stack.Children.Add(controls); UpdateAudioActions();
        stack.Children.Add(WorkspaceFeedback());
        WrapActions(controls,stack);
        var speed=new NumberBox { Header=T("speed"),Minimum=5,Maximum=60,Value=preferences.Wpm,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        speed.ValueChanged+=(_,_)=> { if(!double.IsNaN(speed.Value)) { preferences=preferences with { Wpm=(uint)Math.Clamp(speed.Value,5,60) }; SavePreferences(); } };
        var frequency=new NumberBox { Header=T("frequency"),Minimum=200,Maximum=2000,Value=preferences.Frequency,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        frequency.ValueChanged+=(_,_)=> { if(!double.IsNaN(frequency.Value)) { preferences=preferences with { Frequency=(uint)Math.Clamp(frequency.Value,200,2000) }; SavePreferences(); } };
        stack.Children.Add(new Expander { Header=T("uxAdvanced"),Content=Columns(220,2,speed,frequency),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch });
        PageContent.Children.Add(Card(stack));
    }
    private void BuildAlphabet()
    {
        Heading(T("alphabet"),T("normalizationNote"));
        if(!nativeReady) { PageContent.Children.Add(Label(T("nativeUnavailable"))); return; }
        PageContent.Children.Add(AlphabetChoice());
        var cells=new List<FrameworkElement>();
        var entries=MorseEngine.Alphabet(preferences.Alphabet);
        for(int i=0;i<entries.Count;i++)
        {
            var entry=entries[i]; var line=Row();
            var character=Label(entry.Character.ToString(),22); character.Width=34;
            line.Children.Add(character);
            line.Children.Add(new TextBlock { Text=entry.Code,FontFamily=new FontFamily("Consolas"),FontSize=17,VerticalAlignment=VerticalAlignment.Center,FlowDirection=FlowDirection.LeftToRight });
            var cell=Card(line); cell.Padding=new Thickness(12); cells.Add(cell);
        }
        PageContent.Children.Add(Columns(180,4,cells.ToArray()));
    }
    private CwLesson? lesson;
    private string practiceAnswer="",practiceFeedback="";
    private int practiceMode;
    private void BuildPractice()
    {
        Heading(T("practice"),T("practiceNote"));
        var stack=new StackPanel { Spacing=14 };
        stack.Children.Add(Choice(T("mode"),[T("letters"),T("digits"),T("wordPractice"),T("qcodes"),T("callSigns"),T("prosigns")],practiceMode,i=>practiceMode=i));
        var signal=Label(lesson?.Current?.Morse ?? (lesson!=null ? T("practiceEnded") : "· — · ·"),28);
        signal.FontFamily=new FontFamily("Consolas"); signal.FlowDirection=FlowDirection.LeftToRight;
        var signalCard=Card(signal); signalCard.Background=Brush("ResultBrush"); signal.HorizontalAlignment=HorizontalAlignment.Center;
        stack.Children.Add(signalCard);
        var answer=new TextBox { Header=T("answer"),Text=practiceAnswer,MaxLength=64,FlowDirection=FlowDirection.LeftToRight };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(answer,"PracticeAnswer");
        answer.TextChanged+=(_,_)=>practiceAnswer=answer.Text; stack.Children.Add(answer);
        var feedback=Label(practiceFeedback,14); var counts=Label("",13,true);
        void Refresh()
        {
            signal.Text=lesson?.Current?.Morse ?? T("practiceEnded");
            if(lesson!=null) counts.Text=$"{lesson.Correct} {T("correct")} · {lesson.Incorrect} {T("incorrect")} · {lesson.Skipped} {T("skipped")}";
        }
        Refresh();
        var start=Row();
        start.Children.Add(Action("start",()=> { lesson=new(CwLearning.Questions(practiceMode,Random.Shared.Next())); lesson.Next(); practiceFeedback=feedback.Text=""; answer.Text=""; Refresh(); return Task.CompletedTask; },primary:true));
        start.Children.Add(Action("retryMissed",()=> { if(lesson?.Missed.Count>0) { lesson=new(lesson.Missed.ToArray()); lesson.Next(); answer.Text=""; Refresh(); } return Task.CompletedTask; }));
        stack.Children.Add(start); WrapActions(start,stack);
        void Submit(string value)
        {
            if(lesson?.Current is not { } question) return;
            bool correct=lesson.Submit(value);
            practiceFeedback=feedback.Text=$"{T(correct?"correct":value.Trim().Length==0?"skipped":"incorrect")} · {question.Answer}";
            lesson.Next(); answer.Text=""; Refresh(); answer.Focus(FocusState.Programmatic);
        }
        var controls=Row();
        controls.Children.Add(Action("submit",()=> { Submit(answer.Text); return Task.CompletedTask; }));
        controls.Children.Add(Action("skip",()=> { Submit(""); return Task.CompletedTask; }));
        controls.Children.Add(Action("play",()=>lesson?.Current is { } question ? PlayMorse(question.Morse,'/') : Task.CompletedTask));
        stack.Children.Add(controls); WrapActions(controls,stack); stack.Children.Add(feedback); stack.Children.Add(counts); PageContent.Children.Add(Card(stack));
    }
}
