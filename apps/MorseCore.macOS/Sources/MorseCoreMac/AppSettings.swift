import SwiftUI
import Observation

enum AppLanguage: String, CaseIterable, Identifiable, Sendable {
    case tr, en, de, ru, ar
    var id: String { rawValue }
    var name: String {
        switch self {
        case .tr: "Türkçe"
        case .en: "English"
        case .de: "Deutsch"
        case .ru: "Русский"
        case .ar: "العربية"
        }
    }
    var isRTL: Bool { self == .ar }
}

enum AppTheme: String, CaseIterable, Identifiable, Sendable {
    case system, light, dark
    var id: String { rawValue }
    var colorScheme: ColorScheme? {
        switch self {
        case .system: nil
        case .light: .light
        case .dark: .dark
        }
    }
}

@MainActor @Observable
final class AppSettings {
    private let defaults: UserDefaults
    var language: AppLanguage { didSet { defaults.set(language.rawValue, forKey: "language") } }
    var theme: AppTheme { didSet { defaults.set(theme.rawValue, forKey: "theme") } }
    var separator: UInt32 { didSet { defaults.set(Int(separator), forKey: "separator") } }
    var collapsed: Bool { didSet { defaults.set(collapsed, forKey: "collapsed") } }
    var reduceMotion: Bool { didSet { defaults.set(reduceMotion, forKey: "reduceMotion") } }
    var outputDirectory: String { didSet { defaults.set(outputDirectory, forKey: "outputDirectory") } }
    var speechHome: String { didSet { defaults.set(speechHome, forKey: "speechHome") } }
    var alphabetRaw: Int { didSet { defaults.set(alphabetRaw, forKey: "alphabetRaw") } }
    var wpm: Int { didSet { defaults.set(wpm, forKey: "wpm") } }
    var frequency: Int { didSet { defaults.set(frequency, forKey: "frequency") } }
    var voiceId: String { didSet { defaults.set(voiceId, forKey: "voiceId") } }
    var speechLanguage: String { didSet { defaults.set(speechLanguage, forKey: "speechLanguage") } }
    var checkUpdates: Bool { didSet { defaults.set(checkUpdates, forKey: "checkUpdates") } }
    var lastUpdateCheck: Date { didSet { defaults.set(lastUpdateCheck, forKey: "lastUpdateCheck") } }
    var audioText = ""
    var audioIsMorse = false
    var audioSectionRaw = "morse"
    var availableUpdateVersion = ""

    init(suiteName: String? = nil) {
        // A test can opt into its own suite without touching real preferences.
        let suite = suiteName ?? ProcessInfo.processInfo.environment["MORSECORE_SETTINGS_SUITE"]
        let store = suite.flatMap(UserDefaults.init(suiteName:)) ?? .standard
        defaults = store
        let systemLanguage = Locale.current.language.languageCode?.identifier ?? "en"
        language = AppLanguage(rawValue: store.string(forKey: "language") ?? systemLanguage) ?? .en
        theme = AppTheme(rawValue: store.string(forKey: "theme") ?? "system") ?? .system
        let savedSeparator = store.integer(forKey: "separator")
        separator = [47,124,35,42].contains(savedSeparator) ? UInt32(savedSeparator) : 47
        collapsed = store.bool(forKey: "collapsed")
        reduceMotion = store.bool(forKey: "reduceMotion")
        outputDirectory = store.string(forKey: "outputDirectory") ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Documents/MorseCore").path
        speechHome = store.string(forKey: "speechHome") ?? MacPaths.dataRoot.appendingPathComponent("Speech").path
        alphabetRaw = (0...2).contains(store.integer(forKey: "alphabetRaw")) ? store.integer(forKey: "alphabetRaw") : 0
        wpm = store.object(forKey: "wpm") == nil ? 20 : min(60,max(5,store.integer(forKey: "wpm")))
        frequency = store.object(forKey: "frequency") == nil ? 600 : min(2000,max(200,store.integer(forKey: "frequency")))
        voiceId = store.string(forKey: "voiceId") ?? "ema-tr"
        speechLanguage = store.string(forKey: "speechLanguage") ?? "tr-TR"
        checkUpdates = store.object(forKey: "checkUpdates") == nil ? true : store.bool(forKey: "checkUpdates")
        lastUpdateCheck = store.object(forKey: "lastUpdateCheck") as? Date ?? .distantPast
    }
}
