# Windows Setup

[English README](../README.md) · [Türkçe README](../README.tr.md)

## English

Run **MorseCore-0.4.0-Windows-x64-Setup.exe**. The wizard supports English, Turkish, German, Russian and Arabic. It installs for the current user in `%LOCALAPPDATA%\Programs\MorseCore`, adds Start menu shortcuts and offers an optional desktop shortcut. Administrator rights are not required. The .NET and Windows App SDK runtime files are included.

This installer is unsigned; Windows may show an unknown-publisher warning. Trusted signing and MSIX distribution are separate future work.

- **Speech:** use the Start menu's offline-voices shortcut. Python 3.11–3.13 and internet are needed once; models then work offline from `%LOCALAPPDATA%\MorseCore\Speech`. Models are not bundled or downloaded by Setup.
- **Updates:** run a newer Setup over the existing installation. Its path and preferences are retained. The app checks GitHub for releases and opens the release page; it does not silently install updates. Older installers refuse to replace a newer registered version.
- **Uninstall:** use Windows Settings → Apps. Preferences, voice models and user-created files are retained. Desktop settings are in `%LOCALAPPDATA%\MorseCore\Desktop`.

### Build and check

In a PowerShell 7 terminal with the [desktop build tools](desktop.md#run-from-source):

```powershell
pwsh -File scripts/package-desktop-installer.ps1
pwsh -File scripts/test-desktop-installer.ps1 -InstallerPath "<output>\MorseCore-0.4.0-Windows-x64-Setup.exe"
```

The build produces a desktop ZIP, Setup.exe and `SHA256SUMS` in a unique `cmake-build-desktop-installer/` folder. It downloads Inno Setup 7.1.0 from its official release if needed, verifies its pinned SHA256 and publisher signature, then installs the compiler in the ignored project tools folder. An existing compiler can be selected with `-IsccPath`.

The check requires no existing MorseCore installation. It uses an isolated Unicode/spaced path, checks installed licenses, shortcuts and the native API, launches the installed release, then checks reinstall, downgrade refusal and removal. Add `-PreviousInstallerPath "<older-Setup.exe>"` to exercise a real upgrade from an earlier release. CI uses `-SkipLaunch`; a successful CI installer check alone does not prove GUI startup. Evidence remains in `cmake-build-installer-tests/`.

## Türkçe

**MorseCore-0.4.0-Windows-x64-Setup.exe** dosyasını açın. Sihirbaz Türkçe, İngilizce, Almanca, Rusça ve Arapça destekler. Uygulama mevcut kullanıcı için `%LOCALAPPDATA%\Programs\MorseCore` klasörüne kurulur. Başlat menüsü kısayolları eklenir; masaüstü kısayolu isteğe bağlıdır. Yönetici yetkisi gerekmez. .NET ve Windows App SDK çalışma zamanı dosyaları dahildir.

Kurulum paketi imzasızdır; Windows bilinmeyen yayıncı uyarısı gösterebilir. Güvenilir imza ve MSIX dağıtımı sonraki aşamadır.

- **Seslendirme:** Başlat menüsündeki çevrimdışı ses kurulumu kısayolunu kullanın. İlk kurulum Python 3.11–3.13 ve internet ister. Modeller `%LOCALAPPDATA%\MorseCore\Speech` altında saklanır; sonrasında çevrimdışı çalışır. Setup modelleri içermez veya indirmez.
- **Güncelleme:** yeni Setup'ı mevcut kurulumun üzerine çalıştırın. Kurulum yolu ve tercihler korunur. Uygulama GitHub sürümlerini kontrol edip sürüm sayfasını açar; güncellemeyi sessizce kurmaz. Eski kurulum paketi daha yeni kayıtlı sürümün üzerine yazmaz.
- **Kaldırma:** Windows Ayarları → Uygulamalar bölümünü kullanın. Tercihler, ses modelleri ve kullanıcı dosyaları korunur. Masaüstü ayarları `%LOCALAPPDATA%\MorseCore\Desktop` altındadır.

### Derleme ve kontrol

Yukarıdaki komutları PowerShell 7 ve [masaüstü derleme araçlarıyla](desktop.tr.md#çalıştırma) kullanın. Çıktılar benzersiz bir `cmake-build-desktop-installer/` klasörüne yazılır: masaüstü ZIP'i, Setup.exe ve `SHA256SUMS`.

Gerekirse Inno Setup 7.1.0 resmî sürümünden indirilir; sabit SHA256 ve yayıncı imzası doğrulanıp derleyici projenin yok sayılan araç klasörüne kurulur. Mevcut derleyiciyi `-IsccPath` ile seçebilirsiniz.

Kontrol sırasında önceden kurulmuş MorseCore bulunmamalıdır. Test ayrı, boşluklu/Türkçe bir yolda kurulum yapar; lisansları, kısayolları ve native API'yi kontrol eder, kurulan uygulamayı açıp kapatır. Ardından yeniden kurmayı, eski sürüme dönüşün engellenmesini ve kaldırmayı sınar. Önceki sürümden gerçek yükseltmeyi sınamak için `-PreviousInstallerPath "<eski-Setup.exe>"` ekleyin. CI, `-SkipLaunch` kullanır; tek başına CI sonucu arayüzün açıldığını kanıtlamaz. Kanıtlar `cmake-build-installer-tests/` altında kalır.

## Credits / Teşekkürler

The installer is built with [Inno Setup](https://jrsoftware.org/), by Jordan Russell and Martijn Laan. Its [original license](../third_party/installer/Inno-Setup-LICENSE.txt) is included in the installed package. / Kurulum paketi Jordan Russell ve Martijn Laan'ın Inno Setup projesiyle oluşturulur; özgün lisansı kurulan dosyalara dahildir.
