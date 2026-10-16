import SwiftUI

struct RadioMapView: View {
    let settings: AppSettings
    let outline: ServiceJSON
    let map: ServiceJSON
    @Environment(\.colorScheme) private var scheme
    private var theme: BrandTheme { BrandTheme(dark: scheme == .dark) }
    private var points: [RadioMapPoint] { map["entries"].arrayValue.compactMap(RadioMapPoint.init) }
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(settings.text("logbook.map")).font(BrandTheme.heading(26))
            Text("\(settings.text("logbook.located")): \(map["located"].intValue)/\(map["total"].intValue) · \(settings.text("logbook.shown")): \(points.count)").font(.caption).foregroundStyle(theme.muted)
            Text(settings.text("logbook.mapHint")).font(.caption).foregroundStyle(theme.muted)
            GeometryReader { geometry in
                ZStack(alignment: .topLeading) {
                    Canvas { context, size in
                        let rings = outline["rings"].arrayValue
                        for ring in rings {
                            var path = Path()
                            let coordinates = ring.arrayValue
                            for (index, coordinate) in coordinates.enumerated() {
                                let pair = coordinate.arrayValue
                                guard pair.count == 2 else { continue }
                                let point = project(pair[0].doubleValue, pair[1].doubleValue, size)
                                if index == 0 { path.move(to: point) } else { path.addLine(to: point) }
                            }
                            path.closeSubpath()
                            context.fill(path, with: .color(theme.pane.opacity(0.65)))
                            context.stroke(path, with: .color(theme.line), lineWidth: 0.7)
                        }
                        for longitude in stride(from: -180, through: 180, by: 60) {
                            var line = Path(); line.move(to: project(Double(longitude), 90, size)); line.addLine(to: project(Double(longitude), -90, size))
                            context.stroke(line, with: .color(theme.line.opacity(0.55)), lineWidth: 0.7)
                            context.draw(Text("\(longitude)°").font(.system(size: 9)).foregroundStyle(theme.muted), at: CGPoint(x: min(max(project(Double(longitude), 0, size).x + 3, 15), size.width - 15), y: size.height - 10))
                        }
                        for latitude in stride(from: -60, through: 60, by: 30) {
                            var line = Path(); line.move(to: project(-180, Double(latitude), size)); line.addLine(to: project(180, Double(latitude), size))
                            context.stroke(line, with: .color(theme.line.opacity(latitude == 0 ? 1 : 0.55)), lineWidth: 0.7)
                            context.draw(Text("\(latitude)°").font(.system(size: 9)).foregroundStyle(theme.muted), at: CGPoint(x: 15, y: project(0, Double(latitude), size).y + 7))
                        }
                    }.accessibilityHidden(true)
                    ForEach(points) { point in
                        Circle().fill(theme.accent).overlay(Circle().stroke(theme.ink.opacity(0.65), lineWidth: 1)).frame(width: 9, height: 9)
                            .position(clamp(project(point.longitude, point.latitude, geometry.size), geometry.size))
                            .help(point.caption).accessibilityElement(children: .ignore).accessibilityLabel(point.caption)
                    }
                }
            }.frame(height: 300).background(theme.input, in: RoundedRectangle(cornerRadius: 12)).clipShape(RoundedRectangle(cornerRadius: 12)).environment(\.layoutDirection, .leftToRight)
            if let url = URL(string: outline["licenseUrl"].stringValue), url.scheme == "https" {
                Link(settings.text("logbook.mapCredit"), destination: url).font(.caption)
            }
            ForEach(Array(points.prefix(10))) { point in Text(point.caption).font(.caption).foregroundStyle(theme.muted).textSelection(.enabled).environment(\.layoutDirection, .leftToRight) }
        }
    }
    private func project(_ lon: Double, _ lat: Double, _ size: CGSize) -> CGPoint { CGPoint(x: (lon + 180) / 360 * size.width, y: (90 - lat) / 180 * size.height) }
    private func clamp(_ point: CGPoint, _ size: CGSize) -> CGPoint { CGPoint(x: min(max(point.x, 4.5), max(4.5, size.width - 4.5)), y: min(max(point.y, 4.5), max(4.5, size.height - 4.5))) }
}

private struct RadioMapPoint: Identifiable {
    let id: String
    let latitude: Double
    let longitude: Double
    let call: String
    let grid: String
    var caption: String { call + " · " + radioNumber(latitude) + ", " + radioNumber(longitude) + " · " + grid }
    init?(_ record: ServiceJSON) {
        id = record.radioID; call = record["otherCall"].stringValue; grid = record["gridSquare"].stringValue
        if case .number(let lat) = record["latitude"], case .number(let lon) = record["longitude"], lat.isFinite, lon.isFinite, (-90...90).contains(lat), (-180...180).contains(lon) {
            latitude = lat; longitude = lon
        } else { return nil }
    }
}
