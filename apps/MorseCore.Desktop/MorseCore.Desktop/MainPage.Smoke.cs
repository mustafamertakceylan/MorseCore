#if DEBUG
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using MorseCore.Interop;
using MorseCore.AppCore;
using MorseCore.AppCore.Radio;
using Windows.Graphics.Imaging;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private string? smokeSavePath;
    private string? smokeOperationFailure;
    private bool smokeLoadingControlsHidden;
    private async Task Snapshot(string path,UIElement? element=null,bool contentOnly=false)
    {
        await Task.Delay(250); Root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(); await bitmap.RenderAsync(element ?? Root);
        var pixels=await bitmap.GetPixelsAsync();
        using var stream=File.Create(path);
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream.AsRandomAccessStream());
        byte[] data=pixels.ToArray(); int height=bitmap.PixelHeight;
        if(contentOnly)
        {
            // Exclude native caption capture artifacts; keep the original application pixels unchanged.
            int top=Math.Clamp((int)Math.Round(WindowBar.ActualHeight*XamlRoot!.RasterizationScale),0,height-1);
            data=data.AsSpan(top*bitmap.PixelWidth*4).ToArray(); height-=top;
        }
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)height,96,96,data);
        await encoder.FlushAsync();
    }
    private async Task RunUiSmoke(string directory,double splashVisibleMilliseconds)
    {
        Directory.CreateDirectory(directory); var checks=new List<string>(); string? failure=null;
        var voices=new Dictionary<string,int>(); bool synthesized=false;
        bool mouseDragTested=false;
        object? mouseDragEvidence=null;
        void Check(bool condition,string description) { if(!condition) throw new InvalidOperationException(description); checks.Add(description); }
        IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            { var child=VisualTreeHelper.GetChild(root,i); yield return child; foreach(var node in Descendants(child)) yield return node; }
        }
        async Task Invoke(Button button)
        {
            var peer=new ButtonAutomationPeer(button);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke(); await Task.Delay(60);
        }
        async Task ActionButton(string id)
        {
            Root.UpdateLayout();
            await Invoke(Descendants(Root).OfType<Button>().First(b=>AutomationProperties.GetAutomationId(b)==id));
        }
        async Task ExpandSection(string id)
        {
            Descendants(Root).OfType<Expander>().First(e=>AutomationProperties.GetAutomationId(e)==id).IsExpanded=true;
            await Task.Delay(60); Root.UpdateLayout();
        }
        async Task GuideSnapshot(string name)
        {
            var guide=Descendants(PageContent).OfType<Expander>().First(e=>AutomationProperties.GetAutomationId(e)=="CountryRules");
            double y=guide.TransformToVisual(ContentScroll).TransformPoint(new Windows.Foundation.Point(0,0)).Y+ContentScroll.VerticalOffset;
            ContentScroll.ChangeView(null,y,null,true); await Snapshot(Path.Combine(directory,name));
        }
        async Task WaitFor(Func<bool> condition,string description)
        {
            var deadline=DateTimeOffset.UtcNow.AddSeconds(5);
            while(!condition() && DateTimeOffset.UtcNow<deadline) await Task.Delay(50);
            Check(condition(),description);
        }
        double PaneCenter(FrameworkElement element)
        {
            var bounds=element.TransformToVisual(Pane).TransformBounds(new Windows.Foundation.Rect(0,0,element.ActualWidth,element.ActualHeight));
            return bounds.X+bounds.Width/2;
        }
        void CheckWorkspaceGutters(string state)
        {
            Root.UpdateLayout();
            Windows.Foundation.Rect Bounds(FrameworkElement element)=>element.TransformToVisual(Root).TransformBounds(new Windows.Foundation.Rect(0,0,element.ActualWidth,element.ActualHeight));
            var frame=Bounds(WorkspaceFrame); var shell=Bounds(Shell); var pane=Bounds(Pane);
            double sidebarGap=text.IsRtl ? pane.Left-frame.Right : frame.Left-pane.Right;
            double outerGap=text.IsRtl ? frame.Left-shell.Left : shell.Right-frame.Right;
            Check(frame.Top-shell.Top>=9 && shell.Bottom-frame.Bottom>=9 && sidebarGap>=11 && outerGap>=11,
                $"Outer workspace stays separated from the title bar, sidebar and window edges in {state} (top {frame.Top-shell.Top:0.#}, bottom {shell.Bottom-frame.Bottom:0.#}, sidebar {sidebarGap:0.#}, outer {outerGap:0.#})");
        }
        void CheckCollapsedAlignment(string language)
        {
            Root.UpdateLayout();
            CheckWorkspaceGutters(language+" collapsed");
            double center=PaneCenter((FontIcon)((StackPanel)menuButtons[0].Content).Children[0]);
            Check(menuButtons.All(b=>Math.Abs(PaneCenter((FontIcon)((StackPanel)b.Content).Children[0])-center)<1),"Collapsed navigation and settings icons align in "+language);
            var toggle=PaneToggle.TransformToVisual(WindowBar).TransformBounds(new Windows.Foundation.Rect(0,0,PaneToggle.ActualWidth,PaneToggle.ActualHeight));
            Check(PaneToggle.IsEnabled && toggle.X>=0 && toggle.Right<100 && toggle.Y>=0 && toggle.Bottom<=WindowBar.ActualHeight,"Top-left menu toggle remains accessible with the sidebar collapsed in "+language);
            // Nested controls snap independently at fractional DPI. Keep the absolute center within two screen pixels.
            Check(Math.Abs(center-(Pane.ActualWidth-Pane.BorderThickness.Right+Pane.BorderThickness.Left)/2)*XamlRoot!.RasterizationScale<=2,$"Collapsed icons stay centered in {language} (center {center:0.###}, pane {Pane.ActualWidth:0.###})");
        }
        void CheckPageWidth(string state)
        {
            Root.UpdateLayout();
            bool Shown(FrameworkElement element)
            {
                for(DependencyObject? node=element;node!=null && node!=PageContent;node=VisualTreeHelper.GetParent(node))
                    if(node is UIElement { Visibility:Visibility.Collapsed }) return false;
                return true;
            }
            var overflow=Descendants(PageContent).OfType<FrameworkElement>()
                .Where(e=>e is TextBox or ComboBox or NumberBox or Button or Expander)
                .Where(e=>e.ActualWidth>0 && Shown(e))
                .Where(e=>
                {
                    var bounds=e.TransformToVisual(PageContent).TransformBounds(new Windows.Foundation.Rect(0,0,e.ActualWidth,e.ActualHeight));
                    return bounds.Left < -2 || bounds.Right>PageContent.ActualWidth+2;
                }).Select(e=>AutomationProperties.GetAutomationId(e) is { Length:>0 } id ? id : e.GetType().Name).ToArray();
            Check(overflow.Length==0,"Page controls fit within the workspace in "+state+": "+string.Join(", ",overflow));
        }
        try
        {
            Root.UpdateLayout();
            Check(Splash.Visibility==Visibility.Collapsed && LoadingProgress.Value==100,"Loading screen completes before main interaction");
            Check(smokeLoadingControlsHidden,"Entire title bar occupies no space throughout loading, including the ready frame; navigation stays hidden");
            Check(WindowBar.Visibility==Visibility.Visible && WindowBar.ActualHeight==48 && TitleBrand.Visibility==Visibility.Visible && PaneToggle.Visibility==Visibility.Visible && Shell.Visibility==Visibility.Visible && PaneToggle.IsEnabled,"Title bar and navigation appear only after the loading overlay is dismissed");
            Check(!Backdrop.IsHitTestVisible && !Backdrop.IsTabStop && Descendants(Backdrop).OfType<Viewbox>().Count(c=>c.ActualWidth>0 && c.ActualHeight>0)==4,"All four RAW background corners render without intercepting pointer or keyboard input");
            Check(splashVisibleMilliseconds>=1950,"Welcome screen remains visible for at least two seconds before dismissal");
#if MORSE_WINUI
            var presenter=(Microsoft.UI.Windowing.OverlappedPresenter)App.MainWindow.AppWindow.Presenter;
            Check(App.MainWindow.ExtendsContentIntoTitleBar && App.MainWindow.AppWindow.TitleBar.PreferredHeightOption==Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed && App.MainWindow.AppWindow.TitleBar.Height==0,"System caption controls hidden");
            Check(!presenter.IsResizable && !presenter.IsMaximizable && presenter.IsMinimizable,"Window size stays fixed while minimizing is enabled");
            var fixedSize=App.MainWindow.AppWindow.Size;
            App.MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(fixedSize.Width+80,fixedSize.Height+60)); await Task.Delay(150);
            Check(App.MainWindow.AppWindow.Size.Equals(fixedSize),"Window dimensions stay locked after a resize request");
            presenter.Maximize(); await Task.Delay(150);
            Check(presenter.State==Microsoft.UI.Windowing.OverlappedPresenterState.Restored && App.MainWindow.AppWindow.Size.Equals(fixedSize),"Maximize request cannot change fixed window");
            await ActionButton("WindowMinimize"); await Task.Delay(250);
            Check(presenter.State==Microsoft.UI.Windowing.OverlappedPresenterState.Minimized,"Custom minimize button keeps the window in the taskbar");
            Check(WindowsAppIdentity.Current()==WindowsAppIdentity.Id,"Windows process uses the same stable taskbar identity as the installed shortcuts");
            var originalContent=App.MainWindow.Content;
            var secondLaunch=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,CreateNoWindow=true };
            secondLaunch.Environment.Remove("MORSECORE_DESKTOP_SMOKE");
            secondLaunch.Environment.Remove("MORSECORE_DESKTOP_MOUSE_SMOKE");
            secondLaunch.Environment["MORSECORE_DESKTOP_HOME"]=Path.Combine(directory,"secondary-preferences");
            using(var secondary=Process.Start(secondLaunch)!)
            {
                try
                {
                    await WaitFor(()=>secondary.HasExited,"A second launch redirects and exits instead of leaving another application process");
                    Check(secondary.ExitCode==0,"Redirected second launch exits successfully");
                    await WaitFor(()=>presenter.State==Microsoft.UI.Windowing.OverlappedPresenterState.Restored,"A second launch restores the original minimized window");
                    Check(ReferenceEquals(App.MainWindow.Content,originalContent),"Repeated launch preserves the existing page and user state");
                }
                finally { if(!secondary.HasExited) secondary.Kill(true); }
            }
            App.MainWindow.Activate(); await Task.Delay(200);
            Check(presenter.State==Microsoft.UI.Windowing.OverlappedPresenterState.Restored && App.MainWindow.AppWindow.Size.Equals(fixedSize),"Restoring after minimize preserves the fixed window size");
            var originalPosition=App.MainWindow.AppWindow.Position;
            if(Environment.GetEnvironmentVariable("MORSECORE_DESKTOP_MOUSE_SMOKE")=="1")
            {
                Root.UpdateLayout();
                var brand=TitleBrand.TransformToVisual(Root).TransformBounds(new Windows.Foundation.Rect(0,0,TitleBrand.ActualWidth,TitleBrand.ActualHeight));
                var anchor=WindowChrome.ScreenPoint(App.MainWindow,brand.X+brand.Width/2,brand.Y+brand.Height/2,XamlRoot!.RasterizationScale);
                var mouse=await WindowChrome.CheckMouseDrag(App.MainWindow,anchor);
                mouseDragEvidence=mouse;
                Check(mouse.MovedWhileHeld,"Mouse drag moves the window while the left button is held");
                Check(mouse.StoppedOnRelease,"Window stops when the mouse button is released");
                Check(mouse.ClickDidNotLatch,"A single title bar click does not latch window movement");
                Check(mouse.SecondMovedWhileHeld && mouse.SecondStoppedOnRelease,"A second mouse drag also stops on release");
                Check(mouse.EnteredLoops>0 && mouse.EnteredLoops==mouse.ExitedLoops,"Native mouse move loops all exit without another click");
                Check(App.MainWindow.AppWindow.Size.Equals(fixedSize),"Mouse dragging preserves fixed dimensions");
                var menuBounds=PaneToggle.TransformToVisual(Root).TransformBounds(new Windows.Foundation.Rect(0,0,PaneToggle.ActualWidth,PaneToggle.ActualHeight));
                Check(WindowChrome.IsClientHitTarget(App.MainWindow,WindowChrome.ScreenPoint(App.MainWindow,menuBounds.X+menuBounds.Width/2,menuBounds.Y+menuBounds.Height/2,XamlRoot.RasterizationScale)),"Header menu button is a native client target, not a window drag target");
                mouseDragTested=true;
            }
            App.MainWindow.AppWindow.Move(originalPosition);
