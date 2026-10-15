# Çevrimdışı seslendirme

[English](offline-speech.md) · [Masaüstü](desktop.tr.md)

Seslendirme, Windows ses paketleri yerine cihazdaki sinir ağı modellerini kullanır. Metinden Morse'a dönüşümde özgün metin okunur; Morse'dan metne dönüşümde önce seçilen alfabe ile çözümleme yapılır. Dil ve ses, dönüştürücünün hemen altından seçilir. Sesleri elle taramak veya metni başka ekrana kopyalamak gerekmez. **Durdur**, hem ses üretimini hem oynatmayı keser.

| Dil | Sesler | Çalıştırıcı |
|---|---|---|
| Türkçe | EMA Lightning; Antalia-2 Mini | CPU PyTorch |
| Arapça | Kareem; Ürdün Arapçası, tek konuşmacı | Piper / ONNX Runtime CPU |
| Almanca | Thorsten, yüksek kalite; Kerstin, düşük kalite | Piper / ONNX Runtime CPU |
| Rusça | Dmitri; Denis | Piper / ONNX Runtime CPU |
| İngilizce | Joe | Piper / ONNX Runtime CPU |

[EMA Lightning](https://huggingface.co/canberkkkkkk/ema-lightning), geliştiricisinin verilerine göre 8,6 milyon parametre ve yaklaşık 34 MB ağırlık içerir. [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) 7,62 milyon parametreli, fp32 ağırlıkları yaklaşık 30,5 MB olan ayrı bir modeldir. Bunlar RAM ölçümü veya ses kalitesi garantisi değildir. Alania'nın çevrimiçi API'si ile yerel Antalia Mini aynı ürün değildir.

## Bir kez kurulum

Geliştirme paketi Python 3.11–3.13 x64, `curl` ve kurulum sırasında internet gerektirir. Windows Setup 0.4.1, Python'ı kontrol eder ve eksikse paketindeki değiştirilmemiş [resmî Python 3.13.16 kurucusunu](https://www.python.org/downloads/release/python-31316/) isteğe bağlı sunar. Uyumlu kurulum varsa onu kullanır; PATH değiştirilmez. Ses modelleri ayrıca kullanıcı seçimiyle indirilir. Windows'ta kökteki `setup-speech.cmd` dosyasını çift tıklayabilir veya şu komutu çalıştırabilirsin:

```powershell
python -X utf8 scripts/setup-speech.py
```

Gitignore içindeki `speech-runtime/` klasörüne ayrı bir CPU ortamı ve sekiz ses kurulur. Modeller yaklaşık 472 MiB tutar; Python bağımlılıkları ayrıca yer kaplar. Kurulum tekrarlandığında doğrulanmış dosyalar yeniden indirilmez. Model sürümleri, boyutları ve SHA-256/LFS veya Git blob özetleri `scripts/speech-models.json` içinde sabittir. Model kartları ve bağımlılık lisansları pakette kalır. Ağırlıklar Git'e eklenmez.

Uygulama, kendi klasöründe veya üst klasörlerinde `speech-runtime` arar. Başka konum **Ayarlar → Ses paketi** bölümünden veya mutlak `MORSECORE_SPEECH_HOME` değişkeniyle seçilebilir. Önizlemedeki sanal ortam, kurulduğu makinenin Python kurulumuna bağlıdır; başka bilgisayara taşınan bir kurulum paketi değildir. MSIX isteğe bağlı ses paketini içermez.

## İnternetsiz çalışma ve bellek

Ses üretimi ayrı bir işlemde, yerel model yollarıyla yapılır. Türkçe adaptörü Hub erişimini kapatır ve ağ bağlantılarını reddeder. Piper kendi komut satırı aracıyla, mevcut yerel model üzerinden çağrılır. API anahtarı veya sürekli çalışan sunucu gerekmez. Eksik model için mesaj gösterilir; seslendirme kendiliğinden indirme başlatmaz. Uygulamanın GitHub sürüm kontrolü ayrı ayarına bağlıdır.

Modeller yalnızca ses üretirken açılır; işlem bitince çalıştırıcı kapanır. Geçici WAV dosyaları oynatım veya iptal sonrasında silinir. Sınır 4000 karakter ve iki dakikalık üretim süresidir. CUDA paketi kurulmaz; Türkçe adaptörü iki CPU iş parçacığı kullanır ve toplu iş boyutu araması yapmaz.

Küçük ağırlık dosyası küçük RAM tüketimi anlamına gelmez. Bu Windows makinesindeki tek soğuk CPU örneğinde EMA, Python/model açılışı dahil 2,92 saniyede 4,68 saniyelik ses; Antalia 3,02 saniyede 5,40 saniyelik ses üretti. Çalıştırıcı işlem ağacının çalışma kümesi sırasıyla 335,3/349,5 MiB; özel ayrılmış belleği 1185,7/1139,1 MiB zirvesine çıktı. Ardından işlem kapandı. Arayüzün belleği bu sayılara dahil değildir; performans garantisi değildir. Geliştiricilerin GPU hızları CPU sonucu olarak sunulmaz.

Antalia'nın yayımladığı tarayıcı ONNX örneği 1.0.0, burada kullanılan Python sürümü 1.0.1'dir. Daha az bellek kullanan ONNX adaptörü, çıktı ve metin normalizasyonu eşdeğerliği doğrulanmadan bu yolun yerine konmadı. Önizlemede etkin değildir.

## Lisanslar ve test

MorseCore ve Türkçe adaptörü Apache-2.0 olarak kalır. EMA ve Antalia'nın kod/ağırlıkları da Apache-2.0'dır. Ayrı kurulan [Piper motoru](https://github.com/OHF-Voice/piper1-gpl/tree/v1.3.0) GPL-3.0 lisanslıdır; MorseCore kütüphanesine bağlanmaz, ayrı komut satırı işlemi olarak çağrılır. Bağımlılıkların kendi lisansları geçerlidir.

Piper model kartları Thorsten, Kerstin, Dmitri, Denis ve Joe verilerini CC0 olarak belirtir. Kareem kartı, lisansı açıkça yazmadan bir veri deposuna yönlendirir. Genel dağıtıma dahil edilmeden önce yeniden dağıtım izni netleştirilmelidir. Geliştirme paketinin kurulması, üçüncü taraf motor/model/verilerini Apache-2.0 yapmaz. Sabitlenmiş kartlar `speech-runtime/licenses/` altında; kaynak [Piper ses deposunda](https://huggingface.co/rhasspy/piper-voices).

```powershell
pwsh -File scripts/test-speech.ps1
pwsh -File scripts/test-desktop.ps1
```

Ses testi katalogdaki her sesle gerçek üretim yapar; çıktı, giriş sınırı, eksik paket ve iptali denetler. GUI testi beş dilde yerleşimi ve EMA kuruluysa gerçek ses üretimi ile sessize alınmış MediaPlayer oynatımını sınar. Sesin doğallığı ve fiziksel hoparlör çıkışı ayrıca dinlenerek değerlendirilmelidir.
