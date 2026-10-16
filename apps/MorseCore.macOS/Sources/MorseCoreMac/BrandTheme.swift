import SwiftUI

// These values match the Windows application's App.xaml theme resources.
struct BrandTheme {
    let dark: Bool
    var canvas: Color { color(0xFAF8F2, 0x283322) }
    var pane: Color { color(0xF7F6F0, 0x5E6A42) }
    var workspace: Color { color(0xEEEFE5, 0x2D3A27) }
    var card: Color { color(0xFCFBF7, 0x4C5C40) }
    var input: Color { color(0xFFFFFF, 0x23311F) }
    var result: Color { color(0xF0F3E8, 0x35472C) }
    var ink: Color { color(0x2C2A22, 0xFAF8F2) }
    var muted: Color { color(0x78716C, 0xE1E6D8) }
    var line: Color { color(0xD5D9C9, 0x8FA17A) }
    var accent: Color { Color(hex: 0x5E6A42) }
    var selected: Color { dark ? Color(hex: 0xFAF8F2) : accent.opacity(31.0 / 255.0) }
    var selectedInk: Color { Color(hex: 0x4E5836) }
    var accentInk: Color { color(0x4E5836, 0xF0F4E7) }
    var paneSurface: Color { pane.opacity(dark ? 237.0 / 255.0 : 191.0 / 255.0) }
    var workspaceSurface: Color { workspace.opacity(dark ? 242.0 / 255.0 : 184.0 / 255.0) }

    private func color(_ light: UInt32, _ dark: UInt32) -> Color {
        Color(hex: self.dark ? dark : light)
    }
    static func body(_ size: CGFloat = 16) -> Font {
        .custom("AtkinsonHyperlegible-Regular", size: size)
    }
    static func heading(_ size: CGFloat = 28) -> Font {
        .custom("CrimsonPro-Regular", size: size)
    }
}

extension Color {
    init(hex: UInt32) {
        self.init(.sRGB, red: Double((hex >> 16) & 255) / 255,
                  green: Double((hex >> 8) & 255) / 255,
                  blue: Double(hex & 255) / 255, opacity: 1)
    }
}

struct BrandButtonStyle: ButtonStyle {
    @Environment(\.isEnabled) private var isEnabled
    let theme: BrandTheme
    var prominent = false
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .padding(.horizontal, 12).padding(.vertical, 8).frame(minHeight: 38)
            .foregroundStyle(prominent ? (theme.dark ? theme.selectedInk : Color(hex: 0xFAF8F2)) : theme.ink)
            .background(prominent ? (theme.dark ? Color(hex: 0xFAF8F2) : theme.accent) : theme.card, in: RoundedRectangle(cornerRadius: 10))
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(theme.line, lineWidth: prominent ? 0 : 1))
            .opacity(isEnabled ? (configuration.isPressed ? 0.75 : 1) : 0.45)
    }
}

extension View {
    func brandCard(_ theme: BrandTheme) -> some View {
        frame(maxWidth: .infinity, alignment: .leading)
            .padding(20)
            .background(theme.card, in: RoundedRectangle(cornerRadius: 16))
            .overlay(RoundedRectangle(cornerRadius: 16).stroke(theme.line, lineWidth: 1))
    }
}
