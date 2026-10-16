# README media

- `converter.<language>.png`, `dark.<language>.png` and `map.<language>.png` are actual MorseCore v0.7.4 screenshots captured during release UI checks for `en`, `tr`, `de`, `ru` and `ar`. Navigation and sample conversion text match each README. They use isolated test messages and station records.
- README captures omit the custom title bar to avoid native-caption readback artifacts; the application content pixels are unchanged. Map markers use supplied example locators, never callsign geolocation.
- `morsecore.gif` is a decorative Morse signal animation, generated locally with `python scripts/generate-readme-banner.py`. It is not an application recording. Regenerating it requires Pillow; viewing the README does not.
- `wiki/{audio,learning,radio}.{en,tr}.png` are unmodified macOS 0.7.5 SwiftUI render captures from the tested Apple Silicon app, using isolated sample messages and a synthetic station. They illustrate the corresponding Wiki guides; Windows has its own native layout.

Screenshots and animation use MorseCore's cream and green palette. All assets are stored in the repository, without an external animation service.

The animated banner contains only the language-neutral product name **MorseCore**. Its caption and download badges are localized in each README.
