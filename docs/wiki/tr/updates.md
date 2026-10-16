# Güncelleme ve kurulum sorunları

**Ayarlar → Güncellemeler** bölümünden kurulu sürümü görebilir, yeni sürüm arayabilirsin. Açılışta isteğe bağlı kontrol de buradan açılır. Kontrol tek başına kurulum yapmaz. GitHub'a commit gönderilmesi güncelleme değildir; uygun paketi bulunan daha yeni bir sürümün yayımlanması gerekir.

## Windows

Güncelleme göründüğünde **İndir ve yükle** seçeneğini kullan. MorseCore, Setup dosyasını indirip SHA-256 özetini ve sürümünü doğrular. Ardından uygulamayı kapatıp Setup'ı açmadan önce onay ister. Sihirbazı tamamla. İstersen [resmî sürüm sayfasından]({{RELEASE_URL}}) Setup'ı indirip mevcut kurulumun üzerine çalıştırabilirsin.

## Mac

Sunulan paketi indir; ardından **Kurulumu aç** veya **Finder'da göster** seçeneğini kullan. İndirilen dosyanın SHA-256 özeti önce doğrulanır. DMG için MorseCore'u kendin kapat, yeni uygulamayı Applications klasörüne sürükleyip eski kopyayı değiştir. Açık uygulama sessizce değiştirilmez. Tercihler, QSO kayıtları ve sesler uygulama paketinin dışında tutulur ve korunur.

## Sorun çıkarsa

- **Güncelleme görünmüyor:** sürüm sayfasıyla karşılaştır. Aynı sürüm bir yeniden kurulumdur, güncelleme değildir. İşletim sistemin için paket henüz eklenmemiş olabilir.
- **İnternet yok veya indirme başarısız:** mevcut uygulamayı kullanmaya devam edebilirsin. Bağlantıyı ve boş alanı kontrol edip yeniden dene. Devam eden işlemi **İptal** ile durdurabilirsin.
- **Doğrulama başarısız:** o dosyayı açma. Uygulamadan yeniden dene veya resmî sürüm sayfasından tekrar indir.
- **İlk açılış engelleniyor:** [Kurulum](installation.md) sayfasındaki işletim sistemine uygun adımları izle.

Sorun sürüyorsa [hata bildirimi aç]({{REPO_URL}}/issues). Uygulama sürümünü, işletim sistemini, hatanın tam metnini ve tekrar etme adımlarını yaz. Ekran görüntüsü veya günlük paylaşmadan önce kişisel yolları, mesajları ve QSO bilgilerini kaldır.
