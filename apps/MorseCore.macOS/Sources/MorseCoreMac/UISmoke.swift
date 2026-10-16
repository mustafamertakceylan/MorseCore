import SwiftUI
import AppKit
import MorseCoreBridge

/// Opt-in render evidence, separate from interactive testing.
@MainActor
enum UISmoke {
    private static var started = false
    static func runIfRequested() async {
        #if DEBUG
        guard !started, let path = ProcessInfo.processInfo.environment["MORSECORE_UI_SMOKE_DIR"], path.hasPrefix("/") else { return }
        started = true
        let directory = URL(fileURLWithPath: path, isDirectory: true)
        do {
            guard MacPaths.dataRoot.standardizedFileURL.path.hasPrefix(directory.standardizedFileURL.path + "/") else {
                throw ServiceFailure(message: "Render tests require an isolated data root inside their output directory.")
            }
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            try await seed()
            if ProcessInfo.processInfo.environment["MORSECORE_UPDATE_DOWNLOAD_SMOKE"] == "1" {
                _ = try await MacUpdateClient.verifyPublishedDownload(to: directory)
            }
            try await SidebarInteractionSmoke.run(directory: directory)
            var images: [String] = []
            let focused = ProcessInfo.processInfo.environment["MORSECORE_UI_SMOKE_FAST"] == "1"
            let languages: [AppLanguage] = focused ? [.tr, .ar] : AppLanguage.allCases
            let pages: [MacPage] = focused ? [.converter, .practice, .prepare, .settings] : MacPage.allCases
            for language in languages {
                for dark in [false, true] {
                    for page in pages {
                        images.append(try await render(language, dark, page, directory))
                    }
                    if focused { images.append(try await render(language, dark, .converter, directory, compact: true)) }
                }
            }
            for section in (focused ? [] : ["speech", "decode", "voices"]) {
                for dark in [false, true] {
                    images.append(try await render(.tr, dark, .audio, directory, section: section))
                }
            }
            let evidence: [String: Any] = ["nativeVersion": CoreConverter.version, "width": 1340, "height": 840,
                "images": images, "scope": "Isolated SwiftUI renders with synthetic station/QSO; not interactive end-to-end tests"]
            try JSONSerialization.data(withJSONObject: evidence, options: [.prettyPrinted, .sortedKeys])
                .write(to: directory.appendingPathComponent("render-smoke.json"), options: .atomic)
        } catch {
            try? Data("\(error)".utf8).write(to: directory.appendingPathComponent("render-error.txt"))
        }
        NSApplication.shared.terminate(nil)
        #endif
    }

    #if DEBUG
    private static func render(_ language: AppLanguage, _ dark: Bool, _ page: MacPage, _ directory: URL,
                               section: String = "morse", compact: Bool = false) async throws -> String {
        let suite = "com.morsecore.mac.smoke.\(UUID().uuidString)"
        defer { UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite) }
        let settings = AppSettings(suiteName: suite)
        settings.language = language; settings.theme = dark ? .dark : .light
        settings.reduceMotion = true; settings.checkUpdates = false
        settings.collapsed = compact
        settings.audioSectionRaw = section; settings.audioText = "SOS TEST"
        let view = ContentView(settings: settings, route: page).frame(width: 1340, height: 840)
            .environment(\.colorScheme, dark ? .dark : .light)
        let hosting = NSHostingView(rootView: view)
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1340, height: 840),
                              styleMask: [.borderless], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.appearance = NSAppearance(named: dark ? .darkAqua : .aqua)
        window.contentView = hosting; hosting.frame = NSRect(x: 0, y: 0, width: 1340, height: 840)
        hosting.layoutSubtreeIfNeeded()
        try await Task.sleep(for: .milliseconds(page == .audio || page.radio ? 500 : 150))
        hosting.layoutSubtreeIfNeeded(); hosting.displayIfNeeded()
        guard let bitmap = hosting.bitmapImageRepForCachingDisplay(in: hosting.bounds) else { throw CocoaError(.fileWriteUnknown) }
        hosting.cacheDisplay(in: hosting.bounds, to: bitmap)
        guard let png = bitmap.representation(using: .png, properties: [:]) else { throw CocoaError(.fileWriteUnknown) }
        let suffix = (page == .audio ? "-" + section : "") + (compact ? "-compact" : "")
        let name = "\(language.rawValue)-\(dark ? "dark" : "light")-\(page.rawValue)\(suffix).png"
        try png.write(to: directory.appendingPathComponent(name), options: .atomic)
        window.contentView = nil; window.close()
        return name
    }

    private static func seed() async throws {
        let stationID = "11111111111111111111111111111111"
        _ = try await MacServices.shared.call("profiles.save", .object(["document": .object([
            "schema": .number(1), "activeId": .string(stationID), "profiles": .array([.object([
                "id": .string(stationID), "label": .string("Test station"), "callSign": .string("TA1AAA"),
                "countryId": .string("TR"), "classId": .string("A"), "operatorName": .string("TEST"), "qth": .string("TEST")])])])]))
        _ = try await MacServices.shared.call("qso.append", .object(["entry": .object([
            "id": .string("22222222222222222222222222222222"), "schema": .number(1),
            "utc": .string("2026-10-16T12:00:00Z"), "ownCall": .string("TA1AAA"), "otherCall": .string("DL1ABC"),
            "countryId": .string("DE"), "band": .string("20m"), "frequencyHz": .number(14_060_000),
            "sentRst": .string("599"), "receivedRst": .string("599"), "practice": .bool(true),
            "note": .string("Synthetic render fixture"), "gridSquare": .string("JO62")])]))
    }
    #endif
}
