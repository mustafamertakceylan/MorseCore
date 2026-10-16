import SwiftUI
import AppKit
import UniformTypeIdentifiers
import MorseCoreBridge

private enum AudioSection: String, CaseIterable, Identifiable {
    case morse, speech, decode, voices
    var id: String { rawValue }
    var title: String {
        switch self { case .morse: "uxMorseAudio"; case .speech: "speech"; case .decode: "workspaceDecode"; case .voices: "workspaceVoices" }
    }
}

private struct MacVoice: Identifiable {
    let id: String, language: String, name: String, engine: String, source: String
    let installed: Bool
    let bytes: Double
    init(_ json: ServiceJSON) {
        id = json["id"].stringValue; language = json["language"].stringValue
        name = json["name"].stringValue; engine = json["engine"].stringValue
        installed = json["installed"].boolValue; source = json["sourceUrl"].stringValue
        bytes = json["downloadBytes"].doubleValue
    }
}

@MainActor
struct AudioWorkspace: View {
    @Bindable var settings: AppSettings
    @Environment(\.colorScheme) private var colorScheme
    @State private var audio = MacAudio.shared
    @State private var section = AudioSection.morse
    @State private var source = ""
    @State private var isMorse = false
    @State private var alphabet = Alphabet.latin
    @State private var wpm = 20
    @State private var frequency = 700
    @State private var working = false
    @State private var operation: Task<Void, Never>?
    @State private var message = ""
    @State private var voices: [MacVoice] = []
    @State private var speechLanguage = "tr-TR"
    @State private var selectedVoice = ""
    @State private var pythonAvailable = false
    @State private var pendingInstall: MacVoice?
    @State private var pendingRemove: MacVoice?
    @State private var useMicrophone = false
    @State private var inputs: [MacAudioInput] = []
    @State private var inputID = ""
    @State private var capturePending = false
    @State private var decodeURL: URL?
    @State private var decodeResult: ServiceJSON?
    @State private var autoTone = true
    @State private var autoSpeed = true
    @State private var threshold = 35.0

