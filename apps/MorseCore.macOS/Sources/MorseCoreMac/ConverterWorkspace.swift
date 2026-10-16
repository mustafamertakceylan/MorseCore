import SwiftUI
import AppKit
import UniformTypeIdentifiers
import MorseCoreBridge

@MainActor @Observable
final class ConverterState {
    var input = ""
    var output = ""
    var encoding = true
    var alphabet: Alphabet = .latin
    var statistics: ConversionStatistics?
    var busy = false
    var notice: CopyKey = .ready
    var isError = false
}

struct ConverterWorkspace: View {
    @Bindable var state: ConverterState
    @Bindable var settings: AppSettings
    var openAudio: (String, Bool) -> Void = { _, _ in }
    @Environment(\.colorScheme) private var scheme
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var l: Localizer { Localizer(language: settings.language) }
    private var tooLarge: Bool { state.input.utf16.count > 200_000 }
    private var empty: Bool { state.input.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(l(.subtitle)).foregroundStyle(theme.muted)
            VStack(alignment: .leading, spacing: 16) {
                HStack(alignment: .bottom, spacing: 14) {
                    HStack(spacing: 10) {
                        Button(l(.encode)) { state.encoding = true }
                            .buttonStyle(BrandButtonStyle(theme: theme, prominent: state.encoding))
                        Button(l(.decode)) { state.encoding = false }
                            .buttonStyle(BrandButtonStyle(theme: theme, prominent: !state.encoding))
                    }
                    Spacer(minLength: 0)
                    VStack(alignment: .leading, spacing: 8) {
                        Text(l(.alphabet)).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
                        Picker(l(.alphabet), selection: $state.alphabet) {
                            Text(l(.latin)).tag(Alphabet.latin)
                            Text(l(.russian)).tag(Alphabet.russian)
                            Text(l(.arabic)).tag(Alphabet.arabic)
                        }.labelsHidden().frame(width: 210)
                    }
                }.disabled(state.busy)
                HStack(alignment: .top, spacing: 14) {
                    editor(title: l(state.encoding ? .text : .morse), text: $state.input, result: false)
                    editor(title: l(state.encoding ? .morse : .text), text: $state.output, result: true)
                }
                HStack(spacing: 10) {
                    Button(action: convert) {
                        HStack(spacing: 8) {
                            if state.busy { ProgressView().controlSize(.small) }
                            else { Image(systemName: "arrow.triangle.2.circlepath") }
                            Text(l(state.busy ? .converting : .convert))
                        }
                    }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                        .keyboardShortcut(.return, modifiers: .command)
                        .disabled(state.busy || empty || tooLarge)
                    if !state.output.isEmpty {
                        Button(action: reverse) { Label(l(.swap), systemImage: "arrow.left.arrow.right") }
                            .buttonStyle(BrandButtonStyle(theme: theme)).disabled(state.busy)
                    }
                }
                if let statistics = state.statistics {
                    HStack(spacing: 26) {
                        statistic(l(.characters), value: statistics.inputCharacters)
                        statistic(l(.words), value: statistics.words)
                        statistic(l(.outputCharacters), value: statistics.outputCharacters)
                        Spacer(minLength: 0)
                    }
                } else { Text("⌘ + Return").font(BrandTheme.body(12)).foregroundStyle(theme.muted) }
                if tooLarge || state.notice != .ready {
                    Text(l(tooLarge ? .tooLarge : state.notice)).font(BrandTheme.body(13))
                        .foregroundStyle(state.isError || tooLarge ? Color.red : theme.muted)
                        .accessibilityIdentifier("conversionStatus")
                }
            }.brandCard(theme)
            DisclosureGroup(l(.normalization)) {
                Text(l(.normalizationBody)).foregroundStyle(theme.muted).padding(.top, 12)
            }.brandCard(theme)
        }
        .onChange(of: state.input) { invalidate() }
        .onChange(of: state.encoding) { invalidate() }
        .onChange(of: state.alphabet) { settings.alphabetRaw = Int(state.alphabet.rawValue); invalidate() }
        .onChange(of: settings.separator) { invalidate() }
        .onChange(of: settings.alphabetRaw) { state.alphabet = Alphabet(rawValue: UInt32(clamping: settings.alphabetRaw)) ?? .latin; invalidate() }
        .onAppear { state.alphabet = Alphabet(rawValue: UInt32(clamping: settings.alphabetRaw)) ?? .latin }
    }
    private func statistic(_ label: String, value: UInt64) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(value.formatted(.number.locale(Locale(identifier: settings.language.rawValue))))
                .font(BrandTheme.body(21)).monospacedDigit()
            Text(label).font(BrandTheme.body(12)).foregroundStyle(theme.muted)
        }
    }
    private func editor(title: String, text: Binding<String>, result: Bool) -> some View {
        let morse = result ? state.encoding : !state.encoding
        return VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(title).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
                Spacer()
                if !result {
                    Button { state.input = ""; invalidate() } label: { Image(systemName: "xmark") }
                        .buttonStyle(.borderless).help(l(.clear)).accessibilityLabel(l(.clear))
                        .disabled(state.busy || state.input.isEmpty)
                }
            }.frame(minHeight: 34)
            ZStack(alignment: .topLeading) {
                if text.wrappedValue.isEmpty {
                    Text(l(result ? .outputHint : .inputHint)).foregroundStyle(theme.muted)
                        .padding(12).allowsHitTesting(false)
                }
                if result {
                    ScrollView {
                        Text(text.wrappedValue).font(morse ? .system(size: 17, design: .monospaced) : BrandTheme.body(17))
                            .textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading).padding(12)
                    }
                } else {
                    TextEditor(text: text).font(morse ? .system(size: 17, design: .monospaced) : BrandTheme.body(17))
                        .scrollContentBackground(.hidden).padding(7).disabled(state.busy)
                }
            }
            .environment(\.layoutDirection, morse ? .leftToRight : (state.alphabet == .arabic ? .rightToLeft : .leftToRight))
            .frame(height: 200)
            .background(result ? theme.result : theme.input, in: RoundedRectangle(cornerRadius: 12))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(theme.line, lineWidth: 1))
            .accessibilityLabel(title).accessibilityIdentifier(result ? "conversionOutput" : "conversionInput")
            if result && !state.output.isEmpty {
                ViewThatFits(in: .horizontal) {
                    HStack(spacing: 8) { resultActions }
                    VStack(alignment: .leading, spacing: 8) { resultActions }
                }.buttonStyle(BrandButtonStyle(theme: theme)).disabled(state.busy)
            }
        }.frame(maxWidth: .infinity)
    }
    @ViewBuilder private var resultActions: some View {
        Button(action: copyResult) { Label(l(.copy), systemImage: "doc.on.doc") }
        Button(action: saveResult) { Label(settings.text("save"), systemImage: "square.and.arrow.down") }
        Button { openAudio(state.output, state.encoding) } label: { Label(settings.text("uxOpenAudio"), systemImage: "waveform") }
    }
    private func invalidate() {
        state.output = ""; state.statistics = nil; state.notice = .ready; state.isError = false
    }
    private func convert() {
        guard !state.busy, !empty, !tooLarge else { return }
        let request = ConversionRequest(text: state.input, direction: state.encoding ? .encode : .decode,
                                        alphabet: state.alphabet, separator: settings.separator)
        state.busy = true; state.isError = false; state.notice = .converting
        Task {
            defer { state.busy = false }
            do {
                let converted = try await Task.detached(priority: .userInitiated) { try CoreConverter.convert(request) }.value
                guard request.text == state.input, request.direction == (state.encoding ? .encode : .decode),
                      request.alphabet == state.alphabet, request.separator == settings.separator else { return }
                state.output = converted.text; state.statistics = converted.statistics; state.notice = .ready
            } catch let error as NativeCoreError {
                switch error.status {
                case -1: state.notice = .empty
                case -2, -20, -100, -105: state.notice = .invalid
                case -3: state.notice = .unsupported
                case -4: state.notice = .invalidMorse
                case -5: state.notice = .outOfMemory
                case -6: state.notice = .tooLarge
                default: state.notice = .failed
                }
                state.isError = true
            } catch { state.notice = .failed; state.isError = true }
        }
    }
    private func reverse() {
        guard !state.busy, !state.output.isEmpty else { return }
        let output = state.output
        state.encoding.toggle(); state.input = output; invalidate()
    }
    private func copyResult() {
        guard !state.busy, !state.output.isEmpty else { return }
        NSPasteboard.general.clearContents()
        if NSPasteboard.general.setString(state.output, forType: .string) { state.notice = .copied; state.isError = false }
    }
    private func saveResult() {
        guard !state.busy, !state.output.isEmpty else { return }
        let content = state.output
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText]; panel.nameFieldStringValue = "MorseCore.txt"; panel.canCreateDirectories = true
        if !settings.outputDirectory.isEmpty { panel.directoryURL = URL(fileURLWithPath: settings.outputDirectory, isDirectory: true) }
        panel.begin { response in
            guard response == .OK, let url = panel.url else { return }
            Task { @MainActor in
                state.busy = true; state.notice = .saving
                defer { state.busy = false }
                do {
                    try await Task.detached(priority: .userInitiated) { try content.write(to: url, atomically: true, encoding: .utf8) }.value
                    state.notice = .saved; state.isError = false
                } catch { state.notice = .saveFailed; state.isError = true }
            }
        }
    }
}
