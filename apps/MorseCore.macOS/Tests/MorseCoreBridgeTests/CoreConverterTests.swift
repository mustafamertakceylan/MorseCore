import Testing
@testable import MorseCoreBridge

private func encode(_ text: String, _ alphabet: Alphabet = .latin, separator: UInt32 = 47) throws -> ConversionOutput {
    try CoreConverter.convert(.init(text: text, direction: .encode, alphabet: alphabet, separator: separator))
}

private func decode(_ text: String, _ alphabet: Alphabet = .latin, separator: UInt32 = 47) throws -> ConversionOutput {
    try CoreConverter.convert(.init(text: text, direction: .decode, alphabet: alphabet, separator: separator))
}

@Test func latinAndStatistics() throws {
    let result = try encode("SOS TEST")
    #expect(result.text == "... --- ... / - . ... - ")
    #expect(result.statistics.inputCharacters == 8)
    #expect(result.statistics.words == 2)
    #expect(result.statistics.convertedCharacters == 7)
    #expect(result.statistics.spaces == 1)
    #expect(try decode(result.text).text == "SOS TEST")
}

@Test func unicodeProfiles() throws {
    let russian = try encode("Привет, мир!", .russian)
    #expect(russian.statistics.inputCharacters == 12)
    #expect(try decode(russian.text, .russian).text == "ПРИВЕТ, МИР!")
    #expect(try decode(encode("سَلَامٌ  عالم", .arabic).text, .arabic).text == "سلام عالم")
    #expect(try decode(encode("İĞÜŞÖÇ").text).text == "IGUSOC")
}

@Test func separatorAndErrors() throws {
    #expect(try encode("A B", separator: 124).text == ".- | -... ")
    #expect(try decode(".- | -... ", separator: 124).text == "A B")
    let cases: [(String, Int32)] = [("", -1), ("😀", -3), ("a\0b", -2), (String(repeating: "A", count: 200_001), -6)]
    for (input, expected) in cases {
        do { _ = try encode(input); Issue.record("Expected native error \(expected)") }
        catch let error as NativeCoreError { #expect(error.status == expected) }
    }
    #expect(throws: NativeCoreError.self) { try decode(".-.-.-.-") }
}

@Test func independentConcurrentCalls() async throws {
    try await withThrowingTaskGroup(of: String.self) { group in
        for i in 0..<24 {
            group.addTask {
                let text = i.isMultiple(of: 2) ? "МИР МИР" : "سلام عالم"
                let alphabet: Alphabet = i.isMultiple(of: 2) ? .russian : .arabic
                return try decode(encode(text, alphabet).text, alphabet).text
            }
        }
        var count = 0
        for try await text in group {
            #expect(text == "МИР МИР" || text == "سلام عالم")
            count += 1
        }
        #expect(count == 24)
    }
}
