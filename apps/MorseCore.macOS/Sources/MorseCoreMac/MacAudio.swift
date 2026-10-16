import Foundation
import AVFoundation
import Observation
import MorseCoreBridge

enum MacAudioError: Error { case microphoneDenied, recordingFailed, playbackFailed }

struct MacAudioInput: Identifiable, Sendable { let id: String; let name: String }

/// AVCapture sessions run exclusively on their queue, never on the UI actor.
private final class InputCapture: NSObject, AVCaptureFileOutputRecordingDelegate, @unchecked Sendable {
    private let queue = DispatchQueue(label: "MorseCore.microphone")
    private let session = AVCaptureSession()
    private let output = AVCaptureAudioFileOutput()
    private let url: URL
    private let stopped: @Sendable () -> Void
    private var completed = false
    private var cancelled = false
    private var failure: Error?
    private var waiter: CheckedContinuation<URL, Error>?

    init(url: URL, stopped: @escaping @Sendable () -> Void) { self.url = url; self.stopped = stopped }

    func start(deviceID: String) async throws {
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            queue.async { [self] in
                do {
                    let device = deviceID.isEmpty ? AVCaptureDevice.default(for: .audio) : AVCaptureDevice(uniqueID: deviceID)
                    guard let device else { throw MacAudioError.recordingFailed }
                    let input = try AVCaptureDeviceInput(device: device)
                    session.beginConfiguration()
                    guard session.canAddInput(input), session.canAddOutput(output), AVCaptureAudioFileOutput.availableOutputFileTypes().contains(.wav) else {
                        session.commitConfiguration(); throw MacAudioError.recordingFailed
                    }
                    session.addInput(input); session.addOutput(output)
                    output.audioSettings = [AVFormatIDKey: kAudioFormatLinearPCM,
                        AVSampleRateKey: 16000.0, AVNumberOfChannelsKey: 1,
                        AVLinearPCMBitDepthKey: 16, AVLinearPCMIsFloatKey: false,
                        AVLinearPCMIsBigEndianKey: false]
                    output.maxRecordedDuration = CMTime(seconds: 120, preferredTimescale: 1000)
                    session.commitConfiguration(); session.startRunning()
                    output.startRecording(to: url, outputFileType: .wav, recordingDelegate: self)
                    continuation.resume()
                } catch { session.stopRunning(); continuation.resume(throwing: error) }
            }
        }
    }

    func finish() async throws -> URL {
        try await withCheckedThrowingContinuation { continuation in
            queue.async { [self] in
                if completed {
                    if let failure { continuation.resume(throwing: failure) }
                    else if cancelled { continuation.resume(throwing: CancellationError()) }
                    else { continuation.resume(returning: url) }
                } else if waiter != nil { continuation.resume(throwing: MacAudioError.recordingFailed) }
                else { waiter = continuation; output.stopRecording() }
            }
        }
    }

    func cancel() {
        queue.async { [self] in
            cancelled = true
            if completed { try? FileManager.default.removeItem(at: url) }
            else { output.stopRecording() }
        }
    }

    func fileOutput(_ output: AVCaptureFileOutput, didFinishRecordingTo outputFileURL: URL,
                    from connections: [AVCaptureConnection], error: Error?) {
        // Session state and continuations are serialized even if AVFoundation changes callback queues.
        let successful = (error as NSError?)?.userInfo[AVErrorRecordingSuccessfullyFinishedKey] as? Bool == true
        queue.async { [self] in
            completed = true; failure = successful ? nil : error
            session.stopRunning()
            if cancelled || failure != nil { try? FileManager.default.removeItem(at: url) }
            if let waiter {
                self.waiter = nil
                if cancelled { waiter.resume(throwing: CancellationError()) }
                else if let failure { waiter.resume(throwing: failure) }
                else { waiter.resume(returning: url) }
            }
            stopped()
        }
    }
}

/// One player and recorder for all workspaces; temporary media is owned here.
@MainActor @Observable
final class MacAudio {
    static let shared = MacAudio()
    private(set) var isPlaying = false
    private(set) var isRecording = false
    @ObservationIgnored private var player: AVAudioPlayer?
    @ObservationIgnored private var recorder: InputCapture?
    @ObservationIgnored private var playbackURL: URL?
    @ObservationIgnored private var recordingID: UUID?
    @ObservationIgnored private var monitor: Task<Void, Never>?
    @ObservationIgnored private var toneEngine: AVAudioEngine?
    @ObservationIgnored private var toneNode: AVAudioPlayerNode?
    @ObservationIgnored private var toneFrequency = 0
    @ObservationIgnored private var generation = UUID()

