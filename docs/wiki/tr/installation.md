# Kurulum

MorseCore **{{VERSION}}** sürümünü [resmî sürüm sayfasından]({{RELEASE_URL}}) indir. Bilgisayarına uygun paketi seç; ses modelleri ayrıca ve isteğe bağlı indirilir.

## Windows

Windows x64 desteklenir; Setup için Windows 10 sürüm 2004 veya sonrası gerekir.

1. [Windows Setup dosyasını]({{WINDOWS_SETUP_URL}}) indirip aç.
2. Sihirbazı takip et, istediğin kısayolları seç.
3. MorseCore'u Başlat menüsünden aç.

Kurulum kendi kullanıcı hesabına yapılır, yönetici yetkisi gerekmez. Gerekli .NET ve Windows App SDK dosyaları pakete dahildir. Uyumlu Python bulunamazsa Setup, seslendirme için isteğe bağlı kurulum sunar. Bunu atlayarak da metin dönüştürebilir ve Morse sesi dinleyebilirsin.

Kurulum dosyası imzasızdır; Windows yayıncı uyarısı gösterebilir. Dosyayı projenin resmî sürüm sayfasından indirdiğini kontrol et.

Kurulum istemiyorsan [taşınabilir ZIP'in]({{WINDOWS_ZIP_URL}}) tamamını bir klasöre çıkarıp **MorseCore.Desktop.exe** dosyasını aç. Paketin dosyalarını birlikte tut.

## Mac

[Mac DMG paketi]({{MAC_DMG_URL}}), **Apple Silicon (M1 ve sonrası)** içindir; macOS 14 veya sonrasını hedefler. Henüz Intel paketi yoktur.

1. DMG'yi aç, **MorseCore** uygulamasını **Applications** üzerine sürükle.
2. Disk imajını çıkar, uygulamayı Applications içinden aç.
3. Paket Apple noter onaylı değildir. Açılış engellenirse [Apple'ın uygulamaya özel izin yönergesini](https://support.apple.com/en-us/102445) izle: seçenek görünüyorsa **Sistem Ayarları → Gizlilik ve Güvenlik → Yine de Aç**. Kurum tarafından yönetilen Mac'ler bunu kısıtlayabilir.

## Kaldırma veya yeniden kurma

Windows'ta **Ayarlar → Uygulamalar** bölümünden kaldır. Mac'te uygulamayı kapatıp Çöp Sepeti'ne taşı. Tercihler, QSO kayıtları ve indirilmiş sesler korunur; bilgisayar değiştirmeden önce önemli kayıtlarını yedekle. Dışa aktardığın dosyalar kaydettiğin yerde kalır.

Linux geliştirme sürümü için [Linux rehberine](../../linux.tr.md) bak. Henüz kararlı Linux masaüstü paketi yayımlanmadı. Sonraki adım: [Ses](audio.md) veya [Güncelleme](updates.md).
