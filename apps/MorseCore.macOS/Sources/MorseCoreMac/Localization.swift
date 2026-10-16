import Foundation

enum CopyKey: String {
    case converter, settings, subtitle, encode, decode, alphabet, latin, russian, arabic
    case text, morse, inputHint, outputHint, convert, converting, clear, swap, copy, save
    case characters, words, outputCharacters, appearance, theme, system, light, dark
    case language, separator, separatorHint, ready, copied, saved, saving, tooLarge
    case empty, invalid, unsupported, invalidMorse, outOfMemory, failed, saveFailed
    case normalization, normalizationBody, version
}

struct Localizer {
    let language: AppLanguage
    func callAsFunction(_ key: CopyKey) -> String {
        let index: Int
        switch language { case .tr: index = 0; case .en: index = 1; case .de: index = 2; case .ru: index = 3; case .ar: index = 4 }
        return Self.strings[key]?[index] ?? key.rawValue
    }

    private static let strings: [CopyKey: [String]] = [
        .converter: ["Dönüştürücü", "Converter", "Konverter", "Конвертер", "المحوّل"],
        .settings: ["Ayarlar", "Settings", "Einstellungen", "Настройки", "الإعدادات"],
        .subtitle: ["Metin ile Morse kodu arasında dönüştür.", "Convert between text and Morse code.", "Text und Morsecode umwandeln.", "Преобразование текста и кода Морзе.", "التحويل بين النص وشفرة مورس."],
        .encode: ["Metin → Morse", "Text → Morse", "Text → Morse", "Текст → Морзе", "نص ← مورس"],
        .decode: ["Morse → Metin", "Morse → Text", "Morse → Text", "Морзе → Текст", "مورس ← نص"],
        .alphabet: ["Dönüşüm alfabesi", "Conversion alphabet", "Alphabet", "Алфавит", "أبجدية التحويل"],
        .latin: ["Latin / Türkçe", "Latin / Turkish", "Lateinisch / Türkisch", "Латиница / Турецкий", "اللاتينية / التركية"],
        .russian: ["Rusça", "Russian", "Russisch", "Русский", "الروسية"],
        .arabic: ["Arapça", "Arabic", "Arabisch", "Арабский", "العربية"],
        .text: ["Metin", "Text", "Text", "Текст", "النص"],
        .morse: ["Morse kodu", "Morse code", "Morsecode", "Код Морзе", "شفرة مورس"],
        .inputHint: ["Bir mesaj yaz…", "Write a message…", "Nachricht eingeben…", "Введите сообщение…", "اكتب رسالة…"],
        .outputHint: ["Dönüştürdüğünde burada görünecek.", "Your result will appear here.", "Das Ergebnis erscheint hier.", "Результат появится здесь.", "ستظهر النتيجة هنا."],
        .convert: ["Dönüştür", "Convert", "Umwandeln", "Преобразовать", "تحويل"],
        .converting: ["Dönüştürülüyor…", "Converting…", "Umwandlung…", "Преобразование…", "جارٍ التحويل…"],
        .clear: ["Temizle", "Clear", "Leeren", "Очистить", "مسح"],
        .swap: ["Tersine çevir", "Reverse", "Umkehren", "Обратно", "عكس الاتجاه"],
        .copy: ["Kopyala", "Copy", "Kopieren", "Копировать", "نسخ"],
        .save: ["Metni kaydet", "Save text", "Text speichern", "Сохранить текст", "حفظ النص"],
        .characters: ["Girdi karakteri", "Input characters", "Eingabezeichen", "Символов на входе", "أحرف الإدخال"],
        .words: ["Kelime", "Words", "Wörter", "Слов", "كلمات"],
        .outputCharacters: ["Çıktı karakteri", "Output characters", "Ausgabezeichen", "Символов на выходе", "أحرف الإخراج"],
        .appearance: ["Görünüm ve dil", "Appearance and language", "Darstellung und Sprache", "Внешний вид и язык", "المظهر واللغة"],
        .theme: ["Tema", "Theme", "Darstellung", "Тема", "السمة"],
        .system: ["Sistem", "System", "System", "Системная", "النظام"],
        .light: ["Açık", "Light", "Hell", "Светлая", "فاتحة"],
        .dark: ["Koyu", "Dark", "Dunkel", "Тёмная", "داكنة"],
        .language: ["Uygulama dili", "App language", "App-Sprache", "Язык приложения", "لغة التطبيق"],
        .separator: ["Kelime ayırıcı", "Word separator", "Worttrennzeichen", "Разделитель слов", "فاصل الكلمات"],
        .separatorHint: ["Morse kodunda kelimeler bu işaretle ayrılır.", "Words in Morse code are separated with this symbol.", "Dieses Zeichen trennt Wörter im Morsecode.", "Этот символ разделяет слова в коде Морзе.", "يفصل هذا الرمز بين الكلمات في شفرة مورس."],
        .ready: ["Hazır", "Ready", "Bereit", "Готово", "جاهز"],
        .copied: ["Panoya kopyalandı.", "Copied to clipboard.", "In die Zwischenablage kopiert.", "Скопировано в буфер обмена.", "تم النسخ إلى الحافظة."],
        .saved: ["Metin kaydedildi.", "Text saved.", "Text gespeichert.", "Текст сохранён.", "تم حفظ النص."],
        .saving: ["Kaydediliyor…", "Saving…", "Speichern…", "Сохранение…", "جارٍ الحفظ…"],
        .tooLarge: ["Girdi en fazla 200.000 karakter olabilir.", "Input must be 200,000 characters or fewer.", "Die Eingabe darf höchstens 200.000 Zeichen umfassen.", "Ввод не должен превышать 200 000 символов.", "يجب ألا يتجاوز الإدخال 200000 حرف."],
        .empty: ["Önce bir metin veya Morse kodu gir.", "Enter some text or Morse code first.", "Zuerst Text oder Morsecode eingeben.", "Сначала введите текст или код Морзе.", "أدخل نصاً أو شفرة مورس أولاً."],
        .invalid: ["Girdiyi kontrol et.", "Check your input.", "Bitte die Eingabe prüfen.", "Проверьте ввод.", "تحقق من الإدخال."],
        .unsupported: ["Seçilen alfabede desteklenmeyen bir karakter var.", "A character is not supported by this alphabet.", "Dieses Alphabet unterstützt eines der Zeichen nicht.", "Один из символов не поддерживается этим алфавитом.", "يوجد حرف لا تدعمه هذه الأبجدية."],
        .invalidMorse: ["Morse kodunu kontrol et: nokta, çizgi ve ayırıcı kullan.", "Check the Morse code: use dots, dashes and separators.", "Morsecode prüfen: Punkte, Striche und Trennzeichen verwenden.", "Проверьте код Морзе: точки, тире и разделители.", "تحقق من شفرة مورس: استخدم النقاط والشرطات والفواصل."],
        .outOfMemory: ["Yeterli bellek yok. Daha kısa bir metin dene.", "Not enough memory. Try a shorter message.", "Nicht genug Speicher. Kürzeren Text versuchen.", "Недостаточно памяти. Попробуйте более короткий текст.", "الذاكرة غير كافية. جرّب نصاً أقصر."],
        .failed: ["Dönüşüm tamamlanamadı.", "Conversion could not be completed.", "Umwandlung nicht möglich.", "Не удалось выполнить преобразование.", "تعذر إكمال التحويل."],
        .saveFailed: ["Dosya kaydedilemedi. Başka bir konum seç.", "Could not save the file. Choose another location.", "Datei konnte nicht gespeichert werden. Anderen Ort wählen.", "Не удалось сохранить файл. Выберите другую папку.", "تعذر حفظ الملف. اختر موقعاً آخر."],
        .normalization: ["Alfabe ve dönüşüm notları", "Alphabet and conversion notes", "Hinweise zur Umwandlung", "Об алфавитах и преобразовании", "ملاحظات الأبجدية والتحويل"],
        .normalizationBody: ["Türkçe harfler sadeleşir. Rusçada Ё → Е. Arapçada hareke ve tatvil atlanır; bazı harfler sadeleşir. Çözümleme özgün yazımı geri getirmez. Rusça ve Arapça için doğru alfabeyi seç.", "Turkish letters are normalized. Russian Ё becomes Е. Arabic vowel marks and tatweel are omitted; some letters are normalized. Decoding does not restore the original spelling. Select the correct alphabet for Russian and Arabic.", "Türkische Buchstaben werden vereinfacht. Russisches Ё wird Е. Arabische Vokalzeichen und Tatweel entfallen; einige Buchstaben werden vereinheitlicht. Die ursprüngliche Schreibweise wird nicht wiederhergestellt. Das passende Alphabet wählen.", "Турецкие буквы нормализуются; Ё становится Е. В арабском пропускаются огласовки и татвиль, некоторые буквы нормализуются. Исходное написание не восстанавливается. Выберите нужный алфавит.", "تُبسّط الحروف التركية، وتتحول Ё الروسية إلى Е. تُحذف الحركات والتطويل في العربية وتُبسّط بعض الحروف. لا يعيد فك الشفرة الكتابة الأصلية. اختر الأبجدية المناسبة."],
        .version: ["Çekirdek sürümü", "Core version", "Kernversion", "Версия ядра", "إصدار النواة"]
    ]
}
