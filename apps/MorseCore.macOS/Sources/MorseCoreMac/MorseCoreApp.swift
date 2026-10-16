import SwiftUI
import CoreText

@main
struct MorseCoreApp: App {
    @State private var settings = AppSettings()

    init() {
        for name in ["AtkinsonHyperlegible-Regular", "CrimsonPro"] {
            if let url = Bundle.module.url(forResource: name, withExtension: "ttf", subdirectory: "Resources/Fonts") {
                CTFontManagerRegisterFontsForURL(url as CFURL, .process, nil)
            }
        }
    }

    var body: some Scene {
        Window("MorseCore", id: "main") {
            MacLaunchView(settings: settings)
                .frame(width: 1340, height: 840)
                .preferredColorScheme(settings.theme.colorScheme)
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1340, height: 840)
        .windowResizability(.contentSize)
        .defaultPosition(.center)
        .commands {
            CommandGroup(replacing: .newItem) { }
        }
    }
}
