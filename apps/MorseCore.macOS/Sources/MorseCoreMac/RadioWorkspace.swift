import SwiftUI
import Observation
import AppKit

// The Windows and Mac interfaces share the catalog, call sign rules and persistent store.
enum RadioSection: String, CaseIterable, Identifiable {
    case stations, prepare, logbook, map
    var id: String { rawValue }
}

@MainActor @Observable
final class RadioState {
    var catalog: ServiceJSON = .null
    var document: ServiceJSON = .object(["schema": .number(1), "activeId": .string(""), "profiles": .array([])])
    var warning = ""
    var notice = ""
    var busy = false
    var loaded = false
    var returnToQso = false
    var route: RadioSection?
    var profiles: [ServiceJSON] { document["profiles"].arrayValue }
    var active: ServiceJSON { profiles.first { $0["id"].stringValue == document["activeId"].stringValue } ?? .null }
    var countries: [ServiceJSON] { catalog["radio"]["countries"].arrayValue }
    static func activeID(afterRemoving removed: String, from document: ServiceJSON) -> String {
        let remaining = document["profiles"].arrayValue.filter { $0.radioID != removed }
        let active = document["activeId"].stringValue
        return remaining.contains { $0.radioID == active } ? active : remaining.first?.radioID ?? ""
    }
    func country(_ id: String) -> ServiceJSON { countries.first { $0["id"].stringValue == id } ?? .null }
    func run(_ work: () async throws -> Void) async {
        guard !busy else { return }
        busy = true
        defer { busy = false }
        do { try await work() }
        catch is CancellationError { }
        catch { notice = error.localizedDescription }
    }
    func load() async throws {
        catalog = try await MacServices.shared.call("catalog")
        let response = try await MacServices.shared.call("profiles.load")
        document = response["document"]
        warning = response["warning"].stringValue
        loaded = true
    }
    func saveProfiles(_ rows: [ServiceJSON], activeId: String) async throws {
        let next = ServiceJSON.object(["schema": .number(1), "activeId": .string(activeId), "profiles": .array(rows)])
        _ = try await MacServices.shared.call("profiles.save", .object(["document": next]))
        document = next
    }
}

struct RadioWorkspace: View {
    @Bindable var settings: AppSettings
    let section: RadioSection
    @State private var state = RadioState()
    @Environment(\.colorScheme) private var scheme
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            if !state.warning.isEmpty { Text(settings.text("radioDataError")).foregroundStyle(.red).brandCard(theme) }
            if !state.notice.isEmpty { Text(state.notice).textSelection(.enabled).foregroundStyle(theme.muted).accessibilityIdentifier("RadioNotice") }
            if !state.loaded {
                ProgressView(settings.text("busy"))
                Button(settings.text("retry")) { Task { await state.run { try await state.load() } } }
            } else {
                switch state.route ?? section {
                case .stations: RadioStationView(settings: settings, state: state)
                case .prepare: RadioQsoView(settings: settings, state: state)
                case .logbook: RadioLogView(settings: settings, state: state)
                case .map: RadioLogView(settings: settings, state: state, initiallyShowMap: true)
                }
            }
        }
        .task { if !state.loaded { await state.run { try await state.load() } } }
        .onChange(of: section) { _, _ in state.route = nil; state.notice = "" }
    }
}

struct RadioBandGuide: View {
    let settings: AppSettings
    let state: RadioState
    let countryId: String
    let license: String
    @Environment(\.colorScheme) private var scheme
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var country: ServiceJSON { state.country(countryId) }
    private var ranges: [ServiceJSON] { country["bands"].arrayValue.filter { band in band["powers"].arrayValue.contains { $0["classId"].stringValue == license } } }
    var body: some View {
        DisclosureGroup(settings.text("guide.title") + " · " + settings.text(country["nameKey"].stringValue)) {
            VStack(alignment: .leading, spacing: 16) {
                HStack(spacing: 12) {
                    fact("guide.country", settings.text(country["nameKey"].stringValue))
                    fact("licenseClass", license.isEmpty ? settings.text("selectClass") : radioClassName(license, settings))
                    fact("guide.ranges", String(ranges.count))
                }
                Text(settings.text("guide.intro"))
                Text(settings.text("guide.scope")).font(.caption).foregroundStyle(theme.muted)
                if license.isEmpty { Text(settings.text("selectClass")) }
                else if ranges.isEmpty { Text(settings.text("guide.empty")) }
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 280), spacing: 12)], spacing: 12) {
                    ForEach(ranges, id: \.radioBandID) { band in
                        bandCard(band)
                    }
                }
                Text(settings.text("guide.scale")).font(.caption).foregroundStyle(theme.muted)
                DisclosureGroup(settings.text("guide.details")) {
                    VStack(alignment: .leading, spacing: 10) {
                        Text(settings.text(country["notesKey"].stringValue))
                        Text(settings.text("rulesCoverage")).font(.caption)
                        ForEach(sources, id: \.radioID) { source in
                            if let url = URL(string: source["url"].stringValue), url.scheme == "https" {
                                Link(source["title"].stringValue + " · " + source["section"].stringValue, destination: url)
                            }
                        }
                    }.padding(.top, 12)
                }
                Text(settings.text("rulesReviewed") + " · " + state.catalog["radio"]["reviewedOn"].stringValue).font(.caption).foregroundStyle(theme.muted)
            }.padding(.top, 16)
        }.brandCard(theme).accessibilityIdentifier("CountryRules")
    }
    private var sources: [ServiceJSON] {
        let ids = country["sourceIds"].arrayValue.map(\.stringValue)
        return state.catalog["radio"]["sources"].arrayValue.filter { ids.contains($0["id"].stringValue) }
    }
    private func fact(_ key: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 4) { Text(settings.text(key)).font(.caption); Text(value).font(BrandTheme.heading(23)) }
            .frame(maxWidth: .infinity, alignment: .leading).padding(12).background(theme.result, in: RoundedRectangle(cornerRadius: 12))
    }
    private func bandCard(_ band: ServiceJSON) -> some View {
        let power = band["powers"].arrayValue.first { $0["classId"].stringValue == license } ?? .null
        let basis = power["basis"].stringValue
        return VStack(alignment: .leading, spacing: 12) {
            HStack { Text(band["band"].stringValue).font(BrandTheme.heading(30)); Spacer(); VStack(alignment: .trailing) { Text(settings.text("guide.power")).font(.caption); Text(radioNumber(power["watts"].doubleValue) + " W").font(.title3); Text(basis == "output" ? settings.text("powerOutput") : basis).font(.caption) } }
            HStack { VStack(alignment: .leading) { Text(settings.text("guide.start")).font(.caption); Text(radioMHz(band["startHz"].doubleValue)) }; Spacer(); VStack(alignment: .trailing) { Text(settings.text("guide.end")).font(.caption); Text(radioMHz(band["endHz"].doubleValue)) } }.environment(\.layoutDirection, .leftToRight)
            HStack(spacing: 0) { Circle().fill(theme.accent).frame(width: 10, height: 10); Rectangle().fill(theme.line).frame(height: 3); Circle().stroke(theme.accent, lineWidth: 2).frame(width: 10, height: 10) }
            let condition = band["conditionsKey"].stringValue
            if condition != "bandConditions" && (condition != "trHomebuilt" || license == "B") {
                Text(settings.text(condition)).font(.caption).padding(10).background(theme.result, in: RoundedRectangle(cornerRadius: 8))
            }
        }.brandCard(theme).environment(\.layoutDirection, .leftToRight)
    }
}
