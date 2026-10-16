import SwiftUI
import AppKit
import MorseCoreBridge

struct MacLaunchView: View {
    @Bindable var settings: AppSettings
    @Environment(\.accessibilityReduceMotion) private var systemReduceMotion
    @State private var ready = false
    @State private var progress = 10.0
    @State private var detail = "loadingSettings"
    private var reduceMotion: Bool { systemReduceMotion || settings.reduceMotion }

    var body: some View {
        ZStack {
            if ready {
                ContentView(settings: settings).transition(.opacity)
            } else {
                MacSplashView(settings: settings, progress: progress, detail: detail)
                    .transition(.opacity)
            }
        }
        .background { LaunchWindowControls(hidden: !ready).frame(width: 0, height: 0) }
        .task {
            // Render tests own their windows and preferences; don't delay those snapshots.
            if ProcessInfo.processInfo.environment["MORSECORE_UI_SMOKE_DIR"] != nil {
                await UISmoke.runIfRequested()
                return
            }
            do {
                try await Task.sleep(for: .milliseconds(100))
                _ = CoreConverter.version
                progress = 35
                detail = "loadingEngine"
                try await Task.sleep(for: .milliseconds(1_900))
                progress = 100
                withAnimation(reduceMotion ? nil : .easeOut(duration: 0.15)) { ready = true }
                await UpdateChecker.onLaunch(settings)
            } catch {
                // Closing the only window cancels its launch work.
                return
            }
        }
    }
}

/// The normal launch has no title bar or sidebar, just the shared RAW backdrop.
struct MacSplashView: View {
    @Bindable var settings: AppSettings
    var progress = 35.0
    var detail = "loadingEngine"
    @Environment(\.colorScheme) private var scheme
    @Environment(\.accessibilityReduceMotion) private var systemReduceMotion
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }

    var body: some View {
        ZStack {
            theme.canvas
            RawBackdrop(dark: theme.dark, reduceMotion: systemReduceMotion || settings.reduceMotion)
            VStack(spacing: 20) {
                if let url = Bundle.module.url(forResource: "morsecore", withExtension: "png", subdirectory: "Resources"),
                   let image = NSImage(contentsOf: url) {
                    Image(nsImage: image).resizable().scaledToFit().frame(width: 144, height: 144)
                        .background(Color(hex: 0xFAF8F2))
                        .clipShape(RoundedRectangle(cornerRadius: 28)).accessibilityHidden(true)
                }
                Text("MorseCore").font(BrandTheme.heading(54))
                Text(settings.text("loading")).font(BrandTheme.body(17))
                ProgressView(value: progress, total: 100).progressViewStyle(.linear)
                    .tint(theme.accent).frame(height: 4)
                    .accessibilityLabel(settings.text("loading"))
                Text(settings.text(detail)).font(BrandTheme.body(13)).foregroundStyle(theme.muted)
            }.frame(width: 330)
        }
        .foregroundStyle(theme.ink)
        .environment(\.locale, Locale(identifier: settings.language.rawValue))
        .environment(\.layoutDirection, settings.language.isRTL ? .rightToLeft : .leftToRight)
    }
}

/// A narrow AppKit escape for native window buttons, which SwiftUI cannot hide per launch phase.
private struct LaunchWindowControls: NSViewRepresentable {
    let hidden: Bool
    func makeNSView(context: Context) -> LaunchControlsView {
        let view = LaunchControlsView()
        view.hideButtons = hidden
        return view
    }
    func updateNSView(_ view: LaunchControlsView, context: Context) {
        view.hideButtons = hidden
        view.applyVisibility()
    }

    final class LaunchControlsView: NSView {
        var hideButtons = true
        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            applyVisibility()
        }
        func applyVisibility() {
            for type in [NSWindow.ButtonType.closeButton, .miniaturizeButton, .zoomButton] {
                window?.standardWindowButton(type)?.isHidden = hideButtons
            }
        }
    }
}
