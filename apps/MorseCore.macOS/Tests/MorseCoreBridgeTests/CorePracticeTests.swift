import Testing
@testable import MorseCoreBridge

@Test func practiceLifecycleAndFeedback() async throws {
    let practice = CorePractice()
    try await practice.start(mode: 0, questions: 3, seed: 123)
    let first = try await practice.next()
    let decoded = try CoreConverter.convert(.init(text: first, direction: .decode, alphabet: .latin, separator: 47)).text
    let correct = try await practice.submit(decoded)
    #expect(correct.verdict == 1)
    #expect(correct.expectedAnswer == decoded)
    _ = try await practice.next()
    let incorrect = try await practice.submit("WRONG")
    #expect(incorrect.verdict == 0)
    #expect(!incorrect.expectedAnswer.isEmpty)
    _ = try await practice.next()
    #expect(try await practice.submit("").verdict == 2)
    let stats = try await practice.statistics()
    #expect(stats.total == 3 && stats.answered == 3)
    #expect(stats.correct == 1 && stats.incorrect == 1 && stats.skipped == 1)
    do { _ = try await practice.next(); Issue.record("Completed practice accepted a question") }
    catch let error as NativeCoreError { #expect(error.status == -102) }
}

@Test func practiceRestartAndIndependentSessions() async throws {
    let a = CorePractice(), b = CorePractice()
    try await a.start(mode: 1, questions: 2, seed: 42)
    try await b.start(mode: 1, questions: 2, seed: 42)
    #expect(try await a.next() == b.next())
    #expect(try await a.submit("").verdict == 2)
    #expect(try await b.statistics().answered == 0)
    try await a.start(mode: 2, questions: 1, seed: 88)
    #expect(try await a.statistics().answered == 0)
    #expect(!(try await a.next()).isEmpty)
}

@Test func alphabetProfilesHaveUniqueScalarsAndPlayableCodes() throws {
    for alphabet in Alphabet.allCases {
        let symbols = try CoreAlphabet.symbols(alphabet)
        #expect(symbols.count > 30)
        #expect(Set(symbols.map(\.scalar)).count == symbols.count)
        #expect(symbols.allSatisfy { !$0.character.isEmpty && !$0.morse.isEmpty && $0.morse.allSatisfy { $0 == "." || $0 == "-" } })
    }
    #expect(try CoreAlphabet.symbols(.russian).contains { $0.character == "Я" })
    #expect(try CoreAlphabet.symbols(.arabic).contains { $0.character == "س" })
}
