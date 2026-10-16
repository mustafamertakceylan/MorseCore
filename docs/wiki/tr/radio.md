# Amatör telsiz

![Amatör telsiz çalışma alanı]({{MEDIA_URL}}/wiki/radio.tr.png)

*macOS arayüzü gösterilmiştir; Windows yerleşimi farklı olabilir.*

MorseCore, CW mesajı hazırlamaya ve yerel görüşme günlüğü tutmaya yardımcı olur. **Telsize bağlanmaz ve RF yayını yapmaz.** Oluşturulan Morse sesini dinlemek, bilgisayarda ses çalmaktır.

## 1. İstasyonunu ekle

**Amatör telsiz → İstasyonlarım** sayfasını aç. Çağrı işaretini girip tanımayı çalıştır. Önerilen ülkeyi ve belge sınıfını kendi belgenle karşılaştır, ardından kaydet. Birden fazla istasyon ekleyebilirsin. Ön ek tanıma; çağrının sahibini, tahsisini veya geçerliliğini doğrulamaz.

## 2. Görüşme hazırla

**Görüşme hazırla** sayfasındaki istasyon/frekans bölümünü aç. İstasyonunu, çalışma ülkesini, yerel belge sınıfını ve kullanım biçimini seç. Kendi bölgen dışında çalışıyorsan tam çalışma çağrını ve gereken eki doğrula; yurt dışı çalışma yetkisi varsayılmaz.

Önce dinle. İlk mesaj adımı frekansın kullanımda olup olmadığını sorar (`QRL?`). Ardından genel çağrı, yanıt, gözlenen sinyal raporu, mesaj veya kapanış adımını seç. Morse sesini çalmadan ya da kaydetmeden önce üretilen metni kontrol et. Uluslararası CW şablonlarında ad ve konumu Latin harfleriyle yaz. `<AR>` gibi prosedür işaretleri bitişik gönderilir.

## 3. Kaydet ve yeniden bul

**Görüşmeyi günlüğe ekle** bölümünü aç. Kayıtlar varsayılan olarak **eğitim** kaydıdır; yalnız gerçek telsiz görüşmesinden sonra gerçek kayıt seçeneğini işaretle. Tarihler **UTC** kullanır. **Görüşme günlüğü**, eğitim ve gerçek kayıtları ayrı gösterir; arama ve düzenleme buradadır.

ADIF veya CSV için **Dosyadan al / dosyaya aktar** bölümünü kullan. ADIF içe aktarma, desteklenen CW kayıtlarını onaydan önce gösterir ve tam kopyaları atlar. ASCII `.adi`/`.adif` dosyalarında sınır 8 MiB ve 10.000 kayıttır. Desteklenmeyen ADIF alanları korunmadığından özgün dosyanı sakla. CSV, Unicode notları korur.

Çevrimdışı **Konum haritası**, girilen koordinatları veya Maidenhead locator bilgisini kullanır. Çağrı ön eki operatörün konumunu belirlemez.

## Ülke ve bant rehberi

Türkiye, Almanya, Rusya, Birleşik Krallık, ABD, Suudi Arabistan, BAE ve **Diğer** profilleri bulunur. Seçilmiş CW aralıklarının kaynak kontrol tarihi **15 Ekim 2026**'dır. Rusya için otomatik frekans/güç eşleştirmesi yoktur. Rehberdeki kaynakları ve koşulları oku; eşleşme, yayın izni veya eksiksiz bant planı değildir.

Telsiz sözlüğü için [Eğitim](learning.md), dinleme için [Ses](audio.md) sayfasına bak.
