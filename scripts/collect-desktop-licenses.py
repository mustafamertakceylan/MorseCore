"""Collect notices for the dependencies actually present in a desktop publish."""
import argparse
import json
import re
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path


def collect(publish, packages):
    root = Path(__file__).resolve().parents[1]
    supplemental = root / "third_party" / "runtime"
    output = publish / "licenses"
    output.mkdir(parents=True, exist_ok=True)
    inventory = []
    libraries = json.loads((publish / "MorseCore.Desktop.deps.json").read_text(encoding="utf-8"))["libraries"]
    for identity, library in sorted(libraries.items()):
        if identity.startswith("MorseCore."):
            continue
        if not re.fullmatch(r"[A-Za-z0-9_.+-]+/[A-Za-z0-9_.+-]+", identity):
            raise ValueError("Unexpected dependency identity: " + identity)
        name, version = identity.removeprefix("runtimepack.").split("/")
        if name == "Microsoft.Web.WebView2.Core.Projection":
            inventory.append({"name": name, "version": version, "license_source": "Microsoft.Web.WebView2/" + version})
            continue
        package = packages / name.lower() / version.lower()
        metadata_files = [path for path in package.iterdir()
                          if path.is_file() and path.name.lower() == name.lower() + ".nuspec"]
        if len(metadata_files) != 1:
            raise ValueError("Missing or ambiguous package metadata for " + identity)
        metadata_file = metadata_files[0]
        metadata = ET.parse(metadata_file).find("{*}metadata")
        value = lambda tag: (metadata.findtext("{*}" + tag) or "").strip()
        license_element = metadata.find("{*}license")
        expression = license_element.text if license_element is not None else ""
        destination = output / "runtime" / name / version
        destination.mkdir(parents=True, exist_ok=True)
        copied = []
        for source in package.rglob("*"):
            if source.is_file() and re.search(r"license|notice|copying|copyright", source.name, re.I):
                relative = source.relative_to(package)
                target = destination / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, target)
                copied.append(relative.as_posix())
        shutil.copy2(metadata_file, destination / metadata_file.name)
        # Some publishers supply SPDX metadata rather than a complete license file.
        if expression == "MIT":
            (destination / "LICENSE-MIT.txt").write_text(value("copyright") + "\n\n" + (supplemental / "MIT.txt").read_text(encoding="utf-8"), encoding="utf-8")
            copied.append("LICENSE-MIT.txt")
        elif expression == "Apache-2.0":
            shutil.copy2(root / "LICENSE", destination / "LICENSE-Apache-2.0.txt")
            copied.append("LICENSE-Apache-2.0.txt")
        elif name == "CommonServiceLocator" and version == "2.0.5":
            shutil.copy2(supplemental / "CommonServiceLocator-LICENSE", destination / "LICENSE-MS-PL.txt")
            copied.append("LICENSE-MS-PL.txt")
        elif name == "Microsoft.Windows.SDK.NET.Ref":
            shutil.copy2(supplemental / "Windows-SDK-LICENSE.rtf", destination / "LICENSE.rtf")
            copied.append("LICENSE.rtf")
        elif name == "LibVLCSharp" and version == "3.9.7.1" and expression == "LGPL-2.1-or-later":
            shutil.copy2(supplemental / "LibVLCSharp-LICENSE", destination / "LICENSE-LGPL-2.1.txt")
            copied.append("LICENSE-LGPL-2.1.txt")
        elif name == "Uno.Diagnostics.Eventing" and version == "2.0.1":
            shutil.copy2(supplemental / "Uno.Diagnostics.Eventing-LICENSE.md", destination / "LICENSE-Apache-2.0.md")
            copied.append("LICENSE-Apache-2.0.md")
        elif not any("license" in path.lower() or "copying" in path.lower() for path in copied):
            raise ValueError("No complete license collected for " + identity)
        if name.startswith("Uno."):
            shutil.copy2(supplemental / "Uno-THIRD-PARTY-NOTICES.md", destination / "Uno-THIRD-PARTY-NOTICES.md")
            copied.append("Uno-THIRD-PARTY-NOTICES.md")
        repository = metadata.find("{*}repository")
        inventory.append({"name": name, "version": version, "authors": value("authors"), "copyright": value("copyright"),
                          "license": expression or value("licenseUrl"), "repository": dict(repository.attrib) if repository is not None else {},
                          "files": copied})
    (output / "dependencies.json").write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    shutil.copy2(supplemental / "README.md", output / "SOURCES.md")
    print(f"Collected license records for {len(inventory)} desktop dependencies.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("publish", type=Path)
    parser.add_argument("packages", type=Path)
    args = parser.parse_args()
    collect(args.publish.resolve(), args.packages.resolve())
