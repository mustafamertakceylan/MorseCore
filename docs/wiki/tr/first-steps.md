# İlk kullanım

![Metin ve Morse dönüşümü]({{MEDIA_URL}}/converter.tr.png)

## İlk mesajını dönüştür

1. **Dönüştürücü** sayfasını aç ve **Metin → Morse** yönünü seç.
2. **Latin / Türkçe**, **Rusça** veya **Arapça** dönüşüm alfabesini seç. Bu seçim, arayüz dilinden bağımsızdır.
3. Örneğin `MERHABA DUNYA` yaz ve **Dönüştür** düğmesine bas.
4. Sonucu kopyala, metin dosyasına kaydet veya dinlemek için **Seste aç** seçeneğini kullan. Morse çalma ve metin seslendirme kontrolleri [Ses](audio.md) sayfasındadır.

Tersine dönüşüm için **Morse → Metin** yönünü seç, mesajın üretildiği alfabeyi belirle ve kodu yapıştır. Morse kodu kendi alfabesini belirtmez. **Ayarlar** bölümünden kelime ayırıcısını (`/`, `|`, `#` veya `*`) seçebilirsin; çözümlerken de aynı ayırıcıyı kullan.

## Dönüşümde neler değişir?

Türkçe harfler Latin karşılıklarına, Rusça `Ё` harfi `Е` harfine dönüşür. Arapçada harekeler ve tatvil atlanır; bazı harf biçimleri sadeleşir. Çözümleme özgün yazımı geri getirmez. Desteklenmeyen karakterlerde hata gösterilir; uygun alfabeyi seç veya desteklenmeyen işaretleri çıkar.

Karakter sayısı, baştaki bayt sırası işareti hariç Unicode kod noktalarını sayar. Kelimeler düz metinde sayılır: kodlarken girdide, çözümlerken sonuçta. Boşluk ve desteklenen boşluk karakterleri kelimeleri ayırır. Bu değerler dosya boyutu veya ekranda birleşerek görünen harf sayısı değildir.

## Dosya dönüştür

**Dosyalar** sayfasında yönü, alfabeyi ve kayıt klasörünü seç. Bir veya birden fazla UTF-8 `.txt` dosyası ekleyip **Dönüştür** düğmesine bas. Dosya başına sınır **8 MiB**'tır. Sonuç özetindeki hataları kontrol et. Mevcut çıktı dosyaları korunur; yeniden denemeden önce başka klasör seç veya önceki çıktının adını değiştir.

Görünüm, dil ve varsayılan klasörler **Ayarlar** bölümündedir. Uygulamayı henüz kurmadıysan [Kurulum](installation.md) sayfasına geç.
