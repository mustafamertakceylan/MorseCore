# MorseCore native API

[English](native-api.md) · [Dışa açık başlık](../include/morsecore_native.h)

MorseCore 0.4.0, Windows'ta `MorseCore.Native.dll`, Linux'ta `libMorseCore.Native.so`
üzerinden ABI v1 sunar. Kitaplığı yüklemek dosya veya klasör oluşturmaz.
Uygulama ayarları ve kullanıcı verileri masaüstü katmanında yönetilir.

## Derleme

CMake 3.28+, Ninja ve yapılandırma sırasında sınanan C23 özelliklerini destekleyen
GCC 15+ veya Clang 18+ gerekir. C23 zorunludur; derleyici uzantıları kapalıdır.

```powershell
cmake --preset debug
cmake --build --preset debug
ctest --preset debug
```

`clang-debug`, `clang` derleyicisini kullanır. Windows'ta Windows SDK başlıklarının,
kitaplıklarının ve bağlayıcının bulunabilmesi için Visual Studio x64 geliştirici
terminalinden çalıştırın. Microsoft uyumlu derlemelerde CRT, MinGW derlemelerinde
derleyici çalışma zamanı statik bağlanır. Windows sistem DLL'leri yine gereklidir.
CMake paylaşılan çekirdeği ve testleri derler; masaüstü uygulaması ayrı derlenir.

## ABI ve bellek sahipliği

- Dış tüketiciler yalnız `morsecore_native.h` başlığını kullansın. C++ için `extern "C"`
  tanımlıdır; tüketicinin C++ derleyicisinde C23 desteği gerekmez.
- Diğer fonksiyonlardan önce `mc_abi_version() == MC_ABI_VERSION` denetlensin.
  ABI v1, cdecl, sabit genişlikli tam sayılar ve doğal yapı hizalaması kullanır.
  Yapı yerleşimleri sabittir; değiştirmek yeni ABI gerektirir. DLL ile süreç mimarisi eşleşmelidir.
- Başarı `MC_OK`, yani sıfırdır. Diğer sonuçlar başlıktaki sabitlerle belirtilir.
  Durum mesajları ödünç statik dizgelerdir; serbest bırakılmazlar.
- Girdi, uzunluğu bayt olarak verilen katı UTF-8'dir. İçinde NUL olamaz; sonunda
  NUL bulunması gerekmez. Başlangıç BOM'u kabul edilir.
  Dışa açık DLL API'si Windows-1254 kabul etmez. Bayt uzunluğu ve karakter sayısı ayrı kavramlardır.
- `mc_encode`, `mc_decode` ve `mc_practice_next` opak sonuç nesneleri döndürür.
  `mc_result_copy_text` NULL/0 ile çağrılınca **NUL dahil** gereken kapasiteyi verir;
  ardından kendi tamponunuzu sağlayın. Küçük tampon değiştirilmez. Kodlanmış
  çıktıda çekirdeğin sondaki boşluğu korunur. Kopyalama veya sonraki işlem hata
  verse de sonucu `mc_result_destroy` ile kapatın.
- Alıştırma ve iptal nesnelerini karşılık gelen DLL fonksiyonlarıyla oluşturup kapatın.
  Nesneleri `free`, `delete`, `Marshal.FreeHGlobal` veya başka DLL sürümüyle serbest
  bırakmayın. NULL kapatılabilir; iki kez kapatma veya kapatılmış nesneyi kullanma geçersizdir.
- Yapı çıktılarına bayt kapasitesi verilir: `sizeof(yapı)` kullanın. Küçük yapılar
  herhangi bir yazma yapılmadan reddedilir. Alanlar ve boyutlar başlıkta tanımlıdır.
- Nesneler ve çağrılar bitene kadar DLL yüklü kalsın. Bir nesneyi kullanım sırasında
  kapatmayın. Bağımsız oturumlar ve durumsuz dönüşümler paralel çalışabilir; aynı
  alıştırma oturumuna erişimi ve aynı dosyaya yazmayı çağıran taraf sıralamalıdır.

## İşlemler

