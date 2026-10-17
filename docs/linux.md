# MorseCore on Linux

The Linux desktop shares the Windows XAML interface, C23 engine and
`MorseCore.AppCore` services. Language files, fonts and brand assets have the same
source. The first build targets **x86-64** Linux with glibc; ARM and musl/Alpine
are outside this build.

Download Linux packages from [Releases](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v1.0.0). The application has been
tested in the four virtual machines below, including the shared interface and
all eight offline voices. Physical microphone hardware and a live GitHub upgrade
between two published Linux versions remain unverified.

## Distribution targets and VM results

| Distribution | Package | Tested VM | Package lifecycle |
|---|---|---|---|
| Ubuntu | `.deb` / `.tar.gz` | 24.04.5, GNOME Wayland/XWayland | APT install, remove/reinstall and local fixture upgrade; user data preserved |
| Debian | `.deb` / `.tar.gz` | 12.15, XFCE Xorg | APT install, remove/reinstall and local fixture upgrade; user data preserved |
| Arch Linux | `PKGBUILD` / `.tar.gz` | Rolling system installed from the 2026.10.01 ISO | pacman lifecycle checks passed |
| Pardus | `.deb` / `.tar.gz` | 25.2, XFCE Xorg | APT install, remove/reinstall and local fixture upgrade; user data preserved |

Compatibility targets are Ubuntu 24.04+, Debian 12+, Pardus 23+ and current
Arch Linux. Only the versions listed above were tested; **Pardus 23 remains
untested**. Local upgrade tests used two separately compiled QA versions;
they do not prove migration between two published Linux releases.

All packages contain the same application and self-contained .NET 10 runtime.
No separate .NET installation is needed. The build rejects ELF dependencies
requiring a GLIBC symbol newer than 2.36. This is a compatibility gate, not a
replacement for testing each distribution's libraries.

The desktop host uses X11. A Wayland session needs XWayland. Linux sound uses
SDL2 with the audio backend available on the computer. Python and a speech pack
are optional; conversion and Morse tools do not require a downloaded AI model.
File/folder pickers need a session D-Bus, `xdg-desktop-portal` and a backend
appropriate to the desktop (GTK, GNOME or KDE). On Arch, install that backend
alongside the package. Missing portal services can appear as a cancelled picker.

Linux defaults to software rendering to avoid the capture crash observed with
the VM/Mesa OpenGL path. Continuous background drift is disabled on this path;
static artwork and brief page transitions remain, avoiding continuous redraws
while idle. Developers can compare automatic GPU selection with
`MORSECORE_LINUX_RENDERER=auto morsecore`; that is not the rendering path that
passed the Ubuntu VM acceptance checks.

