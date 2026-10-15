using System.Net.Http;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using MorseCore.AppCore;
using MorseCore.Interop;
using Windows.Storage.Pickers;

namespace MorseCore.Desktop;

public sealed partial class MainPage : Page
{
    private readonly PreferencesStore store;
    private Preferences preferences;
    private Localizer text;
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource? playback;
    private readonly HttpClient http=new();
    private readonly List<Button> menuButtons=[];
    private string page="converter", source="", version="0.4.1";
    private ConversionResult? result;
    private bool decode, working, initialized, disposed, nativeReady;
    private int revision;
    private TextBox? inputBox, outputBox;
    private TextBlock? statistics;
    private PracticeSession? session;
    private SpeechService? speech;
    private ReleaseUpdate? update;
    private Task? updateTask;
    private Storyboard? paneAnimation, contentAnimation;
    private string T(string key)=>text[key];
    private FlowDirection PlainDirection=>preferences.Alphabet==MorseAlphabet.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    private static string? SmokeDirectory
    {
        get
        {
#if DEBUG
            return Environment.GetEnvironmentVariable("MORSECORE_DESKTOP_SMOKE");
#else
            return null;
#endif
        }
    }
    public MainPage()
    {
        string? home=Environment.GetEnvironmentVariable("MORSECORE_DESKTOP_HOME");
        store=new(SmokeDirectory is { } directory ? Path.Combine(directory,"preferences.json") : !string.IsNullOrEmpty(home) && Path.IsPathFullyQualified(home) ? Path.Combine(home,"settings.json") : PreferencesStore.DefaultPath);
        preferences=store.Load(); text=new(preferences.Language);
        InitializeComponent(); InitializeChrome(); ApplyTheme();
        LoadingTitle.Text=T("loading"); LoadingDetail.Text=T("loadingSettings");
        Loaded+=Initialize;
        ActualThemeChanged+=(_,_)=> { if(initialized && !disposed && preferences.Theme=="system") DispatcherQueue.TryEnqueue(Rebuild); };
        SizeChanged+=(_,e)=> { if(initialized && e.NewSize.Width<850 && !preferences.Collapsed) SetCollapsed(true,false); };
    }
    private async void Initialize(object sender,RoutedEventArgs e)
    {
        if(initialized) return;
        initialized=true;
        LoadingProgress.Value=35; LoadingDetail.Text=T("loadingEngine");
        await FirstSplashFrame();
        if(disposed) return;
#if DEBUG
        if(SmokeDirectory is { } captureDirectory) await Snapshot(Path.Combine(captureDirectory,"loading.png"));
#endif
        if(preferences.CheckUpdates && SmokeDirectory is null)
        {
            LoadingDetail.Text=T("loadingEngine")+"\n"+T("checking");
            updateTask=CheckUpdates(false);
        }
        bool ready=true;
        try { version=await Task.Run(()=> { string nativeVersion=MorseEngine.NativeVersion; _=MorseEngine.Alphabet(MorseAlphabet.Latin); return nativeVersion; }); }
        catch(Exception ex) when(ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
        { ready=false; }
        if(disposed) return;
        nativeReady=ready;
        LoadingProgress.Value=85; Rebuild(); LoadingProgress.Value=100;
        Shell.Visibility=Visibility.Visible;
        SetStatus(ready ? store.LoadWarning is null ? T("ready") : T("settingsRecovered") : T("nativeUnavailable"));
        if(update!=null && ready) SetStatus($"{T("updateAvailable")} · {update.Version}");
        await DismissSplash();
        if(disposed) return;
        AnimateContent();
#if DEBUG
        if(SmokeDirectory is { } directory) { await RunUiSmoke(directory); return; }
#endif
    }
    private async Task FirstSplashFrame()
    {
#if MORSE_WINUI
        var drawn=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<object> handler=(_,_)=>drawn.TrySetResult();
        CompositionTarget.Rendering+=handler;
        try { await Task.WhenAny(drawn.Task,Task.Delay(150)); }
        finally { CompositionTarget.Rendering-=handler; }
#else
        await Task.Yield();
#endif
    }
    private async Task DismissSplash()
    {
        if(MotionAllowed())
        {
            var storyboard=new Storyboard();
            var fade=new DoubleAnimation { From=1,To=0,Duration=new Duration(TimeSpan.FromMilliseconds(150)) };
            Storyboard.SetTarget(fade,Splash); Storyboard.SetTargetProperty(fade,"Opacity"); storyboard.Children.Add(fade);
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            storyboard.Completed+=(_,_)=>done.TrySetResult(); storyboard.Begin();
            await Task.WhenAny(done.Task,Task.Delay(500)); storyboard.Stop();
        }
        Splash.Visibility=Visibility.Collapsed; Splash.Opacity=1;
    }
    private void ApplyTheme()=>RequestedTheme=preferences.Theme switch { "light"=>ElementTheme.Light,"dark"=>ElementTheme.Dark,_=>ElementTheme.Default };
    private void Rebuild()
    {
        bool languageChanged=text.Language!=preferences.Language;
        text=new(preferences.Language);
        Shell.FlowDirection=text.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ApplyTheme(); Tagline.Text=T("tagline"); OfflineLabel.Text=T("offline"); VersionLabel.Text=$"MORSECORE · {version}";
        ToolTipService.SetToolTip(CloseWindowButton,T("closeApp"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CloseWindowButton,T("closeApp"));
        Menu.Children.Clear(); BottomMenu.Children.Clear(); menuButtons.Clear();
        AddMenu("converter","\uE8D4"); AddMenu("files","\uE8B7"); AddMenu("audio","\uE8D6"); AddMenu("practice","\uE7BE"); AddMenu("alphabet","\uE8D2"); AddMenu("settings","\uE713",true);
        SetCollapsed(preferences.Collapsed,false); BuildPage();
        if(languageChanged) SetStatus(T(working || batchWorking ? "busy" : "ready"));
    }
    private void AddMenu(string key,string glyph,bool bottom=false)
    {
        var panel=new StackPanel { Orientation=Orientation.Horizontal,Spacing=11 };
        panel.Children.Add(new FontIcon { Glyph=glyph,FontSize=18,Width=20 });
        panel.Children.Add(Label(T(key)));
        var ink=Brush(key==page ? "NavSelectedInkBrush" : "InkBrush");
        ((FontIcon)panel.Children[0]).Foreground=ink; ((TextBlock)panel.Children[1]).Foreground=ink;
        var button=new Button { Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(12),BorderThickness=new Thickness(0),Foreground=ink,Background=Brush(key==page ? "NavSelectedBrush" : "PaneBrush"),Tag=key };
        button.Resources["ButtonBackgroundPointerOver"]=Brush(key==page ? "NavSelectedBrush" : "CanvasBrush");
        button.Resources["ButtonBackgroundPressed"]=Brush(key==page ? "NavSelectedBrush" : "CanvasBrush");
        ToolTipService.SetToolTip(button,T(key));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(button,"Nav-"+key);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,T(key));
        button.Click+=(_,_)=>Navigate(key);
        (bottom ? BottomMenu : Menu).Children.Add(button); menuButtons.Add(button);
    }
    private SolidColorBrush Brush(string key)
    {
        string theme=ActualTheme==ElementTheme.Dark ? "Default" : "Light";
        return (SolidColorBrush)((ResourceDictionary)Application.Current.Resources.ThemeDictionaries[theme])[key];
    }
    private void Navigate(string key)
    {
        if(page==key) return;
        StopPlayback(); revision++;
        if(key=="audio" && string.IsNullOrWhiteSpace(audioSource)) audioSource=decode ? source : result?.Text ?? "";
        page=key; Rebuild(); ContentScroll.ChangeView(null,0,null,true); AnimateContent();
    }
    private void BuildPage()
    {
        inputBox=null; outputBox=null; statistics=null;
        PageContent.Children.Clear(); Breadcrumb.Text=T(page);
        switch(page)
        {
            case "converter": BuildConverter(); break;
            case "files": BuildFiles(); break;
            case "audio": BuildAudio(); break;
            case "practice": BuildPractice(); break;
            case "alphabet": BuildAlphabet(); break;
            case "settings": BuildSettings(); break;
        }
    }
    private void SetStatus(string value) { if(!disposed) Status.Text=value; }
    private async Task Guard(Func<Task> operation)
    {
        try { await operation(); }
        catch(OperationCanceledException) { SetStatus(T("cancelled")); }
        catch(MorseNativeException e) { SetStatus(e.Status switch { -1=>T("missingInput"),-3=>T("unsupported"),-4=>T("invalidMorse"),_=>T("error") }); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or DllNotFoundException or EntryPointNotFoundException or TypeInitializationException)
        { SetStatus(T("error")); }
    }
    private void SavePreferences()
    {
        try { store.Save(preferences); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException) { SetStatus(T("saveError")); }
    }
    private void TogglePane(object sender,RoutedEventArgs e)=>SetCollapsed(!preferences.Collapsed,true);
    private void SetCollapsed(bool collapsed,bool animate)
    {
        paneAnimation?.Stop(); paneAnimation=null;
        preferences=preferences with { Collapsed=collapsed };
        Brand.Visibility=Tagline.Visibility=VersionLabel.Visibility=collapsed ? Visibility.Collapsed : Visibility.Visible;
        foreach(var button in menuButtons)
        {
            ((StackPanel)button.Content).Children[1].Visibility=collapsed ? Visibility.Collapsed : Visibility.Visible;
            button.HorizontalAlignment=collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment=collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            button.Width=collapsed ? 44 : double.NaN;
            button.Height=44;
            button.Padding=collapsed ? new Thickness(0) : new Thickness(12,0,12,0);
        }
        Grid.SetColumn(PaneToggle,collapsed ? 0 : 1); Grid.SetColumnSpan(PaneToggle,collapsed ? 2 : 1);
        PaneToggle.HorizontalAlignment=collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        ToolTipService.SetToolTip(PaneToggle,T(collapsed ? "openPane" : "closePane"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PaneToggle,T(collapsed ? "openPane" : "closePane"));
        double width=collapsed ? 72 : 216;
        if(animate && MotionAllowed())
        {
            var storyboard=new Storyboard();
            var animation=new DoubleAnimation { From=Pane.ActualWidth,To=width,Duration=new Duration(TimeSpan.FromMilliseconds(170)),EnableDependentAnimation=true,EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
            Storyboard.SetTarget(animation,Pane); Storyboard.SetTargetProperty(animation,"Width"); storyboard.Children.Add(animation);
            storyboard.Completed+=(_,_)=> { storyboard.Stop(); Pane.Width=width; if(ReferenceEquals(paneAnimation,storyboard)) paneAnimation=null; };
            paneAnimation=storyboard; storyboard.Begin();
        }
        else Pane.Width=width;
        if(animate) SavePreferences();
    }
    private void AnimateContent()
    {
        contentAnimation?.Stop(); contentAnimation=null;
        if(!MotionAllowed()) { PageContent.Opacity=1; ((TranslateTransform)PageContent.RenderTransform).Y=0; return; }
        var storyboard=new Storyboard();
        var fade=new DoubleAnimation { From=0,To=1,Duration=new Duration(TimeSpan.FromMilliseconds(170)) };
        Storyboard.SetTarget(fade,PageContent); Storyboard.SetTargetProperty(fade,"Opacity"); storyboard.Children.Add(fade);
        var slide=new DoubleAnimation { From=9,To=0,Duration=new Duration(TimeSpan.FromMilliseconds(190)),EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
        Storyboard.SetTarget(slide,PageContent.RenderTransform); Storyboard.SetTargetProperty(slide,"Y"); storyboard.Children.Add(slide);
        storyboard.Completed+=(_,_)=> { storyboard.Stop(); PageContent.Opacity=1; ((TranslateTransform)PageContent.RenderTransform).Y=0; if(ReferenceEquals(contentAnimation,storyboard)) contentAnimation=null; };
        contentAnimation=storyboard; storyboard.Begin();
    }
    private TextBlock Label(string value,double size=15,bool muted=false)=>new() { Text=value,FontSize=size,Foreground=Brush(muted ? "MutedBrush" : "InkBrush"),VerticalAlignment=VerticalAlignment.Center };
    private bool MotionAllowed()
    {
        if(preferences.ReduceMotion) return false;
#if MORSE_WINUI
        return new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#else
        return true;
#endif
    }
    private void Heading(string title,string? subtitle=null)
    {
        Breadcrumb.Text=title;
        if(subtitle!=null) PageContent.Children.Add(Label(subtitle,13,true));
    }
    private Border Card(UIElement child)=>new() { Child=child,Background=Brush("CardBrush"),BorderBrush=Brush("LineBrush"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Padding=new Thickness(16) };
    private StackPanel Row()=>new() { Orientation=Orientation.Horizontal,Spacing=10 };
    private void WrapActions(StackPanel row,FrameworkElement host)
    {
        void Adapt()
        {
            if(host.ActualWidth<=0) return;
            double width=row.Children.Cast<FrameworkElement>().Sum(child=>child.DesiredSize.Width)+row.Spacing*Math.Max(0,row.Children.Count-1);
            row.Orientation=width>host.ActualWidth+1 ? Orientation.Vertical : Orientation.Horizontal;
        }
        bool attached=false;
        SizeChangedEventHandler resize=(_,_)=>Adapt();
        row.Loaded+=(_,_)=> { if(!attached) { host.SizeChanged+=resize; attached=true; } Adapt(); };
        row.Unloaded+=(_,_)=> { if(attached) { host.SizeChanged-=resize; attached=false; } };
    }
    private Button Action(string key,Func<Task> action,string? glyph=null,bool primary=false)
    {
        var row=Row(); if(glyph!=null) row.Children.Add(new FontIcon { Glyph=glyph,FontSize=16 });
        row.Children.Add(Label(T(key)));
        var button=new Button { Content=row };
        if(primary)
        {
            bool dark=ActualTheme==ElementTheme.Dark;
            button.Background=Brush(dark ? "NavSelectedBrush" : "AccentBrush");
            button.Foreground=dark ? Brush("NavSelectedInkBrush") : new SolidColorBrush(Microsoft.UI.Colors.White);
            ((TextBlock)row.Children.Last()).Foreground=button.Foreground;
            button.Resources["ButtonBackgroundPointerOver"]=button.Background;
            button.Resources["ButtonBackgroundPressed"]=button.Background;
            button.Resources["ButtonForegroundPointerOver"]=button.Foreground;
            button.Resources["ButtonForegroundPressed"]=button.Foreground;
        }
        button.Click+=async(_,_)=>await Guard(action);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(button,key);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,T(key)); return button;
    }
    private Button IconAction(string key,Func<Task> action,string glyph)
    {
        var button=Action(key,action);
        button.Content=new FontIcon { Glyph=glyph,FontSize=16 }; button.Padding=new Thickness(9); button.MinWidth=34; button.MinHeight=34;
        button.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent); button.BorderThickness=new Thickness(0);
        ToolTipService.SetToolTip(button,T(key)); return button;
    }
    private ComboBox Choice(string label,IEnumerable<string> options,int selected,Action<int> changed)
    {
        var choice=new ComboBox { Header=label,ItemsSource=options.ToArray(),SelectedIndex=selected,HorizontalAlignment=HorizontalAlignment.Stretch };
        choice.SelectionChanged+=(_,_)=> { if(choice.SelectedIndex>=0) changed(choice.SelectedIndex); }; return choice;
    }
    private ComboBox AlphabetChoice()=>Choice(T("conversionAlphabet"),[T("latin"),T("russian"),T("arabic")],(int)preferences.Alphabet,index=>
    { preferences=preferences with { Alphabet=(MorseAlphabet)index }; result=null; revision++; SavePreferences(); BuildPage(); });
    private void InitializePicker(object picker)
    {
#if MORSE_WINUI
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
#endif
    }
    private async Task<string?> SavePath(string extension,string suggested)
    {
        var picker=new FileSavePicker { SuggestedFileName=suggested }; picker.FileTypeChoices.Add(extension.ToUpperInvariant(),["."+extension]); InitializePicker(picker);
        var file=await picker.PickSaveFileAsync(); return file?.Path;
    }
    private async Task ChooseDirectory()
    {
        var picker=new FolderPicker(); picker.FileTypeFilter.Add("*"); InitializePicker(picker);
        var folder=await picker.PickSingleFolderAsync();
        if(folder!=null) { preferences=preferences with { OutputDirectory=folder.Path }; SavePreferences(); BuildPage(); }
    }
    private void StopPlayback() { playback?.Cancel(); speechRequest?.Cancel(); speech?.Stop(); }
    public void Shutdown()
    {
        if(disposed) return;
        disposed=true; lifetime.Cancel(); StopPlayback(); session?.Dispose(); http.Dispose(); lifetime.Dispose();
    }
}
