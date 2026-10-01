"""Download the Ubuntu source packages used by the Linux runtime."""
import json
from pathlib import Path
import subprocess
import sys

manifest = json.loads(Path(sys.argv[1]).read_text())
for package, version in manifest['source_packages'].items():
    subprocess.run(['apt-get', '-o', 'APT::Sandbox::User=root',
                    '-o', 'Acquire::http::Timeout=30', '-o', 'Acquire::Retries=3',
                    'source', '--download-only', package + '=' + version], check=True)
Path('runtime-packages.json').write_text(json.dumps(manifest, indent=2) + '\n')
