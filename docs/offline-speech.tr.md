# Çevrimdışı seslendirme

Uygulama içi ses yöneticisi **v0.6.0** ile gelir. **v0.5.0** kullanıcıları güncelleyebilir veya Başlat menüsündeki **Çevrimdışı sesleri kur** / taşınabilir ZIP'teki **setup-speech.cmd** kısayolunu kullanmayı sürdürebilir.

[English](offline-speech.md) · [Masaüstü](desktop.tr.md)

**Ses → Ses paketleri** bölümünde dili seç, istediğin sesin yanındaki **İndir ve kur** düğmesine bas. Örneğini dinleyip **Bu sesi kullan** seçeneğine tıkla. **Ses → Ses oluştur** bölümünde metin girerek veya son dönüşümü aktararak **Seslendir** düğmesini kullanabilirsin. Dönüştürücü yalnızca dönüştürür.

Terminal komutu gerekmez. Ses çalıştırıcısı eksikse uygulama, onayınla resmî Python çalıştırıcısını Windows kullanıcın için kurabilir. Uyumlu Python 3.11–3.13 x64 varsa yeniden kullanır; PATH değiştirmez, yönetici yetkisi istemez. Setup da bu isteğe bağlı bileşeni sunar. Resmî Python kurucusu başladıktan sonra bitmesini bekle; uygulama kurucuyu yarıda kesmez.

| Dil | Kullanılabilen sesler |
|---|---|
| Türkçe | EMA Lightning · Antalia-2 Mini |
| İngilizce | Joe · Piper |
| Almanca | Thorsten · Piper; Kerstin · Piper |
| Rusça | Dmitri · Piper; Denis · Piper |
| Arapça | Kareem · Piper, Ürdün Arapçası |

## Bir kez indir, internetsiz kullan

İndirmeler yalnızca onayından sonra başlar. Her modelin kesin indirme boyutu, ilerleme, iptal ve hata durumu uygulamada gösterilir. Ortak çalıştırıcı ve bağımlılıklar ayrıca yer kaplar; boyutları modele dahil değildir. Yalnızca Piper sesi kurduğunda PyTorch veya Türkçe motorları kurulmaz. Paket sayfasından ayrılmak model/bağımlılık kurulumunu iptal eder; yeniden denemede doğrulanmış dosyalar kullanılır.

Seslendirme bundan sonra API anahtarı veya sürekli çalışan sunucu olmadan yerel dosyaları kullanır. Eksik ses için mesaj gösterilir; kendiliğinden indirme başlamaz. **Durdur**, ses üretimini ve oynatımını iptal eder. Uygulama güncelleme kontrolü Ayarlar'dan ayrıca yönetilir.

**Sesi kaldır**, yalnızca o sese ait değiştirilmemiş ve doğrulanmış model dosyalarıyla model kartını kaldırır. Diğer sesler, Python, ortak bağımlılıklar, ayarlar, görüşmeler ve kayıtların korunur. Yönetici dışında değiştirilmiş dosyalar silinmez. Seçili ses klasörü sayfada gösterilir; gerekirse **Ayarlar → Ses paketi** bölümünden değiştirilebilir.

## Sınırlar ve teşekkür

Ses oynatımı şu anda Windows hedefinde kullanılabilir. macOS ve Linux planlanıyor. Modeller gerektiğinde ayrı çalıştırıcıda yüklenir; üretim bitince işlem kapanır. Sınır 4000 karakter ve iki dakikalık ses üretimidir. Küçük model dosyası düşük tepe RAM tüketimi anlamına gelmez; Türkçe sesler CPU PyTorch kullanır ve üretim sırasında birkaç yüz MiB bellek tüketebilir.

[EMA Lightning](https://huggingface.co/canberkkkkkk/ema-lightning), [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) ve [Piper](https://github.com/OHF-Voice/piper1-gpl/tree/v1.3.0) geliştiricilerine teşekkürler. EMA ve Antalia'nın kod ve ağırlıkları Apache-2.0'dır. Piper GPL-3.0 lisansıyla ayrı işlem olarak çalışır. Modellerin ve bağımlılıkların kendi koşulları geçerlidir. Lisans ve NOTICE dosyaları kurulan pakette korunur.

Kareem'in kaynak model kartında veri kümesinin lisansı açıkça belirtilmez. Yeniden dağıtımdan önce [kaynak kartını](https://huggingface.co/rhasspy/piper-voices/blob/c10ece1aade47bb51c153c893d14e5bf8e5b7117/ar/ar_JO/kareem/medium/MODEL_CARD) incele; kurulum onayında da bu durum belirtilir. Diğer katalog kartları veri kümelerini CC0 olarak tanımlar. Kurulum, üçüncü taraf bileşenleri Apache-2.0 yapmaz.

<details>
<summary>Geliştirici kullanımı ve kontroller</summary>

İsteğe bağlı komut satırı kurulumu korunur: `python -X utf8 scripts/setup-speech.py --voice ema-tr`. `--voice` olmadan tüm katalog kurulur. Arayüz aynı sürümü sabitlenmiş motor paketlerini kullanır; `scripts/speech-models.json` içindeki dosyaları geçici dosya, kesin boyut ve SHA-256 veya Git blob özetiyle doğruladıktan sonra taşır.

Uygulama gitignore içindeki `speech-runtime/` geliştirme klasörünü, seçilmiş mutlak klasörü veya `%LOCALAPPDATA%/MorseCore/Speech` konumunu kullanır. Klasör seçilmemişse `MORSECORE_SPEECH_HOME` keşfi değiştirebilir. Kurulum ayrıntıları seçili klasörde `.voice-tools/setup.stdout.log` ve `setup.stderr.log` dosyalarındadır. Sanal ortam aynı bilgisayardaki Python'a bağlıdır; başka bilgisayara kopyalanması desteklenmez.

`python -m unittest discover -s tests -p test_speech_setup.py`, AppCore kontrolleri ve kurulu sesler için `pwsh -File scripts/test-speech.ps1` kullanılabilir. Ses doğallığı ve fiziksel hoparlör çıkışı ayrıca dinlenmelidir; otomatik testler bunları kanıtlamaz.

</details>
