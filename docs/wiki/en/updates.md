# Updates and installation problems

Open **Settings → Updates** to see the installed version and check for a newer release. Optional startup checks can be enabled there. Checking alone never installs an update. GitHub commits are not updates: a newer published release with a compatible package is required.

## Windows

Choose **Download and install** when an update appears. MorseCore downloads the Setup file and verifies its SHA-256 checksum and version. It then asks before closing the app and opening Setup. Follow the wizard to finish. You can also download Setup from the [official release page]({{RELEASE_URL}}) and install over your existing copy.

## Mac

Download the offered package, then choose **Open installer** or **Show in Finder**. The download's SHA-256 is checked first. For a DMG, quit MorseCore yourself and drag the new app into Applications, replacing the old copy. The running app is never silently replaced. Preferences, QSO records and voices remain outside the app bundle.

## If something goes wrong

- **No update appears:** compare versions on the release page. The same version is a reinstall, not an update. A platform package may not yet be available.
- **Offline or download failed:** keep using your current app. Check your connection and free space, then retry. Use **Cancel** to stop a pending operation.
- **Verification failed:** do not open that file. Retry from the app or download again from the official release.
- **First launch blocked:** follow the platform-specific steps in [Installation](installation.md).

Still stuck? [Open an issue]({{REPO_URL}}/issues) with your app version, operating system, exact error and steps to reproduce. Remove personal paths, messages and QSO details from screenshots or logs before sharing.
