import SwiftUI

/// Hover and press feedback belong to one row, never the entire navigation tree.
struct SidebarRow: View {
    let title: String
    var symbol: String? = nil
    let theme: BrandTheme
    var selected = false
    var compact = false
    var child = false
    var expanded: Bool? = nil
    var reduceMotion = false
    let action: () -> Void
    @State private var hovered = false

    var body: some View {
        Button(action: action) {
            HStack(spacing: 11) {
                if let symbol {
                    Image(systemName: symbol).font(.system(size: 17))
                        .frame(width: 20, height: 20)
                } else if !child {
                    Color.clear.frame(width: 20, height: 20)
                }
                if !compact {
                    Text(title).font(BrandTheme.body(child ? 13 : 14))
                        .fontWeight(child && !selected ? .regular : .semibold).lineLimit(1)
                    Spacer(minLength: 4)
                    if let expanded {
                        Image(systemName: "chevron.down").font(.system(size: 10, weight: .semibold))
                            .rotationEffect(.degrees(expanded ? 180 : 0)).frame(width: 12)
                    }
                }
            }
            .frame(maxWidth: .infinity, alignment: compact ? .center : .leading)
        }
        .buttonStyle(SidebarRowStyle(theme: theme, selected: selected, hovered: hovered,
                                     child: child, reduceMotion: reduceMotion))
        .onHover { hovered = $0 }
        .help(title).accessibilityLabel(title)
        .accessibilityAddTraits(selected ? .isSelected : [])
    }
}

struct SidebarRowStyle: ButtonStyle {
    let theme: BrandTheme
    var selected = false
    var hovered = false
    var child = false
    var reduceMotion = false

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .padding(.horizontal, 12).frame(height: child ? 36 : 44)
            .foregroundStyle(selected ? theme.accentInk : theme.ink)
            .background {
                RoundedRectangle(cornerRadius: 9)
                    .fill(selected ? (child ? theme.accentInk.opacity(theme.dark ? 0.14 : 0.10) : theme.card)
                          : theme.accentInk.opacity(configuration.isPressed ? 0.10 : (hovered ? 0.04 : 0)))
            }
            .overlay(alignment: .leading) {
                if selected && !child { Rectangle().fill(theme.accentInk).frame(width: 2, height: 20) }
            }
            .contentShape(Rectangle())
            .scaleEffect(configuration.isPressed ? 0.985 : 1)
            .animation(reduceMotion ? nil : .easeOut(duration: 0.10), value: configuration.isPressed)
    }
}

/// The guide occupies the icon column; child feedback stays entirely beside it.
struct SidebarChildren<Content: View>: View {
    let theme: BrandTheme
    @ViewBuilder let content: () -> Content

    var body: some View {
        VStack(spacing: 2, content: content)
            .padding(.leading, 31)
            .background(alignment: .leading) {
                Rectangle().fill(theme.line).frame(width: 1)
                    .padding(.vertical, 6).padding(.leading, 22)
                    .allowsHitTesting(false).accessibilityHidden(true)
            }
    }
}

/// Native Menu chrome changes icon size and alignment in a narrow sidebar.
/// Keep the trigger identical to every other row and present its children separately.
struct CompactSidebarGroup: View {
    let title: String
    let symbol: String
    let settings: AppSettings
    let theme: BrandTheme
    let pages: [MacPage]
    let route: MacPage
    let reduceMotion: Bool
    let navigate: (MacPage) -> Void
    @State private var presented = false

    var body: some View {
        SidebarRow(title: title, symbol: symbol, theme: theme, compact: true, reduceMotion: reduceMotion) {
            presented.toggle()
        }
        .popover(isPresented: $presented, arrowEdge: settings.language.isRTL ? .trailing : .leading) {
            VStack(spacing: 4) {
                ForEach(pages) { page in
                    SidebarRow(title: settings.text(page.textKey), theme: theme, selected: route == page,
                               reduceMotion: reduceMotion) {
                        presented = false
                        navigate(page)
                    }
                    .accessibilityIdentifier("Compact-" + page.textKey)
                }
            }
            .padding(8).frame(width: 230).background(theme.canvas)
        }
    }
}

/// Only the new page's opacity and position animate; navigation colors and old pages do not.
struct PageEntrance<Content: View>: View {
    let reduceMotion: Bool
    @ViewBuilder let content: () -> Content
    @State private var visible = false

    var body: some View {
        content()
            .opacity(reduceMotion || visible ? 1 : 0)
            .offset(y: reduceMotion || visible ? 0 : 6)
            .onAppear {
                withAnimation(reduceMotion ? nil : .easeOut(duration: 0.16)) { visible = true }
            }
    }
}
