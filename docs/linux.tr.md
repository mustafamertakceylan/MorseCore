# Linux'ta MorseCore

Linux sürümü Windows ile aynı XAML arayüzünü, C23 motorunu ve
`MorseCore.AppCore` servislerini kullanır. Dil dosyaları, fontlar ve görseller
aynı kaynaktan gelir. İlk hedef **x86-64 ve glibc** kullanan Linux'tur;
ARM ve musl/Alpine bu paketin kapsamında değildir.

Linux paketlerini [Releases sayfasından](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v1.0.1) indir. Uygulama,
aşağıdaki dört sanal makinede ortak arayüz ve sekiz çevrimdışı ses dahil
test edildi. Fiziksel mikrofon ve iki yayımlanmış Linux sürümü arasında canlı
GitHub yükseltmesi henüz doğrulanmadı.

## Hedef dağıtımlar ve VM sonuçları

| Dağıtım | Paket | Test edilen VM | Paket yaşam döngüsü |
|---|---|---|---|
| Ubuntu | `.deb` / `.tar.gz` | 24.04.5, GNOME Wayland/XWayland | APT kurulum, kaldırma/yeniden kurma ve yerel fixture yükseltmesi; veriler korundu |
| Debian | `.deb` / `.tar.gz` | 12.15, XFCE Xorg | APT kurulum, kaldırma/yeniden kurma ve yerel fixture yükseltmesi; veriler korundu |
| Arch Linux | `PKGBUILD` / `.tar.gz` | 2026.10.01 ISO'sundan kurulmuş rolling sistem | pacman yaşam döngüsü kontrolleri geçti |
| Pardus | `.deb` / `.tar.gz` | 25.2, XFCE Xorg | APT kurulum, kaldırma/yeniden kurma ve yerel fixture yükseltmesi; veriler korundu |

Uyumluluk hedefleri Ubuntu 24.04+, Debian 12+, Pardus 23+ ve güncel Arch
Linux'tur. Yalnızca tablodaki sürümler test edildi; **Pardus 23 test edilmedi**.
Yerel yükseltme testlerinde ayrı ayrı derlenmiş iki QA sürümü kullanıldı;
iki yayımlanmış Linux sürümü arasındaki veri geçişini kanıtlamaz.

Paketlerin içindeki uygulama aynıdır; .NET 10 çalışma ortamı birlikte gelir.
Ayrıca .NET kurmak gerekmez. Paketleyici, GLIBC 2.36'dan yeni sembol gerektiren
ELF dosyalarını reddeder. Bu kontrol, her dağıtımın kütüphaneleriyle yapılacak
gerçek testin yerini tutmaz.

Arayüz X11 kullanır; Wayland oturumunda XWayland gerekir. Linux sesi, SDL2 ve
bilgisayardaki ses altyapısı üzerinden çalışır. Python ve ses paketleri isteğe
bağlıdır. Dönüştürmek ve Morse araçlarını kullanmak için AI modeli indirilmez.
Dosya/klasör seçiciler için oturum D-Bus'ı, `xdg-desktop-portal` ve masaüstüne
uygun GTK, GNOME veya KDE backend'i gerekir. Arch'ta uygun backend'i ayrıca kur.
Portal eksikliği dosya seçiminin iptal edilmiş gibi görünmesine yol açabilir.

Linux'ta varsayılan çizim yazılımla yapılır; sanal makine/Mesa OpenGL yolunda
görülen görüntü alma çökmesini önler.
Bu yolda sürekli arka plan kayması kapalıdır; statik desenler ve kısa sayfa
geçişleri korunur. Böylece boşta sürekli yeniden çizim yapılmaz.
Geliştirici karşılaştırması için `MORSECORE_LINUX_RENDERER=auto morsecore` ile otomatik grafik sürücüsü seçimi
istenebilir; bu yol Ubuntu VM kabul testinin geçtiği çizim yolu değildir.

