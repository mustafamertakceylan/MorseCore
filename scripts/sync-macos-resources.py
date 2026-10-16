"""Generate macOS copies from the shared strings and Windows brand assets."""

import argparse
import json
import re
import shutil
from pathlib import Path


def sync(check=False):
    root = Path(__file__).resolve().parents[1]
    shared = root / "apps/MorseCore.AppCore/Strings"
    assets = root / "apps/MorseCore.Desktop/MorseCore.Desktop/Assets"
    destination = root / "apps/MorseCore.macOS/Sources/MorseCoreMac/Resources"
    errors = []
    # ADIF ranges are application data, not a second hand-maintained Swift table.
    band_source = (root / "apps/MorseCore.AppCore/Radio/LogQuery.cs").read_text(encoding="utf-8-sig")
    band_section = band_source.split("[] Values=", 1)[1].split("];", 1)[0]
    bands = [{"name": name, "low": int(low), "high": int(high)}
             for name, low, high in re.findall(r'\("([^"]+)",(\d+),(\d+)\)', band_section)]
    if len(bands) < 30 or len({band["name"] for band in bands}) != len(bands):
        raise ValueError("Invalid shared ADIF band source.")
    band_target = destination / "adif-bands.json"
    if check:
        if not band_target.is_file() or json.loads(band_target.read_text(encoding="utf-8-sig")) != bands:
            errors.append(str(band_target.relative_to(root)))
    else:
        band_target.parent.mkdir(parents=True, exist_ok=True)
        band_target.write_text(json.dumps(bands, indent=2) + "\n", encoding="utf-8")
    for language in ("tr", "en", "de", "ru", "ar"):
        values = {}
        for source in sorted(shared.glob("*.json")):
            if source.name != language + ".json" and not source.name.endswith("." + language + ".json"):
                continue
            section = json.loads(source.read_text(encoding="utf-8-sig"))
            for key, value in section.items():
                if key in values or not isinstance(value, str):
                    raise ValueError(f"Duplicate or non-string translation: {source.name}: {key}")
                values[key] = value
        if not values:
            raise ValueError("Missing shared language: " + language)
        target = destination / "Strings" / (language + ".json")
        if check:
            if not target.is_file() or json.loads(target.read_text(encoding="utf-8-sig")) != values:
                errors.append(str(target.relative_to(root)))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(json.dumps(values, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")
    copies = [(assets / "Brand/morsecore.png", destination / "morsecore.png")]
    copies.extend((source, destination / "Fonts" / source.name) for source in sorted((assets / "Fonts").iterdir()) if source.is_file())
    for source, target in copies:
        if check:
            if not target.is_file() or source.read_bytes() != target.read_bytes():
                errors.append(str(target.relative_to(root)))
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
    if errors:
        raise SystemExit("Stale macOS resources; run python scripts/sync-macos-resources.py:\n" + "\n".join(errors))
    print("Shared macOS resources are current." if check else "Generated macOS resources from shared sources.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Fail on drift without writing files.")
    sync(parser.parse_args().check)
