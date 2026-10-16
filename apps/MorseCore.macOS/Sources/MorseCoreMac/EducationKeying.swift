import SwiftUI
import AppKit
import Observation
import MorseCoreBridge

@MainActor @Observable
private final class EducationKeyingSession {
    var pressed = false
    var started = false
    var finished = false
    var busy = false
    var result: ServiceJSON = .null
    var errorKey = ""
    @ObservationIgnored private var events: [ServiceJSON] = []
    @ObservationIgnored private var origin = ProcessInfo.processInfo.systemUptime
    @ObservationIgnored private var generation = 0
    @ObservationIgnored private var analysis: Task<Void, Never>?
    @ObservationIgnored private var timeout: Task<Void, Never>?

    private var now: Double { (ProcessInfo.processInfo.systemUptime - origin) * 1000 }
    func start() {
        cancelPending(); analysis?.cancel(); generation += 1
        events = []; origin = ProcessInfo.processInfo.systemUptime
        result = .null; started = true; finished = false; busy = false; errorKey = ""
    }
    func down(sound: Bool) {
        guard !pressed else { return }
        if !started || finished { start() }
        guard events.count < 8192 else { errorKey = "keyingLimit"; return }
        events.append(.object(["down": .bool(true), "milliseconds": .number(now)]))
        pressed = true
        if sound {
            do { try MacAudio.shared.startTone() }
            catch { errorKey = "keyingSoundUnavailable" }
        }
        timeout?.cancel()
        timeout = Task {
            do { try await Task.sleep(for: .seconds(10)) } catch { return }
            guard !Task.isCancelled else { return }
            cancelPending(); errorKey = "keyingInterrupted"
        }
    }
    func up(wpm: Int, alphabet: Alphabet, expected: String) {
        guard pressed else { return }
        timeout?.cancel(); timeout = nil; pressed = false; MacAudio.shared.stopTone()
        events.append(.object(["down": .bool(false), "milliseconds": .number(now)]))
        analyze(wpm: wpm, alphabet: alphabet, expected: expected, final: false)
    }
    func cancelPending() {
        timeout?.cancel(); timeout = nil; MacAudio.shared.stopTone()
        if pressed {
            // Losing focus discards the unmatched transition rather than creating
            // an invented key-up and a misleading timing measurement.
            if events.last?["down"].boolValue == true { events.removeLast() }
            pressed = false
        }
    }
    func finish(wpm: Int, alphabet: Alphabet, expected: String) {
        cancelPending(); finished = true
        analyze(wpm: wpm, alphabet: alphabet, expected: expected, final: true)
    }
    func cleanup() { cancelPending(); generation += 1; analysis?.cancel(); analysis = nil; busy = false }
    private func analyze(wpm: Int, alphabet: Alphabet, expected: String, final: Bool) {
        analysis?.cancel(); generation += 1
        let request = generation
        let payload: ServiceJSON = .object([
            "wpm": .number(Double(wpm)), "alphabet": .number(Double(alphabet.rawValue)),
            "events": .array(events), "finishMilliseconds": .number(now), "expected": .string(expected)
        ])
        analysis = Task {
            do {
                if !final { try await Task.sleep(for: .milliseconds(100)) }
                try Task.checkCancellation()
                guard request == generation else { return }
                busy = true
                let value = try await MacServices.shared.call("keying.analyze", payload)
                guard !Task.isCancelled, request == generation else { return }
                result = value; busy = false
            } catch {
                guard !Task.isCancelled, request == generation else { return }
                busy = false; errorKey = "error"
            }
        }
    }
}

