import SwiftUI
import AppKit

// Stable routes also let the isolated screenshot runner inspect every workspace.
enum MacPage: String, CaseIterable, Identifiable {
    case converter, files, audio, practice, alphabet, glossary, keying, stations, prepare, logbook, map, settings
    var id: String { rawValue }
    var textKey: String {
        switch self {
        case .stations: "stationProfiles"
        case .prepare: "qsoAssistant"
        case .logbook: "qsoLog"
        case .map: "logbook.map"
        default: rawValue
        }
    }
    var education: Bool { [.practice, .alphabet, .glossary, .keying].contains(self) }
    var radio: Bool { [.stations, .prepare, .logbook, .map].contains(self) }
}

struct ContentView: View {
    @Bindable var settings: AppSettings
    @Environment(\.colorScheme) private var colorScheme
    @Environment(\.accessibilityReduceMotion) private var systemReduceMotion
    @State private var converterState = ConverterState()
    @State private var route: MacPage
    @State private var educationExpanded = false
    @State private var radioExpanded = false
    private var theme: BrandTheme { BrandTheme(dark: colorScheme == .dark) }
    private var reduceMotion: Bool { systemReduceMotion || settings.reduceMotion }

    init(settings: AppSettings, settingsVisible: Bool = false, route: MacPage = .converter) {
        self.settings = settings
        _route = State(initialValue: settingsVisible ? .settings : route)
        _educationExpanded = State(initialValue: route.education)
        _radioExpanded = State(initialValue: route.radio)
    }

    var body: some View {
        VStack(spacing: 0) {
            titleBar
            HStack(spacing: 0) {
                sidebar
                workspace
                    .padding(.horizontal, 12).padding(.vertical, 10)
            }
            .background { RawBackdrop(dark: theme.dark, reduceMotion: reduceMotion) }
        }
        .background(theme.canvas)
        .foregroundStyle(theme.ink).font(BrandTheme.body()).tint(theme.accentInk)
        .environment(\.locale, Locale(identifier: settings.language.rawValue))
        .environment(\.layoutDirection, settings.language.isRTL ? .rightToLeft : .leftToRight)
    }

    private var titleBar: some View {
        ZStack {
            HStack(spacing: 9) {
                if let url = MacResources.bundle.url(forResource: "morsecore", withExtension: "png", subdirectory: "Resources"),
                   let image = NSImage(contentsOf: url) {
                    Image(nsImage: image).resizable().scaledToFit()
                        .frame(width: 24, height: 24)
                        .background(Color(hex: 0xFAF8F2), in: RoundedRectangle(cornerRadius: 9))
                        .clipShape(RoundedRectangle(cornerRadius: 9))
                        .accessibilityHidden(true)
                }
                Text("MorseCore").font(BrandTheme.heading(20))
            }
        }
        .frame(height: 36).frame(maxWidth: .infinity).background(theme.canvas)
        .environment(\.layoutDirection, .leftToRight)
    }

    private var sidebar: some View {
        VStack(spacing: 8) {
            SidebarRow(title: settings.text(settings.collapsed ? "openPane" : "closePane"),
                       symbol: settings.language.isRTL ? "sidebar.right" : "sidebar.left",
                       theme: theme, compact: settings.collapsed, reduceMotion: reduceMotion) {
                settings.collapsed.toggle()
            }
            .accessibilityIdentifier("PaneToggle")
            ScrollView {
                VStack(spacing: 4) {
                    nav(.converter, symbol: "character.bubble")
                    nav(.files, symbol: "folder")
                    nav(.audio, symbol: "music.note")
                    group("education", symbol: "graduationcap", expanded: $educationExpanded,
                          pages: [.alphabet, .practice, .keying, .glossary])
                    group("radio", symbol: "antenna.radiowaves.left.and.right", expanded: $radioExpanded,
                          pages: [.stations, .prepare, .logbook, .map])
                }
            }.scrollIndicators(.hidden)
            nav(.settings, symbol: "gearshape").keyboardShortcut(",", modifiers: .command)
        }
        .padding(.horizontal, 10).padding(.top, 8).padding(.bottom, 12)
        .frame(width: settings.collapsed ? 72 : 240)
        .background(theme.paneSurface)
        .animation(reduceMotion ? nil : .easeInOut(duration: 0.18), value: settings.collapsed)
    }

