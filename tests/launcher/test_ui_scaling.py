"""Check layout, keyboard access and display mode behavior with real Tk widgets."""

import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "platform/launcher"))
import config
import launcher


@unittest.skipUnless(os.environ.get("DISPLAY") or os.name == "nt", "A display server is required")
class LauncherLayoutTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.path = Path(self.temp.name)
        self.roots = []



if __name__ == "__main__":
    unittest.main()
