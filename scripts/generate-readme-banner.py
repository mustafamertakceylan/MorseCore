"""Render the self-contained Morse signal animation used in both READMEs."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "docs" / "media" / "morsecore.gif"
CREAM, GREEN, MUTED, LINE = "#faf8f2", "#5e6a42", "#b4baa6", "#e6e3da"
WIDTH, HEIGHT = 1040, 232
try:
    font = "C:/Windows/Fonts/georgia.ttf" if Path("C:/Windows/Fonts/georgia.ttf").exists() else "DejaVuSerif.ttf"
    title = ImageFont.truetype(font, 62)
except OSError:
    title = ImageFont.load_default(size=62)
signal = "-- --- .-. ... . / -.-. --- .-. ."
segments = []
x = 0
for character in signal:
    size = {".": 8, "-": 22, " ": 12, "/": 24}[character]
    if character in ".-":
        segments.append((x, size))
    x += size + 6
start = (WIDTH - x) // 2
frames = []
for active in range(len(segments) + 6):
    frame = Image.new("RGB", (WIDTH, HEIGHT), CREAM)
    draw = ImageDraw.Draw(frame)
    draw.line((120, 20, WIDTH - 120, 20), fill=LINE, width=1)
    draw.text((WIDTH // 2, 102), "MorseCore", font=title, fill=GREEN, anchor="mm")
    for index, (position, size) in enumerate(segments):
        draw.rounded_rectangle(
            (start + position, 172, start + position + size, 180),
            radius=4,
            fill=GREEN if index == active else MUTED,
        )
    frames.append(frame)
OUTPUT.parent.mkdir(parents=True, exist_ok=True)
frames[0].save(OUTPUT, save_all=True, append_images=frames[1:], duration=150, loop=0, optimize=True)
print(f"{OUTPUT}: {OUTPUT.stat().st_size:,} bytes, {len(frames)} frames")