    private func nav(_ page: MacPage, symbol: String) -> some View {
        SidebarRow(title: settings.text(page.textKey), symbol: symbol, theme: theme,
                   selected: route == page, compact: settings.collapsed, reduceMotion: reduceMotion) { navigate(page) }
            .accessibilityIdentifier("Nav-" + page.textKey)
    }

    @ViewBuilder private func group(_ key: String, symbol: String, expanded: Binding<Bool>, pages: [MacPage]) -> some View {
        if settings.collapsed {
            CompactSidebarGroup(title: settings.text(key), symbol: symbol, settings: settings, theme: theme,
                                pages: pages, route: route, reduceMotion: reduceMotion, navigate: navigate)
                .accessibilityIdentifier("Nav-" + key)
        } else {
            SidebarRow(title: settings.text(key), symbol: symbol, theme: theme,
                       expanded: expanded.wrappedValue, reduceMotion: reduceMotion) {
                withAnimation(reduceMotion ? nil : .easeInOut(duration: 0.18)) { expanded.wrappedValue.toggle() }
            }.accessibilityIdentifier("Nav-" + key)
            if expanded.wrappedValue {
                SidebarChildren(theme: theme) {
                    ForEach(pages) { page in
                        SidebarRow(title: settings.text(page.textKey), theme: theme,
                                   selected: route == page, child: true, reduceMotion: reduceMotion) { navigate(page) }
                            .accessibilityIdentifier("Nav-" + page.textKey)
                    }
                }
            }
        }
    }

    private var workspace: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text(settings.text(route.textKey)).font(BrandTheme.heading(28)).accessibilityAddTraits(.isHeader)
                .accessibilityIdentifier("WorkspaceTitle")
            ScrollViewReader { proxy in
                ScrollView {
                    PageEntrance(reduceMotion: reduceMotion) { pageContent }
                        .id(route).frame(maxWidth: .infinity, alignment: .leading).padding(.bottom, 8)
                }
                .onChange(of: route) { _, next in proxy.scrollTo(next, anchor: .top) }
            }
            HStack {
                Text(settings.text("ready"))
                Spacer()
                if !settings.availableUpdateVersion.isEmpty {
                    Button(settings.text("updateAvailable") + " · " + settings.availableUpdateVersion) { navigate(.settings) }
                        .buttonStyle(.plain)
                }
            }.font(BrandTheme.body(13)).foregroundStyle(theme.muted)
        }
        .padding(.horizontal, 24).padding(.top, 14).padding(.bottom, 12)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .background(theme.workspaceSurface, in: RoundedRectangle(cornerRadius: 18))
    }

    @ViewBuilder private var pageContent: some View {
        switch route {
        case .converter:
            ConverterWorkspace(state: converterState, settings: settings) { text, morse in
                settings.audioText = text; settings.audioIsMorse = morse; settings.audioSectionRaw = "morse"; navigate(.audio)
            }
        case .files: FileWorkspace(settings: settings)
        case .audio: AudioWorkspace(settings: settings)
        case .alphabet: EducationWorkspace(settings: settings, section: .alphabet)
        case .practice: EducationWorkspace(settings: settings, section: .practice)
        case .keying: EducationWorkspace(settings: settings, section: .keying)
        case .glossary: EducationWorkspace(settings: settings, section: .glossary)
        case .stations: RadioWorkspace(settings: settings, section: .stations)
        case .prepare: RadioWorkspace(settings: settings, section: .prepare)
        case .logbook: RadioWorkspace(settings: settings, section: .logbook)
        case .map: RadioWorkspace(settings: settings, section: .map)
        case .settings:
            SettingsWorkspace(settings: settings) {
                settings.audioSectionRaw = "voices"; navigate(.audio)
            }
        }
    }

    private func navigate(_ next: MacPage) {
        guard route != next else { return }
        MacAudio.shared.stop()
        route = next
        if next.education { educationExpanded = true }
        if next.radio { radioExpanded = true }
    }
}
