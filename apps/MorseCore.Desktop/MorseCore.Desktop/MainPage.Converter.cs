using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MorseCore.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private StackPanel? converterResultActions;
    private Button? converterSwap;
    private void BuildConverter()
    {
        Heading(T("converter"),T("converterHint"));
        var workspace=new StackPanel { Spacing=16 };
        var toolbar=new Grid { ColumnSpacing=14,RowSpacing=10 };
        toolbar.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); toolbar.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        var direction=Row();
        direction.Children.Add(Action("textToMorse",()=>ChangeDirection(false),primary:!decode));
        direction.Children.Add(Action("morseToText",()=>ChangeDirection(true),primary:decode));
        direction.VerticalAlignment=VerticalAlignment.Bottom; toolbar.Children.Add(direction);
        var alphabet=AlphabetChoice(); alphabet.MinWidth=180; Grid.SetColumn(alphabet,1); toolbar.Children.Add(alphabet);
        toolbar.SizeChanged+=(_,e)=>
        {
            bool narrow=e.NewSize.Width<570;
            Grid.SetRow(alphabet,narrow?1:0); Grid.SetColumn(alphabet,narrow?0:1);
            Grid.SetColumnSpan(alphabet,narrow?2:1); Grid.SetColumnSpan(direction,narrow?2:1);
        };
        workspace.Children.Add(toolbar);
        var editors=new Grid { ColumnSpacing=14,RowSpacing=12 };
        editors.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        editors.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        editors.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); editors.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        var input=new StackPanel { Spacing=8 };
        var output=new StackPanel { Spacing=8 };
        var sourceHeader=new Grid { MinHeight=34 };
        sourceHeader.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) }); sourceHeader.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); sourceHeader.Children.Add(Label(T(decode?"morseInput":"plainText"),13,true));
        var clear=IconAction("clear",()=> { source=""; result=null; revision++; BuildPage(); return Task.CompletedTask; },"\uE894"); Grid.SetColumn(clear,1); sourceHeader.Children.Add(clear); input.Children.Add(sourceHeader);
        var resultHeader=new Grid { MinHeight=34 };
        resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) }); resultHeader.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); resultHeader.Children.Add(Label(T(decode?"decodedText":"morseInput"),13,true));
        var resultActions=Row(); resultActions.Spacing=4; Grid.SetColumn(resultActions,1);
        resultActions.Children.Add(Action("copy",CopyResult,"\uE8C8")); resultActions.Children.Add(Action("save",SaveResult,"\uE74E")); output.Children.Add(resultHeader);
        converterResultActions=resultActions;
        inputBox=new TextBox { Text=source,PlaceholderText=decode ? "... --- ..." : T("inputPlaceholder"),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=200,MaxLength=200_000,Padding=new Thickness(12),FontSize=17,FlowDirection=decode ? FlowDirection.LeftToRight : PlainDirection };
        outputBox=new TextBox { Text=result?.Text ?? "",PlaceholderText=T("outputPlaceholder"),AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=200,IsReadOnly=true,Padding=new Thickness(12),FontSize=17,FlowDirection=decode ? PlainDirection : FlowDirection.LeftToRight };
        outputBox.Background=Brush("ResultBrush");
        if(decode) inputBox.FontFamily=MonoFont; else outputBox.FontFamily=MonoFont;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(inputBox,T("source"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(outputBox,T("result"));
        var editor=inputBox;
        editor.TextChanged+=(_,_)=>
        {
            // Uno can deliver the initial TextChanged after the page is laid out.
            // Rebuilding or a late event from an old editor must preserve a valid result.
            if(!ReferenceEquals(inputBox,editor) || source==editor.Text) return;
            bool hadResult=result!=null; source=editor.Text; result=null; revision++;
            if(outputBox!=null) outputBox.Text=""; UpdateStatistics(); if(hadResult) SetStatus(T("ready"));
        };
        var shortcut=new KeyboardAccelerator { Key=VirtualKey.Enter,Modifiers=VirtualKeyModifiers.Control };
        shortcut.Invoked+=async(_,e)=> { e.Handled=true; await Guard(ConvertNow); }; inputBox.KeyboardAccelerators.Add(shortcut);
        input.Children.Add(inputBox); output.Children.Add(outputBox); output.Children.Add(resultActions); WrapActions(resultActions,output);
        Grid.SetColumn(output,1); editors.Children.Add(input); editors.Children.Add(output);
        editors.SizeChanged+=(_,e)=>
        {
            bool narrow=e.NewSize.Width<530;
            Grid.SetColumn(output,narrow?0:1); Grid.SetRow(output,narrow?1:0);
            Grid.SetColumnSpan(input,narrow?2:1); Grid.SetColumnSpan(output,narrow?2:1);
        };
        workspace.Children.Add(editors);
        var controls=Row();
        controls.Children.Add(Action("convert",ConvertNow,"\uE8D4",true));
        converterSwap=Action("swap",()=>
        {
            if(result==null) return Task.CompletedTask;
            source=result.Text; result=null; decode=!decode; revision++; BuildPage(); return Task.CompletedTask;
        },"\uE8AB");
        controls.Children.Add(converterSwap);
        resultActions.Children.Add(Action("uxOpenAudio",()=>
        {
            if(result==null) return Task.CompletedTask;
            audioSource=result.Text; audioIsMorse=!decode; audioWorkspace=0; Navigate("audio"); return Task.CompletedTask;
        },"\uE8D6"));
        workspace.Children.Add(controls); WrapActions(controls,workspace);
        statistics=Label("",12,true); workspace.Children.Add(statistics); UpdateStatistics();
        workspace.Children.Add(WorkspaceFeedback());
        PageContent.Children.Add(Card(workspace));
        var note=new Expander { Header=T("conversionNotes"),HorizontalAlignment=HorizontalAlignment.Stretch,Content=Label(T("normalizationNote"),12,true) };
        PageContent.Children.Add(note);
    }
    private Task ChangeDirection(bool value)
    {
        if(decode==value) return Task.CompletedTask;
        decode=value; result=null; revision++; BuildPage(); return Task.CompletedTask;
    }
    private async Task ConvertNow()
    {
        if(working) return;
        working=true; int generation=revision;
        string input=source; var options=preferences; bool direction=decode;
        SetStatus(T("busy"));
        try
        {
            var converted=await Task.Run(()=>direction ? MorseEngine.Decode(input,options.Alphabet,options.Separator) : MorseEngine.Encode(input,options.Alphabet,options.Separator));
            if(disposed) return;
            if(revision!=generation) { if(page=="converter") SetStatus(T("ready")); return; }
            result=converted;
            if(outputBox!=null) outputBox.Text=result.Text;
            UpdateStatistics(); SetStatus(T("ready"));
        }
        finally { working=false; }
    }
    private void UpdateStatistics()
    {
        if(converterResultActions!=null) converterResultActions.Visibility=result==null?Visibility.Collapsed:Visibility.Visible;
        if(converterSwap!=null) converterSwap.Visibility=result==null?Visibility.Collapsed:Visibility.Visible;
        if(statistics==null) return;
        if(result is null) { statistics.Text="Ctrl + Enter"; return; }
        statistics.Text=StatisticsText();
    }
    private string StatisticsText()
    {
        if(result is null) return T("ready");
        var s=result.Statistics;
        return $"{T("characters")}: {s.InputCharacters:N0}  ·  {T("words")}: {s.Words:N0}  ·  {T("converted")}: {s.ConvertedCharacters:N0}";
    }
    private Task CopyResult()
    {
        if(result is null) return Task.CompletedTask;
        var package=new DataPackage(); package.SetText(result.Text); Clipboard.SetContent(package); SetStatus(T("copied")); return Task.CompletedTask;
    }
    private async Task SaveResult()
    {
        string? snapshot=result?.Text; if(snapshot is null) return;
        string? path=await SavePath("txt",decode ? "text" : "morse");
        if(path!=null) { await File.WriteAllTextAsync(path,snapshot,lifetime.Token); SetStatus(T("saved")); }
    }
    private Task PlayMorse(string morse,char? separator=null)=>RunPlayback(()=>Task.FromResult(morse),separator is { } value ? preferences with { Separator=value } : preferences);
    private async Task RunPlayback(Func<Task<string>> getMorse,MorseCore.AppCore.Preferences options)
    {
        if(playback!=null) return;
        speech?.Stop();
        using var token=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); playback=token;
        UpdateAudioActions();
        try
        {
            string morse=await getMorse(); token.Token.ThrowIfCancellationRequested();
            SetStatus(T("play"));
            if(OperatingSystem.IsLinux()) await LinuxAudio.PlayMorseAsync(morse,new(options.Wpm,options.Frequency),options.Separator,token.Token);
            else await MorseAudio.PlayAsync(morse,new(options.Wpm,options.Frequency),options.Separator,token.Token);
            SetStatus(T("ready"));
        }
        finally { if(ReferenceEquals(playback,token)) playback=null; UpdateAudioActions(); }
    }
}
