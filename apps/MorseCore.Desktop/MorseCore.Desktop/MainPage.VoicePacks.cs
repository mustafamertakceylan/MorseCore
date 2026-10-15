using MorseCore.AppCore;
using MorseCore.AppCore.SpeechPackages;
using System.Net.Http;
using Windows.System;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private CancellationTokenSource? voicePackRequest,voicePackProbe;
    private TaskCompletionSource? voicePackFinished;
    private int voicePackGeneration;
    private string? voicePackageFeedback;

    private void CleanupVoicePacks()
    {
        voicePackGeneration++;
        voicePackProbe?.Cancel(); voicePackRequest?.Cancel();
    }

    private void BuildVoicePacksPage()
    {
        int generation=voicePackGeneration;
        if(voicePackRequest!=null && voicePackFinished is { } pending)
        {
            PageContent.Children.Add(Label(T("voicesStopping"),13,true));
            _=WaitForPrevious();
            async Task WaitForPrevious() { await pending.Task; if(!disposed && generation==voicePackGeneration) BuildPage(); }
            return;
        }
        string home;
        VoicePackageManager manager;
        try { home=OfflineSpeech.ResolveHome(preferences.SpeechHome); manager=new VoicePackageManager(home,http); }
        catch(Exception error) when(error is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException)
        {
            var recovery=new StackPanel { Spacing=12 };
            recovery.Children.Add(Label(T("voicesLocationInvalid"),14,true));
            recovery.Children.Add(Action("voicesOpenSettings",()=> { Navigate("settings"); return Task.CompletedTask; },"\uE713"));
            PageContent.Children.Add(Card(recovery));
            return;
        }
        var introduction=new StackPanel { Spacing=10 };
        introduction.Children.Add(Label(T("voicesTitle"),20));
        introduction.Children.Add(Label(T("voicesIntro"),13,true));
        introduction.Children.Add(Label(T("voicesDependencySize"),12,true));
        var location=Label(home,12,true); location.FlowDirection=FlowDirection.LeftToRight;
        introduction.Children.Add(location);
        string[] cultures=["tr-TR","en-US","de-DE","ru-RU","ar-SA"];
        var language=Choice(T("speechLanguage"),Localizer.LanguageNames,Array.IndexOf(cultures,preferences.SpeechLanguage),index=>
        {
            preferences=preferences with { SpeechLanguage=cultures[index] }; SavePreferences(); BuildPage();
        });
        introduction.Children.Add(language); PageContent.Children.Add(Card(introduction));

        var runtime=new StackPanel { Spacing=10 };
        var runtimeState=Label(T("voicesCheckingRuntime"),13,true); runtime.Children.Add(runtimeState);
        PageContent.Children.Add(Card(runtime));

        var progressPanel=new StackPanel { Spacing=8,Visibility=Visibility.Collapsed };
        var progressText=Label("",13,true);
        var progressBar=new ProgressBar { Minimum=0,Maximum=100,IsIndeterminate=true };
        var cancel=Action("voicesCancel",()=> { voicePackRequest?.Cancel(); return Task.CompletedTask; },"\uE711");
        progressPanel.Children.Add(progressText); progressPanel.Children.Add(progressBar); progressPanel.Children.Add(cancel);
        PageContent.Children.Add(progressPanel);
        var feedback=Label(voicePackageFeedback ?? "",13,true); PageContent.Children.Add(feedback);
        var interactive=new List<Control> { language };
        var refreshers=new List<Action>();
        bool runtimeReady=false;
        // All captured controls are assigned before the callback is created. The callback
        // executes only after the button has been added to the page and clicked.
        Button runtimeInstall=null!;
        runtimeInstall=Action("voicesInstallRuntime",()=>InstallRuntime(),"\uE896");
        runtimeInstall.Visibility=OperatingSystem.IsWindows()?Visibility.Visible:Visibility.Collapsed;
        runtimeInstall.IsEnabled=false; runtime.Children.Add(runtimeInstall); interactive.Add(runtimeInstall);
        bool Current()=>!disposed && generation==voicePackGeneration;
        void Refresh()
        {
            if(!Current()) return;
            foreach(var refresh in refreshers) refresh();
        }
        void Report(VoicePackageProgress value)
        {
            if(!Current()) return;
            progressText.Text=T("voicesPhase_"+value.Phase);
            progressBar.IsIndeterminate=value.TotalBytes<=0;
            if(value.TotalBytes>0) progressBar.Value=Math.Clamp(100d*value.CompletedBytes/value.TotalBytes,0,100);
            cancel.IsEnabled=value.Phase!="runtimeInstall";
        }
        async Task<bool> Confirm(string title,string content,string button)
        {
            var dialog=new ContentDialog { XamlRoot=XamlRoot,RequestedTheme=ActualTheme,FlowDirection=text.IsRtl?FlowDirection.RightToLeft:FlowDirection.LeftToRight,
                Title=title,Content=new TextBlock { Text=content,TextWrapping=TextWrapping.Wrap,MaxWidth=460 },PrimaryButtonText=button,CloseButtonText=T("voicesCancel"),DefaultButton=ContentDialogButton.Close };
            dialog.Resources["ContentDialogBackground"]=Brush("CardBrush");
            dialog.Resources["ContentDialogForeground"]=Brush("InkBrush");
            return await dialog.ShowAsync()==ContentDialogResult.Primary;
        }
        async Task Operate(Func<VoicePackageManager,IProgress<VoicePackageProgress>,CancellationToken,Task> operation,string success)
        {
            if(voicePackRequest!=null || !Current()) return;
            StopPlayback();
            using var request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); voicePackRequest=request;
            var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); voicePackFinished=finished;
            foreach(var control in interactive) control.IsEnabled=false;
            progressPanel.Visibility=Visibility.Visible; cancel.IsEnabled=true; feedback.Text="";
            var progress=new Progress<VoicePackageProgress>(value=> { if(Current()) Report(value); });
            try
            {
                await operation(manager,progress,request.Token);
                voicePackageFeedback=T(success);
                if(Current()) feedback.Text=voicePackageFeedback;
            }
            catch(OperationCanceledException) { voicePackageFeedback=T(request.IsCancellationRequested?"voicesCancelled":"voicesFailed"); if(Current()) feedback.Text=voicePackageFeedback; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or HttpRequestException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
            { voicePackageFeedback=T("voicesFailed"); if(Current()) feedback.Text=voicePackageFeedback; }
            finally
            {
                if(ReferenceEquals(voicePackRequest,request)) voicePackRequest=null;
                if(ReferenceEquals(voicePackFinished,finished)) voicePackFinished=null;
                finished.TrySetResult();
                if(Current())
                {
                    progressPanel.Visibility=Visibility.Collapsed;
                    foreach(var control in interactive) control.IsEnabled=true;
                    Refresh(); runtimeInstall.IsEnabled=!runtimeReady;
                }
            }
        }
        async Task InstallRuntime()
        {
            if(!await Confirm(T("voicesInstallRuntime"),T("voicesRuntimeConsent"),T("voicesInstall"))) return;
            await Operate(async(service,progress,token)=> { await service.InstallPythonAsync(progress,token); runtimeReady=true; if(Current()) { runtimeState.Text=T("voicesRuntimeReady"); runtimeInstall.Visibility=Visibility.Collapsed; } },"voicesRuntimeReady");
        }

        foreach(var package in VoicePackageCatalog.Packages.Where(p=>p.Voice.Language==preferences.SpeechLanguage))
        {
            var voice=package.Voice;
            var panel=new StackPanel { Spacing=10 };
            panel.Children.Add(Label(voice.Name,18));
            panel.Children.Add(Label($"{T("voicesModelSize")} · {package.DownloadBytes/(1024d*1024):0.0} MiB",13,true));
            panel.Children.Add(Label(T(voice.Engine=="piper"?"voicesLicensePiper":"voicesLicenseApache"),12,true));
            if(voice.Language=="ar-SA") panel.Children.Add(Label(T("voicesArabicLicense"),12,true));
            var state=Label("",13,true); panel.Children.Add(state);
            var actions=Row();
            var install=Action("voicesInstall",async()=>
            {
                string consent=T("voicesInstallConsent")+"\n\n"+voice.Name+" · "+(package.DownloadBytes/(1024d*1024)).ToString("0.0")+" MiB\n"+T("voicesDependencySize");
                if(voice.Language=="ar-SA") consent+="\n\n"+T("voicesArabicLicense");
                if(!await Confirm(T("voicesInstall"),consent,T("voicesInstall"))) return;
                await Operate((service,progress,token)=>service.InstallAsync(voice.Id,progress,token),"voicesInstalled");
            },"\uE896",true);
            var choose=Action("voicesChoose",()=> { preferences=preferences with { VoiceId=voice.Id,SpeechLanguage=voice.Language }; SavePreferences(); Refresh(); return Task.CompletedTask; },"\uE73E");
            var preview=Action("voicesPreview",async()=>
            {
                if(voicePackRequest!=null) return;
                await Operate(async(_,progress,token)=>
                {
                    progress.Report(new("preview")); speech ??=new SpeechService();
                    await speech.SpeakAsync(VoiceSample(voice.Language),voice.Id,home,token,muted:SmokeDirectory!=null);
                },"voicesPreviewDone");
            },"\uE768");
            var remove=Action("voicesRemove",async()=>
            {
                if(!await Confirm(T("voicesRemove"),voice.Name+"\n\n"+T("voicesRemoveConsent"),T("voicesRemove"))) return;
                await Operate(async(service,progress,token)=> { progress.Report(new("remove")); await service.RemoveAsync(voice.Id,token); },"voicesRemoved");
            },"\uE74D");
            var source=Action("voicesSource",async()=> { await Launcher.LaunchUriAsync(package.Source); },"\uE8A7");
            foreach(var button in new[]{install,choose,preview,remove,source}) { actions.Children.Add(button); interactive.Add(button); }
            panel.Children.Add(actions); WrapActions(actions,panel);
            PageContent.Children.Add(Card(panel));
            refreshers.Add(()=>
            {
                bool installed=OfflineSpeech.IsInstalled(home,voice);
                bool selected=preferences.VoiceId==voice.Id;
                state.Text=T(installed ? selected?"voicesSelected":"voicesInstalled":"voicesNotInstalled");
                install.IsEnabled=runtimeReady; choose.IsEnabled=installed && !selected;
                preview.IsEnabled=installed && SpeechService.IsSupported;
                // Partial, cancelled downloads may leave valid completed files; allow their cleanup.
                remove.IsEnabled=package.Files.Any(file=>File.Exists(Path.Combine(home,file.Path.Replace('/',Path.DirectorySeparatorChar))));
            });
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(panel,"VoicePack-"+voice.Id);
        }
        Refresh();
        _=ProbeRuntime();
        async Task ProbeRuntime()
        {
            using var request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); voicePackProbe=request;
            try
            {
                runtimeReady=await manager.FindPythonAsync(request.Token)!=null;
                if(!Current()) return;
                runtimeState.Text=T(runtimeReady?"voicesRuntimeReady":"voicesRuntimeMissing");
                runtimeInstall.Visibility=runtimeReady || !OperatingSystem.IsWindows()?Visibility.Collapsed:Visibility.Visible;
                runtimeInstall.IsEnabled=!runtimeReady;
                Refresh();
            }
            catch(OperationCanceledException) { }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            { if(Current()) runtimeState.Text=T("voicesFailed"); }
            finally { if(ReferenceEquals(voicePackProbe,request)) voicePackProbe=null; }
        }
    }

    private static string VoiceSample(string language)=>language switch
    {
        "tr-TR"=>"Merhaba. Bu ses cihazınızda, internet olmadan üretiliyor.",
        "de-DE"=>"Willkommen bei Morse Core. Diese Stimme wird auf Ihrem Gerät erzeugt.",
        "ru-RU"=>"Добро пожаловать в Морзе. Этот голос создаётся на вашем устройстве.",
        "ar-SA"=>"مرحبا بكم في مورس. يتم إنشاء هذا الصوت على جهازك دون اتصال بالإنترنت.",
        _=>"Welcome to Morse Core. This voice is generated offline on your device."
    };
}
