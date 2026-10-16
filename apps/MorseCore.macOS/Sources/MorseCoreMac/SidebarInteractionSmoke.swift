import AppKit
import SwiftUI

#if DEBUG
/// Drives only this process's isolated hosting window. No system accessibility permission is needed.
@MainActor
enum SidebarInteractionSmoke {
    static func run(directory: URL) async throws {
        let suite = "com.morsecore.mac.navigation-test.\(UUID().uuidString)"
        defer { UserDefaults(suiteName: suite)?.removePersistentDomain(forName: suite) }
        let settings = AppSettings(suiteName: suite)
        settings.language = .tr; settings.reduceMotion = true; settings.checkUpdates = false
        let hosting = NSHostingView(rootView: ContentView(settings: settings).frame(width: 1340, height: 840))
        let window = NSWindow(contentRect: NSRect(x: 50, y: 50, width: 1340, height: 840),
                              styleMask: [.borderless], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false; window.contentView = hosting
        defer { window.contentView = nil; window.close() }
        window.makeKeyAndOrderFront(nil)
        hosting.layoutSubtreeIfNeeded()
        try await Task.sleep(for: .milliseconds(200))
        var checks: [String] = []
        for (identifier, key) in [("Nav-files", "files"), ("Nav-audio", "audio"), ("Nav-converter", "converter")] {
            try press(identifier, root: hosting)
            try await Task.sleep(for: .milliseconds(180))
            hosting.layoutSubtreeIfNeeded()
            guard let heading = find("WorkspaceTitle", root: hosting),
                  string(heading, "accessibilityValue") == settings.text(key) || string(heading, "accessibilityLabel") == settings.text(key) else {
                throw ServiceFailure(message: "Navigation did not show \(key).")
            }
            checks.append(identifier)
        }
        try press("Nav-education", root: hosting)
        try await Task.sleep(for: .milliseconds(150)); hosting.layoutSubtreeIfNeeded()
        try press("Nav-alphabet", root: hosting)
        checks.append("Education child button")
        try await Task.sleep(for: .milliseconds(150)); hosting.layoutSubtreeIfNeeded()
        try press("Nav-practice", root: hosting)
        try await Task.sleep(for: .milliseconds(150)); hosting.layoutSubtreeIfNeeded()
        try press("PracticeStart", root: hosting)
        try await Task.sleep(for: .milliseconds(180)); hosting.layoutSubtreeIfNeeded()
        guard let signal = find("PracticeSignal", root: hosting) else {
            throw ServiceFailure(message: "Practice did not show a signal.")
        }
        let value = string(signal, "accessibilityValue") + string(signal, "accessibilityLabel")
        guard value.contains(".") || value.contains("-") else {
            throw ServiceFailure(message: "Practice did not produce Morse.")
        }
        checks.append("Practice start produces Morse")
        try press("PracticeSkip", root: hosting)
        try await Task.sleep(for: .milliseconds(180)); hosting.layoutSubtreeIfNeeded()
        guard let feedback = find("PracticeFeedback", root: hosting),
              (string(feedback, "accessibilityValue") + string(feedback, "accessibilityLabel")).contains(settings.text("skipped")) else {
            throw ServiceFailure(message: "Practice skip did not show feedback.")
        }
        checks.append("Practice skip advances with feedback")
        try press("PaneToggle", root: hosting)
        try await Task.sleep(for: .milliseconds(180)); hosting.layoutSubtreeIfNeeded()
        guard settings.collapsed else { throw ServiceFailure(message: "Sidebar did not collapse.") }
        checks.append("Collapse")
        try press("PaneToggle", root: hosting)
        try await Task.sleep(for: .milliseconds(180)); hosting.layoutSubtreeIfNeeded()
        guard !settings.collapsed else { throw ServiceFailure(message: "Sidebar did not expand.") }
        checks.append("Expand")
        let data = try JSONSerialization.data(withJSONObject: ["passed": checks, "scope": "In-process AppKit accessibility press actions on isolated SwiftUI window"], options: [.prettyPrinted, .sortedKeys])
        try data.write(to: directory.appendingPathComponent("sidebar-interaction.json"), options: .atomic)
    }

    private static func press(_ identifier: String, root: NSObject) throws {
        let selector = NSSelectorFromString("accessibilityPerformPress")
        guard let element = find(identifier, root: root), element.responds(to: selector) else {
            throw ServiceFailure(message: "Missing accessible button: \(identifier)")
        }
        typealias Press = @convention(c) (AnyObject, Selector) -> ObjCBool
        let action = unsafeBitCast(element.method(for: selector), to: Press.self)
        guard action(element, selector).boolValue else { throw ServiceFailure(message: "Press failed: \(identifier)") }
    }

    private static func find(_ identifier: String, root: NSObject, depth: Int = 0) -> NSObject? {
        guard depth < 40 else { return nil }
        if string(root, "accessibilityIdentifier") == identifier { return root }
        let selector = NSSelectorFromString("accessibilityChildren")
        guard root.responds(to: selector), let children = root.perform(selector)?.takeUnretainedValue() as? [NSObject] else { return nil }
        for child in children { if let found = find(identifier, root: child, depth: depth + 1) { return found } }
        return nil
    }

    private static func string(_ element: NSObject, _ name: String) -> String {
        let selector = NSSelectorFromString(name)
        guard element.responds(to: selector) else { return "" }
        return element.perform(selector)?.takeUnretainedValue() as? String ?? ""
    }
}
#endif
