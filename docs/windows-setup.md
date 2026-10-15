# Windows Setup

[English README](../README.md) · [Türkçe README](../README.tr.md)

## English

Run **MorseCore-0.4.1-Windows-x64-Setup.exe**. The wizard supports English, Turkish, German, Russian and Arabic. It installs for the current user in `%LOCALAPPDATA%\Programs\MorseCore`, adds Start menu shortcuts and offers an optional desktop shortcut. Administrator rights are not required. The .NET and Windows App SDK runtime files are included.

This installer is unsigned; Windows may show an unknown-publisher warning. Trusted signing and MSIX distribution are separate future work.

- **Requirements:** .NET/Windows App SDK are included. Setup detects working Python 3.11–3.13 x64 (including venv/pip). If missing, an unchecked option offers the bundled official Python 3.13.16 installer. You may decline and still use MorseCore. Python installs per user without changing PATH, file associations or other Python versions.
- **Speech:** choose the optional voice download at the end of Setup, or use the Start menu shortcut later. Internet is required once; models then work offline from `%LOCALAPPDATA%\MorseCore\Speech`. Models are not bundled. Python remains separately uninstallable; voice environments depend on it.
- **Updates:** run a newer Setup over the existing installation. Its path and preferences are retained. In Settings, Download and install downloads the matching GitHub Release Setup and SHA256SUMS, verifies its hash and version, then asks before closing the app and opening Setup. Checks alone never install updates; commits without a published release are not updates. Older installers refuse to replace a newer registered version.
- **Uninstall:** use Windows Settings → Apps. Preferences, voice models and user-created files are retained. Desktop settings are in `%LOCALAPPDATA%\MorseCore\Desktop`.

### Build and check

