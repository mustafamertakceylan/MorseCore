import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct RadioLogView: View {
    @Bindable var settings: AppSettings
    @Bindable var state: RadioState
    var initiallyShowMap = false
    @Environment(\.colorScheme) private var scheme
    @State private var practice = true
    @State private var searchCall = ""
    @State private var searchBand = ""
    @State private var from = ""
    @State private var through = ""
    @State private var applied: ServiceJSON = .object([:])
    @State private var cursors = [""]
    @State private var pageIndex = 0
    @State private var nextCursor = ""
    @State private var entries: [ServiceJSON] = []
    @State private var total = 0
    @State private var showMap = false
    @State private var positions: ServiceJSON = .null
    @State private var loading = false
    @State private var generation = UUID()
    @State private var draft: RadioEditItem?
    @State private var importing = false
    @State private var fallback = ""
    @State private var importCountry = "OTHER"
    @State private var importPractice = true
    @State private var preview: ServiceJSON = .null
    @State private var previewName = ""
    @State private var filterExpanded = false
    @State private var filesExpanded = false
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var importCandidates: [ServiceJSON] { preview["preview"]["candidates"].arrayValue }
    private var uniqueIssues: [ServiceJSON] {
        var seen = Set<String>()
        return preview["document"]["issues"].arrayValue.filter { seen.insert($0.radioIssueID).inserted }
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Picker(settings.text("recordType"), selection: $practice) {
                Text(settings.text("practice")).tag(true)
                Text(settings.text("externalQso")).tag(false)
            }.pickerStyle(.segmented).frame(maxWidth: 420)
            HStack {
                Button(settings.text("logbook.map"), systemImage: "map") { showMap.toggle(); Task { await refresh() } }
                    .buttonStyle(BrandButtonStyle(theme: theme, prominent: showMap))
                Spacer()
                if loading { ProgressView().controlSize(.small) }
                Text("\(settings.text("logbook.matches")): \(total) · \(settings.text("logbook.page")) \(pageIndex + 1)").foregroundStyle(theme.muted)
            }
            filters
            fileActions
            if !previewName.isEmpty { importReview }
            if showMap { RadioMapView(settings: settings, outline: state.catalog["outline"], map: positions).brandCard(theme) }
            if entries.isEmpty && !loading { Text(settings.text("noRecords")).foregroundStyle(theme.muted).brandCard(theme) }
            ForEach(entries, id: \.radioID) { entry in
                VStack(alignment: .leading, spacing: 9) {
                    Text(radioUTC(entry["utc"].stringValue, seconds: !isFalse(entry["timeHasSeconds"])) + " UTC · " + entry["ownCall"].stringValue + " → " + entry["otherCall"].stringValue).font(.headline).environment(\.layoutDirection, .leftToRight)
                    Text((entry["frequencyHz"].doubleValue > 0 ? radioMHz(entry["frequencyHz"].doubleValue) : settings.text("logbook.frequencyUnknown")) + " · " + entry["band"].stringValue + " · CW · " + entry["sentRst"].stringValue + " / " + entry["receivedRst"].stringValue).foregroundStyle(theme.muted).environment(\.layoutDirection, .leftToRight)
                    if !entry["gridSquare"].stringValue.isEmpty { Text(entry["gridSquare"].stringValue).font(.caption).environment(\.layoutDirection, .leftToRight) }
                    if !entry["note"].stringValue.isEmpty { Text(entry["note"].stringValue).textSelection(.enabled) }
                    Button(settings.text("logbook.edit"), systemImage: "pencil") { draft = RadioEditItem(entry: entry) }.disabled(state.busy || loading)
                }.brandCard(theme)
            }
            HStack {
                Button(settings.text("logbook.previous")) { pageIndex -= 1; Task { await refresh() } }.disabled(pageIndex == 0 || loading)
                Button(settings.text("logbook.next")) { cursors = Array(cursors.prefix(pageIndex + 1)) + [nextCursor]; pageIndex += 1; Task { await refresh() } }.disabled(nextCursor.isEmpty || loading)
            }.buttonStyle(BrandButtonStyle(theme: theme))
        }
        .task { showMap = initiallyShowMap; fallback = state.active["callSign"].stringValue; importCountry = state.active["countryId"].stringValue.isEmpty ? "OTHER" : state.active["countryId"].stringValue; await refresh() }
        .onChange(of: practice) { _, _ in resetPage(); Task { await refresh() } }
        .fileImporter(isPresented: $importing, allowedContentTypes: [UTType(filenameExtension: "adi") ?? .data, UTType(filenameExtension: "adif") ?? .data]) { result in
            if case .success(let url) = result { Task { await state.run { try await loadImport(url) } } }
            else if case .failure(let error) = result { state.notice = error.localizedDescription }
        }
        .sheet(item: $draft) { item in
            RadioLogEditor(settings: settings, state: state, entry: item.entry) {
                draft = nil; resetPage(); Task { await refresh() }
            }.frame(minWidth: 640, minHeight: 660)
        }
    }
    private var filters: some View {
        DisclosureGroup(settings.text("logbook.filters"), isExpanded: $filterExpanded) {
            VStack(alignment: .leading, spacing: 12) {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 250), spacing: 16)], spacing: 12) {
                    RadioField(title: settings.text("logbook.searchCall"), value: $searchCall, ltr: true, limit: 32)
                    RadioField(title: settings.text("logbook.band"), value: $searchBand, ltr: true, limit: 30)
                    RadioField(title: settings.text("logbook.from"), value: $from, ltr: true, limit: 10)
                    RadioField(title: settings.text("logbook.through"), value: $through, ltr: true, limit: 10)
                }
                Text(settings.text("logbook.dateHint")).font(.caption).foregroundStyle(theme.muted)
                HStack {
                    Button(settings.text("logbook.search"), systemImage: "magnifyingglass") {
                        guard validDate(from), validDate(through), from.isEmpty || through.isEmpty || from <= through else { state.notice = settings.text("logbook.dateHint"); return }
                        applied = .object(["call": .string(searchCall.trimmingCharacters(in: .whitespacesAndNewlines)), "band": .string(searchBand.trimmingCharacters(in: .whitespacesAndNewlines)), "from": from.isEmpty ? .null : .string(from), "through": through.isEmpty ? .null : .string(through)])
                        resetPage(); Task { await refresh() }
                    }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                    Button(settings.text("logbook.reset")) { searchCall = ""; searchBand = ""; from = ""; through = ""; applied = .object([:]); resetPage(); Task { await refresh() } }
                        .buttonStyle(BrandButtonStyle(theme: theme))
                }.disabled(loading)
            }.padding(.top, 14)
        }.brandCard(theme)
    }
    private var fileActions: some View {
        DisclosureGroup(settings.text("radioFiles"), isExpanded: $filesExpanded) {
            VStack(alignment: .leading, spacing: 14) {
                HStack {
                    Button(settings.text("exportAdif"), systemImage: "square.and.arrow.up") { Task { await state.run { try await export("adif") } } }
                    Button(settings.text("exportCsv"), systemImage: "square.and.arrow.up") { Task { await state.run { try await export("csv") } } }
                }.buttonStyle(BrandButtonStyle(theme: theme)).disabled(state.busy || loading)
                Text(settings.text("logbook.browseHint") + " " + settings.text("logbook.exportAll")).font(.caption).foregroundStyle(theme.muted)
                Divider()
                Text(settings.text("logbook.importHint")).font(.caption).foregroundStyle(theme.muted)
                RadioField(title: settings.text("logbook.fallbackCall"), value: $fallback, ltr: true, limit: 32)
                Picker(settings.text("operatingCountry"), selection: $importCountry) { ForEach(state.countries, id: \.radioID) { Text(settings.text($0["nameKey"].stringValue)).tag($0.radioID) } }
                Picker(settings.text("logbook.unmarked"), selection: $importPractice) { Text(settings.text("practice")).tag(true); Text(settings.text("externalQso")).tag(false) }
                Button(settings.text("logbook.chooseAdi"), systemImage: "square.and.arrow.down") { importing = true }.buttonStyle(BrandButtonStyle(theme: theme)).disabled(state.busy || loading)
            }.padding(.top, 14)
        }.brandCard(theme)
    }
    private var importReview: some View {
        let doc = preview["document"]
        return VStack(alignment: .leading, spacing: 12) {
            Text(settings.text("logbook.review") + " · " + previewName).font(.headline)
            Text("\(settings.text("logbook.importTotal")): \(doc["total"].intValue) · \(settings.text("logbook.ready")): \(importCandidates.count) · \(settings.text("logbook.duplicates")): \(preview["preview"]["duplicates"].intValue) · \(settings.text("logbook.rejected")): \(doc["total"].intValue - doc["records"].arrayValue.count)")
            Text("\(settings.text("practice")): \(importCandidates.filter { $0["practice"].boolValue }.count) · \(settings.text("externalQso")): \(importCandidates.filter { !$0["practice"].boolValue }.count)")
            Text(settings.text("logbook.duplicateHint")).font(.caption).foregroundStyle(theme.muted)
            if doc["ignoredFields"].intValue > 0 { Text("\(settings.text("logbook.ignored")): \(doc["ignoredFields"].intValue)").font(.caption) }
            ForEach(Array(uniqueIssues.prefix(20)), id: \.radioIssueID) { item in
                Text("#\(item["record"].intValue) · \(settings.text(item["key"].stringValue)) · \(item["field"].stringValue)").font(.caption)
            }
            ForEach(Array(importCandidates.prefix(3)), id: \.radioID) { item in
                Text(radioUTC(item["utc"].stringValue) + " UTC · " + item["ownCall"].stringValue + " → " + item["otherCall"].stringValue + " · " + item["band"].stringValue).font(.caption).environment(\.layoutDirection, .leftToRight)
            }
            HStack {
                Button(settings.text("logbook.confirm"), systemImage: "checkmark") { Task { await state.run {
                    let result = try await MacServices.shared.call("adif.import", .object(["entries": .array(importCandidates)]))
                    state.notice = "\(settings.text("logbook.imported")): \(result["added"].intValue) · \(settings.text("logbook.duplicates")): \(result["duplicates"].intValue + preview["preview"]["duplicates"].intValue)"
                    preview = .null; previewName = ""; resetPage(); await refresh()
                } } }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(importCandidates.isEmpty || state.busy)
                Button(settings.text("logbook.cancel")) { preview = .null; previewName = "" }.disabled(state.busy)
            }
        }.brandCard(theme)
    }
    private func resetPage() { cursors = [""]; pageIndex = 0 }
    private func query(before: String? = nil, all: Bool = false) -> ServiceJSON {
        let base: ServiceJSON = all ? .object([:]) : applied
        return base.replacing("practice", with: .bool(practice)).replacing("before", with: (before ?? cursors[pageIndex]).isEmpty ? .null : .string(before ?? cursors[pageIndex])).replacing("limit", with: .number(all ? 100 : 25))
    }
    private func refresh() async {
        let request = UUID(); generation = request; loading = true
        do {
            let snapshot = query()
            let result = try await MacServices.shared.call("qso.query", .object(["query": snapshot]))
            let map = showMap ? try await MacServices.shared.call("qso.map", .object(["query": snapshot, "maximum": .number(500)])) : .null
            guard generation == request, !Task.isCancelled else { return }
            entries = result["entries"].arrayValue; total = result["total"].intValue; nextCursor = result["nextCursor"].stringValue; positions = map
        } catch { if generation == request { state.notice = error.localizedDescription } }
        if generation == request { loading = false }
    }
    private func validDate(_ text: String) -> Bool {
        if text.isEmpty { return true }
        let formatter = DateFormatter(); formatter.locale = Locale(identifier: "en_US_POSIX"); formatter.dateFormat = "yyyy-MM-dd"; formatter.isLenient = false
        guard let date = formatter.date(from: text) else { return false }
        return formatter.string(from: date) == text
    }
    private func loadImport(_ url: URL) async throws {
        let scoped = url.startAccessingSecurityScopedResource(); defer { if scoped { url.stopAccessingSecurityScopedResource() } }
        let resource = try url.resourceValues(forKeys: [.fileSizeKey])
        guard (resource.fileSize ?? 0) <= 8 * 1024 * 1024 else { throw RadioInputError.invalid(settings.text("logbook.importInvalid")) }
        let text = try String(contentsOf: url, encoding: .utf8)
        guard text.utf8.count <= 8 * 1024 * 1024 else { throw RadioInputError.invalid(settings.text("logbook.importInvalid")) }
        preview = try await MacServices.shared.call("adif.preview", .object(["text": .string(text), "ownCall": .string(fallback), "countryId": .string(importCountry), "practice": .bool(importPractice)]))
        previewName = url.lastPathComponent
    }
    private func export(_ format: String) async throws {
        let snapshotPractice = practice
        let panel = NSSavePanel()
        panel.allowedContentTypes = [UTType(filenameExtension: format == "adif" ? "adi" : "csv") ?? .plainText]
        panel.nameFieldStringValue = (snapshotPractice ? "morsecore-practice" : "morsecore-qso") + (format == "adif" ? ".adi" : ".csv")
        guard panel.runModal() == .OK, let destination = panel.url else { return }
        // The shared store streams all records of the selected type. Existing
        // files remain intact if validation, cancellation or writing fails.
        _ = try await MacServices.shared.call("qso.export.file", .object([
            "destination": .string(destination.path), "practice": .bool(snapshotPractice), "format": .string(format)
        ]))
        state.notice = settings.text("saved")
    }
    private func isFalse(_ value: ServiceJSON) -> Bool { if case .bool(false) = value { return true }; return false }
}

