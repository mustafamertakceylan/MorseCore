// swift-tools-version: 6.0
import PackageDescription
import Foundation

let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
let native = root.appendingPathComponent("../../build/mac-native").standardizedFileURL.path

let package = Package(
    name: "MorseCoreMac",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "MorseCoreMac", targets: ["MorseCoreMac"])],
    targets: [
        .systemLibrary(name: "CMorseCore"),
        .target(name: "MorseCoreBridge", dependencies: ["CMorseCore"],
            linkerSettings: [.unsafeFlags(["-L", native, "-Xlinker", "-rpath", "-Xlinker", native])]),
        .executableTarget(name: "MorseCoreMac", dependencies: ["MorseCoreBridge"],
            resources: [.copy("Resources")]),
        .testTarget(name: "MorseCoreBridgeTests", dependencies: ["MorseCoreBridge"]),
        .testTarget(name: "MorseCoreMacTests", dependencies: ["MorseCoreMac"])
    ]
)
