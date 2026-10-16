import SwiftUI
import AppKit
import UniformTypeIdentifiers
import MorseCoreBridge

struct FileWorkspace: View {
    @Bindable var settings: AppSettings
    @Environment(\.colorScheme) private var scheme
    @State private var files: [URL] = []
    @State private var decode = false
    @State private var alphabet: Alphabet = .latin
    @State private var work: Task<Void, Never>?
    @State private var completed = 0
    @State private var attempted = 0
    @State private var failures: [String] = []
    @State private var notice = ""
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var busy: Bool { work != nil }

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(settings.text("fileLimit")).foregroundStyle(theme.muted)
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .top, spacing: 20) { options; destination }
                VStack(alignment: .leading, spacing: 20) { options; destination }
            }
            VStack(alignment: .leading, spacing: 18) {
                HStack {
                    Button { chooseFiles() } label: { Label(settings.text("chooseFiles"), systemImage: "folder.badge.plus") }
                        .buttonStyle(BrandButtonStyle(theme: theme)).disabled(busy)
                    Spacer()
                    if !files.isEmpty { Text("\(files.count)").monospacedDigit().foregroundStyle(theme.muted) }
                }
                if files.isEmpty {
                    Label(settings.text("chooseFiles"), systemImage: "doc.text")
                        .foregroundStyle(theme.muted).padding(.vertical, 18)
                } else {
                    ScrollView {
                        VStack(alignment: .leading, spacing: 10) {
                            ForEach(files, id: \.path) { file in
                                HStack {
                                    Label(file.lastPathComponent, systemImage: "doc.text").lineLimit(1)
                                    Spacer()
                                    Button { files.removeAll { $0 == file } } label: { Image(systemName: "xmark") }
                                        .buttonStyle(.borderless).disabled(busy).accessibilityLabel(settings.text("clear"))
                                }.help(file.path)
                            }
                        }
                    }.frame(maxHeight: 220)
                }
                HStack(spacing: 12) {
                    Button { start() } label: { Label(settings.text("convert"), systemImage: "arrow.triangle.2.circlepath") }
                        .buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
                        .disabled(busy || files.isEmpty || settings.outputDirectory.isEmpty)
                    if busy {
                        Button(settings.text("cancelUpdate")) { work?.cancel() }
                            .buttonStyle(BrandButtonStyle(theme: theme))
                    }
                    if attempted > 0 { Text("\(completed)/\(files.count) · \(settings.text("filesDone"))").monospacedDigit() }
                }
                if busy { ProgressView(value: Double(attempted), total: Double(max(files.count, 1))) }
                if !notice.isEmpty { Text(notice).foregroundStyle(theme.muted).accessibilityIdentifier("FileStatus") }
                if !failures.isEmpty {
                    Text(settings.text("fileError")).foregroundStyle(.red)
                    ForEach(failures, id: \.self) { Text($0).font(.caption).textSelection(.enabled).foregroundStyle(theme.muted) }
                }
            }.brandCard(theme)
        }
        .onDisappear { work?.cancel() }
    }

    private var options: some View {
        VStack(alignment: .leading, spacing: 16) {
            Picker(settings.text("conversionDirection"), selection: $decode) {
                Text(settings.text("textToMorse")).tag(false)
                Text(settings.text("morseToText")).tag(true)
            }.pickerStyle(.segmented)
            Picker(settings.text("conversionAlphabet"), selection: $alphabet) {
                Text(settings.text("latin")).tag(Alphabet.latin)
                Text(settings.text("russian")).tag(Alphabet.russian)
                Text(settings.text("arabic")).tag(Alphabet.arabic)
            }
        }.disabled(busy).frame(minWidth: 260).brandCard(theme)
    }

    private var destination: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text(settings.text("outputFolder")).font(BrandTheme.body(20))
            Text(settings.outputDirectory).font(.caption).textSelection(.enabled).lineLimit(3)
                .foregroundStyle(theme.muted).environment(\.layoutDirection, .leftToRight)
            HStack {
                Button(settings.text("chooseFolder")) { chooseFolder() }
                    .buttonStyle(BrandButtonStyle(theme: theme)).disabled(busy)
                Button { NSWorkspace.shared.open(URL(fileURLWithPath: settings.outputDirectory, isDirectory: true)) } label: {
                    Image(systemName: "folder")
                }.buttonStyle(.borderless).disabled(settings.outputDirectory.isEmpty).help(settings.text("outputFolder"))
            }
        }.frame(minWidth: 240).brandCard(theme)
    }

    private func chooseFiles() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.plainText]
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.begin { response in
            guard response == .OK else { return }
            files = Array(Set(panel.urls.filter { $0.pathExtension.lowercased() == "txt" })).sorted { $0.path < $1.path }
            completed = 0; attempted = 0; failures = []; notice = ""
        }
    }

    private func chooseFolder() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true; panel.canChooseFiles = false; panel.canCreateDirectories = true
        panel.begin { response in
            if response == .OK, let url = panel.url { settings.outputDirectory = url.path }
        }
    }

    private func start() {
        guard !busy, !files.isEmpty, !settings.outputDirectory.isEmpty else { return }
        let selected = files, folder = URL(fileURLWithPath: settings.outputDirectory, isDirectory: true)
        let direction = decode, profile = alphabet.rawValue, separator = settings.separator
        completed = 0; attempted = 0; failures = []; notice = ""
        work = Task { @MainActor in
            defer { work = nil }
            for file in selected {
                if Task.isCancelled { notice = settings.text("cancelled"); break }
                let target = folder.appendingPathComponent(file.deletingPathExtension().lastPathComponent + (direction ? ".text.txt" : ".morse.txt"))
                do {
                    _ = try await MacServices.shared.call("file.convert", .object([
                        "source": .string(file.path), "destination": .string(target.path),
                        "decode": .bool(direction), "alphabet": .number(Double(profile)), "separator": .number(Double(separator))
                    ]))
                    completed += 1
                } catch {
                    if Task.isCancelled { notice = settings.text("cancelled"); break }
                    failures.append(file.lastPathComponent + " · " + error.localizedDescription)
                }
                attempted += 1
            }
        }
    }
}