| İşlem | Fonksiyonlar |
|---|---|
| Dönüşüm ve istatistik | `mc_encode`, `mc_decode`, `mc_result_copy_text`, `mc_result_statistics` |
| Morse tablosu | `mc_alphabet_count`, `mc_alphabet_get` |
| Unicode alfabe profilleri | `mc_encode_alphabet`, `mc_decode_alphabet`, `mc_alphabet_profile_count`, `mc_alphabet_profile_get` |
| Alıştırma | `mc_practice_create`, `mc_practice_next`, `mc_practice_submit`, `mc_practice_stats` |
| Ses | `mc_audio_defaults`, `mc_audio_analyze`, `mc_write_wav`, `mc_play` |
| Oynatmayı iptal etme | `mc_cancel_create`, `mc_cancel_request`, `mc_cancel_destroy` |

Sıfır ayraç, kodlama/ses için `/`, çözümleme için otomatik algılama demektir.
0.3.0'daki dört profil fonksiyonu mevcut ABI v1 çağrılarını ve struct yerleşimini
değiştirmeden eklenmiştir. Profiller `MC_ALPHABET_LATIN` (0),
`MC_ALPHABET_RUSSIAN` (1), `MC_ALPHABET_ARABIC` (2) değerleridir. Bilinmeyen profil
`MC_INVALID_ARGUMENT` döndürür; sayım fonksiyonu sıfır döndürür. Profil sembolü
Unicode kod noktasıdır; eski alfabe fonksiyonu ASCII kalır.
[Normalizasyon kuralları ve kaynakları](desktop.tr.md#alfabeler-ve-sayımlar).

İstatistikler başlangıç BOM'u dışında Unicode kod noktalarını sayar. Arapçada
atlanan hareke/tatvil giriş uzunluğuna dahildir, dönüştürülen karaktere dahil değildir.
Kelime sınırları ASCII boşluk karakterleri, NBSP ve dar NBSP'dir.

Alıştırma sorusu Morse olarak döner; beklenen cevap ancak yanıtlandıktan sonra
gösterilir. Boş cevap atlamadır. Başarısız yanıt veya bellek ayırma soruyu tüketmez.

Çağrılar bitene kadar bekletir (senkron çalışır). Arayüzde dönüşüm,
dosya ve oynatma işlerini arka planda çalıştırın. Oynatma tek kullanımlık iptal
nesnesi kabul eder; başka iş parçacığından iptal istenebilir. Önceden iptal edilen
çağrı ses cihazı açmaz. Windows oynatması iptali denetleyip sıradaki tamponları
sıfırlar. Oynatma ve bütün iptal çağrıları bitene kadar nesne canlı kalmalıdır.
Canlı oynatma Windows'a özeldir; WAV üretimi platformlar arasında kullanılabilir.

WAV üretimi verilen dosyanın üzerine yazar ve iptal desteği yoktur. Üzerine yazma
onayı ve kısmi çıktı hataları tüketici tarafından yönetilmelidir. WAV yolu NUL ile
biten UTF-8'dir. `mc_audio_last_error` çağıran iş parçacığına özeldir; `MC_AUDIO_ERROR`
sonrasında başka iş parçacığına geçmeden veya yeni ses çağrısı yapmadan kopyalayın.

## .NET bağlantısı

`bindings/dotnet/MorseCore.Interop`, WinUI bağımlılığı olmadan .NET 10 hedefler.
Kaynak üretilen `LibraryImport`, cdecl imzaları, katı UTF-8 ve `SafeHandle` kullanır.
Native nesneler kapatılmadan sonuçlar .NET nesnelerine kopyalanır. Alıştırma
çağrıları ve kapatma sıralanır. `MorseAudio.PlayAsync` arka planda çalışıp
`CancellationToken` isteğini native iptal nesnesine iletir. XAML veya arayüz tasarımı içermez.

```powershell
cmake --preset interop
cmake --build --preset interop
ctest --preset interop
```

Arayüzsüz tüketici gerçek kitaplığı yükleyip dönüşüm, istatistik, hatalar, paralel
çağrılar, alıştırma, WAV ve cihaz açmadan iptali kontrol eder. Masaüstü paketinde
uygun DLL uygulamanın yanında bulunmalıdır. Bağlantı katmanı güvenli native
kitaplık arama yollarını kullanır. C derlemesi için .NET isteğe bağlıdır.
