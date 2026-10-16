import Foundation
import CMorseCore

public struct AlphabetSymbol: Sendable, Identifiable {
    public let scalar: UInt32
    public let character: String
    public let morse: String
    public let isExtension: Bool
    public var id: UInt32 { scalar }
}

public enum CoreAlphabet {
    public static func symbols(_ alphabet: Alphabet) throws -> [AlphabetSymbol] {
        let count = mc_alphabet_profile_count(alphabet.rawValue)
        guard count > 0, count < 1024 else { throw NativeCoreError(status: -102) }
        return try (0..<count).map { index in
            var symbol = mc_symbol()
            let status = mc_alphabet_profile_get(alphabet.rawValue, index, &symbol, UInt32(MemoryLayout<mc_symbol>.size))
            guard status == 0, let scalar = Unicode.Scalar(symbol.character) else { throw NativeCoreError(status: status == 0 ? -102 : status) }
            let code = withUnsafeBytes(of: symbol.code) { bytes in
                String(decoding: bytes.prefix { $0 != 0 }, as: UTF8.self)
            }
            return AlphabetSymbol(scalar: symbol.character, character: String(scalar), morse: code, isExtension: symbol.extension != 0)
        }
    }
}

public struct PracticeStatistics: Sendable {
    public let total: UInt32
    public let answered: UInt32
    public let correct: UInt32
    public let incorrect: UInt32
    public let skipped: UInt32
    public let accuracy: Double
}

public struct PracticeFeedback: Sendable {
    public let verdict: Int32
    public let questionNumber: UInt32
    public let expectedAnswer: String
}

// The box never escapes the actor; its destructor runs only after the actor's
// last operation. Native handles cannot race calls or move into a View.
private final class PracticeHandle: @unchecked Sendable {
    let pointer: OpaquePointer
    init(_ pointer: OpaquePointer) { self.pointer = pointer }
    deinit { mc_practice_destroy(pointer) }
}

public actor CorePractice {
    private var handle: PracticeHandle?
    public init() {}

    public func start(mode: UInt32, questions: UInt32 = 10, seed: UInt32) throws {
        var pointer: OpaquePointer?
        let status = mc_practice_create(mode, questions, seed, &pointer)
        guard status == 0, let pointer else { throw NativeCoreError(status: status == 0 ? -102 : status) }
        handle = PracticeHandle(pointer)
    }

    public func next() throws -> String {
        guard let handle else { throw NativeCoreError(status: -102) }
        var result: OpaquePointer?
        let status = mc_practice_next(handle.pointer, &result)
        defer { if let result { mc_result_destroy(result) } }
        guard status == 0, let result else { throw NativeCoreError(status: status == 0 ? -102 : status) }
        var count: UInt64 = 0
        guard mc_result_copy_text(result, nil, 0, &count) == -101, count > 0, count < 65_537 else { throw NativeCoreError(status: -102) }
        var bytes = [CChar](repeating: 0, count: Int(count))
        let capacity = count
        let copy = bytes.withUnsafeMutableBufferPointer { mc_result_copy_text(result, $0.baseAddress, capacity, &count) }
        guard copy == 0 else { throw NativeCoreError(status: copy) }
        return String(decoding: bytes.dropLast().map { UInt8(bitPattern: $0) }, as: UTF8.self)
    }

    public func submit(_ answer: String) throws -> PracticeFeedback {
        guard let handle else { throw NativeCoreError(status: -102) }
        guard answer.utf8.count <= 256 else { throw NativeCoreError(status: -6) }
        var feedback = mc_feedback()
        let bytes = Array(answer.utf8)
        let status = bytes.withUnsafeBufferPointer {
            mc_practice_submit(handle.pointer, $0.baseAddress, UInt64($0.count), &feedback, UInt32(MemoryLayout<mc_feedback>.size))
        }
        guard status == 0 else { throw NativeCoreError(status: status) }
        let expected = withUnsafeBytes(of: feedback.expected_answer) { String(decoding: $0.prefix { $0 != 0 }, as: UTF8.self) }
        return PracticeFeedback(verdict: feedback.verdict, questionNumber: feedback.question_number, expectedAnswer: expected)
    }

    public func statistics() throws -> PracticeStatistics {
        guard let handle else { throw NativeCoreError(status: -102) }
        var stats = mc_practice_statistics()
        let status = mc_practice_stats(handle.pointer, &stats, UInt32(MemoryLayout<mc_practice_statistics>.size))
        guard status == 0 else { throw NativeCoreError(status: status) }
        return PracticeStatistics(total: stats.total, answered: stats.answered, correct: stats.correct, incorrect: stats.incorrect, skipped: stats.skipped, accuracy: stats.accuracy)
    }
}
