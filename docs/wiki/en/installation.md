# Installation

Download MorseCore **{{VERSION}}** from the [official release page]({{RELEASE_URL}}). Choose the package for your computer; voice models are optional downloads.

## Windows

Windows x64 is supported; Setup requires Windows 10 version 2004 or later.

1. Download and open [Windows Setup]({{WINDOWS_SETUP_URL}}).
2. Follow the wizard and choose any shortcuts you want.
3. Open MorseCore from the Start menu.

Setup installs for your user account without administrator access. The required .NET and Windows App SDK files are included. If a compatible Python is missing, Setup offers it as an optional component for speech. You can decline and still convert text and play Morse audio.

The installer is unsigned, so Windows may show a publisher warning. Check that the download came from this project's official release.

Prefer no installation? Extract the entire [portable ZIP]({{WINDOWS_ZIP_URL}}), then open **MorseCore.Desktop.exe**. Keep its files together.

## Mac

The [Mac DMG]({{MAC_DMG_URL}}) supports **Apple Silicon (M1 or newer)** and targets macOS 14 or later. There is no Intel package yet.

1. Open the DMG and drag **MorseCore** onto **Applications**.
2. Eject the disk image and open the app from Applications.
3. The package is not notarized. If blocked, follow [Apple's per-app approval instructions](https://support.apple.com/en-us/102445): **System Settings → Privacy & Security → Open Anyway**, when available. Managed Macs may restrict this.

## Remove or reinstall

On Windows, uninstall through **Settings → Apps**. On Mac, quit the app and move it to Trash. Preferences, QSO records and downloaded voices are retained; back up important records before changing computers. Exported files stay where you saved them.

Linux is planned; there is no Linux desktop package yet. Continue with [Audio](audio.md) or [Updates](updates.md).
