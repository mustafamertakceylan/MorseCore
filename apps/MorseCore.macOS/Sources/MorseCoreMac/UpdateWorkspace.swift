import SwiftUI
import AppKit
import CryptoKit
import MorseCoreBridge

struct UpdateWorkspace: View {
    @Bindable var settings: AppSettings
    @Environment(\.colorScheme) private var scheme
    @State private var release: MacRelease?
    @State private var notice = ""
    @State private var failed = false
    @State private var work: Task<Void, Never>?
    @State private var downloaded: MacVerifiedUpdate?
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text(settings.text("updates")).font(BrandTheme.body(22))
            Text("\(settings.text("currentVersion")): \(CoreConverter.version)").foregroundStyle(theme.muted)
            Toggle(settings.text("updateNotice"), isOn: $settings.checkUpdates)
            HStack(spacing: 12) {
                Button(settings.text(work == nil ? "checkUpdates" : "checking")) { check() }
                    .buttonStyle(BrandButtonStyle(theme: theme)).disabled(work != nil)
                if work != nil {
                    ProgressView().controlSize(.small)
                    Button(settings.text("cancelUpdate")) { work?.cancel() }.buttonStyle(.borderless)
                }
            }
            if let release {
                if release.newer {
                    Text("\(settings.text("updateAvailable")) · \(release.version)").font(BrandTheme.body(20))
                    if let asset = release.installer, release.checksums != nil {
                        Button { download(release, asset) } label: {
                            Label(MacUpdateCopy.text("download", settings.language), systemImage: "arrow.down.circle")
                        }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(work != nil)
                        Text(MacUpdateCopy.text("manual", settings.language)).font(.caption).foregroundStyle(theme.muted)
                    }
                }
                if release.installer == nil || release.checksums == nil {
                    Text(MacUpdateCopy.text("missing", settings.language)).foregroundStyle(theme.muted)
                }
                Link(settings.text("openRelease"), destination: release.page)
                if !release.notes.isEmpty {
                    DisclosureGroup(MacUpdateCopy.text("notes", settings.language)) {
                        Text(release.notes).textSelection(.enabled).font(.caption).padding(.top, 10)
                    }
                }
            }
            if let downloaded {
                HStack(spacing: 12) {
                    Button(MacUpdateCopy.text("reveal", settings.language)) { NSWorkspace.shared.activateFileViewerSelecting([downloaded.url]) }
                        .buttonStyle(BrandButtonStyle(theme: theme))
                    if ["pkg", "dmg"].contains(downloaded.url.pathExtension.lowercased()) {
                        Button(MacUpdateCopy.text("openInstaller", settings.language)) {
                            openInstaller(downloaded)
                        }.buttonStyle(BrandButtonStyle(theme: theme, prominent: true)).disabled(work != nil)
                    }
                }
            }
            if !notice.isEmpty {
                Text(notice).foregroundStyle(failed ? .red : theme.muted).textSelection(.enabled)
                    .accessibilityIdentifier("UpdateStatus")
            }
        }.brandCard(theme).onDisappear { work?.cancel() }
    }

    private func check() {
        guard work == nil else { return }
        notice = ""; failed = false; release = nil; downloaded = nil
        work = Task { @MainActor in
            defer { work = nil }
            do {
                let found = try await MacUpdateClient.latest()
                try Task.checkCancellation()
                release = found
                settings.lastUpdateCheck = Date()
                settings.availableUpdateVersion = found.newer && found.installer != nil && found.checksums != nil ? found.version : ""
                if !found.newer { notice = settings.text("upToDate") }
            } catch {
                failed = !Task.isCancelled
                notice = settings.text(Task.isCancelled ? "cancelled" : "updateUnavailable")
            }
        }
    }

    private func download(_ release: MacRelease, _ asset: MacReleaseAsset) {
        guard work == nil else { return }
        notice = settings.text("updateDownloading"); failed = false
        work = Task { @MainActor in
            defer { work = nil }
            do {
                downloaded = try await MacUpdateClient.download(release: release, asset: asset)
                notice = MacUpdateCopy.text("verified", settings.language)
            } catch {
                failed = !Task.isCancelled
                notice = settings.text(Task.isCancelled ? "cancelled" : "updateFailed")
            }
        }
    }

    private func openInstaller(_ package: MacVerifiedUpdate) {
        guard work == nil else { return }
        work = Task { @MainActor in
            defer { work = nil }
            do {
                // A package can change after download; rehash immediately before
                // handing it to the operating system, outside the UI thread.
                try await Task.detached { try package.verify() }.value
                try Task.checkCancellation()
                guard NSWorkspace.shared.open(package.url) else { throw ServiceFailure(message: "Could not open installer.") }
            } catch {
                failed = !Task.isCancelled
                notice = settings.text(Task.isCancelled ? "cancelled" : "updateFailed")
            }
        }
    }
}

