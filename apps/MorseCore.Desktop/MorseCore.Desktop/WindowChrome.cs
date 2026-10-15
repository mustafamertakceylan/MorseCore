using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace MorseCore.Desktop;

internal static partial class WindowChrome
{
    internal static void Configure(Window window)
    {
        var appWindow=window.AppWindow;
        if(appWindow.Presenter is not OverlappedPresenter presenter) return;
        presenter.IsResizable=false;
        presenter.IsMaximizable=false;
        presenter.IsMinimizable=true;
#if MORSE_WINUI
        presenter.SetBorderAndTitleBar(true,true);
        window.ExtendsContentIntoTitleBar=true;
        appWindow.TitleBar.PreferredHeightOption=TitleBarHeightOption.Collapsed;
#else
        presenter.SetBorderAndTitleBar(true,false);
#endif
        var size=new SizeInt32 { Width=1440,Height=900 };
#if MORSE_WINUI
        var iconPath=Path.Combine(AppContext.BaseDirectory,"icon.ico");
        if(File.Exists(iconPath)) appWindow.SetIcon(iconPath);
        var work=DisplayArea.GetFromWindowId(appWindow.Id,DisplayAreaFallback.Nearest).WorkArea;
        size=new SizeInt32 { Width=Math.Min(size.Width,Math.Max(1,work.Width-32)),Height=Math.Min(size.Height,Math.Max(1,work.Height-32)) };
#endif
        appWindow.Resize(size);
#if MORSE_WINUI
        appWindow.Move(new PointInt32 { X=work.X+(work.Width-size.Width)/2,Y=work.Y+(work.Height-size.Height)/2 });
#endif
        bool adjusting=false;
        void Constrain(AppWindow sender,AppWindowChangedEventArgs change)
        {
            if(adjusting) return;
            if(presenter.State==OverlappedPresenterState.Minimized) return;
            if(change.DidPositionChange && !change.DidSizeChange && !change.DidPresenterChange) return;
            adjusting=true;
            try
            {
                if(presenter.State==OverlappedPresenterState.Maximized) presenter.Restore();
                if(sender.Size.Width!=size.Width || sender.Size.Height!=size.Height) sender.Resize(size);
            }
            finally { adjusting=false; }
        }
        appWindow.Changed+=Constrain;
        window.Closed+=(_,_)=>appWindow.Changed-=Constrain;
    }
    internal static void Minimize(Window window)
    {
        if(window.AppWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize();
    }
}