#endif
            preferences=preferences with { Theme="light",Language="tr",Alphabet=MorseAlphabet.Latin,ReduceMotion=true,Collapsed=false,CheckUpdates=false };
            ApplyTheme(); source="Merhaba dünya!"; Rebuild(); await ConvertNow();
            await ActionButton("PaneToggle"); await Task.Delay(250);
            Check(preferences.Collapsed && store.Load().Collapsed && Math.Abs(Pane.ActualWidth-72)<2,"Header menu button collapses the sidebar and saves its state");
            await ActionButton("PaneToggle"); await Task.Delay(250);
            Check(!preferences.Collapsed && !store.Load().Collapsed && Math.Abs(Pane.ActualWidth-240)<2,"Header menu button restores the expanded sidebar");
            Check(result?.Text=="-- . .-. .... .- -... .- / -.. ..- -. -.-- .- -.-.-- ","Turkish conversion through native DLL");
            Check(result!.Statistics.InputCharacters==14 && result.Statistics.Words==2,"Unicode character and word counters");
            await Snapshot(Path.Combine(directory,"light.png"));
            var lightInput=inputBox!.TransformToVisual(PageContent).TransformBounds(new Windows.Foundation.Rect(0,0,inputBox.ActualWidth,inputBox.ActualHeight));
            var lightOutput=outputBox!.TransformToVisual(PageContent).TransformBounds(new Windows.Foundation.Rect(0,0,outputBox.ActualWidth,outputBox.ActualHeight));
            Check(TitleLogo.Source is BitmapImage { PixelWidth: >0,PixelHeight: >0 },"Supplied logo loads in the application title bar");
            preferences=preferences with { Theme="dark" }; ApplyTheme(); Rebuild(); await Snapshot(Path.Combine(directory,"dark.png"));
            Check(ActualTheme==ElementTheme.Dark,"Dark theme applied");
            var darkSource=inputBox!; var darkResult=outputBox!;
            var darkInput=darkSource.TransformToVisual(PageContent).TransformBounds(new Windows.Foundation.Rect(0,0,darkSource.ActualWidth,darkSource.ActualHeight));
            var darkOutput=darkResult.TransformToVisual(PageContent).TransformBounds(new Windows.Foundation.Rect(0,0,darkResult.ActualWidth,darkResult.ActualHeight));
            Check(Math.Abs(darkInput.X-lightInput.X)<1 && Math.Abs(darkInput.Y-lightInput.Y)<1 && Math.Abs(darkInput.Width-lightInput.Width)<1 && Math.Abs(darkInput.Height-lightInput.Height)<1 &&
                Math.Abs(darkOutput.X-lightOutput.X)<1 && Math.Abs(darkOutput.Y-lightOutput.Y)<1 && Math.Abs(darkOutput.Width-lightOutput.Width)<1 && Math.Abs(darkOutput.Height-lightOutput.Height)<1 && darkOutput.Left-darkInput.Right>=12,
                "Dark converter preserves light-theme editor sizes, padding and the gap between editors");
            var surfaces=new RenderTargetBitmap(); await surfaces.RenderAsync(Root); byte[] surfacePixels=(await surfaces.GetPixelsAsync()).ToArray();
            double Luminance(FrameworkElement element,double x,double y)
            {
                var point=element.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(x,y));
                int px=Math.Clamp((int)Math.Round(point.X*XamlRoot!.RasterizationScale),0,surfaces.PixelWidth-1),py=Math.Clamp((int)Math.Round(point.Y*XamlRoot.RasterizationScale),0,surfaces.PixelHeight-1);
                int offset=(py*surfaces.PixelWidth+px)*4;
                double Linear(byte value) { double channel=value/255.0; return channel<=0.04045 ? channel/12.92 : Math.Pow((channel+0.055)/1.055,2.4); }
                return 0.2126*Linear(surfacePixels[offset+2])+0.7152*Linear(surfacePixels[offset+1])+0.0722*Linear(surfacePixels[offset]);
            }
            double Contrast(double a,double b)=>(Math.Max(a,b)+0.05)/(Math.Min(a,b)+0.05);
            var converterCard=PageContent.Children.OfType<Border>().First(b=>Descendants(b).Contains(darkSource));
            double workspaceTone=Luminance(Workspace,Workspace.ActualWidth/2,5),cardTone=Luminance(converterCard,converterCard.ActualWidth/2,converterCard.ActualHeight-10);
            double inputTone=Luminance(darkSource,darkSource.ActualWidth/2,darkSource.ActualHeight/2),resultTone=Luminance(darkResult,darkResult.ActualWidth/2,darkResult.ActualHeight/2);
            Check(Contrast(workspaceTone,cardTone)>=1.5 && Contrast(cardTone,inputTone)>=1.6 && Contrast(cardTone,resultTone)>=1.2,"Rendered dark workspace, cards, inputs and results have distinct surface tones");
            preferences=preferences with { Language="ar",Alphabet=MorseAlphabet.Arabic,Theme="light" }; source="سلام"; result=null; ApplyTheme(); Rebuild(); await ConvertNow();
            Check(Shell.FlowDirection==FlowDirection.RightToLeft,"Arabic interface RTL");
            Check(inputBox!.FlowDirection==FlowDirection.RightToLeft && outputBox!.FlowDirection==FlowDirection.LeftToRight,"Arabic input RTL and Morse LTR");
            await Snapshot(Path.Combine(directory,"arabic.png"));
            Check(Grid.GetColumn(Pane)==0 && Shell.FlowDirection==FlowDirection.RightToLeft,"Arabic mirrored navigation column");
            preferences=preferences with { Language="tr",Alphabet=MorseAlphabet.Latin,Theme="light" }; source="Merhaba dünya!"; result=null; ApplyTheme();
            foreach(string feature in new[]{"files","audio","practice","alphabet","glossary","radio","settings"})
            { Navigate(feature); await Snapshot(Path.Combine(directory,feature+".png")); Check(PageContent.Children.Count>0,feature+" page rendered"); }
            var credits=Descendants(Root).OfType<Expander>().First(e=>AutomationProperties.GetAutomationId(e)=="OpenSourceCredits");
            credits.IsExpanded=true; await Task.Delay(150); Root.UpdateLayout();
            var creditLinks=Descendants(credits).OfType<HyperlinkButton>().ToArray();
            Check(creditLinks.Length==7 && creditLinks.All(link=>link.NavigateUri?.Scheme=="https"),"Acknowledgements contain six project links and voice model credits");
            Check(creditLinks.Any(link=>Equals(link.Content,"EMA Lightning")) && creditLinks.Any(link=>Equals(link.Content,"Antalia-2 Mini")),"Local Turkish AI authors acknowledged in settings");
            ContentScroll.ChangeView(null,ContentScroll.ScrollableHeight,null,true); await Snapshot(Path.Combine(directory,"credits.png"));
            Navigate("practice"); await Task.Delay(100); await ActionButton("start");
            Check(lesson?.Current!=null,"Practice start button");
            var question=lesson!.Current!;
            Navigate("settings");
            var themeChoice=Descendants(Root).OfType<ComboBox>().First(c=>AutomationProperties.GetAutomationId(c)=="ThemeChoice");
            themeChoice.SelectedIndex=2; await Task.Delay(100);
            Check(preferences.Theme=="dark" && ActualTheme==ElementTheme.Dark && store.Load().Theme=="dark","Theme changes and persists from Settings");
            Navigate("practice");
            Check(ReferenceEquals(question,lesson.Current),"Practice question survives navigation and theme changes");
            var answer=Descendants(Root).OfType<TextBox>().First(b=>Equals(b.Header,T("answer")));
            answer.Text=lesson.Current!.Answer; await ActionButton("submit");
            Check(lesson.Correct==1 && lesson.Incorrect==0,"Practice submit button and score");
            answer.Text="WRONG"; await ActionButton("submit"); await ActionButton("retryMissed");
            Check(lesson.Current!=null && lesson.Correct==0,"Mistakes can be reviewed separately");
            keyingSound=false; keyingWpm=5; Navigate("keying"); await ActionButton("keyingStart");
            Check(keyingSession is { IsFinished:false } && keyingSmokePress!=null,"Keying start opens a measured input session");
            keyingSmokePress!(true); await Task.Delay(260); keyingSmokePress!(false); await Task.Delay(600);
            await ActionButton("keyingFinish");
            Check(keyingSession!.Snapshot().Text=="E" && keyingSession.Snapshot().Marks==1,"Keying paddle handlers decode a measured dot and finalize its letter");
            Check(keyingSession.IsFinished && keyingTimer?.IsEnabled==false,"Finishing keying stops the display timer");
            await Snapshot(Path.Combine(directory,"keying.png"));
            keyingExpected="🙂"; BuildPage(); keyingSmokePress!(true);
            Check(keyingSession?.IsPressed!=true && keyingTone==null && Status.Text==T("unsupported"),"Unsupported keying reference is rejected safely from the direct input handler");
            keyingExpected="SOS"; keyingWpm=20;
            Navigate("practice");
            Check(keyingSmokePress==null && keyingTimer==null && keyingTone==null,"Leaving keying releases its input hook, timer and tone");
            await ActionButton("Nav-education");
            Check(educationChildren?.Visibility==Visibility.Collapsed,"Education group collapses");
            await ActionButton("Nav-education"); await ActionButton("Nav-glossary");
            var search=Descendants(Root).OfType<TextBox>().First(b=>Equals(b.Header,T("searchTerms")));
            search.Text="QRL"; await Task.Delay(70);
            Check(Descendants(PageContent).OfType<TextBlock>().Any(b=>b.Text=="QRL?"),"Radio glossary search filters real definitions");
            if(radioExpanded) await ActionButton("Nav-radio");
            await ActionButton("Nav-radio");
            Check(radioChildren?.Visibility==Visibility.Visible && radioChildren.Children.Count==3,"Radio expands into three sidebar destinations");
            await ActionButton("Nav-qsoAssistant");
            Check(page=="qsoAssistant" && Breadcrumb.Text==T("qsoAssistant") && Descendants(PageContent).OfType<TextBlock>().Any(b=>b.Text==T("stationRequired")),"Contact preparation explains the missing station");
            await ActionButton("stationProfiles");
            Check(page=="stationProfiles" && Breadcrumb.Text==T("stationProfiles"),"Missing-station action opens the station subpage");
            Check(!Descendants(PageContent).OfType<ComboBox>().Any(c=>c.ItemsSource is string[] items && items.SequenceEqual(RadioPages.Select(T))),"Radio subpages no longer repeat the sidebar as a page selector");
            var callInput=Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="callSign");
            callInput.Text="TA1PDM"; await Task.Delay(100); await ActionButton("analyzeCall");
            Check(stationDraft.CountryId=="TR" && stationDraft.ClassId=="A","Station recognition selects Turkish class A and region");
            await ExpandSection("StationOptional");
            Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="operatorName").Text="Ali";
            Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="qth").Text="Ankara";
            await ActionButton("saveStation");
            Check(ActiveStation?.CallSign=="TA1PDM" && radioStore.LoadProfiles().Profiles.Count==1,"Station profile saves through UI");
            preferences=preferences with { Theme="light" }; ApplyTheme(); Rebuild();
            await ExpandSection("CountryRules");
            Check(Descendants(PageContent).OfType<Border>().Any(b=>AutomationProperties.GetAutomationId(b)=="BandGuideCard" && AutomationProperties.GetName(b)=="20m: 14 MHz – 14.35 MHz; 75 W; "+T("powerOutput")),"Band infographic shows exact Turkish class A frequency limits and transmitter output");
            await GuideSnapshot("band-guide-light.png");
            stationDraft=stationDraft with { ClassId="B" }; BuildPage(); await ExpandSection("CountryRules");
            Check(Descendants(PageContent).OfType<Border>().Any(b=>AutomationProperties.GetName(b)=="40m: 7 MHz – 7.1 MHz; 5 W; "+T("powerOutput")) && Descendants(PageContent).OfType<TextBlock>().Any(b=>b.Text==T("trHomebuilt")),"Class B infographic preserves the 5 W home-built equipment condition");
            stationDraft=stationDraft with { CountryId="DE",ClassId="N" }; preferences=preferences with { Language="de",Theme="dark" }; Rebuild(); await ExpandSection("CountryRules");
            Check(Descendants(PageContent).OfType<Border>().Any(b=>AutomationProperties.GetName(b)=="2m: 144 MHz – 146 MHz; 6.1 W; "+T("guide.erp")),"German class N uses ERP without rounding its 6.1 W limit");
            await GuideSnapshot("band-guide-german.png");
            stationDraft=stationDraft with { ClassId="A" }; BuildPage(); await ExpandSection("CountryRules");
            Check(Descendants(PageContent).OfType<Border>().Any(b=>AutomationProperties.GetName(b)=="20m: 14 MHz – 14.35 MHz; 750 W; "+T("guide.pep")),"German class A uses PEP rather than ERP or transmitter output");
            stationDraft=stationDraft with { CountryId="RU",ClassId="1" }; BuildPage(); await ExpandSection("CountryRules");
            Check(!Descendants(PageContent).OfType<Border>().Any(b=>AutomationProperties.GetAutomationId(b)=="BandGuideCard") && Descendants(PageContent).OfType<TextBlock>().Any(b=>b.Text==T("guide.empty")),"Missing country coverage shows an explanation without invented band limits");
            stationDraft=stationDraft with { CountryId="TR",ClassId="" }; BuildPage(); await ExpandSection("CountryRules");
            Check(!Descendants(PageContent).OfType<Border>().Any(b=>AutomationProperties.GetAutomationId(b)=="BandGuideCard") && Descendants(PageContent).OfType<TextBlock>().Any(b=>b.Text==T("selectClass")),"Band guide asks for a licence class before showing its ranges");
            preferences=preferences with { Language="tr" }; LoadStation(ActiveStation!); countryRulesExpanded=false; Rebuild();
            await ActionButton("Nav-qsoAssistant"); await ActionButton("prepareMessage");
            Check(qsoPreview=="QRL? DE TA1PDM K" && qsoMorse.Contains(" / ",StringComparison.Ordinal),"QRL message prepares Latin calls and Morse");
            await ActionButton("Nav-qsoLog"); await ActionButton("Nav-qsoAssistant");
            Check(qsoPreview=="QRL? DE TA1PDM K" && qsoTextBox!.Text==qsoPreview,"Prepared message survives navigation between radio subpages");
            var other=Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="otherCall"); other.Text="DL1ABC"; await Task.Delay(100);
            Check(qsoPreview=="" && qsoTextBox!.Text=="","Changing QSO data invalidates the visible preview");
            sentRst="579"; receivedRst="589"; qsoMessage="TNX FER CALL";
            foreach(QsoStage step in Enum.GetValues<QsoStage>())
            { qsoStage=step; BuildPage(); await ActionButton("prepareMessage"); Check(qsoPreview.Contains("TA1PDM",StringComparison.Ordinal) && qsoMorse.Length>0,"QSO message action: "+step); }
            await Snapshot(Path.Combine(directory,"qso.png"));
            smokeSavePath=Path.Combine(directory,"qso-preview.wav"); File.WriteAllText(smokeSavePath,""); await ActionButton("exportWav");
            var wavDeadline=DateTimeOffset.UtcNow.AddSeconds(5);
            while(new FileInfo(Path.Combine(directory,"qso-preview.wav")).Length==0 && DateTimeOffset.UtcNow<wavDeadline) await Task.Delay(50);
            Check(new FileInfo(Path.Combine(directory,"qso-preview.wav")).Length>44,"QSO WAV button writes to a picker-reserved file");
            await ExpandSection("QsoRecord"); await ActionButton("saveQso");
            Check(radioStore.Recent(true).Count==1 && radioStore.Recent(false).Count==0,"UI saves a practice QSO separately");
            var kind=Descendants(Root).OfType<ToggleSwitch>().First(b=>Equals(b.Header,T("practiceRecord"))); kind.IsOn=false;
            await ActionButton("saveQso");
            Check(radioStore.Recent(false).Count==1 && radioStore.Recent(false)[0].Utc.Offset==TimeSpan.Zero,"External radio contact logs in UTC");
            await ActionButton("Nav-qsoLog"); await Snapshot(Path.Combine(directory,"qso-log.png"));
            Check(PageContent.Children.OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b)=="logbook.map"),"Contact map is directly accessible outside search filters");
            await ActionButton("Nav-radio");
            Check(radioChildren?.Visibility==Visibility.Collapsed && page=="qsoLog","Collapsing the radio group preserves the current page");
            await ActionButton("Nav-radio");
            await ExpandSection("LogFileActions");
            foreach(bool adif in new[]{false,true})
            {
                string path=Path.Combine(directory,adif?"qso-export.adi":"qso-export.csv"); smokeSavePath=path; File.WriteAllText(path,"");
                await ActionButton(adif?"exportAdif":"exportCsv");
                var deadline=DateTimeOffset.UtcNow.AddSeconds(5);
                while(new FileInfo(path).Length==0 && DateTimeOffset.UtcNow<deadline) await Task.Delay(50);
                Check((await File.ReadAllTextAsync(path)).Contains(adif?"<EOR>":"DL1ABC",StringComparison.Ordinal),"QSO "+(adif?"ADIF":"CSV")+" button handles a picker-reserved target");
            }
            await WaitFor(()=>Descendants(PageContent).OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b)=="logbook.edit"),"Saved QSO rows finish loading");
            var logFilters=Descendants(PageContent).OfType<Expander>().First(e=>Equals(e.Header,T("logbook.filters"))); logFilters.IsExpanded=true; Root.UpdateLayout();
            Descendants(PageContent).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="logbook.searchCall").Text="ZZ9ZZZ";
            await ActionButton("logbook.search");
            await WaitFor(()=>Descendants(PageContent).OfType<TextBlock>().Any(b=>b.Text==T("noRecords")),"Call filter shows an empty result for a missing station");
            await ActionButton("logbook.reset");
            await WaitFor(()=>Descendants(PageContent).OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b)=="logbook.edit"),"Resetting log filters restores saved rows");
            string editedId=radioStore.Recent(showPracticeLog).Single().Id;
            await ActionButton("logbook.edit");
            Descendants(PageContent).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="logbook.grid").Text="KN40";
            Descendants(PageContent).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="qsoNote").Text="UI edit test";
            await ActionButton("logbook.save");
            await WaitFor(()=>logDraft==null,"Saving an edited QSO closes its editor");
            Check(radioStore.Recent(showPracticeLog).Single() is { GridSquare:"KN40",Note:"UI edit test" } updated && updated.Id==editedId,"QSO editing preserves identity and saves location and notes");
            Descendants(PageContent).OfType<Expander>().First(e=>Equals(e.Header,T("logbook.filters"))).IsExpanded=false; Root.UpdateLayout();
            await ActionButton("logbook.map");
            await WaitFor(()=>Descendants(PageContent).OfType<Microsoft.UI.Xaml.Shapes.Ellipse>().Any(),"Offline contact map plots a recorded locator");
            Check(Descendants(PageContent).OfType<Canvas>().All(c=>c.FlowDirection==FlowDirection.LeftToRight),"Contact map uses geographic LTR coordinates");
            ContentScroll.ChangeView(null,200,null,true); await Snapshot(Path.Combine(directory,"log-map.png"));
            importDocument=AdifImport.Parse("<QSO_DATE:8>20261016<TIME_ON:6>120000<CALL:6>G4ABCD<STATION_CALLSIGN:6>TA1PDM<FREQ:6>14.025<MODE:2>CW<GRIDSQUARE:4>IO91<EOR>",new("TA1PDM","TR",showPracticeLog));
            importPreview=radioStore.PreviewImport(importDocument.Records); importFileName="ui-import.adi"; BuildPage();
            Check(importPreview.Candidates.Count==1,"ADIF import preview separates new contacts before confirmation");
            await Snapshot(Path.Combine(directory,"adif-preview.png"));
            await ActionButton("logbook.confirm");
            await WaitFor(()=>importDocument==null,"Confirming ADIF import publishes the reviewed contacts");
            Check(radioStore.Query(new(Practice:showPracticeLog,Call:"G4ABCD")).Total==1,"Imported ADIF contact can be found in the log");
            Check(radioStore.PreviewImport(AdifImport.Parse("<QSO_DATE:8>20261016<TIME_ON:6>120000<CALL:6>G4ABCD<STATION_CALLSIGN:6>TA1PDM<FREQ:6>14.025<MODE:2>CW<EOR>",new("TA1PDM","TR",showPracticeLog)).Records).Duplicates==1,"Importing the same exact contact is recognized as a duplicate");
            logShowMap=false; appliedLogQuery=null; ResetLogPage();
            preferences=preferences with { Language="ar" }; Rebuild(); await ActionButton("Nav-qsoAssistant"); await Snapshot(Path.Combine(directory,"qso-arabic.png"));
            Check(qsoTextBox!.FlowDirection==FlowDirection.LeftToRight && qsoCodeBox!.FlowDirection==FlowDirection.LeftToRight,"Arabic QSO calls and Morse stay LTR");
            await ExpandSection("CountryRules");
            await GuideSnapshot("band-guide-arabic.png");
            Check(Descendants(PageContent).OfType<Border>().Any(b=>AutomationProperties.GetName(b)=="20m: 14 MHz – 14.35 MHz; 75 W; "+T("powerOutput")),"Arabic infographic keeps the same numeric frequency and power references");
            ContentScroll.ChangeView(null,0,null,true); countryRulesExpanded=false;
            SetCollapsed(true,false);
            await Task.Delay(120);
            Check(radioChildren?.Visibility==Visibility.Collapsed,"Collapsed sidebar hides radio children in Arabic");
            CheckCollapsedAlignment("ar radio"); SetCollapsed(false,false); await Task.Delay(120);
            Check(radioChildren?.Visibility==Visibility.Visible && radioChildren.FlowDirection==FlowDirection.RightToLeft,"Arabic radio sidebar restores mirrored subpages after expansion");
            preferences=preferences with { Language="tr" }; Rebuild();
            audioWorkspace=1; Navigate("audio"); BuildPage();
            string decoderWave=Path.Combine(directory,"decoder-sos.wav");
            MorseAudio.WriteWav(decoderWave,"... --- ...",new(20,700));
            decoderPath=decoderWave; decoderResult=null; BuildPage();
            await DecodeAudioPath(decoderWave);
            Check(decoderResult?.Text=="SOS" && decoderResult.UncertainCharacters==0,"Audio decoder solves a native-engine WAV through the desktop workflow");
            var decoderText=Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="DecoderText");
            Check(decoderText.Text=="SOS" && decoderText.IsReadOnly,"Decoded audio appears in the readable results field");
            await Snapshot(Path.Combine(directory,"decoder.png"));
            await ActionButton("DecoderCancel");
            var refreshedInputs=Descendants(PageContent).OfType<ComboBox>().First(b=>AutomationProperties.GetAutomationId(b)=="DecoderInputs");
            await ActionButton("DecoderRefreshInputs");
            await WaitFor(()=>refreshedInputs.ItemsSource!=null,"Audio inputs can be refreshed after cancellation");
            string brokenWave=Path.Combine(directory,"decoder-broken.wav"); File.WriteAllText(brokenWave,"broken");
            await DecodeAudioPath(brokenWave);
            Check(Status.Text==T("decoderFileSize") && decoderOperation==null,"Malformed audio reports its error and releases the operation");
            decoderResult=null;
            var cancelledDecode=DecodeAudioPath(decoderWave); Navigate("converter"); await cancelledDecode;
            Check(decoderResult==null && decoderOperation==null,"Navigating away cancels audio decoding without writing stale results");
            Navigate("settings"); await ActionButton("workspaceManageVoices");
            Check(page=="audio" && audioWorkspace==2,"Settings opens the voice manager inside Audio");
            var voiceDeadline=DateTimeOffset.UtcNow.AddSeconds(10);
            while(voicePackProbe!=null && DateTimeOffset.UtcNow<voiceDeadline) await Task.Delay(50);
            Check(voicePackProbe==null,"Voice manager finishes its local runtime detection");
            Check(Descendants(Root).OfType<StackPanel>().Count(p=>AutomationProperties.GetAutomationId(p).StartsWith("VoicePack-",StringComparison.Ordinal))==2,"Turkish voice manager renders both local neural engines");
            Check(Descendants(Root).OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b)=="voicesInstall"),"Voice cards offer installation inside the application");
            await Snapshot(Path.Combine(directory,"voice-packs.png"));
            var sampleVoice=OfflineSpeech.Voices.First(v=>v.Engine=="antalia");
            if(OfflineSpeech.IsInstalled(OfflineSpeech.ResolveHome(preferences.SpeechHome),sampleVoice))
            {
                var voiceCard=Descendants(Root).OfType<StackPanel>().First(p=>AutomationProperties.GetAutomationId(p)=="VoicePack-"+sampleVoice.Id);
                var selectVoice=Descendants(voiceCard).OfType<Button>().First(b=>AutomationProperties.GetAutomationId(b)=="voicesChoose");
                if(selectVoice.IsEnabled) await Invoke(selectVoice);
                Check(preferences.VoiceId==sampleVoice.Id,"An installed voice can be selected from its package card");
                await Invoke(Descendants(voiceCard).OfType<Button>().First(b=>AutomationProperties.GetAutomationId(b)=="voicesPreview"));
                var sampleDeadline=DateTimeOffset.UtcNow.AddSeconds(20);
                while(voicePackRequest!=null && DateTimeOffset.UtcNow<sampleDeadline) await Task.Delay(50);
                Check(voicePackRequest==null && voicePackageFeedback==T("voicesPreviewDone"),"Voice package preview synthesizes locally and releases the worker");
            }
            string originalSpeechHome=preferences.SpeechHome;
            preferences=preferences with { SpeechHome=Path.GetPathRoot(directory)! }; BuildPage();
            Check(Descendants(PageContent).OfType<TextBlock>().Any(b=>b.Text==T("voicesLocationInvalid")),"Invalid voice home renders a recoverable page instead of throwing");
            preferences=preferences with { SpeechHome=originalSpeechHome };
            foreach(string language in Localizer.Languages)
            {
                preferences=preferences with { Language=language }; page="keying"; Rebuild();
                Check(PageContent.Children.Count>0 && Breadcrumb.Text==T("keyingTitle"),"Keying page translates in "+language);
                page="audio"; audioWorkspace=1; Rebuild();
                Check(Descendants(Root).OfType<TextBox>().Any(b=>AutomationProperties.GetAutomationId(b)=="DecoderText"),"Decoder page translates in "+language);
                if(language=="ar")
                {
                    Check(Descendants(PageContent).OfType<TextBox>().First(b=>Equals(b.Header,T("morseInput"))).FlowDirection==FlowDirection.LeftToRight,"Arabic audio decoder keeps Morse LTR");
                    await Snapshot(Path.Combine(directory,"decoder-arabic.png"));
                }
            }
            preferences=preferences with { Language="tr" }; audioWorkspace=0; Rebuild();