LTTng is optional and only needed for that .NET diagnostic tracing facility.
The bundled `libcoreclrtraceptprovider.so` remains in the package; its missing
LTTng libraries produce a diagnostic note rather than preventing normal app
use. Required runtime, graphics, SDL and app libraries are still checked.
See the [.NET runtime explanation](https://github.com/dotnet/runtime/discussions/116386).

## Install

Download the `.deb`, portable archive or `PKGBUILD` from [Releases](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v1.0.0).
Use the version in the filename in place of `VERSION` below.

Ubuntu, Debian and Pardus:

```bash
sudo apt install ./MorseCore-VERSION-Linux-x64.deb
morsecore
```

APT installs declared system-library dependencies. Optional offline speech
packages need Python 3 and venv support (`python3` and `python3-venv` on these
distributions); install those only if you want speech voices. See
[offline speech](offline-speech.md) for voice models and their separate licenses.

Portable package, without a system installation:

```bash
tar -xzf MorseCore-VERSION-Linux-x64.tar.gz
cd MorseCore-VERSION-Linux-x64
./morsecore --check-dependencies
./morsecore
```

The portable archive does not install missing libraries or register a menu
entry. Keep the extracted directory together; do not copy its executable alone.
If dependencies are missing, the `.deb` is the simpler route on APT systems.

Arch Linux: place the generated `PKGBUILD` beside the matching `.tar.gz`, inspect
it, then build it as a normal user:

```bash
makepkg -si
```

The recipe checks the upstream tarball's SHA-256 and declares system libraries.
It is a local packaging recipe, not a published AUR package.

### Optional voices when system Python is too new

Voice engines require **64-bit Python 3.11–3.13** with venv and ensurepip.
The tested Arch system ships Python 3.14.8; its voice tests used a separate
Python 3.12.15 without changing system Python. Ubuntu, Debian and Pardus were
tested with their supported distribution Python versions.

One option is [uv](https://docs.astral.sh/uv/getting-started/installation/).
After installing uv, run these commands as your normal user:

```bash
uv python install 3.12
export PATH="$HOME/.local/bin:$PATH"
morsecore
```

With uv's default directories, this exposes `python3.12` to the application
launched from that terminal. For a portable copy, use `./morsecore` instead.
Do not use `--default`, `--force`, `sudo`, or replace `/usr/bin/python`.
See the [uv Python guide](https://docs.astral.sh/uv/concepts/python-versions/#installing-python-executables).

MorseCore searches its existing voice environment, `/usr/bin`, `/usr/local/bin`
and inherited PATH; it does not search uv's private installation directories.
`uv python update-shell` can add the user executable directory to your shell
configuration. Open a new terminal afterwards. A desktop menu launch may not
inherit that shell PATH, even after logging in again: until your desktop session
also has the versioned executable on PATH, launch MorseCore from the terminal
above to install voices. Voice dependencies stay in MorseCore's own environment.

## Updates and removal

For `.deb`, install the newer `.deb` with `apt install` again. For Arch, use the
matching new recipe and archive with `makepkg -si`. The application can check a
GitHub release and verify its portable archive; it does not run a privileged
package-manager operation. Review the version before installing it.

For a portable update, exit MorseCore and extract the new archive into a new
directory. Keep preferences, voice packages and radio records outside the
application directory; the normal app paths use the user's XDG directories.
Do not run the application with `sudo`.

```bash
# APT package:
sudo apt remove morsecore
# Arch package:
sudo pacman -R morsecore-bin
```

Removing the app package preserves user preferences, downloaded voices and QSO
records. A portable copy can be removed by deleting its extracted application
directory. Back up records before deliberately deleting application data.

## Build and check a package

Use Linux x86-64, a final C23 compiler accepted by the CMake feature probe,
CMake 3.28+, Ninja, .NET SDK 10, Python 3 and binutils. `dpkg-deb` is additionally
needed to produce a `.deb`. The required compiler feature probe, not a compiler
brand/version alone, determines whether the native build is usable.

```bash
bash scripts/package-linux.sh --deb
bash scripts/test-linux-package.sh \
  build/release-linux/MorseCore-VERSION-Linux-x64.tar.gz \
  build/release-linux/MorseCore-VERSION-Linux-x64.deb
```

Set `MORSECORE_LINUX_GUI_SMOKE=1` for the optional isolated startup smoke on an
X11 desktop. It checks that the process remains running, not visual correctness
or sound hardware. The test extracts a package into a temporary directory; it
does not install or remove system packages and does not use normal user records.

To package an existing publish, use `--skip-build` with
`MORSECORE_LINUX_PUBLISH_DIR`. `MORSECORE_PACKAGE_OUTPUT` chooses a fresh output
directory. Existing archives, recipes and checksums are never overwritten.
`MORSECORE_LINUX_BUILD_DIR`, `NUGET_PACKAGES` and `CC` allow an isolated toolchain.
Build on a sufficiently old base; a newer glibc cannot be made portable simply
by copying libraries from another distribution.

Outputs: portable archive, `PKGBUILD`, Linux-specific SHA-256 manifest, optional
`.deb`. `BUILD-INFO.json` inside the package records the source commit, whether
the source tree was dirty, version and highest required GLIBC symbol. Licenses
and notices for the published dependencies are collected from the NuGet cache.

## VM acceptance checklist

- Fresh APT install, launcher/menu/icon, removal without deleting records.
- Update from an older package, SHA mismatch rejection, settings preserved.
- All pages in light/dark mode; Turkish, English, German, Russian and Arabic;
  Arabic menus RTL and Morse fields LTR.
- File picker, UTF-8 conversions, export and radio-log import/export with
  synthetic data.
- Playback, stop, WAV export, microphone permission/recording, offline speech.
- X11 and Wayland/XWayland; scaling and missing-library failure messages.
- Repeat checks when the package or distribution changes. Pardus 25.2 results
  do not establish Pardus 23 support.

Runtime requirements are based on [Uno Skia Desktop](https://platform.uno/docs/articles/features/using-skia-desktop.html)
and [.NET Linux dependencies](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install).
The exact package lists are maintained once in
[`packaging/linux/dependencies.json`](../packaging/linux/dependencies.json).