    private var theme: BrandTheme { BrandTheme(dark: colorScheme == .dark) }
    private func t(_ key: String) -> String { settings.text(key) }
    private var empty: Bool { source.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    private var filteredVoices: [MacVoice] { voices.filter { $0.language == speechLanguage } }

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack(spacing: 10) {
                ForEach(AudioSection.allCases) { item in
                    Button(t(item.title)) { section = item }
                        .buttonStyle(BrandButtonStyle(theme: theme, prominent: section == item))
                        .disabled(working || capturePending)
                }
            }
            switch section {
            case .morse, .speech: sourceCard
            case .decode: decoderCard
            case .voices: voicesCard
            }
            if section == .speech { speechCard }
            if working {
                HStack { ProgressView().controlSize(.small); Text(message); Spacer()
                    Button(t("voicesCancel")) { cancel() }.buttonStyle(BrandButtonStyle(theme: theme))
                }
            } else if !message.isEmpty {
                Text(message).foregroundStyle(theme.muted).textSelection(.enabled)
                    .accessibilityIdentifier("AudioFeedback")
            }
        }
        .font(BrandTheme.body()).foregroundStyle(theme.ink)
        .task {
            section = AudioSection(rawValue: settings.audioSectionRaw) ?? .morse
            source = settings.audioText; isMorse = settings.audioIsMorse
            wpm = min(60, max(5, settings.wpm)); frequency = min(2000, max(200, settings.frequency))
            speechLanguage = settings.speechLanguage.isEmpty ? culture(settings.language) : settings.speechLanguage
            selectedVoice = settings.voiceId
            await refreshVoices()
        }
        .onChange(of: source) { settings.audioText = source }
        .onChange(of: settings.audioSectionRaw) {
            let requested = AudioSection(rawValue: settings.audioSectionRaw) ?? .morse
            if section != requested { section = requested }
        }
        .onChange(of: isMorse) { settings.audioIsMorse = isMorse }
        .onChange(of: wpm) { settings.wpm = wpm }
        .onChange(of: frequency) { settings.frequency = frequency }
        .onChange(of: selectedVoice) { settings.voiceId = selectedVoice }
        .onChange(of: speechLanguage) { settings.speechLanguage = speechLanguage }
        .onChange(of: speechLanguage) { selectAvailableVoice() }
        .onChange(of: section) { settings.audioSectionRaw = section.rawValue; audio.stop(); message = "" }
        .onDisappear { cancel() }
        .confirmationDialog(t("voicesInstall"), isPresented: Binding(
            get: { pendingInstall != nil }, set: { if !$0 { pendingInstall = nil } })) {
                if let voice = pendingInstall {
                    Button(t("voicesInstall")) { pendingInstall = nil; install(voice) }
                }
                Button(t("voicesCancel"), role: .cancel) { pendingInstall = nil }
            } message: {
                Text(t("voicesInstallConsent") + (pythonAvailable ? "" : "\n\n" + AudioCopy.runtimeMissing(settings.language)))
            }
        .confirmationDialog(t("voicesRemove"), isPresented: Binding(
            get: { pendingRemove != nil }, set: { if !$0 { pendingRemove = nil } })) {
                if let voice = pendingRemove {
                    Button(t("voicesRemove"), role: .destructive) { pendingRemove = nil; remove(voice) }
                }
                Button(t("voicesCancel"), role: .cancel) { pendingRemove = nil }
            } message: { Text(t("voicesRemoveConsent")) }
    }

    private var alphabetPicker: some View {
        Picker(t("alphabet"), selection: $alphabet) {
            Text(Localizer(language: settings.language)(.latin)).tag(Alphabet.latin)
            Text(Localizer(language: settings.language)(.russian)).tag(Alphabet.russian)
            Text(Localizer(language: settings.language)(.arabic)).tag(Alphabet.arabic)
        }.pickerStyle(.menu)
    }

    private var sourceCard: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(t(section == .speech ? "uxSpeechIntro" : "uxMorseIntro")).foregroundStyle(theme.muted)
            HStack(spacing: 24) {
                alphabetPicker
                Picker(t("audioInputType"), selection: $isMorse) {
                    Text(t("plainText")).tag(false); Text(t("morseInput")).tag(true)
                }.pickerStyle(.menu)
            }
            Text(t(isMorse ? "morseInput" : "plainText"))
            TextEditor(text: $source)
                .font(isMorse ? .system(size: 18, design: .monospaced) : BrandTheme.body(18))
                .scrollContentBackground(.hidden).padding(12)
                .background(theme.input, in: RoundedRectangle(cornerRadius: 14))
                .overlay(RoundedRectangle(cornerRadius: 14).stroke(theme.line, lineWidth: 1))
                .frame(minHeight: 160, maxHeight: 220)
                .environment(\.layoutDirection, isMorse || alphabet != .arabic ? .leftToRight : .rightToLeft)
                .accessibilityIdentifier("AudioInput")
            if section == .morse {
                HStack {
                    Button(t("play"), systemImage: "play.fill") { morseAction(export: false) }
                        .buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(empty || working || audio.isPlaying)
                    if audio.isPlaying { Button(t("stop"), systemImage: "stop.fill") { audio.stop() }.buttonStyle(BrandButtonStyle(theme: theme)) }
                    Button(t("exportWav"), systemImage: "square.and.arrow.up") { morseAction(export: true) }
                        .buttonStyle(BrandButtonStyle(theme: theme)).disabled(empty || working)
                }
                DisclosureGroup(t("uxAdvanced")) {
                    HStack(spacing: 30) {
                        Stepper("\(t("speed")): \(wpm)", value: $wpm, in: 5...60)
                        Stepper("\(t("frequency")): \(frequency)", value: $frequency, in: 200...2000, step: 50)
                    }.padding(.top, 12)
                }
            }
        }.brandCard(theme).disabled(working)
    }

    private var languagePicker: some View {
        Picker(t("speechLanguage"), selection: $speechLanguage) {
            ForEach(AppLanguage.allCases) { language in Text(language.name).tag(culture(language)) }
        }.pickerStyle(.menu)
    }

    private var speechCard: some View {
        VStack(alignment: .leading, spacing: 16) {
            Label(t("speech"), systemImage: "speaker.wave.2").font(BrandTheme.body(20))
            Text(t("offlineSpeech")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
            HStack(spacing: 24) {
                languagePicker
                Picker(t("voice"), selection: $selectedVoice) {
                    Text("—").tag("")
                    ForEach(filteredVoices) { voice in Text(voice.name + (voice.installed ? "" : " · " + t("voicesNotInstalled"))).tag(voice.id) }
                }.pickerStyle(.menu)
            }.disabled(working)
            HStack {
                Button(t("speak"), systemImage: "play.fill") { speak() }
                    .buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                    .disabled(empty || working || audio.isPlaying || !voices.contains { $0.id == selectedVoice && $0.installed })
                if audio.isPlaying { Button(t("stop"), systemImage: "stop.fill") { audio.stop() }.buttonStyle(BrandButtonStyle(theme: theme)) }
                Button(t("voicesManage")) { section = .voices }.buttonStyle(BrandButtonStyle(theme: theme)).disabled(working)
            }
            Text(t("speechLocalNote")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
        }.brandCard(theme)
    }

    private var decoderCard: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 16) {
                Text(t("uxDecoderIntro")).foregroundStyle(theme.muted)
                Picker(t("audioInputType"), selection: $useMicrophone) {
                    Text(t("uxWaveSource")).tag(false); Text(t("uxMicrophoneSource")).tag(true)
                }.pickerStyle(.segmented).disabled(working || capturePending)
                alphabetPicker.disabled(working || capturePending)
                if useMicrophone {
                    Text(AudioCopy.recording(settings.language)).foregroundStyle(theme.muted)
                    HStack {
                        Picker(t("decoderDevice"), selection: $inputID) {
                            Text(Localizer(language: settings.language)(.system)).tag("")
                            ForEach(inputs) { input in Text(input.name).tag(input.id) }
                        }.pickerStyle(.menu)
                        Button(t("decoderRefreshDevices"), systemImage: "arrow.clockwise") { refreshInputs() }
                            .buttonStyle(BrandButtonStyle(theme: theme))
                    }.disabled(working || capturePending)
                    if capturePending {
                        HStack {
                            if audio.isRecording { ProgressView().controlSize(.small) }
                            Button(t("decoderStopRecord"), systemImage: "stop.circle") { finishCapture() }
                                .buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                            Button(t("decoderCancel")) { cancel() }.buttonStyle(BrandButtonStyle(theme: theme))
                        }
                    } else {
                        Button(t("decoderRecord"), systemImage: "mic") { beginCapture() }
                            .buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(working)
                    }
                } else {
                    HStack {
                        Button(t("decoderChoose"), systemImage: "folder") { chooseWAV() }.buttonStyle(BrandButtonStyle(theme: theme)).disabled(working)
                        Text(decodeURL?.lastPathComponent ?? t("decoderNoFile")).foregroundStyle(theme.muted)
                    }
                    Button(t("decoderSolve")) { if let url = decodeURL { decode(url: url, temporary: false) } }
                        .buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(decodeURL == nil || working)
                }
                DisclosureGroup(t("uxAdvanced")) {
                    VStack(alignment: .leading, spacing: 12) {
                        Text(t("decoderHint")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
                        Toggle(t("decoderAutoTone"), isOn: $autoTone)
                        Stepper("\(t("decoderTone")): \(frequency)", value: $frequency, in: 200...2000, step: 50).disabled(autoTone)
                        Toggle(t("decoderAutoSpeed"), isOn: $autoSpeed)
                        Stepper("\(t("speed")): \(wpm)", value: $wpm, in: 5...60).disabled(autoSpeed)
                        Text(t("decoderThreshold") + ": \(Int(threshold))")
                        Slider(value: $threshold, in: 10...70, step: 5)
                        Text(t("decoderThresholdHint")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
                    }.padding(.top, 12)
                }.disabled(working || capturePending)
            }.brandCard(theme)
            if let result = decodeResult {
                VStack(alignment: .leading, spacing: 14) {
                    Text(t("uxDecoderResult")).font(BrandTheme.body(20))
                    Text(result["text"].stringValue).textSelection(.enabled).frame(maxWidth: .infinity, minHeight: 80, alignment: .topLeading)
                        .padding(14).background(theme.input, in: RoundedRectangle(cornerRadius: 12))
                    Text(result["morse"].stringValue).font(.system(size: 18, design: .monospaced)).textSelection(.enabled)
                        .frame(maxWidth: .infinity, minHeight: 70, alignment: .topLeading).padding(14)
                        .background(theme.result, in: RoundedRectangle(cornerRadius: 12)).environment(\.layoutDirection, .leftToRight)
                    Text(String(format: "%.0f Hz · %.1f WPM · %.1f s", result["frequencyHz"].doubleValue,
                                result["wordsPerMinute"].doubleValue, result["durationSeconds"].doubleValue)
                         + " · \(t("decoderUncertain")): \(result["uncertainCharacters"].intValue)").font(BrandTheme.body(13))
                    if result["timingAmbiguous"].boolValue { Text(t("decoderAmbiguous")).foregroundStyle(theme.muted) }
                    HStack {
                        Button(t("copy")) { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(result["text"].stringValue, forType: .string); message = t("copied") }
                        Button(t("save")) { saveText(result["text"].stringValue) }
                    }.buttonStyle(BrandButtonStyle(theme: theme))
                }.brandCard(theme)
            }
        }.onChange(of: useMicrophone) { decodeResult = nil; decodeURL = nil; if useMicrophone { refreshInputs() } }
    }

    private var voicesCard: some View {
        VStack(alignment: .leading, spacing: 18) {
            VStack(alignment: .leading, spacing: 12) {
                Text(t("voicesTitle")).font(BrandTheme.body(20))
                Text(t("voicesIntro")).foregroundStyle(theme.muted)
                Text(t("voicesDependencySize")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
                languagePicker.disabled(working)
                Text(pythonAvailable ? t("voicesRuntimeReady") : AudioCopy.runtimeMissing(settings.language)).foregroundStyle(theme.muted)
            }.brandCard(theme)
            ForEach(filteredVoices) { voice in
                VStack(alignment: .leading, spacing: 12) {
                    HStack { Text(voice.name).font(BrandTheme.body(20)); Spacer(); Text(t(voice.installed ? "voicesInstalled" : "voicesNotInstalled")).font(BrandTheme.body(13)) }
                    if voice.bytes > 0 { Text(t("voicesModelSize") + ": " + ByteCountFormatter.string(fromByteCount: Int64(voice.bytes), countStyle: .file)).font(BrandTheme.body(13)) }
                    Text(t(voice.engine == "piper" ? "voicesLicensePiper" : "voicesLicenseApache")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
                    if voice.language == "ar-SA" { Text(t("voicesArabicLicense")).font(BrandTheme.body(13)).foregroundStyle(theme.muted) }
                    if let url = URL(string: voice.source), url.scheme == "https" { Link(t("voicesSource"), destination: url) }
                    HStack {
                        if voice.installed {
                            Button(t("voicesPreview")) { preview(voice) }
                            Button(t(selectedVoice == voice.id ? "voicesSelected" : "voicesChoose")) { selectedVoice = voice.id; section = .speech }.disabled(selectedVoice == voice.id)
                            Button(t("voicesRemove"), role: .destructive) { pendingRemove = voice }
                        } else {
                            Button(t("voicesInstall"), systemImage: "arrow.down.circle") { pendingInstall = voice }
                        }
                    }.buttonStyle(BrandButtonStyle(theme: theme)).disabled(working)
                }.brandCard(theme)
            }
        }
    }

    private func run(message key: String, operation action: @escaping @MainActor () async throws -> Void) {
        guard !working else { return }
        working = true; message = t(key)
        operation = Task {
            defer { working = false; operation = nil }
            do { try await action(); if !Task.isCancelled { message = t("ready") } }
            catch is CancellationError { message = t("voicesCancelled") }
            catch MacAudioError.microphoneDenied { message = AudioCopy.microphoneDenied(settings.language) }
            catch { message = t(section == .decode ? "decoderNotMorse" : section == .morse ? "invalidMorse" : "voicesFailed") }
        }
    }

    private func cancel() {
        operation?.cancel(); capturePending = false; audio.stop()
    }

    private func morseAction(export: Bool) {
        let text = source, fromMorse = isMorse, selectedAlphabet = alphabet, separator = settings.separator
        let speed = UInt32(wpm), tone = UInt32(frequency)
        var destination: URL?
        if export {
            let panel = NSSavePanel(); panel.allowedContentTypes = [.wav]; panel.nameFieldStringValue = "morse.wav"
            guard panel.runModal() == .OK, let url = panel.url else { return }; destination = url
        }
        let output = destination ?? MacAudio.temporaryWAV()
        run(message: "busy") {
            do {
                try await Task.detached {
                    let morse = fromMorse ? text : try CoreConverter.convert(.init(text: text, direction: .encode, alphabet: selectedAlphabet, separator: separator)).text
                    try CoreAudio.writeWAV(url: output, morse: morse, separator: separator, wpm: speed, frequency: tone)
                }.value
                try Task.checkCancellation()
                if !export { try audio.play(url: output, temporary: true) }
            } catch { if !export { try? FileManager.default.removeItem(at: output) }; throw error }
        }
    }

    private func speak() {
        let text = source, fromMorse = isMorse, selectedAlphabet = alphabet, separator = settings.separator
        let voice = selectedVoice
        guard text.utf16.count <= 4000 else { message = t("speechLimit"); return }
        run(message: "speechPreparing") {
            let spoken = try await Task.detached {
                fromMorse ? try CoreConverter.convert(.init(text: text, direction: .decode, alphabet: selectedAlphabet, separator: separator)).text : text
            }.value
            try await synthesize(spoken, voice: voice)
        }
    }

    private func synthesize(_ text: String, voice: String) async throws {
        let url = MacAudio.temporaryWAV()
        do {
            _ = try await MacServices.shared.call("speech.synthesize", .object([
                "text": .string(text), "voiceId": .string(voice), "output": .string(url.path)]))
            try Task.checkCancellation(); try audio.play(url: url, temporary: true)
        } catch { try? FileManager.default.removeItem(at: url); throw error }
    }

    private func refreshVoices() async {
        do {
            let result = try await MacServices.shared.call("speech.status")
            try Task.checkCancellation()
            voices = result["voices"].arrayValue.map(MacVoice.init)
            pythonAvailable = !result["python"].stringValue.isEmpty
            selectAvailableVoice()
        } catch is CancellationError {} catch { message = t("voicesFailed") }
    }

    private func selectAvailableVoice() {
        if !filteredVoices.contains(where: { $0.id == selectedVoice }) {
            selectedVoice = filteredVoices.first(where: \.installed)?.id ?? filteredVoices.first?.id ?? ""
        }
    }

    private func install(_ voice: MacVoice) {
        run(message: "voicesPhase_dependencies") {
            _ = try await MacServices.shared.call("speech.install", .object(["voiceId": .string(voice.id)]))
            await refreshVoices()
        }
    }

    private func remove(_ voice: MacVoice) {
        run(message: "voicesPhase_remove") {
            audio.stop()
            _ = try await MacServices.shared.call("speech.remove", .object(["voiceId": .string(voice.id)]))
            await refreshVoices()
        }
    }

    private func preview(_ voice: MacVoice) {
        run(message: "voicesPhase_preview") { try await synthesize(AudioCopy.sample(voice.language), voice: voice.id) }
    }

    private func chooseWAV() {
        let panel = NSOpenPanel(); panel.allowedContentTypes = [.wav]; panel.allowsMultipleSelection = false
        if panel.runModal() == .OK { decodeURL = panel.url; decodeResult = nil }
    }

    private func beginCapture() {
        decodeResult = nil
        run(message: "decoderRecord") {
            try await audio.startRecording(deviceID: inputID)
            try Task.checkCancellation()
            capturePending = true
        }
    }

    private func finishCapture() {
        capturePending = false
        run(message: "decoderWorking") {
            guard let url = try await audio.finishRecording() else { return }
            defer { try? FileManager.default.removeItem(at: url) }
            let result = try await MacServices.shared.call("decoder.file", decodePayload(url))
            try Task.checkCancellation(); decodeResult = result
        }
    }

    private func refreshInputs() {
        inputs = MacAudio.inputs()
        if !inputs.contains(where: { $0.id == inputID }) { inputID = "" }
    }

    private func decodePayload(_ url: URL) -> ServiceJSON {
        var payload: [String: ServiceJSON] = ["path": .string(url.path), "alphabet": .number(Double(alphabet.rawValue)), "threshold": .number(threshold / 100)]
        if !autoTone { payload["frequencyHz"] = .number(Double(frequency)) }
        if !autoSpeed { payload["wpm"] = .number(Double(wpm)) }
        return .object(payload)
    }

    private func decode(url: URL, temporary: Bool) {
        decodeResult = nil
        let request = decodePayload(url)
        run(message: "decoderWorking") {
            defer { if temporary { try? FileManager.default.removeItem(at: url) } }
            let result = try await MacServices.shared.call("decoder.file", request)
            try Task.checkCancellation(); decodeResult = result
        }
    }

    private func saveText(_ text: String) {
        let panel = NSSavePanel(); panel.allowedContentTypes = [.plainText]; panel.nameFieldStringValue = "decoded-morse.txt"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        run(message: "busy") { try await Task.detached { try text.write(to: url, atomically: true, encoding: .utf8) }.value }
    }

    private func culture(_ language: AppLanguage) -> String {
        switch language { case .tr: "tr-TR"; case .en: "en-US"; case .de: "de-DE"; case .ru: "ru-RU"; case .ar: "ar-SA" }
    }
}
