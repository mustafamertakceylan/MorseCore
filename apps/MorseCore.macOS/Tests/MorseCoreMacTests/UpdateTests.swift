import Foundation
import Testing
@testable import MorseCoreMac

@Test func updateAdapterPreservesSharedReleasePolicyResult() throws {
    let fixture = ServiceJSON.object([
        "found": .bool(true), "version": .string("0.7.5"), "newer": .bool(false),
        "page": .string("https://github.com/mustafamertakceylan/MorseCore/releases/tag/v0.7.5"),
        "notes": .string("Same release on both platforms"),
        "installer": .object(["name": .string("MorseCore-0.7.5-macOS-arm64.dmg")]),
        "checksums": .object(["name": .string("SHA256SUMS")])
    ])
    let release = try MacRelease(fixture)
    #expect(!release.newer)
    #expect(release.version == "0.7.5")
    #expect(release.installer?.name == "MorseCore-0.7.5-macOS-arm64.dmg")
    #expect(release.checksums?.name == "SHA256SUMS")
    #expect(throws: (any Error).self) { try MacRelease(.object(["found": .bool(false)])) }
}

@Test func updateOpeningRechecksDownloadedPackageIntegrity() throws {
    let directory = FileManager.default.temporaryDirectory.appendingPathComponent("MorseCore-update-test-" + UUID().uuidString)
    try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    defer { try? FileManager.default.removeItem(at: directory) }
    let path = directory.appendingPathComponent("fixture.dmg")
    try Data("abc".utf8).write(to: path)
    let update = MacVerifiedUpdate(url: path, sha256: "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")
    try update.verify()
    try Data("modified".utf8).write(to: path)
    #expect(throws: (any Error).self) { try update.verify() }
}