@MainActor
enum UpdateChecker {
    private static var checking = false

    static func onLaunch(_ settings: AppSettings) async {
        let environment = ProcessInfo.processInfo.environment
        guard environment["MORSECORE_UI_SMOKE_DIR"] == nil,
              environment["MORSECORE_DATA_ROOT"] == nil,
              settings.checkUpdates, !checking,
              Date().timeIntervalSince(settings.lastUpdateCheck) >= 24 * 60 * 60 else { return }
        checking = true
        defer { checking = false }
        do {
            let found = try await MacUpdateClient.latest()
            try Task.checkCancellation()
            guard settings.checkUpdates else { return }
            settings.lastUpdateCheck = Date()
            settings.availableUpdateVersion = found.newer && found.installer != nil && found.checksums != nil ? found.version : ""
        } catch {
            // Startup remains usable offline; a manual check can show an actionable error.
        }
    }
}

struct MacReleaseAsset: Sendable { let name: String }

struct MacRelease: Sendable {
    let version: String
    let page: URL
    let notes: String
    let newer: Bool
    let installer: MacReleaseAsset?
    let checksums: MacReleaseAsset?

    init(_ result: ServiceJSON) throws {
        guard result["found"].boolValue, !result["version"].stringValue.isEmpty,
              let page = URL(string: result["page"].stringValue), page.scheme == "https" else {
            throw ServiceFailure(message: "No stable release is available.")
        }
        self.version = result["version"].stringValue
        self.page = page; self.notes = result["notes"].stringValue
        self.newer = result["newer"].boolValue
        self.installer = result["installer"]["name"].stringValue.isEmpty ? nil : MacReleaseAsset(name: result["installer"]["name"].stringValue)
        self.checksums = result["checksums"]["name"].stringValue.isEmpty ? nil : MacReleaseAsset(name: result["checksums"]["name"].stringValue)
    }
}

struct MacVerifiedUpdate: Sendable {
    let url: URL
    let sha256: String

    func verify() throws {
        let input = try FileHandle(forReadingFrom: url); defer { try? input.close() }
        var hash = SHA256()
        while let bytes = try input.read(upToCount: 65_536), !bytes.isEmpty {
            try Task.checkCancellation(); hash.update(data: bytes)
        }
        guard hash.finalize().map({ String(format: "%02x", $0) }).joined() == sha256 else {
            throw ServiceFailure(message: "Downloaded installer was modified.")
        }
    }
}

// Shared AppCore owns release selection, HTTPS redirects, CPU/asset policy,
// bounded downloads and integrity checks. Swift adapts only the IPC and OS UI.
enum MacUpdateClient {
    // A slow network update must not hold the catalog/radio/audio helper queue.
    // This actor has no resident process while idle; jobs still exit on reply.
    private static let transport = MacServices()

    static func latest() async throws -> MacRelease {
        try MacRelease(await transport.call("updates.check", .object(["currentVersion": .string(CoreConverter.version)])))
    }

