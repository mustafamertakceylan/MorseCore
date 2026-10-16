import SwiftUI
import MorseCoreBridge

struct EducationWorkspace: View {
    let settings: AppSettings
    let section: EducationSection
    var body: some View {
        Group {
            switch section {
            case .practice: EducationPractice(settings: settings)
            case .alphabet: EducationAlphabet(settings: settings)
            case .glossary: EducationGlossary(settings: settings)
            case .keying: EducationKeying(settings: settings)
            }
        }
    }
}

private struct EducationAlphabet: View {
    let settings: AppSettings
    @Environment(\.colorScheme) private var scheme
    @State private var alphabet: Alphabet = .latin
    @State private var query = ""
    @State private var symbols: [AlphabetSymbol] = []
    @State private var error = ""
    @State private var playback: Task<Void, Never>?
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var filtered: [AlphabetSymbol] {
        let value = query.trimmingCharacters(in: .whitespacesAndNewlines)
        return symbols.filter { value.isEmpty || $0.character.localizedCaseInsensitiveContains(value) || $0.morse.contains(value) }
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            EducationHeading(title: settings.text("alphabet"), subtitle: settings.text("normalizationNote"), theme: theme)
            HStack(spacing: 20) {
                EducationAlphabetPicker(settings: settings, selection: $alphabet)
                TextField(settings.text("searchTerms"), text: $query).textFieldStyle(.roundedBorder)
            }.brandCard(theme)
            if !error.isEmpty { Text(error).foregroundStyle(.red) }
            if filtered.isEmpty { Text(settings.text("noResults")).foregroundStyle(theme.muted) }
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 190), spacing: 12)], spacing: 12) {
                ForEach(filtered) { symbol in
                    Button { play(symbol.morse) } label: {
                        HStack(spacing: 12) {
                            Text(symbol.character).font(BrandTheme.heading(28)).frame(minWidth: 30)
                            Text(symbol.morse).font(.system(size: 19, design: .monospaced))
                            Spacer(minLength: 0)
                            Image(systemName: "speaker.wave.2").font(.caption)
                        }
                        .environment(\.layoutDirection, .leftToRight)
                        .padding(14).frame(maxWidth: .infinity, minHeight: 60)
                        .background(theme.card, in: RoundedRectangle(cornerRadius: 12))
                        .overlay(RoundedRectangle(cornerRadius: 12).stroke(theme.line.opacity(0.7)))
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("\(symbol.character), \(symbol.morse), \(settings.text("play"))")
                }
            }
            Button(settings.text("stop")) { stopPlayback() }.buttonStyle(BrandButtonStyle(theme: theme))
        }
        .task(id: alphabet) {
            do { symbols = try CoreAlphabet.symbols(alphabet); error = "" }
            catch { symbols = []; self.error = settings.text("nativeUnavailable") }
        }
        .onChange(of: alphabet) { stopPlayback() }
        .onDisappear { stopPlayback() }
    }
    private func play(_ morse: String) {
        stopPlayback()
        playback = Task {
            do { try await MacAudio.shared.play(morse: morse); error = "" }
            catch is CancellationError { }
            catch { self.error = settings.text("error") }
        }
    }
    private func stopPlayback() { playback?.cancel(); playback = nil; MacAudio.shared.stop() }
}

private struct EducationGlossary: View {
    let settings: AppSettings
    @Environment(\.colorScheme) private var scheme
    @State private var query = ""
    @State private var category = "allTerms"
    @State private var terms: [EducationTerm] = []
    @State private var phonetics: [String] = []
    @State private var loading = false
    @State private var error = ""
    @State private var playback: Task<Void, Never>?
    private let categories = ["allTerms", "qcodes", "abbreviations", "prosigns", "phonetics"]
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var filtered: [EducationTerm] {
        terms.filter { term in
            (category == "allTerms" || category == term.category) &&
            (query.isEmpty || term.code.localizedCaseInsensitiveContains(query) || settings.text("term_" + term.key).localizedCaseInsensitiveContains(query))
        }
    }
    private var filteredPhonetics: [String] {
        guard category == "allTerms" || category == "phonetics" else { return [] }
        return phonetics.filter { query.isEmpty || $0.localizedCaseInsensitiveContains(query) }
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            EducationHeading(title: settings.text("glossary"), subtitle: settings.text("glossaryHint"), theme: theme)
            HStack(spacing: 20) {
                TextField(settings.text("searchTerms"), text: $query).textFieldStyle(.roundedBorder)
                Picker(settings.text("category"), selection: $category) {
                    ForEach(categories, id: \.self) { Text(settings.text($0)).tag($0) }
                }.frame(maxWidth: 280)
            }.brandCard(theme)
            if loading { ProgressView() }
            if !error.isEmpty { Text(error).foregroundStyle(.red) }
            if !loading && filtered.isEmpty && filteredPhonetics.isEmpty { Text(settings.text("noResults")).foregroundStyle(theme.muted) }
            LazyVGrid(columns: [GridItem(.adaptive(minimum: 290), spacing: 14)], alignment: .leading, spacing: 14) {
                ForEach(filtered) { term in
                    VStack(alignment: .leading, spacing: 9) {
                        HStack {
                            Text(term.code).font(.system(size: 20, weight: .medium, design: .monospaced))
                            Spacer()
                            Button { play(term.code) } label: { Image(systemName: "play.fill") }
                                .buttonStyle(.plain).accessibilityLabel(settings.text("play") + " " + term.code)
                        }.environment(\.layoutDirection, .leftToRight)
                        Text(settings.text("term_" + term.key))
                        Text(term.example).font(BrandTheme.body(14)).foregroundStyle(theme.muted)
                            .environment(\.layoutDirection, .leftToRight)
                    }.brandCard(theme)
                }
                ForEach(filteredPhonetics, id: \.self) { Text($0).frame(maxWidth: .infinity, alignment: .leading).brandCard(theme).environment(\.layoutDirection, .leftToRight) }
            }
            Button(settings.text("stop")) { stopPlayback() }.buttonStyle(BrandButtonStyle(theme: theme))
        }
        .task { await load() }
        .onDisappear { stopPlayback() }
    }
    private func load() async {
        loading = true
        defer { loading = false }
        do {
            let data = try await MacServices.shared.call("catalog")
            try Task.checkCancellation()
            terms = data["terms"].arrayValue.map { EducationTerm(code: $0["code"].stringValue, key: $0["key"].stringValue, category: $0["category"].stringValue, example: $0["example"].stringValue) }
            phonetics = data["phonetics"].arrayValue.enumerated().map { index, item in
                "\(String(UnicodeScalar(65 + index)!)) · \(item.stringValue)"
            }
            error = ""
        } catch is CancellationError { }
        catch { self.error = settings.text("error") }
    }
    private func play(_ text: String) {
        stopPlayback()
        playback = Task {
            do {
                let result = try await MacServices.shared.call("cw.encode", .object(["text": .string(text)]))
                try await MacAudio.shared.play(morse: result["morse"].stringValue)
                error = ""
            } catch is CancellationError { }
            catch { self.error = settings.text("error") }
        }
    }
    private func stopPlayback() { playback?.cancel(); playback = nil; MacAudio.shared.stop() }
}
