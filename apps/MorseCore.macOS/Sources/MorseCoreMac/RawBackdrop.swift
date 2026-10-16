import SwiftUI

/// Exact corner geometry and sizing from the Windows RawBackdrop / RAW SiteBackdrop.
struct RawBackdrop: View {
    let dark: Bool
    let reduceMotion: Bool
    @Environment(\.scenePhase) private var scenePhase
    @State private var windowActive = true
    private var moving: Bool { !reduceMotion && scenePhase == .active && windowActive }
    var body: some View {
        TimelineView(.animation(minimumInterval: 1.0 / 30.0, paused: !moving)) { timeline in
            Canvas { context, size in
                let t = moving ? timeline.date.timeIntervalSinceReferenceDate : 0
                let widths = [min(size.height * 0.42, min(720, max(250, size.width * 0.33))),
                              min(size.height * 0.28, min(520, max(180, size.width * 0.27))),
                              min(size.height * 0.36, min(520, max(190, size.width * 0.24))),
                              min(size.height * 0.38, min(620, max(220, size.width * 0.30)))]
                let bounds: [CGSize] = [.init(width: 900, height: 780), .init(width: 570, height: 900),
                                        .init(width: 700, height: 639), .init(width: 700, height: 495)]
                let shifts: [CGPoint] = [.zero, .init(x: -1430, y: 0), .init(x: 0, y: -486), .init(x: -1300, y: -630)]
                let offsets: [(Double, Double, Double)] = [(7, -5, 16), (-6, 6, 19), (6, 5, 21), (-7, -6, 17)]
                for corner in 0..<4 {
                    var layer = context
                    let scale = widths[corner] / bounds[corner].width
                    let height = bounds[corner].height * scale
                    let wave = moving ? (1 - cos(t * 2 * .pi / offsets[corner].2)) / 2 : 0
                    layer.translateBy(x: (corner == 1 || corner == 3 ? size.width - widths[corner] + 10 : -10) + offsets[corner].0 * wave,
                                      y: (corner >= 2 ? size.height - height + 10 : -10) + offsets[corner].1 * wave)
                    layer.scaleBy(x: scale, y: scale)
                    layer.translateBy(x: shifts[corner].x, y: shifts[corner].y)
                    for (index, item) in Self.paths[corner].enumerated() {
                        var paint = layer
                        paint.opacity = item.opacity * (dark ? 0.38 : 0.85)
                        if item.stroke > 0 {
                            paint.stroke(item.path, with: .color(item.color), style: StrokeStyle(lineWidth: item.stroke, dash: item.dashed ? [7, 11] : []))
                        } else if index == 2 && corner < 3 {
                            let bounds = item.path.boundingRect
                            let colors: [[Color]] = [[Color(hex: 0x8D9479), Color(hex: 0xA9AE96)],
                                                    [Color(hex: 0x949A80), Color(hex: 0xB2B6A0)],
                                                    [Color(hex: 0x7F876C), Color(hex: 0xA3A891)]]
                            let start = CGPoint(x: corner == 1 ? bounds.maxX : bounds.minX,
                                                y: corner == 2 ? bounds.maxY : bounds.minY)
                            let end = CGPoint(x: corner == 1 ? bounds.minX : bounds.maxX,
                                              y: corner == 2 ? bounds.minY : bounds.maxY)
                            paint.fill(item.path, with: .linearGradient(Gradient(colors: colors[corner]), startPoint: start, endPoint: end))
                        } else { paint.fill(item.path, with: .color(item.color)) }
                    }
                }
            }
        }
        .clipped().allowsHitTesting(false).accessibilityHidden(true)
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didBecomeKeyNotification)) { _ in windowActive = true }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didResignKeyNotification)) { _ in windowActive = false }
    }
    private struct RawPath {
        let path: Path
        let color: Color
        let opacity: Double
        let stroke: CGFloat
        let dashed: Bool
    }
    private static let paths: [[RawPath]] = [
        [
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 0.0)); p.addLine(to: CGPoint(x: 640.0, y: 0.0)); p.addCurve(to: CGPoint(x: 404.0, y: 250.0), control1: CGPoint(x: 580.0, y: 120.0), control2: CGPoint(x: 470.0, y: 176.0)); p.addCurve(to: CGPoint(x: 250.0, y: 494.0), control1: CGPoint(x: 332.0, y: 330.0), control2: CGPoint(x: 322.0, y: 424.0)); p.addCurve(to: CGPoint(x: 0.0, y: 636.0), control1: CGPoint(x: 176.0, y: 566.0), control2: CGPoint(x: 78.0, y: 588.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xEAE3D1), opacity: 0.78, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 0.0)); p.addLine(to: CGPoint(x: 470.0, y: 0.0)); p.addCurve(to: CGPoint(x: 296.0, y: 214.0), control1: CGPoint(x: 420.0, y: 96.0), control2: CGPoint(x: 344.0, y: 150.0)); p.addCurve(to: CGPoint(x: 176.0, y: 438.0), control1: CGPoint(x: 240.0, y: 288.0), control2: CGPoint(x: 232.0, y: 372.0)); p.addCurve(to: CGPoint(x: 0.0, y: 566.0), control1: CGPoint(x: 122.0, y: 500.0), control2: CGPoint(x: 56.0, y: 520.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xDED6C2), opacity: 0.72, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 0.0)); p.addLine(to: CGPoint(x: 210.0, y: 0.0)); p.addCurve(to: CGPoint(x: 138.0, y: 224.0), control1: CGPoint(x: 176.0, y: 78.0), control2: CGPoint(x: 150.0, y: 150.0)); p.addCurve(to: CGPoint(x: 60.0, y: 400.0), control1: CGPoint(x: 126.0, y: 300.0), control2: CGPoint(x: 108.0, y: 352.0)); p.addCurve(to: CGPoint(x: 0.0, y: 486.0), control1: CGPoint(x: 34.0, y: 426.0), control2: CGPoint(x: 14.0, y: 452.0)); p.closeSubpath(); return p }(), color: Color(hex: 0x8D9479), opacity: 0.42, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 748.0, y: 0.0)); p.addCurve(to: CGPoint(x: 664.0, y: 212.0), control1: CGPoint(x: 702.0, y: 84.0), control2: CGPoint(x: 708.0, y: 154.0)); p.addCurve(to: CGPoint(x: 440.0, y: 364.0), control1: CGPoint(x: 608.0, y: 286.0), control2: CGPoint(x: 496.0, y: 298.0)); p.addCurve(to: CGPoint(x: 350.0, y: 568.0), control1: CGPoint(x: 380.0, y: 434.0), control2: CGPoint(x: 404.0, y: 510.0)); p.addCurve(to: CGPoint(x: 118.0, y: 662.0), control1: CGPoint(x: 298.0, y: 624.0), control2: CGPoint(x: 192.0, y: 620.0)); p.addCurve(to: CGPoint(x: 0.0, y: 738.0), control1: CGPoint(x: 76.0, y: 686.0), control2: CGPoint(x: 36.0, y: 700.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.85, stroke: 2.4, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 566.0, y: 0.0)); p.addCurve(to: CGPoint(x: 504.0, y: 178.0), control1: CGPoint(x: 532.0, y: 74.0), control2: CGPoint(x: 540.0, y: 130.0)); p.addCurve(to: CGPoint(x: 334.0, y: 304.0), control1: CGPoint(x: 460.0, y: 238.0), control2: CGPoint(x: 376.0, y: 252.0)); p.addCurve(to: CGPoint(x: 268.0, y: 460.0), control1: CGPoint(x: 288.0, y: 360.0), control2: CGPoint(x: 310.0, y: 418.0)); p.addCurve(to: CGPoint(x: 96.0, y: 532.0), control1: CGPoint(x: 226.0, y: 502.0), control2: CGPoint(x: 152.0, y: 502.0)); p.addCurve(to: CGPoint(x: 0.0, y: 582.0), control1: CGPoint(x: 60.0, y: 552.0), control2: CGPoint(x: 26.0, y: 568.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.55, stroke: 2, dashed: true)
        ],
        [
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 0.0)); p.addLine(to: CGPoint(x: 1548.0, y: 0.0)); p.addCurve(to: CGPoint(x: 1642.0, y: 272.0), control1: CGPoint(x: 1596.0, y: 96.0), control2: CGPoint(x: 1638.0, y: 186.0)); p.addCurve(to: CGPoint(x: 1614.0, y: 496.0), control1: CGPoint(x: 1646.0, y: 368.0), control2: CGPoint(x: 1596.0, y: 424.0)); p.addCurve(to: CGPoint(x: 1790.0, y: 668.0), control1: CGPoint(x: 1634.0, y: 578.0), control2: CGPoint(x: 1722.0, y: 620.0)); p.addCurve(to: CGPoint(x: 2000.0, y: 812.0), control1: CGPoint(x: 1866.0, y: 722.0), control2: CGPoint(x: 1944.0, y: 764.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xEAE3D1), opacity: 0.76, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 0.0)); p.addLine(to: CGPoint(x: 1712.0, y: 0.0)); p.addCurve(to: CGPoint(x: 1774.0, y: 240.0), control1: CGPoint(x: 1748.0, y: 88.0), control2: CGPoint(x: 1774.0, y: 168.0)); p.addCurve(to: CGPoint(x: 1752.0, y: 420.0), control1: CGPoint(x: 1774.0, y: 318.0), control2: CGPoint(x: 1738.0, y: 362.0)); p.addCurve(to: CGPoint(x: 1884.0, y: 556.0), control1: CGPoint(x: 1768.0, y: 486.0), control2: CGPoint(x: 1834.0, y: 518.0)); p.addCurve(to: CGPoint(x: 2000.0, y: 656.0), control1: CGPoint(x: 1934.0, y: 594.0), control2: CGPoint(x: 1974.0, y: 626.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xDDD5C1), opacity: 0.68, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 0.0)); p.addLine(to: CGPoint(x: 1846.0, y: 0.0)); p.addCurve(to: CGPoint(x: 1890.0, y: 204.0), control1: CGPoint(x: 1872.0, y: 68.0), control2: CGPoint(x: 1890.0, y: 140.0)); p.addCurve(to: CGPoint(x: 1876.0, y: 350.0), control1: CGPoint(x: 1890.0, y: 274.0), control2: CGPoint(x: 1866.0, y: 306.0)); p.addCurve(to: CGPoint(x: 1966.0, y: 452.0), control1: CGPoint(x: 1888.0, y: 400.0), control2: CGPoint(x: 1934.0, y: 424.0)); p.addCurve(to: CGPoint(x: 2000.0, y: 500.0), control1: CGPoint(x: 1986.0, y: 470.0), control2: CGPoint(x: 1996.0, y: 486.0)); p.closeSubpath(); return p }(), color: Color(hex: 0x949A80), opacity: 0.44, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 1468.0, y: 0.0)); p.addCurve(to: CGPoint(x: 1550.0, y: 244.0), control1: CGPoint(x: 1516.0, y: 92.0), control2: CGPoint(x: 1554.0, y: 170.0)); p.addCurve(to: CGPoint(x: 1514.0, y: 434.0), control1: CGPoint(x: 1546.0, y: 322.0), control2: CGPoint(x: 1502.0, y: 370.0)); p.addCurve(to: CGPoint(x: 1656.0, y: 596.0), control1: CGPoint(x: 1528.0, y: 510.0), control2: CGPoint(x: 1600.0, y: 548.0)); p.addCurve(to: CGPoint(x: 1808.0, y: 766.0), control1: CGPoint(x: 1716.0, y: 650.0), control2: CGPoint(x: 1762.0, y: 706.0)); p.addCurve(to: CGPoint(x: 2000.0, y: 876.0), control1: CGPoint(x: 1850.0, y: 820.0), control2: CGPoint(x: 1914.0, y: 850.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.85, stroke: 2.4, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 1644.0, y: 0.0)); p.addCurve(to: CGPoint(x: 1708.0, y: 208.0), control1: CGPoint(x: 1684.0, y: 80.0), control2: CGPoint(x: 1712.0, y: 146.0)); p.addCurve(to: CGPoint(x: 1680.0, y: 368.0), control1: CGPoint(x: 1704.0, y: 274.0), control2: CGPoint(x: 1670.0, y: 314.0)); p.addCurve(to: CGPoint(x: 1796.0, y: 502.0), control1: CGPoint(x: 1692.0, y: 428.0), control2: CGPoint(x: 1750.0, y: 462.0)); p.addCurve(to: CGPoint(x: 1926.0, y: 644.0), control1: CGPoint(x: 1848.0, y: 548.0), control2: CGPoint(x: 1888.0, y: 594.0)); p.addCurve(to: CGPoint(x: 2000.0, y: 722.0), control1: CGPoint(x: 1956.0, y: 684.0), control2: CGPoint(x: 1978.0, y: 708.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.5, stroke: 2, dashed: true)
        ],
        [
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 1125.0)); p.addLine(to: CGPoint(x: 0.0, y: 596.0)); p.addCurve(to: CGPoint(x: 244.0, y: 842.0), control1: CGPoint(x: 118.0, y: 648.0), control2: CGPoint(x: 202.0, y: 738.0)); p.addCurve(to: CGPoint(x: 470.0, y: 1074.0), control1: CGPoint(x: 288.0, y: 952.0), control2: CGPoint(x: 366.0, y: 1030.0)); p.addCurve(to: CGPoint(x: 568.0, y: 1125.0), control1: CGPoint(x: 512.0, y: 1092.0), control2: CGPoint(x: 546.0, y: 1108.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xEAE3D1), opacity: 0.8, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 1125.0)); p.addLine(to: CGPoint(x: 0.0, y: 700.0)); p.addCurve(to: CGPoint(x: 178.0, y: 892.0), control1: CGPoint(x: 86.0, y: 742.0), control2: CGPoint(x: 148.0, y: 812.0)); p.addCurve(to: CGPoint(x: 348.0, y: 1078.0), control1: CGPoint(x: 210.0, y: 978.0), control2: CGPoint(x: 268.0, y: 1040.0)); p.addCurve(to: CGPoint(x: 424.0, y: 1125.0), control1: CGPoint(x: 382.0, y: 1094.0), control2: CGPoint(x: 408.0, y: 1110.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xD9D1BC), opacity: 0.7, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 1125.0)); p.addLine(to: CGPoint(x: 0.0, y: 806.0)); p.addCurve(to: CGPoint(x: 116.0, y: 950.0), control1: CGPoint(x: 56.0, y: 838.0), control2: CGPoint(x: 96.0, y: 890.0)); p.addCurve(to: CGPoint(x: 236.0, y: 1092.0), control1: CGPoint(x: 138.0, y: 1014.0), control2: CGPoint(x: 180.0, y: 1062.0)); p.addCurve(to: CGPoint(x: 284.0, y: 1125.0), control1: CGPoint(x: 258.0, y: 1104.0), control2: CGPoint(x: 274.0, y: 1114.0)); p.closeSubpath(); return p }(), color: Color(hex: 0x7F876C), opacity: 0.5, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 492.0)); p.addCurve(to: CGPoint(x: 180.0, y: 692.0), control1: CGPoint(x: 88.0, y: 538.0), control2: CGPoint(x: 152.0, y: 610.0)); p.addCurve(to: CGPoint(x: 236.0, y: 922.0), control1: CGPoint(x: 212.0, y: 784.0), control2: CGPoint(x: 192.0, y: 856.0)); p.addCurve(to: CGPoint(x: 444.0, y: 1056.0), control1: CGPoint(x: 280.0, y: 992.0), control2: CGPoint(x: 372.0, y: 1022.0)); p.addCurve(to: CGPoint(x: 692.0, y: 1125.0), control1: CGPoint(x: 522.0, y: 1094.0), control2: CGPoint(x: 602.0, y: 1104.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.5, stroke: 2, dashed: true),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 0.0, y: 712.0)); p.addCurve(to: CGPoint(x: 130.0, y: 862.0), control1: CGPoint(x: 66.0, y: 748.0), control2: CGPoint(x: 110.0, y: 802.0)); p.addCurve(to: CGPoint(x: 170.0, y: 1028.0), control1: CGPoint(x: 152.0, y: 930.0), control2: CGPoint(x: 140.0, y: 980.0)); p.addCurve(to: CGPoint(x: 324.0, y: 1116.0), control1: CGPoint(x: 204.0, y: 1082.0), control2: CGPoint(x: 270.0, y: 1100.0)); p.addCurve(to: CGPoint(x: 396.0, y: 1125.0), control1: CGPoint(x: 354.0, y: 1124.0), control2: CGPoint(x: 378.0, y: 1124.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.8, stroke: 2.4, dashed: false)
        ],
        [
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 1125.0)); p.addLine(to: CGPoint(x: 2000.0, y: 702.0)); p.addCurve(to: CGPoint(x: 1800.0, y: 822.0), control1: CGPoint(x: 1918.0, y: 722.0), control2: CGPoint(x: 1848.0, y: 764.0)); p.addCurve(to: CGPoint(x: 1662.0, y: 996.0), control1: CGPoint(x: 1746.0, y: 888.0), control2: CGPoint(x: 1730.0, y: 950.0)); p.addCurve(to: CGPoint(x: 1400.0, y: 1125.0), control1: CGPoint(x: 1590.0, y: 1046.0), control2: CGPoint(x: 1490.0, y: 1064.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xEAE3D1), opacity: 0.76, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 1125.0)); p.addLine(to: CGPoint(x: 2000.0, y: 830.0)); p.addCurve(to: CGPoint(x: 1854.0, y: 922.0), control1: CGPoint(x: 1938.0, y: 846.0), control2: CGPoint(x: 1888.0, y: 878.0)); p.addCurve(to: CGPoint(x: 1754.0, y: 1052.0), control1: CGPoint(x: 1816.0, y: 972.0), control2: CGPoint(x: 1804.0, y: 1018.0)); p.addCurve(to: CGPoint(x: 1592.0, y: 1125.0), control1: CGPoint(x: 1700.0, y: 1090.0), control2: CGPoint(x: 1636.0, y: 1100.0)); p.closeSubpath(); return p }(), color: Color(hex: 0xDCD4C0), opacity: 0.68, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 1125.0)); p.addLine(to: CGPoint(x: 2000.0, y: 938.0)); p.addCurve(to: CGPoint(x: 1898.0, y: 1008.0), control1: CGPoint(x: 1958.0, y: 950.0), control2: CGPoint(x: 1922.0, y: 974.0)); p.addCurve(to: CGPoint(x: 1826.0, y: 1102.0), control1: CGPoint(x: 1870.0, y: 1046.0), control2: CGPoint(x: 1862.0, y: 1080.0)); p.addCurve(to: CGPoint(x: 1758.0, y: 1125.0), control1: CGPoint(x: 1798.0, y: 1118.0), control2: CGPoint(x: 1776.0, y: 1122.0)); p.closeSubpath(); return p }(), color: Color(hex: 0x9AA086), opacity: 0.46, stroke: 0, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 638.0)); p.addCurve(to: CGPoint(x: 1814.0, y: 700.0), control1: CGPoint(x: 1930.0, y: 668.0), control2: CGPoint(x: 1872.0, y: 662.0)); p.addCurve(to: CGPoint(x: 1676.0, y: 852.0), control1: CGPoint(x: 1748.0, y: 742.0), control2: CGPoint(x: 1728.0, y: 808.0)); p.addCurve(to: CGPoint(x: 1484.0, y: 942.0), control1: CGPoint(x: 1620.0, y: 900.0), control2: CGPoint(x: 1542.0, y: 900.0)); p.addCurve(to: CGPoint(x: 1346.0, y: 1125.0), control1: CGPoint(x: 1422.0, y: 986.0), control2: CGPoint(x: 1380.0, y: 1054.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.8, stroke: 2.4, dashed: false),
            RawPath(path: { var p = Path(); p.move(to: CGPoint(x: 2000.0, y: 784.0)); p.addCurve(to: CGPoint(x: 1864.0, y: 828.0), control1: CGPoint(x: 1948.0, y: 804.0), control2: CGPoint(x: 1906.0, y: 800.0)); p.addCurve(to: CGPoint(x: 1764.0, y: 940.0), control1: CGPoint(x: 1816.0, y: 858.0), control2: CGPoint(x: 1802.0, y: 906.0)); p.addCurve(to: CGPoint(x: 1624.0, y: 1004.0), control1: CGPoint(x: 1724.0, y: 974.0), control2: CGPoint(x: 1666.0, y: 976.0)); p.addCurve(to: CGPoint(x: 1524.0, y: 1125.0), control1: CGPoint(x: 1578.0, y: 1036.0), control2: CGPoint(x: 1548.0, y: 1084.0)); return p }(), color: Color(hex: 0xC9A961), opacity: 0.45, stroke: 2, dashed: true)
        ]
    ]
}
