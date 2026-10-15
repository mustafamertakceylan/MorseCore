<div align="center">

# MorseCore

<img src="apps/MorseCore.Desktop/MorseCore.Desktop/Assets/Brand/morsecore.png" width="92" alt="MorseCore logosu">

**Morse kodunu dönüştürün, dinleyin ve alıştırma yapın.**

Metin ve dosyaları Morse koduna çevirin, mesajları çözün, ses dosyası oluşturun; okuyarak veya dinleyerek alıştırma yapın. Windows masaüstü önizlemesini, konsol menüsünü veya betiklerle dönüşümü kullanın.

![C23](https://img.shields.io/badge/C-23-225E85?style=flat-square&logo=c&logoColor=white)
[![Sürüm](https://img.shields.io/badge/Version-0.3.0-5e6a42?style=flat-square)](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.3.0)
![CMake](https://img.shields.io/badge/CMake-3.28%2B-064F8C?style=flat-square&logo=cmake&logoColor=white)
[![Lisans](https://img.shields.io/badge/License-Apache%202.0-2A9D8F?style=flat-square)](LICENSE)

[English](README.md) · **Türkçe**

</div>

---

## İçindekiler

- [İndir](#indir)
- [Ne yapar](#ne-yapar)
- [Özellikler](#özellikler)
- [Derleme ve çalıştırma](#derleme-ve-çalıştırma)
- [Masaüstü önizlemesi](#masaüstü-önizlemesi)
- [Konsol menüsü](#konsol-menüsü)
- [Komut satırı](#komut-satırı)
- [Ses ve WAV](#ses-ve-wav)
- [Alıştırma](#alıştırma)
- [Dosyalar ve ayarlar](#dosyalar-ve-ayarlar)
- [Desteklenen girdiler](#desteklenen-girdiler)
- [İstatistikler](#istatistikler)
- [Taşınabilir paket](#taşınabilir-paket)
- [DLL ve .NET bağlantısı](#dll-ve-net-bağlantısı)
- [Testler ve otomasyon](#testler-ve-otomasyon)
- [Klasör yapısı](#klasör-yapısı)
- [Lisans](#lisans)
- [Teşekkürler](#teşekkürler)

## İndir

**[Windows x64 masaüstü önizlemesini indir](https://github.com/mustafamertakceylan/MorseCore/releases/download/v0.3.0/MorseCore-0.3.0-Windows-x64-Desktop.zip)** · [Sürüm notları ve dosya özetleri](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.3.0) · [Değişiklik günlüğü](CHANGELOG.md)

ZIP'in tamamını yazılabilir bir klasöre açıp **MorseCore.Desktop.exe** dosyasını çalıştırın. DLL'leri ve alt klasörleri bir arada tutun. Masaüstü paketi .NET/Windows App SDK çalışma zamanını içerir; uygulamayı kullanmak için kaynak koddan derlemeniz gerekmez. Bu, imzasız bir Windows önizlemesidir; MSIX kurulum paketi değildir.

Seslendirme isteğe bağlıdır. Python 3.11–3.13 kurulu bir bilgisayarda **setup-speech.cmd** dosyasını bir kez çalıştırın; kurulum internet ister, ardından ses üretimi çevrimdışı kalır. Model ağırlıkları ve ses bağımlılıkları ayrıca indirilir; sürüm ZIP'ine dahil değildir. Ayrıntılar [ses rehberinde](docs/offline-speech.tr.md).

Konsol uygulaması ve native DLL için ayrı `MorseCore-0.3.0-Windows-AMD64.zip` dosyasını indirin. macOS ve Linux masaüstü sürümleri henüz plan aşamasındadır.

## Ne yapar

MorseCore harfleri, rakamları ve desteklenen noktalama işaretlerini nokta ve çizgilere dönüştürür; Morse kodunu yeniden okunabilir metne çevirir. Bilgisayarınızda çalışır, arayüz dili ve kelime ayracı tercihlerinizi sonraki açılışa taşır.

Örneğin `HELLO WORLD` metni şu koda dönüşür:

```text
.... . .-.. .-.. --- / .-- --- .-. .-.. -..
```

Çözümleme `HELLO WORLD` sonucunu verir. Türkçe harfler Latin karşılıklarına dönüştürülür; özgün yazımları Morse kodundan geri getirilemez.

## Özellikler

| Özellik | Yapabilecekleriniz |
|---|---|
| Metin ve dosyalar | Yazılan mesajları, standart girdiyi veya `.txt` dosyalarını iki yönde dönüştürme |
| Komut satırı | Tek bir dönüşümü otomatikleştirme veya klasördeki metin dosyalarını topluca işleme |
| Ses | Windows'ta Morse dinleme veya PCM WAV dosyası oluşturma |
| Alıştırma | Rastgele harf, rakam veya kısa kelimeleri okuyarak ya da dinleyerek cevaplama |
| Arayüz | İngilizce/Türkçe konsol; açık/koyu temalı, beş dilli Windows masaüstü önizlemesi |
| Masaüstü alfabeleri | Alfabe seçerek native kitaplık üzerinden Rusça ve Arapça metin dönüşümü |
| Taşınabilir Windows paketi | Kurulu klasörü yeniden derlemeden taşıma |

## Derleme ve çalıştırma

Gereksinimler: **CMake 3.28+**, **C23 destekleyen GCC 15+ veya Clang 18+** ve hazır yapılandırmalar için **Ninja**. Python 3 bütünleşme testlerinde gereklidir; uygulamayı çalıştırmak için gerekmez. Derleme araçlarının terminalden erişilebilir olması gerekir.

```powershell
git clone https://github.com/mustafamertakceylan/MorseCore.git
cd MorseCore
cmake --preset debug
cmake --build --preset debug
.\run.cmd
```

Windows'ta derlemeden sonra `run.cmd` dosyasına çift tıklayabilirsiniz. Kaynak klasöründeki başlatıcı, `data/` ve `config/` için proje kökünü kullanır. Önce yanındaki paketlenmiş uygulamayı, sonra debug, ardından release derlemesini arar.

Yerel Windows taşınabilirlik kontrolleri taşınmış kurulumları, boşluklu yolları, Unicode adları ve uzun yolları kapsar. Linux derleme ve test otomasyonu yapılandırılmıştır; uzaktaki çalışma durumu [Testler ve otomasyon](#testler-ve-otomasyon) bölümündedir.

## Masaüstü önizlemesi

0.3.0 Windows önizlemesi; daraltılan menü, Ayarlar'dan seçilen açık/koyu tema, kısa geçişler, beş arayüz dili ve Arapça sağdan sola yerleşim sunar. Özel üst çubukta logo ortadadır; pencere basılı tutarak taşınır ve fare bırakılınca durur. Dönüşüm, dosyalar, ses, alıştırma ve ayarlar C23 motora bağlıdır. Türkçede **EMA Lightning** ve **Antalia-2 Mini**, İngilizce, Almanca, Rusça ve Arapçada Piper ile çevrimdışı seslendirme yapılır. [Ses paketinin kurulumu ve lisansları](docs/offline-speech.tr.md).

.NET 10, Windows SDK/WinUI araçları ve PowerShell 7 kurulduktan sonra:

```powershell
pwsh -File scripts/run-desktop.ps1
```

Gereksinimler, alfabe normalizasyonu, MSIX, testler ve ortak macOS/Linux planı için [masaüstü rehberine](docs/desktop.tr.md) bakın. İmzalı kurulum ve gerçek güncelleme testi bekliyor; Mac ve Linux arayüz sürümleri sonraki aşamadır.

## Konsol menüsü

Argümansız çalıştırınca menü açılır. Numarayı yazıp Enter'a basın:

```text
1  Metinden Morse koduna
2  Morse kodundan metne
3  Metin dosyasından Morse koduna
4  Morse dosyasından metne
5  Morse tablosu
6  MorseCore hakkında
7  Ayarlar
8  Sesli Morse / WAV
9  Morse alıştırması
0  Çıkış
```

**1** seçeneğinde `HELLO WORLD` yazıp sonucu ve istatistikleri görün. Morse çıktısını kaydetmek için `y`, devam etmek için `n` yazın. Morse kodunu çözmek için **2** seçeneğini kullanın; bu seçenek metni gösterir, kaydetme sorusu sormaz.

Her Morse harfi ayrı bir gruptur ve gruplar boşluk karakterleriyle ayrılır. Kelime ayracı, `... --- ... / .-` örneğindeki gibi tek başına durmalıdır. Çözümleme, kayıtlı tercihiniz farklı olsa da girdideki `/`, `|`, `#` veya `*` ayracını algılar.

## Komut satırı

Aşağıdaki PowerShell örnekleri proje kökünden çalıştırılır:

```powershell
.\run.cmd --help
.\run.cmd --version
.\run.cmd --encode "HELLO WORLD"
.\run.cmd --decode ".... . .-.. .-.. --- / .-- --- .-. .-.. -.."
.\run.cmd --encode "Merhaba Dünya" --separator "|"
.\run.cmd --encode --input "message.txt" --output "message.morse.txt"
.\run.cmd --decode --input "message.morse.txt" --output "decoded.txt"
"SOS" | .\run.cmd --encode --input -
```

Dönüşüm girdisi tek bir tırnaklı metin argümanı, `--input DOSYA` veya girdi sonuna kadar okunan standart girdi olabilir. `--input -`, standart girdiyi açıkça seçer. `--output` verilmezse standart çıktıya yalnızca sonuç yazılır; hata ve durum mesajları standart hata kanalına gider. CLI yolları çalışma dizinine göre veya mutlak olarak verilir; `data/` içindeki dosya adlarıyla sınırlı değildir.

CLI ayracı varsayılan olarak `/` olur; menüde kaydedilmiş tercih okunmaz. Kodlamada başka ayraç seçmek için `--separator` kullanın. Çözümleme desteklenen ayracı otomatik algılar.

### Bir klasörü topluca dönüştürme

```powershell
.\run.cmd --batch "input" --encode --output-dir "encoded"
.\run.cmd --batch "encoded" --decode --output-dir "decoded"
```

Toplu işlem, normal `.txt` dosyalarını dosya adı sırasıyla okur; alt klasörlere girmez. `message.txt` dosyası kodlanınca `message.morse.txt`, çözülünce `message.text.txt` oluşur. Girdi ve çıktı klasörleri farklı olmalıdır. Üst klasörü mevcutsa çıktı klasörü oluşturulabilir; kaynak klasör önceden var olmalıdır.

Toplu işlem dahil mevcut metin ve WAV çıktıları varsayılan olarak korunur. Üzerlerine yazmak istediğinizde **`--overwrite`** ekleyin. Girdi dosyası kendi çıktısı olarak kullanılamaz. Toplu işlem bildirimleri ve başarı/hata sayıları standart hata kanalına yazılır; tek bir dosyanın başarısız olması bile komutun hata koduyla bitmesine yol açar.

Çıkış kodları: `0` başarı, `1` dönüşüm/dosya/ses hatası, `2` geçersiz argümanlar.

## Ses ve WAV

**8** menüsünü açın, düz metni girin, hız ve tonu seçin. Varsayılanı korumak için Enter'a basın. Dinletmek için **1**, WAV kaydetmek için **2**, kaydedip dinletmek için **3** seçin. Menüden oluşturulan sesler `data/` altında numaralı `morse_audio_*.wav` dosyalarına kaydedilir.

```powershell
.\run.cmd --encode "SOS" --wav "sos.wav"
.\run.cmd --encode "SOS" --play --wpm 15 --frequency 700
```

| Ses ayarı | Varsayılan | Desteklenen aralık |
|---|---|---|
| Hız | 20 WPM | 5–60 WPM |
| Ton | 600 Hz | 200–2000 Hz |
| Örnekleme hızı | 22050 Hz | Ses çekirdeğinde 8000–48000 Hz; menü ve CLI varsayılanı kullanır |

WAV çıktısı **16 bit tek kanallı PCM** biçimindedir. Zamanlama nokta için 1, çizgi için 3; harf içi, harfler arası ve kelimeler arası boşluklar için 1/3/7 birim kullanır. Ses, akış tamponlarıyla üretilir. Ses işlemleri en fazla **10 dakika** ve **1 MiB Morse girdisi** ile sınırlıdır.

Canlı oynatma Windows'ta kullanılabilir; ses cihazı açılamazsa hata bildirilir. WAV üretimi ses cihazı gerektirmez. `--wav` ve `--play`, tek bir `--encode` işlemiyle kullanılır; toplu işlemde kullanılamaz.

## Alıştırma

**9** menüsünde Morse okuyarak veya dinleyerek cevaplamayı, ardından harfleri, rakamları ya da kısa İngilizce kelimeleri seçin. Kelime kümesinde 32 ASCII kelime bulunur. Soru sayısı 1–100 arasındadır, varsayılan 10'dur. Dinleme yöntemi ayrıca hız ve tonu sorar; Windows ses oynatması gerektirir.

Cevabı düz metin olarak yazın. ASCII büyük/küçük harf farkı ve çevredeki boşluklar dikkate alınmaz. Her cevap o soruyu bitirir ve beklenen cevabı gösterir.

- Atlamak için Enter'a basın veya `:skip` yazın.
- Geçerli soruyu tekrar görmek ya da dinlemek için `:replay` yazın.
- Erken bitirmek için `:quit` yazın.

Özet; doğru, yanlış, atlanan ve tamamlanan soru sayılarını gösterir. Doğruluk `doğru / tamamlanan × 100` olarak hesaplanır; atlanan sorular da tamamlananlara dahildir. Sonuçlar yalnızca oturum boyunca tutulur.

## Dosyalar ve ayarlar

Konsoldaki **3** ve **4** seçenekleri için dosyaları etkin `data/` klasörüne koyup `message` veya `message.txt` gibi bir ad girin. Gerektiğinde `.txt` eklenir. Dönüşümler numaralı `morse_output_*.txt` veya `decoded_output_*.txt` dosyalarına otomatik kaydedilir. Mevcut adlar atlanır.

Varsayılan olarak `data/` ve `config/`, uygulamanın yanında oluşturulur. Paketteki `run.cmd` kendi klasörünü, kaynak klasöründeki `run.cmd` proje kökünü kullanır. Ayrı ve önceden var olan yazılabilir bir kök seçmek için **`MORSECORE_HOME`** değişkenini ayarlayıp uygulamayı doğrudan açın; `run.cmd` bu değişkeni kendisi ayarlar:

```powershell
$env:MORSECORE_HOME = "C:\MorseCore Data"
.\cmake-build-release\MorseCore.exe
```

**7** menüsü kelime ayracını ve arayüz dilini `config/settings.txt` dosyasına kaydeder. Varsayılanlar:

```ini
word_separator=/
language=EN
```

Diller `EN` ve `TR` seçenekleridir. Türkçe menü metinleri terminal uyumluluğu için ASCII yazımlıdır; Türkçe girdi ve Unicode dosya yolları desteklenir. Terminal destekliyorsa renkler açılır. Yönlendirilen çıktı ANSI kaçış dizileri içermez; `NO_COLOR` değişkenini ayarlamak renkleri kapatır.

Projenin `data/` klasöründe sonradan oluşan `.txt` dosyaları Git tarafından yok sayılır; önceden takip edilen örnekler takip edilmeye devam eder.

## Desteklenen girdiler

Konsol ve CLI aşağıdaki Latin/Türkçe alfabeyi kullanır. Masaüstü ve native profil API'si Rusça ve Arapçayı da destekler; [normalizasyon kurallarına](docs/desktop.tr.md#alfabeler-ve-sayımlar) bakın.

`A–Z`, `a–z`, `0–9` ve ASCII boşluk karakterleri kabul edilir. UTF-8 veya desteklenen Windows-1254 alt kümesindeki Türkçe harfler dönüştürülür:

```text
Ç/ç → C    Ğ/ğ → G    İ/ı → I
Ö/ö → O    Ş/ş → S    Ü/ü → U

Merhaba Dünya → MERHABA DUNYA
```

Başlangıçtaki UTF-8 BOM kabul edilir; dönüşüme ve karakter sayılarına eklenmez. Diğer alfabeler, emoji, desteklenen eski kodlama alt kümesi dışındaki bozuk girdiler ve bilinmeyen Morse grupları reddedilir.

Desteklenen ITU noktalama işaretleri:

```text
. , ? ' / ( ) : = + - " @
```

Uygulama ayrıca yaygın uzantılar olan **`! & ; _ $`** işaretlerini destekler; bu beş işaret ITU standardı olarak sunulmaz. Düz metindeki gerçek `/` işareti `-..-.` olarak kodlanır; Morse girdisinde tek başına duran ayraç kelime sınırını belirtir.

Kodlama, kelimeler arasındaki boşluk karakterlerini tek ayraca dönüştürür; baştaki ve sondaki boşlukları kaldırır. Çözümleme büyük harf üretir. Metin girdisi ve çıktısı, kullanılabilir bellek ve temsil edilebilir boyutlarla sınırlı olarak dinamik büyür. Bellek ayırma ve boyut taşması sorunları hata döndürür; sesin ayrı sınırları yukarıda belirtilmiştir.

<a id="istatistikler"></a>

## İstatistikler

Konsol dönüşümleri girdi/çıktı karakter sayılarını, kelime sayısını, dönüştürülen karakterleri ve gerçek boşlukları gösterir. Desteklenen Türkçe harfler bir kez sayılır. Dönüştürülen karakterlere desteklenen noktalama işaretleri dahildir. Kelime, dönüştürülen karakter ve boşluk sayıları düz metin tarafında hesaplanır: kodlamada girdi, çözümlemede çıktı kullanılır.

ASCII boşluk karakterleri kelimeleri ayırır; boşluk sayısına yalnız gerçek boşluk karakteri eklenir. Örneğin `Merhaba Dünya` 13 karakter, dönüştürülen 12 harf, 2 kelime ve 1 boşluk içerir. Çıkış özeti geçerli oturumdaki dönüşümleri, dosya işlemlerini, geçersiz girdileri ve kaydedilen çıktıları sayar.

## Taşınabilir paket

Yerel release derlemesini oluşturun, test edin ve paketleyin:

```powershell
cmake --preset release
cmake --build --preset release
ctest --preset release
cpack --preset release
cmake --install cmake-build-release --prefix "C:\Apps\MorseCore"
```

CPack, ZIP dosyasını `cmake-build-release/` içine yazar. Windows AMD64 üzerinde 0.3.0 sürümünün dosya adı `MorseCore-0.3.0-Windows-AMD64.zip` olur. Yazılabilir bir klasöre açıp `MorseCore.exe` veya `run.cmd` çalıştırın. Kurulum komutu, ZIP'i açmaya alternatiftir. Kurulu klasörü bir arada tutun; uygulama, başlatıcı, belgeler, lisans ve varsayılan yapılandırmayı içerir.

## DLL ve .NET bağlantısı

C23 çekirdek, konsolun yanında paylaşılan kitaplık olarak derlenir. Windows'ta
`MorseCore.Native.dll`; dönüşüm, istatistik, alfabe, alıştırma, WAV üretimi ve
iptal edilebilir oynatma için sürümlü C API sunar. Konsol aynı çekirdek kodunu
kullanır ve tek başına çalışmaya devam eder.

Paket `include/morsecore_native.h` başlığını ve bağlama kitaplığını içerir. DLL
çağrıları katı UTF-8 kullanır; veri klasörü oluşturmaz veya konsol ayarlarını
okumaz. Dönen nesneler DLL'nin kendi serbest bırakma fonksiyonlarıyla kapatılır.
Ayrıntılar [native API rehberinde](docs/native-api.tr.md).

.NET 10 kitaplığı, `LibraryImport` bağlantısı ve `SafeHandle` ile kaynak yönetimi
sağlar. WinUI masaüstü uygulaması ve arayüzsüz bütünleşme testleri bu kitaplığı
kullanır. Ortak XAML ekranları Uno'nun masaüstü hedefinde de derlenir;
macOS/Linux cihaz testleri ve platform servisleri sonraki aşamadadır.

```powershell
cmake --preset interop
cmake --build --preset interop
ctest --preset interop
```

Native derleme .NET SDK gerektirmez; `interop` için .NET 10 gerekir. Windows'ta
Clang kullanırken Visual Studio x64 geliştirici terminalinde `clang-debug`
yapılandırmasını seçin. CMake; nihai C23 modunu, taşma denetimli aritmetiği,
atomikleri ve iş parçacığına özel belleği sınar; C17'ye sessizce geri dönmez.

## Testler ve otomasyon

```powershell
ctest --preset debug
ctest --preset release
```

C testleri dönüşümleri, noktalama işaretlerini, dinamik tamponları, ses zamanlamasını/WAV yapısını ve alıştırma puanlamasını kapsar. Python bütünleşme testleri konsol/CLI akışlarını, toplu işlemde çıktı korumasını, ayarları ve taşınmış kurulumları kapsar. Python olmadan bütünleşme testleri eklenmez. Testler sırasında uygulamayı kapatın; testler ayarları geçici olarak değiştirip geri yükleyebilir.

[CI](.github/workflows/ci.yml), push, pull request ve elle çalıştırma için yapılandırılmıştır. Ortak [derleme akışı](.github/workflows/build.yml), Windows'ta GCC UCRT64 ve Ubuntu 24.04'te Clang 18 ile derleme ve test yapacak şekilde ayarlanmıştır. Başarılı Windows işleri ZIP ve `SHA256SUMS` dosyalarını `MorseCore-Windows-x64` adıyla yükleyecek ve 14 gün saklayacak şekilde yapılandırılmıştır.

[Release akışı](.github/workflows/release.yml), CMake proje sürümüyle eşleşen bir `v<sürüm>` etiketi, şu anda `v0.3.0`, ve GitHub Release yayımlamadan önce başarılı Windows/Linux kontrolleri gerektirir. Yerel kontroller ve GitHub Actions sonuçları ayrıdır; uzaktaki durum için deponun Actions sekmesine bakın. Otomatik ses testleri fiziksel donanımda duyulabilir oynatmayı kanıtlamaz.

## Klasör yapısı

```text
src/                  Dönüşüm, konsol, CLI, ses, alıştırma, dosyalar ve ayarlar
include/              Başlık dosyaları ve ortak yapılandırma
tests/                C testleri ve Python bütünleşme testleri
config/settings.txt   Kayıtlı dil ve kelime ayracı
data/                 Konsol girdileri, örnekler ve üretilen çıktılar
.github/workflows/    CI, ortak derleme kontrolleri ve release otomasyonu
apps/                WinUI masaüstü ekranları ve ortak uygulama servisleri
third_party/         Özgün lisans ve üçüncü taraf atıfları
CMakeLists.txt        C23 çekirdek, DLL, konsol, test ve paketleme hedefleri
CMakePresets.json     GCC / Ninja debug ve release yapılandırmaları
run.cmd               Windows başlatıcısı
```

## Lisans

Telif hakkı © 2026 Mustafa Mert Akceylan. [Apache Lisansı 2.0](LICENSE) kapsamında lisanslanmıştır. Atıf bilgileri için [NOTICE](NOTICE) dosyasına bakın.

Üçüncü taraf motorlar, modeller, fontlar ve çalışma zamanı bileşenleri kendi lisanslarını korur. Ses ağırlıkları ayrıca kurulur. Türkçe motorların özgün lisansları ve Antalia'nın upstream bildirimi [third_party/speech](third_party/speech/README.md) altında saklanır; masaüstü ZIP'leri çalışma zamanı lisans envanterini de içerir.

## Teşekkürler

[EMA Lightning](https://github.com/canberk7/ema-lightning) için **Canberk Aslan'a**, [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) için **PatientDesk AI ekibine** teşekkür ederiz. Apache-2.0 lisanslı çalışmaları, MorseCore'un iki yerel Türkçe ses seçeneğini mümkün kılıyor. Bu sürüm EMA Lightning **1.1.2** ve Antalia Mini **1.0.1** kullanır; model revizyonları ses manifestinde sabittir.

Piper ses topluluğuna, Erdem Tuna'nın normalizer-tr projesine, PyTorch, ONNX Runtime, Uno Platform ve Windows App SDK ekiplerine; Atkinson Hyperlegible ve Crimson Pro yazarlarına da teşekkür ederiz. **Ayarlar → Açık kaynağa teşekkürler** bölümünde proje ve ses modeli bağlantıları bulunur. Bu teşekkürler özgün lisans/atıf dosyalarıyla birlikte sunulur; onların yerine geçmez ve geliştiricilerin MorseCore'u onayladığı anlamına gelmez.
