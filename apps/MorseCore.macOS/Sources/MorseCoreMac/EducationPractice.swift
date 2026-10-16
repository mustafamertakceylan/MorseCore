import SwiftUI
import Observation
import MorseCoreBridge

@MainActor @Observable
private final class EducationLesson {
    var current: EducationQuestion?
    var correct = 0
    var incorrect = 0
    var skipped = 0
    var total = 0
    var busy = false
    var started = false
    var feedbackKey = ""
    var feedbackAnswer = ""
    var errorKey = ""
    var missed: [EducationQuestion] = []
    @ObservationIgnored private var native: CorePractice?
    @ObservationIgnored private var pending: [EducationQuestion] = []
    @ObservationIgnored private var generation = 0

    func start(mode: Int, retry: Bool = false) async {
        guard !busy else { return }
        let previousMissed = missed
        generation += 1
        let request = generation
        busy = true
        defer { if generation == request { busy = false } }
        current = nil; errorKey = ""; feedbackKey = ""; feedbackAnswer = ""
        correct = 0; incorrect = 0; skipped = 0; total = 0; native = nil
        pending = []; missed = []; started = true
        do {
            if retry {
                pending = previousMissed
                total = pending.count
                current = takeNext()
            } else if mode < 3 {
                let practice = CorePractice()
                try await practice.start(mode: UInt32(mode), seed: UInt32.random(in: 1...UInt32.max))
                let code = try await practice.next()
                guard request == generation else { return }
                native = practice; total = 10
                current = EducationQuestion(answer: "", morse: code)
            } else {
                let result = try await MacServices.shared.call("learning.questions", .object(["mode": .number(Double(mode)), "seed": .number(Double(Int32.random(in: 1...Int32.max)))]))
                guard request == generation else { return }
                pending = result.arrayValue.map { EducationQuestion(answer: $0["answer"].stringValue, morse: $0["morse"].stringValue) }
                total = pending.count; current = takeNext()
            }
        } catch {
            guard request == generation else { return }
            errorKey = "error"
        }
    }

    func submit(_ answer: String) async {
        guard !busy, let current else { return }
        busy = true
        let request = generation
        defer { if request == generation { busy = false } }
        do {
            let expected: String
            let verdict: Int32
            if let native {
                let feedback = try await native.submit(answer)
                expected = feedback.expectedAnswer; verdict = feedback.verdict
            } else {
                expected = current.answer
                let value = answer.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
                let matches = value == expected || (expected.hasPrefix("<") && expected.hasSuffix(">") && value == String(expected.dropFirst().dropLast()))
                verdict = matches ? 1 : value.isEmpty ? 2 : 0
            }
            guard generation == request else { return }
            feedbackKey = verdict == 1 ? "correct" : verdict == 2 ? "skipped" : "incorrect"
            feedbackAnswer = expected
            if verdict == 1 { correct += 1 }
            else {
                if verdict == 2 { skipped += 1 } else { incorrect += 1 }
                if !missed.contains(where: { $0.answer == expected }) { missed.append(EducationQuestion(answer: expected, morse: current.morse)) }
            }
            if let native {
                let stats = try await native.statistics()
                guard generation == request else { return }
                if stats.answered < stats.total {
                    let next = try await native.next()
                    guard generation == request else { return }
                    self.current = EducationQuestion(answer: "", morse: next)
                } else { self.current = nil }
            } else { self.current = takeNext() }
            errorKey = ""
        } catch { if generation == request { errorKey = "error" } }
    }

    func cancel() { generation += 1; busy = false }
    private func takeNext() -> EducationQuestion? { pending.isEmpty ? nil : pending.removeFirst() }
}

