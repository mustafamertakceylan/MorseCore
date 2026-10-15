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
using Windows.Graphics.Imaging;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
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
    private async Task RunUiSmoke(string directory)
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
            foreach(string feature in new[]{"files","audio","practice","alphabet","settings"})
            { Navigate(feature); await Snapshot(Path.Combine(directory,feature+".png")); Check(PageContent.Children.Count>0,feature+" page rendered"); }
            var credits=Descendants(Root).OfType<Expander>().First(e=>AutomationProperties.GetAutomationId(e)=="OpenSourceCredits");
            credits.IsExpanded=true; await Task.Delay(150); Root.UpdateLayout();
            var creditLinks=Descendants(credits).OfType<HyperlinkButton>().ToArray();
            Check(creditLinks.Length==7 && creditLinks.All(link=>link.NavigateUri?.Scheme=="https"),"Acknowledgements contain six project links and voice model credits");
            Check(creditLinks.Any(link=>Equals(link.Content,"EMA Lightning")) && creditLinks.Any(link=>Equals(link.Content,"Antalia-2 Mini")),"Local Turkish AI authors acknowledged in settings");
            ContentScroll.ChangeView(null,ContentScroll.ScrollableHeight,null,true); await Snapshot(Path.Combine(directory,"credits.png"));
            Navigate("practice"); await Task.Delay(100); await ActionButton("start");
            Check(session!=null && practiceQuestion!=null,"Practice start button");
            var question=practiceQuestion!;
            Navigate("settings");
            var themeChoice=Descendants(Root).OfType<ComboBox>().First(c=>AutomationProperties.GetAutomationId(c)=="ThemeChoice");
            themeChoice.SelectedIndex=2; await Task.Delay(100);
            Check(preferences.Theme=="dark" && ActualTheme==ElementTheme.Dark && store.Load().Theme=="dark","Theme changes and persists from Settings");
            Navigate("practice");
            Check(ReferenceEquals(question,practiceQuestion),"Practice question survives navigation and theme changes");
            var answer=Descendants(Root).OfType<TextBox>().First(b=>Equals(b.Header,T("answer")));
            answer.Text=MorseEngine.Decode(practiceQuestion!.Morse).Text; await ActionButton("submit");
            Check(session!.Statistics.Correct==1 && session.Statistics.Answered==1,"Practice submit button and score");
#if MORSE_WINUI
            foreach(string culture in new[]{"tr-TR","en-US","de-DE","ru-RU","ar-SA"}) voices[culture]=MorseCore.AppCore.OfflineSpeech.ForLanguage(culture).Count;
            Check(voices.Count==5 && voices.All(v=>v.Value>0),"Offline neural voice catalog covers five languages");
            Check(voices["tr-TR"]==2,"EMA Lightning and Antalia Mini selectable for Turkish");
            string speechHome=MorseCore.AppCore.OfflineSpeech.ResolveHome();
            if(MorseCore.AppCore.OfflineSpeech.IsInstalled(speechHome,MorseCore.AppCore.OfflineSpeech.Voices[0]))
            {
                foreach(bool fromMorse in new[]{false,true})
                {
                    page="converter"; decode=fromMorse; source=fromMorse ? "... --- ..." : "Merhaba."; result=null; Rebuild();
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
            SetCollapsed(false,true); await Task.Delay(350); Check(Math.Abs(Pane.ActualWidth-216)<2,"Animated menu expansion");
            await ConvertNow();
            foreach(string language in new[]{"en","de","ru","ar","tr"})
            {
                preferences=preferences with { Language=language }; Rebuild(); await Snapshot(Path.Combine(directory,language+".png")); Check(Menu.Children.Count==5,"Navigation in "+language);
                var save=Descendants(Root).OfType<Button>().First(b=>AutomationProperties.GetAutomationId(b)=="save");
                var host=(FrameworkElement)((FrameworkElement)save.Parent).Parent;
                var transform=save.TransformToVisual(host);
                double start=transform.TransformPoint(new Windows.Foundation.Point(0,0)).X;
                double end=transform.TransformPoint(new Windows.Foundation.Point(save.ActualWidth,0)).X;
                Check(Math.Min(start,end)>=-1 && Math.Max(start,end)<=host.ActualWidth+1,"Save button fits in "+language);
                var speak=Descendants(Root).OfType<Button>().First(b=>AutomationProperties.GetAutomationId(b)=="speak");
                var position=speak.TransformToVisual(ContentScroll).TransformPoint(new Windows.Foundation.Point(0,speak.ActualHeight));
                Check(position.Y<=ContentScroll.ActualHeight+1,"Speech action visible without scrolling in "+language);
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
        catch(Exception e) { failure=e.ToString(); }
        var report=new { Passed=failure is null,Checks=checks,Failure=failure,NativeVersion=version,CatalogVoices=voices,SpeechSynthesized=synthesized,MouseDragTested=mouseDragTested,MouseDragEvidence=mouseDragEvidence,WorkingSetMiB=Math.Round(Process.GetCurrentProcess().WorkingSet64/1048576.0,1) };
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
