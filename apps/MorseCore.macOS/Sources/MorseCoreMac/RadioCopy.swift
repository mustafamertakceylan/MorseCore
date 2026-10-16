import SwiftUI
import AppKit
import UniformTypeIdentifiers

extension ServiceJSON {
    var radioID: String { self["id"].stringValue }
    var radioIssueID: String { String(self["record"].intValue) + self["key"].stringValue + self["field"].stringValue }
    var radioBandID: String { self["band"].stringValue + ":" + String(self["startHz"].doubleValue) + ":" + String(self["endHz"].doubleValue) }
    func replacing(_ key: String, with value: ServiceJSON) -> ServiceJSON {
        guard case .object(var object) = self else { return .object([key: value]) }
        object[key] = value
        return .object(object)
    }
}

func radioNumber(_ value: Double) -> String {
    String(format: "%.6f", locale: Locale(identifier: "en_US_POSIX"), value)
        .replacingOccurrences(of: #"\.?0+$"#, with: "", options: .regularExpression)
}
func radioMHz(_ value: Double) -> String { radioNumber(value / 1_000_000) + " MHz" }
@MainActor func radioClassName(_ value: String, _ settings: AppSettings) -> String {
    let known = ["Foundation", "Intermediate", "Full", "General", "Technician", "Extra", "Advanced", "Novice", "Other", "Special"]
    return known.contains(value) ? settings.text("class_" + value) : value
}
func radioNewID() -> String { UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased() }
func radioUTC(_ value: String, seconds: Bool = true) -> String {
    let parser = ISO8601DateFormatter()
    parser.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
    let date = parser.date(from: value) ?? ISO8601DateFormatter().date(from: value)
    guard let date else { return value }
    let formatter = DateFormatter()
    formatter.locale = Locale(identifier: "en_US_POSIX")
    formatter.timeZone = TimeZone(secondsFromGMT: 0)
    formatter.dateFormat = seconds ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm"
    return formatter.string(from: date)
}
func radioParseUTC(_ value: String) -> String? {
    let formatter = DateFormatter()
    formatter.locale = Locale(identifier: "en_US_POSIX")
    formatter.timeZone = TimeZone(secondsFromGMT: 0)
    formatter.isLenient = false
    formatter.dateFormat = value.count == 16 ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd HH:mm:ss"
    guard let date = formatter.date(from: value), formatter.string(from: date) == value else { return nil }
    return ISO8601DateFormatter().string(from: date)
}
@MainActor func radioCopy(_ value: String) {
    NSPasteboard.general.clearContents()
    NSPasteboard.general.setString(value, forType: .string)
}
@MainActor func radioSaveText(_ text: String, extension ext: String, name: String) throws {
    let panel = NSSavePanel()
    panel.allowedContentTypes = [UTType(filenameExtension: ext) ?? .plainText]
    panel.nameFieldStringValue = name + "." + ext
    guard panel.runModal() == .OK, let url = panel.url else { return }
    try text.write(to: url, atomically: true, encoding: .utf8)
}

struct RadioField: View {
    let title: String
    @Binding var value: String
    var ltr = false
    var limit = 80
    @Environment(\.layoutDirection) private var direction
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(title).font(.subheadline)
            TextField(title, text: $value).textFieldStyle(.roundedBorder)
                .environment(\.layoutDirection, ltr ? .leftToRight : direction)
                .onChange(of: value) { _, next in if next.count > limit { value = String(next.prefix(limit)) } }
                .accessibilityLabel(title)
        }
    }
}



// Generated from AppCore/Radio/LogQuery.cs; these are ADIF interchange names,
// not transmission permissions. AppCore remains authoritative when saving.
private struct RadioAdifBand: Decodable, Sendable {
    let name: String
    let low: Double
    let high: Double
}
private let radioAdifBands: [RadioAdifBand] = {
    guard let url = Bundle.module.url(forResource: "adif-bands", withExtension: "json", subdirectory: "Resources"),
          let data = try? Data(contentsOf: url),
          let bands = try? JSONDecoder().decode([RadioAdifBand].self, from: data),
          bands.allSatisfy({ !$0.name.isEmpty && $0.low.isFinite && $0.high.isFinite && $0.low > 0 && $0.low <= $0.high }) else { return [] }
    return bands
}()
func radioBand(_ entered: String, frequencyHz: Double) -> String? {
    let value = entered.trimmingCharacters(in: .whitespacesAndNewlines)
    if value.isEmpty { return radioAdifBands.first { frequencyHz >= $0.low && frequencyHz <= $0.high }?.name ?? (frequencyHz > 0 ? "" : nil) }
    guard let match = radioAdifBands.first(where: { $0.name.caseInsensitiveCompare(value) == .orderedSame }), frequencyHz == 0 || frequencyHz >= match.low && frequencyHz <= match.high else { return nil }
    return match.name
}
func radioFrequencyHz(_ text: String, allowEmpty: Bool = false) -> Double? {
    if text.isEmpty { return allowEmpty ? 0 : nil }
    guard text.utf8.allSatisfy({ (48...57).contains($0) || $0 == 46 }),
          let mhz = Decimal(string: text, locale: Locale(identifier: "en_US_POSIX")), mhz > 0, mhz <= 300_000 else { return nil }
    var value = mhz * 1_000_000, integer = Decimal()
    NSDecimalRound(&integer, &value, 0, .plain)
    guard value == integer else { return nil }
    return NSDecimalNumber(decimal: integer).doubleValue
}
