#!/usr/bin/env python3
"""Check that the module manifest covers source files and includes are canonical."""
from pathlib import Path
import re
import sys


def check_source_structure(root: Path) -> list[str]:
    errors = []
    source_root = root / "src"
    manifest = (root / "cmake" / "GameSources.cmake").read_text()
    listed = set(re.findall(r"src/[A-Za-z0-9_/]+\.cpp", manifest))
    actual = {path.relative_to(root).as_posix() for path in source_root.rglob("*.cpp")}
    for path in sorted(actual - listed):
        errors.append(f"Source missing from module manifest: {path}")
    for path in sorted(listed - actual):
        errors.append(f"Manifest source does not exist: {path}")
    for path in root.iterdir():
        if path.suffix in (".cpp", ".h"):
            errors.append(f"Source file outside a system directory: {path.name}")

    for directory in (source_root, root / "platform", root / "tests"):
        for path in sorted(directory.rglob("*")):
            if path.suffix not in (".cpp", ".h"):
                continue
            for include in re.findall(r'^\s*#include\s+"([^\"]+)"', path.read_text(), re.MULTILINE):
                # Local backend headers and generated launcher/version headers
                # may legitimately sit alongside their consumer.
                if (path.parent / include).is_file():
                    continue
                if (source_root / include).is_file() or (root / include).is_file():
                    continue
                errors.append(f"Unresolved project include in {path.relative_to(root)}: {include}")
    return errors


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    errors = check_source_structure(root)
    if errors:
        print("\n".join(errors), file=sys.stderr)
        return 1
    print("Source manifest and project includes are consistent.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
