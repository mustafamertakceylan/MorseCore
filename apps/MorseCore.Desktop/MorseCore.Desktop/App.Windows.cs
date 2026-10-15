#if MORSE_WINUI
using Microsoft.Windows.AppLifecycle;
using AppInstance=Microsoft.Windows.AppLifecycle.AppInstance;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;

namespace MorseCore.Desktop;

public partial class App
{
    private AppInstance? windowsInstance;
    private DispatcherQueue? windowsDispatcher;

    private async Task<bool> RegisterWindowsInstance()
    {
        windowsDispatcher=DispatcherQueue.GetForCurrentThread();
        windowsInstance=AppInstance.FindOrRegisterForKey("MorseCore.Main");
        if(!windowsInstance.IsCurrent)
        {
            await windowsInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            return false;
        }
        windowsInstance.Activated+=OnWindowsActivation;
        return true;
    }

    private void OnWindowsActivation(object? sender,AppActivationArguments args)
    {
        windowsDispatcher?.TryEnqueue(()=>
        {
            // The first launch may still be constructing its window.
            if(MainWindow is null) return;
            if(MainWindow.AppWindow.Presenter is OverlappedPresenter presenter &&
                presenter.State==OverlappedPresenterState.Minimized) presenter.Restore();
            MainWindow.Activate();
        });
    }

    private void ReleaseWindowsInstance()
    {
        if(windowsInstance is not { IsCurrent:true }) return;
        windowsInstance.Activated-=OnWindowsActivation;
        windowsInstance.UnregisterKey();
    }
}
#endif
