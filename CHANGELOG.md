# Changelog / Değişiklik günlüğü

## 0.7.5 — 2026-10-16

- Added the Apple Silicon macOS preview with a drag-and-drop DMG, native SwiftUI interface and the shared C23/AppCore engine. Windows packages and the existing version tag remain unchanged.
- Apple Silicon için SwiftUI arayüzlü, ortak C23/AppCore motorunu kullanan macOS önizlemesi ve sürükle-bırak DMG kurulumu eklendi. Mevcut Windows paketleri ve sürüm etiketi değişmedi.
- Shared update verification, generated language/brand/ADIF resources and AppCore map coordinates prevent platform drift. Fixed Mac service deadlines/cancellation, audio lifecycle and QSO state races.
- Ortak güncelleme doğrulaması, üretilen dil/marka/ADIF kaynakları ve AppCore harita koordinatlarıyla platform farkları azaltıldı. Mac servis zaman aşımı/iptali, ses yaşam döngüsü ve QSO eşzamanlılık hataları düzeltildi.
- Separate Morse audio, speech, decoding and voice packages into visible tabs; simplify WAV/microphone input, advanced settings and operation controls.
- Morse sesi, metin seslendirme, ses çözme ve paketler görünür sekmelere ayrıldı; WAV/mikrofon girişi, gelişmiş ayarlar ve işlem düğmeleri sadeleştirildi.
- Show contextual result actions and feedback, return first-time station setup to contact preparation, and group settings clearly in all five languages.
- Sonuç eylemleri ve geri bildirim ilgili panelde gösterilir; ilk istasyon kaydı görüşme hazırlığına döner, ayarlar beş dilde açık başlıklarla gruplanır.

## 0.7.4 — 2026-10-16

- Replace the square taskbar icon with a rounded green tile; give Windows and installed shortcuts a shared application identity. Reopening the app restores its existing window.
- Kare görev çubuğu simgesi yuvarlatılmış yeşil simgeyle değiştirildi; Windows ve kurulum kısayollarına ortak uygulama kimliği verildi. Uygulamayı yeniden açmak mevcut pencereyi getirir.
- Simplify the GitHub description and topics for users. Keep three-part versioning so existing installations can discover this update.
- GitHub açıklaması ve etiketleri kullanıcılar için sadeleştirildi. Mevcut kurulumların güncellemeyi bulabilmesi için üç parçalı sürüm biçimi korundu.

## 0.7.3 — 2026-10-16

- Add the missing top and sidebar gutters around the outer workspace; keep navigation aligned with the page heading in both themes and Arabic layout.
- Dış çalışma panelinin eksik üst ve yan menü boşlukları eklendi; iki temada ve Arapça düzende menü ile sayfa başlığı hizası korundu.

## 0.7.2 — 2026-10-16

- Show a full-window loading screen without a title bar or reserved header space. Separate dark-mode workspace, cards, editors and result surfaces with stronger green tones and borders; preserve light-mode colors and spacing.
- Açılış ekranı üst çubuk veya ayrılmış başlık boşluğu olmadan tüm pencereyi kullanır. Karanlık temada zemin, kart, giriş ve sonuç alanları daha belirgin yeşil tonları ve kenarlıklarla ayrılır; açık tema renkleri ve aralıkları korunur.

## 0.7.0 — 2026-10-16

- Wider desktop window, responsive forms, clearer cream/green surfaces and short page transitions. Added RAW-style animated corner artwork that stops when inactive or reduced motion is enabled; loading now hides navigation until ready.
- Daha geniş pencere, genişliğe uyarlanan formlar, belirgin krem/yeşil yüzeyler ve kısa sayfa geçişleri. Etkin olmayan pencerede veya hareket azaltıldığında duran RAW tarzı köşe deseni eklendi; gezinme yükleme bitene kadar gizli.
- Simplified amateur-radio navigation into My stations, Prepare a contact and Contact log; kept the map directly accessible. Replaced the dense country/band text with a sourced infographic preserving exact frequency, power and special-condition information.
- Amatör telsiz, İstasyonlarım / Görüşme hazırla / Görüşme günlüğü alt menülerine ayrıldı; harita doğrudan erişilebilir. Yoğun ülke/bant metni, kesin frekans/güç değerlerini ve özel koşulları koruyan kaynaklı infografiğe dönüştürüldü.
- Added German, Russian and Arabic READMEs alongside English and Turkish, with current screenshots matching each document's language.
- İngilizce ve Türkçenin yanına Almanca, Rusça ve Arapça README eklendi; güncel ekran görüntüleri belge diliyle eşleştirildi.

## 0.6.0 — 2026-10-15

- Added a keyboard/touch Morse key with measured timing, speed, rhythm feedback and five-language sending practice.
- Klavye/dokunma mors anahtarı, süre ve hız ölçümü, ritim geri bildirimi ve beş dilde gönderme eğitimi eklendi.
- Added offline single-tone WAV decoding and Windows audio-input recording, with uncertain-character markers and cancellation.
- Belirsiz karakter işaretleri ve iptal desteğiyle çevrimdışı tek tonlu WAV çözme ve Windows ses girişinden kayıt eklendi.
- Added searchable, editable QSO logs, reviewed ADIF imports, exact duplicate checks and a small offline contact map. Existing records remain compatible.
- QSO günlüğüne arama, düzenleme, önizlemeli ADIF içe aktarma, kesin yinelenen kontrolü ve küçük çevrimdışı harita eklendi; eski kayıt uyumluluğu korundu.
- Added in-app voice-pack installation, selection, samples and removal. Reworked both READMEs with actual screenshots, an animated banner and clearer platform/language information.
- Uygulama içinden ses paketi kurma, seçme, örnek dinleme ve kaldırma eklendi. İki README gerçek görüntüler, hareketli başlık ve açık platform/dil bilgisiyle sadeleştirildi.
- The welcome screen now stays visible for at least two seconds, including with reduced motion.
- Karşılama ekranı, hareket azaltılmışken de en az iki saniye görünür kalır.

