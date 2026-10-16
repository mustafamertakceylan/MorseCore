import Foundation
import Testing
@testable import MorseCoreBridge

@Test func audioAnalysisAndWaveHeader() throws {
    let morse = try CoreConverter.convert(.init(text: "SOS TEST", direction: .encode, alphabet: .latin, separator: 47)).text
    let analysis = try CoreAudio.analyze(morse: morse)
    #expect(analysis.tones > 0)
    #expect(analysis.samples > 0)
    #expect(analysis.duration > 0)
    let directory = FileManager.default.temporaryDirectory.appendingPathComponent("MorseCore-audio-test-\(UUID().uuidString)", isDirectory: true)
    try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
    defer { try? FileManager.default.removeItem(at: directory) }
    let url = directory.appendingPathComponent("synthetic.wav")
    try CoreAudio.writeWAV(url: url, morse: morse)
    let bytes = try Data(contentsOf: url)
    #expect(bytes.count > 44)
    #expect(String(decoding: bytes.prefix(4), as: UTF8.self) == "RIFF")
    #expect(String(decoding: bytes[8..<12], as: UTF8.self) == "WAVE")
    #expect(UInt64(bytes.count - 44) == analysis.samples * 2)
}

@Test func audioValidationAndSeparator() throws {
    let slash = try CoreAudio.analyze(morse: ".- / -...")
    let pipe = try CoreAudio.analyze(morse: ".- | -...", separator: 124)
    #expect(slash.samples == pipe.samples)
    #expect(throws: NativeCoreError.self) { try CoreAudio.analyze(morse: "not morse") }
    #expect(throws: NativeCoreError.self) { try CoreAudio.analyze(morse: ".-", wpm: 0) }
    #expect(throws: NativeCoreError.self) { try CoreAudio.analyze(morse: String(repeating: ".", count: 65_537)) }
}
