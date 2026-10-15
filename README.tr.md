<div align="center">

# MorseCore

<img src="apps/MorseCore.Desktop/MorseCore.Desktop/Assets/Brand/morsecore.png" width="92" alt="MorseCore logosu">

**Morse kodunu dönüştürün, dinleyin ve alıştırma yapın.**

![C23](https://img.shields.io/badge/C-23-225E85?style=flat-square&logo=c&logoColor=white)
[![Sürüm](https://img.shields.io/badge/Version-0.3.0-5e6a42?style=flat-square)](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.3.0)
[![Lisans](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

[English](README.md) · **Türkçe**

</div>

MorseCore, metin ve dosyaları Morse koduna çevirir; Morse kodunu yeniden metne dönüştürür. Windows masaüstü arayüzünden, konsol menüsünden veya komut satırından kullanabilirsiniz.

## İndir

**[Windows x64 için indir — v0.3.0](https://github.com/mustafamertakceylan/MorseCore/releases/download/v0.3.0/MorseCore-0.3.0-Windows-x64-Desktop.zip)**

ZIP'in tamamını açıp **MorseCore.Desktop.exe** dosyasını çalıştırın. DLL'leri ve alt klasörleri bir arada tutun. Bu, imzasız bir masaüstü önizlemesidir; macOS ve Linux arayüzleri planlanmaktadır.

İsteğe bağlı seslendirme için Python 3.11–3.13 ile **setup-speech.cmd** dosyasını çalıştırın. Modeller kurulumda bir kez indirilir; ardından seslendirme çevrimdışı çalışır. Modeller ZIP'e dahil değildir. [Ses kurulumu →](docs/offline-speech.tr.md)

Konsol uygulaması, native DLL, dosya özetleri ve sürüm notları [sürüm sayfasındadır](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.3.0).

## Özellikler

- Metinleri ve `.txt` dosyalarını tek tek veya topluca dönüştürün.
- Morse dinleyin, WAV kaydedin; okuyarak veya dinleyerek alıştırma yapın.
- Masaüstünde Latin/Türkçe, Rusça ve Arapça metinleri dönüştürün.
- Açık/koyu tema; Türkçe, İngilizce, Almanca, Rusça ve Arapça menüler kullanın.
- Metni çevrimdışı seslendirin: Türkçede EMA Lightning ve Antalia-2 Mini, diğer dört dilde Piper.

## Derleme ve çalıştırma

C23 çekirdek ve konsol için **CMake 3.28+**, **GCC 15+** ve **Ninja** gerekir. Clang 18+ için `clang-debug` yapılandırması da desteklenir.

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
cmake --preset debug
cmake --build --preset debug
.\run.cmd
```

Komut satırında `.\run.cmd --encode "HELLO WORLD"` ile başlayın; tüm seçenekler için `.\run.cmd --help` kullanın. Konsol, Latin/Türkçe girdileri destekler.

## Rehberler

- [Masaüstü kullanımı ve arayüzü derleme](docs/desktop.tr.md)
- [Çevrimdışı seslendirme ve ses modelleri](docs/offline-speech.tr.md)
- [C API ve .NET bağlantısı](docs/native-api.tr.md)
- [Değişiklik günlüğü](CHANGELOG.md)

## Teşekkürler ve lisans

MorseCore'un Türkçe seslendirmesinde kullanılan [EMA Lightning](https://github.com/canberk7/ema-lightning) için **Canberk Aslan'a**, [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) için **PatientDesk AI ekibine** teşekkür ederiz. Diğer katkılar **Ayarlar → Açık kaynağa teşekkürler** bölümündedir.

MorseCore, [Apache 2.0](LICENSE) lisanslıdır. Atıflar [NOTICE](NOTICE) dosyasındadır. Üçüncü taraf bileşenler ve modeller kendi lisanslarını korur; ses motorlarının özgün lisansları ve Antalia'nın bildirimi [third_party/speech](third_party/speech/README.md) altında bulunur.
