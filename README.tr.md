<div align="center">

# MorseCore

<img src="apps/MorseCore.Desktop/MorseCore.Desktop/Assets/Brand/morsecore.png" width="92" alt="MorseCore logosu">

**Morse kodunu dönüştürün, dinleyin ve alıştırma yapın.**

![C23](https://img.shields.io/badge/C-23-225E85?style=flat-square&logo=c&logoColor=white)
[![Sürüm](https://img.shields.io/badge/Version-0.4.0-5e6a42?style=flat-square)](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.4.0)
[![Lisans](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

[English](README.md) · **Türkçe**

</div>

MorseCore, metin ve dosyaları Morse koduna çevirir; Morse kodunu yeniden metne dönüştürür. C23 motoruyla çalışan bir Windows masaüstü uygulamasıdır.

## İndir

**[Windows Setup indir — v0.4.0](https://github.com/mustafamertakceylan/MorseCore/releases/download/v0.4.0/MorseCore-0.4.0-Windows-x64-Setup.exe)** · [Taşınabilir ZIP](https://github.com/mustafamertakceylan/MorseCore/releases/download/v0.4.0/MorseCore-0.4.0-Windows-x64-Desktop.zip)

Setup'ı çalıştırıp uygulamayı Başlat menüsünden açın. Taşınabilir ZIP'i kullanıyorsanız tüm dosyaları açıp **MorseCore.Desktop.exe** dosyasını çalıştırın. Bu, imzasız bir önizlemedir; macOS ve Linux arayüzleri planlanmaktadır.

İsteğe bağlı seslendirme için Başlat menüsündeki **Çevrimdışı sesleri kur** kısayolunu veya ZIP'teki **setup-speech.cmd** dosyasını kullanın. Python 3.11–3.13 ve internet bir kez gerekir; sonra seslendirme çevrimdışı çalışır. Modeller ayrıca indirilir. [Ses kurulumu →](docs/offline-speech.tr.md)

Dosya özetleri ve sürüm notları [sürüm sayfasındadır](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.4.0).

## Özellikler

- Metinleri ve `.txt` dosyalarını tek tek veya topluca dönüştürün.
- Morse dinleyin, WAV kaydedin; okuyarak veya dinleyerek alıştırma yapın.
- Masaüstünde Latin/Türkçe, Rusça ve Arapça metinleri dönüştürün.
- Açık/koyu tema; Türkçe, İngilizce, Almanca, Rusça ve Arapça menüler kullanın.
- Metni çevrimdışı seslendirin: Türkçede EMA Lightning ve Antalia-2 Mini, diğer dört dilde Piper.

## Derleme ve çalıştırma

**PowerShell 7**, **.NET 10**, Windows SDK/WinUI araçları, **CMake 3.28+**, **GCC 15+** ve **Ninja** gerekir. Ayrıntılar [masaüstü rehberindedir](docs/desktop.tr.md#çalıştırma).

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
pwsh -File scripts/run-desktop.ps1
```

## Rehberler

- [Masaüstü kullanımı ve arayüzü derleme](docs/desktop.tr.md)
- [Windows Setup: kurulum ve derleme](docs/windows-setup.md#türkçe)
- [Çevrimdışı seslendirme ve ses modelleri](docs/offline-speech.tr.md)
- [C API ve .NET bağlantısı](docs/native-api.tr.md)
- [Değişiklik günlüğü](CHANGELOG.md)

## Teşekkürler ve lisans

MorseCore'un Türkçe seslendirmesinde kullanılan [EMA Lightning](https://github.com/canberk7/ema-lightning) için **Canberk Aslan'a**, [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) için **PatientDesk AI ekibine** teşekkür ederiz. Diğer katkılar **Ayarlar → Açık kaynağa teşekkürler** bölümündedir.

MorseCore, [Apache 2.0](LICENSE) lisanslıdır. Atıflar [NOTICE](NOTICE) dosyasındadır. Üçüncü taraf bileşenler ve modeller kendi lisanslarını korur; ses motorlarının özgün lisansları ve Antalia'nın bildirimi [third_party/speech](third_party/speech/README.md) altında bulunur.