#if MORSE_WINUI
            foreach(string culture in new[]{"tr-TR","en-US","de-DE","ru-RU","ar-SA"}) voices[culture]=MorseCore.AppCore.OfflineSpeech.ForLanguage(culture).Count;
            Check(voices.Count==5 && voices.All(v=>v.Value>0),"Offline neural voice catalog covers five languages");
            Check(voices["tr-TR"]==2,"EMA Lightning and Antalia Mini selectable for Turkish");
            string speechHome=MorseCore.AppCore.OfflineSpeech.ResolveHome();
            if(MorseCore.AppCore.OfflineSpeech.IsInstalled(speechHome,MorseCore.AppCore.OfflineSpeech.Voices[0]))
            {
                foreach(bool fromMorse in new[]{false,true})
                {
                    page="audio"; audioIsMorse=fromMorse; audioSource=fromMorse ? "... --- ..." : "Merhaba."; Rebuild();
                    await ActionButton("speak");
                    var deadline=DateTimeOffset.UtcNow+TimeSpan.FromSeconds(20);
                    while(speaking && DateTimeOffset.UtcNow<deadline) await Task.Delay(100);
                    Check(!speaking && Status.Text==T("ready"),fromMorse ? "Morse to speech button and muted playback" : "Text to speech button and muted playback");
                }
                synthesized=true; decode=false; source="Merhaba dünya!"; result=null;
            }
