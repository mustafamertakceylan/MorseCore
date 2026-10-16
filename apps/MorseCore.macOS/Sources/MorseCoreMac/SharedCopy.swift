import Foundation

enum SharedCopy {
    static let languages: [String: [String: String]] = {
        var result: [String: [String: String]] = [:]
        for language in ["tr", "en", "de", "ru", "ar"] {
            if let url = Bundle.module.url(forResource: language, withExtension: "json", subdirectory: "Resources/Strings"),
               let data = try? Data(contentsOf: url),
               let values = try? JSONDecoder().decode([String: String].self, from: data) {
                result[language] = values
            }
        }
        return result
    }()
}

extension AppSettings {
    func text(_ key: String) -> String {
        if key == "retry" {
            return ["tr":"Yeniden dene", "en":"Retry", "de":"Erneut versuchen", "ru":"Повторить", "ar":"إعادة المحاولة"][language.rawValue] ?? "Retry"
        }
        return SharedCopy.languages[language.rawValue]?[key] ?? SharedCopy.languages["en"]?[key] ?? key
    }
}
