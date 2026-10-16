import Foundation
import Darwin

enum ServiceJSON: Codable, Sendable, Equatable {
    case object([String: ServiceJSON]), array([ServiceJSON]), string(String), number(Double), bool(Bool), null

    init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() { self = .null }
        else if let value = try? container.decode(Bool.self) { self = .bool(value) }
        else if let value = try? container.decode(Double.self) { self = .number(value) }
        else if let value = try? container.decode(String.self) { self = .string(value) }
        else if let value = try? container.decode([ServiceJSON].self) { self = .array(value) }
        else { self = .object(try container.decode([String: ServiceJSON].self)) }
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .null: try container.encodeNil()
        case .bool(let value): try container.encode(value)
        case .number(let value): try container.encode(value)
        case .string(let value): try container.encode(value)
        case .array(let value): try container.encode(value)
        case .object(let value): try container.encode(value)
        }
    }

    subscript(_ key: String) -> ServiceJSON { if case .object(let value) = self { value[key] ?? .null } else { .null } }
    var stringValue: String { if case .string(let value) = self { value } else { "" } }
    var arrayValue: [ServiceJSON] { if case .array(let value) = self { value } else { [] } }
    var objectValue: [String: ServiceJSON] { if case .object(let value) = self { value } else { [:] } }
    var doubleValue: Double { if case .number(let value) = self { value } else { 0 } }
    var intValue: Int { doubleValue.isFinite && doubleValue >= Double(Int.min) && doubleValue < Double(Int.max) ? Int(doubleValue) : 0 }
    var boolValue: Bool { if case .bool(let value) = self { value } else { false } }
}

enum MacPaths {
    static let dataRoot: URL = {
        if let isolated = ProcessInfo.processInfo.environment["MORSECORE_DATA_ROOT"], isolated.hasPrefix("/") {
            return URL(fileURLWithPath: isolated, isDirectory: true)
        }
        return FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("MorseCore")
    }()
}

struct ServiceFailure: LocalizedError, Sendable {
    let message: String
    var errorDescription: String? { message }
}

actor MacServices {
    static let shared = MacServices()
    private var catalog: ServiceJSON?
    private var active = false
    private var waiting: [(id: UUID, continuation: CheckedContinuation<Void, Error>)] = []

    private func acquire() async throws {
        try Task.checkCancellation()
        if !active { active = true; return }
        let id = UUID()
        try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                waiting.append((id, continuation))
            }
        } onCancel: { Task { await self.cancelWaiting(id) } }
    }
    private func cancelWaiting(_ id: UUID) {
        guard let index = waiting.firstIndex(where: { $0.id == id }) else { return }
        waiting.remove(at: index).continuation.resume(throwing: CancellationError())
    }
    private func release() {
        if waiting.isEmpty { active = false } else { waiting.removeFirst().continuation.resume() }
    }

    static func timeout(for command: String) -> TimeInterval {
        switch command {
        case "speech.install": 1800
        case "speech.synthesize", "decoder.file": 300
        case "updates.download", "update.download": 900
        default: 120
        }
    }

    func call(_ command: String, _ payload: ServiceJSON = .object([:])) async throws -> ServiceJSON {
        try await acquire()
        defer { release() }
        try Task.checkCancellation()
        if command == "catalog", let catalog { return catalog }
        let request = try JSONEncoder().encode(ServiceJSON.object(["command": .string(command), "payload": payload]))
        guard request.count <= 64 * 1024 * 1024 else { throw ServiceFailure(message: "Request exceeds 64 MiB.") }
        let job = ServiceJob(timeout: Self.timeout(for: command))
        let data = try await withTaskCancellationHandler {
            try await Task.detached(priority: .userInitiated) { try job.run(request) }.value
        } onCancel: { job.cancel() }
        try Task.checkCancellation()
        let envelope = try JSONDecoder().decode(ServiceJSON.self, from: data)
        guard envelope["ok"].boolValue else { throw ServiceFailure(message: envelope["error"].stringValue) }
        let result = envelope["data"]
        if command == "catalog" { catalog = result }
        return result
    }
}

