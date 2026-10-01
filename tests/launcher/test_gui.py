"""Exercise real Tk widgets and the import worker under a display server."""

import os
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "platform/launcher"))
import config
import launcher


@unittest.skipUnless(os.environ.get("DISPLAY") or os.name == "nt", "A display server is required")
class LauncherGuiTests(unittest.TestCase):
    def setUp(self):
        import tkinter as tk
        self.temp = tempfile.TemporaryDirectory()
        self.root = tk.Tk()
        self.path = Path(self.temp.name)
        self.window = launcher.SetupWindow(self.root, dict(config.DEFAULTS), self.path / "settings.json", self.path)
        self.root.update()

    def tearDown(self):
        try:
            self.root.destroy()
        except self.window.tk.TclError:
            pass
        self.temp.cleanup()