struct EducationKeying: View {
    let settings: AppSettings
    @Environment(\.colorScheme) private var scheme
    @State private var session = EducationKeyingSession()
    @State private var wpm = 20
    @State private var sound = true
    @State private var alphabet: Alphabet = .latin
    @State private var expected = "SOS"
    @State private var focusRequest = 0
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var snapshot: ServiceJSON { session.result["snapshot"] }
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            EducationHeading(title: settings.text("keyingTitle"), subtitle: settings.text("keyingIntroduction"), theme: theme)
            VStack(alignment: .leading, spacing: 20) {
                EducationAlphabetPicker(settings: settings, selection: $alphabet)
                HStack(spacing: 28) {
                    Stepper("\(settings.text("keyingTargetSpeed")): \(wpm) WPM", value: $wpm, in: 5...60)
                    Toggle(settings.text("keyingSidetone"), isOn: $sound)
                }
                TextField(settings.text("keyingReference"), text: $expected).textFieldStyle(.roundedBorder)
                    .environment(\.layoutDirection, alphabet == .arabic ? .rightToLeft : .leftToRight)
                    .onChange(of: expected) { _, value in if value.count > 64 { expected = String(value.prefix(64)) } }
                KeyingPressArea(label: settings.text("keyingKeyboardHelp"), focusRequest: focusRequest, down: { session.down(sound: sound) }, up: {
                    session.up(wpm: wpm, alphabet: alphabet, expected: expected)
                }, cancel: { session.cancelPending() })
                    .overlay { Text(settings.text(session.pressed ? "keyingHeld" : "keyingPress")).font(BrandTheme.heading(28)).allowsHitTesting(false) }
                    .frame(height: 110)
                    .background(session.pressed ? theme.accent : theme.input, in: RoundedRectangle(cornerRadius: 14))
                    .overlay(RoundedRectangle(cornerRadius: 14).stroke(theme.line, lineWidth: 2))
                    .foregroundStyle(session.pressed ? Color.white : theme.ink)
                    .accessibilityIdentifier("KeyingPaddle")
                Text(settings.text("keyingKeyboardHelp")).foregroundStyle(theme.muted).font(BrandTheme.body(14))
                HStack(spacing: 12) {
                    Button(settings.text("keyingStart")) { session.start(); focusRequest += 1 }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                    Button(settings.text("keyingFinish")) { session.finish(wpm: wpm, alphabet: alphabet, expected: expected) }
                        .buttonStyle(BrandButtonStyle(theme: theme)).disabled(!session.started || session.finished)
                    if session.busy { ProgressView().controlSize(.small) }
                }
                HStack(alignment: .top, spacing: 16) {
                    transcript(settings.text("keyingDecoded"), value: snapshot["text"].stringValue, code: false)
                    transcript(settings.text("keyingMorse"), value: snapshot["morse"].stringValue, code: true)
                }
                if session.started && snapshot["marks"].intValue > 0 { measurement }
                if !session.errorKey.isEmpty { Text(settings.text(session.errorKey)).foregroundStyle(.red) }
                Text(settings.text("keyingMeasurementNote")).font(BrandTheme.body(14)).foregroundStyle(theme.muted)
            }.brandCard(theme)
        }
        .onChange(of: wpm) { session.start() }
        .onChange(of: alphabet) { session.start() }
        .onChange(of: expected) { session.start() }
        .onChange(of: sound) { if !sound { MacAudio.shared.stopTone() } }
        .onDisappear { session.cleanup() }
    }
    private func transcript(_ title: String, value: String, code: Bool) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).foregroundStyle(theme.muted)
            Text(value.isEmpty ? "—" : value)
                .font(code ? .system(size: 20, design: .monospaced) : BrandTheme.body(20))
                .textSelection(.enabled).frame(maxWidth: .infinity, minHeight: 72, alignment: .topLeading)
                .environment(\.layoutDirection, code || alphabet != .arabic ? .leftToRight : .rightToLeft)
                .padding(14).background(theme.result, in: RoundedRectangle(cornerRadius: 12))
        }.frame(maxWidth: .infinity)
    }
    private var measurement: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("\(settings.text("keyingEffectiveSpeed")): \(snapshot["effectiveWpm"].doubleValue, specifier: "%.1f") WPM · \(settings.text("keyingCharacterSpeed")): \(snapshot["characterWpm"].doubleValue, specifier: "%.1f") WPM")
            Text("\(settings.text("keyingRhythm")): \(snapshot["rhythmScore"].intValue)%")
            if session.finished && !expected.isEmpty { Text("\(settings.text("keyingAccuracy")): \(session.result["accuracy"].intValue)%") }
            ForEach(snapshot["timings"].arrayValue.map { EducationTiming(value: $0, id: timingKind($0["kind"])) }) { row in
                let timing = row.value
                Text("\(settings.text("keyingTiming" + timingKind(timing["kind"]))): \(timing["averageMilliseconds"].doubleValue, specifier: "%.0f") ms / \(settings.text("keyingTarget")): \(timing["expectedMilliseconds"].doubleValue, specifier: "%.0f") ms")
                    .font(BrandTheme.body(14)).foregroundStyle(theme.muted)
            }
            ForEach(snapshot["feedback"].arrayValue.map(\.stringValue), id: \.self) { Text(settings.text($0)).font(BrandTheme.body(14)) }
        }
    }
    private func timingKind(_ value: ServiceJSON) -> String {
        if !value.stringValue.isEmpty { return value.stringValue }
        let kinds = ["Dot", "Dash", "ElementGap", "LetterGap", "WordGap"]
        return kinds[min(4, max(0, value.intValue))]
    }
}

