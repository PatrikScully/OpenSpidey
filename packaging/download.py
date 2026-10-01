"""Download only the checksum-pinned build inputs in versions.json."""
import argparse
import hashlib
import json
from pathlib import Path
import urllib.request


def download(name, destination):
    versions = json.loads(Path(__file__).with_name("versions.json").read_text())
    entry = versions[name]
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    target = destination / entry["url"].rsplit("/", 1)[1]
    if not target.is_file() or hashlib.sha256(target.read_bytes()).hexdigest() != entry["sha256"]:
        temporary = target.with_name(target.name + ".part")
        try:
            urllib.request.urlretrieve(entry["url"], temporary)
            if hashlib.sha256(temporary.read_bytes()).hexdigest() != entry["sha256"]:
                raise RuntimeError("Download checksum does not match: " + entry["url"])
            temporary.replace(target)
        finally:
            temporary.unlink(missing_ok=True)
    return target


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("name", choices=("sdl", "sdl_mingw", "ffmpeg"))
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    print(download(args.name, args.destination))
