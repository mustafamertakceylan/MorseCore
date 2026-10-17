# Geliştiriciler ve katkılar

MorseCore açık kaynaklıdır. Hata bildirimi, anlaşılır bir çeviri veya test sonucu da katkıdır; kod yazmak şart değildir.

## Nasıl katkıda bulunabilirsin?

1. [Issues]({{REPO_URL}}/issues) içinde benzer bir kayıt var mı bak.
2. Hata bildirirken işletim sistemi, MorseCore sürümü, izlediğin adımlar ve beklediğin sonucu yaz.
3. Örnek metni veya sentetik bir görüşme kaydını paylaş. Kişisel günlüğünü, erişim anahtarlarını veya özel ses kayıtlarını ekleme.
4. Kod değiştireceksen küçük ve tek amaçlı bir değişiklik yap; ilgili testleri çalıştırıp bir pull request aç.

## Ortak altyapı

Windows ve Mac, **aynı C23 motorunu ve AppCore iş kurallarını** kullanır. Arayüzler WinUI ve SwiftUI'dır. Dönüşüm, QSO, ses paketleri ve güncelleme doğrulaması gibi ortak davranışlar platformlara ayrı ayrı yazılmaz.

- [Ortak mimari](../../architecture.md)
- [Windows kaynak koddan derleme](../../desktop.tr.md)
- [Mac kaynak koddan derleme](../../../apps/MorseCore.macOS/README.md)
- [Native API](../../native-api.tr.md)

Linux geliştirme sürümü aynı XAML, AppCore ve C23 motorunu kullanır. [Linux rehberi](../../linux.tr.md) paketleri, otomatik kontrolleri ve henüz tamamlanmamış dağıtım testlerini ayırır; CI başarısı her dağıtımın test edildiği anlamına gelmez.

## Wiki'yi güncellemek

Wiki sayfalarının kaynağı depodaki `docs/wiki/` klasörüdür. Düzenlemeyi burada yap; aktarım betiği dil geçişlerini, menüyü ve bağlantıları oluşturur. [Wiki bakım yönergesi](../README.md).

## Lisans ve teşekkür

MorseCore [Apache 2.0]({{REPO_URL}}/blob/main/LICENSE) lisanslıdır. Ses motoru ve model lisansları ayrıdır: EMA Lightning, Antalia-2 Mini, Piper ve diğer katkılar için [NOTICE]({{REPO_URL}}/blob/main/NOTICE) dosyasına bak. Dağıtımda ilgili lisansları ve bildirimleri koru.