## 0.5.0 — 2026-10-15

- Added station profiles, call recognition, sourced country guides, CW message preparation and separate UTC practice/contact logs with CSV/ADIF export. No RF or hardware control. Russian power matching remains disabled because the current primary annex could not be accessed.
- İstasyon profilleri, çağrı tanıma, kaynaklı ülke rehberleri, CW mesaj hazırlığı ve CSV/ADIF aktarımlı ayrı UTC eğitim/görüşme günlükleri eklendi. RF/cihaz kontrolü yoktur. Güncel birincil eke erişilemediği için Rusya güç eşleştirmesi kapalıdır.
- Grouped alphabet, exercises and the radio glossary under Learning; added call/Q-code/prosign exercises and mistake review. Moved playback and TTS to Audio and simplified navigation in all five languages.
- Alfabe, alıştırmalar ve telsiz sözlüğü Eğitim altında toplandı; çağrı/Q kodu/prosedür işareti alıştırmaları ve yanlış tekrarı eklendi. Çalma/TTS Ses'e taşındı; beş dilde menü sadeleştirildi.
- Replaced the curved navigation selection border with a separate straight indicator, preserving rounded rows and Arabic mirroring.
- Kavisli menü seçim kenarı bağımsız düz çizgiyle değiştirildi; yuvarlak satırlar ve Arapça yön düzeni korundu.
- Added verified in-app Windows update downloads, cancellation and confirmation before handing off to Setup.
- Uygulama içinden doğrulanan Windows güncelleme indirmesi, iptal ve Setup öncesi onay eklendi.
- Setup detects compatible Python and offers a bundled, signed official Python installer only with consent; the voice launcher finds it without PATH changes.
- Setup uyumlu Python'ı algılar; eksikse paketindeki imzalı resmî kurucuyu kullanıcı seçimiyle çalıştırır. Ses başlatıcısı PATH değişikliği istemez.

## 0.4.0 — 2026-10-15

- Added a Windows x64 Setup.exe build with five wizard languages, per-user shortcuts/uninstall, persistent optional speech storage and install/reinstall/removal checks. The release workflow includes Setup alongside the portable ZIPs.
- Beş dilli kurulum sihirbazı, kullanıcıya özel kısayol/kaldırma, kalıcı isteğe bağlı ses paketi ve kurma/yeniden kurma/kaldırma kontrolleriyle Windows x64 Setup.exe derlemesi eklendi. Yayın akışı Setup'ı taşınabilir ZIP'lerle birlikte paketler.
- Removed the legacy console/CLI application, launcher, console-only helpers/tests and console ZIP. Retained the C23 engine and ABI v1, adapted shared regression tests, and removed IDE/generated user files from Git tracking.
- Eski konsol/CLI uygulaması, başlatıcısı, konsola özel yardımcılar/testler ve konsol ZIP'i kaldırıldı. C23 motoru ve ABI v1 korundu; ortak testler ayrıştırıldı, IDE ve üretilmiş kullanıcı dosyaları Git takibinden çıkarıldı.

## 0.3.0 — 2026-10-15

### English

- Added the Windows WinUI desktop preview: converter, file batches, Morse audio, practice, alphabet and Settings.
- Added Turkish, English, German, Russian and Arabic interface languages; Arabic navigation mirrors while Morse fields stay left to right.
- Added Russian/Arabic native alphabet profiles and Unicode character/word statistics.
- Added optional offline speech: EMA Lightning 1.1.2 and Antalia Mini 1.0.1 for Turkish; six Piper voices for the other four languages.
- Added the MorseCore logo, RAW olive/cream themes, centered custom title bar and fixed window dimensions. Native caption regions provide press–drag–release behavior.
- Added open-source acknowledgements, original Turkish-engine licenses/Antalia NOTICE, and runtime notices in desktop packages.
- Added Windows desktop ZIP packaging alongside the console/native package. Signed MSIX installation and macOS/Linux desktop releases remain pending.

### Türkçe

- Dönüştürücü, toplu dosya işlemleri, Morse sesi, alıştırma, alfabe ve Ayarlar içeren Windows WinUI önizlemesi eklendi.
- Türkçe, İngilizce, Almanca, Rusça ve Arapça arayüz eklendi; Arapçada menü sağa geçer, Morse alanları soldan sağa kalır.
- Native Rusça/Arapça alfabe profilleri ve Unicode karakter/kelime sayımları eklendi.
- İsteğe bağlı çevrimdışı seslendirme eklendi: Türkçede EMA Lightning 1.1.2 ve Antalia Mini 1.0.1; diğer dört dilde altı Piper sesi.
- MorseCore logosu, RAW zeytin/krem temaları, ortalanmış özel üst çubuk ve sabit pencere boyutu eklendi. Yerel başlık alanı basma–taşıma–bırakma davranışını sağlar.
- Açık kaynak teşekkürleri, Türkçe motorların özgün lisansları/Antalia NOTICE ve masaüstü paketine çalışma zamanı bildirimleri eklendi.
- Konsol/native paketin yanında Windows masaüstü ZIP'i eklendi. İmzalı MSIX kurulumu ve macOS/Linux masaüstü yayınları henüz hazır değil.
