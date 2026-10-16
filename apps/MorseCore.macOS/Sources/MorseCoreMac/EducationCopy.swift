import SwiftUI
import MorseCoreBridge

enum EducationSection: String, CaseIterable, Identifiable {
    case practice, alphabet, glossary, keying
    var id: String { rawValue }
    var titleKey: String { self == .keying ? "keyingTitle" : rawValue }
}

struct EducationQuestion: Identifiable, Sendable {
    let answer: String
    let morse: String
    var id: String { answer }
}

struct EducationTerm: Identifiable {
    let code: String
    let key: String
    let category: String
    let example: String
    var id: String { code }
}

struct EducationHeading: View {
    let title: String
    let subtitle: String
    let theme: BrandTheme
    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(subtitle).foregroundStyle(theme.muted)
        }
    }
}

struct EducationAlphabetPicker: View {
    let settings: AppSettings
    @Binding var selection: Alphabet
    var body: some View {
        Picker(settings.text("conversionAlphabet"), selection: $selection) {
            Text(settings.text("latin")).tag(Alphabet.latin)
            Text(settings.text("russian")).tag(Alphabet.russian)
            Text(settings.text("arabic")).tag(Alphabet.arabic)
        }.pickerStyle(.menu).frame(maxWidth: 330, alignment: .leading)
    }
}
