using Microsoft.UI.Xaml.Media;
using MorseCore.AppCore;
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
        stack.Children.Add(AlphabetChoice());
        stack.Children.Add(Choice("",[T("textToMorse"),T("morseToText")],decode?1:0,i=> { decode=i==1; result=null; revision++; }));
        stack.Children.Add(Action("chooseFiles",async()=>
        {
            if(batchWorking) return;
            var picker=new FileOpenPicker(); picker.FileTypeFilter.Add(".txt"); InitializePicker(picker);
            var selected=await picker.PickMultipleFilesAsync();
            if(selected.Count>0) { files.Clear(); files.AddRange(selected.Select(f=>f.Path)); BuildPage(); }
        },"\uE8B7"));
        var list=new ListView { ItemsSource=files.Select(Path.GetFileName).ToArray(),MaxHeight=240,SelectionMode=ListViewSelectionMode.None };
        stack.Children.Add(list); stack.Children.Add(Label(T("outputFolder"),13,true));
        stack.Children.Add(Label(preferences.OutputDirectory,14)); stack.Children.Add(Action("chooseFolder",ChooseDirectory));
        stack.Children.Add(Action("convert",async()=>
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
        },"\uE8D4",true));
        PageContent.Children.Add(Card(stack));
    }
    private string audioSource="";
    private void BuildAudio()
    {
        Heading(T("audio"));
        var stack=new StackPanel { Spacing=18 }; stack.Children.Add(AlphabetChoice());
        var morse=new TextBox { Header=T("morse"),Text=audioSource,PlaceholderText="... --- ...",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=140,MaxLength=200_000,FlowDirection=FlowDirection.LeftToRight,FontFamily=new FontFamily("Consolas"),FontSize=20 };
        morse.TextChanged+=(_,_)=>audioSource=morse.Text; stack.Children.Add(morse);
        var controls=Row();
        controls.Children.Add(Action("play",()=>PlayMorse(audioSource),"\uE768",true));
        controls.Children.Add(Action("stop",()=> { StopPlayback(); return Task.CompletedTask; },"\uE71A"));
        controls.Children.Add(Action("exportWav",async()=>
        {
            string snapshot=audioSource; var options=preferences;
            string? path=await SavePath("wav","morse");
            if(path!=null) { await Task.Run(()=>MorseAudio.WriteWav(path,snapshot,new(options.Wpm,options.Frequency),options.Separator)); SetStatus(T("saved")); }
        },"\uE74E")); stack.Children.Add(controls);
        WrapActions(controls,stack);
        var optionsRow=Row();
        var speed=new NumberBox { Header=T("speed"),Minimum=5,Maximum=60,Value=preferences.Wpm,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        speed.ValueChanged+=(_,_)=> { if(!double.IsNaN(speed.Value)) { preferences=preferences with { Wpm=(uint)Math.Clamp(speed.Value,5,60) }; SavePreferences(); } };
        var frequency=new NumberBox { Header=T("frequency"),Minimum=200,Maximum=2000,Value=preferences.Frequency,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact };
        frequency.ValueChanged+=(_,_)=> { if(!double.IsNaN(frequency.Value)) { preferences=preferences with { Frequency=(uint)Math.Clamp(frequency.Value,200,2000) }; SavePreferences(); } };
        optionsRow.Children.Add(speed); optionsRow.Children.Add(frequency); stack.Children.Add(optionsRow);
        PageContent.Children.Add(Card(stack));
        BuildSpeech(async()=>
        {
            string value=audioSource; var options=preferences;
            return (await Task.Run(()=>MorseEngine.Decode(value,options.Alphabet,options.Separator))).Text;
        });
    }
    private void BuildAlphabet()
    {
        Heading(T("alphabet"),T("normalizationNote"));
        if(!nativeReady) { PageContent.Children.Add(Label(T("nativeUnavailable"))); return; }
        PageContent.Children.Add(AlphabetChoice());
        var grid=new Grid { RowSpacing=9,ColumnSpacing=12 };
        for(int i=0;i<3;i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        var entries=MorseEngine.Alphabet(preferences.Alphabet);
        for(int i=0;i<(entries.Count+2)/3;i++) grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        for(int i=0;i<entries.Count;i++)
        {
            var entry=entries[i]; var line=Row();
            var character=Label(entry.Character.ToString(),22); character.Width=34;
            line.Children.Add(character);
            line.Children.Add(new TextBlock { Text=entry.Code,FontFamily=new FontFamily("Consolas"),FontSize=17,VerticalAlignment=VerticalAlignment.Center,FlowDirection=FlowDirection.LeftToRight });
            var cell=Card(line); cell.Padding=new Thickness(12); Grid.SetRow(cell,i/3); Grid.SetColumn(cell,i%3); grid.Children.Add(cell);
        }
        PageContent.Children.Add(grid);
    }
    private PracticeQuestion? practiceQuestion;
    private string practiceAnswer="",practiceFeedback="";
    private int practiceMode;
    private void BuildPractice()
    {
        Heading(T("practice"),T("practiceNote"));
        var stack=new StackPanel { Spacing=18 };
        var mode=Choice(T("mode"),[T("letters"),T("digits"),T("wordPractice")],practiceMode,i=>practiceMode=i); stack.Children.Add(mode);
        var signal=Label(practiceQuestion?.Morse ?? (session?.Statistics.Answered==10 ? T("practiceEnded") : "· — · ·"),32); signal.FontFamily=new FontFamily("Consolas"); signal.FlowDirection=FlowDirection.LeftToRight;
        stack.Children.Add(signal);
        var answer=new TextBox { Header=T("answer"),Text=practiceAnswer,MaxLength=64,FlowDirection=FlowDirection.LeftToRight };
        answer.TextChanged+=(_,_)=>practiceAnswer=answer.Text; stack.Children.Add(answer);
        var feedback=Label(practiceFeedback,16); var counts=Label("",14,true);
        void Counts()
        {
            if(session is null) return;
            var stats=session.Statistics;
            counts.Text=$"{stats.Correct} {T("correct")} · {stats.Incorrect} {T("incorrect")} · {stats.Skipped} {T("skipped")} · {stats.Accuracy:F0}%";
        }
        Counts();
        void Next()
        {
            var stats=session!.Statistics;
            Counts();
            if(stats.Answered==stats.Total) { practiceQuestion=null; signal.Text=T("practiceEnded"); return; }
            practiceQuestion=session.Next(); signal.Text=practiceQuestion.Morse; answer.Text=""; answer.Focus(FocusState.Programmatic);
        }
        stack.Children.Add(Action("start",()=> { session?.Dispose(); session=new((PracticeMode)mode.SelectedIndex,10,(uint)Random.Shared.Next(1,int.MaxValue)); practiceFeedback=feedback.Text=""; Next(); return Task.CompletedTask; },primary:true));
        var controls=Row();
        controls.Children.Add(Action("submit",()=>
        {
            if(session is null || practiceQuestion is null) return Task.CompletedTask;
            var verdict=session.Submit(answer.Text); practiceFeedback=feedback.Text=$"{T(verdict.Verdict==PracticeVerdict.Correct?"correct":verdict.Verdict==PracticeVerdict.Skipped?"skipped":"incorrect")} · {verdict.ExpectedAnswer}"; Next(); return Task.CompletedTask;
        }));
        controls.Children.Add(Action("skip",()=> { if(session!=null && practiceQuestion!=null) { var verdict=session.Skip(); practiceFeedback=feedback.Text=$"{T("skipped")} · {verdict.ExpectedAnswer}"; Next(); } return Task.CompletedTask; }));
        controls.Children.Add(Action("play",()=>practiceQuestion!=null ? PlayMorse(signal.Text) : Task.CompletedTask));
        stack.Children.Add(controls); stack.Children.Add(feedback); stack.Children.Add(counts); PageContent.Children.Add(Card(stack));
    }
}
