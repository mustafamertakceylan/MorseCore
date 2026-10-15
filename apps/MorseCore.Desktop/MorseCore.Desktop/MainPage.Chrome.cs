using Microsoft.UI.Xaml.Input;
using Windows.System;
#if MORSE_WINUI
using Microsoft.UI.Input;
using Windows.Foundation;
using Windows.Graphics;
#endif

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private void InitializeChrome()
    {
        var close=new KeyboardAccelerator { Key=VirtualKey.F4,Modifiers=VirtualKeyModifiers.Menu };
        close.Invoked+=(_,e)=> { e.Handled=true; App.MainWindow.Close(); };
        Root.KeyboardAccelerators.Add(close);
#if MORSE_WINUI
        TitleDragRegion.Loaded+=(_,_)=>
        {
            UpdateCaptionRegions();
            XamlRoot.Changed+=ChromeRootChanged;
        };
        TitleDragRegion.Unloaded+=(_,_)=> { if(XamlRoot is { } root) root.Changed-=ChromeRootChanged; };
        TitleDragRegion.SizeChanged+=(_,_)=>UpdateCaptionRegions();
        PaneToggle.SizeChanged+=(_,_)=>UpdateCaptionRegions();
#else
        App.MainWindow.SetTitleBar(TitleDragRegion);
#endif
    }
#if MORSE_WINUI
    private void ChromeRootChanged(XamlRoot sender,XamlRootChangedEventArgs e)=>UpdateCaptionRegions();
    private void UpdateCaptionRegions()
    {
        if(XamlRoot is not { } root || TitleDragRegion.ActualWidth<=0 || CloseWindowButton.ActualWidth<=0 || MinimizeWindowButton.ActualWidth<=0) return;
        var title=TitleDragRegion.TransformToVisual(Root).TransformBounds(new Rect(0,0,TitleDragRegion.ActualWidth,TitleDragRegion.ActualHeight));
        var close=CloseWindowButton.TransformToVisual(Root).TransformBounds(new Rect(0,0,CloseWindowButton.ActualWidth,CloseWindowButton.ActualHeight));
        var minimize=MinimizeWindowButton.TransformToVisual(Root).TransformBounds(new Rect(0,0,MinimizeWindowButton.ActualWidth,MinimizeWindowButton.ActualHeight));
        bool menuVisible=PaneToggle.Visibility==Visibility.Visible && PaneToggle.ActualWidth>0;
        var toggle=menuVisible ? PaneToggle.TransformToVisual(Root).TransformBounds(new Rect(0,0,PaneToggle.ActualWidth,PaneToggle.ActualHeight)) : new Rect();
        double scale=root.RasterizationScale;
        RectInt32 Pixels(Rect rect)=>new() { X=(int)Math.Round(rect.X*scale),Y=(int)Math.Round(rect.Y*scale),Width=(int)Math.Round(rect.Width*scale),Height=(int)Math.Round(rect.Height*scale) };
        var input=InputNonClientPointerSource.GetForWindowId(App.MainWindow.AppWindow.Id);
        input.SetRegionRects(NonClientRegionKind.Caption,[Pixels(new Rect(toggle.Right,title.Y,Math.Max(0,minimize.X-toggle.Right),title.Height))]);
        input.SetRegionRects(NonClientRegionKind.Passthrough,menuVisible ? [Pixels(toggle),Pixels(minimize),Pixels(close)] : [Pixels(minimize),Pixels(close)]);
    }
#endif
    private void CloseWindow(object sender,RoutedEventArgs e)=>App.MainWindow.Close();
    private void MinimizeWindow(object sender,RoutedEventArgs e)=>WindowChrome.Minimize(App.MainWindow);
}