    func play(morse: String, wpm: Int = 20, frequency: Int = 600, separator: UInt32 = 47) async throws {
        stop()
        let generation = self.generation
        let url = Self.temporaryWAV()
        do {
            try await Task.detached {
                try CoreAudio.writeWAV(url: url, morse: morse, separator: separator,
                                       wpm: UInt32(clamping: wpm), frequency: UInt32(clamping: frequency))
            }.value
            try Task.checkCancellation()
            guard self.generation == generation else { throw CancellationError() }
            try play(url: url, temporary: true)
        } catch { try? FileManager.default.removeItem(at: url); throw error }
    }

    func startTone(frequency: Int = 600) throws {
        stop()
        let frequency = min(2000, max(200, frequency))
        if toneFrequency == frequency, let toneEngine, let toneNode {
            try toneEngine.start(); toneNode.play(); return
        }
        let engine = AVAudioEngine(), node = AVAudioPlayerNode()
        guard let format = AVAudioFormat(standardFormatWithSampleRate: 48000, channels: 1),
              let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: 48000),
              let samples = buffer.floatChannelData?[0] else { throw MacAudioError.playbackFailed }
        buffer.frameLength = 48000
        for index in 0..<48000 {
            samples[index] = Float(sin(Double(index) * 2 * .pi * Double(frequency) / 48000)) * 0.18
        }
        engine.attach(node); engine.connect(node, to: engine.mainMixerNode, format: format)
        node.scheduleBuffer(buffer, at: nil, options: .loops)
        try engine.start(); node.play()
        toneEngine = engine; toneNode = node; toneFrequency = frequency
    }

    func stopTone() {
        // Keep the prepared loop for straight-key presses; pause rendering while released.
        toneNode?.pause(); toneEngine?.pause()
    }

    static func temporaryWAV() -> URL {
        FileManager.default.temporaryDirectory.appendingPathComponent("MorseCore-\(UUID().uuidString).wav")
    }

    func play(url: URL, temporary: Bool = false) throws {
        stop()
        let next: AVAudioPlayer
        do {
            next = try AVAudioPlayer(contentsOf: url)
            guard next.prepareToPlay(), next.play() else { throw MacAudioError.playbackFailed }
        } catch {
            if temporary { try? FileManager.default.removeItem(at: url) }
            throw error
        }
        player = next
        playbackURL = temporary ? url : nil
        isPlaying = true
        monitor = Task { [weak self] in
            while !Task.isCancelled {
                try? await Task.sleep(for: .milliseconds(200))
                guard let self else { return }
                if self.player?.isPlaying != true { self.stop(); return }
            }
        }
    }

    func stop() {
        generation = UUID()
        stopTone()
        monitor?.cancel(); monitor = nil
        player?.stop(); player = nil; isPlaying = false
        if let playbackURL { try? FileManager.default.removeItem(at: playbackURL) }
        playbackURL = nil
        recorder?.cancel(); recorder = nil; isRecording = false; recordingID = nil
    }

    static func inputs() -> [MacAudioInput] {
        AVCaptureDevice.DiscoverySession(deviceTypes: [.microphone, .external], mediaType: .audio, position: .unspecified)
            .devices.map { MacAudioInput(id: $0.uniqueID, name: $0.localizedName) }
    }

    func startRecording(deviceID: String = "") async throws {
        stop()
        let generation = self.generation
        let granted = await AVCaptureDevice.requestAccess(for: .audio)
        try Task.checkCancellation()
        guard self.generation == generation else { throw CancellationError() }
        guard granted else { throw MacAudioError.microphoneDenied }
        let url = Self.temporaryWAV()
        let identifier = UUID()
        let next = InputCapture(url: url) { [weak self] in
            Task { @MainActor in
                guard let self, self.recordingID == identifier else { return }
                self.isRecording = false
            }
        }
        // Register the pending capture before awaiting, so stop() can cancel startup too.
        recorder = next; recordingID = identifier
        do {
            try await next.start(deviceID: deviceID)
            try Task.checkCancellation()
            guard self.generation == generation, recordingID == identifier else { throw CancellationError() }
            isRecording = true
        } catch {
            next.cancel()
            if recordingID == identifier { recorder = nil; recordingID = nil; isRecording = false }
            throw error
        }
    }

    /// Transfers ownership of the captured WAV to the caller.
    func finishRecording() async throws -> URL? {
        guard let recorder else { return nil }
        let identifier = recordingID
        defer {
            if recordingID == identifier { self.recorder = nil; recordingID = nil; isRecording = false }
        }
        return try await withTaskCancellationHandler {
            let url = try await recorder.finish()
            try Task.checkCancellation()
            return url
        } onCancel: { recorder.cancel() }
    }
}
