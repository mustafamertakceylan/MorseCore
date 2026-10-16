import SwiftUI

struct RadioStationView: View {
    @Bindable var settings: AppSettings
    @Bindable var state: RadioState
    @Environment(\.colorScheme) private var scheme
    @State private var id = radioNewID()
    @State private var call = ""
    @State private var country = "TR"
    @State private var license = ""
    @State private var label = ""
    @State private var name = ""
    @State private var qth = ""
    @State private var analysis = ""
    @State private var deleteConfirmation = false
    @State private var optional = false
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var classes: [String] { state.country(country)["classes"].arrayValue.map(\.stringValue) }
    private var exists: Bool { state.profiles.contains { $0.radioID == id } }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 14) {
                Text(settings.text("uxStationStep")).foregroundStyle(theme.muted)
                if !state.profiles.isEmpty {
                    Picker(settings.text("stationPicker"), selection: $id) {
                        Text(settings.text("newStation")).tag(exists ? "new" : id)
                        ForEach(state.profiles, id: \.radioID) { profile in Text(profile["label"].stringValue + " · " + profile["callSign"].stringValue).tag(profile.radioID) }
                    }.onChange(of: id) { _, selected in
                        if selected == "new" { clear() }
                        else if let profile = state.profiles.first(where: { $0.radioID == selected }) { load(profile) }
                    }
                }
                RadioField(title: settings.text("callSign"), value: $call, ltr: true, limit: 32)
                Button(settings.text("analyzeCall"), systemImage: "magnifyingglass") { Task { await state.run { try await analyze() } } }
                    .buttonStyle(BrandButtonStyle(theme: theme)).disabled(state.busy || call.isEmpty)
                if !analysis.isEmpty { Text(analysis).foregroundStyle(theme.muted).textSelection(.enabled) }
                HStack(alignment: .top, spacing: 16) {
                    Picker(settings.text("homeCountry"), selection: $country) { ForEach(state.countries, id: \.radioID) { item in Text(settings.text(item["nameKey"].stringValue)).tag(item.radioID) } }
                        .onChange(of: country) { _, _ in if !classes.contains(license) { license = "" } }
                    Picker(settings.text("licenseClass"), selection: $license) {
                        Text(settings.text("selectClass")).tag("")
                        ForEach(classes, id: \.self) { Text(radioClassName($0, settings)).tag($0) }
                    }
                }
                DisclosureGroup(settings.text("stationOptional"), isExpanded: $optional) {
                    VStack(spacing: 12) {
                        HStack(spacing: 14) { RadioField(title: settings.text("profileName"), value: $label); RadioField(title: settings.text("operatorName"), value: $name) }
                        RadioField(title: settings.text("qth"), value: $qth)
                    }.padding(.top, 12)
                }
                Text(settings.text("callOfflineNote")).font(.caption).foregroundStyle(theme.muted)
                HStack {
                    Button(settings.text(state.returnToQso ? "uxSaveAndContinue" : "saveStation"), systemImage: "square.and.arrow.down") { Task { await state.run { try await save() } } }
                        .buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(state.busy || call.isEmpty || license.isEmpty || !state.warning.isEmpty)
                    if exists {
                        Button(settings.text("deleteStation"), role: .destructive) { deleteConfirmation = true }.disabled(state.busy || !state.warning.isEmpty)
                    }
                }
            }.brandCard(theme).disabled(state.busy)
            RadioBandGuide(settings: settings, state: state, countryId: country, license: license)
        }
        .task { if !state.active.radioID.isEmpty { load(state.active) } }
        .confirmationDialog(settings.text("deleteStation"), isPresented: $deleteConfirmation, titleVisibility: .visible) {
            Button(settings.text("deleteStation"), role: .destructive) { Task { await state.run {
                let rows = state.profiles.filter { $0.radioID != id }
                let activeID = RadioState.activeID(afterRemoving: id, from: state.document)
                try await state.saveProfiles(rows, activeId: activeID)
                clear()
            } } }
            Button(settings.text("cancelUpdate"), role: .cancel) { }
        } message: { Text(call) }
    }
    private func load(_ item: ServiceJSON) {
        id = item.radioID; call = item["callSign"].stringValue; country = item["countryId"].stringValue
        license = item["classId"].stringValue; label = item["label"].stringValue
        name = item["operatorName"].stringValue; qth = item["qth"].stringValue; analysis = ""
    }
    private func clear() { id = radioNewID(); call = ""; country = "TR"; license = ""; label = ""; name = ""; qth = ""; analysis = "" }
    private func analyze() async throws {
        let info = try await MacServices.shared.call("callsign", .object(["value": .string(call)]))
        call = info["normalized"].stringValue
        country = info["countryId"].stringValue.isEmpty ? "OTHER" : info["countryId"].stringValue
        license = info["classId"].stringValue
        analysis = settings.text(state.country(country)["nameKey"].stringValue)
            + " · " + (license.isEmpty ? settings.text("classManual") : settings.text("licenseClass") + ": " + radioClassName(license, settings))
        let region = info["region"].stringValue
        if !region.isEmpty { analysis += " · " + settings.text("region") + ": " + region }
        let detail = info["areaDetail"].stringValue
            .replacingOccurrences(of: "@EUROPE@", with: settings.text("europe"))
            .replacingOccurrences(of: "@ASIA@", with: settings.text("asia"))
            .replacingOccurrences(of: "@ISLANDS@", with: settings.text("allIslands"))
        if !detail.isEmpty { analysis += "\n" + detail }
    }
    private func save() async throws {
        let info = try await MacServices.shared.call("callsign", .object(["value": .string(call)]))
        let normalized = info["normalized"].stringValue
        let recognizedCountry = info["countryId"].stringValue
        guard classes.contains(license), recognizedCountry.isEmpty || recognizedCountry == country else { state.notice = settings.text("stationCheck"); return }
        let profile: ServiceJSON = .object(["id": .string(id), "callSign": .string(normalized), "countryId": .string(country), "classId": .string(license), "label": .string(label.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? normalized : label.trimmingCharacters(in: .whitespacesAndNewlines)), "operatorName": .string(name), "qth": .string(qth)])
        try await state.saveProfiles(state.profiles.filter { $0.radioID != id } + [profile], activeId: id)
        call = normalized; state.notice = settings.text("saved")
        if state.returnToQso { state.returnToQso = false; state.route = .prepare }
    }
}
