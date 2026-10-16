import SwiftUI
import AppKit
import MorseCoreBridge

struct SettingsWorkspace: View {
    @Bindable var settings: AppSettings
    var openVoices: () -> Void = { }
    @Environment(\.colorScheme) private var scheme
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var l: Localizer { Localizer(language: settings.language) }
    private let columns = [GridItem(.flexible(), spacing: 16, alignment: .top), GridItem(.flexible(), spacing: 16, alignment: .top)]

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            LazyVGrid(columns: columns, alignment: .leading, spacing: 16) {
                appearance
                conversions
                voices
                UpdateWorkspace(settings: settings)
            }
            credits
            Text("MorseCore · macOS · \(CoreConverter.version) · Apache-2.0")
                .font(BrandTheme.body(12)).foregroundStyle(theme.muted)
        }
    }
    private var appearance: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(settings.text("uxAppearance")).font(BrandTheme.body(22))
            Picker(l(.theme), selection: $settings.theme) {
                Text(l(.system)).tag(AppTheme.system)
                Text(l(.light)).tag(AppTheme.light)
                Text(l(.dark)).tag(AppTheme.dark)
            }.accessibilityIdentifier("ThemeChoice")
            Picker(l(.language), selection: $settings.language) {
                ForEach(AppLanguage.allCases) { language in Text(language.name).tag(language) }
            }
            Toggle(settings.text("reduceMotion"), isOn: $settings.reduceMotion)
        }.brandCard(theme)
    }
    private var conversions: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text(settings.text("uxConversion")).font(BrandTheme.body(22))
            Picker(l(.alphabet), selection: $settings.alphabetRaw) {
                Text(l(.latin)).tag(0)
                Text(l(.russian)).tag(1)
                Text(l(.arabic)).tag(2)
            }
            Picker(l(.separator), selection: $settings.separator) {
                Text("/").tag(UInt32(47))
                Text("|").tag(UInt32(124))
                Text("#").tag(UInt32(35))
                Text("*").tag(UInt32(42))
            }
            Text(settings.text("outputFolder")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
            Text(settings.outputDirectory).font(BrandTheme.body(14)).textSelection(.enabled).lineLimit(2)
            Button(settings.text("chooseFolder")) { chooseFolder(speech: false) }
                .buttonStyle(BrandButtonStyle(theme: theme))
        }.brandCard(theme)
    }
    private var voices: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(settings.text("uxSpeechSettings")).font(BrandTheme.body(22))
            Text(settings.text("speechPackHelp")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
            Button(action: openVoices) {
                Label(settings.text("workspaceManageVoices"), systemImage: "arrow.down.circle")
            }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true))
            DisclosureGroup(settings.text("uxAdvanced")) {
                VStack(alignment: .leading, spacing: 12) {
                    Text(settings.text("speechPack"))
                    Text(settings.speechHome).font(BrandTheme.body(12)).foregroundStyle(theme.muted).textSelection(.enabled)
                    Button(settings.text("chooseSpeechPack")) { chooseFolder(speech: true) }
                        .buttonStyle(BrandButtonStyle(theme: theme))
                }.padding(.top, 12)
            }.accessibilityIdentifier("SpeechSettingsAdvanced")
        }.brandCard(theme)
    }
    private var credits: some View {
        DisclosureGroup(settings.text("thanksTitle")) {
            VStack(alignment: .leading, spacing: 14) {
                Text(settings.text("thanksBody")).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
                credit("EMA Lightning", author: "Canberk Aslan", license: "Apache-2.0", url: "https://github.com/canberk7/ema-lightning")
                credit("Antalia-2 Mini", author: "PatientDesk AI", license: "Apache-2.0", url: "https://huggingface.co/cloud0day3/antalia-mini")
                credit("Piper", author: "Open Home Foundation / Home Assistant contributors", license: "GPL-3.0-or-later", url: "https://github.com/OHF-Voice/piper1-gpl")
                credit("normalizer-tr", author: "Erdem Tuna", license: "Apache-2.0", url: "https://github.com/erdemtuna/normalizer-tr")
                credit("PyTorch", author: "PyTorch contributors", license: "BSD-3-Clause", url: "https://github.com/pytorch/pytorch")
                credit("ONNX Runtime", author: "Microsoft / ONNX Runtime contributors", license: "MIT", url: "https://github.com/microsoft/onnxruntime")
                Link(settings.text("voiceCredits"), destination: URL(string: "https://huggingface.co/rhasspy/piper-voices")!)
                Text(settings.text("creditsLicenseNote")).font(BrandTheme.body(12)).foregroundStyle(theme.muted)
            }.padding(.top, 12)
        }.brandCard(theme).accessibilityIdentifier("OpenSourceCredits")
    }
    private func credit(_ name: String, author: String, license: String, url: String) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Link(name, destination: URL(string: url)!).foregroundStyle(theme.accentInk)
            Text(author + " · " + license).font(BrandTheme.body(12)).foregroundStyle(theme.muted)
        }
    }
    private func chooseFolder(speech: Bool) {
        let panel = NSOpenPanel()
        panel.canChooseFiles = false; panel.canChooseDirectories = true
        panel.canCreateDirectories = true; panel.allowsMultipleSelection = false
        let current = speech ? settings.speechHome : settings.outputDirectory
        if !current.isEmpty { panel.directoryURL = URL(fileURLWithPath: current, isDirectory: true) }
        panel.begin { response in
            guard response == .OK, let url = panel.url else { return }
            Task { @MainActor in
                if speech { settings.speechHome = url.path }
                else { settings.outputDirectory = url.path }
            }
        }
    }
}