LTTng yalnızca .NET'in ilgili tanılama/izleme özelliği için gerekir; isteğe
bağlıdır. Paketli `libcoreclrtraceptprovider.so` korunur. LTTng kütüphaneleri
eksikse bağımlılık kontrolü normal kullanımı engellemek yerine tanılama notu
gösterir. Zorunlu çalışma ortamı, grafik, SDL ve uygulama kontrolleri sürer.
Kaynak: [.NET runtime açıklaması](https://github.com/dotnet/runtime/discussions/116386).

## Kurulum

`.deb`, taşınabilir arşiv veya `PKGBUILD` dosyasını [Releases sayfasından](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v1.0.1) indir.
Aşağıdaki `VERSION` yerine indirdiğin dosyadaki sürümü yaz.

Ubuntu, Debian ve Pardus:

```bash
sudo apt install ./MorseCore-VERSION-Linux-x64.deb
morsecore
```

APT, gerekli sistem kütüphanelerini kurar. Çevrimdışı sesler için Python 3 ve
venv gerekir; bu dağıtımlarda `python3` ve `python3-venv` paketlerini yalnızca
sesleri kullanacaksan kur. Modeller ve ayrı lisansları için
[çevrimdışı seslendirme rehberine](offline-speech.tr.md) bak.

Sisteme kurmadan taşınabilir paketi kullanmak için:

```bash
tar -xzf MorseCore-VERSION-Linux-x64.tar.gz
cd MorseCore-VERSION-Linux-x64
./morsecore --check-dependencies
./morsecore
```

Taşınabilir paket eksik kütüphane kurmaz ve uygulama menüsüne kısayol eklemez.
Çıkardığın klasörün bütünlüğünü koru; yalnızca çalıştırılabilir dosyayı taşıma.
APT kullanan dağıtımlarda eksik bağımlılıklar varsa `.deb` daha kolay yoldur.

Arch Linux'ta üretilen `PKGBUILD` ile aynı sürüm `.tar.gz` dosyasını aynı klasöre
koy, tarifi incele ve normal kullanıcıyla çalıştır:

```bash
makepkg -si
```

Tarif arşivin SHA-256 değerini kontrol eder ve sistem bağımlılıklarını bildirir.
Bu yerel paket tarifidir; AUR'a yüklenmiş paket değildir.

### Sistem Python'u yeniyse isteğe bağlı sesler

Ses motorları venv ve ensurepip içeren **64 bit Python 3.11–3.13** ister.
Test edilen Arch sisteminde Python 3.14.8 vardı; sesler sistem Python'u
değiştirilmeden ayrı Python 3.12.15 ile denendi. Ubuntu, Debian ve Pardus'ta
dağıtımın desteklenen Python sürümleri kullanıldı.

Seçeneklerden biri [uv](https://docs.astral.sh/uv/getting-started/installation/).
uv kurulduktan sonra normal kullanıcıyla çalıştır:

```bash
uv python install 3.12
export PATH="$HOME/.local/bin:$PATH"
morsecore
```

uv'nin varsayılan dizinlerinde bu işlem `python3.12` dosyasını o terminalden
açılan uygulamaya görünür yapar. Taşınabilir pakette `./morsecore` kullan.
`--default`, `--force`, `sudo` kullanma; `/usr/bin/python` dosyasını değiştirme.
Kaynak: [uv Python rehberi](https://docs.astral.sh/uv/concepts/python-versions/#installing-python-executables).

MorseCore mevcut ses ortamını, `/usr/bin`, `/usr/local/bin` ve devraldığı PATH'i
arar; uv'nin özel kurulum dizinlerini doğrudan aramaz. `uv python update-shell`,
kullanıcı çalıştırılabilir dizinini shell ayarına ekleyebilir; ardından yeni
terminal aç. Masaüstü menüsü, oturumu yeniden açsan bile bu shell PATH'ini
devralmayabilir. Sürümlü Python dosyası masaüstü oturumunun PATH'ine de girene
kadar sesleri kurmak için uygulamayı yukarıdaki terminalden aç.
Ses bağımlılıkları MorseCore'un kendi ortamına kurulur.

## Güncelleme ve kaldırma

`.deb` için yeni paketi yine `apt install` ile kur. Arch için yeni tarif ve
arşivle `makepkg -si` kullan. Uygulama GitHub sürümünü kontrol edip taşınabilir
arşivin bütünlüğünü doğrulayabilir; yönetici yetkisiyle paket yöneticisi
çalıştırmaz. Kurmadan önce sürümü kontrol et.

Taşınabilir güncellemede uygulamayı kapatıp yeni arşivi yeni klasöre çıkar.
Ayarlar, indirilen sesler ve telsiz kayıtları uygulama klasörünün dışında,
kullanıcının XDG dizinlerinde tutulur. Uygulamayı `sudo` ile çalıştırma.

```bash
# APT paketi:
sudo apt remove morsecore
# Arch paketi:
sudo pacman -R morsecore-bin
```

Paket kaldırıldığında kullanıcı ayarları, indirilmiş sesler ve QSO kayıtları
korunur. Taşınabilir kopyada yalnızca çıkardığın uygulama klasörünü silebilirsin.
Uygulama verilerini bilerek silmeden önce kayıtlarını yedekle.

## Paket üretme ve kontrol

Linux x86-64, CMake'in özellik kontrolünden geçen final C23 derleyicisi,
CMake 3.28+, Ninja, .NET SDK 10, Python 3 ve binutils gerekir. `.deb` üretmek
için ayrıca `dpkg-deb` gerekir. Derleyicinin adı/sürümü tek başına yeterli
değildir; CMake özellik kontrolü belirleyicidir.

```bash
bash scripts/package-linux.sh --deb
bash scripts/test-linux-package.sh \
  build/release-linux/MorseCore-VERSION-Linux-x64.tar.gz \
  build/release-linux/MorseCore-VERSION-Linux-x64.deb
```

X11 masaüstünde isteğe bağlı, izole açılış testi için
`MORSECORE_LINUX_GUI_SMOKE=1` kullan. Bu test sürecin açık kaldığına bakar;
görsel doğruluk veya ses donanımı testi değildir. Paket geçici klasöre çıkarılır;
sisteme paket kurup kaldırılmaz, normal kullanıcı kayıtları kullanılmaz.

Önceden yayıma hazırlanmış derlemeyi paketlemek için `--skip-build` ve
`MORSECORE_LINUX_PUBLISH_DIR` kullan. `MORSECORE_PACKAGE_OUTPUT` yeni çıktı
klasörünü seçer. Mevcut paket, tarif ve sağlamalar üzerine yazılmaz.
`MORSECORE_LINUX_BUILD_DIR`, `NUGET_PACKAGES` ve `CC` ile derlemeyi izole
edebilirsin. Yeterince eski sistem tabanında derle; yeni glibc ile üretilen
paket başka dağıtımdan kütüphane kopyalayarak taşınabilir hâle gelmez.

Çıktılar: taşınabilir arşiv, `PKGBUILD`, Linux'a özel SHA-256 listesi ve
istenirse `.deb`. Paket içindeki `BUILD-INFO.json`, kaynak commit'ini, kaynakta
commit dışı değişiklik bulunup bulunmadığını, sürümü ve en yeni GLIBC sembolünü
kaydeder. Yayıma giren bağımlılıkların lisansları NuGet önbelleğinden toplanır.

## Sanal makinede kabul testleri

- Temiz APT kurulumu; başlatma, menü ve simge; kayıtları silmeden kaldırma.
- Eski paketten güncelleme, hatalı SHA reddi ve ayarların korunması.
- Tüm sayfalar, açık/koyu tema; Türkçe, İngilizce, Almanca, Rusça ve Arapça;
  Arapça menüler sağdan sola, Morse alanları soldan sağa.
- Dosya seçici, UTF-8 dönüştürme, dışa aktarma ve sentetik QSO içe/dışa aktarma.
- Ses çalma/durdurma, WAV üretme, mikrofon izni/kaydı ve çevrimdışı sesler.
- X11 ve Wayland/XWayland, ölçekleme ve eksik bağımlılık hata mesajları.
- Paket veya dağıtım değişince kontrolleri tekrarla. Pardus 25.2 sonucu
  Pardus 23'ün doğrulandığı anlamına gelmez.

Gereksinim kaynakları: [Uno Skia Desktop](https://platform.uno/docs/articles/features/using-skia-desktop.html)
ve [.NET Linux bağımlılıkları](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install).
Paket listelerinin tek kaynağı
[`packaging/linux/dependencies.json`](../packaging/linux/dependencies.json) dosyasıdır.
