# Runtime license sources

The portable desktop pack collects original license/notice files from its published NuGet dependencies and includes their `.nuspec` metadata and copyright statements. These supplemental files cover packages whose NuGet archives omit a complete license text:

- `MIT.txt`: verbatim from Microsoft.NETCore.App.Runtime.win-x64 10.0.12, `LICENSE.TXT`; individual package copyright statements accompany it in the generated inventory.
- `CommonServiceLocator-LICENSE`: [upstream MS-PL](https://github.com/unitycontainer/commonservicelocator/blob/master/LICENSE); CommonServiceLocator 2.0.5's NuGet metadata identifies this license.
- `Uno-THIRD-PARTY-NOTICES.md`: [Uno commit da217df2312e9e39676880f9cb4b8d519cc297e0](https://github.com/unoplatform/uno/blob/da217df2312e9e39676880f9cb4b8d519cc297e0/THIRD-PARTY-NOTICES.md), matching Uno.WinUI 6.7.135's package repository metadata.
- `Windows-SDK-LICENSE.rtf`: the license URL declared by Microsoft.Windows.SDK.NET.Ref 10.0.26100.57, [aka.ms/WinSDKLicenseURL](https://aka.ms/WinSDKLicenseURL), resolved to Microsoft's original SDK license RTF.
- `LibVLCSharp-LICENSE`: verbatim LGPL-2.1 text from [VideoLAN commit 1f84e0679cd48b73c3a3f6176c45a3c554398b6a](https://github.com/videolan/libvlcsharp/blob/1f84e0679cd48b73c3a3f6176c45a3c554398b6a/LICENSE), matching LibVLCSharp 3.9.7.1's NuGet metadata. Uno's X11 host references this unmodified managed library; MorseCore audio uses its own SDL2 adapter. The package preserves a separate replaceable `LibVLCSharp.dll`; matching library sources are available at the pinned [source tree](https://github.com/videolan/libvlcsharp/tree/1f84e0679cd48b73c3a3f6176c45a3c554398b6a).

Apache-2.0 dependencies also receive the complete [Apache license](../../LICENSE). These files are license records; they do not change the original terms or claim that all dependencies use MorseCore's license. The packaged `licenses/dependencies.json` identifies the exact runtime versions in each build.

`Uno.Diagnostics.Eventing-LICENSE.md` comes from the official [2.0 source tag at commit 60b69b8d743d612350cbd3214a7211159e6f0d4f](https://github.com/unoplatform/Uno.Diagnostics.Eventing/blob/60b69b8d743d612350cbd3214a7211159e6f0d4f/License.md). The 2.0.1 NuGet package identifies this repository but omits a complete license and source commit; that distinction is retained here.
