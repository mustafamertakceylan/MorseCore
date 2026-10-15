namespace MorseCore.Desktop;

public sealed partial class MainPage
{
#if MORSE_WINUI
    private Windows.UI.ViewManagement.UISettings? backdropSystemSettings;
#endif
    private void InitializeBackdrop()
    {
        App.MainWindow.Activated+=BackdropWindowActivated;
#if MORSE_WINUI
        backdropSystemSettings=new();
        if(OperatingSystem.IsWindowsVersionAtLeast(10,0,19041)) backdropSystemSettings.AnimationsEnabledChanged+=BackdropSystemMotionChanged;
#endif
        UpdateBackdropMotion();
    }
    private void BackdropWindowActivated(object sender,WindowActivatedEventArgs e)
    {
#if MORSE_WINUI
        Backdrop.SetActive(e.WindowActivationState!=WindowActivationState.Deactivated);
#else
        Backdrop.SetActive(e.WindowActivationState!=Windows.UI.Core.CoreWindowActivationState.Deactivated);
#endif
    }
    private void UpdateBackdropMotion() { if(!disposed) Backdrop.SetMotionEnabled(MotionAllowed()); }
#if MORSE_WINUI
    private void BackdropSystemMotionChanged(Windows.UI.ViewManagement.UISettings sender,object args)=>DispatcherQueue.TryEnqueue(()=>UpdateBackdropMotion());
#endif
    private void CleanupBackdrop()
    {
        App.MainWindow.Activated-=BackdropWindowActivated;
#if MORSE_WINUI
        if(OperatingSystem.IsWindowsVersionAtLeast(10,0,19041) && backdropSystemSettings!=null) backdropSystemSettings.AnimationsEnabledChanged-=BackdropSystemMotionChanged;
#endif
        Backdrop.SetMotionEnabled(false);
    }
}