In a PowerShell 7 terminal with the [desktop build tools](desktop.md#run-from-source):

```powershell
pwsh -File scripts/package-desktop-installer.ps1
pwsh -File scripts/test-desktop-installer.ps1 -InstallerPath "<output>\MorseCore-0.4.1-Windows-x64-Setup.exe"
```

The build produces a desktop ZIP, Setup.exe and `SHA256SUMS` in a unique `cmake-build-desktop-installer/` folder. It downloads Inno Setup 7.1.0 from its official release if needed, verifies its pinned SHA256 and publisher signature, then installs the compiler in the ignored project tools folder. An existing compiler can be selected with `-IsccPath`. The official Python installer is also downloaded at build time and checked against its pinned SHA256 and Python Software Foundation signature; it is embedded so installing Python needs no internet.

The check requires no existing MorseCore installation. It uses an isolated Unicode/spaced path, checks installed licenses, shortcuts and the native API, launches the installed release, then checks reinstall, downgrade refusal and removal. Add `-PreviousInstallerPath "<older-Setup.exe>"` to exercise a real upgrade from an earlier release. CI uses `-SkipLaunch`; a successful CI installer check alone does not prove GUI startup. Evidence remains in `cmake-build-installer-tests/`.

`pwsh -File scripts/test-live-update.ps1 -PreviousInstallerPath "<older-Setup.exe>"` tests the production updater against the real GitHub latest release, completes its interactive wizard and checks the upgraded app. Use a baseline older than the published release. `pwsh -File scripts/test-installer-prerequisites.ps1` checks present/missing/declined/failed/accepted Python using isolated discovery keys and the production prerequisite code; it requires a compatible existing Python and no Python 3.13 installation, and removes its temporary Python afterward. Both are opt-in local tests.

## Türkçe

**MorseCore-0.4.1-Windows-x64-Setup.exe** dosyasını açın. Sihirbaz Türkçe, İngilizce, Almanca, Rusça ve Arapça destekler. Uygulama mevcut kullanıcı için `%LOCALAPPDATA%\Programs\MorseCore` klasörüne kurulur. Başlat menüsü kısayolları eklenir; masaüstü kısayolu isteğe bağlıdır. Yönetici yetkisi gerekmez. .NET ve Windows App SDK çalışma zamanı dosyaları dahildir.

Kurulum paketi imzasızdır; Windows bilinmeyen yayıncı uyarısı gösterebilir. Güvenilir imza ve MSIX dağıtımı sonraki aşamadır.

- **Gereksinimler:** .NET/Windows App SDK dahildir. Setup, venv/pip destekli Python 3.11–3.13 x64 arar. Eksikse paketteki resmî Python 3.13.16 kurucusu için başlangıçta seçili olmayan bir seçenek sunar. Atlayıp MorseCore'u kullanabilirsiniz. Python kullanıcıya özel kurulur; PATH, dosya ilişkileri ve başka Python sürümleri değiştirilmez.
- **Seslendirme:** kurulum sonunda isteğe bağlı ses indirmesini seçin veya daha sonra Başlat menüsü kısayolunu kullanın. İlk indirme internet ister; modeller `%LOCALAPPDATA%\MorseCore\Speech` altında tutulur ve sonraki üretim çevrimdışıdır. Modeller pakete dahil değildir. Python ayrıca kaldırılabilir; ses ortamları bu kuruluma bağlıdır.
- **Güncelleme:** yeni Setup'ı mevcut kurulumun üzerine çalıştırın. Kurulum yolu ve tercihler korunur. Ayarlardaki İndir ve yükle, aynı GitHub Release sürümünün Setup dosyasını ve SHA256SUMS listesini indirir; özet ve sürüm doğrulamasından sonra uygulamayı kapatıp Setup'ı açmak için onay ister. Kontrol tek başına kurulum yapmaz; Release yayımlanmadan commit atılması güncelleme oluşturmaz. Eski kurulum paketi daha yeni kayıtlı sürümün üzerine yazmaz.
- **Kaldırma:** Windows Ayarları → Uygulamalar bölümünü kullanın. Tercihler, ses modelleri ve kullanıcı dosyaları korunur. Masaüstü ayarları `%LOCALAPPDATA%\MorseCore\Desktop` altındadır.

### Derleme ve kontrol

Yukarıdaki komutları PowerShell 7 ve [masaüstü derleme araçlarıyla](desktop.tr.md#çalıştırma) kullanın. Çıktılar benzersiz bir `cmake-build-desktop-installer/` klasörüne yazılır: masaüstü ZIP'i, Setup.exe ve `SHA256SUMS`.

Gerekirse Inno Setup 7.1.0 resmî sürümünden indirilir; sabit SHA256 ve yayıncı imzası doğrulanıp derleyici projenin yok sayılan araç klasörüne kurulur. Mevcut derleyiciyi `-IsccPath` ile seçebilirsiniz. Resmî Python kurucusu da derleme sırasında indirilir; sabit SHA256 ve Python Software Foundation imzası doğrulanarak Setup içine gömülür. Python kurulumu internet gerektirmez.

Kontrol sırasında önceden kurulmuş MorseCore bulunmamalıdır. Test ayrı, boşluklu/Türkçe bir yolda kurulum yapar; lisansları, kısayolları ve native API'yi kontrol eder, kurulan uygulamayı açıp kapatır. Ardından yeniden kurmayı, eski sürüme dönüşün engellenmesini ve kaldırmayı sınar. Önceki sürümden gerçek yükseltmeyi sınamak için `-PreviousInstallerPath "<eski-Setup.exe>"` ekleyin. CI, `-SkipLaunch` kullanır; tek başına CI sonucu arayüzün açıldığını kanıtlamaz. Kanıtlar `cmake-build-installer-tests/` altında kalır.

Yukarıdaki `test-live-update.ps1`, yayınlanmış sürümden eski bir Setup ile gerçek GitHub indirmesini, kurucunun açılmasını, sihirbazı ve yükselen uygulamayı sınar. `test-installer-prerequisites.ps1`, ayrı keşif kayıtları ve üretimdeki ön gereksinim koduyla Python var/yok/ret/hata/onay senaryolarını sınar; uyumlu bir Python ve önceden kurulmamış Python 3.13 gerektirir, kendi geçici Python'ını kaldırır. İkisi de isteğe bağlı yerel testtir.

## Credits / Teşekkürler

The installer is built with [Inno Setup](https://jrsoftware.org/), by Jordan Russell and Martijn Laan. Its [original license](../third_party/installer/Inno-Setup-LICENSE.txt) is included in the installed package. / Kurulum paketi Jordan Russell ve Martijn Laan'ın Inno Setup projesiyle oluşturulur; özgün lisansı kurulan dosyalara dahildir.
