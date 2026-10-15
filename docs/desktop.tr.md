# MorseCore masaüstü

[English](desktop.md) · [Ana sayfa](../README.tr.md)

Windows arayüzü; metin ve dosya dönüşümünü, Morse sesini, seslendirmeyi ve alıştırmayı aynı yerde toplar. Bu, 0.3.0 için geliştirilen bir önizlemedir. İmzalı bir kurulum sürümü henüz yayımlanmadı.

## Görünüm ve kullanım

- RAW Akademi açık renkleri birebir: zeytin `#5E6A42`, krem `#FAF8F2`, beyaz yüzey `#FFFFFF`, metin `#2C2A22`, kenarlık `#E7E4DF`. Koyu temada aynı zeytin rengi menü/üst çubukta kullanılır; yüzeyler daha açık yeşil, ana düğmeler kremdir.
- Uygulamaya ait üst çubuk ve kapatma düğmesi; Windows'a tanımlanan başlık alanı, fare basılıyken taşınır ve bırakılınca durur. Logo ve uygulama adı pencerenin ortasındadır. Pencere boyutu kilitlidir; yeniden boyutlandırma, büyütme ve küçültme engellenir. Açılış boyutu ekranın çalışma alanına sığdırılır.
- MorseCore logosu üst çubukta, menüde, açılış ekranında ve uygulama simgesinde kullanılır. **Sistem / Açık / Koyu** tema seçimi Ayarlar bölümündedir.
- Atkinson Hyperlegible metin yazı tipi, Crimson Pro başlıklar; Morse alanlarında eş aralıklı yazı.
- Daraltılabilen menü; dönüştürücü, dosyalar, ses stüdyosu, alıştırma ve alfabe. Ayarlar altta. Dar menüde açma düğmesi ve tüm simgeler aynı eksende ortalanır; Arapça yerleşim de buna dahildir.
- Kısa sayfa geçişleri ve menü animasyonu. Windows'un hareket tercihi ve uygulamadaki **Hareketi azalt** seçeneği dikkate alınır. Boşta sürekli çalışan animasyon yoktur.
- Türkçe, İngilizce, Almanca, Rusça ve Arapça arayüz. Arapçada menü sağa geçer; Morse alanları soldan sağa kalır.
- Kompakt dönüştürücüde kopyalama/kayıt, Morse dinleme ve çevrimdışı seslendirme aynı ekrandadır. `Ctrl+Enter` dönüştürür.

Ayarlardaki **Açık kaynağa teşekkürler** bölümünde EMA Lightning, Antalia Mini, Piper, normalizer-tr, PyTorch, ONNX Runtime ve ses modellerinin geliştiricileri, lisans adları ve kaynak bağlantıları yer alır. Motor ve model lisansları ayrıdır.

Arayüz dili, dönüşüm alfabesi ve konuşma dili ayrı seçimlerdir. Örneğin Türkçe arayüzde Rusça bir mesaj dönüştürülebilir. Morse'dan metne dönerken alfabe seçilmelidir; nokta ve çizgiler alfabe bilgisi taşımaz.

## Çalıştırma

