# MorseCore 0.3.0

[Source and full documentation / Kaynak ve tam belgeler](https://github.com/mustafamertakceylan/MorseCore)

## English

1. Extract the entire ZIP to a writable folder. Open **MorseCore.Desktop.exe**; keep the other files beside it.
2. Choose the interface language and System/Light/Dark theme in **Settings**. Russian/Arabic conversion alphabets and the speech language are independent choices.
3. For optional offline speech, install Python 3.11–3.13, then run **setup-speech.cmd** once with internet. Models and speech dependencies are downloaded separately. Synthesis stays offline afterward; the speech virtual environment is tied to that computer's Python installation.

This is an unsigned Windows x64 preview. It includes .NET/Windows App SDK runtime files and does not install MSIX or configure automatic installation updates. No model weights, speech virtual environment or API keys are included.

## Türkçe

1. ZIP'in tamamını yazılabilir bir klasöre açın. **MorseCore.Desktop.exe** dosyasını çalıştırın; diğer dosyaları yanında tutun.
2. **Ayarlar** bölümünden arayüz dilini ve Sistem/Açık/Koyu temayı seçin. Rusça/Arapça dönüşüm alfabesi ve seslendirme dili birbirinden bağımsızdır.
3. İsteğe bağlı çevrimdışı seslendirme için Python 3.11–3.13 kurun, **setup-speech.cmd** dosyasını internet açıkken bir kez çalıştırın. Modeller ve ses bağımlılıkları ayrıca indirilir. Sonraki ses üretimi çevrimdışıdır; ses sanal ortamı kurulduğu bilgisayarın Python'una bağlıdır.

Bu, imzasız Windows x64 önizlemesidir. .NET/Windows App SDK çalışma zamanı dosyalarını içerir; MSIX kurmaz veya otomatik kurulum güncellemesi ayarlamaz. Model ağırlığı, ses sanal ortamı veya API anahtarı içermez.

## Credits / Teşekkürler

Thank you / Teşekkürler: **Canberk Aslan — EMA Lightning 1.1.2**, **PatientDesk AI — Antalia-2 Mini 1.0.1**, Piper contributors, normalizer-tr, PyTorch, ONNX Runtime, Uno Platform, Windows App SDK and the bundled font authors.

MorseCore: **Apache-2.0**, see [LICENSE](LICENSE) and [NOTICE](NOTICE). Original Turkish-engine files: [third_party/speech](third_party/speech/README.md). Bundled runtime notices: [licenses](licenses/). Engines, models and voices retain separate licenses. / Motorlar, modeller ve sesler kendi lisanslarını korur.
