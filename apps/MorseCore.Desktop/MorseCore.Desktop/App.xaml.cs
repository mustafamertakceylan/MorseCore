namespace MorseCore.Desktop;

public partial class App : Application
{
    public static Window MainWindow { get; private set; } = null!;
    public App()
    {
        UnhandledException+=(_,e)=>
        {
            string? directory=Environment.GetEnvironmentVariable("MORSECORE_DIAGNOSTICS_DIR");
            if(string.IsNullOrEmpty(directory) || !Path.IsPathFullyQualified(directory) || !Directory.Exists(directory)) return;
            try { File.WriteAllText(Path.Combine(directory,"crash.txt"),e.Exception?.ToString() ?? e.Message); }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        };
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow=new Window { Title="MorseCore" };
        var page=new MainPage(); MainWindow.Content=page;
        WindowChrome.Configure(MainWindow);
        MainWindow.Closed+=(_,_)=>page.Shutdown();
        MainWindow.Activate();
    }
    public static void InitializeLogging() { }
}
