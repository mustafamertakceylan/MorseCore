"""Validate canonical Wiki sources and render GitHub Wiki Markdown."""

import argparse
import json
import re
from pathlib import Path
from urllib.parse import quote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs/wiki"
REPO = "https://github.com/mustafamertakceylan/MorseCore"
WIKI = REPO + "/wiki"
RAW = "https://raw.githubusercontent.com/mustafamertakceylan/MorseCore/main"
LINK = re.compile(r"(!?\[[^\]\n]*\]\()([^\s)]+)(\))")


def render():
    pages = json.loads((SOURCE / "pages.json").read_text(encoding="utf-8"))
    version = re.search(r"project\(MorseCore VERSION (\d+\.\d+\.\d+)", (ROOT / "CMakeLists.txt").read_text()).group(1)
    release = REPO + "/releases/download/v" + version
    values = {
        "VERSION": version, "REPO_URL": REPO, "MEDIA_URL": RAW + "/docs/media",
        "RELEASE_URL": REPO + "/releases/tag/v" + version,
        "WINDOWS_SETUP_URL": release + f"/MorseCore-{version}-Windows-x64-Setup.exe",
        "WINDOWS_ZIP_URL": release + f"/MorseCore-{version}-Windows-x64-Desktop.zip",
        "MAC_DMG_URL": release + f"/MorseCore-{version}-macOS-arm64.dmg",
    }
    by_source, by_slug = {}, {}
    for page in pages:
        slug, source = page["slug"], (SOURCE / page["source"]).resolve()
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9-]*", slug) or slug.casefold() in by_slug:
            raise ValueError("Invalid or duplicate Wiki slug: " + slug)
        if not source.is_relative_to(SOURCE) or source in by_source or not source.is_file():
            raise ValueError("Invalid Wiki source: " + str(source))
        by_source[source] = page
        by_slug[slug.casefold()] = page
    result = {}
    images = set()
    for page in pages:
        source = (SOURCE / page["source"]).resolve()
        body = source.read_text(encoding="utf-8")
        for name, value in values.items():
            body = body.replace("{{" + name + "}}", value)
        if "{{" in body or "}}" in body:
            raise ValueError("Unresolved Wiki placeholder: " + str(source))

        def rewrite(match):
            start, address, end = match.groups()
            split = urlsplit(address)
            if split.scheme or split.netloc:
                if split.scheme != "https" or not split.netloc:
                    raise ValueError("Wiki links must use HTTPS: " + address)
                if address.startswith(RAW + "/"):
                    asset = ROOT / address[len(RAW) + 1:]
                    if not asset.is_file():
                        raise ValueError("Missing Wiki asset: " + str(asset))
                    if start.startswith("!"):
                        images.add(asset.relative_to(ROOT).as_posix())
                return match.group(0)
            if not split.path:
                return match.group(0)
            target = (source.parent / split.path).resolve()
            if not target.is_relative_to(ROOT) or not target.is_file():
                raise ValueError("Broken Wiki source link: " + address + " in " + str(source))
            if target in by_source:
                url = WIKI + "/" + by_source[target]["slug"]
            else:
                url = REPO + "/blob/main/" + quote(target.relative_to(ROOT).as_posix(), safe="/")
            if split.fragment:
                url += "#" + split.fragment
            return start + url + end

        body = LINK.sub(rewrite, body)
        # HTML is limited to the Home banner; validate its image too.
        for address in re.findall(r'<img\s+[^>]*src="([^"]+)"', body):
            if not address.startswith(RAW + "/") or not (ROOT / address[len(RAW) + 1:]).is_file():
                raise ValueError("Invalid Wiki HTML image: " + address)
            images.add(address[len(RAW) + 1:])
        if page["language"] != "both":
            peer = by_slug.get(page["peer"].casefold())
            if not peer or peer.get("peer") != page["slug"] or peer["language"] == page["language"]:
                raise ValueError("Broken Wiki language pair: " + page["slug"])
            switch = f"**Türkçe** · [English]({WIKI}/{peer['slug']})" if page["language"] == "tr" else f"[Türkçe]({WIKI}/{peer['slug']}) · **English**"
            body = switch + "\n\n" + body
        relative = source.relative_to(ROOT).as_posix()
        result[page["slug"] + ".md"] = f"<!-- Generated from {relative}; edit the repository source. -->\n\n" + body.rstrip() + "\n"
    sidebar = [f"[**MorseCore Wiki**]({WIKI}/Home)", ""]
    for language, title in (("tr", "Türkçe"), ("en", "English")):
        sidebar += ["### " + title, ""]
        sidebar += [f"- [{page['title']}]({WIKI}/{page['slug']})" for page in pages if page["language"] == language]
        sidebar.append("")
    result["_Sidebar.md"] = "\n".join(sidebar)
    result["_Footer.md"] = f"[MorseCore]({REPO}) · [İndir / Download]({values['RELEASE_URL']}) · [Hata bildir / Report an issue]({REPO}/issues) · [Apache 2.0]({REPO}/blob/main/LICENSE)\n"
    return result, images


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "build/wiki")
    parser.add_argument("--check", action="store_true", help="Validate sources without writing generated pages.")
    args = parser.parse_args()
    pages, images = render()
    if not args.check:
        output = args.output.resolve()
        if not any(output.is_relative_to(ROOT / name) for name in ("build", "cmake-build-agent-work")):
            raise ValueError("Output must be a dedicated build or Wiki checkout directory in this repository.")
        output.mkdir(parents=True, exist_ok=True)
        for name, content in pages.items():
            (output / name).write_text(content, encoding="utf-8", newline="\n")
    print(f"Validated {len(pages) - 2} Wiki pages, {len(images)} images and every internal source/language link; sidebar and footer ready.")


if __name__ == "__main__":
    main()
