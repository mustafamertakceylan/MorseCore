# Turkish speech credits / Türkçe seslendirme atıfları

Thank you to Canberk Aslan for **EMA Lightning** and PatientDesk AI for **Antalia-2 Mini**. / **EMA Lightning** için Canberk Aslan'a, **Antalia-2 Mini** için PatientDesk AI ekibine teşekkür ederiz.

| Project / Proje | Integrated version / Kullanılan sürüm | Source / Kaynak | Original files / Özgün dosyalar |
|---|---|---|---|
| EMA Lightning | 1.1.2 | [Code](https://github.com/canberk7/ema-lightning), [model](https://huggingface.co/canberkkkkkk/ema-lightning) | [LICENSE](ema-lightning/LICENSE) |
| Antalia-2 Mini | 1.0.1 | [Code and model](https://huggingface.co/cloud0day3/antalia-mini) | [LICENSE](antalia-mini/LICENSE), [NOTICE](antalia-mini/NOTICE) |

These files are copied without modification from the corresponding installed wheel distributions. EMA's wheel declares Canberk Aslan as author and includes LICENSE; Antalia's wheel declares PatientDesk AI and includes LICENSE and NOTICE. Model revisions and integrity digests are pinned in [speech-models.json](../../scripts/speech-models.json).

Bu dosyalar ilgili wheel dağıtımlarından değiştirilmeden alınmıştır. EMA'nın paketi Canberk Aslan'ı yazar olarak belirtir ve LICENSE içerir; Antalia'nın paketi PatientDesk AI'ı belirtir, LICENSE ve NOTICE içerir. Model revizyonları ve bütünlük özetleri [speech-models.json](../../scripts/speech-models.json) içinde sabittir.

MorseCore does not vendor their Python code or weights. The optional installer downloads the original packages, retains their own notices and also copies this folder into the local pack. The adapter redirects EMA's model-file resolver to installed files to enforce offline synthesis; upstream package files remain unchanged. Other speech dependencies and Piper voices have separate terms; see [offline speech](../../docs/offline-speech.md).

MorseCore bu projelerin Python kodunu veya ağırlıklarını kaynak deposuna almaz. İsteğe bağlı kurucu özgün paketleri indirir, kendi bildirimlerini korur ve bu klasörü yerel pakete de kopyalar. Adaptör, çevrimdışı üretim için EMA'nın model çözümleyicisini kurulu dosyalara yönlendirir; upstream paket dosyaları değiştirilmez. Diğer ses bağımlılıkları ve Piper seslerinin koşulları ayrıdır; [ses rehberine](../../docs/offline-speech.tr.md) bakın.
