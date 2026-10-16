import Foundation
import Testing
@testable import MorseCoreMac

@Test func serviceResponseIsBoundedBeforeFullRead() async throws {
    let job = ServiceJob(executable: URL(fileURLWithPath: "/bin/sh"),
        arguments: ["-c", "printf 123456789"], maximumResponseBytes: 8)
    do {
        _ = try await Task.detached { try job.run(Data()) }.value
        Issue.record("Oversized helper output was accepted.")
    } catch let error as ServiceFailure {
        #expect(error.message.contains("size limit"))
    }
}

@Test func serviceDeadlineTerminatesUncooperativeOwnedChild() async throws {
    let job = ServiceJob(executable: URL(fileURLWithPath: "/bin/sh"),
        arguments: ["-c", "trap '' TERM; exec /bin/sleep 30"], timeout: 0.1)
    let started = ContinuousClock.now
    do {
        _ = try await Task.detached { try job.run(Data()) }.value
        Issue.record("Timed-out helper was accepted.")
    } catch let error as ServiceFailure {
        #expect(error.message.contains("timed out"))
    }
    #expect(started.duration(to: .now) < .seconds(5))
}

@Test func cancelledServiceDoesNotLaunch() async throws {
    let job = ServiceJob(executable: URL(fileURLWithPath: "/bin/sh"), arguments: ["-c", "exit 77"])
    job.cancel()
    do {
        _ = try await Task.detached { try job.run(Data()) }.value
        Issue.record("Cancelled helper was launched.")
    } catch is CancellationError { }
}

@Test func cancellingRunningServiceReturnsWithoutLeavingQueueBlocked() async throws {
    let job = ServiceJob(executable: URL(fileURLWithPath: "/bin/sh"),
        arguments: ["-c", "trap '' TERM; exec /bin/sleep 30"])
    let started = ContinuousClock.now
    let worker = Task.detached { try job.run(Data()) }
    try await Task.sleep(for: .milliseconds(100))
    job.cancel()
    do {
        _ = try await worker.value
        Issue.record("Cancelled helper response was accepted.")
    } catch is CancellationError { }
    #expect(started.duration(to: .now) < .seconds(5))
}

@Test func serviceNormalResponseAndOperationTimeouts() async throws {
    let job = ServiceJob(executable: URL(fileURLWithPath: "/bin/sh"), arguments: ["-c", "printf '{\"ok\":true}'"])
    let response = try await Task.detached { try job.run(Data()) }.value
    #expect(String(decoding: response, as: UTF8.self) == "{\"ok\":true}")
    #expect(MacServices.timeout(for: "speech.install") == 1800)
    #expect(MacServices.timeout(for: "updates.download") == 900)
    #expect(MacServices.timeout(for: "catalog") == 120)
}

@MainActor @Test func failedTemporaryPlaybackRemovesOnlyOwnedFile() throws {
    let temporary = MacAudio.temporaryWAV()
    let retained = MacAudio.temporaryWAV()
    try Data("invalid audio".utf8).write(to: temporary)
    try Data("keep".utf8).write(to: retained)
    defer { try? FileManager.default.removeItem(at: temporary); try? FileManager.default.removeItem(at: retained) }
    let audio = MacAudio()
    #expect(throws: (any Error).self) { try audio.play(url: temporary, temporary: true) }
    #expect(!FileManager.default.fileExists(atPath: temporary.path))
    #expect(try Data(contentsOf: retained) == Data("keep".utf8))
    #expect(!audio.isPlaying)
}
