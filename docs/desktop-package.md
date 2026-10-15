# MorseCore 0.6.0

[Source and full documentation / Kaynak ve tam belgeler](https://github.com/mustafamertakceylan/MorseCore)

## English

1. Extract the entire ZIP to a writable folder. Open **MorseCore.Desktop.exe**; keep the other files beside it.
2. Choose the interface language and System/Light/Dark theme in **Settings**. Russian/Arabic conversion alphabets and the speech language are independent choices.
3. For optional offline speech, open **Audio → Voice packs** and install a voice with internet once. The app can install missing Python after confirmation. Models and dependencies download separately; synthesis stays offline afterward. The speech environment is tied to that computer's Python installation.

Setup checks for compatible Python and offers its bundled official Python installer if needed. If you used **Setup.exe**, launch MorseCore from the Start menu instead of extracting a ZIP. Its offline-voices shortcut stores models in `%LOCALAPPDATA%\MorseCore\Speech`; Windows Settings → Apps removes the application while retaining preferences and models.

This is an unsigned Windows x64 preview. It includes .NET/Windows App SDK runtime files and does not install MSIX. Settings can download and verify a newer release, then open Setup with your confirmation. No model weights, speech virtual environment or API keys are included. [Setup guide](docs/windows-setup.md).

## Türkçe

1. ZIP'in tamamını yazılabilir bir klasöre açın. **MorseCore.Desktop.exe** dosyasını çalıştırın; diğer dosyaları yanında tutun.
2. **Ayarlar** bölümünden arayüz dilini ve Sistem/Açık/Koyu temayı seçin. Rusça/Arapça dönüşüm alfabesi ve seslendirme dili birbirinden bağımsızdır.
3. İsteğe bağlı çevrimdışı seslendirme için **Ses → Ses paketleri** bölümünden internet açıkken bir ses kurun. Eksik Python onayınızla kurulabilir. Model ve bağımlılıklar ayrıca indirilir; sonraki ses üretimi çevrimdışıdır. Ses ortamı kurulduğu bilgisayarın Python'una bağlıdır.

Setup uyumlu Python'ı arar; eksikse paketindeki resmî kurucuyu isteğe bağlı sunar. **Setup.exe** kullandıysanız ZIP açmak yerine uygulamayı Başlat menüsünden çalıştırın. Çevrimdışı ses kurulumu kısayolu modelleri `%LOCALAPPDATA%\MorseCore\Speech` altında tutar. Windows Ayarları → Uygulamalar bölümünden kaldırabilirsiniz; tercihler ve modeller korunur.

Bu, imzasız Windows x64 önizlemesidir. .NET/Windows App SDK çalışma zamanı dosyalarını içerir; MSIX kurmaz. Ayarlardan yeni sürüm indirilip doğrulanabilir; onayınızdan sonra Setup açılır. Model ağırlığı, ses sanal ortamı veya API anahtarı içermez. [Kurulum rehberi](docs/windows-setup.md#türkçe).

## Credits / Teşekkürler

Thank you / Teşekkürler: **Canberk Aslan — EMA Lightning 1.1.2**, **PatientDesk AI — Antalia-2 Mini 1.0.1**, Piper contributors, normalizer-tr, PyTorch, ONNX Runtime, Uno Platform, Windows App SDK and the bundled font authors.

MorseCore: **Apache-2.0**, see [LICENSE](LICENSE) and [NOTICE](NOTICE). Original Turkish-engine files: [third_party/speech](third_party/speech/README.md). Bundled runtime notices: [licenses](licenses/). Engines, models and voices retain separate licenses. / Motorlar, modeller ve sesler kendi lisanslarını korur.
