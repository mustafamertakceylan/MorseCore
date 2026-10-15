using System.Net.Http;
using System.Text.Json;
using MorseCore.AppCore;
using Windows.System;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private bool checkingUpdates;
    private bool installingUpdate;
    private ContentDialog? updateDialog;
    private void BuildSettings()
    {
        Heading(T("settings"));
        var appearance=new StackPanel { Spacing=18 };
        var theme=Choice(T("theme"),[T("system"),T("light"),T("dark")],preferences.Theme switch { "light"=>1,"dark"=>2,_=>0 },i=>
        { preferences=preferences with { Theme=i==1?"light":i==2?"dark":"system" }; ApplyTheme(); SavePreferences(); Rebuild(); AnimateContent(); });
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(theme,"ThemeChoice");
        appearance.Children.Add(theme);
        appearance.Children.Add(Choice(T("language"),Localizer.LanguageNames,Array.IndexOf(Localizer.Languages,preferences.Language),i=>
        { StopPlayback(); preferences=preferences with { Language=Localizer.Languages[i] }; SavePreferences(); Rebuild(); AnimateContent(); }));
        var motion=new ToggleSwitch { Header=T("reduceMotion"),IsOn=preferences.ReduceMotion };
        motion.Toggled+=(_,_)=> { preferences=preferences with { ReduceMotion=motion.IsOn }; SavePreferences(); };
        appearance.Children.Add(motion); PageContent.Children.Add(Card(appearance));
        var conversions=new StackPanel { Spacing=18 };
        conversions.Children.Add(AlphabetChoice());
        conversions.Children.Add(Choice(T("separator"),["/","|","#","*"],"/|#*".IndexOf(preferences.Separator),i=>
        { preferences=preferences with { Separator="/|#*"[i] }; result=null; revision++; SavePreferences(); }));
        conversions.Children.Add(Label(T("outputFolder"),13,true)); conversions.Children.Add(Label(preferences.OutputDirectory,14));
        conversions.Children.Add(Action("chooseFolder",ChooseDirectory)); PageContent.Children.Add(Card(conversions));
        var voices=new StackPanel { Spacing=10 };
        voices.Children.Add(Label(T("speechPack"),22)); voices.Children.Add(Label(T("speechPackHelp"),13,true));
        voices.Children.Add(Label(OfflineSpeech.ResolveHome(preferences.SpeechHome),12,true));
        voices.Children.Add(Action("chooseSpeechPack",async()=>
        {
            var picker=new Windows.Storage.Pickers.FolderPicker(); picker.FileTypeFilter.Add("*"); InitializePicker(picker);
            var folder=await picker.PickSingleFolderAsync();
            if(folder!=null) { preferences=preferences with { SpeechHome=folder.Path }; SavePreferences(); BuildPage(); }
        },"\uE8B7")); PageContent.Children.Add(Card(voices));
        var updates=new StackPanel { Spacing=18 };
        updates.Children.Add(Label(T("updates"),23)); updates.Children.Add(Label($"{T("currentVersion")} {version}",14,true));
        var automatic=new ToggleSwitch { Header=T("updateNotice"),IsOn=preferences.CheckUpdates };
        automatic.Toggled+=(_,_)=> { preferences=preferences with { CheckUpdates=automatic.IsOn }; SavePreferences(); };
        updates.Children.Add(automatic); updates.Children.Add(Action("checkUpdates",()=>CheckUpdates(true)));
        if(update!=null)
        {
            updates.Children.Add(Label($"{T("updateAvailable")} · {update.Version}"));
            if(OperatingSystem.IsWindows() && update.Installer!=null && update.Checksums!=null)
            {
                var install=Action("installUpdate",InstallUpdate,"\uE896",true);
                install.IsEnabled=!installingUpdate; updates.Children.Add(install);
            }
            updates.Children.Add(Action("openRelease",async()=> { await Launcher.LaunchUriAsync(update.Page); }));
        }
        PageContent.Children.Add(Card(updates)); BuildCredits(); PageContent.Children.Add(Label(T("about"),12,true));
    }
    private async Task InstallUpdate()
    {
        if(installingUpdate || update is not { Installer:not null,Checksums:not null } release) return;
        await DownloadAndInstallUpdate(new UpdateService(http),release);
    }
    private async Task DownloadAndInstallUpdate(UpdateService updater,ReleaseUpdate release)
    {
        if(installingUpdate) return;
        installingUpdate=true;
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var progress=new ProgressBar { Minimum=0,Maximum=100,Value=0,Width=340,FlowDirection=FlowDirection.LeftToRight };
        var detail=Label(T("updateDownloading"),14,true); detail.TextWrapping=TextWrapping.Wrap;
        var content=new StackPanel { Spacing=16 }; content.Children.Add(detail); content.Children.Add(progress);
        var dialog=new ContentDialog { XamlRoot=XamlRoot,RequestedTheme=ActualTheme,FlowDirection=text.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,Title=$"{T("updateAvailable")} · {release.Version}",Content=content,
            PrimaryButtonText=T("updateInstallClose"),CloseButtonText=T("cancelUpdate"),IsPrimaryButtonEnabled=false,DefaultButton=ContentDialogButton.Close };
        dialog.Resources["ContentDialogBackground"]=Brush("CardBrush");
        dialog.Resources["ContentDialogForeground"]=Brush("InkBrush");
        dialog.Resources["ContentDialogTitleForeground"]=Brush("InkBrush");
        dialog.Closing+=(_,args)=> { if(args.Result!=ContentDialogResult.Primary) cancellation.Cancel(); };
        try
        {
            updateDialog=dialog;
            var shown=dialog.ShowAsync();
            string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MorseCore","Updates");
            using var prepared=await updater.DownloadAsync(release,cache,new Progress<double>(value=> { if(!disposed) progress.Value=value; }),cancellation.Token);
            if(disposed) return;
            progress.Value=100; detail.Text=T("updateConfirm"); dialog.IsPrimaryButtonEnabled=true;
            if(await shown==ContentDialogResult.Primary)
            {
                using var installer=prepared.StartInstaller(AppContext.BaseDirectory,preferences.Language);
                App.MainWindow.Close();
            }
        }
        catch(Exception e) when(e is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { if(!disposed) SetStatus(T(cancellation.IsCancellationRequested ? "cancelled" : "updateFailed")); }
        finally { dialog.Hide(); updateDialog=null; installingUpdate=false; }
    }
    private async Task CheckUpdates(bool manual)
    {
        if(checkingUpdates || (!manual && DateTimeOffset.UtcNow-preferences.LastUpdateCheck<TimeSpan.FromHours(6))) return;
        checkingUpdates=true; if(manual) SetStatus(T("checking"));
        try
        {
            var found=await new UpdateService(http).CheckAsync(Version.Parse(version),lifetime.Token);
            if(disposed) return;
            update=found; preferences=preferences with { LastUpdateCheck=DateTimeOffset.UtcNow }; SavePreferences();
            if(found!=null) { SetStatus($"{T("updateAvailable")} · {found.Version}"); if(page=="settings") BuildPage(); }
            else if(manual) SetStatus(T("upToDate"));
        }
        catch(Exception e) when(e is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or IOException or InvalidOperationException or FormatException)
        { if(!disposed && manual) SetStatus(T("updateUnavailable")); }
        finally { checkingUpdates=false; }
    }
}
