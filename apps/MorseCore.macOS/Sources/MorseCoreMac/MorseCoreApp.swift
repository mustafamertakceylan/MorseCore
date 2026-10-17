import SwiftUI
import CoreText
import AppKit
import MorseCoreBridge
import Darwin

@main
struct MorseCoreApp: App {
    @State private var settings = AppSettings()

    init() {
        if CommandLine.arguments.contains("--verify-packaged-resources") {
            do {
                let proof = try MacResources.verifyResources()
                let data = try JSONSerialization.data(withJSONObject: proof, options: [.sortedKeys])
                print(String(decoding: data, as: UTF8.self))
                exit(0)
            } catch {
                FileHandle.standardError.write(Data("Packaged resources could not be decoded: \(error)\n".utf8))
                exit(1)
            }
        }
        for name in ["AtkinsonHyperlegible-Regular", "CrimsonPro"] {
            if let url = MacResources.bundle.url(forResource: name, withExtension: "ttf", subdirectory: "Resources/Fonts") {
                CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil)
            }
        }
    }

    var body: some Scene {
        Window("MorseCore", id: "main") {
            MacLaunchView(settings: settings)
                .frame(width: 1340, height: 840)
                .preferredColorScheme(settings.theme.colorScheme)
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1340, height: 840)
        .windowResizability(.contentSize)
        .defaultPosition(.center)
        .commands {
            CommandGroup(replacing: .newItem) { }
        }
    }
}

@MainActor
enum PackageReadiness {
    static func isDirectChild(_ output: URL, of root: URL) -> Bool {
        // Resolve existing directories, never the not-yet-created marker leaf.
        // Foundation can preserve /var for one and /private/var for the other.
        guard let parentPath = realpath(output.deletingLastPathComponent().path, nil) else { return false }
        defer { free(parentPath) }
        guard let rootPath = realpath(root.path, nil) else { return false }
        defer { free(rootPath) }
        return String(cString: parentPath) == String(cString: rootPath)
    }

    static func writeIfRequested() async {
        let environment = ProcessInfo.processInfo.environment
        guard let path = environment["MORSECORE_PACKAGE_READY_FILE"],
              environment["MORSECORE_DATA_ROOT"] != nil,
              environment["MORSECORE_SETTINGS_SUITE"]?.hasPrefix("com.morsecore.mac.package-test.") == true else { return }
        let output = URL(fileURLWithPath: path).standardizedFileURL.resolvingSymlinksInPath()
        let root = MacPaths.dataRoot.standardizedFileURL.resolvingSymlinksInPath()
        let diagnostic = root.appendingPathComponent("package-readiness-diagnostic.json")
        func record(_ phase: String, _ error: String = "") {
            let proof: [String: Any] = ["phase": phase, "error": error, "root": root.path, "output": output.path,
                "windows": NSApp.windows.map { ["visible": $0.isVisible, "width": $0.frame.width, "hasContent": $0.contentView != nil] as [String: Any] }]
            if let data = try? JSONSerialization.data(withJSONObject: proof, options: [.sortedKeys]) { try? data.write(to: diagnostic, options: .atomic) }
        }
        record("entered")
        guard isDirectChild(output, of: root) else { record("invalid-output"); return }
        do {
            // Wait past the splash, then require a real visible app window.
            try await Task.sleep(for: .seconds(3))
            for _ in 0..<50 {
                if NSApp.windows.contains(where: { $0.isVisible && $0.contentView != nil && $0.frame.width >= 500 }) {
                    var proof = try MacResources.verifyResources()
                    proof["version"] = CoreConverter.version
                    proof["pid"] = ProcessInfo.processInfo.processIdentifier
                    proof["visibleWindow"] = true
                    let data = try JSONSerialization.data(withJSONObject: proof, options: [.sortedKeys])
                    try data.write(to: output, options: .atomic)
                    record("ready")
                    return
                }
                try await Task.sleep(for: .milliseconds(100))
            }
            record("no-visible-window")
        } catch {
            // Only an explicitly isolated QA launch asks for this marker.
            // Failure leaves it absent so the package check fails.
            record("failed", String(describing: error))
        }
    }
}