Windows x64, .NET 10 SDK, Windows SDK 10.0.26100, WinUI derleme araçları, PowerShell 7 ve [C23 araçları](../README.tr.md#derleme-ve-çalıştırma) gerekir. NuGet bağımlılıkları ilk derlemede indirilir. Uno SDK sürümü `global.json` içinde sabittir.

```powershell
pwsh -File scripts/run-desktop.ps1
```

Kökteki `run-gui.cmd` aynı işlemi başlatır. Release derlemesi için:

```powershell
pwsh -File scripts/run-desktop.ps1 -Configuration Release
```

Arayüz C23 DLL'yi kullanır; konsol uygulaması kendi başına çalışmaya devam eder. Tercihler konsol ayarlarından ayrı olarak `%LOCALAPPDATA%/MorseCore/Desktop/settings.json` dosyasında tutulur. `MORSECORE_DESKTOP_HOME`, bu dosya için başka bir mutlak klasör seçebilir. Mesajlar tercih dosyasına yazılmaz.

## Alfabeler ve sayımlar

| Seçim | Davranış |
|---|---|
| Latin / Türkçe | Mevcut Latin tablosu; Türkçe harfler Latin karşılıklarına dönüşür |
| Rusça | Kiril harfleri; küçük harfler büyür, `Ё` ve `ё`, `Е` olur |
| Arapça | 28 temel harf ve bağımsız hemze; hareke ve tatvil atlanır |

Arapçada `آ أ إ ٱ → ا`, `ؤ → و`, `ئ ى → ي`, `ة → ه` uygulanır. `٠…٩` rakamları `0…9`, Arapça virgül/noktalı virgül/soru işareti Latin karşılıklarına dönüşür. Bunlar belgelenmiş, kayıplı normalizasyon kurallarıdır; özgün yazım geri getirilemez. Diğer alfabelerden karışık harfler, emoji, desteklenmeyen işaretler ve ayrıştırılmış Unicode birleşim dizileri sessizce atlanmaz; hata verilir.

Rusça/Arapça temel eşlemelerinin kaynağı [Signal Corps TM 11-459, Ek VII, basılı s. 71–72](https://www.govinfo.gov/content/pkg/GOVPUB-W-824b48e17943859ed2af2b45952379df/pdf/GOVPUB-W-824b48e17943859ed2af2b45952379df.pdf). Bunlar [ITU'nun Latin tablosundan](https://www.itu.int/rec/R-REC-M.1677-1-200910-I) ayrı profillerdir. Ortak rakam ve noktalama tablosu kullanılır. Alfabe ekranından mevcut eşlemeler görülebilir.

Karakter sayıları bayt veya UTF-16 kod birimi sayısı değildir; Unicode kod noktaları sayılır, başlangıç BOM'u sayılmaz. Arapça harekeler giriş karakteri sayısına dahildir, dönüştürülen karakter sayısına dahil değildir. Kelimeler boşluk sınırlarına göre sayılır. Birleştirilmiş görsel karakterleri sayan genel bir grapheme algoritması uygulanmaz.

## Dosyalar ve ses

Dosya dönüşümü UTF-8 `.txt` dosyalarını sırayla işler. Sınır dosya başına 8 MiB'dir. Sonuç önce geçici dosyaya yazılır, sonra yayımlanır; mevcut hedef dosyası değiştirilmez. Hedef klasör ve alfabe işlem başında sabitlenir. Native dönüşüm başladıktan sonra kesilmez; iptal dosyalar arasında/yazım öncesinde denetlenir.

Morse sesi Windows'ta çalınabilir ve WAV olarak kaydedilebilir. Hız 5–60 WPM, frekans 200–2000 Hz aralığındadır. Alıştırmalar 10 soruluk oturumlarda Latin harfleri, rakamlar veya kısa kelimeler kullanır. Sayfalar ve tema arasında geçiş yaparken oturum korunur.

Seslendirme [çevrimdışı sinir ağı modellerini](offline-speech.tr.md) kullanır: Türkçede EMA Lightning ve Antalia Mini; Arapça, Almanca, Rusça ve İngilizcede Piper. İsteğe bağlı paket `setup-speech.cmd` ile bir kez kurulur, ses dönüştürücüden seçilir. Windows ses paketleri kullanılmaz. Model ayrı bir işlemde yalnızca gerektiğinde açılır; iş bitince işlem kapanır. Sınır 4000 karakterdir. Küçük ağırlık dosyası düşük aktif RAM anlamına gelmez; ölçümler ve modellerin ayrı lisansları ses kılavuzundadır.

## Güncelleme ve MSIX

Açılış ekranı tercihleri ve dönüşüm motorunu hazırlar. Ağ kontrolü ana ekranı engellemez. Uygulama en fazla altı saatte bir GitHub'ın son kararlı Release sürümünü kontrol eder; taslak, ön sürüm ve eski sürümler önerilmez. Ayarlardan elle kontrol edilebilir veya açılış kontrolü kapatılabilir. Sürüm kontrolü dosya indirip çalıştırmaz.

İmzasız doğrulama paketi:

```powershell
pwsh -File scripts/package-desktop.ps1 -Unsigned
```

Komut, gitignore içindeki `cmake-build-desktop-package/` altında benzersiz bir klasörde `MorseCore.msix`, `MorseCore.appinstaller` ve `SHA256SUMS` üretir. İmzasız paket normal dağıtım için kullanılamaz. Komut uygulama kurmaz, sertifika oluşturmaz veya sertifikaya güven eklemez.

İmzalı paket için geçerli özel anahtarı olan sertifika parmak izi verilir:

```powershell
pwsh -File scripts/package-desktop.ps1 -CertificateThumbprint <40-haneli-parmak-izi>
```

Mevcut paket yayıncısı `O=Mustafa Mert Akceylan` değeridir; sertifika kimliğiyle birebir eşleşmelidir. Gerçek dağıtım sertifikasının konusu farklıysa yayıncı ve denetim birlikte güncellenmelidir. Güvenilir sertifika ve yayın süreci henüz kurulmadı.

Otomatik kurulum/güncelleme için imzalı MSIX ile App Installer dosyasının GitHub Releases altında sabit dosya adlarıyla yayımlanması ve uygulamanın `.appinstaller` üzerinden kurulması gerekir. Sadece depoya commit atılması güncelleme oluşturmaz. Kurulum ve iki gerçek sürüm arasındaki güncelleme testi yayın öncesi gereklidir. [App Installer belgeleri](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview).

## Test ve performans

```powershell
cmake --preset release -DMORSECORE_BUILD_DOTNET_BINDINGS=ON
cmake --build --preset release
ctest --preset release
pwsh -File scripts/test-desktop.ps1 -MouseInput
```

GUI testi ayrı tercihlerle çalışır; ekran görüntüleri ve JSON sonucu `cmake-build-desktop-tests/` içine yazılır. Duyulabilir ses çıkarmaz. Native dönüşümü, sayaçları, tema/RTL yerleşimini, menü animasyonlarını, alıştırma düğmelerini, çevrimdışı ses kataloğunu ve beş dilde seslendirme düğmesinin görünürlüğünü denetler. EMA kuruluysa gerçek üretim yapar ve sessize alınmış MediaPlayer oynatımını tamamlar. İşletim sistemi dosya seçicileri ile fiziksel ses çıkışı bu otomasyonun kapsamı dışındadır.

`-MouseInput`, test penceresine fare girdisi göndererek basma–taşıma–bırakma, tek tıklama ve ikinci sürüklemeyi denetler. Ön plandaki pencere başka uygulamaya geçerse girdi gönderimini durdurur. Fare testinin gerçekten çalışıp çalışmadığı JSON raporunda ayrıca belirtilir.

Sayfalar gerektiğinde oluşturulur; kapatılan sayfaların kontrol ağacı saklanmaz. Uzun dönüşümler, dosyalar ve ses işleri UI iş parçacığında çalıştırılmaz. Dosyalar paralel olarak belleğe alınmaz. Windows pilotunda yayın kırpması kapalıdır: kullanılan WinRT bileşenleriyle kırpılmış pakette açılış hatası görüldü. Native AOT/kırpma iyileştirmeleri, çalışan dağıtım üzerinde ayrıca ölçülüp doğrulanmadan açılmayacak.

## Sonraki platformlar

Windows'ta gerçek WinUI, macOS/Linux için Uno'nun masaüstü hedefi aynı XAML ekranlarını kullanır. [Uno platform desteği](https://platform.uno/docs/articles/getting-started/requirements.html).

1. Windows önizlemesinde tasarım geri bildirimi, büyük dosya/bellek ölçümleri ve ses/dosya seçici QA'sı.
2. Güvenilir MSIX imzası; gerçek kurulum ve güncelleme doğrulaması.
3. Aynı ekranlarla macOS: C23 `.dylib`, yerel ses/TTS/dosya seçicileri, cihaz testi ve imzalı dağıtım.
4. Aynı ekranlarla Linux: C23 `.so`, yerel ses/TTS/dosya seçicileri, masaüstü erişilebilirliği ve dağıtım paketleri.

Ortak masaüstü hedefinin Windows'ta derlenmesi, macOS veya Linux uygulamasının o cihazlarda çalıştığı anlamına gelmez. Bu iki platformun GUI çalıştırma ve dağıtım testleri sonraki aşamadadır.
