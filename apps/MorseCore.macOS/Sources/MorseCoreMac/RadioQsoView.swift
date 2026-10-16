import SwiftUI
import AppKit
import UniformTypeIdentifiers
import MorseCoreBridge

struct RadioQsoView: View {
    @Bindable var settings: AppSettings
    @Bindable var state: RadioState
    @Environment(\.colorScheme) private var scheme
    @State private var country = ""
    @State private var license = ""
    @State private var mode = 0
    @State private var region = ""
    @State private var manual = ""
    @State private var frequency = "14.025"
    @State private var stage = 0
    @State private var other = ""
    @State private var sent = ""
    @State private var received = ""
    @State private var message = ""
    @State private var note = ""
    @State private var grid = ""
    @State private var practice = true
    @State private var prepared = ""
    @State private var morse = ""
    @State private var identity = ""
    @State private var band = ""
    @State private var setupExpanded = false
    @State private var recordExpanded = false
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private let stages = ["checkFrequency", "generalCall", "answerCall", "signalReport", "freeMessage", "closeQso"]
    private var classes: [String] { state.country(country)["classes"].arrayValue.map(\.stringValue) }
    private var context: ServiceJSON { .object(["countryId": .string(country), "classId": .string(license), "mode": .number(Double(mode)), "region": .string(region), "manualCall": .string(manual)]) }
    private var setupKey: String { state.active.radioID + "|" + country + "|" + license + "|" + String(mode) + "|" + region + "|" + manual }
    private var inputKey: String { setupKey + "|" + frequency + "|" + String(stage) + "|" + other + "|" + sent + "|" + message }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            if state.active.radioID.isEmpty {
                VStack(alignment: .leading, spacing: 16) {
                    Text(settings.text("stationRequired"))
                    Button(settings.text("uxCreateStation"), systemImage: "person.crop.circle.badge.plus") { state.returnToQso = true; state.route = .stations }
                        .buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                }.brandCard(theme)
            } else {
                setup
                composition
                recording
                RadioBandGuide(settings: settings, state: state, countryId: country, license: license)
            }
        }
        .task(id: state.active.radioID) { await loadActive() }
        .onChange(of: inputKey) { _, _ in prepared = ""; morse = "" }
        .task(id: setupKey) {
            identity = ""; band = ""
            guard !state.active.radioID.isEmpty, !country.isEmpty else { return }
            do {
                let call = try await operating()
                try Task.checkCancellation()
                identity = call["callSign"].stringValue + " · " + settings.text(call["explanationKey"].stringValue)
            } catch is CancellationError { }
            catch { if !Task.isCancelled { identity = settings.text("invalidCall") } }
        }
    }
    private var setup: some View {
        DisclosureGroup(identity.isEmpty ? state.active["callSign"].stringValue : identity, isExpanded: $setupExpanded) {
            VStack(alignment: .leading, spacing: 14) {
                Picker(settings.text("activeStation"), selection: Binding(get: { state.document["activeId"].stringValue }, set: { selected in Task { await state.run { try await state.saveProfiles(state.profiles, activeId: selected) } } })) {
                    ForEach(state.profiles, id: \.radioID) { Text($0["callSign"].stringValue).tag($0.radioID) }
                }
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 270), spacing: 16)], alignment: .leading, spacing: 14) {
                    Picker(settings.text("operatingCountry"), selection: $country) { ForEach(state.countries, id: \.radioID) { Text(settings.text($0["nameKey"].stringValue)).tag($0.radioID) } }
                        .onChange(of: country) { _, value in license = value == state.active["countryId"].stringValue ? state.active["classId"].stringValue : ""; manual = "" }
                    Picker(settings.text("operatingClass"), selection: $license) { Text(settings.text("selectClass")).tag(""); ForEach(classes, id: \.self) { Text(radioClassName($0, settings)).tag($0) } }
                    Picker(settings.text("stationMode"), selection: $mode) { Text(settings.text("fixedStation")).tag(0); Text(settings.text("portableStation")).tag(1); Text(settings.text("mobileStation")).tag(2) }
                    if country == "TR" { RadioField(title: settings.text("operatingRegion"), value: $region, ltr: true, limit: 1) }
                    RadioField(title: settings.text("manualOperatingCall"), value: $manual, ltr: true, limit: 32)
                    RadioField(title: settings.text("qsoFrequency"), value: $frequency, ltr: true, limit: 18)
                }
                Button(settings.text("checkBand"), systemImage: "checkmark.shield") { Task { await state.run { let assessment = try await assess(); band = assessmentDescription(assessment) } } }
                    .buttonStyle(BrandButtonStyle(theme: theme)).disabled(state.busy)
                if !band.isEmpty { Text(band).font(.caption).foregroundStyle(theme.muted) }
            }.padding(.top, 14).disabled(state.busy)
        }.brandCard(theme)
    }
    private var composition: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 16) {
                Picker(settings.text("qsoStep"), selection: $stage) { ForEach(Array(stages.enumerated()), id: \.element) { item in Text(settings.text(item.element)).tag(item.offset) } }
                RadioField(title: settings.text("otherCall"), value: $other, ltr: true, limit: 32)
            }.disabled(state.busy)
            if stage == 3 {
                HStack(spacing: 16) { RadioField(title: settings.text("sentRst"), value: $sent, ltr: true, limit: 3); RadioField(title: settings.text("receivedRst"), value: $received, ltr: true, limit: 3) }.disabled(state.busy)
                Text(settings.text("rstHint")).font(.caption).foregroundStyle(theme.muted)
            }
            if stage == 4 { RadioField(title: settings.text("freeMessage"), value: $message, limit: 2000).disabled(state.busy) }
            Button(settings.text("prepareMessage"), systemImage: "text.badge.checkmark") { Task { await state.run { try await compose() } } }
                .buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(state.busy)
            if !prepared.isEmpty {
                ViewThatFits(in: .horizontal) {
                    HStack(alignment: .top, spacing: 16) { result(settings.text("qsoPreview"), prepared); result(settings.text("morseInput"), morse, code: true) }.frame(minWidth: 600)
                    VStack(alignment: .leading, spacing: 16) { result(settings.text("qsoPreview"), prepared); result(settings.text("morseInput"), morse, code: true) }
                }
                ViewThatFits(in: .horizontal) {
                    HStack { audioButtons }
                    VStack(alignment: .leading) { audioButtons }
                }
            }
            Text(settings.text("qsoProcedureNote")).font(.caption).foregroundStyle(theme.muted)
        }.brandCard(theme)
    }
    @ViewBuilder private var audioButtons: some View {
        Button(settings.text("play"), systemImage: "play.fill") { Task { await state.run { try await MacAudio.shared.play(morse: morse, wpm: settings.wpm, frequency: settings.frequency) } } }.disabled(state.busy || morse.isEmpty)
        Button(settings.text("stop"), systemImage: "stop.fill") { MacAudio.shared.stop() }
        Button(settings.text("copy"), systemImage: "document.on.document") { radioCopy(prepared); state.notice = settings.text("copied") }
        Button(settings.text("exportWav"), systemImage: "square.and.arrow.down") { Task { await state.run { try await exportWav() } } }.disabled(state.busy || morse.isEmpty)
    }
    private var recording: some View {
        DisclosureGroup(settings.text("qsoRecordDetails"), isExpanded: $recordExpanded) {
            VStack(alignment: .leading, spacing: 14) {
                Toggle(settings.text("practiceRecord"), isOn: $practice)
                Text(settings.text(practice ? "practice" : "externalQso")).font(.caption).foregroundStyle(theme.muted)
                HStack(spacing: 16) { RadioField(title: settings.text("sentRst"), value: $sent, ltr: true, limit: 3); RadioField(title: settings.text("receivedRst"), value: $received, ltr: true, limit: 3) }
                RadioField(title: settings.text("qsoNote"), value: $note, limit: 4000)
                RadioField(title: settings.text("logbook.grid"), value: $grid, ltr: true, limit: 12)
                Text(settings.text("logbook.locationHint")).font(.caption).foregroundStyle(theme.muted)
                Button(settings.text("saveQso"), systemImage: "square.and.arrow.down") { Task { await state.run { try await save() } } }
                    .buttonStyle(BrandButtonStyle(theme: theme)).disabled(state.busy || other.isEmpty)
            }.padding(.top, 14).disabled(state.busy)
        }.brandCard(theme)
    }
    private func result(_ title: String, _ content: String, code: Bool = false) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).foregroundStyle(theme.muted)
            Text(content).font(code ? .system(.body, design: .monospaced) : BrandTheme.body())
                .textSelection(.enabled).frame(maxWidth: .infinity, minHeight: 110, alignment: .topLeading)
                .padding(16).background(code ? theme.result : theme.input, in: RoundedRectangle(cornerRadius: 14))
        }.environment(\.layoutDirection, .leftToRight)
    }
    private func loadActive() async {
        let profile = state.active
        country = profile["countryId"].stringValue; license = profile["classId"].stringValue; manual = ""; region = ""
        if let parsed = try? await MacServices.shared.call("callsign", .object(["value": profile["callSign"]])),
           !Task.isCancelled, profile == state.active { region = parsed["region"].stringValue }
    }
    private func hz() throws -> Double {
        guard let value = radioFrequencyHz(frequency) else { throw RadioInputError.invalid(settings.text("logbook.editInvalid")) }
        return value
    }
    private func operating() async throws -> ServiceJSON { try await MacServices.shared.call("operating", .object(["profile": state.active, "context": context])) }
    private func confirmedCall(profile: ServiceJSON, context: ServiceJSON) async throws -> String {
        let value = try await MacServices.shared.call("operating", .object(["profile": profile, "context": context]))
        if value["needsConfirmation"].boolValue && context["manualCall"].stringValue.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { throw RadioInputError.invalid(settings.text(value["explanationKey"].stringValue)) }
        return value["callSign"].stringValue
    }
    private func assess() async throws -> ServiceJSON {
        try await MacServices.shared.call("assess", .object(["countryId": .string(country), "classId": .string(license), "frequencyHz": .number(try hz())]))
    }
    private func assessmentDescription(_ value: ServiceJSON) -> String {
        switch value["status"].intValue {
        case 0: return settings.text("bandMatch") + " · " + value["band"]["band"].stringValue + " · " + radioNumber(value["power"]["watts"].doubleValue) + " W " + value["power"]["basis"].stringValue + " · " + settings.text(value["band"]["conditionsKey"].stringValue)
        case 2: return settings.text("selectClass")
        case 3: return settings.text("unknownCountry")
        default: return settings.text("bandUnlisted")
        }
    }
    private func compose() async throws {
        let snapshot = inputKey
        let profile = state.active, operatingContext = context
        let payload: ServiceJSON = .object(["stage": .number(Double(stage)), "otherCall": .string(other), "sentRst": .string(sent), "name": profile["operatorName"], "qth": profile["qth"], "message": .string(message)])
        let assessmentPayload: ServiceJSON = .object(["countryId": .string(country), "classId": .string(license), "frequencyHz": .number(try hz())])
        let own = try await confirmedCall(profile: profile, context: operatingContext)
        let result = try await MacServices.shared.call("compose", payload.replacing("ownCall", with: .string(own)))
        guard snapshot == inputKey else { return }
        prepared = result["text"].stringValue; morse = result["morse"].stringValue
        let assessment = try await MacServices.shared.call("assess", assessmentPayload)
        guard snapshot == inputKey else { return }
        band = assessmentDescription(assessment)
    }
    private func save() async throws {
        let profile = state.active, operatingContext = context
        let frequencyHz = try hz()
        let assessmentPayload: ServiceJSON = .object(["countryId": .string(country), "classId": .string(license), "frequencyHz": .number(frequencyHz)])
        let draft: ServiceJSON = .object(["schema": .number(1), "id": .string(radioNewID()), "utc": .string(ISO8601DateFormatter().string(from: Date())), "otherCall": .string(other), "countryId": .string(country), "frequencyHz": .number(frequencyHz), "sentRst": .string(sent), "receivedRst": .string(received), "practice": .bool(practice), "note": .string(note), "gridSquare": .string(grid.trimmingCharacters(in: .whitespacesAndNewlines).uppercased())])
        let own = try await confirmedCall(profile: profile, context: operatingContext)
        let assessment = try await MacServices.shared.call("assess", assessmentPayload)
        let band = assessment["band"]["band"].stringValue
        let entry = draft.replacing("ownCall", with: .string(own)).replacing("band", with: .string(band.isEmpty ? radioBand("", frequencyHz: frequencyHz) ?? "" : band))
        _ = try await MacServices.shared.call("qso.append", .object(["entry": entry]))
        state.notice = settings.text("saved")
    }
    private func exportWav() async throws {
        let panel = NSSavePanel(); panel.allowedContentTypes = [.wav]; panel.nameFieldStringValue = "qso.wav"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        let snapshot = morse, wpm = UInt32(settings.wpm), frequency = UInt32(settings.frequency)
        try await Task.detached { try CoreAudio.writeWAV(url: url, morse: snapshot, separator: 47, wpm: wpm, frequency: frequency) }.value
        state.notice = settings.text("saved")
    }
}

enum RadioInputError: LocalizedError {
    case invalid(String)
    var errorDescription: String? { switch self { case .invalid(let text): text } }
}
