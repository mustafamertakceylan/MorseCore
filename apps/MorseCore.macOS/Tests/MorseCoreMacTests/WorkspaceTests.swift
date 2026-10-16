import Foundation
import Testing
@testable import MorseCoreMac

@Test func serviceJSONPreservesUnicodeAndTypes() throws {
    let source = ServiceJSON.object(["text": .string("سلام Привет İĞ"), "id": .number(14_060_000), "practice": .bool(true)])
    let decoded = try JSONDecoder().decode(ServiceJSON.self, from: JSONEncoder().encode(source))
    #expect(decoded == source)
    #expect(decoded["missing"] == .null)
    #expect(decoded["practice"].boolValue)
    #expect(decoded["id"].intValue == 14_060_000)
}

@Test func radioFrequencyAndSharedBandCatalog() {
    #expect(radioFrequencyHz("14.025123") == 14_025_123)
    #expect(radioFrequencyHz("14.0251234") == nil)
    #expect(radioFrequencyHz("1e2") == nil)
    #expect(radioBand("20M", frequencyHz: 14_025_000) == "20m")
    #expect(radioBand("40m", frequencyHz: 14_025_000) == nil)
    #expect(radioBand("", frequencyHz: 14_025_000) == "20m")
    #expect(radioBand("20m", frequencyHz: 0) == "20m")
    #expect(radioBand("unknown", frequencyHz: 14_025_000) == nil)
}

@MainActor @Test func translationsAndSettingsAreIsolated() {
    let suite = "com.morsecore.mac.tests.\(UUID().uuidString)"
    defer { UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite) }
    let settings = AppSettings(suiteName: suite)
    for language in AppLanguage.allCases {
        settings.language = language
        #expect(SharedCopy.languages[language.rawValue]?.count == 467)
        #expect(settings.text("radio") != "radio")
    }
    settings.wpm = 25; settings.separator = 124
    let restored = AppSettings(suiteName: suite)
    #expect(restored.wpm == 25 && restored.separator == 124)
}