/// A single owned helper process. Limits apply while reading, before allocating the full response.
final class ServiceJob: @unchecked Sendable {
    private let lock = NSLock()
    private var process: Process?
    private var failure: Error?
    private let executable: URL?
    private let arguments: [String]?
    private let timeout: TimeInterval
    private let maximumResponseBytes: Int

    init(executable: URL? = nil, arguments: [String]? = nil, timeout: TimeInterval = 120,
         maximumResponseBytes: Int = 128 * 1024 * 1024) {
        self.executable = executable; self.arguments = arguments; self.timeout = timeout
        self.maximumResponseBytes = maximumResponseBytes
    }

    func cancel() { stop(throwing: CancellationError()) }

    private func stop(throwing error: Error) {
        lock.lock()
        if failure == nil { failure = error }
        let child = process
        lock.unlock()
        guard let child, child.isRunning else { return }
        child.terminate()
        // SIGTERM is cooperative in .NET. A stuck helper must not hold the serialized queue forever.
        DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + 1) {
            if child.isRunning { _ = Darwin.kill(child.processIdentifier, SIGKILL) }
        }
    }

    func run(_ request: Data) throws -> Data {
        let contents = Bundle.main.bundleURL.appendingPathComponent("Contents")
        let helper = executable ?? contents.appendingPathComponent("Helpers/MorseCore.MacService")
        guard FileManager.default.isExecutableFile(atPath: helper.path) else {
            throw ServiceFailure(message: "MorseCore Mac service is missing. Rebuild the application bundle.")
        }
        let suite = ProcessInfo.processInfo.environment["MORSECORE_SETTINGS_SUITE"]
        let defaults = suite.flatMap(UserDefaults.init(suiteName:)) ?? .standard
        let speech = defaults.string(forKey: "speechHome") ?? MacPaths.dataRoot.appendingPathComponent("Speech").path
        let child = Process()
        child.executableURL = helper
        child.arguments = arguments ?? ["--data", MacPaths.dataRoot.appendingPathComponent("Radio").path,
                           "--speech", speech, "--native", contents.appendingPathComponent("Frameworks/libMorseCore.Native.dylib").path]
        var environment = ProcessInfo.processInfo.environment
        environment["PATH"] = "/opt/homebrew/bin:/usr/local/bin:" + (environment["PATH"] ?? "/usr/bin:/bin")
        environment["DOTNET_NOLOGO"] = "1"
        environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        child.environment = environment
        let input = Pipe(), output = Pipe()
        child.standardInput = input
        child.standardOutput = output
        child.standardError = FileHandle.nullDevice
        lock.lock()
        if let failure { lock.unlock(); throw failure }
        process = child
        do { try child.run(); lock.unlock() } catch { process = nil; lock.unlock(); throw error }
        let deadline = DispatchSource.makeTimerSource(queue: DispatchQueue.global(qos: .utility))
        deadline.schedule(deadline: .now() + timeout)
        deadline.setEventHandler { [weak self] in self?.stop(throwing: ServiceFailure(message: "Mac service timed out.")) }
        deadline.resume()
        defer {
            deadline.cancel()
            input.fileHandleForWriting.closeFile()
            output.fileHandleForReading.closeFile()
            if child.isRunning { stop(throwing: ServiceFailure(message: "Mac service interrupted.")) }
            child.waitUntilExit()
            lock.lock(); process = nil; lock.unlock()
        }
        do {
            try input.fileHandleForWriting.write(contentsOf: request)
            try input.fileHandleForWriting.close()
            var response = Data()
            while let block = try output.fileHandleForReading.read(upToCount: min(65536, maximumResponseBytes - response.count + 1)), !block.isEmpty {
                guard block.count <= maximumResponseBytes - response.count else {
                    throw ServiceFailure(message: "Mac service response exceeds its size limit.")
                }
                response.append(block)
            }
            child.waitUntilExit()
            lock.lock(); let failure = self.failure; lock.unlock()
            if let failure { throw failure }
            guard !response.isEmpty else {
                throw ServiceFailure(message: "Invalid Mac service response (exit \(child.terminationStatus)).")
            }
            return response
        } catch {
            lock.lock(); let failure = self.failure; lock.unlock()
            throw failure ?? error
        }
    }
}