    static func download(release: MacRelease, asset: MacReleaseAsset, cacheDirectory: URL? = nil) async throws -> MacVerifiedUpdate {
        guard release.installer?.name == asset.name else { throw ServiceFailure(message: "Invalid update selection.") }
        let cache = cacheDirectory ?? FileManager.default.urls(for: .cachesDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("MorseCore/Updates", isDirectory: true)
        let result = try await transport.call("updates.download", .object([
            "version": .string(release.version), "cacheDirectory": .string(cache.path)
        ]))
        let path = result["path"].stringValue, digest = result["sha256"].stringValue
        guard path.hasPrefix("/"), result["version"].stringValue == release.version,
              digest.count == 64, digest.utf8.allSatisfy({ (48...57).contains($0) || (97...102).contains($0) }) else {
            throw ServiceFailure(message: "Invalid update service response.")
        }
        return MacVerifiedUpdate(url: URL(fileURLWithPath: path), sha256: digest)
    }

    // Opt-in smoke harness only: does not require a newer version, open an
    // installer, touch preferences, or use the normal user's update cache.
    static func verifyPublishedDownload(to directory: URL) async throws -> URL {
        let release = try await latest()
        guard let asset = release.installer, release.checksums != nil else {
            throw ServiceFailure(message: "Published macOS installer or checksums are missing.")
        }
        let package = try await download(release: release, asset: asset, cacheDirectory: directory)
        try await Task.detached { try package.verify() }.value
        let report = ServiceJSON.object([
            "version": .string(release.version), "asset": .string(asset.name),
            "path": .string(package.url.path), "sha256": .string(package.sha256),
            "verified": .bool(true)
        ])
        try JSONEncoder().encode(report).write(to: directory.appendingPathComponent("update-download.json"), options: .atomic)
        return package.url
    }
}

private enum MacUpdateCopy {
    static func text(_ key: String, _ language: AppLanguage) -> String {
        let copy: [String: [String]] = [
            "download": ["İndir ve doğrula", "Download and verify", "Laden und prüfen", "Скачать и проверить", "تنزيل والتحقق"],
            "manual": ["Doğrulamadan sonra dosyayı Finder’da açıp kurulumu kendin başlat. Açık uygulama otomatik kapatılmaz.", "After verification, reveal the file in Finder and start installation yourself. The app stays open.", "Nach der Prüfung die Datei im Finder anzeigen und selbst installieren. Die App bleibt geöffnet.", "После проверки откройте файл в Finder и запустите установку. Приложение останется открытым.", "بعد التحقق اعرض الملف في Finder وابدأ التثبيت بنفسك. يبقى التطبيق مفتوحًا."],
            "missing": ["Bu sürüm için uyumlu bir macOS kurulum paketi henüz yayımlanmamış.", "No compatible macOS installer has been published for this release yet.", "Für diese Version wurde noch kein passendes macOS-Paket veröffentlicht.", "Для этой версии пока нет совместимого установщика macOS.", "لم تُنشر حزمة تثبيت macOS متوافقة لهذا الإصدار بعد."],
            "notes": ["Sürüm notları", "Release notes", "Versionshinweise", "Примечания к выпуску", "ملاحظات الإصدار"],
            "reveal": ["Finder’da göster", "Show in Finder", "Im Finder zeigen", "Показать в Finder", "إظهار في Finder"],
            "openInstaller": ["Kurulumu aç", "Open installer", "Installation öffnen", "Открыть установщик", "فتح ملف التثبيت"],
            "verified": ["İndirme tamamlandı, SHA-256 doğrulandı. Kurulum dosyasını Finder’da açabilirsin.", "Download complete; SHA-256 verified. Reveal the installer in Finder to continue.", "Download abgeschlossen, SHA-256 geprüft. Zur Installation im Finder anzeigen.", "Загрузка завершена, SHA-256 проверен. Откройте установщик через Finder.", "اكتمل التنزيل والتحقق من SHA-256. اعرض ملف التثبيت في Finder للمتابعة."]
        ]
        let index: Int
        switch language { case .tr: index = 0; case .en: index = 1; case .de: index = 2; case .ru: index = 3; case .ar: index = 4 }
        return copy[key]?[index] ?? key
    }
}
