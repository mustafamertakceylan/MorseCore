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
    private async Task Snapshot(string path,UIElement? element=null)
    {
        await Task.Delay(250); Root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(); await bitmap.RenderAsync(element ?? Root);
        var pixels=await bitmap.GetPixelsAsync();
        using var stream=File.Create(path);
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,stream.AsRandomAccessStream());
        encoder.SetPixelData(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,(uint)bitmap.PixelWidth,(uint)bitmap.PixelHeight,96,96,pixels.ToArray());
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
        async Task WaitFor(Func<bool> condition,string description)
        {
            var deadline=DateTimeOffset.UtcNow.AddSeconds(5);
            while(!condition() && DateTimeOffset.UtcNow<deadline) await Task.Delay(50);
            Check(condition(),description);
        }
        void CheckCollapsedAlignment(string language)
        {
            Root.UpdateLayout();
            double Center(FrameworkElement element)
            {
                var bounds=element.TransformToVisual(Pane).TransformBounds(new Windows.Foundation.Rect(0,0,element.ActualWidth,element.ActualHeight));
                return bounds.X+bounds.Width/2;
            }
            double center=Center((FontIcon)PaneToggle.Content);
            Check(menuButtons.All(b=>Math.Abs(Center((FontIcon)((StackPanel)b.Content).Children[0])-center)<1),"Collapsed navigation and settings icons align with menu toggle in "+language);
            Check(Math.Abs(center-(Pane.ActualWidth-Pane.BorderThickness.Right+Pane.BorderThickness.Left)/2)<1,"Collapsed icons stay centered in "+language);
        }
        try
        {
            Check(Splash.Visibility==Visibility.Collapsed && LoadingProgress.Value==100,"Loading screen completes before main interaction");
            Check(splashVisibleMilliseconds>=1950,"Welcome screen remains visible for at least two seconds before dismissal");
#if MORSE_WINUI
            var presenter=(Microsoft.UI.Windowing.OverlappedPresenter)App.MainWindow.AppWindow.Presenter;
            Check(App.MainWindow.ExtendsContentIntoTitleBar && App.MainWindow.AppWindow.TitleBar.PreferredHeightOption==Microsoft.UI.Windowing.TitleBarHeightOption.Collapsed && App.MainWindow.AppWindow.TitleBar.Height==0,"System caption controls hidden");
            Check(!presenter.IsResizable && !presenter.IsMaximizable && !presenter.IsMinimizable,"Fixed window presenter flags");
            var fixedSize=App.MainWindow.AppWindow.Size;
            App.MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32(fixedSize.Width+80,fixedSize.Height+60)); await Task.Delay(150);
            Check(App.MainWindow.AppWindow.Size.Equals(fixedSize),"Window dimensions stay locked after a resize request");
            presenter.Maximize(); await Task.Delay(150);
            Check(presenter.State==Microsoft.UI.Windowing.OverlappedPresenterState.Restored && App.MainWindow.AppWindow.Size.Equals(fixedSize),"Maximize request cannot change fixed window");
            presenter.Minimize(); await Task.Delay(150);
            Check(presenter.State==Microsoft.UI.Windowing.OverlappedPresenterState.Restored,"Minimize request is restored");
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
                mouseDragTested=true;
            }
            App.MainWindow.AppWindow.Move(originalPosition);
#endif
            preferences=preferences with { Theme="light",Language="tr",Alphabet=MorseAlphabet.Latin,ReduceMotion=true,Collapsed=false,CheckUpdates=false };
            ApplyTheme(); source="Merhaba dünya!"; Rebuild(); await ConvertNow();
            Check(result?.Text=="-- . .-. .... .- -... .- / -.. ..- -. -.-- .- -.-.-- ","Turkish conversion through native DLL");
            Check(result!.Statistics.InputCharacters==14 && result.Statistics.Words==2,"Unicode character and word counters");
            await Snapshot(Path.Combine(directory,"light.png"));
            Check(TitleLogo.Source is BitmapImage { PixelWidth: >0,PixelHeight: >0 },"Supplied logo loads in the application title bar");
            preferences=preferences with { Theme="dark" }; ApplyTheme(); Rebuild(); await Snapshot(Path.Combine(directory,"dark.png"));
            Check(ActualTheme==ElementTheme.Dark,"Dark theme applied");
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
            Navigate("radio");
            var callInput=Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="callSign");
            callInput.Text="TA1PDM"; await Task.Delay(100); await ActionButton("analyzeCall");
            Check(stationDraft.CountryId=="TR" && stationDraft.ClassId=="A","Station recognition selects Turkish class A and region");
            Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="operatorName").Text="Ali";
            Descendants(Root).OfType<TextBox>().First(b=>AutomationProperties.GetAutomationId(b)=="qth").Text="Ankara";
            await ActionButton("saveStation");
            Check(ActiveStation?.CallSign=="TA1PDM" && radioStore.LoadProfiles().Profiles.Count==1,"Station profile saves through UI");
            radioTab=1; BuildPage(); await ActionButton("prepareMessage");
            Check(qsoPreview=="QRL? DE TA1PDM K" && qsoMorse.Contains(" / ",StringComparison.Ordinal),"QRL message prepares Latin calls and Morse");
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
            await ActionButton("saveQso");
            Check(radioStore.Recent(true).Count==1 && radioStore.Recent(false).Count==0,"UI saves a practice QSO separately");
            var kind=Descendants(Root).OfType<ToggleSwitch>().First(b=>Equals(b.Header,T("practiceRecord"))); kind.IsOn=false;
            await ActionButton("saveQso");
            Check(radioStore.Recent(false).Count==1 && radioStore.Recent(false)[0].Utc.Offset==TimeSpan.Zero,"External radio contact logs in UTC");
            radioTab=2; BuildPage(); await Snapshot(Path.Combine(directory,"qso-log.png"));
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
            Descendants(PageContent).OfType<Expander>().First(e=>Equals(e.Header,T("logbook.filters"))).IsExpanded=true; Root.UpdateLayout();
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
            preferences=preferences with { Language="ar" }; Rebuild(); radioTab=1; BuildPage(); await Snapshot(Path.Combine(directory,"qso-arabic.png"));
            Check(qsoTextBox!.FlowDirection==FlowDirection.LeftToRight && qsoCodeBox!.FlowDirection==FlowDirection.LeftToRight,"Arabic QSO calls and Morse stay LTR");
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
                preferences=preferences with { Language=language }; Rebuild(); await Snapshot(Path.Combine(directory,language+".png")); Check(Menu.Children.Count==6 && menuButtons.Count==6,"Navigation in "+language);
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
            }
            preferences=preferences with { ReduceMotion=true }; SetCollapsed(true,true); Check(Pane.Width==72,"Reduced motion skips menu animation");
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
        }
        catch(Exception e)
        {
            failure=e.ToString();
            try { await Snapshot(Path.Combine(directory,"failure.png")); } catch(Exception) { }
        }
        var report=new { Passed=failure is null,Checks=checks,Failure=failure,SplashVisibleMilliseconds=Math.Round(splashVisibleMilliseconds,1),Status=Status.Text,Page=page,RadioTab=radioTab,LogCall=logCall,AppliedLogCall=appliedLogQuery?.Call,NativeVersion=version,CatalogVoices=voices,SpeechSynthesized=synthesized,MouseDragTested=mouseDragTested,MouseDragEvidence=mouseDragEvidence,WorkingSetMiB=Math.Round(Process.GetCurrentProcess().WorkingSet64/1048576.0,1) };
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