private struct EducationTiming: Identifiable {
    let value: ServiceJSON
    let id: String
}

/// Only this focused view receives keyboard input. No event tap, global monitor
/// or accessibility permission is installed for straight-key practice.
private struct KeyingPressArea: NSViewRepresentable {
    let label: String
    let focusRequest: Int
    let down: () -> Void
    let up: () -> Void
    let cancel: () -> Void
    func makeNSView(context: Context) -> KeyingInputView { KeyingInputView() }
    func updateNSView(_ view: KeyingInputView, context: Context) {
        view.onDown = down; view.onUp = up; view.onCancel = cancel
        view.setAccessibilityLabel(label)
        if view.focusRequest != focusRequest {
            view.focusRequest = focusRequest
            view.window?.makeFirstResponder(view)
        }
    }
    static func dismantleNSView(_ view: KeyingInputView, coordinator: ()) { view.cancelInput(); view.removeObservers() }
}

@MainActor private final class KeyingInputView: NSView {
    var onDown: (() -> Void)?
    var onUp: (() -> Void)?
    var onCancel: (() -> Void)?
    var focusRequest = 0
    private var input: String?
    private var observers: [NSObjectProtocol] = []
    override var acceptsFirstResponder: Bool { true }
    override init(frame: NSRect) {
        super.init(frame: frame)
        setAccessibilityElement(true); setAccessibilityRole(.group)
        for name in [NSApplication.didResignActiveNotification, NSWindow.didResignKeyNotification] {
            observers.append(NotificationCenter.default.addObserver(forName: name, object: nil, queue: .main) { [weak self] notification in
                // Send only a value identity across the actor boundary; Notification
                // and its arbitrary object payload are not Sendable in Swift 6.
                let lostWindowID = (notification.object as? NSWindow).map { ObjectIdentifier($0) }
                MainActor.assumeIsolated {
                    guard let self else { return }
                    if let lostWindowID, self.window.map({ ObjectIdentifier($0) }) != lostWindowID { return }
                    self.cancelInput()
                }
            })
        }
    }
    required init?(coder: NSCoder) { fatalError("init(coder:) has not been implemented") }
    override func mouseDown(with event: NSEvent) {
        guard input == nil else { return }
        window?.makeFirstResponder(self); input = "mouse"; onDown?()
    }
    override func mouseUp(with event: NSEvent) { release("mouse") }
    override func keyDown(with event: NSEvent) {
        guard event.keyCode == 49 else { super.keyDown(with: event); return }
        guard !event.isARepeat, input == nil else { return }
        input = "keyboard"; onDown?()
    }
    override func keyUp(with event: NSEvent) {
        guard event.keyCode == 49 else { super.keyUp(with: event); return }
        release("keyboard")
    }
    override func resignFirstResponder() -> Bool { cancelInput(); return super.resignFirstResponder() }
    override func viewWillMove(toWindow newWindow: NSWindow?) { if newWindow == nil { cancelInput() }; super.viewWillMove(toWindow: newWindow) }
    func cancelInput() { input = nil; onCancel?() }
    func removeObservers() { observers.forEach(NotificationCenter.default.removeObserver); observers = [] }
    private func release(_ kind: String) { guard input == kind else { return }; input = nil; onUp?() }
}
