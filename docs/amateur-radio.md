# Amateur radio / Amatör telsiz

## Türkçe

**Kaynak kontrolü: 15 Ekim 2026.** Bu sürüm çağrı işareti tanıma, CW mesajı hazırlama, Morse sesi/WAV ve yerel QSO günlüğü sağlar. Telsize bağlanmaz ve RF yayını yapmaz.

1. **Amatör telsiz → İstasyonlarım:** Çağrını gir, tanımayı çalıştır ve belge sınıfını belgenle karşılaştır. Birden fazla istasyon kaydedebilirsin. Ön ek tanıma, tahsis/sahiplik/geçerlilik sorgusu değildir.
2. **QSO hazırlığı:** İstasyon/frekans satırını açarak çalışma ülkesini, yerel yetki sınıfını ve sabit/taşınabilir/araç kullanımını seç. Yurt dışında yetki veya CEPT denkliği varsayılmaz; onayladığın tam çalışma çağrısı gerekir. Türkiye bölge eki önerisi bildirim koşullarını ortadan kaldırmaz.
3. Önce dinle ve frekansın kullanımda olup olmadığını sor. CQ, yanıt, gerçek RST, mesaj ve kapanış adımları için metin oluştur. `<AR>`, `<SK>`, `<BT>`, `<AS>`, `<KN>` ve `<HH>` bitişik prosedür işaretleridir. QSO şablonları uluslararası Latin CW içindir; ad/konum alanlarını Latin harflerle yaz.
4. Kayıtlar varsayılan olarak eğitimdir. Gerçek telsiz görüşmesini ayrıca seç. UTC kayıtları ve ayarlar `%LOCALAPPDATA%\MorseCore\Desktop\Radio` altında tutulur; özel uygulama veri dizini kullanılıyorsa onun `Radio` alt dizini kullanılır.
5. **QSO günlüğü:** Eğitim ve gerçek görüşmeler ayrı listelenir/dışa aktarılır. ADIF 3.1.4 `.adi` temel ASCII bağlantı alanlarını içerir. UTF-8 CSV notları korur; tablo yazılımlarında formül gibi çalışabilecek metinler güvenli biçimde kaçışlanır. Mevcut dosyanın değiştirilmesine yalnız Kaydet penceresinden onay verebilirsin. Bozuk istasyon dosyasının üzerine sessizce yazılmaz.

**Eğitim** altında Morse alfabesi, altı alıştırma türü, yanlış tekrarları, Q kodları/kısaltmalar/prosedür işaretleri ve fonetik alfabe bulunur. Dönüştürücü yalnız dönüştürür; Morse çalma ve çevrimdışı seslendirme **Ses** sayfasındadır.

## English

**Sources checked: 15 October 2026.** This version provides call recognition, CW preparation, Morse audio/WAV and a local QSO log. It neither connects to radio hardware nor transmits RF.

Save a station, identify its call and confirm the class against the actual licence. Open the station/frequency summary in **QSO preparation** to choose the operating country, local class and setup. Foreign privileges are never assumed; enter a confirmed full operating call. Turkish area suffix suggestions do not replace notification requirements.

Listen, check whether the frequency is occupied, then prepare CQ, reply, observed RST, message and closing templates. Names and locations in international CW templates use Latin letters. Prosigns are sent as joined signals. Training and actual radio contacts have separate UTC logs and exports. ADI exports basic ASCII contact fields; UTF-8 CSV preserves notes and neutralises spreadsheet formulas. Only a save-dialog selection authorises replacing an existing export. Unreadable station files are preserved.

The **Learning** group contains the alphabet, six exercise types, mistake review, radio glossary and phonetics. Playback and offline text-to-speech are on **Audio**; the converter only converts.

## Country coverage / Ülke kapsamı

Interface language and operating country are independent. Seven country profiles and a manual **Other** profile are included. The offline catalog is in [`rules.json`](../apps/MorseCore.AppCore/Radio/rules.json). It contains **selected CW reference ranges**, not a complete set of authorisations. Upper bounds are excluded. A match is a catalog match, never a legal clearance; signal bandwidth, individual licence, location, current restrictions and source footnotes still matter. IARU band plans are operating recommendations, separate from national licence rules.

| Profile | Source and scope |
| --- | --- |
| Türkiye | [BTK Article 21 / Table 25](https://www.btk.gov.tr/uploads/regulations/frekans-tahsisinden-muaf-telsiz-cihaz-sistemleri-olcutler.pdf), [KEGM Articles 6/10 and Annex 1](https://www.kiyiemniyeti.gov.tr/userfiles/editor/pdf/yonetmelik.pdf). CW transmitter output limits; home-built class B HF exceptions retain their condition. Selected ranges only; the special 30m conditions are excluded. |
| Germany | [AFuV Annex 1](https://www.gesetze-im-internet.de/afuv_2005/anlage_1.html), [identification §11](https://www.gesetze-im-internet.de/afuv_2005/__11.html), [BNetzA call plan, effective April 2025](https://www.bundesnetzagentur.de/Amateurfunk). A/E/N separated; ERP and PEP are distinct. Temporary exceptions are excluded. |
| Russia | [SRR's official regulatory index](https://srr.ru/dokumenty/dokumenty-minkomsvyazi-i-radiochastot/) points to GKRCh 10-07-01 / 18-45-02 and Order 184. The current primary power annex was inaccessible during research. **No automatic frequency/power matching is provided.** Categories and operating calls require manual confirmation. |
| United Kingdom | [Ofcom OFW611, Condition 6 and Schedule 1 A–C](https://www.ofcom.org.uk/siteassets/resources/documents/spectrum/emf/emf-amateur-licence-terms-and-conditions.pdf?v=386197). Ground stations; selected ranges. Airborne, 60m, coordination-specific ranges and special permissions are excluded. |
| United States | [Official 2025 CFR, Part 97](https://www.govinfo.gov/content/pkg/CFR-2025-title47-vol5/pdf/CFR-2025-title47-vol5-part97.pdf), §§97.119/301/305/313. Selected CW ranges in **ITU Region 2** only. Legacy classes retained; 60m and Region 1/3 exceptions excluded. This is the dated annual CFR reference, not a live eCFR mirror. |
| Saudi Arabia | [CST RR55, decision 559/1446, January 2025](https://www.cst.gov.sa/en/regulations-and-licenses/regulations/Document-1606), amateur radio annexes 19–20. The reversed 80m bounds printed in the English document are omitted. Class 2 follows the explicitly listed microwave ranges. This app's simple log does not replace all local record-keeping requirements. |
| UAE | [TDRA Amateur Radio Regulations v3](https://tdra.gov.ae/-/media/About/regulations-and-ruling/EN/Amateur-Radio-Regs-v3-0-pdf.ashx), articles 8–9 and annexes 1–2. A/B/Special separated; visitor authorisation remains manual. |

Call prefix references also use [ITU Radio Regulations 2024, Appendix 42](https://search.itu.int/history/HistoryDigitalCollectionDocLibrary/1.49.48.en.102.pdf). See the [IARU regional band plans](https://www.iaru-r1.org/on-the-air/band-plans/) for recommended operating segments, and the [ADIF 3.1.4 specification](https://adif.org/314/ADIF_314.htm) for export field definitions.

### Updating the catalog

Verify the primary regulator document and its effective date before changing a rule. Retain its source/section, exact Hz bounds, class, power **basis** and conditional restrictions. Do not turn an unverified or missing range into an affirmative permission. Update the review date and add boundary/unknown-class tests in `RadioChecks.cs`; run AppCore, WinUI and installer checks. No remote rule feed is enabled: changes travel with a reviewed application build.
