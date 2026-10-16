# MorseCore for Mac / Mac kurulumu

## English

Download **MorseCore-0.7.5-macOS-arm64.dmg** from the [0.7.5 release](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.7.5).
Requires an Apple Silicon Mac (M1 or newer), macOS 14 or newer. Intel is not
included in this first package. Older macOS versions have not been device-tested.

1. Open the DMG and drag **MorseCore** onto **Applications**.
2. Eject the disk image and open MorseCore from Applications.
3. This preview is ad hoc signed, **not Developer ID signed or notarized**.
   If macOS blocks it, follow [Apple's per-app approval instructions](https://support.apple.com/en-ie/102445)
   in **System Settings → Privacy & Security → Open Anyway**. Managed Macs may
   prevent approval. Do not disable Gatekeeper or remove quarantine globally.

.NET is bundled. Optional offline voices install their own runtime when needed;
the first voice installation needs internet. Your messages and contacts stay local.

**Updates:** Settings checks GitHub for a newer release with a matching Mac package.
The app downloads it and checks SHA-256. Open the verified DMG, quit the old app
yourself, and replace it in Applications. This is a user-controlled upgrade;
MorseCore does not silently replace an open app. Contact data and preferences live
outside the app bundle and remain after replacement. Replacing an app with the
same version is a reinstall; the update checker offers only newer versions.

To uninstall, quit MorseCore and move the app to Trash. This preserves your
contacts, settings and voices. No privileged installer or background agent is used.

This Mac build was added to the existing Windows 0.7.5 release. The original tag
and Windows binaries are unchanged. Use the attached **macOS Source** archive for
the exact Mac sources; its commit is recorded in the release notes.

## Türkçe

[0.7.5 sürümünden](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.7.5)
**MorseCore-0.7.5-macOS-arm64.dmg** dosyasını indir. Apple Silicon (M1 ve sonrası)
ve macOS 14 veya sonrası gerekir. İlk pakette Intel yoktur; eski macOS sürümleri
gerçek cihazda henüz denenmedi.

1. DMG dosyasını aç, **MorseCore** uygulamasını **Applications** üzerine sürükle.
2. Disk imajını çıkar, uygulamayı Applications içinden aç.
3. Paket **Apple Developer ID imzalı ve noter onaylı değildir**. macOS engellerse
   **Sistem Ayarları → Gizlilik ve Güvenlik → Yine de Aç** yoluyla uygulamaya
   özel izin ver. [Apple'ın açıklaması](https://support.apple.com/en-ie/102445).
   Kurum tarafından yönetilen Mac'ler bunu engelleyebilir.

.NET paket içindedir. Çevrimdışı sesler için ilk kurulumda internet gerekir;
gerekirse uygulamaya özel bir Python kurulur.

**Güncelleme:** Ayarlar yeni sürümü bulur, paketi indirir ve SHA-256 ile doğrular.
Doğrulanmış DMG'yi aç; eski uygulamayı kendin kapatıp Applications içindeki kopyayı
değiştir. Günlük ve ayarlar korunur. Aynı sürüm yeniden kurulabilir, ancak
güncelleyici yalnız daha yeni sürümleri önerir. Kaldırmak için uygulamayı kapatıp
Çöp Sepeti'ne taşı; kişisel veriler ve sesler otomatik silinmez.

Kaynak kodu derlemek için [geliştirici yönergeleri](../apps/MorseCore.macOS/README.md).
