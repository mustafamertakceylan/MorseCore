import Foundation
import CMorseCore

public struct CoreAudioAnalysis: Sendable {
    public let samples: UInt64
    public let units: UInt64
    public let tones: UInt64
    public let duration: Double
}

public enum CoreAudio {
    private static func options(wpm: UInt32, frequency: UInt32) throws -> mc_audio_options {
        var options = mc_audio_options()
        let status = mc_audio_defaults(&options, UInt32(MemoryLayout<mc_audio_options>.size))
        guard status == 0 else { throw NativeCoreError(status: status) }
        options.wpm = wpm
        options.frequency_hz = frequency
        return options
    }

    public static func analyze(morse: String, separator: UInt32 = 47, wpm: UInt32 = 20,
                               frequency: UInt32 = 700) throws -> CoreAudioAnalysis {
        guard morse.utf8.count <= 2_000_000 else { throw NativeCoreError(status: -6) }
        var options = try options(wpm: wpm, frequency: frequency)
        var result = mc_audio_analysis()
        let status = Array(morse.utf8).withUnsafeBufferPointer {
            mc_audio_analyze($0.baseAddress, UInt64($0.count), separator, &options,
                             UInt32(MemoryLayout<mc_audio_options>.size), &result,
                             UInt32(MemoryLayout<mc_audio_analysis>.size))
        }
        guard status == 0 else { throw NativeCoreError(status: status) }
        return CoreAudioAnalysis(samples: result.samples, units: result.units, tones: result.tones,
                                 duration: Double(result.samples) / Double(options.sample_rate))
    }

    /// Call on a worker: file writing is synchronous in the native ABI.
    public static func writeWAV(url: URL, morse: String, separator: UInt32 = 47,
                                wpm: UInt32 = 20, frequency: UInt32 = 700) throws {
        guard url.isFileURL, !url.path.contains("\0") else { throw NativeCoreError(status: -100) }
        guard morse.utf8.count <= 2_000_000 else { throw NativeCoreError(status: -6) }
        var options = try options(wpm: wpm, frequency: frequency)
        let status = url.path.withCString { path in
            Array(morse.utf8).withUnsafeBufferPointer {
                mc_write_wav(path, $0.baseAddress, UInt64($0.count), separator, &options,
                             UInt32(MemoryLayout<mc_audio_options>.size))
            }
        }
        guard status == 0 else { throw NativeCoreError(status: status) }
    }
}
