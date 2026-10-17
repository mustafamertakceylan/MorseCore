import AppKit
import CoreText
import Foundation

/// SwiftPM's command-line accessor can embed the builder's absolute path.
/// Installed apps always resolve their own standard Resources directory first.
enum MacResources {
    static let name = "MorseCoreMac_MorseCoreMac"

    static func packaged(in app: Bundle) -> Bundle? {
        guard let root = app.resourceURL else { return nil }
        return Bundle(url: root.appendingPathComponent(name + ".bundle"))
    }

    static let bundle: Bundle = {
        if let installed = packaged(in: .main) { return installed }
        // Swift tests and unbundled development executables use SwiftPM's bundle.
        // An installed app must never fall back to its build machine's resources.
        guard Bundle.main.bundleURL.pathExtension != "app" else {
            fatalError("MorseCore resources are missing from Contents/Resources.")
        }
        return .module
    }()

    static func verifyResources() throws -> [String: Any] {
        var languages: [String] = []
        for language in ["tr", "en", "de", "ru", "ar"] {
            guard let url = bundle.url(forResource: language, withExtension: "json", subdirectory: "Resources/Strings"),
                  let copy = try JSONSerialization.jsonObject(with: Data(contentsOf: url)) as? [String: String],
                  !copy.isEmpty else { throw CocoaError(.fileReadCorruptFile) }
            languages.append(language)
        }
        guard let logo = bundle.url(forResource: "morsecore", withExtension: "png", subdirectory: "Resources"),
              NSImage(contentsOf: logo) != nil else { throw CocoaError(.fileReadCorruptFile) }
        var fonts: [String] = []
        for name in ["AtkinsonHyperlegible-Regular", "CrimsonPro"] {
            guard let url = bundle.url(forResource: name, withExtension: "ttf", subdirectory: "Resources/Fonts"),
                  let descriptors = CTFontManagerCreateFontDescriptorsFromURL(url as CFURL),
                  CFArrayGetCount(descriptors) > 0 else { throw CocoaError(.fileReadCorruptFile) }
            fonts.append(name)
        }
        return ["ok": true, "resourceBundle": bundle.bundleURL.path, "languages": languages, "fonts": fonts, "logoDecoded": true]
    }
}