struct EducationPractice: View {
    let settings: AppSettings
    @Environment(\.colorScheme) private var scheme
    @State private var lesson = EducationLesson()
    @State private var mode = 0
    @State private var answer = ""
    @State private var audioError = false
    @State private var playback: Task<Void, Never>?
    @FocusState private var answerFocused: Bool
    private let modes = ["letters", "digits", "wordPractice", "qcodes", "callSigns", "prosigns"]
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            EducationHeading(title: settings.text("practice"), subtitle: settings.text("practiceNote"), theme: theme)
            VStack(alignment: .leading, spacing: 20) {
                Picker(settings.text("mode"), selection: $mode) {
                    ForEach(Array(modes.enumerated()), id: \.element) { index, key in Text(settings.text(key)).tag(index) }
                }.frame(maxWidth: 380).disabled(lesson.busy)
                HStack(spacing: 12) {
                    Button(settings.text("start")) { start() }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(lesson.busy)
                        .accessibilityIdentifier("PracticeStart")
                    Button(settings.text("retryMissed")) { start(retry: true) }.buttonStyle(BrandButtonStyle(theme: theme)).disabled(lesson.busy || lesson.missed.isEmpty)
                    if lesson.busy { ProgressView().controlSize(.small) }
                }
                Text(lesson.current?.morse ?? (lesson.started ? settings.text("practiceEnded") : "· — · ·"))
                    .font(.system(size: 28, design: .monospaced)).textSelection(.enabled)
                    .frame(maxWidth: .infinity, minHeight: 110)
                    .padding(18).background(theme.result, in: RoundedRectangle(cornerRadius: 14))
                    .environment(\.layoutDirection, .leftToRight)
                    .accessibilityIdentifier("PracticeSignal")
                TextField(settings.text("answer"), text: $answer).textFieldStyle(.roundedBorder)
                    .environment(\.layoutDirection, .leftToRight).focused($answerFocused)
                    .onSubmit { submit(answer) }.disabled(lesson.current == nil || lesson.busy)
                    .onChange(of: answer) { _, value in if value.count > 64 { answer = String(value.prefix(64)) } }
                    .accessibilityIdentifier("PracticeAnswer")
                HStack(spacing: 12) {
                    Button(settings.text("submit")) { submit(answer) }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                    Button(settings.text("skip")) { submit("") }.buttonStyle(BrandButtonStyle(theme: theme))
                        .accessibilityIdentifier("PracticeSkip")
                    Button { play() } label: { Label(settings.text("play"), systemImage: "speaker.wave.2") }.buttonStyle(BrandButtonStyle(theme: theme))
                    Button(settings.text("stop")) { stopPlayback() }.buttonStyle(BrandButtonStyle(theme: theme))
                }.disabled(lesson.current == nil || lesson.busy)
                if !lesson.feedbackKey.isEmpty {
                    Text("\(settings.text(lesson.feedbackKey)) · \(lesson.feedbackAnswer)")
                        .foregroundStyle(theme.ink).accessibilityIdentifier("PracticeFeedback")
                }
                if lesson.started {
                    Text("\(lesson.correct) \(settings.text("correct")) · \(lesson.incorrect) \(settings.text("incorrect")) · \(lesson.skipped) \(settings.text("skipped")) · \(lesson.correct + lesson.incorrect + lesson.skipped) / \(lesson.total)")
                        .foregroundStyle(theme.muted)
                }
                if !lesson.errorKey.isEmpty || audioError { Text(settings.text("error")).foregroundStyle(.red) }
            }.brandCard(theme)
        }.onDisappear { lesson.cancel(); stopPlayback() }
    }
    private func start(retry: Bool = false) {
        stopPlayback(); answer = ""; audioError = false
        Task { await lesson.start(mode: mode, retry: retry); answerFocused = true }
    }
    private func submit(_ value: String) {
        stopPlayback()
        Task { await lesson.submit(value); answer = ""; answerFocused = true }
    }
    private func play() {
        guard let question = lesson.current else { return }
        stopPlayback()
        playback = Task {
            do { try await MacAudio.shared.play(morse: question.morse); audioError = false }
            catch is CancellationError { }
            catch { audioError = true }
        }
    }
    private func stopPlayback() { playback?.cancel(); playback = nil; MacAudio.shared.stop() }
}
