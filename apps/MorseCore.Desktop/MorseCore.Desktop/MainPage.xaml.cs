using System.Net.Http;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using MorseCore.AppCore;
using MorseCore.Interop;
using MorseCore.AppCore.Radio;
using Windows.Storage.Pickers;

namespace MorseCore.Desktop;

public sealed partial class MainPage : Page
{
    private readonly PreferencesStore store;
    private readonly RadioStore radioStore;
    private Preferences preferences;
    private Localizer text;
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource? playback;
    private readonly HttpClient http=new();
    private readonly List<Button> menuButtons=[];
    private readonly Dictionary<Button,Border> navigationIndicators=[];
    private static readonly string[] EducationPages=["alphabet","practice","keying","glossary"];
    private static readonly string[] RadioPages=["stationProfiles","qsoAssistant","qsoLog"];
    private bool educationExpanded,radioExpanded;
    private StackPanel? educationChildren,radioChildren;
    private string page="converter", source="", version="0.7.4";
    private ConversionResult? result;
    private bool decode, working, initialized, disposed, nativeReady;
    private int revision;
    private TextBox? inputBox, outputBox;
    private TextBlock? statistics;
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
        string preferencesPath=SmokeDirectory is { } directory ? Path.Combine(directory,"preferences.json") : !string.IsNullOrEmpty(home) && Path.IsPathFullyQualified(home) ? Path.Combine(home,"settings.json") : PreferencesStore.DefaultPath;
        store=new(preferencesPath); radioStore=new(Path.Combine(Path.GetDirectoryName(preferencesPath)!,"Radio"));
        preferences=store.Load(); text=new(preferences.Language);
        InitializeComponent(); InitializeChrome(); ApplyTheme();
        InitializeBackdrop();
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
        var minimumSplash=Task.Delay(TimeSpan.FromSeconds(2),lifetime.Token);
#if DEBUG
        var splashTimer=System.Diagnostics.Stopwatch.StartNew();
        smokeLoadingControlsHidden=WindowBar.Visibility==Visibility.Collapsed && WindowBar.ActualHeight==0 && TitleBrand.Visibility==Visibility.Collapsed && PaneToggle.Visibility==Visibility.Collapsed && Shell.Visibility==Visibility.Collapsed;
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
        SetStatus(ready ? store.LoadWarning is null ? T("ready") : T("settingsRecovered") : T("nativeUnavailable"));
        if(update!=null && ready) SetStatus($"{T("updateAvailable")} · {update.Version}");
        try { await minimumSplash; }
        catch(OperationCanceledException) { return; }
        if(disposed) return;
#if DEBUG
        smokeLoadingControlsHidden &= WindowBar.Visibility==Visibility.Collapsed && WindowBar.ActualHeight==0 && TitleBrand.Visibility==Visibility.Collapsed && PaneToggle.Visibility==Visibility.Collapsed && Shell.Visibility==Visibility.Collapsed;
        if(SmokeDirectory is { } readyDirectory) await Snapshot(Path.Combine(readyDirectory,"loading-ready.png"));
        double splashVisibleMilliseconds=splashTimer.Elapsed.TotalMilliseconds;
#endif
        await DismissSplash();
        if(disposed) return;
        WindowBar.Visibility=Visibility.Visible;
        TitleBrand.Visibility=Visibility.Visible;
        Shell.Visibility=Visibility.Visible;
        PaneToggle.Visibility=Visibility.Visible; PaneToggle.IsEnabled=true;
        AnimateContent();
#if DEBUG
        if(SmokeDirectory is { } directory) { await RunUiSmoke(directory,splashVisibleMilliseconds); return; }
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
        ApplyTheme();
        UpdateBackdropMotion();
        ToolTipService.SetToolTip(CloseWindowButton,T("closeApp"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CloseWindowButton,T("closeApp"));
        ToolTipService.SetToolTip(MinimizeWindowButton,T("minimizeApp"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(MinimizeWindowButton,T("minimizeApp"));
        BuildNavigation();
        SetCollapsed(preferences.Collapsed,false); BuildPage();
        if(languageChanged) SetStatus(T(working || batchWorking ? "busy" : "ready"));
    }
    private void BuildNavigation()
    {
        Menu.Children.Clear(); BottomMenu.Children.Clear(); menuButtons.Clear(); navigationIndicators.Clear();
        AddMenu("converter","\uE8D4"); AddMenu("files","\uE8B7"); AddMenu("audio","\uE8D6");
        AddMenu("education","\uE7BE");
        educationChildren=AddMenuChildren(EducationPages,educationExpanded);
        AddMenu("radio","\uE704");
        radioChildren=AddMenuChildren(RadioPages,radioExpanded);
        AddMenu("settings","\uE713",true);
    }
    private StackPanel AddMenuChildren(string[] pages,bool expanded)
    {
        var children=new StackPanel { Spacing=2,Visibility=expanded && !preferences.Collapsed ? Visibility.Visible : Visibility.Collapsed };
        foreach(string key in pages)
        {
            var title=Label(T(key),13); title.Margin=new Thickness(4,0,4,0);
            var child=new Button { Content=title,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(8,9,8,9),Margin=new Thickness(24,0,0,0),
                Background=page==key ? Brush("CardBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent),BorderThickness=new Thickness(0),BorderBrush=Brush("LineBrush"),CornerRadius=new CornerRadius(8) };
            child.Margin=new Thickness(5,0,5,0);
            title.Foreground=Brush(page==key ? "AccentInkBrush" : "MutedBrush");
            title.FontWeight=page==key ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(child,"Nav-"+key);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(child,T(key));
            child.Click+=(_,_)=>Navigate(key); children.Children.Add(child);
        }
        Menu.Children.Add(new Border { Child=children,BorderBrush=Brush("LineBrush"),BorderThickness=text.IsRtl?new Thickness(0,0,1,0):new Thickness(1,0,0,0),Margin=text.IsRtl?new Thickness(0,0,24,0):new Thickness(24,0,0,0) });
        return children;
    }
    private void AddMenu(string key,string glyph,bool bottom=false)
    {
        var panel=new StackPanel { Orientation=Orientation.Horizontal,Spacing=11 };
        panel.Children.Add(new FontIcon { Glyph=glyph,FontSize=18,Width=20 });
        var title=Label(T(key),14); title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold; panel.Children.Add(title);
        bool group=key is "education" or "radio";
        bool expanded=key=="education" ? educationExpanded : radioExpanded;
        bool selected=key==page || key=="education" && EducationPages.Contains(page) || key=="radio" && RadioPages.Contains(page);
        if(group) panel.Children.Add(new FontIcon { Glyph=expanded?"\uE70E":"\uE70D",FontSize=10,Margin=new Thickness(4,0,0,0) });
        var ink=Brush(selected ? "AccentInkBrush" : "MutedBrush");
        ((FontIcon)panel.Children[0]).Foreground=ink; ((TextBlock)panel.Children[1]).Foreground=ink;
        var button=new Button { Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(12),BorderThickness=new Thickness(0),Foreground=ink,Background=selected && !group ? Brush("CardBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent),Tag=key };
        var indicator=new Border { Width=2,Height=22,CornerRadius=new CornerRadius(0),Background=Brush("AccentInkBrush"),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false };
        var item=new Grid(); item.Children.Add(button); item.Children.Add(indicator); navigationIndicators.Add(button,indicator);
        button.Resources["ButtonBackgroundPointerOver"]=Brush("CanvasBrush");
        button.Resources["ButtonBackgroundPressed"]=Brush("CanvasBrush");
        ToolTipService.SetToolTip(button,T(key));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(button,"Nav-"+key);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,T(key));
        button.Click+=(_,_)=>
        {
            if(!group) { Navigate(key); return; }
            if(preferences.Collapsed)
            {
                var flyout=new MenuFlyout();
                foreach(string child in key=="education" ? EducationPages : RadioPages)
                {
                    var entry=new MenuFlyoutItem { Text=T(child) }; entry.Click+=(_,_)=>Navigate(child); flyout.Items.Add(entry);
                }
                flyout.ShowAt(button); return;
            }
            if(key=="education") educationExpanded=!educationExpanded; else radioExpanded=!radioExpanded;
            BuildNavigation(); SetCollapsed(preferences.Collapsed,false);
        };
        (bottom ? BottomMenu : Menu).Children.Add(item); menuButtons.Add(button);
    }
    private SolidColorBrush Brush(string key)
    {
        string theme=ActualTheme==ElementTheme.Dark ? "Default" : "Light";
        return (SolidColorBrush)((ResourceDictionary)Application.Current.Resources.ThemeDictionaries[theme])[key];
    }
    private void Navigate(string key)
    {
        if(key=="radio") key="stationProfiles";
        if(page==key) return;
        StopPlayback(); revision++;
        if(EducationPages.Contains(key)) educationExpanded=true;
        if(RadioPages.Contains(key)) radioExpanded=true;
        page=key; Rebuild(); ContentScroll.ChangeView(null,0,null,true);
    }
    private void BuildPage()
    {
        CleanupKeying(); CleanupAudioDecoder(); CleanupVoicePacks();
        inputBox=null; outputBox=null; statistics=null;
        PageContent.Children.Clear(); Breadcrumb.Text=T(page);
        switch(page)
        {
            case "converter": BuildConverter(); break;
            case "files": BuildFiles(); break;
            case "audio": BuildAudio(); break;
            case "practice": BuildPractice(); break;
            case "keying": BuildKeying(); break;
            case "alphabet": BuildAlphabet(); break;
            case "glossary": BuildGlossary(); break;
            case "stationProfiles": case "qsoAssistant": case "qsoLog": BuildRadio(); break;
            case "settings": BuildSettings(); break;
        }
        AnimateContent();
    }
    private void SetStatus(string value) { if(!disposed) Status.Text=value; }
    private async Task Guard(Func<Task> operation)
    {
        try { await operation(); }
        catch(OperationCanceledException) { SetStatus(T("cancelled")); }
        catch(MorseNativeException e) { SetStatus(e.Status switch { -1=>T("missingInput"),-3=>T("unsupported"),-4=>T("invalidMorse"),_=>T("error") }); }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or DllNotFoundException or EntryPointNotFoundException or TypeInitializationException)
        {
#if DEBUG
            if(SmokeDirectory!=null) smokeOperationFailure=e.ToString();
#endif
            SetStatus(T("error"));
        }
    }
    private void SavePreferences()
    {
        try { store.Save(preferences); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException) { SetStatus(T("saveError")); }
    }
    private void TogglePane(object sender,RoutedEventArgs e)=>SetCollapsed(!preferences.Collapsed,true);
    private void SetCollapsed(bool collapsed,bool animate)
    {
        var previous=paneAnimation; paneAnimation=null; previous?.Stop();
        preferences=preferences with { Collapsed=collapsed };
        if(educationChildren!=null) educationChildren.Visibility=!collapsed && educationExpanded ? Visibility.Visible : Visibility.Collapsed;
        if(radioChildren!=null) radioChildren.Visibility=!collapsed && radioExpanded ? Visibility.Visible : Visibility.Collapsed;
        foreach(var button in menuButtons)
        {
            ((StackPanel)button.Content).Children[1].Visibility=collapsed ? Visibility.Collapsed : Visibility.Visible;
            if(((StackPanel)button.Content).Children.Count>2) ((StackPanel)button.Content).Children[2].Visibility=collapsed ? Visibility.Collapsed : Visibility.Visible;
            button.HorizontalAlignment=collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment=collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            button.Width=collapsed ? 44 : double.NaN;
            button.Height=42;
            button.Padding=collapsed ? new Thickness(0) : new Thickness(12,0,12,0);
            bool selected=Equals(button.Tag,page);
            navigationIndicators[button].Visibility=!collapsed && selected ? Visibility.Visible : Visibility.Collapsed;
        }
        ToolTipService.SetToolTip(PaneToggle,T(collapsed ? "openPane" : "closePane"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PaneToggle,T(collapsed ? "openPane" : "closePane"));
        double width=collapsed ? 72 : 240;
        if(animate && MotionAllowed())
        {
            var storyboard=new Storyboard();
            var animation=new DoubleAnimation { From=Pane.ActualWidth,To=width,Duration=new Duration(TimeSpan.FromMilliseconds(210)),EnableDependentAnimation=true,EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
            Storyboard.SetTarget(animation,Pane); Storyboard.SetTargetProperty(animation,"Width"); storyboard.Children.Add(animation);
            storyboard.Completed+=(_,_)=> { if(!ReferenceEquals(paneAnimation,storyboard)) return; paneAnimation=null; storyboard.Stop(); Pane.Width=width; };
            paneAnimation=storyboard; storyboard.Begin();
        }
        else Pane.Width=width;
        if(animate) SavePreferences();
    }
    private void AnimateContent()
    {
        var previous=contentAnimation; contentAnimation=null; previous?.Stop();
        var offset=(TranslateTransform)PageContent.RenderTransform;
        PageContent.Opacity=1; offset.X=offset.Y=0;
        if(!MotionAllowed()) return;
        var storyboard=new Storyboard();
        var fade=new DoubleAnimation { From=0.35,To=1,Duration=new Duration(TimeSpan.FromMilliseconds(200)) };
        Storyboard.SetTarget(fade,PageContent); Storyboard.SetTargetProperty(fade,"Opacity"); storyboard.Children.Add(fade);
        var slide=new DoubleAnimation { From=text.IsRtl ? -22 : 22,To=0,Duration=new Duration(TimeSpan.FromMilliseconds(240)),EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
        Storyboard.SetTarget(slide,offset); Storyboard.SetTargetProperty(slide,"X"); storyboard.Children.Add(slide);
        storyboard.Completed+=(_,_)=> { if(!ReferenceEquals(contentAnimation,storyboard)) return; storyboard.Stop(); PageContent.Opacity=1; offset.X=offset.Y=0; contentAnimation=null; };
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
    private Border Card(UIElement child)=>new() { Child=child,Background=Brush("CardBrush"),BorderBrush=Brush("LineBrush"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Padding=new Thickness(20) };
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
    { preferences=preferences with { Alphabet=(MorseAlphabet)index }; result=null; decoderResult=null; revision++; SavePreferences(); BuildPage(); });
    private void InitializePicker(object picker)
    {
#if MORSE_WINUI
        WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
#endif
    }
    private async Task<string?> SavePath(string extension,string suggested)
    {
#if DEBUG
        if(SmokeDirectory!=null && smokeSavePath is { } reserved)
        { smokeSavePath=null; return reserved; }
#endif
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
        disposed=true; lifetime.Cancel(); CleanupKeying(); CleanupAudioDecoder(); CleanupVoicePacks(); StopPlayback(); http.Dispose(); lifetime.Dispose();
        paneAnimation?.Stop(); contentAnimation?.Stop();
        CleanupBackdrop();
    }
}
