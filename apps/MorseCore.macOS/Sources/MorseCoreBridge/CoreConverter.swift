import Foundation
import CMorseCore

public enum Alphabet: UInt32, CaseIterable, Sendable {
    case latin = 0, russian = 1, arabic = 2
}

public enum ConversionDirection: Sendable { case encode, decode }

public struct ConversionRequest: Sendable {
    public let text: String
    public let direction: ConversionDirection
    public let alphabet: Alphabet
    public let separator: UInt32

    public init(text: String, direction: ConversionDirection, alphabet: Alphabet, separator: UInt32) {
        self.text = text
        self.direction = direction
        self.alphabet = alphabet
        self.separator = separator
    }
}

public struct ConversionStatistics: Sendable {
    public let inputCharacters: UInt64
    public let outputCharacters: UInt64
    public let words: UInt64
    public let convertedCharacters: UInt64
    public let spaces: UInt64
}

public struct ConversionOutput: Sendable {
    public let text: String
    public let statistics: ConversionStatistics
}

public struct NativeCoreError: Error, Sendable {
    public let status: Int32
}

public enum CoreConverter {
    public static var version: String { String(cString: mc_version()) }

    /// Native handles are owned and destroyed on this call's worker thread.
    /// Nothing backed by a C allocation escapes into SwiftUI.
    public static func convert(_ request: ConversionRequest) throws -> ConversionOutput {
        guard mc_abi_version() == 1 else { throw NativeCoreError(status: -102) }
        guard request.text.utf16.count <= 200_000 else { throw NativeCoreError(status: -6) }
        let bytes = Array(request.text.utf8)
        var handle: OpaquePointer?
        let status = bytes.withUnsafeBufferPointer { input in
            switch request.direction {
            case .encode:
                mc_encode_alphabet(input.baseAddress, UInt64(input.count), request.separator,
                                   request.alphabet.rawValue, &handle)
            case .decode:
                mc_decode_alphabet(input.baseAddress, UInt64(input.count), request.separator,
                                   request.alphabet.rawValue, &handle)
            }
        }
        defer { if let handle { mc_result_destroy(handle) } }
        guard status == 0, let handle else { throw NativeCoreError(status: status == 0 ? -102 : status) }
        var required: UInt64 = 0
        let query = mc_result_copy_text(handle, nil, 0, &required)
        guard query == -101, required > 0, required <= UInt64(Int.max) else {
            throw NativeCoreError(status: query == -101 ? -102 : query)
        }
        var buffer = [CChar](repeating: 0, count: Int(required))
        let copyStatus = buffer.withUnsafeMutableBufferPointer {
            mc_result_copy_text(handle, $0.baseAddress, UInt64($0.count), &required)
        }
        guard copyStatus == 0 else { throw NativeCoreError(status: copyStatus) }
        var stats = mc_statistics()
        let statsStatus = mc_result_statistics(handle, &stats, UInt32(MemoryLayout<mc_statistics>.size))
        guard statsStatus == 0 else { throw NativeCoreError(status: statsStatus) }
        let text = String(decoding: buffer.dropLast().map { UInt8(bitPattern: $0) }, as: UTF8.self)
        return ConversionOutput(text: text, statistics: ConversionStatistics(
            inputCharacters: stats.input_characters, outputCharacters: stats.output_characters,
            words: stats.words, convertedCharacters: stats.converted_characters, spaces: stats.spaces))
    }
}
