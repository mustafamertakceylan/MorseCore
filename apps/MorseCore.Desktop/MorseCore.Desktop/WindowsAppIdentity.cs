#if MORSE_WINUI
using System.Runtime.InteropServices;

namespace MorseCore.Desktop;

internal static class WindowsAppIdentity
{
    // Keep in sync with InstallerId and the application shortcuts in MorseCore.iss.
    internal const string Id="io.github.mustafamertakceylan.morsecore.desktop";
    internal static void Configure()=>Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(Id));

#if DEBUG
    internal static string? Current()
    {
        Marshal.ThrowExceptionForHR(GetCurrentProcessExplicitAppUserModelID(out var value));
        try { return Marshal.PtrToStringUni(value); }
        finally { Marshal.FreeCoTaskMem(value); }
    }

    [DllImport("shell32.dll",ExactSpelling=true)]
    private static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr value);
#endif

    [DllImport("shell32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string value);
}
#endif
