import Foundation

enum AudioCopy {
    static func sample(_ culture: String) -> String {
        switch culture {
        case "tr-TR": "Merhaba, MorseCore ile seslendirme denemesi."
        case "de-DE": "Hallo, dies ist eine Sprachprobe mit MorseCore."
        case "ru-RU": "Здравствуйте, это пример голоса в MorseCore."
        case "ar-SA": "مرحباً، هذه عينة صوتية من مورس كور."
        default: "Hello, this is a voice sample from MorseCore."
        }
    }
    static func runtimeMissing(_ language: AppLanguage) -> String {
        switch language {
        case .tr: "Uyumlu Python bulunamadı. Ses kurulumu sırasında gerekirse uygulamaya özel Python 3.13 de indirilip doğrulanır. Sistem Python'u ve ayarları değiştirilmez."
        case .en: "Compatible Python was not found. Voice installation also downloads and verifies a private Python 3.13 runtime when needed. System Python and settings are unchanged."
        case .de: "Kein kompatibles Python gefunden. Bei Bedarf lädt und prüft die Stimmeninstallation eine eigene Python-3.13-Laufzeit für die App. System-Python und Systemeinstellungen bleiben unverändert."
        case .ru: "Совместимый Python не найден. При установке голоса при необходимости загружается и проверяется отдельная среда Python 3.13 для приложения. Системный Python и настройки не изменяются."
        case .ar: "لم يتم العثور على Python متوافق. عند الحاجة، يؤدي تثبيت الصوت أيضاً إلى تنزيل بيئة Python 3.13 خاصة بالتطبيق والتحقّق منها. لا يتغيّر Python النظام أو إعداداته."
        }
    }
    static func recording(_ language: AppLanguage) -> String {
        switch language {
        case .tr: "Kayıt en fazla 120 saniye sürer. Durdurunca çözümle."
        case .en: "Record up to 120 seconds. Stop to decode."
        case .de: "Bis zu 120 Sekunden aufnehmen. Zum Dekodieren stoppen."
        case .ru: "Запись до 120 секунд. Остановите для расшифровки."
        case .ar: "سجّل لمدة تصل إلى 120 ثانية. أوقف التسجيل لفك الترميز."
        }
    }
    static func microphoneDenied(_ language: AppLanguage) -> String {
        switch language {
        case .tr: "Mikrofon izni verilmedi. Sistem Ayarları → Gizlilik ve Güvenlik → Mikrofon bölümünü kontrol et."
        case .en: "Microphone access denied. Check System Settings → Privacy & Security → Microphone."
        case .de: "Mikrofonzugriff verweigert. Systemeinstellungen → Datenschutz & Sicherheit → Mikrofon prüfen."
        case .ru: "Доступ к микрофону запрещён. Проверьте Системные настройки → Конфиденциальность и безопасность → Микрофон."
        case .ar: "تم رفض الوصول إلى الميكروفون. تحقّق من إعدادات النظام ← الخصوصية والأمان ← الميكروفون."
        }
    }
}
