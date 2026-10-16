# Ses ve çevrimdışı seslendirme

Yan panelden **Ses** bölümünü aç. Dört sekme, farklı işlemleri birbirinden ayırır.

![MorseCore ses çalışma alanı]({{MEDIA_URL}}/wiki/audio.tr.png)

*macOS arayüzü gösterilmiştir; Windows yerleşimi farklı olabilir.*

## Morse sesi

Metin veya Morse kodu gir, giriş türünü ve alfabeyi seç. **Morse'u dinle** düğmesiyle dinle; **WAV olarak kaydet** ile ses dosyası oluştur. **Gelişmiş ayarlar** bölümünden hızı ve ton frekansını değiştir. Morse sesi için ses paketi gerekmez. Bu işlem bilgisayardan ses üretir; telsiz yayını yapmaz.

## Metni seslendir

Seslendirme dilini ve kurulu sesi seç, metnini girip **Seslendir** düğmesine bas. Morse girdisi de kullanabilirsin; önce metne çözülüp ardından seslendirilir. **Durdur**, üretimi veya oynatımı iptal eder. Bir istekte en fazla 4.000 karakter kullanılabilir; uzun metinleri parçalara ayır.

## Sesi çöz

WAV dosyası seç veya bir ses girişinden kayıt alıp çözümle. Temiz, tek tonlu Morse kullan; konuşma, müzik ve üst üste binen istasyonlar desteklenmez.

- WAV dosyası: en fazla **120 saniye / 64 MiB**, mono veya stereo PCM/float.
- Mikrofon kaydı: **Windows'ta 30 saniye**, **Mac'te 120 saniye**. İstenirse mikrofon izni ver; kaydı durdurunca çözümleme başlar.
- `□`, belirsiz karakterdir. Sonuç bozuksa gürültüyü azalt veya **Gelişmiş ayarlar** içinden ton, hız ve eşiği düzenle.

## Ses paketleri

Dili seç, **İndir ve kur** düğmesine basıp onayla. Ardından **Örneği dinle → Bu sesi kullan** yolunu izle.

| Dil | Sesler |
|---|---|
| Türkçe | EMA Lightning · Antalia-2 Mini |
| İngilizce | Joe · Piper |
| Almanca | Thorsten · Kerstin · Piper |
| Rusça | Dmitri · Denis · Piper |
| Arapça | Kareem · Piper, Ürdün Arapçası |

İlk kurulum internet ister; ek çalıştırıcı bileşenleri de hazırlanabilir. Sonrasında seslendirme API anahtarı olmadan çevrimdışı yapılır. Gösterilen model boyutu ortak bağımlılıkları kapsamaz. Paket sayfasından ayrılmak kurulumu iptal eder; yeniden denemede doğrulanmış indirmeler kullanılır. Bir sesi kaldırmak diğer sesleri ve kayıtlarını silmez.

[EMA Lightning](https://huggingface.co/canberkkkkkk/ema-lightning), [Antalia](https://huggingface.co/cloud0day3/antalia-mini) ve [Piper](https://github.com/OHF-Voice/piper1-gpl) geliştiricilerine teşekkürler. EMA/Antalia kod ve ağırlıkları Apache-2.0, Piper motoru GPL-3.0 lisanslıdır. Ses modellerinin koşulları ayrıdır. Yeniden dağıtmadan önce **Kaynak ve lisans** bağlantısını oku; Kareem'in kaynak kartında veri kümesi lisansı açık belirtilmez.
