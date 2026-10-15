<div align="center">

# MorseCore

**Metinden Morse koduna, Morse kodundan metne.**

Terminale yazdığınız veya bir metin dosyasından okuduğunuz mesajı dönüştürün. Morse alfabesine bakın, sonuçları kaydedin; dil ve kelime ayracı tercihlerinizi sonraki açılışta kullanın.

![C17](https://img.shields.io/badge/C-17-225E85?style=flat-square&logo=c&logoColor=white)
![CMake](https://img.shields.io/badge/CMake-3.28%2B-064F8C?style=flat-square&logo=cmake&logoColor=white)
[![Lisans](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

[English](README.md) · **Türkçe**

</div>

---

## İçindekiler

- [Ne yapar](#ne-yapar)
- [Özellikler](#özellikler)
- [Kurulum](#kurulum)
- [Kullanım](#kullanım)
- [Dosyalar](#dosyalar)
- [Ayarlar](#ayarlar)
- [Desteklenen girdiler](#desteklenen-girdiler)
- [İstatistikler](#istatistikler)
- [Klasör yapısı](#klasör-yapısı)
- [Testler](#testler)
- [Lisans](#lisans)

## Ne yapar

MorseCore harf ve rakamları nokta ve çizgilerden oluşan Morse koduna, Morse kodunu da okunabilir büyük harfli metne dönüştürür. Bilgisayarınızda, numaralı bir konsol menüsüyle çalışır.

Örneğin `HELLO WORLD` metni şu koda dönüşür:

```text
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Ters yönde dönüştürdüğünüzde yeniden `HELLO WORLD` metnini elde edersiniz.

## Özellikler

- Yazılan metni veya `.txt` dosyalarını iki yönde dönüştürme.
- Türkçe harfleri Latin karşılıklarına çevirerek işleme.
- Morse kelimeleri arasında `/`, `|`, `#` veya `*` kullanma; çözerken ayracı algılama.
- İngilizce ve Türkçe menüler; kalıcı dil ve ayraç tercihleri.
- Her dönüşümün istatistikleri ve geçerli oturumun özeti.
- Harf ve rakamlar için 36 öğeli Morse tablosu.
- ASCII `MORSE CORE` başlığıyla renkli konsol arayüzü.

## Kurulum

### Gereksinimler

| Araç | Kullanım amacı |
|---|---|
| CMake 3.28 veya üzeri | Projeyi yapılandırma ve derleme |
| C17 destekleyen GCC | Hazır `debug` yapılandırması |
| Ninja | Hazır `debug` yapılandırması |
| Python 3 | CLI bütünleşme testleri; uygulamayı çalıştırmak için gerekmez |

Derleme ve testler Windows üzerinde MSYS2 UCRT64 GCC ile kontrol edilmiştir. Diğer platformlar doğrulanmamıştır.

### Derleme ve çalıştırma

Depoyu klonlayın ve aşağıdaki komutları proje kökünde çalıştırın. `cmake`, `gcc` ve `ninja` komutlarının terminalden erişilebilir olması gerekir.

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
cmake --preset debug
cmake --build --preset debug
.\run.cmd
```

Derledikten sonra Windows'ta `run.cmd` dosyasına çift tıklayabilir veya uygulamayı doğrudan açabilirsiniz:

```powershell
.\cmake-build-debug\MorseCore.exe
```

Derleme, `data/` ve `config/` için proje klasörünün mutlak yolunu kaydeder. Böylece uygulama başka bir çalışma dizininden başlatıldığında da aynı dosyaları kullanır. Proje klasörünü taşırsanız yeniden yapılandırıp derleyin. Çalıştırılabilir dosya tek başına dağıtılabilir bir paket değildir.

## Kullanım

Menü numarasını yazıp Enter'a basın:

```text
1  Metinden Morse koduna
2  Morse kodundan metne
3  Metin dosyasından Morse koduna
4  Morse dosyasından metne
5  Morse tablosu
6  MorseCore hakkında
7  Ayarlar
0  Çıkış
```

### Bir mesajı dönüştürme

**1** seçeneğini açın, `HELLO WORLD` yazıp Enter'a basın. Uygulama sonucu ve istatistikleri gösterir, ardından Morse çıktısını kaydetmek isteyip istemediğinizi sorar. Kaydetmek için `y`, kaydetmeden devam etmek için `n` yazın.

Aşağıdaki mesajı çözmek için **2** seçeneğini kullanın:

```text
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Her harfin Morse kodu ayrı bir gruptur; gruplar boşluk karakterleriyle ayrılır. Kelime ayracı da `... --- ... / .-` örneğindeki gibi tek başına bir grup olmalıdır. Çözümleme sırasında mesajdaki desteklenen ayraç algılanır; kayıtlı tercihiniz farklı olabilir. Doğrudan girilen Morse kodu çözülürken kaydetme sorulmaz. Dosyadan çözülen çıktıyı kaydetmek için **4** seçeneğini kullanın.

## Dosyalar

Girdi dosyalarını `data/` klasörüne koyun. `message.txt` dosyası için **3** veya **4** seçeneğini açıp `message` ya da `message.txt` yazın. Gerektiğinde `.txt` uzantısı otomatik eklenir; tam yol yerine dosya adını girin.

Dosya dönüşümleri girdiyi ve çıktıyı gösterir, ardından sonucu aynı klasöre numaralı bir dosya olarak otomatik kaydeder:

```text
data/
  message.txt
  morse_output_1.txt
  morse_output_2.txt
  decoded_output_1.txt
```

Numaralandırma, mevcut dosya adlarını atlayarak dosyaların üzerine yazılmasını önler. Doğrudan yazılan metnin Morse çıktısını kaydettiğinizde de `morse_output_*.txt` adında numaralı bir dosya oluşturulur.

`data/` altında sonradan oluşan `.txt` çalışma dosyaları Git tarafından yok sayılır. Depoda zaten takip edilen örnek `.txt` dosyaları takip edilmeye devam eder.

## Ayarlar

Kelime ayracını veya arayüz dilini değiştirmek için **7 → Ayarlar** menüsünü açın. Tercihler `config/settings.txt` dosyasına kaydedilir ve sonraki açılışta yeniden kullanılır.

Varsayılan değerler:

```ini
word_separator=/
language=EN
```

Desteklenen diller `EN` ve `TR` seçenekleridir. Türkçe menü metinleri terminal uyumluluğu için ASCII harflerle yazılmıştır; dönüşüm girdisinde Türkçe harfler yine kabul edilir.

Renkler, terminal destekliyorsa açılır. Dosyaya veya başka bir programa yönlendirilen çıktı ANSI kaçış dizileri içermez. PowerShell'de renkleri kapatmak için:

```powershell
$env:NO_COLOR = "1"
.\run.cmd
```

## Desteklenen girdiler

Düz metinde `A–Z`, `a–z`, `0–9`, boşluk, sekme ve satır sonu karakterleri desteklenir. Türkçe harfler UTF-8 veya Windows-1254 kodlamasıyla kabul edilir:

```text
Ç/ç → C    Ğ/ğ → G    İ/ı → I
Ö/ö → O    Ş/ş → S    Ü/ü → U

Merhaba Dünya → MERHABA DUNYA
```

Çözümleme, büyük Latin harfleri üretir; metnin özgün Türkçe yazımı geri getirilemez. Kodlama sırasında kelimeler arasındaki boşluk karakterleri tek ayraca dönüşür, baştaki ve sondaki boşluklar atılır.

Dosya içeriği dahil girdi sınırı **999 bayttır**; UTF-8 kodlamasında bir Türkçe harf iki bayt tutar. Sınırı aşan girdi kısmen dönüştürülmez, reddedilir. Noktalama işaretleri ve diğer desteklenmeyen karakterler de reddedilir. Morse girdisinde harfler için `.` ve `-`, kelimeler arasında `/`, `|`, `#` veya `*` kullanılır.

Uygulama şu anda metin dönüşümü sunar. Sesli oynatma, mikrofonla çözümleme, grafik arayüz ve komut satırı dönüşüm argümanları henüz uygulanmamıştır.

<a id="istatistikler"></a>

<a id="istatistikler"></a>

## İstatistikler

Başarılı dönüşümden sonra girdi ve çıktı karakter sayıları, kelime sayısı, dönüştürülen harf ve rakam sayısı ile gerçek boşluk sayısı gösterilir. Desteklenen bir Türkçe harf, UTF-8'de iki bayt tutsa da tek karakter sayılır.

Kelime, dönüştürülen karakter ve boşluk sayıları işlemin düz metin tarafında hesaplanır: kodlamada girdi, çözümlemede çıktı kullanılır. Sekme ve satır sonları kelimeleri ayırır ancak gerçek boşluk sayısına eklenmez. Örneğin `Merhaba Dünya` 13 karakter, 12 harf, 2 kelime ve 1 boşluk içerir.

Çıkış özeti, geçerli oturumdaki dönüşümleri, dosya işlemlerini, geçersiz girdileri ve kaydedilen çıktıları gösterir. Oturum istatistikleri sonraki açılışa taşınmaz.

## Klasör yapısı

```text
src/                 Uygulama akışı, dönüşüm, konsol arayüzü, dosyalar ve ayarlar
include/             Başlık dosyaları ve ortak yapılandırma
tests/               Çekirdek testleri ve CLI bütünleşme senaryoları
config/settings.txt  Kayıtlı dil ve kelime ayracı
data/                Girdi dosyaları, örnekler ve üretilen çıktılar
CMakeLists.txt       Derleme ve test hedefleri
CMakePresets.json    GCC / Ninja debug yapılandırması
run.cmd              Derlenmiş uygulama için Windows başlatıcısı
```

## Testler

Yapılandırma ve derleme sonrasında çalıştırın:

```powershell
ctest --preset debug
```

CTest, C çekirdek testlerini çalıştırır. Yapılandırma sırasında Python 3 bulunmuşsa gerçek uygulama üzerinde **12 senaryolu** CLI test paketi de çalışır. Python yoksa CLI paketi eklenmez; çekirdek testleri çalışmaya devam eder.

Testler dönüşümleri, Türkçe kodlamaları, dört ayracı, istatistikleri, girdi ve çıktı sınırlarını, dosya işlemlerini, kalıcı ayarları, geçersiz girdileri ve girdi sonunu kapsar. CLI paketi ayarları geri yükler, kendi oluşturduğu dosyaları siler ve mevcut örnek dosyaların değişmediğini kontrol eder. Test sırasında ayarlar geçici olarak değiştirildiği için uygulamayı kapalı tutun.

## Lisans

Telif hakkı © 2026 Mustafa Mert Akceylan. [Apache Lisansı 2.0](LICENSE) kapsamında lisanslanmıştır. Atıf bilgileri için [NOTICE](NOTICE) dosyasına bakın.
