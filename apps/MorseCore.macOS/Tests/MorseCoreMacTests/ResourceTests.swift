import Foundation
import Testing
@testable import MorseCoreMac

@Test func resourceAdapterDecodesSharedResources() throws {
    let proof = try MacResources.verifyResources()
    #expect(proof["ok"] as? Bool == true)
    #expect((proof["languages"] as? [String])?.count == 5)
    #expect((proof["fonts"] as? [String])?.count == 2)
    #expect(proof["logoDecoded"] as? Bool == true)
}

@Test func installedResourceLookupUsesRelocatedApp() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent("MorseCore-resource-test-" + UUID().uuidString)
    defer { try? FileManager.default.removeItem(at: root) }
    let app = root.appendingPathComponent("Relocated.app")
    let resources = app.appendingPathComponent("Contents/Resources")
    try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
    let info: [String: Any] = ["CFBundleIdentifier": "com.morsecore.resource-test", "CFBundlePackageType": "APPL", "CFBundleExecutable": "Probe"]
    try PropertyListSerialization.data(fromPropertyList: info, format: .xml, options: 0)
        .write(to: app.appendingPathComponent("Contents/Info.plist"))
    let relocated = resources.appendingPathComponent(MacResources.name + ".bundle")
    try FileManager.default.copyItem(at: MacResources.bundle.bundleURL, to: relocated)
    let application = try #require(Bundle(url: app))
    let found = try #require(MacResources.packaged(in: application))
    #expect(found.bundleURL.standardizedFileURL == relocated.standardizedFileURL)
    #expect(found.url(forResource: "tr", withExtension: "json", subdirectory: "Resources/Strings") != nil)
}

@MainActor @Test func packageReadinessAcceptsOnlyItsOwnExistingDirectory() throws {
    let root = FileManager.default.temporaryDirectory.appendingPathComponent("MorseCore-ready-test-" + UUID().uuidString, isDirectory: true)
    defer { try? FileManager.default.removeItem(at: root) }
    try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
    let alias = root.appendingPathComponent("alias", isDirectory: true)
    try FileManager.default.createSymbolicLink(at: alias, withDestinationURL: root)
    #expect(PackageReadiness.isDirectChild(alias.appendingPathComponent("not-created.json"), of: root))
    #expect(!PackageReadiness.isDirectChild(root.deletingLastPathComponent().appendingPathComponent("outside.json"), of: root))
}
