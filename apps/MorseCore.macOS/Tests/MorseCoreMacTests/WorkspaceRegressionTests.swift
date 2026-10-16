import Foundation
import Testing
@testable import MorseCoreMac

@MainActor @Test func deletingAnotherStationKeepsTheActiveStation() {
    let document: ServiceJSON = .object([
        "activeId": .string("second"),
        "profiles": .array([.object(["id": .string("first")]), .object(["id": .string("second")]), .object(["id": .string("third")])])
    ])
    #expect(RadioState.activeID(afterRemoving: "third", from: document) == "second")
    #expect(RadioState.activeID(afterRemoving: "second", from: document) == "first")
    #expect(RadioState.activeID(afterRemoving: "missing", from: document) == "second")
    #expect(RadioState.activeID(afterRemoving: "only", from: .object([
        "activeId": .string("only"), "profiles": .array([.object(["id": .string("only")])])
    ])) == "")
}

@MainActor @Test func radioOperationsDoNotOverlapAndRecoverAfterFailure() async {
    let state = RadioState()
    var nestedRan = false
    await state.run {
        #expect(state.busy)
        await state.run { nestedRan = true }
        throw ServiceFailure(message: "Synthetic failure")
    }
    #expect(!nestedRan)
    #expect(!state.busy)
    #expect(state.notice == "Synthetic failure")
    var recovered = false
    await state.run { recovered = true }
    #expect(recovered && !state.busy)
}

@MainActor @Test func cancelledRadioReadDoesNotShowAnError() async {
    let state = RadioState()
    await state.run { throw CancellationError() }
    #expect(!state.busy)
    #expect(state.notice.isEmpty)
}