private struct RadioEditItem: Identifiable { let entry: ServiceJSON; var id: String { entry.radioID } }

private struct RadioLogEditor: View {
    let settings: AppSettings
    let state: RadioState
    let onSaved: () -> Void
    @Environment(\.dismiss) private var dismiss
    @Environment(\.colorScheme) private var scheme
    @State private var draft: ServiceJSON
    @State private var utc: String
    @State private var frequency: String
    @State private var latitude: String
    @State private var longitude: String
    @State private var error = ""
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    init(settings: AppSettings, state: RadioState, entry: ServiceJSON, onSaved: @escaping () -> Void) {
        self.settings = settings; self.state = state; self.onSaved = onSaved
        // These states intentionally seed the editable draft for one sheet presentation.
        _draft = State(initialValue: entry)
        let seconds: Bool; if case .bool(false) = entry["timeHasSeconds"] { seconds = false } else { seconds = true }
        _utc = State(initialValue: radioUTC(entry["utc"].stringValue, seconds: seconds))
        _frequency = State(initialValue: entry["frequencyHz"].doubleValue > 0 ? radioNumber(entry["frequencyHz"].doubleValue / 1_000_000) : "")
        if case .number(let lat) = entry["latitude"] { _latitude = State(initialValue: String(lat)) } else { _latitude = State(initialValue: "") }
        if case .number(let lon) = entry["longitude"] { _longitude = State(initialValue: String(lon)) } else { _longitude = State(initialValue: "") }
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(settings.text("logbook.edit")).font(BrandTheme.heading(28))
            ScrollView {
                LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], alignment: .leading, spacing: 16) {
                    RadioField(title: settings.text("logbook.utc"), value: $utc, ltr: true, limit: 19)
                    RadioField(title: settings.text("callSign"), value: binding("ownCall"), ltr: true, limit: 32)
                    RadioField(title: settings.text("otherCall"), value: binding("otherCall"), ltr: true, limit: 32)
                    RadioField(title: settings.text("qsoFrequency"), value: $frequency, ltr: true, limit: 18)
                    RadioField(title: settings.text("logbook.band"), value: binding("band"), ltr: true, limit: 30)
                    RadioField(title: settings.text("sentRst"), value: binding("sentRst"), ltr: true, limit: 3)
                    RadioField(title: settings.text("receivedRst"), value: binding("receivedRst"), ltr: true, limit: 3)
                    Picker(settings.text("operatingCountry"), selection: binding("countryId")) { ForEach(state.countries, id: \.radioID) { Text(settings.text($0["nameKey"].stringValue)).tag($0.radioID) } }
                    Toggle(settings.text("practiceRecord"), isOn: Binding(get: { draft["practice"].boolValue }, set: { draft = draft.replacing("practice", with: .bool($0)) }))
                    RadioField(title: settings.text("qsoNote"), value: binding("note"), limit: 4000)
                    RadioField(title: settings.text("logbook.grid"), value: binding("gridSquare"), ltr: true, limit: 12)
                    RadioField(title: settings.text("logbook.latitude"), value: $latitude, ltr: true, limit: 24)
                    RadioField(title: settings.text("logbook.longitude"), value: $longitude, ltr: true, limit: 24)
                }
                Text(settings.text("logbook.locationHint")).font(.caption).foregroundStyle(theme.muted).padding(.top, 12)
            }
            if !error.isEmpty { Text(error).foregroundStyle(.red) }
            HStack {
                Button(settings.text("logbook.save")) { Task { await save() } }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(state.busy)
                Button(settings.text("logbook.cancel")) { dismiss() }.disabled(state.busy)
            }
        }.padding(24).background(theme.canvas).foregroundStyle(theme.ink)
    }
    private func binding(_ key: String) -> Binding<String> { Binding(get: { draft[key].stringValue }, set: { draft = draft.replacing(key, with: .string($0)) }) }
    private func save() async {
        guard !state.busy else { return }
        guard let instant = radioParseUTC(utc), let hz = radioFrequencyHz(frequency, allowEmpty: true), let band = radioBand(draft["band"].stringValue, frequencyHz: hz) else { error = settings.text("logbook.editInvalid"); return }
        func coordinate(_ text: String) throws -> ServiceJSON { if text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return .null }; guard let value = Double(text), value.isFinite else { throw RadioInputError.invalid(settings.text("logbook.editInvalid")) }; return .number(value) }
        do {
            let edited = draft.replacing("band", with: .string(band)).replacing("utc", with: .string(instant)).replacing("timeHasSeconds", with: .bool(utc.count == 19)).replacing("frequencyHz", with: .number(hz)).replacing("latitude", with: try coordinate(latitude)).replacing("longitude", with: try coordinate(longitude))
            state.busy = true; defer { state.busy = false }
            _ = try await MacServices.shared.call("qso.update", .object(["entry": edited]))
            state.notice = settings.text("saved"); onSaved()
        } catch { self.error = error.localizedDescription }
    }
}
