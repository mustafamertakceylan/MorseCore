# Runtime license sources

The portable desktop pack collects original license/notice files from its published NuGet dependencies and includes their `.nuspec` metadata and copyright statements. These supplemental files cover packages whose NuGet archives omit a complete license text:

- `MIT.txt`: verbatim from Microsoft.NETCore.App.Runtime.win-x64 10.0.12, `LICENSE.TXT`; individual package copyright statements accompany it in the generated inventory.
- `CommonServiceLocator-LICENSE`: [upstream MS-PL](https://github.com/unitycontainer/commonservicelocator/blob/master/LICENSE); CommonServiceLocator 2.0.5's NuGet metadata identifies this license.
- `Uno-THIRD-PARTY-NOTICES.md`: [Uno commit da217df2312e9e39676880f9cb4b8d519cc297e0](https://github.com/unoplatform/uno/blob/da217df2312e9e39676880f9cb4b8d519cc297e0/THIRD-PARTY-NOTICES.md), matching Uno.WinUI 6.7.135's package repository metadata.
- `Windows-SDK-LICENSE.rtf`: the license URL declared by Microsoft.Windows.SDK.NET.Ref 10.0.26100.57, [aka.ms/WinSDKLicenseURL](https://aka.ms/WinSDKLicenseURL), resolved to Microsoft's original SDK license RTF.

Apache-2.0 dependencies also receive the complete [Apache license](../../LICENSE). These files are license records; they do not change the original terms or claim that all dependencies use MorseCore's license. The packaged `licenses/dependencies.json` identifies the exact runtime versions in each build.