#endif
            preferences=preferences with { ReduceMotion=false }; page="converter"; Rebuild(); SetCollapsed(true,true); AnimateContent(); await Task.Delay(350);
            Check(Math.Abs(Pane.ActualWidth-72)<2,"Animated menu collapse");
            CheckCollapsedAlignment("tr"); await Snapshot(Path.Combine(directory,"collapsed-dark.png"));
            preferences=preferences with { Theme="light" }; ApplyTheme(); Rebuild(); await Snapshot(Path.Combine(directory,"collapsed-light.png"));
            CheckCollapsedAlignment("tr light");
            preferences=preferences with { Language="ar" }; Rebuild(); await Snapshot(Path.Combine(directory,"collapsed-arabic.png"));
            CheckCollapsedAlignment("ar");
            preferences=preferences with { Language="tr" }; Rebuild();
            SetCollapsed(false,true); await Task.Delay(350); Check(Math.Abs(Pane.ActualWidth-240)<2,"Animated menu expansion");
            await ConvertNow();
            foreach(string language in new[]{"en","de","ru","ar","tr"})
            {
                preferences=preferences with { Language=language }; Rebuild(); await Snapshot(Path.Combine(directory,language+".png"));
                Check(menuButtons.Count==6 && RadioPages.All(key=>radioChildren!.Children.OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b)=="Nav-"+key && AutomationProperties.GetName(b)==T(key))),"Translated radio subpages and main navigation in "+language);
                double firstMenuY=menuButtons[0].TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0,0)).Y;
                double headingY=Breadcrumb.TransformToVisual(Root).TransformPoint(new Windows.Foundation.Point(0,0)).Y;
                Check(Math.Abs(firstMenuY-headingY)*XamlRoot!.RasterizationScale<=2,"Navigation starts level with the page heading without a reserved sidebar header in "+language);
                var save=Descendants(Root).OfType<Button>().First(b=>AutomationProperties.GetAutomationId(b)=="save");
                var host=(FrameworkElement)((FrameworkElement)save.Parent).Parent;
                var transform=save.TransformToVisual(host);
                double start=transform.TransformPoint(new Windows.Foundation.Point(0,0)).X;
                double end=transform.TransformPoint(new Windows.Foundation.Point(save.ActualWidth,0)).X;
                Check(Math.Min(start,end)>=-1 && Math.Max(start,end)<=host.ActualWidth+1,"Save button fits in "+language);
                Check(!Descendants(Root).OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b) is "speak" or "play"),"Converter contains only conversion actions in "+language);
                Check(Root.FlowDirection==FlowDirection.LeftToRight && WindowBar.FlowDirection==FlowDirection.LeftToRight,"Window frame remains LTR in "+language);
                var brandBounds=TitleBrand.TransformToVisual(WindowBar).TransformBounds(new Windows.Foundation.Rect(0,0,TitleBrand.ActualWidth,TitleBrand.ActualHeight));
                Check(Math.Abs(brandBounds.X+brandBounds.Width/2-WindowBar.ActualWidth/2)<1 && Math.Abs(brandBounds.Y+brandBounds.Height/2-(WindowBar.ActualHeight-WindowBar.BorderThickness.Bottom)/2)<1,"Logo and name centered in window title bar in "+language);
                var closePosition=CloseWindowButton.TransformToVisual(WindowBar).TransformPoint(new Windows.Foundation.Point(0,0));
                Check(closePosition.X>=WindowBar.ActualWidth-CloseWindowButton.ActualWidth-1,"Close button stays on the right in "+language);
                var minimizePosition=MinimizeWindowButton.TransformToVisual(WindowBar).TransformPoint(new Windows.Foundation.Point(0,0));
                Check(minimizePosition.X+MinimizeWindowButton.ActualWidth<=closePosition.X+1 && brandBounds.Right<=minimizePosition.X,"Minimize button stays separate from the centered brand and close button in "+language);
            }
            preferences=preferences with { ReduceMotion=true }; SetCollapsed(true,true); Check(Pane.Width==72,"Reduced motion skips menu animation");
            SetCollapsed(false,false); audioWorkspace=0;
            foreach(string theme in new[]{"light","dark"})
            foreach(string language in new[]{"tr","en","de","ru","ar"})
            {
                preferences=preferences with { Language=language,Theme=theme };
                foreach(string feature in new[]{"converter","files","audio","practice","alphabet","keying","glossary","stationProfiles","qsoAssistant","qsoLog","settings"})
                {
                    page=feature; Rebuild(); await Task.Delay(60); Root.UpdateLayout();
                    if(feature=="converter") CheckWorkspaceGutters(language+" "+theme);
                    CheckPageWidth(language+" "+theme+" "+feature);
                    if(language=="tr") await Snapshot(Path.Combine(directory,"layout-"+theme+"-"+feature+".png"));
                }
                foreach(int audioSection in new[]{1,2})
                {
                    page="audio"; audioWorkspace=audioSection; Rebuild(); await Task.Delay(60); Root.UpdateLayout();
                    CheckPageWidth(language+" "+theme+" audio "+audioSection);
                }
                audioWorkspace=0;
            }
            try
            {
                Workspace.Width=640;
                foreach(string language in new[]{"tr","ar"})
                {
                    preferences=preferences with { Language=language };
                    foreach(string feature in new[]{"alphabet","qsoAssistant","settings"})
                    {
                        page=feature; Rebuild();
                        foreach(var section in Descendants(PageContent).OfType<Expander>().ToArray()) section.IsExpanded=true;
                        await Task.Delay(100); Root.UpdateLayout(); CheckPageWidth(language+" narrow "+feature);
                        await Snapshot(Path.Combine(directory,"narrow-"+language+"-"+feature+".png"));
                    }
                }
            }
            finally { Workspace.Width=double.NaN; }
            preferences=preferences with { Language="tr",ReduceMotion=false }; page="converter"; Rebuild();
            Backdrop.SetActive(true);
            Check(Backdrop.IsAnimating==MotionAllowed(),"RAW background motion follows the system animation preference");
            Backdrop.SetActive(false); Check(!Backdrop.IsAnimating,"RAW background animation stops when the application is inactive");
            Backdrop.SetActive(true); Check(Backdrop.IsAnimating==MotionAllowed(),"RAW background animation resumes when the application becomes active");
            Check((contentAnimation!=null)==MotionAllowed(),"Page transition follows the system and application motion preferences");
            await Task.Delay(350);
            Check(contentAnimation==null && PageContent.Opacity==1 && ((TranslateTransform)PageContent.RenderTransform).X==0,"Page transition completes without leaving displaced or transparent content");
            preferences=preferences with { ReduceMotion=true }; AnimateContent();
            Check(contentAnimation==null && PageContent.Opacity==1,"Reduced motion skips the page transition");
            page="settings"; Rebuild();
            var reduceMotion=Descendants(PageContent).OfType<ToggleSwitch>().First(t=>Equals(t.Header,T("reduceMotion")));
            reduceMotion.IsOn=false;
            Check(Backdrop.IsAnimating==MotionAllowed(),"Settings can enable RAW background motion immediately");
            reduceMotion.IsOn=true;
            Check(!Backdrop.IsAnimating,"Settings reduced motion immediately freezes the RAW background");
            byte[] updateBytes=System.Text.Encoding.UTF8.GetBytes("UI update verification fixture");
            string updateName="MorseCore-2.0.0-Windows-x64-Setup.exe";
            string updateRoot="https://github.com/mustafamertakceylan/MorseCore/releases/";
            var available=new ReleaseUpdate(new Version(2,0,0),new Uri(updateRoot+"tag/v2.0.0"),
                new ReleaseAsset(updateName,new Uri(updateRoot+"download/v2.0.0/"+updateName),updateBytes.Length),
                new ReleaseAsset("SHA256SUMS",new Uri(updateRoot+"download/v2.0.0/SHA256SUMS"),200));
            update=available; Navigate("settings"); BuildPage();
            Check(Descendants(Root).OfType<Button>().Any(b=>AutomationProperties.GetAutomationId(b)=="installUpdate"),"Settings offers installation when a verified-download-capable release is available");
            using(var updateClient=new System.Net.Http.HttpClient(new SmokeUpdateHandler(updateBytes,updateName)))
            {
                var operation=DownloadAndInstallUpdate(new UpdateService(updateClient),available);
                var deadline=DateTimeOffset.UtcNow.AddSeconds(5);
                while(updateDialog?.IsPrimaryButtonEnabled!=true && !operation.IsCompleted && DateTimeOffset.UtcNow<deadline) await Task.Delay(50);
                Check(updateDialog?.IsPrimaryButtonEnabled==true,"Update dialog enables installation after download verification");
                Check(updateDialog!.RequestedTheme==ActualTheme && updateDialog.FlowDirection==Shell.FlowDirection,"Update confirmation follows the selected theme and language direction");
                await Snapshot(Path.Combine(directory,"update-confirm.png"),updateDialog);
                updateDialog!.Hide(); await operation;
                Check(!installingUpdate && updateDialog==null,"Dismissing update confirmation leaves the application open");
            }
            foreach(string language in new[]{"en","tr","de","ru","ar"})
            {
                preferences=preferences with { Language=language,Theme="light",ReduceMotion=true,Collapsed=false,Alphabet=language=="ru"?MorseAlphabet.Russian:language=="ar"?MorseAlphabet.Arabic:MorseAlphabet.Latin };
                page="converter"; decode=false; result=null;
                source=language switch { "en"=>"Hello world!","tr"=>"Merhaba dünya!","de"=>"Hallo Welt!","ru"=>"Привет мир",_=>"مرحبا بالعالم" };
                Rebuild(); ContentScroll.ChangeView(null,0,null,true); await ConvertNow();
                Check(result is { Text.Length:>0 } && Breadcrumb.Text==T("converter"),"README conversion example and navigation match "+language);
                await Snapshot(Path.Combine(directory,"readme-converter."+language+".png"),contentOnly:true);
                preferences=preferences with { Theme="dark" }; Rebuild();
                await Snapshot(Path.Combine(directory,"readme-dark."+language+".png"),contentOnly:true);
                preferences=preferences with { Theme="light" }; showPracticeLog=true; appliedLogQuery=null; ResetLogPage(); logShowMap=true; page="qsoLog"; Rebuild();
                await WaitFor(()=>Descendants(PageContent).OfType<Canvas>().Any() && Descendants(PageContent).OfType<Microsoft.UI.Xaml.Shapes.Ellipse>().Any(),"README contact map shows supplied locations in "+language);
                var map=Descendants(PageContent).OfType<Canvas>().First();
                double mapTop=map.TransformToVisual(ContentScroll).TransformPoint(new Windows.Foundation.Point(0,0)).Y+ContentScroll.VerticalOffset;
                ContentScroll.ChangeView(null,Math.Max(0,mapTop-45),null,true);
                await Snapshot(Path.Combine(directory,"readme-map."+language+".png"),contentOnly:true);
            }
        }
        catch(Exception e)
        {
            failure=e.ToString()+(smokeOperationFailure is { } operation ? "\nOperation failure: "+operation : "");
            try { await Snapshot(Path.Combine(directory,"failure.png")); } catch(Exception) { }
        }
        var report=new { Passed=failure is null,Checks=checks,Failure=failure,SplashVisibleMilliseconds=Math.Round(splashVisibleMilliseconds,1),Status=Status.Text,Page=page,LogCall=logCall,AppliedLogCall=appliedLogQuery?.Call,NativeVersion=version,CatalogVoices=voices,SpeechSynthesized=synthesized,MouseDragTested=mouseDragTested,MouseDragEvidence=mouseDragEvidence,WorkingSetMiB=Math.Round(Process.GetCurrentProcess().WorkingSet64/1048576.0,1) };
        await File.WriteAllTextAsync(Path.Combine(directory,"ui-smoke.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
        var closePeer=new ButtonAutomationPeer(CloseWindowButton);
        ((IInvokeProvider)closePeer.GetPattern(PatternInterface.Invoke)).Invoke();
    }
    private sealed class SmokeUpdateHandler(byte[] payload,string name) : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)
        {
            System.Net.Http.HttpContent content=request.RequestUri!.AbsolutePath.EndsWith("/SHA256SUMS",StringComparison.Ordinal) ?
                new System.Net.Http.StringContent(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload))+"  "+name+"\n") : new System.Net.Http.ByteArrayContent(payload);
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content=content,RequestMessage=request });
        }
    }
}
#endif
