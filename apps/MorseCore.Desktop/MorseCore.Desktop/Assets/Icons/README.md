# Application icon

`icon.svg` embeds the selected bitmap recolor of the existing MorseCore mark and clips it to a rounded tile with transparent outer corners. `icon_foreground.svg` deliberately contains no artwork: an opaque foreground would cover those corners again. Uno Resizetizer builds the Windows icon and executable resource from these sources.

Artwork was edited with the built-in ImageGen tool, using the existing `../Brand/morsecore.png` as the edit target. Final prompt: preserve the wordless MorseCore mark and geometry; cream mark on olive green; flat, clean colors; no text, shadows or extra shapes. The requested palette was RAW olive `#5E6A42` and cream `#FAF8F2`; generated bitmap colors may vary slightly. The rounded clipping and padding are native SVG code.

The original brand image remains the source for the in-app logo and splash screen.
