"""Copy notices from the exact self-contained .NET runtime package in use."""

import json
import os
import shutil
import sys
from pathlib import Path


def copy_notices(helper: Path, rid: str, assets: Path) -> None:
    config = json.loads((helper / "MorseCore.MacService.runtimeconfig.json").read_text())
    frameworks = config["runtimeOptions"]["includedFrameworks"]
    version = next(item["version"] for item in frameworks if item["name"] == "Microsoft.NETCore.App")
    roots = []
    if assets.is_file():
        roots += [Path(folder) for folder in json.loads(assets.read_text())["packageFolders"]]
    if os.environ.get("NUGET_PACKAGES"):
        roots.append(Path(os.environ["NUGET_PACKAGES"]))
    roots.append(Path(os.environ.get("DOTNET_CLI_HOME", str(Path.home()))) / ".nuget/packages")
    package = "microsoft.netcore.app.runtime." + rid
    candidates = [root / package / version for root in roots]
    for candidate in candidates:
        license_file = candidate / "LICENSE.TXT"
        notices = candidate / "THIRD-PARTY-NOTICES.TXT"
        if license_file.is_file() and notices.is_file():
            shutil.copyfile(license_file, helper / "LICENSE.txt")
            shutil.copyfile(notices, helper / "ThirdPartyNotices.txt")
            (helper / "runtime-notices-version.txt").write_text(version + "\n")
            print(f"Bundled .NET {version} license and third-party notices.")
            return
    # Explicit reuse is permitted only when the matching publish already included
    # its notices; normal builds locate them through their freshly restored assets.
    marker = helper / "runtime-notices-version.txt"
    if marker.is_file() and marker.read_text().strip() == version and all(
        (helper / name).is_file() and (helper / name).stat().st_size > 0
        for name in ("LICENSE.txt", "ThirdPartyNotices.txt")
    ):
        return
    raise SystemExit(f"Missing license/notices for {package} {version}; restore/publish the runtime package before bundling.")


if __name__ == "__main__":
    if len(sys.argv) != 4:
        raise SystemExit("usage: copy-runtime-notices.py HELPER_DIR RID PROJECT_ASSETS_JSON")
    copy_notices(Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3]))
