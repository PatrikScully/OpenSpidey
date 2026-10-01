"""Require release notes and a changelog entry for the requested tag."""
from pathlib import Path
import re
import sys

root = Path(__file__).resolve().parent.parent
version = sys.argv[1]
if not re.fullmatch(r'v[0-9]+\.[0-9]+\.[0-9]+(?:[.-][A-Za-z0-9.-]+)?', version):
    raise SystemExit('Use a version tag such as v0.0.3.')
notes = (root / '.github/release-notes.md').read_text(encoding='utf-8')
if notes.splitlines()[0] != '# OpenSpidey ' + version:
    raise SystemExit('Update .github/release-notes.md for ' + version + ' before publishing.')
changelog = (root / 'CHANGELOG.md').read_text(encoding='utf-8')
if not re.search(r'^## ' + re.escape(version) + r'(?:\s|,|$)', changelog, re.MULTILINE):
    raise SystemExit('Add the changelog entry for ' + version + ' before publishing.')
print('Release notes and changelog match ' + version + '.')
